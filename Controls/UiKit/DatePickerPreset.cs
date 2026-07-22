namespace Controls;

/// <summary>
/// A labeled shortcut in a <see cref="DatePicker"/>'s preset sidebar (e.g. "Today", "Next Monday").
/// </summary>
/// <param name="Label">Text shown for this preset in the sidebar.</param>
/// <param name="Resolve">Produces the date this preset selects, evaluated at click time — so a
/// relative preset ("Next Monday", "In 30 Days") stays correct in a long-lived page without the
/// consumer having to rebuild the list.</param>
/// <remarks>
/// Mirrors <see cref="DateRangePreset"/>'s shape and naming for the single-date picker. The
/// resolved value is normalized to <see cref="DatePicker.Mode"/>'s own granularity and guarded by
/// the same Min/Max/DisabledDate/DisabledTime checks as any other commit — a rejected result
/// no-ops instead of committing an invalid date. Unlike the range picker's presets, clicking one
/// here is a COMPLETE pick in every <see cref="DatePickerMode"/> (including <c>Time</c>/
/// <c>DateTime</c>) — it always closes the panel, where those modes' own incremental selects
/// normally leave it open for OK to close.
/// </remarks>
public sealed record DatePickerPreset(string Label, Func<DateTime> Resolve);
