using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditRange{T}"/> — the AntD-style slider form control. These cover the
/// pure-C# halves: binding, snapping, the keyboard model, marks/dots rendering, the read-only view,
/// and the ARIA wiring. The JS-owned half (the real pointer drag through <c>wss-slider.js</c>, and
/// the arrow-key page-scroll suppression) is covered by <c>EditRangeE2ETests</c> — bUnit executes no
/// JavaScript. The drag *report* channel is reachable here, though: the module writes into a hidden
/// input, and dispatching an input event on it is an ordinary Blazor event.
/// </summary>
public class EditRangeTests : BunitContext
{
    // Loose so the lazy wss-slider.js import "succeeds" the way it does in a real browser -- which is
    // also what gates the no-JS @onclick fallback off (see the Strict-mode tests further down, and
    // ColorPickerTests' identical split).
    public EditRangeTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // EditRange.TrackWidth: the width the no-JS click fallback normalizes OffsetX against.
    const double TrackWidth = 320d;

    class RangeModel
    {
        [DisplayName("Volume Level")]
        public int Volume { get; set; } = 40;
    }

    class DoubleRangeModel
    {
        // The same three settings the Min/Max/Step parameters carry, declared on the model instead.
        [MinValue(0), MaxValue(5), Step(0.5)]
        public double Rating { get; set; } = 3.5;
    }

    class NullableRangeModel
    {
        [Required]
        public int? Priority { get; set; }
    }

