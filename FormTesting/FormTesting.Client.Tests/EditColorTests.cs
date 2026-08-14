using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditColor"/> — the form-control wrapper around the
/// <see cref="ColorPicker"/> UI-kit popup. These cover the layer this control adds over the picker
/// itself (EditContext binding, validation, label, read-only view, parameter forwarding); the picker's
/// own open/close/drag/keyboard behavior is covered by <c>ColorPickerTests</c>, and its JS-owned parts
/// by <c>EditColorE2ETests</c>.
/// </summary>
public class EditColorTests : BunitContext
{
    public EditColorTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay/color JS imports

    class ColorModel
    {
        [DisplayName("Brand Color")]
        public string? Brand { get; set; }
    }

    class RequiredColorModel
    {
        [Required]
        public string? Accent { get; set; }
    }

    IRenderedComponent<ContainerFragment> RenderColor(ColorModel model, Action<RenderTreeBuilder>? extra = null)
    {
        Expression<Func<string?>> field = () => model.Brand;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditColor>(0);
            b.AddAttribute(1, "Value", model.Brand);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => model.Brand = v));
            extra?.Invoke(b);
            b.CloseComponent();
        }));
    }

    static void Open(IRenderedComponent<ContainerFragment> cut) => cut.Find(".wss-color-picker-trigger").Click();

    // The saturation/value area's drag report channel -- see ColorPickerTests.Signals.
    static void DragSaturation(IRenderedComponent<ContainerFragment> cut, string payload) =>
        cut.FindAll(".wss-color-picker-signal")[0].Input(payload);

    static string MessageFor(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find($"#error-msg-{cut.Find(".wss-color-picker-trigger").GetAttribute("id")}").TextContent;

    // ----- Binding -----------------------------------------------------------

    [Fact]
    public void A_committed_color_round_trips_through_bind_Value()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model);

        Open(cut);
        DragSaturation(cut, "0.5,0.5");

        Assert.Equal("#804040", model.Brand);
        // ...and the new value is what the trigger now shows.
        Assert.Contains("rgb(128, 64, 64)", cut.Find(".wss-color-picker-swatch-fill").GetAttribute("style"));
    }

    [Fact]
    public void A_bound_in_rgb_string_is_accepted_and_normalized_on_the_next_commit()
    {
        var model = new ColorModel { Brand = "rgb(255, 0, 0)" };
        var cut = RenderColor(model);

        Open(cut);
        cut.Find(".wss-color-picker-hex").Change("#00ff00");

        Assert.Equal("#00ff00", model.Brand);
    }

    [Fact]
    public void Commit_notifies_the_EditContext_field_changed_event()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var editContext = new EditContext(model);
        var notifiedFields = new List<string>();
        editContext.OnFieldChanged += (_, e) => notifiedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<string?>> field = () => model.Brand;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditColor>(0);
                content.AddAttribute(1, "Value", model.Brand);
                content.AddAttribute(2, "ValueExpression", field);
                content.AddAttribute(3, "ValueChanged",
                    EventCallback.Factory.Create<string?>(this, v => model.Brand = v));
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        Open(cut);
        DragSaturation(cut, "0.5,0.5");

        Assert.Contains("Brand", notifiedFields);
    }

    // ----- Label + ARIA ------------------------------------------------------

    [Fact]
    public void The_model_DisplayName_drives_both_the_label_and_the_triggers_accessible_name()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model);

        Assert.Equal("Brand Color", cut.Find("label").TextContent.Trim());
        // The label's `for` points at the trigger button, so the button's aria-label wins the
        // accessible-name computation -- it has to start with the visible label text (WCAG 2.5.3),
        // which is why EditColor forwards the resolved field label as TriggerLabel.
        Assert.Equal("Brand", cut.Find("label").GetAttribute("for"));
        Assert.Equal("Brand Color: #ff0000", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void An_explicit_TriggerLabel_wins_over_the_field_label()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "TriggerLabel", "Pick a swatch"));

        Assert.Equal("Pick a swatch: #ff0000", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void Required_attribute_flows_to_the_star_and_aria_required()
    {
        var model = new RequiredColorModel();
        Expression<Func<string?>> field = () => model.Accent;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditColor>(0);
            b.AddAttribute(1, "Value", model.Accent);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        // aria-required reaches the picker's trigger button via ColorPicker's AriaRequired parameter
        // (the same forwarding shape EditDate -> DatePicker uses).
        Assert.Equal("true", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-required"));
    }

    [Fact]
    public void An_invalid_field_marks_the_trigger_aria_invalid_and_points_at_its_message()
    {
        var model = new RequiredColorModel(); // Accent empty -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string?>> field = () => model.Accent;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditColor>(1);
                content.AddAttribute(2, "Value", model.Accent);
                content.AddAttribute(3, "ValueExpression", field);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var trigger = cut.Find(".wss-color-picker-trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-invalid"));
        Assert.Equal("error-msg-Accent", trigger.GetAttribute("aria-errormessage"));
        Assert.Contains("error-msg-Accent", trigger.GetAttribute("aria-describedby"));
        // The EditContext state classes reach the picker wrapper (its documented splat target), which
        // is what the .wss-color-picker.invalid styling hangs off.
        Assert.Contains("invalid", cut.Find(".wss-color-picker").ClassList);
    }

    // ----- Parameter forwarding ---------------------------------------------

    [Fact]
    public void IsDisabled_disables_the_trigger_and_keeps_the_popup_shut()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "IsDisabled", true));

        Assert.NotNull(cut.Find(".wss-color-picker-trigger").GetAttribute("disabled"));
        cut.Find(".wss-color-picker-trigger-slot").Click();

        Assert.Empty(cut.FindAll(".wss-color-picker-panel"));
    }

    [Fact]
    public void AllowClear_sets_the_bound_value_to_null()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "AllowClear", true));

        cut.Find(".wss-color-picker-clear").Click();

        Assert.Null(model.Brand);
        Assert.Empty(cut.FindAll(".wss-color-picker-clear"));
    }

    [Fact]
    public void No_clear_affordance_renders_by_default()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model);

        Assert.Empty(cut.FindAll(".wss-color-picker-clear"));
    }

    [Fact]
    public void A_preset_click_commits_that_color()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model, b =>
            b.AddAttribute(10, "Presets", (IReadOnlyList<string>)["#00ff00", "#0000ff"]));

        Open(cut);
        cut.FindAll(".wss-color-picker-preset")[0].Click();

        Assert.Equal("#00ff00", model.Brand);
    }

    [Fact]
    public void ShowAlpha_false_strips_the_alpha_channel_from_the_bound_value()
    {
        var model = new ColorModel { Brand = "#ff000080" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "ShowAlpha", false));

        Open(cut);
        Assert.Empty(cut.FindAll(".wss-color-picker-alpha"));
        DragSaturation(cut, "1,0");

        Assert.Equal("#ff0000", model.Brand);
    }

    [Fact]
    public void ShowText_renders_the_normalized_value_beside_the_swatch()
    {
        var model = new ColorModel { Brand = "rgba(255, 0, 0, 0.5)" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "ShowText", true));

        Assert.Equal("#ff000080", cut.Find(".wss-color-picker-value").TextContent);
    }

    // ----- Parse errors ------------------------------------------------------

    [Fact]
    public void An_unparseable_typed_entry_surfaces_the_ParsingErrorMessage_and_leaves_the_value_alone()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model);

        Open(cut);
        cut.Find(".wss-color-picker-hex").Input("not a color");
        cut.Find(".wss-color-picker-hex").Change("not a color");

        // {0} is the field's own FieldIdentifier.FieldName; the sr-only region shows
        // ValidationHelper's labeled rewrite of that message.
        Assert.Contains("must be a color", MessageFor(cut));
        Assert.Equal("#ff0000", model.Brand); // reverted, not committed
    }

    [Fact]
    public void A_custom_ParsingErrorMessage_is_used()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "ParsingErrorMessage", "{0} is not a color I know."));

        Open(cut);
        cut.Find(".wss-color-picker-hex").Change("nope");

        Assert.Contains("Brand is not a color I know.", MessageFor(cut));
    }

    [Fact]
    public void The_next_valid_commit_clears_the_parse_error()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var cut = RenderColor(model);

        Open(cut);
        cut.Find(".wss-color-picker-hex").Change("nope");
        Assert.Contains("must be a color", MessageFor(cut));

        cut.Find(".wss-color-picker-hex").Change("#00ff00");

        Assert.Equal(string.Empty, MessageFor(cut));
        Assert.Equal("#00ff00", model.Brand);
    }

    // ----- Read-only ---------------------------------------------------------

    [Fact]
    public void Read_only_mode_renders_the_normalized_value_instead_of_the_picker()
    {
        var model = new ColorModel { Brand = "rgb(255, 0, 170)" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "IsEditMode", false));

        Assert.Empty(cut.FindAll(".wss-color-picker"));
        Assert.Equal("#ff00aa", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_mode_renders_nothing_for_a_value_the_picker_could_not_use_either()
    {
        var model = new ColorModel { Brand = "chartreuse" };
        var cut = RenderColor(model, b => b.AddAttribute(10, "IsEditMode", false));

        // ReadOnlyValue's own empty state: a visibility:hidden "No Value" spacer, exactly what every
        // other control renders for an empty read-only field.
        Assert.Equal("No Value", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    // ----- Empty / hiding ----------------------------------------------------

    [Fact]
    public void An_empty_value_renders_the_empty_indicator()
    {
        var model = new ColorModel();
        var cut = RenderColor(model);

        Assert.Contains("wss-color-picker-swatch-empty", cut.Find(".wss-color-picker-swatch-fill").ClassList);
        Assert.Contains("no color", cut.Find(".wss-color-picker-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void An_empty_string_counts_as_semantically_empty_for_hiding()
    {
        // WhenReadOnlyAndNullOrDefault hides a control whose value is the type's semantic empty; for a
        // color that has to include "" and not just null (the base's EqualityComparer default would
        // only recognize null).
        var model = new ColorModel { Brand = "" };
        var cut = RenderColor(model, b =>
        {
            b.AddAttribute(10, "IsEditMode", false);
            b.AddAttribute(11, "Hiding", HidingMode.WhenReadOnlyAndNullOrDefault);
        });

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }
}
