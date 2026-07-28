using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the model-declared <see cref="MinValueAttribute"/>/<see cref="MaxValueAttribute"/>
/// (falling back to <see cref="RangeAttribute"/>) reaching <see cref="EditDateRange"/>'s shared
/// <see cref="DateRangePicker"/> calendar via <c>EffectiveMin</c>/<c>EffectiveMax</c>. Unlike
/// <c>ModelPlaceholderDateTests</c>' Start/EndPlaceholder (which resolve independently per input),
/// Min/Max bound the ONE calendar both fields share, so each effective value prefers its "natural"
/// field's attributes (Start for Min, End for Max) and falls back to the OTHER field's -- the union of
/// what either field's own validation would accept. Assertions read the resolved bounds straight off
/// the rendered <see cref="DateRangePicker"/> instance rather than the DOM, since Min/Max only affect
/// which days render disabled/selectable, not any inspectable attribute.
/// </summary>
public class ModelMinMaxDateRangeTests : BunitContext
{
    public ModelMinMaxDateRangeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static readonly DateTime Jan1_2024 = new(2024, 1, 1);
    static readonly DateTime Jun30_2024 = new(2024, 6, 30);
    static readonly DateTime Dec31_2024 = new(2024, 12, 31);

    // Start carries the "natural" Min annotation, End the "natural" Max -- the pairing the doc
    // comments on EditDateRange.Min/Max describe as the expected shape.
    class MinOnStartMaxOnEndModel
    {
        [MinValue("2024-01-01")]
        public DateTime? Start { get; set; }

        [MaxValue("2024-12-31")]
        public DateTime? End { get; set; }
    }

    // A single Range on Start alone -- proves both bounds can come from one property, with Max
    // falling back to Start's own attributes when End supplies nothing.
    class RangeOnStartOnlyModel
    {
        [Range(typeof(DateTime), "2024-01-01", "2024-12-31")]
        public DateTime? Start { get; set; }

        public DateTime? End { get; set; }
    }

    // Start's Range supplies a (looser) maximum too, but End's own MaxValue is the "natural" source
    // for Max and must win over Start's Range maximum.
    class RangeOnStartWithMaxValueOnEndModel
    {
        [Range(typeof(DateTime), "2024-01-01", "2024-06-30")]
        public DateTime? Start { get; set; }

        [MaxValue("2024-12-31")]
        public DateTime? End { get; set; }
    }

    class NoAttributesModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    static DateRangePicker Picker(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponent<DateRangePicker>().Instance;

    [Fact]
    public void MinValue_on_Start_and_MaxValue_on_End_both_reach_the_shared_picker()
    {
        var model = new MinOnStartMaxOnEndModel();
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

        var picker = Picker(cut);
        Assert.Equal(Jan1_2024, picker.Min);
        Assert.Equal(Dec31_2024, picker.Max);
    }

    [Fact]
    public void Range_attribute_on_Start_alone_supplies_both_bounds()
    {
        // Max has no natural source on End here, so EffectiveMax falls back to Start's own Range
        // maximum -- proves the "other field" fallback, not just the "natural field" case above.
        var model = new RangeOnStartOnlyModel();
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

        var picker = Picker(cut);
        Assert.Equal(Jan1_2024, picker.Min);
        Assert.Equal(Dec31_2024, picker.Max);
    }

    [Fact]
    public void Explicit_Min_and_Max_parameters_win_over_model_attributes()
    {
        var model = new MinOnStartMaxOnEndModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var explicitMin = new DateTime(2023, 6, 1);
        var explicitMax = new DateTime(2023, 6, 30);
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Min", explicitMin);
            b.AddAttribute(6, "Max", explicitMax);
            b.CloseComponent();
        }));

        var picker = Picker(cut);
        Assert.Equal(explicitMin, picker.Min);
        Assert.Equal(explicitMax, picker.Max);
    }

    [Fact]
    public void End_MaxValue_wins_over_Start_Range_maximum()
    {
        // Start's own Range supplies a looser maximum (2024-06-30), but End is Max's "natural" field
        // and carries its own MaxValue (2024-12-31) -- the preference order must prefer End's over
        // falling through to Start's Range maximum.
        var model = new RangeOnStartWithMaxValueOnEndModel();
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

        var picker = Picker(cut);
        Assert.Equal(Jan1_2024, picker.Min); // Start's own Range minimum -- Min's natural field
        Assert.Equal(Dec31_2024, picker.Max); // End's MaxValue, not Start's Range maximum (06-30)
    }

    [Fact]
    public void Neither_attribute_nor_parameter_set_yields_null_bounds_for_both()
    {
        var model = new NoAttributesModel();
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

        var picker = Picker(cut);
        Assert.Null(picker.Min);
        Assert.Null(picker.Max);
    }
}
