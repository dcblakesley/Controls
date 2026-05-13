namespace FormTesting.Client.E2ETests;

/// <summary>
/// Owns the shared Playwright runtime + browser instance. One Chromium process is launched for
/// the whole test run; each test gets its own <see cref="IBrowserContext"/> (via
/// <see cref="PageTestBase"/>) for isolation.
/// </summary>
public class BrowserFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = default!;
    public IBrowser Browser { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            // Headless = false during local debugging if you want to watch the tests run.
            Headless = Environment.GetEnvironmentVariable("PWTEST_HEADED") != "1",
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser != null) await Browser.CloseAsync();
        Playwright?.Dispose();
    }
}
