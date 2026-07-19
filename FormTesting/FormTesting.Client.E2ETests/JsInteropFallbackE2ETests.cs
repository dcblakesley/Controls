namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for JsInteropEc's cross-origin-MFE fallback: when <c>window.WssEditControls</c> is
/// missing (a host page that never linked <c>edit-controls.js</c> via a classic <c>&lt;script&gt;</c>
/// tag), <c>FocusFirstInvalidField</c> lazily imports the module and retries once, and degrades
/// quietly if that retry also fails. This needs a real browser (bUnit doesn't execute JavaScript, and
/// its strict-mode JSInterop can't simulate "the global exists but a specific function is missing").
/// Drives <c>/js-interop-fallback</c> directly (not the CurrentView-switcher gallery), following the
/// <see cref="UiKitGalleryE2ETests"/> precedent for a standalone route.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class JsInteropFallbackE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public JsInteropFallbackE2ETests(AppFixture app, BrowserFixture browser)
    {
        _app = app;
        _browser = browser;
    }

    public async Task InitializeAsync()
    {
        _context = await _browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            DeviceScaleFactor = 1,
        });
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    async Task GotoAsync(string? assetBase = null)
    {
        var url = $"{_app.BaseUrl}/js-interop-fallback";
        if (assetBase is not null) url += $"?assetBase={Uri.EscapeDataString(assetBase)}";

        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "JS Interop Fallback" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task FocusFirstInvalidField_lazily_reimports_and_focuses_when_the_global_is_missing()
    {
        await GotoAsync();

        // Simulate the cross-origin MFE case: the host page never linked edit-controls.js as a
        // classic <script> tag, so the global namespace JsInteropEc calls into doesn't exist yet.
        await _page.EvaluateAsync("() => { delete window.WssEditControls; }");

        await _page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();

        // Passes only if InvokeBestEffortAsync's JSException catch fired, lazily imported
        // edit-controls.js (default relative path -- same origin as this page), and retried.
        await Expect(_page.Locator("input.invalid")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task FocusFirstInvalidField_does_not_throw_when_the_lazy_import_404s()
    {
        // Point the re-import at a same-origin path nothing serves, so the dynamic import() rejects
        // (module 404) -- exercising the "retry also failed" branch, which must still degrade quietly
        // rather than surface an unhandled exception to the page.
        await GotoAsync(assetBase: $"{_app.BaseUrl}/definitely-not-a-real-path");
        await _page.EvaluateAsync("() => { delete window.WssEditControls; }");

        var pageErrors = new List<string>();
        _page.PageError += (_, error) => pageErrors.Add(error);

        await _page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        // Give the (failing) import + retry a moment to settle before asserting nothing blew up.
        await _page.WaitForTimeoutAsync(500);
        Assert.Empty(pageErrors);

        // The app must still be responsive afterwards -- a second click re-validates without error.
        await _page.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await _page.WaitForTimeoutAsync(200);
        Assert.Empty(pageErrors);
    }
}
