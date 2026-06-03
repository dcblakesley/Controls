namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the UI Kit gallery page (/uikit). It is a standalone route (not the form-demo
/// view switcher), so this test drives the page directly rather than via PageTestBase.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class UiKitGalleryE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public UiKitGalleryE2ETests(AppFixture app, BrowserFixture browser)
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

    async Task GotoAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Gallery_renders_core_controls()
    {
        await GotoAsync();
        await Expect(_page.Locator(".wss-alert").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-table").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-pagination").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Modal_opens_on_button_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        await Expect(_page.Locator(".wss-modal")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Alert_section_visual_baseline()
    {
        await GotoAsync();
        var alertSection = _page.Locator("section.demo-section").First; // first section is Alert
        await Expect(alertSection).ToBeVisibleAsync();
        var bytes = await alertSection.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-alert-section");
    }
}
