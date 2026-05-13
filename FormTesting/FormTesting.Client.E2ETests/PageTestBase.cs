using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// Base class for all per-control e2e tests. Wires up the shared <see cref="AppFixture"/> +
/// <see cref="BrowserFixture"/>, gives each test method a fresh <see cref="IBrowserContext"/> +
/// <see cref="IPage"/>, and exposes helpers for navigating to a specific demo view + waiting
/// for Blazor WebAssembly to hydrate.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public abstract class PageTestBase : IAsyncLifetime
{
    protected readonly AppFixture App;
    protected readonly BrowserFixture Browser;

    protected IBrowserContext Context { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;

    /// <summary>
    /// The <see cref="CurrentView"/> this test class targets. Used by <see cref="NavigateAsync"/>
    /// to build the demo URL.
    /// </summary>
    protected abstract CurrentView View { get; }

    protected PageTestBase(AppFixture app, BrowserFixture browser)
    {
        App = app;
        Browser = browser;
    }

    public async Task InitializeAsync()
    {
        // Each test gets a fresh context — independent cookies, storage, and page state.
        Context = await Browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Fixed viewport so screenshot baselines are deterministic across machines.
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            // Reduces noise from font/anti-aliasing differences between machines.
            DeviceScaleFactor = 1,
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
    }

    /// <summary>
    /// Navigate to this test class's demo view and wait for Blazor WebAssembly to hydrate enough
    /// that controls are interactive. Use this at the start of each test rather than ad-hoc
    /// <c>Page.GotoAsync</c> calls.
    /// </summary>
    protected async Task NavigateAsync()
    {
        await Page.GotoAsync($"{App.BaseUrl}/?view={View}", new PageGotoOptions
        {
            // NetworkIdle waits until the WASM bundle has finished downloading and no further
            // network activity is happening — a reliable hydration-ready signal for this app.
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000, // first-run WASM download can be slow.
        });

        // Belt-and-suspenders: confirm the sidebar nav rendered. If this isn't visible, hydration
        // failed and downstream interactions would all retry-fail anyway.
        await Expect(Page.Locator("button", new() { HasTextString = View.ToString() }).First)
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Captures a PNG screenshot of the locator and asserts it matches the committed baseline
    /// under <c>Snapshots/&lt;TestClass&gt;-&lt;name&gt;.png</c>. Re-run with
    /// <c>UPDATE_SNAPSHOTS=1</c> to regenerate baselines after intentional UI changes.
    /// </summary>
    protected async Task ExpectMatchesBaselineAsync(ILocator locator, string name)
    {
        var bytes = await locator.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-{name}");
    }
}
