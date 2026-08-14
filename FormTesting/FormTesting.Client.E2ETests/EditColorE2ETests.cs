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
    public async Task The_tracks_take_a_press_from_their_expanded_hit_area_without_moving_the_mapping()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);
        var sv = panel.Locator(".wss-color-picker-sv");
        var hue = panel.Locator(".wss-color-picker-hue");
        var alpha = panel.Locator(".wss-color-picker-alpha");
        var svBox = await sv.BoundingBoxAsync();
        var hueBox = await hue.BoundingBoxAsync();
        var alphaBox = await alpha.BoundingBoxAsync();
        Assert.NotNull(svBox);
        Assert.NotNull(hueBox);
        Assert.NotNull(alphaBox);

        // The visible design is unchanged (WCAG 2.5.8 is met with an invisible ::before, not a taller
        // track) -- if this ever reads 24, the visual baselines moved with it.
        Assert.True(hueBox.Height is > 9 and < 11, $"hue track height {hueBox.Height}");
        Assert.True(alphaBox.Height is > 9 and < 11, $"alpha track height {alphaBox.Height}");

        // 6px ABOVE the hue track's top edge is outside its own box but inside its hit area...
        await Page.Mouse.ClickAsync(hueBox.X + hueBox.Width / 2, hueBox.Y - 6);
        // ...and the coordinate math still normalizes against the VISIBLE track, so mid-width is still
        // 180 degrees -- the expanded box must never become the denominator.
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", new Regex("^1(79|80|81)$"));

        // 8px BELOW the alpha track, at a quarter of its width.
        await Page.Mouse.ClickAsync(alphaBox.X + (float)(alphaBox.Width * 0.25), alphaBox.Y + alphaBox.Height + 8);
        await Expect(alpha).ToHaveAttributeAsync("aria-valuenow", new Regex("^(24|25|26)$"));

        // The expansion must not eat into the 2D area above: a press just inside its bottom edge still
        // belongs to the 2D area (and reads as near-zero brightness), not to the hue track's hit box.
        await Page.Mouse.ClickAsync(svBox.X + svBox.Width / 2, svBox.Y + svBox.Height - 2);
        await Expect(sv).ToHaveAttributeAsync("aria-valuetext", new Regex("^Saturation 50%, brightness [012]%$"));
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
    public async Task A_typed_rgb_channel_commits_on_Enter_without_submitting_the_form()
    {
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);

        await panel.Locator(".wss-color-picker-format").SelectOptionAsync("Rgb");
        var green = panel.Locator(".wss-color-picker-channel").Nth(1);
        await green.FillAsync("0");
        await green.PressAsync("Enter");

        // #1890ff with green zeroed. The channel boxes had no Enter handling at all before: Enter
        // neither committed nor was preventDefaulted, so it just submitted the enclosing EditForm.
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", "Basic Color: #1800ff");
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

        // The 2D area's own Home/End (saturation to either end) -- the keys wss-color.js has always
        // preventDefaulted on every track, and which this one now actually handles.
        var area = panel.Locator(".wss-color-picker-sv");
        await area.FocusAsync();
        await area.PressAsync("Home");
        await Expect(area).ToHaveAttributeAsync("aria-valuenow", "0");
        await area.PressAsync("End");
        await Expect(area).ToHaveAttributeAsync("aria-valuenow", "100");

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
        var invalidTrigger = wrapper.Locator(".wss-color-picker-trigger");
        await Expect(invalidTrigger).ToHaveCSSAsync("border-color", "rgb(207, 19, 34)");

        // ...and it survives hover: the primary hover rule is more specific than the base invalid one,
        // so pointing at an invalid field used to turn its border blue.
        await invalidTrigger.HoverAsync();
        await Expect(invalidTrigger).ToHaveCSSAsync("border-color", "rgb(207, 19, 34)");
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

    // Navigates to this view with the demo page's ?assetBase test hook set to `assetBase` verbatim
    // (encoded on the way into the query string).
    async Task GotoWithAssetBaseAsync(string assetBase)
    {
        await Page.GotoAsync($"{App.BaseUrl}/?view={View}&assetBase={Uri.EscapeDataString(assetBase)}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });
        await Expect(Page.Locator("h1", new() { HasTextString = "EditColor Demo" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    // Resolves once wss-color.js has actually imported AND run initTrack on the hue track -- the
    // module's own idempotency expando is the only in-DOM evidence that the wiring happened.
    Task WaitForHueWiredAsync() => Page.WaitForFunctionAsync(
        "() => document.querySelector('.wss-color-picker-hue')?.__wssColorWired === true",
        null, new PageWaitForFunctionOptions { Timeout = 15_000 });

    [Fact]
    public async Task Without_the_js_module_a_click_still_positions_the_handle()
    {
        // The documented no-JS degrade, verified in a real browser: assetBase points every lazily
        // imported wss-*.js at a path nothing serves, so the drag module never loads and the
        // component's own @onclick fallback -- which reads MouseEventArgs.OffsetX/OffsetY -- has to
        // carry the press. bUnit can only simulate this with synthetic event args (see
        // ColorPickerTests' Strict-mode fallback tests); this proves the browser really does populate
        // those offsets relative to the track, which the handles being pointer-events:none guarantees.
        await GotoWithAssetBaseAsync("/definitely-not-a-real-path");

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

    [Fact]
    public async Task An_asset_base_that_would_escape_the_origin_is_dropped_by_the_test_hook()
    {
        // Not a library behavior -- the demo page's own ?assetBase hook, whose value becomes the
        // specifier of a dynamic import(). Its guard used to be a blacklist (reject a leading "//" or
        // any ":"), which "/\evil.example" walked straight through: under a special (http) scheme the
        // URL parser treats a backslash as a forward slash, so "/\evil.example/_content/..." resolves
        // protocol-relative to another HOST. The whitelist rejects it, and the page then renders exactly
        // as it does with no parameter at all -- which is what the wiring + request assertions below
        // pin down. Its mirror image is Without_the_js_module_a_click_still_positions_the_handle above,
        // where an ACCEPTED (rooted, same-origin) base really does stop the module from loading.
        var colorModuleRequests = new List<string>();
        Page.Request += (_, request) =>
        {
            if (request.Url.Contains("wss-color.js", StringComparison.Ordinal)) colorModuleRequests.Add(request.Url);
        };

        await GotoWithAssetBaseAsync("/\\evil.example");
        await Trigger(Section(0)).ClickAsync();
        await WaitForHueWiredAsync(); // the module loaded and wired: the crafted base never reached it

        Assert.NotEmpty(colorModuleRequests);
        Assert.All(colorModuleRequests, url => Assert.StartsWith(App.BaseUrl, url, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Re_wiring_an_already_wired_track_stays_a_single_commit_per_press()
    {
        // The F5/F4 fixes (a ShowAlpha flip drops the wiring latches so the newly-rendered alpha track
        // gets wired, re-running the whole pass) rest entirely on wss-color.js's initTrack/initTextInput
        // being idempotent per ELEMENT -- the __wssColorWired/__wssColorInputWired expandos. bUnit can't
        // cover that (it executes no JS), so this re-invokes both entry points on already-wired elements
        // in a real browser and counts what one press produces. Each initTrack call builds its OWN
        // closure state (dragging/pending/frame/last), so without the expando two listeners would each
        // dispatch their own input event for a single pointerdown -- two commits, two renders, two
        // Blazor Server round trips.
        await NavigateAsync();
        var section = Section(0);
        var panel = await OpenAsync(section);
        await WaitForHueWiredAsync();

        // Re-import (the same cached module instance -- the guard is in the DOM, not in module state),
        // re-init the hue track and the HEX box, and count input events on the hue track's own drag
        // signal from here on.
        var alreadyWired = await Page.EvaluateAsync<bool>(
            """
            async () => {
                const m = await import('/_content/WssBlazorControls/wss-color.js');
                const hue = document.querySelector('.wss-color-picker-hue');
                const signal = document.querySelectorAll('.wss-color-picker-signal')[1];
                const hex = document.querySelector('.wss-color-picker-hex');
                const wired = hue.__wssColorWired === true && hex.__wssColorInputWired === true;
                window.__wssSignalCount = 0;
                signal.addEventListener('input', () => window.__wssSignalCount++);
                m.initTrack(hue, signal);
                m.initTextInput(hex);
                return wired;
            }
            """);
        Assert.True(alreadyWired); // both expandos were already set -- the re-init hit the guard

        var hue = panel.Locator(".wss-color-picker-hue");
        var box = await hue.BoundingBoxAsync();
        Assert.NotNull(box);
        await Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);

        // One press, one report. (Two wirings would make this 2 -- delete either expando guard and it
        // fails.) The commit itself still lands, which the hue value proves.
        await Expect(hue).ToHaveAttributeAsync("aria-valuenow", new Regex("^1(79|80|81)$"));
        Assert.Equal(1, await Page.EvaluateAsync<int>("() => window.__wssSignalCount"));

        // The text box survives its own re-init: Enter still commits exactly once and still doesn't
        // submit the enclosing EditForm (a duplicate keydown preventDefault is unobservable by design).
        await panel.Locator(".wss-color-picker-hex").FillAsync("#00ff00");
        await panel.Locator(".wss-color-picker-hex").PressAsync("Enter");
        await Expect(Trigger(section)).ToHaveAttributeAsync("aria-label", "Basic Color: #00ff00");
        await Expect(panel).ToBeVisibleAsync();
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