    IRenderedComponent<ContainerFragment> RenderRange(RangeModel model, Action<RenderTreeBuilder>? extra = null)
    {
        Expression<Func<int>> field = () => model.Volume;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRange<int>>(0);
            b.AddAttribute(1, "Value", model.Volume);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int>(this, v => model.Volume = v));
            extra?.Invoke(b);
            b.CloseComponent();
        }));
    }

    static IElement Track(IRenderedComponent<ContainerFragment> cut) => cut.Find(".edit-range-track");

    static void Press(IRenderedComponent<ContainerFragment> cut, string key) =>
        Track(cut).KeyDown(new KeyboardEventArgs { Key = key });

    // The hidden input wss-slider.js reports "x,pressed" through.
    static void Drag(IRenderedComponent<ContainerFragment> cut, string payload) =>
        cut.Find(".edit-range-signal").Input(payload);

    static readonly Dictionary<decimal, string> ThreeMarks = new()
    {
        [0] = "Off",
        [50] = "Half",
        [100] = "Full"
    };

    // ----- Binding -----------------------------------------------------------

    [Fact]
    public void A_drag_report_round_trips_through_bind_Value()
    {
        var model = new RangeModel();
        var cut = RenderRange(model);

        Drag(cut, "0.75,1");

        Assert.Equal(75, model.Volume);
        Assert.Equal("75", Track(cut).GetAttribute("aria-valuenow"));
    }

    [Fact]
    public void A_repeated_report_of_the_already_bound_value_commits_nothing()
    {
        var model = new RangeModel();
        var commits = 0;
        Expression<Func<int>> field = () => model.Volume;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRange<int>>(0);
            b.AddAttribute(1, "Value", model.Volume);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int>(this, v =>
            {
                commits++;
                model.Volume = v;
            }));
            b.CloseComponent();
        }));

        // Three frames of a drag that never leaves the same step -- one commit, not three (each one
        // would cost a render, and a network round trip on Blazor Server).
        Drag(cut, "0.6,1");
        Drag(cut, "0.601,1");
        Drag(cut, "0.6,1");

        Assert.Equal(60, model.Volume);
        Assert.Equal(1, commits);
    }

    [Fact]
    public void The_release_report_ends_the_drag_without_committing_again()
    {
        var model = new RangeModel();
        var cut = RenderRange(model);

        Drag(cut, "0.75,1");
        Assert.Contains("edit-range-tooltip-visible", cut.Find(".edit-range-tooltip").ClassName);

        Drag(cut, "0.75,0");

        Assert.Equal(75, model.Volume);
        Assert.DoesNotContain("edit-range-tooltip-visible", cut.Find(".edit-range-tooltip").ClassName);
    }

    // ----- Positioning -------------------------------------------------------

    [Fact]
    public void The_handle_and_the_fill_are_positioned_by_percent_of_the_range()
    {
        var cut = RenderRange(new RangeModel { Volume = 40 });

        Assert.Equal("left:40%", cut.Find(".edit-range-handle").GetAttribute("style"));
        Assert.Equal("width:40%", cut.Find(".edit-range-fill").GetAttribute("style"));
    }

    [Fact]
    public void A_non_zero_Min_positions_against_the_span_not_the_raw_value()
    {
        var cut = RenderRange(new RangeModel { Volume = 10 }, b =>
        {
            b.AddAttribute(10, "Min", -20m);
            b.AddAttribute(11, "Max", 40m);
        });

        // 10 sits half way along -20..40.
        Assert.Equal("left:50%", cut.Find(".edit-range-handle").GetAttribute("style"));
    }

    [Fact]
    public void Included_false_renders_no_filled_track_and_no_active_marks()
    {
        var cut = RenderRange(new RangeModel { Volume = 50 }, b =>
        {
            b.AddAttribute(10, "Included", false);
            b.AddAttribute(11, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks);
        });

        Assert.Empty(cut.FindAll(".edit-range-fill"));
        Assert.Empty(cut.FindAll(".edit-range-mark-active"));
        Assert.Equal(3, cut.FindAll(".edit-range-mark").Count);
    }

    [Fact]
    public void Marks_at_or_below_the_value_are_active_while_Included_is_on()
    {
        var cut = RenderRange(new RangeModel { Volume = 50 }, b =>
            b.AddAttribute(10, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks));

        // Off (0) and Half (50), not Full (100).
        Assert.Equal(2, cut.FindAll(".edit-range-mark-active").Count);
    }

    // ----- Dots --------------------------------------------------------------

    [Fact]
    public void Dots_renders_one_per_step_increment_inclusive_of_both_ends()
    {
        var cut = RenderRange(new RangeModel { Volume = 30 }, b =>
        {
            b.AddAttribute(10, "Dots", true);
            b.AddAttribute(11, "Step", 10m);
        });

        Assert.Equal(11, cut.FindAll(".edit-range-dot").Count); // 0, 10, ... 100
        Assert.Equal(4, cut.FindAll(".edit-range-dot-active").Count); // 0, 10, 20, 30
    }

    [Fact]
    public void A_step_that_would_flood_the_rail_drops_the_step_dots_but_keeps_the_marks()
    {
        var cut = RenderRange(new RangeModel { Volume = 50 }, b =>
        {
            b.AddAttribute(10, "Dots", true);
            b.AddAttribute(11, "Step", 0.1m); // 1001 dots -- well past EditRange.MaxDots
            b.AddAttribute(12, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks);
        });

        Assert.Equal(3, cut.FindAll(".edit-range-dot").Count); // the marks' own dots only
    }

    // ----- Keyboard ----------------------------------------------------------

    [Fact]
    public void Arrow_keys_step_the_value_in_both_directions()
    {
        var model = new RangeModel();
        var cut = RenderRange(model);

        Press(cut, "ArrowRight");
        Assert.Equal(41, model.Volume);
        Press(cut, "ArrowUp");
        Assert.Equal(42, model.Volume);
        Press(cut, "ArrowLeft");
        Assert.Equal(41, model.Volume);
        Press(cut, "ArrowDown");
        Assert.Equal(40, model.Volume);
    }

    [Fact]
    public void Home_and_End_jump_to_the_bounds_and_the_arrows_clamp_there()
    {
        var model = new RangeModel();
        var cut = RenderRange(model);

        Press(cut, "End");
        Assert.Equal(100, model.Volume);
        Press(cut, "ArrowRight");
        Assert.Equal(100, model.Volume);

        Press(cut, "Home");
        Assert.Equal(0, model.Volume);
        Press(cut, "ArrowLeft");
        Assert.Equal(0, model.Volume);
    }

    [Fact]
    public void PageUp_and_PageDown_move_ten_steps()
    {
        var model = new RangeModel();
        var cut = RenderRange(model, b => b.AddAttribute(10, "Step", 2m));

        Press(cut, "PageUp");
        Assert.Equal(60, model.Volume); // 40 + 10 x 2
        Press(cut, "PageDown");
        Assert.Equal(40, model.Volume);
    }

    [Fact]
    public void An_unhandled_key_leaves_the_value_alone()
    {
        var model = new RangeModel();
        var cut = RenderRange(model);

        Press(cut, "a");

        Assert.Equal(40, model.Volume);
    }

    [Fact]
    public void SnapToMarks_moves_the_keyboard_between_adjacent_marks()
    {
        var model = new RangeModel { Volume = 50 };
        var cut = RenderRange(model, b =>
        {
            b.AddAttribute(10, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks);
            b.AddAttribute(11, "SnapToMarks", true);
        });

        Press(cut, "ArrowRight");
        Assert.Equal(100, model.Volume);
        Press(cut, "ArrowRight"); // already at the last mark
        Assert.Equal(100, model.Volume);
        Press(cut, "ArrowLeft");
        Assert.Equal(50, model.Volume);
        Press(cut, "ArrowLeft");
        Assert.Equal(0, model.Volume);
    }

    [Fact]
    public void SnapToMarks_pulls_a_pointer_report_onto_the_nearest_mark()
    {
        var model = new RangeModel();
        var cut = RenderRange(model, b =>
        {
            b.AddAttribute(10, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks);
            b.AddAttribute(11, "SnapToMarks", true);
        });

        Drag(cut, "0.62,1"); // 62 -> the Half mark, not 62

        Assert.Equal(50, model.Volume);
    }

    [Fact]
    public void A_mark_label_click_commits_that_marks_value()
    {
        var model = new RangeModel();
        var cut = RenderRange(model, b =>
            b.AddAttribute(10, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks));

        cut.FindAll(".edit-range-mark")[2].Click(); // "Full"

        Assert.Equal(100, model.Volume);
    }

    // ----- Snapping ----------------------------------------------------------

    [Fact]
    public void A_report_snaps_to_the_nearest_step_increment_anchored_at_Min()
    {
        var model = new RangeModel();
        var cut = RenderRange(model, b =>
        {
            b.AddAttribute(10, "Min", 5m);
            b.AddAttribute(11, "Max", 105m);
            b.AddAttribute(12, "Step", 10m);
        });

        // 5 + 0.43 x 100 = 48 -> the offered increments are 5/15/25/... so 45, not 50.
        Drag(cut, "0.43,1");

        Assert.Equal(45, model.Volume);
    }

    [Fact]
    public void Model_declared_bounds_and_step_drive_the_control_with_no_markup_parameters()
    {
        var model = new DoubleRangeModel();
        Expression<Func<double>> field = () => model.Rating;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRange<double>>(0);
            b.AddAttribute(1, "Value", model.Rating);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<double>(this, v => model.Rating = v));
            b.CloseComponent();
        }));

        var track = Track(cut);
        Assert.Equal("0", track.GetAttribute("aria-valuemin"));
        Assert.Equal("5", track.GetAttribute("aria-valuemax"));
        Assert.Equal("3.5", track.GetAttribute("aria-valuenow"));

        Press(cut, "ArrowRight");

        Assert.Equal(4d, model.Rating); // [Step(0.5)]
    }

    // ----- The no-JS click fallback ------------------------------------------
    // Strict JSInterop, not this class's usual Loose: under Loose, bUnit hands back a working fake
    // module for the `import`, so initSlider "succeeds" and the component correctly treats the press
    // as JS-owned (which gates the fallback off). Strict makes the import throw, which is what a real
    // prerender / unreachable-asset host does. The real-browser counterpart is EditRangeE2ETests.

    [Fact]
    public void A_track_click_positions_from_the_event_offset_when_there_is_no_js()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var model = new RangeModel();
        var cut = RenderRange(model);

        Track(cut).Click(new MouseEventArgs { OffsetX = TrackWidth / 2 });

        Assert.Equal(50, model.Volume);
    }

    [Fact]
    public void A_click_past_either_end_of_the_track_clamps_to_the_bounds()
    {
        JSInterop.Mode = JSRuntimeMode.Strict;
        var model = new RangeModel();
        var cut = RenderRange(model);

        Track(cut).Click(new MouseEventArgs { OffsetX = TrackWidth + 40 });
        Assert.Equal(100, model.Volume);

        Track(cut).Click(new MouseEventArgs { OffsetX = -40 });
        Assert.Equal(0, model.Volume);
    }

    [Fact]
    public void A_click_is_inert_once_the_drag_module_owns_the_track()
    {
        // The Loose default stands in for "wss-slider.js loaded": the click that a pointerdown-driven
        // drag also produces must not report a second time.
        var model = new RangeModel();
        var cut = RenderRange(model);

        Track(cut).Click(new MouseEventArgs { OffsetX = TrackWidth / 2 });

        Assert.Equal(40, model.Volume);
    }

    // ----- Empty and disabled states -----------------------------------------

    [Fact]
    public void A_null_value_parks_the_handle_at_Min_without_committing_anything()
    {
        var model = new NullableRangeModel();
        Expression<Func<int?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRange<int?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Min", 10m);
            b.AddAttribute(5, "Max", 20m);
            b.CloseComponent();
        }));

        var track = Track(cut);
        Assert.Equal("10", track.GetAttribute("aria-valuenow"));
        Assert.Equal("left:0%", cut.Find(".edit-range-handle").GetAttribute("style"));
        Assert.Null(model.Priority); // rendering at Min is not a commit

        // ...and the first interaction is what writes a real value.
        track.KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal(11, model.Priority);
    }

    [Fact]
    public void A_disabled_control_ignores_every_input_channel()
    {
        var model = new RangeModel();
        var cut = RenderRange(model, b =>
        {
            b.AddAttribute(10, "IsDisabled", true);
            b.AddAttribute(11, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks);
        });

        Press(cut, "ArrowRight");
        Drag(cut, "0.9,1");
        cut.FindAll(".edit-range-mark")[2].Click();

        Assert.Equal(40, model.Volume);
        var track = Track(cut);
        Assert.Equal("true", track.GetAttribute("aria-disabled"));
        Assert.Equal("-1", track.GetAttribute("tabindex")); // still reachable by a touch tap, never by Tab
        Assert.Contains("edit-range-disabled", cut.Find(".edit-range").ClassName);
    }

    // ----- Tooltip + formatting ----------------------------------------------

    [Fact]
    public void The_value_bubble_renders_the_formatted_value_and_is_hidden_from_assistive_tech()
    {
        var cut = RenderRange(new RangeModel { Volume = 40 }, b =>
            b.AddAttribute(10, "TooltipFormat", "0 GB"));

        var tooltip = cut.Find(".edit-range-tooltip");
        Assert.Equal("40 GB", tooltip.TextContent);
        Assert.Equal("true", tooltip.GetAttribute("aria-hidden"));
        Assert.Equal("left:40%", tooltip.GetAttribute("style"));
    }

    [Fact]
    public void ShowTooltip_false_renders_no_bubble_at_all()
    {
        var cut = RenderRange(new RangeModel(), b => b.AddAttribute(10, "ShowTooltip", false));

        Assert.Empty(cut.FindAll(".edit-range-tooltip"));
    }

    [Fact]
    public void Read_only_mode_shows_the_formatted_value_as_text_and_no_track()
    {
        var cut = RenderRange(new RangeModel { Volume = 40 }, b =>
        {
            b.AddAttribute(10, "IsEditMode", false);
            b.AddAttribute(11, "TooltipFormat", "0 GB");
        });

        Assert.Empty(cut.FindAll(".edit-range-track"));
        Assert.Equal("40 GB", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    // ----- ARIA --------------------------------------------------------------

    [Fact]
    public void The_track_carries_the_full_slider_role_and_range_state()
    {
        var cut = RenderRange(new RangeModel { Volume = 40 }, b =>
        {
            b.AddAttribute(10, "Min", 0m);
            b.AddAttribute(11, "Max", 200m);
        });

        var track = Track(cut);
        Assert.Equal("slider", track.GetAttribute("role"));
        Assert.Equal("horizontal", track.GetAttribute("aria-orientation"));
        Assert.Equal("0", track.GetAttribute("aria-valuemin"));
        Assert.Equal("200", track.GetAttribute("aria-valuemax"));
        Assert.Equal("40", track.GetAttribute("aria-valuenow"));
        Assert.Equal("0", track.GetAttribute("tabindex"));
        // No aria-valuetext: the bare number already reads correctly, and duplicating aria-valuenow
        // would just make a screen reader say the value twice.
        Assert.False(track.HasAttribute("aria-valuetext"));
    }

    [Fact]
    public void A_format_string_supplies_the_human_reading_through_aria_valuetext()
    {
        var cut = RenderRange(new RangeModel { Volume = 40 }, b =>
            b.AddAttribute(10, "TooltipFormat", "0 GB"));

        var track = Track(cut);
        Assert.Equal("40", track.GetAttribute("aria-valuenow")); // still the bare number
        Assert.Equal("40 GB", track.GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void A_value_sitting_exactly_on_a_mark_announces_that_marks_label()
    {
        var cut = RenderRange(new RangeModel { Volume = 50 }, b =>
            b.AddAttribute(10, "Marks", (IReadOnlyDictionary<decimal, string>)ThreeMarks));

        Assert.Equal("Half", Track(cut).GetAttribute("aria-valuetext"));
    }

    [Fact]
    public void The_track_takes_its_accessible_name_from_the_labels_naming_anchor()
    {
        var cut = RenderRange(new RangeModel());

        var label = cut.Find("label.edit-label");
        // A role="slider" <div> is not labelable, so the label carries no `for` and the track points
        // back at the naming anchor instead.
        Assert.False(label.HasAttribute("for"));
        Assert.Equal("lbltext-Volume", Track(cut).GetAttribute("aria-labelledby"));
        Assert.Equal("Volume Level", cut.Find("#lbltext-Volume").TextContent.Trim());
    }

    [Fact]
    public void A_failed_submit_marks_the_track_invalid_and_points_it_at_the_message()
    {
        var model = new NullableRangeModel();
        Expression<Func<int?>> field = () => model.Priority;
        var cut = Render(WithValidatedForm(model, true, b =>
        {
            b.OpenComponent<EditRange<int?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => model.Priority = v));
            b.CloseComponent();
        }));

        Assert.False(Track(cut).HasAttribute("aria-invalid"));
        Assert.Equal("true", Track(cut).GetAttribute("aria-required"));
        var describedBy = Track(cut).GetAttribute("aria-describedby");
        Assert.Contains("error-msg-Priority", describedBy);

        cut.Find("form").Submit();

        var track = Track(cut);
        Assert.Equal("true", track.GetAttribute("aria-invalid"));
        Assert.Equal("error-msg-Priority", track.GetAttribute("aria-errormessage"));
        Assert.NotEmpty(cut.Find("#error-msg-Priority").QuerySelectorAll("div"));
        // The EditContext's own state class lands on the track, which is what the invalid CSS keys off.
        Assert.Contains("invalid", track.ClassName);
    }

    [Fact]
    public void A_consumer_attribute_lands_on_the_track_and_a_consumer_style_on_the_wrapper()
    {
        var cut = RenderRange(new RangeModel(), b =>
        {
            b.AddAttribute(10, "data-role", "volume");
            b.AddAttribute(11, "style", "margin-top: 12px");
            b.AddAttribute(12, "class", "tall");
        });

        Assert.Equal("volume", Track(cut).GetAttribute("data-role"));
        Assert.Contains("margin-top: 12px", cut.Find(".edit-control-wrapper").GetAttribute("style"));
        Assert.Contains("tall", Track(cut).ClassName); // `class` travels CssClass onto the field element
    }
}
