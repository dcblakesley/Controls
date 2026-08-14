using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the <see cref="ColorPicker"/> UI-kit control: open/close, the drag report channel,
/// the keyboard steps, the input row, presets, clearing, and the ARIA wiring. The JS-owned behaviors
/// (the actual pointer drag, the viewport flip/clamp, Enter submit-suppression) are covered by
/// <c>EditColorE2ETests</c> — bUnit does not execute JavaScript, so every JS import here is a no-op
/// and the component takes its documented no-JS path.
/// </summary>
public class ColorPickerTests : BunitContext
{
    public ColorPickerTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay/color JS imports

    const string Red = "#ff0000";

    IRenderedComponent<ColorPicker> RenderPicker(
        Action<ComponentParameterCollectionBuilder<ColorPicker>>? configure = null, string? value = Red) =>
        Render<ColorPicker>(p =>
        {
            p.Add(c => c.Value, value);
            configure?.Invoke(p);
        });

    // A picker whose commits land in `committed` instead of a bound model. Nothing re-supplies Value,
    // so the component's own session state survives across the interactions in a single test -- which
    // is exactly the uncontrolled case its _lastValueParam guard is designed for.
    IRenderedComponent<ColorPicker> RenderPicker(
        out Func<string?> committed,
        Action<ComponentParameterCollectionBuilder<ColorPicker>>? configure = null,
        string? value = Red)
    {
        string? captured = null;
        var cut = RenderPicker(p =>
        {
            p.Add(c => c.ValueChanged, EventCallback.Factory.Create<string?>(this, v => captured = v));
            configure?.Invoke(p);
        }, value);
        committed = () => captured;
        return cut;
    }

    static void Open(IRenderedComponent<ColorPicker> cut) => cut.Find(".wss-color-picker-trigger").Click();

    // The three hidden inputs wss-color.js reports normalized drag coordinates through, in render
    // order: saturation/value area, hue track, alpha track.
    static IReadOnlyList<IElement> Signals(IRenderedComponent<ColorPicker> cut) =>
        cut.FindAll(".wss-color-picker-signal");

    // ----- Trigger + open/close ----------------------------------------------

    [Fact]
    public void Closed_picker_renders_only_the_trigger()
    {
        var cut = RenderPicker();

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
        Assert.Empty(cut.FindAll(".wss-color-picker-backdrop"));
        Assert.Single(cut.FindAll(".wss-color-picker-trigger"));
        // The value reaches the swatch as an inline background-color, checkerboard behind it.
        Assert.Contains("rgb(255, 0, 0)", cut.Find(".wss-color-picker-swatch-fill").GetAttribute("style"));
    }

