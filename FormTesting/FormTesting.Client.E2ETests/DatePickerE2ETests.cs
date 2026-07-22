using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the single-date DatePicker UI-kit control (driven on the /uikit gallery,
/// which pins its picker to Feb 2026 so screenshots and month assertions are deterministic).
/// This suite owns the JS-interop behaviors bUnit can't execute: placePanel's placement and
/// open-order stacking, initPicker's Enter handling, and the focus-out close. It also covers the
/// Month/Time/DateTime <see cref="Controls.DatePickerMode"/> demos (#demo-month/#demo-time/
/// #demo-datetime) -- specifically the month-grid keyboard nav-key scroll suppression and DOM
/// focus follow that wss-picker.js owns, which bUnit can't exercise either.
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
    public async Task Enter_on_a_focused_day_commits_and_returns_focus_to_the_input()
    {
        await GotoAsync();
        await OpenAsync();

        // The demo pins Value=2026-02-14, which carries the roving tabindex on open. A native
        // Enter on a focused <button> synthesizes a click, so this exercises the same
        // panel-originated close (day click) that should restore focus to the text input instead
        // of leaving it stranded on the day button that's about to unmount.
        var day = Dropdown.Locator("[data-date='2026-02-14']");
        await day.FocusAsync();
        await _page.Keyboard.PressAsync("Enter");

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(Input).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Escape_from_the_day_grid_returns_focus_to_the_input()
    {
        await GotoAsync();
        await OpenAsync();

        // Escape while focus is on a descendant of the panel (here, the day grid) must restore
        // focus to the text input rather than dropping it to <body> once the panel unmounts.
        var day = Dropdown.Locator("[data-date='2026-02-14']");
        await day.FocusAsync();
        await _page.Keyboard.PressAsync("Escape");

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(Input).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Outside_click_closes_without_moving_focus_to_the_input()
    {
        await GotoAsync();
        await OpenAsync();

        // Any click while open lands on the full-viewport backdrop (position:fixed; inset:0) --
        // the practical stand-in for "click elsewhere on the page". Unlike Escape/Enter/day-click,
        // this outside-close path must NOT steal focus from wherever the user actually clicked.
        await _page.Locator(".wss-picker-backdrop").ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = 5, Y = 40 },
        });

        await Expect(Dropdown).Not.ToBeVisibleAsync();
        await Expect(Input).Not.ToBeFocusedAsync();
    }

    // --- Mode.Month demo (#demo-month, pinned Value=2026-02-01) ------------------------------

    ILocator MonthPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-month") });
    ILocator MonthField => MonthPicker.Locator(".wss-picker-input");
    ILocator MonthInput => _page.Locator("#demo-month");
    ILocator MonthDropdown => MonthPicker.Locator(".wss-picker-dropdown");

    async Task OpenMonthAsync()
    {
        await MonthField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await MonthField.ClickAsync();
        await Expect(MonthDropdown).ToBeVisibleAsync();
        await Expect(MonthDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Month_click_commits_the_first_of_month_and_closes()
    {
        await GotoAsync();
        await OpenMonthAsync();

        // The demo pins Value=2026-02-01, so the year header shows 2026 with Feb selected. Pick a
        // different month so the click is observably responsible for the resulting value.
        await MonthDropdown.Locator("[data-date='2026-05-01']").ClickAsync();

        await Expect(MonthDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='month-result']")).ToContainTextAsync("2026-05");
        await Expect(MonthInput).ToHaveValueAsync("05/2026");
        // The clicked month button is about to unmount -- focus returns to the text input.
        await Expect(MonthInput).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Month_grid_ArrowRight_moves_focus_and_PageDown_moves_year_without_scrolling_the_page()
    {
        await GotoAsync();
        await OpenMonthAsync();

        // The demo pins Value=2026-02-01, which carries the roving tabindex on open.
        var start = MonthDropdown.Locator("[data-date='2026-02-01']");
        await start.FocusAsync();
        var scrollBefore = await _page.EvaluateAsync<double>("window.scrollY");

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(MonthDropdown.Locator("[data-date='2026-03-01']")).ToBeFocusedAsync();

        // PageDown moves the displayed year by one, re-rendering the grid with new button instances
        // (exercises the pending-focus-date handoff, same as the day grid's month-crossing move).
        await _page.Keyboard.PressAsync("PageDown");
        await Expect(MonthDropdown.Locator("[data-date='2027-03-01']")).ToBeFocusedAsync();

        // wss-picker.js suppresses the native scroll-on-PageDown behavior for month buttons too.
        var scrollAfter = await _page.EvaluateAsync<double>("window.scrollY");
        Assert.Equal(scrollBefore, scrollAfter);
    }

    // --- Mode.Year demo (#demo-year, pinned Value=2026-02-14) --------------------------------

    ILocator YearPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-year") });
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
    public async Task Year_grid_PageDown_flips_the_decade_with_focus_follow_and_no_page_scroll()
    {
        await GotoAsync();
        await OpenYearAsync();

        // The demo pins Value=2026-02-14, which carries the roving tabindex (2026-01-01, the year's
        // own 1st) on open.
        var start = YearDropdown.Locator("[data-date='2026-01-01']");
        await start.FocusAsync();
        var scrollBefore = await _page.EvaluateAsync<double>("window.scrollY");

        // PageDown flips a whole decade (+10 years, 2020s -> 2030s) -- the grid re-renders with new
        // button instances, exercising the pending-focus-date handoff (same as the day/month grids'
        // month/year-crossing moves). Year cells reuse wss-picker-month-btn precisely so this is the
        // same JS path wss-picker.js already suppresses scroll for -- no JS module change needed.
        await _page.Keyboard.PressAsync("PageDown");
        await Expect(YearDropdown.Locator("[data-date='2036-01-01']")).ToBeFocusedAsync();

        var scrollAfter = await _page.EvaluateAsync<double>("window.scrollY");
        Assert.Equal(scrollBefore, scrollAfter);
    }

    // --- Mode.Time demo (#demo-time, pinned Value time-of-day 09:30:15) ----------------------

    ILocator TimePicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-time") });
    ILocator TimeField => TimePicker.Locator(".wss-picker-input");
    ILocator TimeInput => _page.Locator("#demo-time");
    ILocator TimeDropdown => TimePicker.Locator(".wss-picker-dropdown");

    async Task OpenTimeAsync()
    {
        await TimeField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await TimeField.ClickAsync();
        await Expect(TimeDropdown).ToBeVisibleAsync();
        await Expect(TimeDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Time_mode_select_change_commits_immediately_and_ok_closes_returning_focus_to_the_input()
    {
        await GotoAsync();
        await OpenTimeAsync();

        await TimeDropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("14");

        // A select change commits without closing -- only OK is the close signal in Time mode.
        await Expect(TimeDropdown).ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='time-result']")).ToContainTextAsync("14:30:15");
        await Expect(TimeInput).ToHaveValueAsync("14:30:15");

        await TimeDropdown.Locator(".wss-picker-ok").ClickAsync();
        await Expect(TimeDropdown).Not.ToBeVisibleAsync();
        await Expect(TimeInput).ToBeFocusedAsync();
    }

    // --- Mode.DateTime demo (#demo-datetime, pinned Value=2026-02-14 09:30:15) ---------------

    ILocator DateTimePicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-datetime") });
    ILocator DateTimeField => DateTimePicker.Locator(".wss-picker-input");
    ILocator DateTimeInput => _page.Locator("#demo-datetime");
    ILocator DateTimeDropdown => DateTimePicker.Locator(".wss-picker-dropdown");

    async Task OpenDateTimeAsync()
    {
        await DateTimeField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await DateTimeField.ClickAsync();
        await Expect(DateTimeDropdown).ToBeVisibleAsync();
        await Expect(DateTimeDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    ILocator DateTimeDay(string dayText) =>
        DateTimeDropdown.Locator(".wss-picker-day:not(.wss-picker-day-outside)", new() { HasTextString = dayText });

    [Fact]
    public async Task DateTime_mode_day_click_preserves_time_stays_open_then_time_select_preserves_date_then_ok_closes()
    {
        await GotoAsync();
        await OpenDateTimeAsync();

        // Day click sets the date part only, keeping the already-committed time-of-day
        // (09:30:15), and the panel stays open for further adjustment.
        await DateTimeDay("20").ClickAsync();

        await Expect(DateTimeDropdown).ToBeVisibleAsync();
        await Expect(DateTimeInput).ToHaveValueAsync("02/20/2026 09:30:15");

        // A time select change keeps the date part just set (Feb 20), not the original Feb 14.
        await DateTimeDropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("16");
        await Expect(DateTimeDropdown).ToBeVisibleAsync();
        await Expect(DateTimeInput).ToHaveValueAsync("02/20/2026 16:30:15");

        await DateTimeDropdown.Locator(".wss-picker-ok").ClickAsync();
        await Expect(DateTimeDropdown).Not.ToBeVisibleAsync();
        await Expect(DateTimeInput).ToBeFocusedAsync();
    }

    // --- Mode.Week demo (#demo-week, pinned Value=2026-02-14, FirstDayOfWeek=Sunday) ----------

    ILocator WeekPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-week") });
    ILocator WeekField => WeekPicker.Locator(".wss-picker-input");
    ILocator WeekInput => _page.Locator("#demo-week");
    ILocator WeekDropdown => WeekPicker.Locator(".wss-picker-dropdown");

    async Task OpenWeekAsync()
    {
        await WeekField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await WeekField.ClickAsync();
        await Expect(WeekDropdown).ToBeVisibleAsync();
        await Expect(WeekDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Week_mode_shows_the_selected_band_and_a_mid_week_click_commits_the_week_start()
    {
        await GotoAsync();
        await OpenWeekAsync();

        // The pinned Value=2026-02-14 (a Saturday) falls in the Feb 8 (Sun) - Feb 14 (Sat) week --
        // the row carrying that band on open, before any click.
        var selectedRow = WeekDropdown.Locator(".wss-picker-week-row-selected");
        await Expect(selectedRow).ToHaveCountAsync(1);
        await Expect(selectedRow.Locator("[data-date='2026-02-08']")).ToHaveCountAsync(1);
        await Expect(selectedRow.Locator("[data-date='2026-02-14']")).ToHaveCountAsync(1);

        // Clicking a MID-week day (Feb 11, Wednesday -- neither the row's first nor last day) must
        // still commit that row's week START, not the clicked day.
        await WeekDropdown.Locator("[data-date='2026-02-11']").ClickAsync();

        await Expect(WeekDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='week-result']")).ToContainTextAsync("2026-02-08");
        // Format null in Week mode shows the "yyyy-Www" shorthand, not the clicked day's own date.
        await Expect(WeekInput).ToHaveValueAsync(new Regex(@"^2026-W\d{2}$"));
    }

    // --- Presets + ShowToday demo (#demo-presets, pinned Value=2026-02-14) --------------------

    ILocator PresetsPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-presets") });
    ILocator PresetsField => PresetsPicker.Locator(".wss-picker-input");
    ILocator PresetsDropdown => PresetsPicker.Locator(".wss-picker-dropdown");

    async Task OpenPresetsAsync()
    {
        await PresetsField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await PresetsField.ClickAsync();
        await Expect(PresetsDropdown).ToBeVisibleAsync();
        await Expect(PresetsDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Preset_click_commits_and_closes()
    {
        await GotoAsync();
        await OpenPresetsAsync();

        // "Today" resolves DateTime.Today at click time -- assert against the date the app itself
        // reports rather than hardcoding "today" in the test (which would drift the day this runs).
        var today = await _page.EvaluateAsync<string>("() => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }");

        await PresetsDropdown.Locator(".wss-picker-preset", new() { HasTextString = "Today" }).ClickAsync();

        await Expect(PresetsDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='presets-result']")).ToContainTextAsync(today);
    }

    [Fact]
    public async Task Today_link_commits_todays_date_and_closes()
    {
        await GotoAsync();
        await OpenPresetsAsync();

        var today = await _page.EvaluateAsync<string>("() => { const d = new Date(); return d.getFullYear() + '-' + String(d.getMonth() + 1).padStart(2, '0') + '-' + String(d.getDate()).padStart(2, '0'); }");

        await PresetsDropdown.Locator(".wss-picker-today-btn").ClickAsync();

        await Expect(PresetsDropdown).Not.ToBeVisibleAsync();
        await Expect(_page.Locator("[data-test-id='presets-result']")).ToContainTextAsync(today);
    }

    // --- Use12Hours + MinuteStep=15 + ShowSeconds=false demo (#demo-12h, pinned 14:30) ---------

    ILocator TwelveHourPicker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-12h") });
    ILocator TwelveHourField => TwelveHourPicker.Locator(".wss-picker-input");
    ILocator TwelveHourInput => _page.Locator("#demo-12h");
    ILocator TwelveHourDropdown => TwelveHourPicker.Locator(".wss-picker-dropdown");

    async Task OpenTwelveHourAsync()
    {
        await TwelveHourField.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await TwelveHourField.ClickAsync();
        await Expect(TwelveHourDropdown).ToBeVisibleAsync();
        await Expect(TwelveHourDropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    [Fact]
    public async Task Use12Hours_period_change_shifts_the_committed_value_and_input_by_12_hours()
    {
        await GotoAsync();
        await OpenTwelveHourAsync();

        // MinuteStep=15 restricts the minute select to the 0/15/30/45 lattice -- the pinned 14:30
        // value sits on it, so no off-lattice "current value" option should be present.
        var minuteOptions = await TwelveHourDropdown.Locator("select[aria-label='Minute'] option").AllTextContentsAsync();
        Assert.Equal(["00", "15", "30", "45"], minuteOptions);

        await Expect(TwelveHourInput).ToHaveValueAsync("2:30 PM");

        await TwelveHourDropdown.Locator("select[aria-label='AM/PM']").SelectOptionAsync("AM");

        // A period change commits without closing (matches every other Time-mode select).
        await Expect(TwelveHourDropdown).ToBeVisibleAsync();
        await Expect(TwelveHourInput).ToHaveValueAsync("2:30 AM");
        await Expect(_page.Locator("[data-test-id='twelve-hour-result']")).ToContainTextAsync("02:30");
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
