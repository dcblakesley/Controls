namespace FormTesting.Client.E2ETests;

/// <summary>
/// The only proof that <c>FocusAsync()</c> actually MOVES focus. Both of its channels bottom out in
/// JS interop (<see cref="Microsoft.AspNetCore.Components.ElementReference"/>'s own focus call, and
/// <c>WssEditControls.focusGroupInput</c> for the radio/checkbox groups), so bUnit can only show that
/// the right call was issued — <c>FocusApiTests</c> does that part. Here a real browser answers the
/// question that matters: is <c>document.activeElement</c> the element the contract names?
/// </summary>
/// <remarks>
/// <para>
/// Every assertion below is preceded by a button CLICK, which puts focus on the button. So each
/// <c>ToBeFocusedAsync</c> can only pass if <c>FocusAsync()</c> pulled focus off the button and onto
/// the field — there is no starting state that makes one of these pass by accident.
/// </para>
/// <para>
/// One class covering a representative control per mechanism, rather than the usual one-class-per-
/// control split: the mechanisms are what vary (a captured <c>_editorRef</c>, a forwarded picker/select
/// reference, <c>InputFile.Element</c>, the group-query JS), and the rest of the library reaches focus
/// through one of these four. Drives <c>/focus-api</c> directly, following
/// <see cref="JsInteropFallbackE2ETests"/>'s precedent for a standalone test-only route.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class FocusApiE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public FocusApiE2ETests(AppFixture app, BrowserFixture browser)
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

    async Task GotoAsync(string? auto = null)
    {
        var url = $"{_app.BaseUrl}/focus-api";
        if (auto is not null) url += $"?auto={Uri.EscapeDataString(auto)}";

        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000, // first-run WASM download can be slow
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "Focus API" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    // Clicks the button and asserts focus ended up on `expected` rather than staying on the button.
    async Task ExpectFocusMovesAsync(string buttonId, string expectedSelector)
    {
        await _page.Locator($"#{buttonId}").ClickAsync();
        await Expect(_page.Locator($"#{buttonId}")).Not.ToBeFocusedAsync();
        await Expect(_page.Locator(expectedSelector)).ToBeFocusedAsync();
    }

    // ───────────── captured _editorRef: the scalar single-editor controls ─────────────

    [Fact]
    public async Task EditString_FocusAsync_moves_focus_to_its_input()
    {
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-string", "input#Text");
    }

    [Fact]
    public async Task EditNumber_FocusAsync_moves_focus_to_its_input()
    {
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-number", "input#Number");
    }

    [Fact]
    public async Task EditBool_FocusAsync_moves_focus_to_its_checkbox()
    {
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-bool", "input#Flag");
    }

    // ───────────── forwarded child references: pickers and the select engine ─────────────

    [Fact]
    public async Task EditDateRange_FocusAsync_moves_focus_to_the_START_input()
    {
        // Not the End input, and not "whichever end was last active" -- see DateRangePicker's
        // PrimaryInputRef. #Start is the Start field's own id; the End input is #Start-end.
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-range", "input#Start");
        await Expect(_page.Locator("input#Start-end")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task EditSelectSearch_FocusAsync_moves_focus_to_the_combobox_input()
    {
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-search", "input#Choice");
    }

    // ───────────── the group-query channel: radios and checkbox lists ─────────────

    [Fact]
    public async Task EditRadioEnum_FocusAsync_lands_on_the_CHECKED_radio_not_the_first_one()
    {
        // The bound value starts at High, the third option, so the two candidate rules disagree and
        // only "focus the checked radio" (real radiogroup Tab semantics) puts focus here.
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-radio", "input#rb-Priority-High");
        await Expect(_page.Locator("input#rb-Priority-Low")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task EditCheckedStringList_FocusAsync_lands_on_the_first_ENABLED_checkbox()
    {
        // "a" is disabled and "c" is ticked, so "b" is neither the first box nor the checked one --
        // the mirror image of the radio rule, and the whole reason the shared JS helper takes a
        // preferChecked flag.
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-list", "input#cbx-Tags-b");
        await Expect(_page.Locator("input#cbx-Tags-a")).Not.ToBeFocusedAsync();
        await Expect(_page.Locator("input#cbx-Tags-c")).Not.ToBeFocusedAsync();
    }

    // ───────────── the never-throws contract, in a real browser ─────────────

    [Fact]
    public async Task FocusAsync_on_a_read_only_control_is_silent_and_leaves_focus_alone()
    {
        // Read-only renders a display value, not an editor, so there is nothing to focus. The contract
        // is a no-op, not an error -- and specifically not an unhandled exception reaching the page,
        // which on Blazor Server would tear the circuit down.
        await GotoAsync();
        var pageErrors = new List<string>();
        _page.PageError += (_, error) => pageErrors.Add(error);

        await _page.Locator("#focus-readonly").ClickAsync();
        await _page.WaitForTimeoutAsync(300);

        Assert.Empty(pageErrors);
        // Focus stayed where the click left it rather than jumping somewhere arbitrary.
        await Expect(_page.Locator("#focus-readonly")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task FocusAsync_on_a_DISABLED_EditRange_leaves_focus_where_it_was()
    {
        // The one control the disabled contract could not hold structurally. Every other control's
        // focus target is a native element carrying `disabled`, where .focus() is a browser no-op;
        // EditRange's is a role="slider" <div> with tabindex="-1", which is out of the Tab order but
        // still fully focusable from script -- so this used to pull focus off the button and park it
        // on a control whose OnKeyDown early-returns. Asserted in a browser rather than bUnit because
        // "tabindex=-1 is still programmatically focusable" is a DOM fact, not a C# one.
        await GotoAsync();

        await _page.Locator("#focus-disabled-range").ClickAsync();
        await _page.WaitForTimeoutAsync(300);

        await Expect(_page.Locator("#focus-disabled-range")).ToBeFocusedAsync();
        await Expect(_page.Locator("div#Volume[role=slider]")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task FocusAsync_is_repeatable_across_an_intervening_focus_change()
    {
        await GotoAsync();
        await ExpectFocusMovesAsync("focus-string", "input#Text");
        await ExpectFocusMovesAsync("focus-number", "input#Number");
        await ExpectFocusMovesAsync("focus-string", "input#Text");
    }

    // ───────────────────────────────── FocusOnFirstRender ─────────────────────────────────

    [Fact]
    public async Task FocusOnFirstRender_lands_focus_on_the_control_once_the_page_is_interactive()
    {
        // The declarative form, and the reason the caveat is documented: the focus can't happen during
        // prerender (this page renders InteractiveWebAssembly with prerendering on), so it lands on the
        // first INTERACTIVE render instead -- which is exactly what this asserts.
        await GotoAsync(auto: "string");

        await Expect(_page.Locator("input#Text")).ToBeFocusedAsync(new() { Timeout = 15_000 });
    }

    // ───────────── FocusOnFirstRender inside an overlay (Modal/Drawer) ─────────────

    [Fact]
    public async Task FocusOnFirstRender_inside_a_Modal_beats_the_overlays_own_initial_focus()
    {
        // Modal gates its children on @if (Visible), so a child control's FIRST render coincides with
        // the open -- and the overlay's activateModal grabs initial focus at the end of that same
        // cycle. The two genuinely race, and the child used to lose: the measured focusin/focusout
        // order was `IN input#ModalText >> OUT input#ModalText >> IN button.wss-modal-close`, i.e. the
        // control focused itself and the overlay then yanked focus onto the close button. wss-overlay
        // now skips its initial focus when something inside the panel already has it.
        await GotoAsync();
        await _page.Locator("#open-modal").ClickAsync();
        await Expect(_page.Locator(".wss-modal")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Expect(_page.Locator("input#ModalText")).ToBeFocusedAsync();
        await Expect(_page.Locator(".wss-modal-close")).Not.ToBeFocusedAsync();
        // ...and it STAYS there: the trap's focusin handler only re-routes focus that lands OUTSIDE
        // the panel, so it must not fight a legitimate in-panel focus a beat later.
        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#ModalText")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task FocusOnFirstRender_defaults_off_so_a_plain_load_focuses_nothing()
    {
        await GotoAsync();

        await Expect(_page.Locator("input#Text")).Not.ToBeFocusedAsync();
        // ...and no OTHER field on the page grabbed it either. Asserting document.activeElement is
        // <body> would be wrong: the app's router applies its own focus-on-navigation (the heading),
        // which is nothing to do with this parameter. What matters is that no form control took focus.
        Assert.False(await _page.EvaluateAsync<bool>(
            "() => ['INPUT','TEXTAREA','SELECT'].includes(document.activeElement?.tagName)"));
    }
}
