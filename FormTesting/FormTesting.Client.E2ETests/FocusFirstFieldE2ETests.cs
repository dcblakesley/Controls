namespace FormTesting.Client.E2ETests;

/// <summary>
/// The only proof that <c>FormDefaults.FocusFirstField</c> actually MOVES focus, and to the right
/// element. The whole resolution — which field is first in document order, and which candidates are
/// skipped for being disabled / readonly / <c>tabindex="-1"</c> / not rendered — happens in
/// <c>WssEditControls.focusFirstField</c>, so bUnit can only show that the interop call was issued
/// with the right scope id (<c>FocusFirstFieldTests</c> does that part). Here a real browser answers
/// the questions that matter: what is <c>document.activeElement</c>, and does it STAY there?
/// </summary>
/// <remarks>
/// Drives <c>/focus-first-field</c> directly (one scenario per <c>?case=</c>), following
/// <see cref="FocusApiE2ETests"/>'s precedent for a standalone test-only route. The default case is
/// deliberately the feature-OFF one, so the "changes nothing when unset" assertion runs against a
/// plain visit rather than a special page.
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class FocusFirstFieldE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public FocusFirstFieldE2ETests(AppFixture app, BrowserFixture browser)
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

    async Task GotoAsync(string? scenario = null)
    {
        var url = $"{_app.BaseUrl}/focus-first-field";
        if (scenario is not null) url += $"?case={Uri.EscapeDataString(scenario)}";

        await _page.GotoAsync(url, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000, // first-run WASM download can be slow
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "Focus First Field" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    // True when SOME form control on the page holds focus. Asserting document.activeElement is <body>
    // would be wrong: the app's router applies its own focus-on-navigation (the heading), which has
    // nothing to do with this feature -- see FocusApiE2ETests' matching helper.
    Task<bool> AnyFieldFocusedAsync() => _page.EvaluateAsync<bool>(
        "() => ['INPUT','TEXTAREA','SELECT'].includes(document.activeElement?.tagName)");

    // ───────────────────────────── the basic contract ─────────────────────────────

    [Fact]
    public async Task Focuses_the_first_field_in_the_scope()
    {
        await GotoAsync("plain");

        await Expect(_page.Locator("input#PlainFirst")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#PlainSecond")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task Fires_once_and_leaves_focus_alone_afterwards()
    {
        // "Once per instance, on first render" -- so typing (which re-renders the control, the form
        // and its validation state) must not pull focus back to the first field.
        await GotoAsync("plain");
        await Expect(_page.Locator("input#PlainFirst")).ToBeFocusedAsync(new() { Timeout = 15_000 });

        await _page.Locator("input#PlainSecond").ClickAsync();
        await _page.Locator("input#PlainSecond").PressSequentiallyAsync("typing here");
        await _page.WaitForTimeoutAsync(400);

        await Expect(_page.Locator("input#PlainSecond")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#PlainFirst")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task Unset_changes_nothing_and_focuses_no_field_at_all()
    {
        await GotoAsync(); // default case: FormDefaults present, FocusFirstField unset

        await Expect(_page.Locator("input#OffFirst")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await _page.WaitForTimeoutAsync(400);

        Assert.False(await AnyFieldFocusedAsync());
        // ...and the scope markers the feature needs were never rendered, so the DOM is exactly what
        // it was before the feature existed.
        Assert.Equal(0, await _page.Locator("template[id^='wss-focus-scope-']").CountAsync());
    }

    // ─────────────────── asynchronous forms: the bounded retry ───────────────────

    [Fact]
    public async Task A_form_whose_fields_arrive_later_still_gets_focused()
    {
        // The scope mounts empty and the fields arrive ~250ms later -- the ordinary "load the model
        // in OnInitializedAsync, render the form when it arrives" page. A single first-render attempt
        // found no candidate and there was never another, so this focused NOTHING, ever.
        await GotoAsync("async");

        // Proof the scope really was empty when it first rendered (otherwise this passes for the
        // wrong reason): the placeholder is on screen and no field exists yet.
        await Expect(_page.Locator("#async-loading")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        Assert.Equal(0, await _page.Locator("input#AsyncFirst").CountAsync());

        await Expect(_page.Locator("input#AsyncFirst")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#AsyncSecond")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_late_arriving_form_does_not_pull_focus_off_a_field_the_user_is_using()
    {
        // The hazard the retry could have introduced. The "never take focus off a field that already
        // has it" guard still applies to every attempt, so a user who clicked into something before
        // the form arrived keeps their caret -- and the scope settles rather than trying again.
        await GotoAsync("async");
        await Expect(_page.Locator("#async-loading")).ToBeVisibleAsync(new() { Timeout = 15_000 });

        // A field OUTSIDE the armed scope, focused while the scope is still empty.
        await _page.EvaluateAsync(
            "() => { const i = document.createElement('input'); i.id = 'outside-typing';"
            + " document.body.appendChild(i); i.focus(); }");
        await Expect(_page.Locator("input#outside-typing")).ToBeFocusedAsync();

        await Expect(_page.Locator("input#AsyncFirst")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await _page.WaitForTimeoutAsync(500);

        await Expect(_page.Locator("input#outside-typing")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#AsyncFirst")).Not.ToBeFocusedAsync();
    }

    // ───────────────────────────── the skip rules ─────────────────────────────

    [Fact]
    public async Task Skips_disabled_readonly_and_hidden_fields()
    {
        // Three fields precede the target, each unusable for a different reason. Only a DOM-side
        // answer gets this right -- "the first registered field" would land on the disabled one.
        await GotoAsync("skip");

        await Expect(_page.Locator("input#SkipTarget")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#SkipDisabled")).Not.ToBeFocusedAsync();
        await Expect(_page.Locator("input#SkipReadOnly")).Not.ToBeFocusedAsync();
        // The hidden one isn't rendered at all, which is exactly why it can't be a candidate.
        Assert.Equal(0, await _page.Locator("input#SkipHidden").CountAsync());
    }

    [Fact]
    public async Task Skips_an_aria_disabled_checkbox_that_carries_no_disabled_attribute()
    {
        // EditBool + IsDisabled + AllowFocusWhenDisabled (the DEFAULT) renders aria-disabled="true",
        // tabindex="0" and NO `disabled` attribute, so that a screen-reader user can still reach and
        // read it. Testing el.disabled alone therefore saw a perfectly ordinary focusable checkbox
        // and landed on it -- against the documented skip list, and on a control the user can't act on.
        await GotoAsync("aria-disabled");

        await Expect(_page.Locator("input#AfterDisabledBool")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#DisabledFlag")).Not.ToBeFocusedAsync();
        // The premise of the test, pinned: no `disabled` attribute, so only aria-disabled can tell.
        Assert.Equal("true", await _page.Locator("input#DisabledFlag").GetAttributeAsync("aria-disabled"));
        Assert.False(await _page.Locator("input#DisabledFlag").IsDisabledAsync());
    }

    [Fact]
    public async Task A_non_searchable_select_is_a_candidate_despite_its_readonly_attribute()
    {
        // ShowSearch="false" renders the combobox readonly purely as a typing guard, and says so with
        // an explicit aria-readonly="false": arrows, Enter, Space and type-ahead all still change the
        // value. Skipping it on `readOnly` alone stranded focus on the NEXT field, past a control the
        // user could have used.
        await GotoAsync("select-no-search");

        await Expect(_page.Locator("input#NoSearchChoice")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#AfterSelect")).Not.ToBeFocusedAsync();
        Assert.Equal("false", await _page.Locator("input#NoSearchChoice").GetAttributeAsync("aria-readonly"));
    }

    [Fact]
    public async Task A_non_searchable_multi_select_is_a_candidate_too()
    {
        // Same engine, same combobox input -- pinned separately because EditMultiSelect declares its
        // own ShowSearch pass-through and could drift from EditSelectSearch's.
        await GotoAsync("multi-select-no-search");

        await Expect(_page.Locator("input#NoSearchTags")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#AfterMultiSelect")).Not.ToBeFocusedAsync();
    }

    // ───────────────── precedence: an explicit FocusOnFirstRender wins ─────────────────

    [Fact]
    public async Task An_explicit_FocusOnFirstRender_on_a_later_control_wins()
    {
        // Both mechanisms arm in the same render cycle and genuinely race. A consumer who named a
        // specific field meant it, so the named one must win in EITHER order -- and must not then be
        // yanked back a beat later by the form-level default.
        await GotoAsync("explicit");

        await Expect(_page.Locator("input#ExplicitSecond")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("input#ExplicitFirst")).Not.ToBeFocusedAsync();

        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#ExplicitSecond")).ToBeFocusedAsync();
    }

    // ───────────────────────────── scoping ─────────────────────────────

    [Fact]
    public async Task A_second_form_in_the_same_scope_does_not_steal_focus_from_the_first()
    {
        await GotoAsync("two-forms");

        await Expect(_page.Locator("input#FormOneField")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await _page.WaitForTimeoutAsync(400);
        await Expect(_page.Locator("input#FormOneField")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#FormTwoField")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task A_second_armed_scope_does_not_steal_focus_from_the_first()
    {
        // Two independently armed FormDefaults. Both fire; the guard ("never take focus off a field
        // that already has it") is what stops the later one winning by accident.
        await GotoAsync("sibling-scopes");

        await Expect(_page.Locator("input#ScopeAField")).ToBeFocusedAsync(new() { Timeout = 15_000 });
        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#ScopeAField")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#ScopeBField")).Not.ToBeFocusedAsync();
    }

    [Fact]
    public async Task An_empty_scope_does_not_reach_past_its_own_closing_marker()
    {
        // The end marker earns its keep here: the armed scope contains no field, and the field that
        // follows </FormDefaults> in the same parent is NOT in scope, so nothing is focused.
        await GotoAsync("bounded");

        await Expect(_page.Locator("input#OutsideField")).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await _page.WaitForTimeoutAsync(400);

        await Expect(_page.Locator("input#OutsideField")).Not.ToBeFocusedAsync();
        Assert.False(await AnyFieldFocusedAsync());
    }

    // ───────────── the primary use case: a form in a dialog ─────────────

    [Fact]
    public async Task Inside_a_Modal_focus_lands_on_the_first_field_and_stays_there()
    {
        // Modal gates its children on @if (Visible), so the FormDefaults inside it first renders as
        // the dialog opens -- the same cycle in which wss-overlay.js's activateModal takes its own
        // initial focus (the close X, being the first focusable element in the panel). The two are
        // timing-sensitive by construction, and the pair of guards is what makes the result stable in
        // either order: activateModal skips its grab when something in the panel already has focus,
        // and focusFirstField skips its own when a FIELD already has focus (a button, close X
        // included, deliberately does not block it).
        await GotoAsync("modal");
        // Record every focusin from before the open, so the assertions below can be about the SETTLED
        // outcome rather than a lucky instant -- and so a regression reports the actual sequence.
        await _page.EvaluateAsync(
            "() => { window.__wssFocusLog = []; document.addEventListener('focusin', e =>"
            + " window.__wssFocusLog.push(e.target.id || e.target.className || e.target.tagName)); }");

        await _page.Locator("#open-modal").ClickAsync();
        await Expect(_page.Locator(".wss-modal")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Expect(_page.Locator("input#ModalFirst")).ToBeFocusedAsync(new() { Timeout = 10_000 });
        await Expect(_page.Locator(".wss-modal-close")).Not.ToBeFocusedAsync();

        // ...and it STAYS: the assertion above can pass at one instant and still be wrong if the
        // overlay's own initial focus lands a beat later. Half a second is well past both.
        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#ModalFirst")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#ModalSecond")).Not.ToBeFocusedAsync();

        // The settled end of the sequence, not just its current state: whatever order the overlay's
        // activation and this feature landed in, the LAST focus move of the open must be onto the
        // first field. Measured sequence is `open-modal >> ModalFirst` -- a single move into the
        // dialog. The assertion is on the tail rather than the whole log so an implementation that
        // corrects a beat later (close X first, then the field) stays legal here; the REOPEN test
        // below is the one that pins the single move.
        var focusLog = await _page.EvaluateAsync<string[]>("() => window.__wssFocusLog");
        Assert.Equal("ModalFirst", focusLog[^1]);
    }

    [Fact]
    public async Task Reopening_a_Modal_still_takes_one_move_straight_to_the_field()
    {
        // The first open and every REOPEN used to differ, and only the module cache decided it: on
        // the first open wss-overlay.js is still being imported when the scope fires, so focus goes
        // straight to the field; on a reopen the module is cached, activateModal ran first, and the
        // sequence was `wss-modal-close >> ModalFirst` -- two moves, with a screen reader announcing
        // the close button before the thing the user came for. activateModal now offers the open to
        // an in-panel FocusFirstField scope before its own grab, which makes both opens identical.
        await GotoAsync("modal");
        await _page.Locator("#open-modal").ClickAsync();
        await Expect(_page.Locator("input#ModalFirst")).ToBeFocusedAsync(new() { Timeout = 10_000 });

        await _page.Locator(".wss-modal-close").ClickAsync();
        await Expect(_page.Locator(".wss-modal")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

        // Record the REOPEN only -- the close and its focus restore are not what's under test.
        await _page.EvaluateAsync(
            "() => { window.__wssFocusLog = []; document.addEventListener('focusin', e =>"
            + " window.__wssFocusLog.push(e.target.id || e.target.className || e.target.tagName)); }");

        await _page.Locator("#open-modal").ClickAsync();
        await Expect(_page.Locator("input#ModalFirst")).ToBeFocusedAsync(new() { Timeout = 10_000 });
        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#ModalFirst")).ToBeFocusedAsync();

        var focusLog = await _page.EvaluateAsync<string[]>("() => window.__wssFocusLog");
        Assert.Equal("ModalFirst", focusLog[^1]);
        Assert.DoesNotContain(focusLog, entry => entry.Contains("wss-modal-close"));
    }

    [Fact]
    public async Task Inside_a_Drawer_focus_lands_on_the_first_field_and_stays_there()
    {
        // Drawer runs the exact same activateModal path as Modal, so it gets the same treatment --
        // measured sequence `open-drawer >> DrawerFirst`.
        await GotoAsync("drawer");
        await _page.EvaluateAsync(
            "() => { window.__wssFocusLog = []; document.addEventListener('focusin', e =>"
            + " window.__wssFocusLog.push(e.target.id || e.target.className || e.target.tagName)); }");

        await _page.Locator("#open-drawer").ClickAsync();
        await Expect(_page.Locator(".wss-drawer")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Expect(_page.Locator("input#DrawerFirst")).ToBeFocusedAsync(new() { Timeout = 10_000 });
        await Expect(_page.Locator(".wss-drawer-close").First).Not.ToBeFocusedAsync();

        await _page.WaitForTimeoutAsync(500);
        await Expect(_page.Locator("input#DrawerFirst")).ToBeFocusedAsync();
        await Expect(_page.Locator("input#DrawerSecond")).Not.ToBeFocusedAsync();

        var focusLog = await _page.EvaluateAsync<string[]>("() => window.__wssFocusLog");
        Assert.Equal("DrawerFirst", focusLog[^1]);
    }

    [Fact]
    public async Task Nothing_is_focused_before_the_dialog_opens()
    {
        // The scope inside a closed Modal has not rendered, so the feature is dormant -- proving the
        // dialog assertion above measures the OPEN, not something the page did on load.
        await GotoAsync("modal");
        await _page.WaitForTimeoutAsync(400);

        Assert.False(await AnyFieldFocusedAsync());
        Assert.Equal(0, await _page.Locator("template[id^='wss-focus-scope-']").CountAsync());
    }
}
