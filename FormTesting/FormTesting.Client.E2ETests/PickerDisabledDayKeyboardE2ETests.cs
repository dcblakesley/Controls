namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the one picker behavior that only a real browser can prove: a calendar cell the
/// picker rejects (Min/Max/DisabledDate) renders <c>aria-disabled</c> instead of the native
/// <c>disabled</c> attribute, so arrow navigation can still put real DOM focus on it.
/// </summary>
/// <remarks>
/// bUnit can assert the attributes and the roving-tabindex state, but not what the BROWSER does with
/// them, and that was the whole bug: a natively <c>disabled</c> button refuses <c>.focus()</c> (the
/// focus ring stalls mid-run) and is not a tab stop (so the grid's sole <c>tabindex="0"</c> landing on
/// one left the panel unreachable), and when a view-crossing re-render patched the focused slot into a
/// disabled one the browser blurred focus to <c>&lt;body&gt;</c> — out of reach of the picker's own
/// Escape/arrow handlers, with <c>wss-overlay.js</c>'s focus-out close early-returning on the null
/// <c>relatedTarget</c> so the panel didn't dismiss either.
/// <para>
/// Driven on the /uikit gallery's <c>#demo-range-disabled</c> picker — the only demo with disabled
/// cells inside the initially-shown view (weekends, via <c>DisabledDate</c>) and pinned to Feb/Mar
/// 2026, so every date below is deterministic. Feb 1 2026 and Mar 1 2026 are both Sundays, so the
/// left panel's 42-cell grid spans Feb 1 - Mar 14 and the right panel's spans Mar 1 - Apr 11; every
/// February date used here therefore exists exactly once in the DOM.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class PickerDisabledDayKeyboardE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public PickerDisabledDayKeyboardE2ETests(AppFixture app, BrowserFixture browser)
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

    // Same /uikit stabilization the other gallery suites use: wait for the page height to stop moving
    // before any geometry- or focus-sensitive step (see DateRangePickerE2ETests.GotoAsync for the full
    // story on the late layout shifts).
    async Task GotoAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
        await _page.WaitForFunctionAsync(
            @"() => {
                const h = document.body.scrollHeight;
                if (window.__wssLastHeight !== h) { window.__wssLastHeight = h; window.__wssStableSince = Date.now(); }
                return Date.now() - window.__wssStableSince > 600;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    ILocator Picker => _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-range-disabled") });
    ILocator Field => Picker.Locator(".wss-picker-input");
    ILocator Dropdown => Picker.Locator(".wss-picker-dropdown");
    ILocator Result => _page.Locator("[data-test-id='range-disabled-result']");

    // behavior:'instant' — the app CSS sets html { scroll-behavior: smooth }, so a default scroll
    // animates and anything measured right after it is mid-flight garbage.
    async Task OpenAsync()
    {
        await Field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await Field.ClickAsync();
        await PageTestBase.WaitForOpenAndPositionedAsync(Dropdown);
    }

    ILocator Day(string date) => Dropdown.Locator($"[data-date='{date}']");

    // The roving-tabindex stop, for dates that render in BOTH panels (a date near a panel boundary
    // exists twice: once as the real in-month cell, once as the neighbouring panel's dimmed
    // leading/trailing duplicate). C# only ever puts tabindex="0" on the real one.
    ILocator FocusStop(string date) => Dropdown.Locator($"[data-date='{date}'][tabindex='0']");

    Task<string?> ActiveDateAsync() =>
        _page.EvaluateAsync<string?>("() => document.activeElement && document.activeElement.getAttribute('data-date')");

    Task<bool> FocusIsInsidePanelAsync() =>
        _page.EvaluateAsync<bool>("() => !!document.activeElement && !!document.activeElement.closest('.wss-picker-dropdown')");

    [Fact]
    public async Task Arrowing_across_a_disabled_run_keeps_dom_focus_moving_day_by_day()
    {
        await GotoAsync();
        await OpenAsync();

        // Navigation always starts from the picker's OWN roving stop (C# tracks the target date, not
        // whatever the DOM happens to have focused), which here is the committed start endpoint.
        await Expect(Day("2026-02-02")).ToHaveAttributeAsync("tabindex", "0");
        await Day("2026-02-02").FocusAsync();

        // Mon Feb 2 -> Fri Feb 6, all enabled.
        foreach (var date in new[] { "2026-02-03", "2026-02-04", "2026-02-05", "2026-02-06" })
        {
            await _page.Keyboard.PressAsync("ArrowRight");
            await Expect(Day(date)).ToBeFocusedAsync();
        }

        // Sat Feb 7 and Sun Feb 8 are DisabledDate-rejected. With the old native `disabled` attribute
        // the browser refused .focus() on both, so the ring stalled on Feb 6 for two keypresses and
        // the steps across the weekend went unannounced.
        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(Day("2026-02-07")).ToBeFocusedAsync();
        await Expect(Day("2026-02-07")).ToHaveAttributeAsync("aria-disabled", "true");
        Assert.False(await Day("2026-02-07").EvaluateAsync<bool>("el => el.disabled"));
        // ...and the grid still has a real tab stop, on that same disabled cell.
        await Expect(Day("2026-02-07")).ToHaveAttributeAsync("tabindex", "0");

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(Day("2026-02-08")).ToBeFocusedAsync();

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(Day("2026-02-09")).ToBeFocusedAsync();
        await Expect(Day("2026-02-09")).Not.ToHaveAttributeAsync("aria-disabled", "true");
    }

    [Fact]
    public async Task Activating_a_focused_disabled_day_commits_nothing_and_leaves_the_panel_open()
    {
        await GotoAsync();
        await OpenAsync();

        var before = await Result.TextContentAsync();

        // A native Enter on a focused <button> synthesizes a click, which the browser used to swallow
        // for a `disabled` button. It now reaches the component, where the commit guard rejects it.
        await Day("2026-02-07").FocusAsync();
        await _page.Keyboard.PressAsync("Enter");

        await Expect(Dropdown).ToBeVisibleAsync();
        Assert.Equal(before, await Result.TextContentAsync());
        Assert.True(await FocusIsInsidePanelAsync());
    }

    [Fact]
    public async Task A_view_crossing_move_onto_a_disabled_day_never_strands_focus_on_body()
    {
        await GotoAsync();
        await OpenAsync();

        // Walk the roving stop to Sat Mar 28 (End jumps to the focused week's Saturday; PageDown steps
        // a month; ArrowDown steps a week), then step off the end of the shown pair onto Sat Apr 4 --
        // outside BOTH months, so the whole dual panel re-renders on the way, AND disabled.
        //
        // That re-render is what used to blur focus to <body>: Blazor patches the fixed 42 cells in
        // place, so the focused slot became a `disabled` button under the browser's feet. From <body>
        // arrows and Escape no longer reached the picker's handlers, and wss-overlay.js's focus-out
        // close early-returns on the null relatedTarget, so the panel didn't dismiss either -- there
        // was no way back in short of the mouse or a full document Tab cycle.
        await Day("2026-02-02").FocusAsync();
        await _page.Keyboard.PressAsync("End");                     // Sat Feb 7
        await Expect(Day("2026-02-07")).ToBeFocusedAsync();
        await _page.Keyboard.PressAsync("PageDown");                // Sat Mar 7 (right panel)
        await Expect(FocusStop("2026-03-07")).ToBeFocusedAsync();
        foreach (var date in new[] { "2026-03-14", "2026-03-21", "2026-03-28" })
        {
            await _page.Keyboard.PressAsync("ArrowDown");
            await Expect(FocusStop(date)).ToBeFocusedAsync();
        }

        await _page.Keyboard.PressAsync("ArrowDown");               // Sat Apr 4 -- crosses the view

        await Expect(FocusStop("2026-04-04")).ToBeFocusedAsync();
        Assert.Equal("2026-04-04", await ActiveDateAsync());
        Assert.True(await FocusIsInsidePanelAsync());
        await Expect(FocusStop("2026-04-04")).ToHaveAttributeAsync("aria-disabled", "true");

        // The real proof that focus never left the panel: Escape still reaches the wrapper's keydown.
        await _page.Keyboard.PressAsync("Escape");
        await Expect(Dropdown).Not.ToBeVisibleAsync();
    }
}
