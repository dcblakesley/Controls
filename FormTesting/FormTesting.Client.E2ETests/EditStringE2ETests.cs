using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditStringE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.String;

    [Fact]
    public async Task First_input_accepts_text_and_round_trips_to_bound_value_display()
    {
        await NavigateAsync();
        // Turn on "Display bound values" so we can verify the @bind round-trip from the DOM.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var input = Page.Locator("section.demo-section").First.Locator("input.edit-string-input").First;
        await input.FillAsync("hello world");
        await input.PressAsync("Tab"); // commit binding on blur

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("hello world");
    }

    [Fact]
    public async Task Tooltip_escape_dismisses_while_the_trigger_stays_focused()
    {
        await NavigateAsync();
        var trigger = Page.Locator(".edit-tooltip-container").First;
        var content = Page.Locator(".edit-tooltip-content").First;

        // Focus shows the tooltip (keyboard path).
        await trigger.FocusAsync();
        await Expect(content).ToBeVisibleAsync();

        // Escape must dismiss it even though the trigger keeps focus (WCAG 1.4.13) — the CSS
        // :focus reveal used to override the aria-hidden state and keep it visible until blur.
        await Page.Keyboard.PressAsync("Escape");
        await Expect(content).Not.ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();

        // Re-triggering still works after a dismissal.
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(content).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Tooltip_stays_visible_while_the_pointer_travels_onto_the_bubble()
    {
        await NavigateAsync();
        var trigger = Page.Locator(".edit-tooltip-container").First;
        var content = Page.Locator(".edit-tooltip-content").First;

        // Hover the icon. The reveal sits behind a 0.35s hover-intent delay plus the aria flip,
        // so let the assertion retry.
        var t = await trigger.BoundingBoxAsync();
        Assert.NotNull(t);
        await Page.Mouse.MoveAsync((float)(t.X + t.Width / 2), (float)(t.Y + t.Height / 2));
        await Expect(content).ToBeVisibleAsync();

        // WCAG 1.4.13 "hoverable": travel straight down from the icon, through the gap bridge,
        // onto the bubble — the tooltip must not dismiss mid-travel or while the pointer rests on
        // it. Steps make Playwright fire intermediate moves like a real pointer.
        var c = await content.BoundingBoxAsync();
        Assert.NotNull(c);
        await Page.Mouse.MoveAsync((float)(t.X + t.Width / 2), (float)(c.Y + c.Height / 2), new() { Steps = 12 });
        await Page.WaitForTimeoutAsync(400); // outlive any wrongly-scheduled hide round-trip
        await Expect(content).ToBeVisibleAsync();

        // Leaving both the trigger and the bubble dismisses it.
        await Page.Mouse.MoveAsync((float)(c.X + c.Width + 100), (float)(c.Y + c.Height + 100));
        await Expect(content).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Tooltip_auto_places_toward_the_viewport_center()
    {
        await NavigateAsync();
        var trigger = Page.Locator(".edit-tooltip-container").First;
        var content = Page.Locator(".edit-tooltip-content").First;

        // Near the top of the viewport the bubble must open BELOW the trigger — the data-tooltip
        // placement convention (via wss-tooltip.js), replacing the old hardcoded always-above CSS.
        // behavior:'instant' because the host page sets scroll-behavior:smooth, which would leave
        // the geometry mid-animation.
        await trigger.EvaluateAsync("el => el.scrollIntoView({ block: 'start', behavior: 'instant' })");
        await trigger.FocusAsync();
        await Expect(content).ToBeVisibleAsync();

        var triggerBox = await trigger.BoundingBoxAsync();
        var contentBox = await content.BoundingBoxAsync();
        Assert.NotNull(triggerBox);
        Assert.NotNull(contentBox);
        Assert.True(contentBox.Y > triggerBox.Y + triggerBox.Height,
            $"bubble top ({contentBox.Y}) should sit below the trigger bottom ({triggerBox.Y + triggerBox.Height})");

        // Shrink the viewport and pin the same trigger to its bottom edge: the placer must now
        // flip the bubble above (wss-tooltip-top). Placement recomputes on the next focusin, so
        // blur first.
        await trigger.BlurAsync();
        var absoluteY = await trigger.EvaluateAsync<double>("el => el.getBoundingClientRect().top + window.scrollY");
        await Page.SetViewportSizeAsync(1280, Math.Max(100, (int)absoluteY - 20));
        await trigger.EvaluateAsync("el => el.scrollIntoView({ block: 'end', behavior: 'instant' })");
        await trigger.FocusAsync();
        await Expect(content).ToBeVisibleAsync();
        await Expect(trigger).ToHaveClassAsync(new Regex(@"\bwss-tooltip-top\b"));

        var flippedTriggerBox = await trigger.BoundingBoxAsync();
        var flippedContentBox = await content.BoundingBoxAsync();
        Assert.NotNull(flippedTriggerBox);
        Assert.NotNull(flippedContentBox);
        Assert.True(flippedContentBox.Y + flippedContentBox.Height < flippedTriggerBox.Y,
            $"bubble bottom ({flippedContentBox.Y + flippedContentBox.Height}) should sit above the trigger top ({flippedTriggerBox.Y})");
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_swaps_inputs_for_ReadOnlyValue()
    {
        await NavigateAsync();
        // Edit-mode toggle starts true; click to enter read-only.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        // The first demo-section's first EditString should no longer render its input.
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator("input.edit-string-input").First).Not.ToBeVisibleAsync();
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Percentage_width_on_the_input_resolves_against_the_control_column()
    {
        // Regression: .edit-input-with-icon used to shrink-wrap (align-self: flex-start), which made
        // a consumer width:100% on the editor circular per the CSS sizing spec — it silently resolved
        // to auto and the input stayed at its intrinsic default size. The custom-styling demo section
        // sets width:100%; the input must now span (nearly) its purple container's inner width.
        await NavigateAsync();
        var input = Page.Locator("input.my-custom-input");
        var container = Page.Locator(".my-custom-container");

        var inputBox = await input.BoundingBoxAsync();
        var containerBox = await container.BoundingBoxAsync();
        Assert.NotNull(inputBox);
        Assert.NotNull(containerBox);

        // Container has 10px padding per side; allow slack for borders/rounding. A collapsed input
        // renders ~180px (Chromium's default size="20" width), far below this threshold.
        Assert.True(inputBox.Width >= containerBox.Width - 25,
            $"input width {inputBox.Width}px should fill the container ({containerBox.Width}px wide) — percentage width collapsed");
    }

    // ───────────────────────── affix chrome (edit mode) ─────────────────────────
    //
    // The control emits data-test-id=@_id, which is the bound property's name unless the demo sets
    // an explicit Id -- so `input[data-test-id='Clearable']` addresses exactly one editor. The affix
    // chrome has no test ids of its own (it is class-addressed), so each button/counter is scoped by
    // the general-sibling combinator off its own input: in affix layout the suffix <span> is always a
    // following sibling of the editor inside one .edit-input-affix-wrapper.

    [Fact]
    public async Task AllowClear_empties_the_value_and_returns_focus_to_the_input()
    {
        await NavigateAsync();
        var input = Page.Locator("input[data-test-id='Clearable']");
        var clear = Page.Locator("input[data-test-id='Clearable'] ~ .edit-input-suffix .edit-input-clear");

        await input.FillAsync("something to clear");
        await Expect(clear).ToBeVisibleAsync();

        await clear.ClickAsync();

        // Clear() assigns the empty string, not null -- the same value the user's own deletion
        // produces, and the value that keeps the control mounted under HidingMode.WhenNull.
        await Expect(input).ToHaveValueAsync("");
        // The reason this test exists: Clear() refocuses the editor through
        // ElementReference.FocusAsync, which is JS interop -- bUnit cannot execute it, so nothing
        // below the e2e layer can prove the focus actually comes back rather than falling to <body>.
        await Expect(input).ToBeFocusedAsync();
        // The button withdraws once there is nothing left to clear (IsClearable).
        await Expect(clear).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Password_toggle_flips_the_input_type_and_keeps_the_typed_value()
    {
        await NavigateAsync();
        var input = Page.Locator("input[data-test-id='Password']");
        var toggle = Page.Locator("input[data-test-id='Password'] ~ .edit-input-suffix .edit-input-password-toggle");

        await input.FillAsync("s3cret-value");
        await Expect(input).ToHaveAttributeAsync("type", "password");
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");

        await toggle.ClickAsync();

        await Expect(input).ToHaveAttributeAsync("type", "text");
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        // Revealing must not disturb what was typed -- the type flip is an attribute patch on the
        // same element, not a swap for a second input.
        await Expect(input).ToHaveValueAsync("s3cret-value");

        await toggle.ClickAsync();

        await Expect(input).ToHaveAttributeAsync("type", "password");
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(input).ToHaveValueAsync("s3cret-value");
    }

    [Fact]
    public async Task Password_toggle_keeps_one_stable_accessible_name_across_both_states()
    {
        await NavigateAsync();
        var toggle = Page.Locator("input[data-test-id='Password'] ~ .edit-input-suffix .edit-input-password-toggle");

        // The label names the ACTION and never changes; aria-pressed alone carries the state. A
        // toggle whose name AND pressed state both flip is ambiguous ("Hide password, pressed").
        // TXT-4: the name also folds in the field's own label ("Password" -- DemoEditString's
        // Password property has no [DisplayName]) so a Password/Confirm-Password pair wouldn't
        // render two toggles with the same accessible name.
        await Expect(toggle).ToHaveAttributeAsync("aria-label", "Show Password password");
        await toggle.ClickAsync();
        await Expect(toggle).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(toggle).ToHaveAttributeAsync("aria-label", "Show Password password");
    }

    [Fact]
    public async Task ShowCount_counter_tracks_typing_live()
    {
        await NavigateAsync();
        var input = Page.Locator("input[data-test-id='Counted']");
        var count = Page.Locator("input[data-test-id='Counted'] ~ .edit-input-suffix .edit-input-count");
        // The visible counter is aria-hidden ("5 / 20" reads as "five slash twenty"); AT gets this
        // visually-hidden sibling instead, which the input's aria-describedby points at.
        var spoken = Page.Locator("#count-Counted");

        await Expect(count).ToHaveTextAsync("0 / 20");

        await input.PressSequentiallyAsync("abcde");

        // Per keystroke: the string editors default to UpdateTrigger.Input, so CurrentValue moves
        // with each oninput and the chrome follows without waiting for a blur.
        await Expect(count).ToHaveTextAsync("5 / 20");
        await Expect(spoken).ToHaveTextAsync("5 of 20 characters");

        await input.FillAsync("");
        await Expect(count).ToHaveTextAsync("0 / 20");
        await Expect(spoken).ToHaveTextAsync("0 of 20 characters");
    }

    // ───────────────────────── read-only views ─────────────────────────
    //
    // Against the "Read-Only Views" demo section, whose controls each set an explicit Id so their
    // data-test-ids are unique (the older sections bind the same property two or three times).

    [Fact]
    public async Task Read_only_mask_eye_reveals_the_value_and_keeps_its_own_focus()
    {
        await NavigateAsync();
        var row = Page.Locator("[data-test-id='ro-masked']");
        var text = row.Locator(".edit-readonly-value");
        var eye = row.Locator("button");

        // A multi-character mask is a prefix: it covers the head of the value and the uncovered
        // tail still shows. ("123-45-6789" under mask "***" -> "***-45-6789".)
        await Expect(text).ToHaveTextAsync("***-45-6789");
        await Expect(eye).ToHaveAttributeAsync("aria-pressed", "false");
        // TXT-4: folds in the bound property's own auto-generated label ("Masked Text" --
        // _model.MaskedText has no [DisplayName]) so two masked fields on the demo page aren't both
        // named "Show value".
        await Expect(eye).ToHaveAttributeAsync("aria-label", "Show Masked Text value");

        await eye.ClickAsync();

        await Expect(text).ToHaveTextAsync("123-45-6789");
        await Expect(eye).ToHaveAttributeAsync("aria-pressed", "true");
        // Element identity: the masked row renders from ONE site with ternaries on the attributes and
        // text, so Blazor's diff patches this button in place and the user keeps standing on it. The
        // two-sibling-@if shape it replaced destroyed and rebuilt the button, dropping focus to
        // <body> mid-gesture. bUnit can pin the handler id; only a real browser can pin the focus.
        await Expect(eye).ToBeFocusedAsync();

        // Toggling back is the same patch in reverse, and the name still doesn't move.
        await eye.ClickAsync();
        await Expect(text).ToHaveTextAsync("***-45-6789");
        await Expect(eye).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(eye).ToHaveAttributeAsync("aria-label", "Show Masked Text value");
        await Expect(eye).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Read_only_password_field_masks_with_bullets_instead_of_printing_the_secret()
    {
        await NavigateAsync();
        var row = Page.Locator("[data-test-id='ro-password']");
        var text = row.Locator(".edit-readonly-value");

        // [DataType(DataType.Password)] alone, no MaskText: read-only is not a reason to print a
        // secret in the clear, so the field supplies its own single-bullet mask.
        var masked = await text.TextContentAsync();
        Assert.False(string.IsNullOrEmpty(masked));
        Assert.All(masked, c => Assert.Equal('•', c));

        await row.Locator("button").ClickAsync();
        await Expect(text).ToHaveTextAsync("correct-horse-battery");
    }

    [Fact]
    public async Task Read_only_Url_hardens_its_rel_and_rejects_a_javascript_scheme()
    {
        await NavigateAsync();

        var blank = Page.Locator("a[data-test-id='ro-url-blank']");
        await Expect(blank).ToHaveAttributeAsync("href", "https://example.com/vendors/42");
        await Expect(blank).ToHaveAttributeAsync("target", "_blank");
        await Expect(blank).ToHaveAttributeAsync("rel", "noopener noreferrer");
        // _blank always creates a context, so the link can honestly say so -- visually hidden, and
        // inside the <a> so the self-referencing aria-labelledby folds it into the accessible name.
        await Expect(blank.Locator(".edit-sr-only")).ToHaveTextAsync("(opens in new tab)");

        // A NAMED target is the case that most needs the rel: its window.opener points back here.
        var named = Page.Locator("a[data-test-id='ro-url-named']");
        await Expect(named).ToHaveAttributeAsync("target", "vendor");
        await Expect(named).ToHaveAttributeAsync("rel", "noopener noreferrer");
        // ...but it must not claim a new tab -- a named target reuses a context already by that name.
        await Expect(named.Locator(".edit-sr-only")).ToHaveCountAsync(0);

        // _self reuses our own context: no opener to sever, and noreferrer would needlessly drop the
        // referrer on a navigation inside our own frame tree.
        var self = Page.Locator("a[data-test-id='ro-url-self']");
        await Expect(self).ToHaveAttributeAsync("target", "_self");
        Assert.Null(await self.GetAttributeAsync("rel"));

        // javascript: never becomes an <a> at all. The SafeUrl allow-list (http/https/mailto, plus
        // same-origin relative) declines it and the control falls through to plain read-only text --
        // so there is no script-executing link to click even if the URL came from model data.
        await Expect(Page.Locator("a[data-test-id='ro-url-unsafe']")).ToHaveCountAsync(0);
        var rejected = Page.Locator("div[data-test-id='ro-url-unsafe']");
        await Expect(rejected).ToHaveClassAsync(new Regex(@"\bedit-readonly-value\b"));
        await Expect(rejected).ToHaveTextAsync("Friendly name for a website");
    }

    [Fact]
    public async Task Read_only_MaskText_beats_Url_so_a_masked_value_never_becomes_a_link()
    {
        await NavigateAsync();

        // Same control, both MaskText and Url set. The mask branch is checked first, so no <a>
        // renders -- a value the page was asked to hide must not be published as link text.
        await Expect(Page.Locator("a[data-test-id='ro-mask-over-url']")).ToHaveCountAsync(0);
        var row = Page.Locator("div[data-test-id='ro-mask-over-url']");
        await Expect(row).ToHaveClassAsync(new Regex(@"\bedit-masked-value\b"));
        // MaskText="*" -- a single-character mask repeats to cover the whole value.
        await Expect(row.Locator(".edit-readonly-value")).ToHaveTextAsync(new string('*', "123-45-6789".Length));
    }
}
