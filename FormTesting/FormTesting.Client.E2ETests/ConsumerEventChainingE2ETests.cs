namespace FormTesting.Client.E2ETests;

/// <summary>
/// The browser half of the same-element handler-chaining contract that
/// <c>ConsumerEventChainingTests</c> pins in bUnit: where a control binds a handler of its own on the
/// element it splats the consumer's unmatched attributes onto, both must run — library first,
/// consumer second.
/// </summary>
/// <remarks>
/// <para>
/// bUnit can dispatch the event and count, but it executes no JavaScript, so it cannot answer the
/// question that actually matters here: do the consumer's handler and the JS-adjacent halves of these
/// controls coexist? The pickers' arrow navigation moves real DOM focus between grid cells and their
/// panels are JS-placed; <c>wss-slider.js</c> owns the track's pointer gestures and suppresses the
/// arrow keys' native page scroll; the dialogs run a JS focus trap. Every test below therefore
/// asserts the library's own browser-level effect (focus moved, panel closed, dropdown opened, value
/// stepped) alongside the consumer's counter.
/// </para>
/// <para>
/// Driven on the standalone <c>/consumer-events</c> route, following
/// <see cref="FocusApiE2ETests"/>'s precedent — the demo gallery deliberately carries no consumer
/// event handlers, and adding them there would perturb existing baselines.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class ConsumerEventChainingE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public ConsumerEventChainingE2ETests(AppFixture app, BrowserFixture browser)
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
        await _page.GotoAsync($"{_app.BaseUrl}/consumer-events", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000, // first-run WASM download can be slow
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "Consumer Events" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    // The two pickers on the page, told apart by what they RENDER rather than by document order, so a
    // future edit to the page can't silently re-point these. (Only the single picker's wrapper carries
    // wss-picker-single; the range one is distinguished by its two-input field.) CSS :has() rather
    // than Playwright's Has option, which re-roots the locator.
    ILocator DatePicker => _page.Locator(".wss-picker-single");
    ILocator RangePicker => _page.Locator(".wss-picker:has(.wss-picker-input-start)");

    ILocator Counter(string id) => _page.Locator($"#{id}");

    // behavior:'instant' -- the app CSS sets html { scroll-behavior: smooth }, so a default scroll
    // animates and anything measured right after it is mid-flight garbage. (Same guard the picker
    // gallery suites use.)
    async Task OpenPickerAsync(ILocator picker)
    {
        var field = picker.Locator(".wss-picker-input");
        await field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await field.ClickAsync();
        await PageTestBase.WaitForOpenAndPositionedAsync(picker.Locator(".wss-picker-dropdown"));
    }

    // ───────────────────────────── EditDate → DatePicker ─────────────────────────────

    [Fact]
    public async Task Date_grid_navigation_still_moves_focus_while_the_consumers_onkeydown_runs()
    {
        await GotoAsync();
        await OpenPickerAsync(DatePicker);

        // The picker's own roving stop is the committed date (Feb 10 2026). Arrowing off it is a
        // handler on the GRID; the keydown then bubbles to the wrapper, which is where the
        // collision was -- so both effects have to be observable from the one keypress.
        var grid = DatePicker.Locator(".wss-picker-dropdown");
        await grid.Locator("[data-date='2026-02-10']").FocusAsync();

        await _page.Keyboard.PressAsync("ArrowRight");

        await Expect(grid.Locator("[data-date='2026-02-11']")).ToBeFocusedAsync(); // library navigation intact
        await Expect(Counter("date-keys")).ToHaveTextAsync("1");                   // consumer's handler ran
    }

    [Fact]
    public async Task Escape_closes_the_date_picker_and_still_reaches_the_consumer()
    {
        await GotoAsync();
        await OpenPickerAsync(DatePicker);

        await _page.Keyboard.PressAsync("Escape");

        await Expect(DatePicker.Locator(".wss-picker-dropdown")).ToBeHiddenAsync();
        await Expect(Counter("date-keys")).ToHaveTextAsync("1");
    }

    // ───────────────────────────── EditDateRange → DateRangePicker ─────────────────────────────

    [Fact]
    public async Task Range_grid_navigation_still_moves_focus_while_the_consumers_onkeydown_runs()
    {
        await GotoAsync();
        await OpenPickerAsync(RangePicker);

        var grid = RangePicker.Locator(".wss-picker-dropdown");
        await grid.Locator("[data-date='2026-02-10'][tabindex='0']").FocusAsync();

        await _page.Keyboard.PressAsync("ArrowRight");

        await Expect(grid.Locator("[data-date='2026-02-11'][tabindex='0']")).ToBeFocusedAsync();
        await Expect(Counter("range-keys")).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task Escape_closes_the_range_picker_and_still_reaches_the_consumer()
    {
        await GotoAsync();
        await OpenPickerAsync(RangePicker);

        await _page.Keyboard.PressAsync("Escape");

        await Expect(RangePicker.Locator(".wss-picker-dropdown")).ToBeHiddenAsync();
        await Expect(Counter("range-keys")).ToHaveTextAsync("1");
    }

    // ───────────────────────────── EditRange ─────────────────────────────

    [Fact]
    public async Task Slider_arrow_keys_still_step_the_value_while_the_consumers_onkeydown_runs()
    {
        await GotoAsync();
        var track = _page.Locator(".edit-range-track");
        await track.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await track.FocusAsync();

        await _page.Keyboard.PressAsync("ArrowRight");

        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "41"); // library stepping intact
        await Expect(Counter("slider-keys")).ToHaveTextAsync("1");       // consumer's handler ran
    }

    [Fact]
    public async Task Slider_click_reaches_the_consumer_even_though_the_drag_module_owns_the_track()
    {
        await GotoAsync();
        var track = _page.Locator(".edit-range-track");
        await track.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");

        // With wss-slider.js live the control's own OffsetX click fallback deliberately early-returns
        // (the pointerdown already reported this press through the drag channel). The consumer's
        // onclick is chained PAST that early return -- and the drag channel still moved the value, so
        // neither half is lost.
        await track.ClickAsync();

        await Expect(Counter("slider-clicks")).ToHaveTextAsync("1");
        await Expect(track).Not.ToHaveAttributeAsync("aria-valuenow", "40");
    }

    // ───────────────────────────── Select engine ─────────────────────────────

    [Fact]
    public async Task Select_still_opens_on_a_wrapper_click_while_the_consumers_onclick_runs()
    {
        await GotoAsync();
        var select = _page.Locator(".wss-select");
        await select.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");

        await select.ClickAsync();

        await PageTestBase.WaitForOpenAndPositionedAsync(select.Locator(".wss-select-dropdown"));
        await Expect(Counter("select-clicks")).ToHaveTextAsync("1");
    }

    // ───────────────────────────── Modal / Drawer ─────────────────────────────

    [Fact]
    public async Task Escape_closes_the_modal_and_still_reaches_the_consumer()
    {
        await GotoAsync();
        await _page.Locator("#open-modal").ClickAsync();
        await Expect(_page.Locator(".wss-modal")).ToBeVisibleAsync();

        // The JS focus trap has already put focus inside the panel, so the keydown bubbles to it --
        // which is the element carrying both the library's Escape handler and (previously) the
        // consumer's discarded one.
        await _page.Keyboard.PressAsync("Escape");

        await Expect(_page.Locator(".wss-modal")).ToBeHiddenAsync();
        await Expect(Counter("modal-keys")).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task Escape_closes_the_drawer_and_still_reaches_the_consumer()
    {
        await GotoAsync();
        await _page.Locator("#open-drawer").ClickAsync();
        await Expect(_page.Locator(".wss-drawer")).ToBeVisibleAsync();

        await _page.Keyboard.PressAsync("Escape");

        await Expect(_page.Locator(".wss-drawer")).ToBeHiddenAsync();
        await Expect(Counter("drawer-keys")).ToHaveTextAsync("1");
    }
}
