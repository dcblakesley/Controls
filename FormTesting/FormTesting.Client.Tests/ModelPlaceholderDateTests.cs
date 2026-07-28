using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the model-declared <see cref="PlaceholderAttribute"/> (falling back to
/// <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/>'s <c>Prompt</c>) reaching the
/// two calendar-dropdown controls, <see cref="EditDatePicker{T}"/> and <see cref="EditDateRange"/>.
/// Covers the universal resolution precedence (the control's own placeholder parameter -> the model
/// attribute -> the control's built-in default) at the rendering layer; the attribute/extension logic
/// itself is covered by <c>AttributesHelperTests</c>.
/// </summary>
public class ModelPlaceholderDateTests : BunitContext
{
    public ModelPlaceholderDateTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ----- EditDatePicker<T> ---------------------------------------------------------------------

    class PlaceholderDateModel
    {
        [Placeholder("Pick a birthday")]
        public DateTime? ShipDate { get; set; }
    }

    [Fact]
    public void Placeholder_attribute_reaches_the_pickers_input_when_no_explicit_parameter_is_set()
    {
        var model = new PlaceholderDateModel();
        Expression<Func<DateTime?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Pick a birthday", cut.Find(".wss-picker-input-date").GetAttribute("placeholder"));
    }

    [Fact]
    public void Explicit_Placeholder_parameter_overrides_the_model_attribute()
    {
        var model = new PlaceholderDateModel();
        Expression<Func<DateTime?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Placeholder", "Explicit wins");
            b.CloseComponent();
        }));

        Assert.Equal("Explicit wins", cut.Find(".wss-picker-input-date").GetAttribute("placeholder"));
    }

    [Fact]
    public void Neither_parameter_nor_attribute_set_still_yields_the_modes_derived_default()
    {
        // The regression that matters most: EffectivePlaceholder must forward null (not substitute a
        // literal) so the inner DatePicker's own mode-derived default ("Select date" for Date mode)
        // still applies. PersonModel.BirthDate carries no [Placeholder]/[Display(Prompt)].
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Select date", cut.Find(".wss-picker-input-date").GetAttribute("placeholder"));
    }

    // ----- EditDateRange --------------------------------------------------------------------------

    // Both endpoints carry their own [Placeholder] so a single render proves each end resolves
    // against its OWN property's attributes -- a value on Start must never leak onto End, or vice versa.
    class PlaceholderRangeModel
    {
        [Placeholder("Pick start")]
        public DateTime? Start { get; set; }

        [Placeholder("Pick end")]
        public DateTime? End { get; set; }
    }

    [Fact]
    public void Placeholder_attribute_on_Start_lands_on_the_start_input_only()
    {
        var model = new PlaceholderRangeModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.Equal("Pick start", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
        Assert.NotEqual("Pick start", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
    }

    [Fact]
    public void Placeholder_attribute_on_End_lands_on_the_end_input_only()
    {
        var model = new PlaceholderRangeModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.Equal("Pick end", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
        Assert.NotEqual("Pick end", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
    }

    [Fact]
    public void Explicit_StartPlaceholder_and_EndPlaceholder_each_override_their_own_ends_attribute()
    {
        var model = new PlaceholderRangeModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "StartPlaceholder", "Explicit start wins");
            b.AddAttribute(6, "EndPlaceholder", "Explicit end wins");
            b.CloseComponent();
        }));

        Assert.Equal("Explicit start wins", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
        Assert.Equal("Explicit end wins", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
    }

    [Fact]
    public void Neither_parameter_nor_attribute_set_still_yields_the_pickers_default_for_both_ends()
    {
        // Same null-preserving regression guard as EditDatePicker's, per end: this RangeModel carries
        // no [Placeholder]/[Display(Prompt)] on either property, so both inputs must still show
        // DateRangePicker's own DefaultPlaceholder (the uppercased EffectiveFormat -- "MM/DD/YYYY" for
        // the default Date mode).
        var model = new RangeModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.Equal("MM/DD/YYYY", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
        Assert.Equal("MM/DD/YYYY", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
    }

    // A dedicated two-property model mirroring EditDateRangeTests' own RangeModel (that class is
    // private to its own test file, so this file declares an identically-shaped one locally rather
    // than reaching across files).
    class RangeModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }
}
