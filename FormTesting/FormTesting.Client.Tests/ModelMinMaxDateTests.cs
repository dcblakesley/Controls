using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the model-declared <see cref="MinValueAttribute"/>/<see cref="MaxValueAttribute"/>
/// (falling back to <see cref="RangeAttribute"/>) reaching the two single-date controls,
/// <see cref="EditDate{T}"/> and <see cref="EditDateNative{T}"/>, via <c>EffectiveMin</c>/<c>EffectiveMax</c>.
/// <see cref="EditDate{T}"/>'s Min/Max only affect which days the inner <see cref="DatePicker"/>
/// disables (no inspectable DOM attribute), so its assertions read the resolved bounds off the
/// rendered <see cref="DatePicker"/> instance -- the same approach <c>ModelMinMaxDateRangeTests</c>
/// uses for <see cref="EditDateRange"/>'s shared <see cref="DateRangePicker"/>.
/// <see cref="EditDateNative{T}"/> renders native <c>min</c>/<c>max</c> attributes directly, so its
/// assertions read the DOM, matching <c>ModelMinMaxNumberTests</c>' approach for
/// <see cref="EditNumber{T}"/>. The extension-method resolution itself is covered at the unit level
/// in <c>AttributesHelperTests</c>; this file proves both controls actually wire it through.
/// </summary>
public class ModelMinMaxDateTests : BunitContext
{
    public ModelMinMaxDateTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static readonly DateTime Jan1_2024 = new(2024, 1, 1);
    static readonly DateTime Dec31_2024 = new(2024, 12, 31);

    // ----- EditDate<T> -----------------------------------------------------------------------

    class MinMaxDateModel
    {
        [MinValue("2024-01-01")]
        [MaxValue("2024-12-31")]
        public DateTime? WithMinMaxAttrs { get; set; }

        public DateTime? WithNoAttrs { get; set; }

        [Range(typeof(DateTime), "2024-01-01", "2024-12-31")]
        public DateTime? WithRangeOnly { get; set; }
    }

    static DatePicker Picker(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponent<DatePicker>().Instance;

    [Fact]
    public void MinValue_and_MaxValue_attributes_reach_the_pickers_Min_and_Max_when_no_parameter_is_set()
    {
        var model = new MinMaxDateModel();
        Expression<Func<DateTime?>> field = () => model.WithMinMaxAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var picker = Picker(cut);
        Assert.Equal(Jan1_2024, picker.Min);
        Assert.Equal(Dec31_2024, picker.Max);
    }

    [Fact]
    public void Explicit_Min_and_Max_parameters_win_over_the_model_attributes()
    {
        var model = new MinMaxDateModel();
        Expression<Func<DateTime?>> field = () => model.WithMinMaxAttrs;
        var explicitMin = new DateTime(2023, 6, 1);
        var explicitMax = new DateTime(2023, 6, 30);
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Min", explicitMin);
            b.AddAttribute(4, "Max", explicitMax);
            b.CloseComponent();
        }));

        var picker = Picker(cut);
        Assert.Equal(explicitMin, picker.Min);
        Assert.Equal(explicitMax, picker.Max);
    }

    [Fact]
    public void Range_attribute_is_the_fallback_when_no_MinValue_or_MaxValue_is_present()
    {
        var model = new MinMaxDateModel();
        Expression<Func<DateTime?>> field = () => model.WithRangeOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithRangeOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var picker = Picker(cut);
        Assert.Equal(Jan1_2024, picker.Min);
        Assert.Equal(Dec31_2024, picker.Max);
    }

    [Fact]
    public void Neither_attribute_nor_parameter_set_yields_null_Min_and_Max()
    {
        var model = new MinMaxDateModel();
        Expression<Func<DateTime?>> field = () => model.WithNoAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var picker = Picker(cut);
        Assert.Null(picker.Min);
        Assert.Null(picker.Max);
    }

    // ----- EditDateNative<T> -----------------------------------------------------------------

    class MinMaxDateNativeModel
    {
        [MinValue("2024-01-01")]
        [MaxValue("2024-12-31")]
        public DateTime? WithMinMaxAttrs { get; set; }

        public DateTime? WithNoAttrs { get; set; }

        [Range(typeof(DateTime), "2024-01-01", "2024-12-31")]
        public DateTime? WithRangeOnly { get; set; }

        [MinValue("2024-01-01")]
        public DateTime? MonthValue { get; set; }

        [MinValue("2024-01-01")]
        public DateTime? DateTimeLocalValue { get; set; }

        [MinValue("2024-01-01")]
        [MaxValue("2024-12-31")]
        public DateTime? TimeValue { get; set; }
    }

    [Fact]
    public void MinValue_and_MaxValue_attributes_render_min_and_max_for_Type_Date_when_no_parameter_is_set()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.WithMinMaxAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.Equal("2024-01-01", input.GetAttribute("min"));
        Assert.Equal("2024-12-31", input.GetAttribute("max"));
    }

    [Fact]
    public void Explicit_Min_parameter_wins_over_the_model_attribute()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.WithMinMaxAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithMinMaxAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Min", new DateTime(2023, 6, 1));
            b.CloseComponent();
        }));

        Assert.Equal("2023-06-01", cut.Find("input.edit-date-input").GetAttribute("min"));
    }

    [Fact]
    public void Range_attribute_is_the_fallback_on_EditDateNative_when_no_MinValue_or_MaxValue_is_present()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.WithRangeOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithRangeOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.Equal("2024-01-01", input.GetAttribute("min"));
        Assert.Equal("2024-12-31", input.GetAttribute("max"));
    }

    [Fact]
    public void MinValue_attribute_renders_in_yyyy_MM_format_for_Type_Month()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.MonthValue;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.MonthValue);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Month);
            b.CloseComponent();
        }));

        Assert.Equal("2024-01", cut.Find("input.edit-date-input").GetAttribute("min"));
    }

    [Fact]
    public void MinValue_attribute_renders_in_yyyy_MM_ddTHH_mm_ss_format_for_Type_DateTimeLocal()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.DateTimeLocalValue;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.DateTimeLocalValue);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.DateTimeLocal);
            b.CloseComponent();
        }));

        Assert.Equal("2024-01-01T00:00:00", cut.Find("input.edit-date-input").GetAttribute("min"));
    }

    [Fact]
    public void Time_type_renders_no_min_or_max_attribute_even_when_model_attributes_are_present()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.TimeValue;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.TimeValue);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.False(input.HasAttribute("min"));
        Assert.False(input.HasAttribute("max"));
    }

    [Fact]
    public void Renders_no_min_or_max_attribute_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new MinMaxDateNativeModel();
        Expression<Func<DateTime?>> field = () => model.WithNoAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.False(input.HasAttribute("min"));
        Assert.False(input.HasAttribute("max"));
    }
}