    [Fact]
    public void Trigger_click_opens_the_panel_and_the_backdrop_closes_it()
    {
        var cut = RenderPicker();

        Open(cut);
        Assert.Single(cut.FindAll(".wss-color-picker-panel"));
        Assert.Contains("wss-color-picker-open", cut.Find(".wss-color-picker").ClassList);
        // Default placement, and no JS to resolve a flip in bUnit.
        Assert.Contains("wss-color-picker-bottom", cut.Find(".wss-color-picker-panel").ClassList);

        cut.Find(".wss-color-picker-backdrop").Click();
        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    [Fact]
    public void Escape_in_the_panel_closes_it()
    {
        var cut = RenderPicker();
        Open(cut);

        cut.Find(".wss-color-picker-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    [Fact]
    public void Disabled_trigger_does_not_open_the_panel()
    {
        var cut = RenderPicker(p => p.Add(c => c.Disabled, true));

        Assert.NotNull(cut.Find(".wss-color-picker-trigger").GetAttribute("disabled"));
        Assert.Contains("wss-color-picker-disabled", cut.Find(".wss-color-picker").ClassList);

        // Click the slot, not the (natively inert) button: the guard being tested is C#'s, and this is
        // the element the handler is actually on.
        cut.Find(".wss-color-picker-trigger-slot").Click();

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    [Fact]
    public void Disabled_set_while_open_closes_the_panel()
    {
        var cut = RenderPicker();
        Open(cut);
        Assert.Single(cut.FindAll(".wss-color-picker-panel"));

        cut.Render(p => p.Add(c => c.Disabled, true));

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    // ----- The drag report channel -------------------------------------------

    [Fact]
    public void Saturation_drag_report_commits_the_expected_color()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        // Half saturation, and y is measured downward -- 0.5 means half brightness.
        Signals(cut)[0].Input("0.5,0.5");

        Assert.Equal("#804040", committed());
    }

    [Fact]
    public void Hue_drag_report_commits_the_expected_hue()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        Signals(cut)[1].Input("0.5,0"); // half way along the spectrum == 180 degrees == cyan

        Assert.Equal("#00ffff", committed());
        Assert.Equal("180", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Alpha_drag_report_commits_an_eight_digit_hex()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        Signals(cut)[2].Input("0.5,0");

        Assert.Equal("#ff000080", committed());
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("0.5")]      // no separator
    [InlineData("x,0.5")]
    [InlineData("0.5,y")]
    // NumberStyles.Float accepts these, and Math.Clamp propagates NaN rather than clamping it -- so a
    // non-finite coordinate used to commit through (NaN,NaN landed on #000000, and left the handle at
    // `left: NaN%` with the arrow keys unable to recover).
    [InlineData("NaN,NaN")]
    [InlineData("NaN,0.5")]
    [InlineData("0.5,NaN")]
    [InlineData("Infinity,0.5")]
    [InlineData("0.5,-Infinity")]
    public void A_malformed_drag_report_is_ignored(string payload)
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        Signals(cut)[0].Input(payload);

        Assert.Null(committed());
        // ...and the handle offsets stay renderable, which a NaN coordinate would not be (`left: NaN%`).
        Assert.DoesNotContain("NaN", cut.Find(".wss-color-picker-sv-handle").GetAttribute("style"));
        Assert.Equal("100", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuenow"));
    }

    // Strict JSInterop, not this class's usual Loose: under Loose, bUnit hands back a working fake
    // module for the `import`, so initTrack "succeeds" and the component correctly treats the drag as
    // JS-owned (which gates these click fallbacks off). Strict makes the import throw, which is what a
    // real prerender / unreachable-asset host does -- the only mode that actually exercises the
    // no-JS path. The real-browser counterpart is EditColorE2ETests' assetBase-404 test.
    [Fact]
    public void Saturation_click_positions_from_the_event_offsets_when_there_is_no_js()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var cut = RenderPicker(out var committed);
        Open(cut);

        // 117/234 and 70/140 are both mid-track.
        cut.Find(".wss-color-picker-sv").Click(new MouseEventArgs { OffsetX = 117, OffsetY = 70 });

        Assert.Equal("#804040", committed());
    }

    [Fact]
    public void Hue_click_positions_from_the_event_offset_when_there_is_no_js()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-hue").Click(new MouseEventArgs { OffsetX = 117 });

        Assert.Equal("#00ffff", committed());
    }

    [Fact]
    public void Alpha_click_positions_from_the_event_offset_when_there_is_no_js()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-alpha").Click(new MouseEventArgs { OffsetX = 117 });

        Assert.Equal("#ff000080", committed());
    }

    [Fact]
    public void A_click_is_inert_once_the_drag_module_owns_the_track()
    {
        // The Loose default stands in for "wss-color.js loaded": the click that a pointerdown-driven
        // drag also produces must not report a second time.
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-sv").Click(new MouseEventArgs { OffsetX = 117, OffsetY = 70 });

        Assert.Null(committed());
    }

    // ----- Keyboard ----------------------------------------------------------

    [Fact]
    public void Arrow_keys_step_saturation_and_brightness_by_one_percent()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);
        var area = cut.Find(".wss-color-picker-sv");

