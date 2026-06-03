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

    [Fact]
    public async Task Drawer_opens_and_matches_baseline()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Drawer" }).ClickAsync();
        var drawer = _page.Locator(".wss-drawer");
        await Expect(drawer).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-drawer-right")).ToBeVisibleAsync();
        await BaselineAsync(drawer, "drawer");
    }

    [Fact]
    public async Task Popover_opens_and_matches_baseline()
    {
        await GotoAsync();
        await _page.Locator(".wss-popover-trigger").ClickAsync();
        var popover = _page.Locator(".wss-popover");
        await Expect(popover).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-popover-content")).ToContainTextAsync("popover content");
        await BaselineAsync(popover, "popover");
    }

    [Fact]
    public async Task Popconfirm_opens_baselines_then_confirms()
    {
        await GotoAsync();
        await _page.Locator(".wss-popconfirm-trigger").ClickAsync();
        var pop = _page.Locator(".wss-popconfirm");
        await Expect(pop).ToBeVisibleAsync();
        await BaselineAsync(pop, "popconfirm");

        // The primary button confirms, closes the popover, and records the result.
        await _page.Locator(".wss-popconfirm-buttons .wss-dialog-btn-primary").ClickAsync();
        await Expect(_page.Locator(".wss-popconfirm")).Not.ToBeVisibleAsync();
        await Expect(_page.GetByText("Last action: confirmed")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Pagination_baselines_then_changes_page()
    {
        await GotoAsync();
        // The Table also renders a pager, so scope to the standalone Pagination demo (the first one).
        var pager = _page.Locator(".wss-pagination").First;
        await BaselineAsync(pager, "pagination");

        await pager.Locator(".wss-pagination-item").Nth(2).ClickAsync(); // page 3
        await Expect(_page.GetByText("Current page: 3")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Message_toast_appears_on_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Message" }).ClickAsync();
        await Expect(_page.Locator(".wss-msg")).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-msg-content")).ToContainTextAsync("Saved!");
    }

    [Fact]
    public async Task Notification_appears_on_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Notification" }).ClickAsync();
        await Expect(_page.Locator(".wss-notification")).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-notification-message")).ToContainTextAsync("Notification");
    }

    async Task BaselineAsync(ILocator locator, string name)
    {
        await Expect(locator).ToBeVisibleAsync();
        var bytes = await locator.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-{name}");
    }
}
