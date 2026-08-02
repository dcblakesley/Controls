using System.ComponentModel.DataAnnotations;
using System.Globalization;
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

        // int.MinValue/int.MaxValue are int's own "no bound" idiom, not long's -- on a long property
        // this is a genuine "must fit in an int" constraint, so both bounds must render (Finding: the
        // type-blind RangeSentinels regression). Type-gating threads through EditNumber<long?>'s own
        // UnderlyingNumericType.
        [Range(int.MinValue, int.MaxValue)]
        public long? WithIntExtremesOnLong { get; set; }

        // Same constraint as WithIntExtremesOnLong, but via the (Type, string, string) ctor with
        // OperandType=long: RangeAttribute's (int, int) ctor converts the VALUE to Int32 before
        // comparing (Convert.ToInt32), which THROWS OverflowException -- uncaught by RangeAttribute
        // itself -- for a genuinely out-of-int-range long, rather than failing gracefully. That's a
        // pre-existing BCL sharp edge unrelated to this fix, not something to route around silently in
        // production code; the string-ctor spelling here (which real code migrating to a wider type
        // would use to avoid the crash) compares as long throughout, so validation degrades to a normal,
        // message-producing failure -- what the end-to-end test below actually exercises.
        [Range(typeof(long), "-2147483648", "2147483647", ParseLimitsInInvariantCulture = true)]
        public long? WithIntExtremesOnLongViaStringCtor { get; set; }

        // The mirror image of WithIntExtremesOnLong: the SAME int extremes on types that cannot reach
        // them. [Range(0, int.MaxValue)] is the "non-negative integer" idiom, and on a short/byte the
        // ceiling is unreachable -- a max attribute of 2147483647 on a short input is a limit the
        // control can never enforce and the user can never approach, so only the floor renders.
        // (Regression: short/byte carry no extreme row of their own, so an exact-row-only type gate
        // stopped recognizing the idiom and rendered both bounds.)
        [Range(0, int.MaxValue)]
        public short? NonNegativeShort { get; set; }

        [Range(0, int.MaxValue)]
        public byte? NonNegativeByte { get; set; }

        // Same on the min side: no short is below int.MinValue, so only the cap is real.
        [Range(int.MinValue, 100)]
        public short? CappedShort { get; set; }
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
    public void Int_extreme_Range_bounds_render_as_real_min_and_max_on_a_long_property()
    {
        // Regression coverage for the type-blind RangeSentinels defect: [Range(int.MinValue,
        // int.MaxValue)] used to be suppressed on EVERY numeric type (rendering no min/max at all),
        // even though on a long property it's a real "must fit in an int" bound -- 5000000000 violates
        // it. EditNumber<long?> now passes its own UnderlyingNumericType through, so both bounds render.
        var model = new MinMaxModel();
        Expression<Func<long?>> field = () => model.WithIntExtremesOnLong;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<long?>>(0);
            b.AddAttribute(1, "Value", model.WithIntExtremesOnLong);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal(int.MinValue.ToString(CultureInfo.InvariantCulture), input.GetAttribute("min"));
        Assert.Equal(int.MaxValue.ToString(CultureInfo.InvariantCulture), input.GetAttribute("max"));
    }

    [Fact]
    public void Int_extreme_Range_max_renders_as_min_only_on_a_short_property()
    {
        // The other direction from the test above, and the regression the exact-row type gate caused:
        // int.MaxValue is not short's OWN extreme, but it is outside everything a short can hold, so
        // it is the vacuous half of [Range(0, int.MaxValue)] -- min="0" and no max at all.
        var model = new MinMaxModel();
        Expression<Func<short?>> field = () => model.NonNegativeShort;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<short?>>(0);
            b.AddAttribute(1, "Value", model.NonNegativeShort);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("0", input.GetAttribute("min"));
        Assert.False(input.HasAttribute("max"));
    }

    [Fact]
    public void Int_extreme_Range_max_renders_as_min_only_on_a_byte_property()
    {
        // byte has no extreme row either, and unlike the short above its own ceiling (255) is a
        // magnitude real bounds use -- which is exactly why "is this SOME type's extreme" has to stay
        // part of the rule: [Range(0, 255)] on a byte still renders max="255" (ValidationHelperTests'
        // Range_spanning_a_byte_type_in_full_names_both_bounds), only the unreachable one is dropped.
        var model = new MinMaxModel();
        Expression<Func<byte?>> field = () => model.NonNegativeByte;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<byte?>>(0);
            b.AddAttribute(1, "Value", model.NonNegativeByte);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("0", input.GetAttribute("min"));
        Assert.False(input.HasAttribute("max"));
    }

    [Fact]
    public void Int_extreme_Range_min_renders_as_max_only_on_a_short_property()
    {
        var model = new MinMaxModel();
        Expression<Func<short?>> field = () => model.CappedShort;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<short?>>(0);
            b.AddAttribute(1, "Value", model.CappedShort);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.False(input.HasAttribute("min"));
        Assert.Equal("100", input.GetAttribute("max"));
    }

    [Fact]
    public void Unreachable_Range_bound_on_a_short_property_renders_the_one_sided_message()
    {
        // The message layer through the REAL reflection path (valueType "System.Nullable`1[System.Int16]"),
        // proving it reaches the same verdict as the rendered min/max above: a form that shows no max
        // must not then tell the user their entry has to be "between 0 and 2147483647".
        var model = new MinMaxModel { NonNegativeShort = -1 };
        var editContext = new EditContext(model);
        Expression<Func<short?>> field = () => model.NonNegativeShort;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions { ShowFieldNameInValidation = false }, content =>
        {
            content.OpenComponent<EditNumber<short?>>(0);
            content.AddAttribute(1, "Value", model.NonNegativeShort);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Equal("Must be at least 0", cut.Find(".edit-validation-message:not(.edit-sr-only) > div").TextContent);
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

    [Fact]
    public void Int_extreme_Range_violation_on_a_long_property_renders_the_real_bounded_message()
    {
        // End-to-end proof of the type-blindness fix, through the REAL reflection path
        // (FieldValidationDisplay.GetPropertyTypeName -> "System.Int64", not a hand-typed string):
        // 5000000000 is a valid long but fails this int-sized bound, and the rendered message must name
        // the true bound rather than collapse to "Must be a number".
        var model = new MinMaxModel { WithIntExtremesOnLongViaStringCtor = 5_000_000_000L };
        var editContext = new EditContext(model);
        Expression<Func<long?>> field = () => model.WithIntExtremesOnLongViaStringCtor;
        // ShowFieldNameInValidation: false so the visible region is the short rewritten form (no label
        // prefix) -- FormOptions.DefaultShowFieldNameInValidation is true, and this test isn't about
        // label rendering.
        var cut = Render(RenderValidatedForm(editContext, new FormOptions { ShowFieldNameInValidation = false }, content =>
        {
            content.OpenComponent<EditNumber<long?>>(0);
            content.AddAttribute(1, "Value", model.WithIntExtremesOnLongViaStringCtor);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        // The visible (non-screen-reader) region -- the short form without the label prefix; see
        // FieldValidationDisplay.razor's two regions and FieldValidationDisplayTests' RenderMessages.
        var message = cut.Find(".edit-validation-message:not(.edit-sr-only) > div").TextContent;
        Assert.Equal(
            $"Must be between {int.MinValue.ToString(CultureInfo.InvariantCulture)} and {int.MaxValue.ToString(CultureInfo.InvariantCulture)}",
            message);
    }
}
