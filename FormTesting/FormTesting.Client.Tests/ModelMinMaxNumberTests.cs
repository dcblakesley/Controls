using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the model-declared Min/Max resolution EditNumber wires through
/// <see cref="Controls.Helpers.AttributesHelper.MinNumber"/>/<see cref="Controls.Helpers.AttributesHelper.MaxNumber"/>:
/// the control's own <c>Min</c>/<c>Max</c> parameters win, else the bound property's
/// <c>[MinValue]</c>/<c>[MaxValue]</c> supplies the rendered <c>min</c>/<c>max</c> attributes, else
/// <c>[Range]</c> is the fallback, else the attribute is omitted entirely -- the extension-method
/// resolution itself is covered at the unit level in <c>AttributesHelperTests</c>, this file proves
/// EditNumber actually wires it through to the DOM. <see cref="MinValueAttribute"/>/
/// <see cref="MaxValueAttribute"/> are <see cref="ValidationAttribute"/>s too, so the last test proves
/// an out-of-range committed value still fails validation through the normal
/// <see cref="DataAnnotationsValidator"/> flow, independent of the min/max rendering.
/// </summary>
public class ModelMinMaxNumberTests : BunitContext
{
    class MinMaxModel
    {
        [MinValue(0)]
        [MaxValue(100)]
        public int? WithMinMaxAttrs { get; set; }

        public int? WithNoAttrs { get; set; }

        [Range(1, 50)]
        public int? WithRangeOnly { get; set; }

        // [MinValue] must win over [Range]'s lower bound; [Range]'s upper bound still supplies Max
        // because no [MaxValue] is present to out-rank it.
        [MinValue(10)]
        [Range(1, 50)]
        public int? WithMinValueAndRange { get; set; }

        // double.MaxValue overflows decimal, so MaxNumber() treats it as unbounded -- min renders,
        // max does not.
        [Range(0, double.MaxValue)]
        public double? WithUnrepresentableRangeMax { get; set; }

        [MinValue(0.5)]
        public double? WithDecimalMinValue { get; set; }

        [MinValue(0)]
        public int? ValidatedBound { get; set; }
    }

    [Fact]
    public void Model_declared_MinValue_and_MaxValue_attributes_render_min_and_max_when_no_parameter_is_set()
    {
        var model = new MinMaxModel();
        Expression<Func<int?>> field = () => model.WithMinMaxAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("0", input.GetAttribute("min"));
        Assert.Equal("100", input.GetAttribute("max"));
    }

    [Fact]
    public void Explicit_Min_and_Max_parameters_override_the_model_attributes()
    {
        var model = new MinMaxModel();
        Expression<Func<int?>> field = () => model.WithMinMaxAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Min", 5m);
            b.AddAttribute(5, "Max", 50m);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("5", input.GetAttribute("min"));
        Assert.Equal("50", input.GetAttribute("max"));
    }

    [Fact]
    public void Range_attribute_is_the_fallback_when_no_MinValue_or_MaxValue_is_present()
    {
        var model = new MinMaxModel();
        Expression<Func<int?>> field = () => model.WithRangeOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.WithRangeOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("1", input.GetAttribute("min"));
        Assert.Equal("50", input.GetAttribute("max"));
    }

    [Fact]
    public void MinValue_wins_over_Range_lower_bound_while_Range_upper_bound_still_supplies_Max()
    {
        var model = new MinMaxModel();
        Expression<Func<int?>> field = () => model.WithMinValueAndRange;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.WithMinValueAndRange);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("10", input.GetAttribute("min")); // [MinValue(10)] beats [Range(1, 50)]'s 1
        Assert.Equal("50", input.GetAttribute("max")); // no [MaxValue] present -> [Range]'s 50 falls through
    }

    [Fact]
    public void Range_bound_unrepresentable_as_decimal_is_treated_as_unbounded()
    {
        var model = new MinMaxModel();
        Expression<Func<double?>> field = () => model.WithUnrepresentableRangeMax;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.WithUnrepresentableRangeMax);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("0", input.GetAttribute("min"));
        Assert.False(input.HasAttribute("max")); // double.MaxValue overflows decimal -> omitted, not clamped
    }

    [Fact]
    public void Renders_no_min_or_max_attribute_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new MinMaxModel();
        Expression<Func<int?>> field = () => model.WithNoAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.False(input.HasAttribute("min"));
        Assert.False(input.HasAttribute("max"));
    }

    [Fact]
    public void Decimal_valued_MinValue_renders_InvariantCulture()
    {
        var model = new MinMaxModel();
        Expression<Func<double?>> field = () => model.WithDecimalMinValue;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.WithDecimalMinValue);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("0.5", cut.Find("input.edit-number-input").GetAttribute("min"));
    }

    [Fact]
    public void Out_of_range_committed_value_fails_validation_through_the_normal_DataAnnotationsValidator_flow()
    {
        var model = new MinMaxModel { ValidatedBound = -5 }; // below [MinValue(0)]
        var editContext = new EditContext(model);
        Expression<Func<int?>> field = () => model.ValidatedBound;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditNumber<int?>>(1);
                content.AddAttribute(2, "Value", model.ValidatedBound);
                content.AddAttribute(3, "ValueExpression", field);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        var message = cut.Find("#error-msg-ValidatedBound").TextContent;
        Assert.False(string.IsNullOrWhiteSpace(message));
    }
}
