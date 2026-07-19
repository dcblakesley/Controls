using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the single-date DatePicker UI-kit control (driven on the /uikit gallery,
/// which pins its picker to Feb 2026 so screenshots and month assertions are deterministic).
/// This suite owns the JS-interop behaviors bUnit can't execute: placePanel's placement and
/// open-order stacking, initPicker's Enter handling, and the focus-out close.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class DatePickerE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public DatePickerE2ETests(AppFixture app, BrowserFixture browser)
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
        // Wait for the page height to be final before geometry-sensitive steps (see
        // DateRangePickerE2ETests.GotoAsync for the full story on the late layout shifts).
        await Expect(_page.Locator(".wss-table").Last.Locator(".wss-table-row"))
            .ToHaveCountAsync(10, new() { Timeout = 15_000 });
        await _page.WaitForFunctionAsync(
            @"() => {
                const h = document.body.scrollHeight;
                if (window.__wssLastHeight !== h) { window.__wssLastHeight = h; window.__wssStableSince = Date.now(); }
                return Date.now() - window.__wssStableSince > 600;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    // Scoped to the single-date picker's wrapper — /uikit also hosts the DateRangePicker demo,
    // whose field/dropdown carry the same wss-picker classes.
    ILocator Picker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-date") });
    ILocator Field => Picker.Locator(".wss-picker-input");
    ILocator Input => _page.Locator("#demo-date");
    ILocator Dropdown => Picker.Locator(".wss-picker-dropdown");

    // Scrolls the picker to the center of the viewport before opening (behavior:'instant' — the
    // app CSS sets html { scroll-behavior: smooth }, so a default scroll animates and geometry
    // measured right after it is mid-flight garbage).
    async Task OpenAsync()
    {
        await Field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await Field.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        await Expect(Dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    ILocator Day(string dayText) =>
        Dropdown.Locator(".wss-picker-day:not(.wss-picker-day-outside)", new() { HasTextString = dayText });

    [Fact]
    public async Task Opens_below_the_field_showing_the_pinned_month_with_the_value_selected()
    {
        await GotoAsync();
        await OpenAsync();

        await Expect(Dropdown).ToBeInViewportAsync(new() { Ratio = 1 });

        // The demo pins Value=2026-02-14, so the single panel shows Feb 2026 with 14 selected.
        var selects = Dropdown.Locator(".wss-picker-month-header select");
        await Expect(selects.Nth(0)).ToHaveValueAsync("2");
        await Expect(selects.Nth(1)).ToHaveValueAsync("2026");
        await Expect(Dropdown.Locator(".wss-picker-day-selected")).ToHaveTextAsync("14");

        var f = await Field.BoundingBoxAsync();
        var p = await Dropdown.BoundingBoxAsync();
        Assert.NotNull(f);
        Assert.NotNull(p);
        Assert.InRange(p!.Y - (f!.Y + f.Height), 0, 12);
        Assert.InRange(p.X, f.X - 1, f.X + 1); // left-aligned to the field
    }

    [Fact]
    public async Task One_day_click_commits_and_closes()
    {
        await GotoAsync();
        await OpenAsync();

        await Day("20").ClickAsync();

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='date-result']")).ToContainTextAsync("2026-02-20");
        await Expect(Input).ToHaveValueAsync("02/20/2026");
    }

    [Fact]
    public async Task Typing_a_date_commits_on_enter_and_closes()
    {
        await GotoAsync();
        await OpenAsync();

        await Input.FillAsync("03/05/2026");
        await Input.PressAsync("Enter");

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='date-result']")).ToContainTextAsync("2026-03-05");
    }

    [Fact]
    public async Task Escape_closes_and_backdrop_click_closes()
    {
        await GotoAsync();
        await OpenAsync();
        await _page.Keyboard.PressAsync("Escape");
        await Expect(Dropdown).Not.ToBeVisibleAsync();

        await Field.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        await _page.Locator(".wss-picker-backdrop").ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = 5, Y = 40 },
        });
        await Expect(Dropdown).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Tabbing_backwards_out_of_the_field_closes_the_dropdown()
    {
        await GotoAsync();
        await Input.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await Input.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        await Expect(Input).ToBeFocusedAsync();

        await _page.Keyboard.PressAsync("Shift+Tab");
        await Expect(Dropdown).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Arrow_keys_move_focus_between_days_without_scrolling_the_page()
    {
        await GotoAsync();
        await OpenAsync();

        // The demo pins Value=2026-02-14, which also carries the roving tabindex on open.
        var start = Dropdown.Locator("[data-date='2026-02-14']");
        await start.FocusAsync();
        var scrollBefore = await _page.EvaluateAsync<double>("window.scrollY");

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(Dropdown.Locator("[data-date='2026-02-15']")).ToBeFocusedAsync();

        await _page.Keyboard.PressAsync("ArrowDown");
        await Expect(Dropdown.Locator("[data-date='2026-02-22']")).ToBeFocusedAsync();

        // Crosses a month boundary — the grid re-renders with new button instances, exercising the
        // pending-focus-date handoff (an ElementReference can't survive that re-render).
        await _page.Keyboard.PressAsync("PageDown");
        await Expect(Dropdown.Locator("[data-date='2026-03-22']")).ToBeFocusedAsync();

        // wss-picker.js suppresses the native scroll-on-arrow-key/PageDown behavior for day buttons.
        var scrollAfter = await _page.EvaluateAsync<double>("window.scrollY");
        Assert.Equal(scrollBefore, scrollAfter);
    }

    [Fact]
    public async Task Open_panel_visual_baseline()
    {
        await GotoAsync();
        await OpenAsync();
        // Pinned demo value 2026-02-14: the single Feb 2026 panel with the selected day filled.
        var bytes = await Dropdown.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-open-panel");
    }

    [Fact]
    public async Task Field_visual_baseline_closed()
    {
        await GotoAsync();
        await Field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        var bytes = await Field.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-field-closed");
    }
}
