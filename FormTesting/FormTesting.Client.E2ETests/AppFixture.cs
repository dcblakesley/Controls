using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// Launches the FormTesting Blazor Server host on a free local port for the duration of the test
/// run. Disposed at the end via xUnit's <see cref="IAsyncLifetime"/>.
/// </summary>
/// <remarks>
/// <para>
/// The host is launched out-of-process via <c>dotnet run</c> rather than in-process via
/// <c>WebApplicationFactory</c> because the demo pages render under
/// <c>InteractiveWebAssembly</c> rendermode — they need a real Kestrel + WASM bundle download,
/// which <c>WebApplicationFactory</c>'s TestServer pipeline doesn't fully support.
/// </para>
/// <para>
/// Used as an xUnit <c>ICollectionFixture</c> via <see cref="PlaywrightCollection"/> so a single
/// host process is shared across every e2e test class — saves ~10s of WASM-bundle download time
/// per class.
/// </para>
/// </remarks>
public class AppFixture : IAsyncLifetime
{
    Process? _process;
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var port = FindFreeLocalPort();
        BaseUrl = $"http://127.0.0.1:{port}";

        // FORMTESTING_E2E_APP: full path to a published FormTesting.dll. When set, the fixture
        // launches that published output instead of `dotnet run` on the project — used to run this
        // suite against a trimmed Release publish (trim-safety verification). The working directory
        // must be the publish folder so wwwroot/static web assets resolve.
        var publishedApp = Environment.GetEnvironmentVariable("FORMTESTING_E2E_APP");
        var arguments = publishedApp is null
            ? $"run --project \"{LocateFormTestingProject()}\" --no-build --no-launch-profile --urls {BaseUrl}"
            : $"\"{publishedApp}\" --urls {BaseUrl}";

        _process = new Process
        {
            StartInfo =
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = publishedApp is null ? string.Empty : Path.GetDirectoryName(publishedApp)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Development",
                    // Silence HTTPS redirect — we're using http:// for tests, the redirect would force the browser away.
                    ["ASPNETCORE_HTTPS_PORTS"] = "",
                },
            },
            EnableRaisingEvents = true,
        };

        _process.Start();
        // Drain stdout/stderr so the buffer doesn't fill and block the process.
        _ = Task.Run(() => DrainStream(_process.StandardOutput));
        _ = Task.Run(() => DrainStream(_process.StandardError));

        await WaitForAppReadyAsync(BaseUrl, TimeSpan.FromSeconds(60));
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            try { _process.Kill(entireProcessTree: true); } catch { /* best-effort */ }
            _process.WaitForExit(TimeSpan.FromSeconds(10));
        }
        _process?.Dispose();
        return Task.CompletedTask;
    }

    static int FindFreeLocalPort()
    {
        // Listener-then-release is the canonical ephemeral-port trick. There's a tiny race window
        // between us releasing and Kestrel binding, but it's vanishingly rare on developer machines.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static string LocateFormTestingProject()
    {
        // The test binary lives at .../FormTesting.Client.E2ETests/bin/Debug/net10.0/.
        // Walk up until we find FormTesting.sln, then resolve the host project path from there.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FormTesting.sln")))
            dir = dir.Parent;
        if (dir == null)
            throw new DirectoryNotFoundException("Could not locate FormTesting.sln walking up from test bin folder.");
        return Path.Combine(dir.FullName, "FormTesting", "FormTesting", "FormTesting.csproj");
    }

    static async Task WaitForAppReadyAsync(string baseUrl, TimeSpan timeout)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + timeout;
        Exception? last = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var resp = await client.GetAsync(baseUrl);
                if (resp.IsSuccessStatusCode || resp.StatusCode == HttpStatusCode.Redirect)
                    return;
            }
            catch (Exception ex) { last = ex; }
            await Task.Delay(500);
        }
        throw new TimeoutException($"FormTesting host at {baseUrl} did not become ready within {timeout}.", last);
    }

    static async Task DrainStream(StreamReader reader)
    {
        try { while (await reader.ReadLineAsync() is not null) { /* discard */ } }
        catch { /* fixture is shutting down */ }
    }
}
