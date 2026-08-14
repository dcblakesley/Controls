using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the EditColor form control and, through it, the UI-kit <c>ColorPicker</c>'s
/// JS-owned internals — the pointer drag (<c>wss-color.js</c>), the panel placement
/// (<c>wss-overlay.js</c>), Enter's suppressed form submission, and the focus restore on close. None of
/// those are reachable from bUnit, which executes no JavaScript; the form-integration and pure-C#
/// halves are covered by <c>EditColorTests</c>/<c>ColorPickerTests</c>.
/// </summary>
public class EditColorE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Color;

    // Section order on the demo page: 0 basic, 1 ShowText + read-only, 2 ShowAlpha=false,
    // 3 presets, 4 AllowClear, 5 disabled + [Required].
    ILocator Section(int index) => Page.Locator("section.demo-section").Nth(index);

    static ILocator Trigger(ILocator scope) => scope.Locator(".wss-color-picker-trigger").First;
    static ILocator Panel(ILocator scope) => scope.Locator(".wss-color-picker-panel");

    async Task<ILocator> OpenAsync(ILocator scope)
    {
        await Trigger(scope).ClickAsync();
        var panel = Panel(scope);
        // Mandatory for a JS-positioned popup: visible AND past the wss-measuring phase.
        await WaitForOpenAndPositionedAsync(panel);
        return panel;
    }

    // Drags along a track's horizontal axis from `fromFraction` to `toFraction` of its own width,
    // through real pointer events -- the only way to exercise wss-color.js's drag path.
    async Task DragAsync(ILocator track, double fromFraction, double toFraction)
    {
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        var y = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync(box.X + (float)(box.Width * fromFraction), y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + (float)(box.Width * toFraction), y);
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task Saturation_click_commits_the_picked_color()
    {
        await NavigateAsync();

        // First section: EditColor bound to a fixed #1890ff, no ShowText -- the trigger's own
        // aria-label carries the value, which is what makes it observable in every section.
        var section = Section(0);
        var trigger = Trigger(section);
        await Expect(trigger).ToHaveAttributeAsync("aria-label", "Basic Color: #1890ff");

        var panel = await OpenAsync(section);
        // The 2D area is 234x140 (--wss-color-picker-width / -sv-height), so this lands dead centre:
        // half saturation, half brightness. The click goes through wss-color.js's pointerdown, not the
        // no-JS @onclick fallback -- see Without_the_js_module_a_click_still_positions_the_handle.
        await panel.Locator(".wss-color-picker-sv").ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = 117, Y = 70 }
        });

        var area = panel.Locator(".wss-color-picker-sv");
        await Expect(area).ToHaveAttributeAsync("aria-valuenow", "50");
        await Expect(area).ToHaveAttributeAsync("aria-valuetext", "Saturation 50%, brightness 50%");
        // Committed through @bind-Value and back out as normalized hex; the panel stays open (a color
        // picker commits continuously, unlike the date picker's one-click-and-close).
        await Expect(trigger).ToHaveAttributeAsync("aria-label", new Regex("^Basic Color: #[0-9a-f]{6}$"));
        await Expect(trigger).Not.ToHaveAttributeAsync("aria-label", "Basic Color: #1890ff");
        await Expect(panel).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Hue_drag_moves_the_hue_and_commits()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);
        var hue = panel.Locator(".wss-color-picker-hue");

        // A real press-move-release across the track: the reports are rAF-throttled, so the assertions
        // below rely on Playwright's own retrying.
        await DragAsync(hue, 0.1, 0.5);

        // Half way along the spectrum is 180 degrees; a pixel of subpixel slop either way is fine.
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", new Regex("^1(79|80|81)$"));
        await Expect(hue).ToHaveAttributeAsync("aria-valuetext", new Regex("^1(79|80|81)°$"));
        // Only the hue moved: #1890ff's saturation (0.906) and value (1) are preserved, so the red
        // channel stays at 0x18 and the drag lands on cyan (#18ffff, give or take that pixel of slop).
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", new Regex("^Basic Color: #18[0-9a-f]{4}$"));
    }

    [Fact]
    public async Task Alpha_drag_commits_an_eight_digit_value()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);

        await DragAsync(panel.Locator(".wss-color-picker-alpha"), 0.9, 0.5);

        // Translucent, so the normalized value grows the alpha pair.
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", new Regex("^Basic Color: #[0-9a-f]{8}$"));
        await Expect(panel.Locator(".wss-color-picker-alpha"))
            .ToHaveAttributeAsync("aria-valuenow", new Regex("^(49|50|51)$"));
    }

    [Fact]
    public async Task A_typed_hex_commits_on_Enter_without_submitting_the_form()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);

        await panel.Locator(".wss-color-picker-hex").FillAsync("#00ff00");
        await panel.Locator(".wss-color-picker-hex").PressAsync("Enter");

        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", "Basic Color: #00ff00");
        // wss-color.js preventDefaults Enter, so the enclosing EditForm never implicitly submitted --
        // had it, this Blazor page would have navigated/re-rendered and the panel would be gone.
        await Expect(panel).ToBeVisibleAsync();
    }

    [Fact]
    public async Task An_unparseable_typed_hex_surfaces_a_validation_message()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);

        await panel.Locator(".wss-color-picker-hex").FillAsync("definitely not a color");
        await panel.Locator(".wss-color-picker-hex").PressAsync("Enter");

        // Ids default to the bound property name, so the message region is addressable directly.
        await Expect(section.Locator("#error-msg-BasicColor")).ToContainTextAsync("must be a color");
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", "Basic Color: #1890ff"); // untouched
    }

    [Fact]
    public async Task Escape_closes_the_panel_and_restores_focus_to_the_trigger()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);
        // Focus lands in the panel on open (the 2D area), so the restore is observable.
        await Expect(panel.Locator(".wss-color-picker-sv")).ToBeFocusedAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(panel).Not.ToBeVisibleAsync();
        await Expect(Trigger(section)).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Arrow_keys_step_the_hue_with_no_pointer_involved()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);
        var hue = panel.Locator(".wss-color-picker-hue");
        await hue.FocusAsync();
        var before = await hue.GetAttributeAsync("aria-valuenow");

        await hue.PressAsync("Home");
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", "0");
        await hue.PressAsync("ArrowRight");
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", "1");
        await hue.PressAsync("Shift+ArrowRight");
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", "11");

        Assert.NotEqual("11", before); // the steps actually moved something
        // The page must not have scrolled: wss-color.js preventDefaults exactly these keys.
        Assert.Equal(0, await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)"));
    }

    [Fact]
    public async Task A_preset_click_commits_that_color()
    {
        await NavigateAsync();

        // Fourth section: PresetColor pinned to the first preset (#f5222d), so picking a different one
        // makes the click observably responsible.
        var section = Section(3);
        var panel = await OpenAsync(section);

        await panel.Locator(".wss-color-picker-preset").Nth(4).ClickAsync(); // #1890ff

        await Expect(section.Locator(".wss-color-picker-value")).ToHaveTextAsync("#1890ff");
        await Expect(panel.Locator(".wss-color-picker-preset").Nth(4))
            .ToHaveAttributeAsync("aria-pressed", "true");
    }

    [Fact]
    public async Task The_clear_button_nulls_the_value()
    {
        await NavigateAsync();

        // Fifth section: AllowClear + ShowText, pinned to #fa8c16.
        var section = Section(4);
        await Expect(section.Locator(".wss-color-picker-value")).ToHaveTextAsync("#fa8c16");

        await section.Locator(".wss-color-picker-clear").ClickAsync();

        await Expect(section.Locator(".wss-color-picker-value")).Not.ToBeVisibleAsync();
        await Expect(section.Locator(".wss-color-picker-clear")).Not.ToBeVisibleAsync();
        // The demo sets Label="Clearable", and EditColor forwards the resolved field label as the
        // trigger's accessible-name prefix.
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", "Clearable: no color");
        // Clearing must not open the popup -- the clear button's click stops short of the trigger slot.
        await Expect(Panel(section)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_disabled_trigger_does_not_open_the_panel()
    {
        await NavigateAsync();

        // Sixth section: the disabled instance first, then the [Required] one.
        var section = Section(5);
        var trigger = section.Locator(".wss-color-picker-trigger").First;
        await Expect(trigger).ToBeDisabledAsync();

        await trigger.ClickAsync(new LocatorClickOptions { Force = true });

        await Expect(section.Locator(".wss-color-picker-panel")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task The_required_field_is_marked_invalid_before_any_interaction()
    {
        await NavigateAsync();

        // The demo force-validates on every render (DemoFormPage), so the [Required] instance is
        // already invalid with no interaction.
        var section = Section(5);
        var trigger = section.Locator("#Required");
        await Expect(trigger).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(section.Locator("#error-msg-Required")).ToContainTextAsync("Required is required.");

        // The .wss-color-picker.invalid rule actually applies. The Has locator must be simple and
        // page-rooted: Playwright re-roots it at each candidate, so a section-chained locator would
        // look for "section.demo-section" INSIDE .wss-color-picker and never match.
        var wrapper = section.Locator(".wss-color-picker", new() { Has = Page.Locator("#Required") });
        // --wss-color-error bridges to --color-danger, which the FormTesting host's app.css overrides
        // to #CF1322 at :root -- that value, not the #ff4d4f fallback, is what reaches the browser.
        await Expect(wrapper.Locator(".wss-color-picker-trigger"))
            .ToHaveCSSAsync("border-color", "rgb(207, 19, 34)");
    }

    [Fact]
    public async Task ShowAlpha_false_renders_no_alpha_track_and_keeps_the_value_six_digit()
    {
        await NavigateAsync();

        var section = Section(2);
        var panel = await OpenAsync(section);
        await Expect(panel.Locator(".wss-color-picker-alpha")).Not.ToBeVisibleAsync();

        await DragAsync(panel.Locator(".wss-color-picker-hue"), 0.2, 0.6);

        await Expect(section.Locator(".wss-color-picker-value")).ToHaveTextAsync(new Regex("^#[0-9a-f]{6}$"));
    }

    [Fact]
    public async Task Without_the_js_module_a_click_still_positions_the_handle()
    {
        // The documented no-JS degrade, verified in a real browser: assetBase points every lazily
        // imported wss-*.js at a path nothing serves, so the drag module never loads and the
        // component's own @onclick fallback -- which reads MouseEventArgs.OffsetX/OffsetY -- has to
        // carry the press. bUnit can only simulate this with synthetic event args (see
        // ColorPickerTests' Strict-mode fallback tests); this proves the browser really does populate
        // those offsets relative to the track, which the handles being pointer-events:none guarantees.
        await Page.GotoAsync($"{App.BaseUrl}/?view={View}&assetBase={Uri.EscapeDataString("/definitely-not-a-real-path")}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });
        await Expect(Page.Locator("h1", new() { HasTextString = "EditColor Demo" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        var section = Section(0);
        await Trigger(section).ClickAsync();
        var panel = Panel(section);
        // No JS to position it, so it stays at the CSS default placement -- but wss-measuring is still
        // dropped (the component reveals the panel whether or not placement succeeded).
        await WaitForOpenAndPositionedAsync(panel);

        await panel.Locator(".wss-color-picker-sv").ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = 117, Y = 70 }
        });

        await Expect(panel.Locator(".wss-color-picker-sv")).ToHaveAttributeAsync("aria-valuenow", "50");
        await Expect(panel.Locator(".wss-color-picker-sv"))
            .ToHaveAttributeAsync("aria-valuetext", "Saturation 50%, brightness 50%");
        await Expect(Trigger(section)).Not.ToHaveAttributeAsync("aria-label", "Basic Color: #1890ff");
    }

    static Task<string> ForcedColorAdjustAsync(ILocator locator) =>
        locator.EvaluateAsync<string>("el => getComputedStyle(el).forcedColorAdjust");

    [Fact]
    public async Task Under_forced_colors_the_swatches_and_tracks_keep_carrying_their_color()
    {
        // Windows High Contrast substitutes the OS palette for every author color and drops box-shadow.
        // For this control that erases the entire signal: swatches, the 2D area's gradients and the
        // hue/alpha tracks are nothing BUT color, so they opt out with forced-color-adjust: none (the
        // "color IS the information" exception, media-query-gated so nothing changes at rest).
        await NavigateAsync();
        var section = Section(3); // presets -- PresetColor is pinned to the first one, so it reads pressed
        var panel = await OpenAsync(section);
        var swatch = section.Locator(".wss-color-picker-trigger-swatch");
        var fill = swatch.Locator(".wss-color-picker-swatch-fill");
        var pressed = panel.Locator(".wss-color-picker-preset[aria-pressed=\"true\"] .wss-color-picker-swatch");
        await Expect(pressed).ToBeVisibleAsync();

        // Baseline: nothing opts out while forced colors are off, so the assertions below aren't vacuous.
        Assert.Equal("auto", await ForcedColorAdjustAsync(swatch));
        Assert.Equal("auto", await ForcedColorAdjustAsync(panel.Locator(".wss-color-picker-sv")));

        await Page.EmulateMediaAsync(new PageEmulateMediaOptions { ForcedColors = ForcedColors.Active });

        Assert.Equal("none", await ForcedColorAdjustAsync(swatch));
        Assert.Equal("none", await ForcedColorAdjustAsync(panel.Locator(".wss-color-picker-sv")));
        Assert.Equal("none", await ForcedColorAdjustAsync(panel.Locator(".wss-color-picker-hue")));
        Assert.Equal("none", await ForcedColorAdjustAsync(panel.Locator(".wss-color-picker-alpha")));
        Assert.Equal("none", await ForcedColorAdjustAsync(pressed));
        // The property inherits, which is why the fills/handles/gradient overlays need no rule of their own.
        Assert.Equal("none", await ForcedColorAdjustAsync(fill));

        // The selected preset's ring is a box-shadow (dropped by forced colors even where the swatch
        // itself opts out of the palette), re-expressed as a system-colored outline.
        Assert.Equal("2px", await pressed.EvaluateAsync<string>("el => getComputedStyle(el).outlineWidth"));
        Assert.Equal("solid", await pressed.EvaluateAsync<string>("el => getComputedStyle(el).outlineStyle"));
    }

    [Fact]
    public async Task Visual_baseline_open_panel()
    {
        await NavigateAsync();
        var panel = await OpenAsync(Section(3)); // the presets section -- the widest panel shape

        await ExpectMatchesBaselineAsync(panel, "open-panel");
    }
}
