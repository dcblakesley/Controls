using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the DateRangePicker UI-kit control (driven on the /uikit gallery, which pins
/// its picker to Jan/Feb 2025 so screenshots and month assertions are deterministic). This suite
/// owns the JS-interop behaviors bUnit can't execute: placePanel's below/above flip and open-order
/// stacking, initPicker's Enter handling, and the focus-out close.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class DateRangePickerE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public DateRangePickerE2ETests(AppFixture app, BrowserFixture browser)
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
        // The anchor/flip tests scroll the picker to an exact viewport position, so the page height
        // must be final before any test proceeds. Two late layout shifts happen after network-idle:
        // the server-paging table demo fills its rows ~150ms after init, and that init can run
        // twice (prerender, then WASM hydration) — so a bare row-count wait can pass against the
        // prerendered DOM just before hydration resets it. Wait for the rows AND for the document
        // height to hold still.
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

    // Scoped to the range picker's wrapper — /uikit also hosts a single-date DatePicker demo,
    // whose field/dropdown carry the same wss-picker classes.
    ILocator Picker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range") });
    ILocator Field => Picker.Locator(".wss-picker-input");
    ILocator Dropdown => Picker.Locator(".wss-picker-dropdown");

    // Scrolls the picker to the center of the viewport before opening, so placePanel always has
    // room below (the anchor/baseline tests rely on the downward placement). behavior:'instant'
    // everywhere: the app CSS sets html { scroll-behavior: smooth }, so a default scroll ANIMATES
    // and geometry measured right after it is mid-flight garbage.
    async Task OpenAsync()
    {
        await Field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await Field.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        // placePanel reveals the panel (drops wss-measuring) once it has measured and positioned it.
        await Expect(Dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    // The in-month day button for a given panel (0 = left, 1 = right) and zero-padded day text.
    ILocator Day(int panel, string dayText) =>
        _page.Locator(".wss-picker-month").Nth(panel)
            .Locator(".wss-picker-day:not(.wss-picker-day-outside)", new() { HasTextString = dayText });

    [Fact]
    public async Task Opens_below_the_field_with_the_pinned_months_and_stays_in_viewport()
    {
        await GotoAsync();
        await OpenAsync();

        await Expect(Dropdown).ToBeInViewportAsync(new() { Ratio = 1 });

        // The demo pins Start=2025-01-15, so the linked panels show Jan 2025 + Feb 2025.
        var monthSelects = _page.Locator(".wss-picker-month-header select");
        await Expect(monthSelects.Nth(0)).ToHaveValueAsync("1");
        await Expect(monthSelects.Nth(1)).ToHaveValueAsync("2025");
        await Expect(monthSelects.Nth(2)).ToHaveValueAsync("2");
        await Expect(monthSelects.Nth(3)).ToHaveValueAsync("2025");

        // Anchored just below the field (the field was centered, so there is room below).
        var f = await Field.BoundingBoxAsync();
        var p = await Dropdown.BoundingBoxAsync();
        Assert.NotNull(f);
        Assert.NotNull(p);
        Assert.InRange(p!.Y - (f!.Y + f.Height), 0, 12);
        Assert.InRange(p.X, f.X - 1, f.X + 1); // left-aligned to the field
    }

    [Fact]
    public async Task Flips_above_the_field_when_there_is_no_room_below()
    {
        await GotoAsync();
        // Pin the field ~80px above the viewport bottom (scrollIntoView block:'end' can't get it
        // low enough — the content after the picker section leaves ~300px of room below, which the
        // ~290px panel correctly still fits into). With 80px of room, placePanel must open upward.
        await Field.EvaluateAsync("el => { const r = el.getBoundingClientRect(); window.scrollBy({ top: r.top - (window.innerHeight - 80), behavior: 'instant' }); }");
        await Field.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        await Expect(Dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        var f = await Field.BoundingBoxAsync();
        var p = await Dropdown.BoundingBoxAsync();
        Assert.NotNull(f);
        Assert.NotNull(p);
        Assert.InRange(f!.Y - (p!.Y + p.Height), 0, 12);
    }

    [Fact]
    public async Task Two_day_clicks_commit_the_range_and_close()
    {
        await GotoAsync();
        await OpenAsync();

        await Day(0, "20").ClickAsync(); // first click keeps the panel open
        await Expect(Dropdown).ToBeVisibleAsync();
        await Day(1, "05").ClickAsync(); // second click commits and closes

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-result']"))
            .ToContainTextAsync("2025-01-20 → 2025-02-05");
        await Expect(_page.Locator(".wss-picker-input-start")).ToHaveValueAsync("01/20/2025");
        await Expect(_page.Locator(".wss-picker-input-end")).ToHaveValueAsync("02/05/2025");
    }

    [Fact]
    public async Task Preset_click_commits_and_closes()
    {
        await GotoAsync();
        await OpenAsync();

        await _page.Locator(".wss-picker-preset", new() { HasTextString = "This Month" }).ClickAsync();

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        // Presets resolve at click time from the machine's clock.
        var today = DateTime.Today;
        var expected = $"{today.Year:0000}-{today.Month:00}-01 → {today:yyyy-MM-dd}";
        await Expect(_page.Locator("[data-test-id='range-result']")).ToContainTextAsync(expected);
    }

    [Fact]
    public async Task Typing_a_start_date_commits_on_enter_and_retargets_the_panels()
    {
        await GotoAsync();
        await OpenAsync();

        var startInput = _page.Locator(".wss-picker-input-start");
        await startInput.FillAsync("03/05/2025");
        await startInput.PressAsync("Enter");

        // Committed (without closing — only completing a pick or a preset closes) and the left
        // panel followed the typed month.
        await Expect(_page.Locator("[data-test-id='range-result']")).ToContainTextAsync("2025-03-05");
        await Expect(Dropdown).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-picker-month-header select").First).ToHaveValueAsync("3");
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
        // Click the backdrop itself at a far-left offset (its center may be covered by the panel,
        // and the top-right viewport corner is owned by the fixed toast containers).
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
        // Click the start input itself (not the composite field, whose center may land elsewhere)
        // so the focus origin for Shift+Tab is deterministic.
        var startInput = _page.Locator(".wss-picker-input-start");
        await startInput.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await startInput.ClickAsync();
        await Expect(Dropdown).ToBeVisibleAsync();
        await Expect(startInput).ToBeFocusedAsync();

        // Shift+Tab leaves the wrapper backwards (the clear/panel elements all sit after the
        // inputs in DOM order) — the focusout wiring must route through the backdrop's close.
        await _page.Keyboard.PressAsync("Shift+Tab");
        await Expect(Dropdown).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Arrow_keys_move_focus_between_days_without_scrolling_the_page()
    {
        await GotoAsync();
        await OpenAsync();

        // The demo pins Start=2025-01-15, which also carries the roving tabindex on open.
        var start = _page.Locator("[data-date='2025-01-15']");
        await start.FocusAsync();
        var scrollBefore = await _page.EvaluateAsync<double>("window.scrollY");

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(_page.Locator("[data-date='2025-01-16']")).ToBeFocusedAsync();

        await _page.Keyboard.PressAsync("ArrowDown");
        await Expect(_page.Locator("[data-date='2025-01-23']")).ToBeFocusedAsync();

        // Lands on Feb 23 — already the right panel, so this exercises the no-view-change path
        // (the left/right month selects don't move; only the roving tabindex and DOM focus do).
        await _page.Keyboard.PressAsync("PageDown");
        await Expect(_page.Locator("[data-date='2025-02-23']")).ToBeFocusedAsync();

        // wss-picker.js suppresses the native scroll-on-arrow-key/PageDown behavior for day buttons.
        var scrollAfter = await _page.EvaluateAsync<double>("window.scrollY");
        Assert.Equal(scrollBefore, scrollAfter);
    }

    [Fact]
    public async Task ArrowRight_across_the_left_panel_month_boundary_focuses_the_in_month_first_of_month_cell()
    {
        await GotoAsync();
        await OpenAsync();

        // The demo pins Start=2025-01-15, which carries the roving tabindex on open (the left
        // panel's month, Jan 2025). Walk forward to Jan 31 -- the left panel's last day -- via
        // real key presses so the component's own _focusDay state (not just DOM focus) tracks
        // along; a raw FocusAsync only moves the DOM, which OnGridKeyDown never reads from.
        var start = _page.Locator("[data-date='2025-01-15']");
        await start.FocusAsync();
        await _page.Keyboard.PressAsync("ArrowDown"); // Jan 22
        await _page.Keyboard.PressAsync("ArrowDown"); // Jan 29
        await _page.Keyboard.PressAsync("ArrowRight"); // Jan 30
        await _page.Keyboard.PressAsync("ArrowRight"); // Jan 31 -- left panel's last day
        await Expect(_page.Locator(":focus")).ToHaveAttributeAsync("data-date", "2025-01-31");

        // The actual crossing under test: Feb 1 exists in BOTH grids near this boundary -- as a
        // dimmed trailing cell in the left (Jan) grid, and as the real in-month first day in the
        // right (Feb) grid. Only the latter carries the roving tabindex="0". This is the
        // regression coverage for the focusDay selector fix in wss-picker.js, which used to match
        // the dimmed duplicate because it comes first in DOM order (left panel renders before
        // right).
        await _page.Keyboard.PressAsync("ArrowRight");

        var active = _page.Locator(":focus");
        await Expect(active).ToHaveAttributeAsync("data-date", "2025-02-01");
        await Expect(active).ToHaveAttributeAsync("tabindex", "0");
        await Expect(active).Not.ToHaveClassAsync(new Regex("wss-picker-day-outside"));
    }

    [Fact]
    public async Task ArrowRight_past_the_right_panel_shifts_the_view_by_one_month_not_two()
    {
        await GotoAsync();
        await OpenAsync();

        // Walk forward from the pinned Start (Jan 15, left panel) to Feb 28 -- the right panel's
        // last day, 2025 not being a leap year -- via real key presses so _focusDay tracks along.
        var start = _page.Locator("[data-date='2025-01-15']");
        await start.FocusAsync();
        for (var i = 0; i < 6; i++)
        {
            await _page.Keyboard.PressAsync("ArrowDown"); // Jan 22, 29; Feb 5, 12, 19, 26
        }
        await _page.Keyboard.PressAsync("ArrowRight"); // Feb 27
        await _page.Keyboard.PressAsync("ArrowRight"); // Feb 28 -- right panel's last day
        await Expect(_page.Locator(":focus")).ToHaveAttributeAsync("data-date", "2025-02-28");

        // Crossing forward out of the right panel must anchor the RIGHT panel on the new month --
        // a one-month view shift (Jan/Feb -> Feb/Mar) -- rather than the left panel, which would
        // jump straight to Mar/Apr and skip Feb entirely.
        await _page.Keyboard.PressAsync("ArrowRight");

        var monthSelects = _page.Locator(".wss-picker-month-header select");
        await Expect(monthSelects.Nth(0)).ToHaveValueAsync("2");
        await Expect(monthSelects.Nth(1)).ToHaveValueAsync("2025");
        await Expect(monthSelects.Nth(2)).ToHaveValueAsync("3");
        await Expect(monthSelects.Nth(3)).ToHaveValueAsync("2025");
    }

    [Fact]
    public async Task Open_panel_visual_baseline()
    {
        await GotoAsync();
        await OpenAsync();
        // Pinned demo range 01/15/2025 → 02/03/2025: both endpoints and the in-range band are
        // visible across the two panels, plus the preset sidebar.
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
