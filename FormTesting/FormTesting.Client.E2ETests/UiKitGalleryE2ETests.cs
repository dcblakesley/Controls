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
    public async Task Modal_traps_focus_when_shift_tabbing_from_the_panel()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();

        // Wait until the focus trap has activated (it moves focus into the panel on open).
        await _page.WaitForFunctionAsync(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");

        // Focus the panel itself (tabindex=-1, as if the user clicked an empty area of the body),
        // then Shift+Tab. The old trap only caught Tab on the first/last item, so focus on the panel
        // escaped backwards to the page behind the overlay.
        await panel.EvaluateAsync("el => el.focus()");
        await _page.Keyboard.PressAsync("Shift+Tab");

        var trapped = await _page.EvaluateAsync<bool>(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");
        Assert.True(trapped);
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
    public async Task Popover_opens_and_anchors_to_trigger()
    {
        await GotoAsync();
        await _page.Locator(".wss-popover-trigger").ClickAsync();
        var popover = _page.Locator(".wss-popover");
        await Expect(popover).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-popover-content")).ToContainTextAsync("popover content");
        await AssertAnchoredAboveAsync(".wss-popover-trigger", ".wss-popover");
    }

    [Fact]
    public async Task Popconfirm_anchors_to_trigger_then_confirms()
    {
        await GotoAsync();
        await _page.Locator(".wss-popconfirm-trigger").ClickAsync();
        var pop = _page.Locator(".wss-popconfirm");
        await Expect(pop).ToBeVisibleAsync();

        // Regression guard for the flex/grid stretch bug (left:50% on a full-width wrap): the panel
        // must be centred over the trigger and sit just above it, not drift to the section centre.
        await AssertAnchoredAboveAsync(".wss-popconfirm-trigger", ".wss-popconfirm");

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
    public async Task Table_select_all_checkbox_reaches_the_indeterminate_state()
    {
        await GotoAsync();
        const string headerSelector = ".wss-table-thead .wss-table-checkbox";
        var header = _page.Locator(headerSelector);
        var rows = _page.Locator(".wss-table-tbody .wss-table-checkbox");
        await Expect(header).ToBeVisibleAsync();

        // Start: neither checked nor indeterminate.
        Assert.False(await header.IsCheckedAsync());
        Assert.False(await header.EvaluateAsync<bool>("el => el.indeterminate"));

        // Select one row → header reaches the mixed state (a DOM property with no HTML attribute,
        // set from C# via wss-table.js). WaitForFunction tolerates the post-render interop gap.
        await rows.First.ClickAsync();
        await _page.WaitForFunctionAsync(
            $"() => {{ const cb = document.querySelector('{headerSelector}'); return !!cb && cb.indeterminate === true && cb.checked === false; }}");

        // Select all → fully checked, no longer indeterminate.
        await header.ClickAsync();
        await _page.WaitForFunctionAsync(
            $"() => {{ const cb = document.querySelector('{headerSelector}'); return !!cb && cb.indeterminate === false && cb.checked === true; }}");

        // Clear all → neither.
        await header.ClickAsync();
        await _page.WaitForFunctionAsync(
            $"() => {{ const cb = document.querySelector('{headerSelector}'); return !!cb && cb.indeterminate === false && cb.checked === false; }}");
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

    // Asserts an overlay panel is centred over its trigger and sits just above it (Top placement).
    // A precise geometric guard for anchoring — more reliable than a screenshot for an absolutely
    // -positioned overlay that can overflow the viewport when the trigger is near an edge.
    async Task AssertAnchoredAboveAsync(string triggerSelector, string panelSelector)
    {
        var panel = _page.Locator(panelSelector);
        // Edge-aware positioning: auto-wait for the JS flip/shift to settle and assert the panel
        // is fully within the viewport (Ratio = 1 ⇒ no part overflows the edge).
        await Expect(panel).ToBeInViewportAsync(new() { Ratio = 1 });

        var t = await _page.Locator(triggerSelector).BoundingBoxAsync();
        var p = await panel.BoundingBoxAsync();
        Assert.NotNull(t);
        Assert.NotNull(p);
        var triggerCenterX = t!.X + t.Width / 2;
        // The panel still covers the trigger (so the arrow points at it) ...
        Assert.InRange(triggerCenterX, p!.X, p.X + p.Width);
        // ... and sits just above the trigger (Top placement, ~10px gap).
        var gapAbove = t.Y - (p.Y + p.Height);
        Assert.InRange(gapAbove, -2.0, 40.0);
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
