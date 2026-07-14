namespace Controls;

/// <summary>
/// A labeled shortcut in a <see cref="DateRangePicker"/>'s preset sidebar (e.g. "This Week").
/// </summary>
/// <remarks>
/// The range is resolved when the preset is clicked, not when the list is built — so relative
/// presets ("This Week", "Last Month") stay correct in a long-lived page without the consumer
/// having to rebuild the list. Use the fixed-dates constructor when the range genuinely is fixed.
/// </remarks>
public sealed class DateRangePreset
{
    /// <summary>Text shown for this preset in the sidebar.</summary>
    public string Label { get; }

    /// <summary>Produces the range this preset selects, evaluated at click time.</summary>
    public Func<(DateTime Start, DateTime End)> Resolve { get; }

    public DateRangePreset(string label, Func<(DateTime Start, DateTime End)> resolve)
    {
        Label = label;
        Resolve = resolve;
    }

    /// <summary>A preset with a fixed range (captured now, not re-evaluated at click time).</summary>
    public DateRangePreset(string label, DateTime start, DateTime end)
        : this(label, () => (start, end))
    {
    }
}