        area.KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("99", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuenow"));

        cut.Find(".wss-color-picker-sv").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("Saturation 99%, brightness 99%", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuetext"));

        Assert.NotNull(committed());
    }

    [Fact]
    public void Shift_arrow_takes_the_large_step()
    {
        var cut = RenderPicker();
        Open(cut);

        cut.Find(".wss-color-picker-sv").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft", ShiftKey = true });

        Assert.Equal("90", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void PageDown_is_a_large_brightness_step()
    {
        var cut = RenderPicker();
        Open(cut);

        cut.Find(".wss-color-picker-sv").KeyDown(new KeyboardEventArgs { Key = "PageDown" });

        Assert.Equal("Saturation 100%, brightness 90%", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void Hue_arrow_steps_one_degree_and_Home_End_jump_to_the_ends()
    {
        var cut = RenderPicker();
        Open(cut);

        cut.Find(".wss-color-picker-hue").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("1", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));

        cut.Find(".wss-color-picker-hue").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("360", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));

        cut.Find(".wss-color-picker-hue").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void Alpha_arrow_steps_one_percent_and_Home_goes_fully_transparent()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-alpha").KeyDown(new KeyboardEventArgs { Key = "Home" });

        Assert.Equal("0", cut.Find(".wss-color-picker-alpha").GetAttribute("aria-valuenow"));
        Assert.Equal("#ff000000", committed());

        cut.Find(".wss-color-picker-alpha").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("1", cut.Find(".wss-color-picker-alpha").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void An_unhandled_key_changes_nothing()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-sv").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Null(committed());
    }

    // ----- The lossy-hue invariant -------------------------------------------

    [Fact]
    public void The_session_hue_survives_a_commit_that_produces_black()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);
        Signals(cut)[1].Input("0.5,0"); // hue 180
        Assert.Equal("180", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));

        // Drag brightness to zero: the committed color is black, which carries no hue at all.
        Signals(cut)[0].Input("0.5,1");
        Assert.Equal("#000000", committed());

        // The binding writes that value back (the normal controlled round trip). Recognized as our own
        // emission, so no re-derivation happens at all -- which is what keeps the slider at 180.
        cut.Render(p => p.Add(c => c.Value, committed()));

        Assert.Equal("180", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void An_achromatic_external_value_keeps_the_session_hue_and_adopts_its_brightness()
    {
        var cut = RenderPicker();
        Open(cut);
        Signals(cut)[1].Input("0.5,0"); // hue 180
        Assert.Equal("180", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));

        // A grey this component never emitted: SyncFromValue DOES run, and #333333 carries no hue --
        // adopting the derived 0 would snap the slider to red.
        cut.Render(p => p.Add(c => c.Value, "#333333"));

        Assert.Equal("180", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));
        Assert.Equal("Saturation 0%, brightness 20%", cut.Find(".wss-color-picker-sv").GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void An_externally_changed_value_is_adopted()
    {
        var cut = RenderPicker();
        Open(cut);

        cut.Render(p => p.Add(c => c.Value, "#00ff00"));

        Assert.Equal("120", cut.Find(".wss-color-picker-hue").GetAttribute("aria-valuenow"));
        Assert.Contains("rgb(0, 255, 0)", cut.Find(".wss-color-picker-swatch-fill").GetAttribute("style"));
    }

    // ----- Alpha off ---------------------------------------------------------

    [Fact]
    public void ShowAlpha_false_hides_the_alpha_track_and_strips_the_channel()
    {
        // Bound in WITH an alpha pair, which ShowAlpha="false" must drop.
        var cut = RenderPicker(out var committed, p => p.Add(c => c.ShowAlpha, false), value: "#ff000080");
        Open(cut);

        Assert.Empty(cut.FindAll(".wss-color-picker-alpha"));
        Assert.Equal(2, Signals(cut).Count); // no alpha channel to report through either

        Signals(cut)[0].Input("1,0"); // full saturation + brightness

        Assert.Equal("#ff0000", committed());
    }

    [Fact]
    public void ShowAlpha_false_drops_the_alpha_column_from_the_rgb_row()
    {
        var cut = RenderPicker(p => p.Add(c => c.ShowAlpha, false));
        Open(cut);

        cut.Find(".wss-color-picker-format").Change(nameof(ColorFormat.Rgb));

        Assert.Equal(3, cut.FindAll(".wss-color-picker-channel").Count);
    }

    // ----- Input row ---------------------------------------------------------

    [Fact]
    public void A_typed_hex_commits()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-hex").Change("00ff00"); // no leading # -- still accepted

        Assert.Equal("#00ff00", committed());
    }

    [Fact]
    public void Enter_in_the_hex_box_commits_without_waiting_for_a_change_event()
    {
        // Enter is handled explicitly rather than relying on the browser's own change-on-Enter, which
        // wss-color.js's preventDefault (there to stop an enclosing form submitting) makes unreliable.
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-hex").Input("#00ff00");
        cut.Find(".wss-color-picker-hex").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("#00ff00", committed());
    }

    [Fact]
    public void An_unparseable_typed_hex_raises_OnParseError_reverts_the_box_and_commits_nothing()
    {
        var errors = new List<string>();
        var cut = RenderPicker(out var committed, p =>
            p.Add(c => c.OnParseError, EventCallback.Factory.Create<string>(this, errors.Add)));
        Open(cut);

        // Input then Change, exactly as a browser sequences them -- the per-keystroke handler is what
        // puts the bad text into the render, which is what makes the revert an observable diff.
        cut.Find(".wss-color-picker-hex").Input("not a color");
        Assert.Equal("not a color", cut.Find(".wss-color-picker-hex").GetAttribute("value"));
        cut.Find(".wss-color-picker-hex").Change("not a color");

        Assert.Equal("not a color", Assert.Single(errors));
        Assert.Null(committed());
        Assert.Equal(Red, cut.Find(".wss-color-picker-hex").GetAttribute("value"));
    }

    [Fact]
    public void Emptying_the_hex_box_clears_when_AllowClear_is_on_and_reverts_otherwise()
    {
        var cleared = RenderPicker(out var clearedValue, p => p.Add(c => c.AllowClear, true));
        Open(cleared);
        cleared.Find(".wss-color-picker-hex").Change("");
        Assert.Null(clearedValue());
        Assert.Contains("wss-color-picker-swatch-empty", cleared.Find(".wss-color-picker-swatch-fill").ClassList);

        var reverted = RenderPicker(out var revertedValue);
        Open(reverted);
        reverted.Find(".wss-color-picker-hex").Input("");
        reverted.Find(".wss-color-picker-hex").Change("");
        Assert.Null(revertedValue()); // nothing committed at all
        Assert.Equal(Red, reverted.Find(".wss-color-picker-hex").GetAttribute("value"));
    }

    [Fact]
    public void The_format_switch_renders_the_rgb_row_and_a_channel_commits()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);

        cut.Find(".wss-color-picker-format").Change(nameof(ColorFormat.Rgb));
        var channels = cut.FindAll(".wss-color-picker-channel");
        Assert.Equal(4, channels.Count);
        Assert.Equal("255", channels[0].GetAttribute("value"));

        channels[1].Change("128"); // green

        Assert.Equal("#ff8000", committed());
    }

