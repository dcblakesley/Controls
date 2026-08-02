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
/// Min/Max bound the ONE calendar both fields share, so each effective value is a TRUE union of what
/// either field's own validation would accept: whichever ONE field declares when only one does (the
/// "natural" pairing -- Start for Min, End for Max -- still reaches the picker with nothing to
/// compare against), else the earlier Min / later Max of the two when BOTH declare one (see
/// <c>UnionMin</c>/<c>UnionMax</c>) -- never a blind preference for one field that could pick the
/// TIGHTER of two conflicting bounds (see the <c>Conflicting*</c> models/tests below). Assertions
/// read the resolved bounds straight off the rendered <see cref="DateRangePicker"/> instance rather
/// than the DOM, since Min/Max only affect which days render disabled/selectable, not any inspectable
/// attribute.
/// </summary>
public class ModelMinMaxDateRangeTests : BunitContext
{
    public ModelMinMaxDateRangeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

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

    // Both fields declare a bound of the SAME kind, with conflicting tightness -- the "true union"
    // case finding 16 exists for. A fallback that blindly prefers one field over the other (the old
    // first-non-null behavior, which always checked Start first for Min and End first for Max) would
    // pick the TIGHTER bound whenever the "natural" field happened to be the tighter one, blocking a
    // value the OTHER (looser) field's own validation would still accept. Split by direction so each
    // model's attribute list stays unambiguous (a property carries only one MinValue/MaxValue pair).
    class ConflictingMinModel
    {
        [MinValue("2025-01-10")] // tighter
        public DateTime? Start { get; set; }

        [MinValue("2025-01-05")] // looser -- must win
        public DateTime? End { get; set; }
    }

    class ConflictingMaxModel
    {
        [MaxValue("2025-03-01")] // looser -- must win
        public DateTime? Start { get; set; }

        [MaxValue("2025-02-20")] // tighter, but "natural" for Max
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
    public void Conflicting_MinValue_on_both_fields_takes_the_earlier_looser_bound()
    {
        // Start's own Min (Jan 10) is tighter than End's (Jan 5). The old first-non-null resolution
        // always checked Start first for Min and would have returned Jan 10, blocking Jan 5-9 even
        // though End's own [MinValue] validation accepts them. The true union takes the earlier
        // (looser) of the two.
        var model = new ConflictingMinModel();
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

        Assert.Equal(new DateTime(2025, 1, 5), Picker(cut).Min);
    }

    [Fact]
    public void Conflicting_MaxValue_on_both_fields_takes_the_later_looser_bound()
    {
        // End's own Max (Feb 20) is the "natural" field and tighter than Start's (Mar 1). The old
        // first-non-null resolution always checked End first for Max and would have returned Feb 20,
        // blocking Feb 21 - Mar 1 even though Start's own [MaxValue] validation accepts them. The
        // true union takes the later (looser) of the two.
        var model = new ConflictingMaxModel();
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

        Assert.Equal(new DateTime(2025, 3, 1), Picker(cut).Max);
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
