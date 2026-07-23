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
        // height to hold still. Scoped by section (not .wss-table.Last) so appended sections below
        // it on the gallery page can never shift which table this resolves to.
        await Expect(_page.Locator("section.demo-section", new() { HasTextString = "server-side paging" })
                .Locator(".wss-table-row"))
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
        await Expect(Picker.Locator(".wss-picker-input-start")).ToHaveValueAsync("01/20/2025");
        await Expect(Picker.Locator(".wss-picker-input-end")).ToHaveValueAsync("02/05/2025");
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

        var startInput = Picker.Locator(".wss-picker-input-start");
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
        var startInput = Picker.Locator(".wss-picker-input-start");
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

    // --- Mode.Month demo (#demo-range-month, pinned Start=2026-02-01, End=2026-08-01) --------------

    ILocator MonthPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-month") });
    ILocator MonthField => MonthPicker.Locator(".wss-picker-input");
    ILocator MonthDropdown => MonthPicker.Locator(".wss-picker-dropdown");

    async Task OpenMonthAsync()
    {
        await MonthField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await MonthField.ClickAsync();
        await Expect(MonthDropdown).ToBeVisibleAsync();
        await Expect(MonthDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Month_mode_shows_dual_year_panels_and_two_clicks_commit_month_starts()
    {
        await GotoAsync();
        await OpenMonthAsync();

        // The demo pins Start=2026-02-01, so the left panel's year header shows 2026; the right
        // panel is always the following year, 2027 -- dual panels, not a single one.
        var yearSelects = MonthDropdown.Locator(".wss-picker-month-header select");
        await Expect(yearSelects.Nth(0)).ToHaveValueAsync("2026");
        await Expect(yearSelects.Nth(1)).ToHaveValueAsync("2027");

        await MonthDropdown.Locator("[data-date='2026-05-01']").ClickAsync(); // first click: pending start
        await Expect(MonthDropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click
        await MonthDropdown.Locator("[data-date='2027-03-01']").ClickAsync(); // second click: commits + closes

        await Expect(MonthDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-month-result']")).ToContainTextAsync("2026-05 → 2027-03");
    }

    // --- Mode.Year demo (#demo-range-year, pinned Start=2024-01-01, End=2027-01-01) ----------------

    ILocator YearPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-year") });
    ILocator YearField => YearPicker.Locator(".wss-picker-input");
    ILocator YearDropdown => YearPicker.Locator(".wss-picker-dropdown");

    async Task OpenYearAsync()
    {
        await YearField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await YearField.ClickAsync();
        await Expect(YearDropdown).ToBeVisibleAsync();
        await Expect(YearDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Year_mode_shows_dual_decade_panels_and_two_clicks_commit_year_starts()
    {
        await GotoAsync();
        await OpenYearAsync();

        // The demo pins Start=2024-01-01, so the left panel's decade reads 2020-2029; the right
        // panel is always the following decade, 2030-2039 -- dual panels, not a single one.
        var decadeLabels = YearDropdown.Locator(".wss-picker-decade-label");
        await Expect(decadeLabels.Nth(0)).ToHaveTextAsync("2020-2029");
        await Expect(decadeLabels.Nth(1)).ToHaveTextAsync("2030-2039");

        await YearDropdown.Locator("[data-date='2023-01-01']").ClickAsync(); // first click: pending start
        await Expect(YearDropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click
        await YearDropdown.Locator("[data-date='2032-01-01']").ClickAsync(); // second click: commits + closes

        await Expect(YearDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-year-result']")).ToContainTextAsync("2023 → 2032");
    }

    // --- Mode.Week demo (#demo-range-week, pinned Start=2026-02-08, End=2026-03-01, both Sundays,
    // FirstDayOfWeek=Sunday) -------------------------------------------------------------------------

    ILocator WeekPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-week") });
    ILocator WeekField => WeekPicker.Locator(".wss-picker-input");
    ILocator WeekDropdown => WeekPicker.Locator(".wss-picker-dropdown");

    async Task OpenWeekAsync()
    {
        await WeekField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await WeekField.ClickAsync();
        await Expect(WeekDropdown).ToBeVisibleAsync();
        await Expect(WeekDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    // The in-panel day button for a given panel (0 = left/Feb, 1 = right/Mar) and ISO date --
    // needed because the fixed 6-row/42-cell grid overlaps at this particular boundary (Feb and
    // Mar 2026 both happen to start on a Sunday, so the left panel's own trailing weeks duplicate
    // some of the right panel's leading dates -- see the test's own comment below).
    ILocator WeekDay(int panel, string date) =>
        WeekDropdown.Locator(".wss-picker-month").Nth(panel).Locator($"[data-date='{date}']");

    [Fact]
    public async Task Week_mode_shows_week_numbers_and_a_mid_week_click_commits_the_week_start_with_row_range_styling()
    {
        await GotoAsync();
        await OpenWeekAsync();

        // Mode.Week always renders the leading week-number column, regardless of ShowWeekNumbers.
        await Expect(WeekDropdown.Locator(".wss-picker-week-no").First).ToBeVisibleAsync();

        // The pinned Start=2026-02-08/End=2026-03-01 (both already week starts) paint the whole
        // ROW, not a single cell -- the demo's Start anchors the left panel on Feb 2026, so the
        // right panel shows Mar 2026 and both endpoint rows are visible without navigating. Feb
        // 8's row is unambiguous (Start's own row, count 1); Mar 1's row shows up TWICE -- as the
        // left (Feb) panel's own trailing week AND the right (Mar) panel's leading week, since
        // Feb and Mar 2026 both happen to start on a Sunday, so the fixed 6-row/42-cell grid
        // overlaps there (the same phenomenon the Date-mode day grid's own month-boundary
        // regression coverage exercises one level down).
        await Expect(WeekDropdown.Locator(".wss-picker-week-row-start")).ToHaveCountAsync(1);
        await Expect(WeekDropdown.Locator(".wss-picker-week-row-start").Locator("[data-date='2026-02-08']")).ToHaveCountAsync(1);
        await Expect(WeekDropdown.Locator(".wss-picker-week-row-end")).ToHaveCountAsync(2);

        // A MID-week click (Feb 11, Wednesday -- neither the row's first nor last day) starts a
        // fresh pick and must commit that row's week START (Feb 8), not the clicked day.
        await WeekDay(0, "2026-02-11").ClickAsync();
        await Expect(WeekDropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click

        // Hovering a day in a different row (before the second click) previews the row-level band
        // between the pending start's row and the hovered row.
        await WeekDay(0, "2026-02-25").HoverAsync();
        await Expect(WeekDropdown.Locator(
            ".wss-picker-week-row-preview, .wss-picker-week-row-preview-start, .wss-picker-week-row-preview-end").First)
            .ToBeVisibleAsync();

        // Second click lands in the RIGHT (Mar) panel specifically -- Mar 11 also appears as a
        // trailing/outside cell in the left panel's own grid (same overlap as above), so this
        // must be panel-scoped to stay unambiguous.
        await WeekDay(1, "2026-03-11").ClickAsync(); // second click: commits + closes

        await Expect(WeekDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-week-result']")).ToContainTextAsync("2026-02-08 → 2026-03-08");
    }

    // --- Mode.Quarter demo (#demo-range-quarter, pinned Start=2026-01-01 [Q1], End=2026-07-01 [Q3]) --

    ILocator QuarterPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-quarter") });
    ILocator QuarterField => QuarterPicker.Locator(".wss-picker-input");
    ILocator QuarterDropdown => QuarterPicker.Locator(".wss-picker-dropdown");

    async Task OpenQuarterAsync()
    {
        await QuarterField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await QuarterField.ClickAsync();
        await Expect(QuarterDropdown).ToBeVisibleAsync();
        await Expect(QuarterDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Quarter_mode_two_clicks_commit_quarter_starts()
    {
        await GotoAsync();
        await OpenQuarterAsync();

        // The demo pins Start=2026-01-01, so the left panel's year header shows 2026; the right
        // panel is always the following year, 2027.
        var yearSelects = QuarterDropdown.Locator(".wss-picker-month-header select");
        await Expect(yearSelects.Nth(0)).ToHaveValueAsync("2026");
        await Expect(yearSelects.Nth(1)).ToHaveValueAsync("2027");

        await QuarterDropdown.Locator("[data-date='2026-04-01']").ClickAsync(); // Q2 2026: pending start
        await Expect(QuarterDropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click
        await QuarterDropdown.Locator("[data-date='2027-10-01']").ClickAsync(); // Q4 2027: commits + closes

        await Expect(QuarterDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-quarter-result']")).ToContainTextAsync("2026-04-01 → 2027-10-01");
    }

    // --- Mode.DateTime demo (#demo-range-datetime, both endpoints start empty,
    // DefaultViewDate=2026-02-10) ----------------------------------------------------------------

    ILocator DateTimePicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-datetime") });
    ILocator DateTimeField => DateTimePicker.Locator(".wss-picker-input");
    ILocator DateTimeDropdown => DateTimePicker.Locator(".wss-picker-dropdown");
    // DOM order: the start input's slot is always first, the end input's slot always second.
    ILocator DateTimeStartSlot => DateTimePicker.Locator(".wss-picker-input-slot").Nth(0);
    ILocator DateTimeEndSlot => DateTimePicker.Locator(".wss-picker-input-slot").Nth(1);

    async Task OpenDateTimeAsync()
    {
        await DateTimeField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await DateTimeField.ClickAsync();
        await Expect(DateTimeDropdown).ToBeVisibleAsync();
        await Expect(DateTimeDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task DateTime_mode_session_advances_from_start_to_end_then_commits_both_on_ok()
    {
        await GotoAsync();
        await OpenDateTimeAsync();

        // A SINGLE panel (not the dual-panel layout), with a day grid, a time row, and an OK
        // footer below it -- and the START side's underline active (a field click always opens
        // with the start side active).
        await Expect(DateTimeDropdown).ToHaveClassAsync(new Regex("wss-picker-dropdown-single"));
        await Expect(DateTimeDropdown.Locator("select[aria-label='Hour']")).ToHaveCountAsync(1);
        await Expect(DateTimeDropdown.Locator(".wss-picker-ok")).ToBeVisibleAsync();
        await Expect(DateTimeStartSlot).ToHaveClassAsync(new Regex("wss-picker-slot-active"));
        await Expect(DateTimeEndSlot).Not.ToHaveClassAsync(new Regex("wss-picker-slot-active"));

        // Pick a day and an hour for the START side, then OK.
        await DateTimeDropdown.Locator("[data-date='2026-02-10']").ClickAsync();
        await DateTimeDropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("9");
        await DateTimeDropdown.Locator(".wss-picker-ok").ClickAsync();

        // Nothing has reached the bound Start/End yet (both endpoints started empty) -- OK just
        // advances the session: the active underline moves to the end side and the panel stays open.
        await Expect(DateTimeDropdown).ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-datetime-result']")).ToContainTextAsync("— → —");
        await Expect(DateTimeStartSlot).Not.ToHaveClassAsync(new Regex("wss-picker-slot-active"));
        await Expect(DateTimeEndSlot).ToHaveClassAsync(new Regex("wss-picker-slot-active"));

        // Pick a day and an hour for the END side, then OK -- both sides are now resolved, so this
        // commits both together and closes.
        await DateTimeDropdown.Locator("[data-date='2026-02-20']").ClickAsync();
        await DateTimeDropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("17");
        await DateTimeDropdown.Locator(".wss-picker-ok").ClickAsync();

        await Expect(DateTimeDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-datetime-result']"))
            .ToContainTextAsync("2026-02-10 09:00:00 → 2026-02-20 17:00:00");
    }

    // --- Mode.Time demo (#demo-range-time, Use12Hours + MinuteStep=15 + ShowSeconds=false,
    // pinned 09:30/14:30) ------------------------------------------------------------------------

    ILocator TimePicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-time") });
    ILocator TimeField => TimePicker.Locator(".wss-picker-input");
    ILocator TimeDropdown => TimePicker.Locator(".wss-picker-dropdown");

    async Task OpenTimeAsync()
    {
        await TimeField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await TimeField.ClickAsync();
        await Expect(TimeDropdown).ToBeVisibleAsync();
        await Expect(TimeDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Time_mode_renders_12_hour_options_on_the_15_minute_lattice_and_ok_commits()
    {
        await GotoAsync();
        await OpenTimeAsync();

        // No calendar at all in Time mode -- just the time row and OK footer.
        await Expect(TimeDropdown.Locator(".wss-picker-grid, .wss-picker-grid-rows")).ToHaveCountAsync(0);
        await Expect(TimeDropdown.Locator("select[aria-label='AM/PM']")).ToHaveCountAsync(1);
        // ShowSeconds=false drops the seconds select entirely.
        await Expect(TimeDropdown.Locator("select[aria-label='Second']")).ToHaveCountAsync(0);
        // Use12Hours restricts the hour select to one 12-hour period's 12 values (the pinned 09:30
        // start is AM), not the full 24-hour set.
        await Expect(TimeDropdown.Locator("select[aria-label='Hour'] option")).ToHaveCountAsync(12);
        // MinuteStep=15 restricts the minute select to the 0/15/30/45 lattice -- the pinned 09:30
        // value sits on it, so no off-lattice "current value" option should be present.
        var minuteOptions = await TimeDropdown.Locator("select[aria-label='Minute'] option").AllTextContentsAsync();
        Assert.Equal(["00", "15", "30", "45"], minuteOptions);

        // Nudge the START side's minute -- a select change commits to the pending session value
        // without closing (only OK is the close signal in Time mode).
        await TimeDropdown.Locator("select[aria-label='Minute']").SelectOptionAsync("15");
        await Expect(TimeDropdown).ToBeVisibleAsync();

        // Both endpoints are already resolved (pinned), so OK commits immediately and closes.
        await TimeDropdown.Locator(".wss-picker-ok").ClickAsync();
        await Expect(TimeDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='range-time-result']")).ToContainTextAsync("09:15 → 14:30");
    }

    // --- DisabledDate (weekends) + ExtraFooter demo (#demo-range-disabled, pinned
    // Start=2026-02-02 [Mon], End=2026-02-13 [Fri]) ------------------------------------------------

    ILocator DisabledPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-disabled") });
    ILocator DisabledField => DisabledPicker.Locator(".wss-picker-input");
    ILocator DisabledDropdown => DisabledPicker.Locator(".wss-picker-dropdown");

    async Task OpenDisabledAsync()
    {
        await DisabledField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await DisabledField.ClickAsync();
        await Expect(DisabledDropdown).ToBeVisibleAsync();
        await Expect(DisabledDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task DisabledDate_disables_weekend_cells_and_ExtraFooter_content_is_visible()
    {
        await GotoAsync();
        await OpenDisabledAsync();

        // The demo pins Start=2026-02-02 (a Monday), so the left panel shows Feb 2026 -- Feb 7/8
        // (Sat/Sun) are the first weekend cells in that panel and must be disabled (unclickable).
        await Expect(DisabledDropdown.Locator("[data-date='2026-02-07']")).ToBeDisabledAsync();
        await Expect(DisabledDropdown.Locator("[data-date='2026-02-08']")).ToBeDisabledAsync();
        // A weekday stays enabled.
        await Expect(DisabledDropdown.Locator("[data-date='2026-02-09']")).ToBeEnabledAsync();

        await Expect(DisabledDropdown.Locator("[data-test-id='range-disabled-extra-footer']")).ToBeVisibleAsync();
        await Expect(DisabledDropdown.Locator(".wss-picker-extra-footer")).ToContainTextAsync("Weekdays only");
    }
}