    [Fact]
    public void An_out_of_range_channel_clamps_and_a_non_numeric_one_reverts()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);
        cut.Find(".wss-color-picker-format").Change(nameof(ColorFormat.Rgb));

        // Input then Change, as a browser sequences them (see the invalid-hex test's own note).
        cut.FindAll(".wss-color-picker-channel")[2].Input("900");
        cut.FindAll(".wss-color-picker-channel")[2].Change("900");
        Assert.Equal("#ff00ff", committed());
        Assert.Equal("255", cut.FindAll(".wss-color-picker-channel")[2].GetAttribute("value"));

        cut.FindAll(".wss-color-picker-channel")[2].Input("nope");
        cut.FindAll(".wss-color-picker-channel")[2].Change("nope");
        Assert.Equal("#ff00ff", committed()); // unchanged
        Assert.Equal("255", cut.FindAll(".wss-color-picker-channel")[2].GetAttribute("value"));
    }

    [Fact]
    public void The_rgb_alpha_column_commits_a_percentage()
    {
        var cut = RenderPicker(out var committed);
        Open(cut);
        cut.Find(".wss-color-picker-format").Change(nameof(ColorFormat.Rgb));

        cut.FindAll(".wss-color-picker-channel")[3].Change("50");

        Assert.Equal("#ff000080", committed());
    }

    // ----- Presets -----------------------------------------------------------

    [Fact]
    public void A_preset_click_commits_that_color_and_marks_it_pressed()
    {
        var cut = RenderPicker(out var committed, p =>
            p.Add(c => c.Presets, (IReadOnlyList<string>)["#00ff00", "rgb(0, 0, 255)"]));
        Open(cut);
        var presets = cut.FindAll(".wss-color-picker-preset");
        Assert.Equal(2, presets.Count);

        presets[1].Click();

        Assert.Equal("#0000ff", committed());
        // aria-pressed follows the committed value, and an rgb() preset matches its hex equivalent.
        Assert.Equal("true", cut.FindAll(".wss-color-picker-preset")[1].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll(".wss-color-picker-preset")[0].GetAttribute("aria-pressed"));
    }

    [Fact]
    public void An_unparseable_preset_is_disabled_and_shows_the_empty_indicator()
    {
        var cut = RenderPicker(out var committed, p =>
            p.Add(c => c.Presets, (IReadOnlyList<string>)["chartreuse"]));
        Open(cut);

        var preset = cut.Find(".wss-color-picker-preset");
        Assert.NotNull(preset.GetAttribute("disabled"));
        Assert.Contains("wss-color-picker-swatch-empty",
            preset.QuerySelector(".wss-color-picker-swatch-fill")!.ClassList);
        Assert.Null(committed());
    }

    [Fact]
    public void No_preset_row_renders_without_presets()
    {
        var cut = RenderPicker();
        Open(cut);

        Assert.Empty(cut.FindAll(".wss-color-picker-presets"));
    }

    // ----- Clearing ----------------------------------------------------------

    [Fact]
    public void Clear_raises_null_and_hides_itself()
    {
        var cut = RenderPicker(out var committed, p => p.Add(c => c.AllowClear, true));

        cut.Find(".wss-color-picker-clear").Click();

        Assert.Null(committed());
        Assert.Empty(cut.FindAll(".wss-color-picker-clear"));
        Assert.Contains("wss-color-picker-swatch-empty", cut.Find(".wss-color-picker-swatch-fill").ClassList);
    }

    [Fact]
    public void Clear_does_not_toggle_the_popup()
    {
        var cut = RenderPicker(p => p.Add(c => c.AllowClear, true));

        cut.Find(".wss-color-picker-clear").Click();

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    [Theory]
    [InlineData(false, Red, false)]  // AllowClear off
    [InlineData(true, null, false)]  // nothing to clear
    [InlineData(true, "", false)]    // empty is nothing to clear either
    [InlineData(true, Red, true)]    // disabled
    public void Clear_only_renders_when_there_is_a_color_to_clear_on_an_enabled_picker(
        bool allowClear, string? value, bool disabled)
    {
        var cut = Render<ColorPicker>(p => p
            .Add(c => c.Value, value)
            .Add(c => c.AllowClear, allowClear)
            .Add(c => c.Disabled, disabled));

        Assert.Empty(cut.FindAll(".wss-color-picker-clear"));
    }

    // ----- Empty / text / bound-in forms -------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("chartreuse")] // a named CSS color is not supported -- treated as no color, not an error
    public void An_unusable_value_renders_the_empty_indicator_without_throwing(string? value)
    {
        var cut = Render<ColorPicker>(p => p.Add(c => c.Value, value).Add(c => c.ShowText, true));

        Assert.Contains("wss-color-picker-swatch-empty", cut.Find(".wss-color-picker-swatch-fill").ClassList);
        Assert.Empty(cut.FindAll(".wss-color-picker-value"));
        Assert.Contains("no color", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowText_renders_the_normalized_hex_beside_the_swatch()
    {
        var cut = Render<ColorPicker>(p => p
            .Add(c => c.Value, "rgba(255, 0, 0, 0.5)")
            .Add(c => c.ShowText, true));

        Assert.Equal("#ff000080", cut.Find(".wss-color-picker-value").TextContent);
        Assert.Equal("Color: #ff000080", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowText_is_off_by_default()
    {
        var cut = RenderPicker();

        Assert.Empty(cut.FindAll(".wss-color-picker-value"));
    }

    // ----- ARIA + splat ------------------------------------------------------

    [Fact]
    public void The_tracks_carry_full_slider_aria()
    {
        var cut = RenderPicker();
        Open(cut);

        var area = cut.Find(".wss-color-picker-sv");
        Assert.Equal("slider", area.GetAttribute("role"));
        Assert.Equal("0", area.GetAttribute("aria-valuemin"));
        Assert.Equal("100", area.GetAttribute("aria-valuemax"));
        Assert.Equal("100", area.GetAttribute("aria-valuenow"));
        Assert.Equal("Saturation 100%, brightness 100%", area.GetAttribute("aria-valuetext"));
        Assert.Equal("Saturation and brightness", area.GetAttribute("aria-label"));
        Assert.Equal("0", area.GetAttribute("tabindex"));

        var hue = cut.Find(".wss-color-picker-hue");
        Assert.Equal("slider", hue.GetAttribute("role"));
        Assert.Equal("360", hue.GetAttribute("aria-valuemax"));
        Assert.Equal("0°", hue.GetAttribute("aria-valuetext"));

        var alpha = cut.Find(".wss-color-picker-alpha");
        Assert.Equal("100", alpha.GetAttribute("aria-valuemax"));
        Assert.Equal("100%", alpha.GetAttribute("aria-valuetext"));

        var panel = cut.Find(".wss-color-picker-panel");
        Assert.Equal("dialog", panel.GetAttribute("role"));
        Assert.Equal("Choose color", panel.GetAttribute("aria-label"));
    }

    [Fact]
    public void Forwarded_validation_aria_lands_on_the_trigger_button()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.AriaRequired, "true")
            .Add(c => c.AriaInvalid, true)
            .Add(c => c.AriaDescribedBy, "desc-1")
            .Add(c => c.AriaErrorMessage, "err-1"));

        var trigger = cut.Find(".wss-color-picker-trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-required"));
        Assert.Equal("true", trigger.GetAttribute("aria-invalid"));
        Assert.Equal("desc-1", trigger.GetAttribute("aria-describedby"));
        Assert.Equal("err-1", trigger.GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void Unmatched_attributes_land_on_the_wrapper_with_class_and_style_merged()
    {
        var cut = RenderPicker(p => p.AddUnmatched("class", "mine").AddUnmatched("style", "margin:4px").AddUnmatched("data-test", "x"));

        var wrapper = cut.Find(".wss-color-picker");
        Assert.Contains("mine", wrapper.ClassList);
        Assert.Contains("wss-color-picker", wrapper.ClassList);
        Assert.Equal("margin:4px", wrapper.GetAttribute("style"));
        Assert.Equal("x", wrapper.GetAttribute("data-test"));
    }

    [Fact]
    public void The_placement_parameter_drives_the_panel_class()
    {
        var cut = RenderPicker(p => p.Add(c => c.Placement, PopupPlacement.Right));
        Open(cut);

        Assert.Contains("wss-color-picker-right", cut.Find(".wss-color-picker-panel").ClassList);
    }
}
