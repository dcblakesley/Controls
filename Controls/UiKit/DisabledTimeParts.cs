namespace Controls;

/// <summary>
/// The hour/minute/second values a <see cref="DatePicker"/>'s <see cref="DatePicker.DisabledTime"/>
/// callback disables in the <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
/// time row, at each unit's own granularity (an hour listed here disables every minute/second under
/// it — the row's three selects are independent, so disabling e.g. hour 13 doesn't imply anything
/// about minutes/seconds when a different, enabled hour is picked).
/// </summary>
/// <param name="Hours">Disabled hour values (0-23). Null (default) disables none.</param>
/// <param name="Minutes">Disabled minute values (0-59). Null (default) disables none.</param>
/// <param name="Seconds">Disabled second values (0-59). Null (default) disables none.</param>
/// <remarks>
/// A null collection means nothing is disabled in that unit — not "everything" — so a caller only
/// needs to populate the unit(s) it actually restricts. Values outside each unit's own range are
/// simply never matched by any option (no validation, no throw).
/// </remarks>
public sealed record DisabledTimeParts(
    IReadOnlyCollection<int>? Hours = null,
    IReadOnlyCollection<int>? Minutes = null,
    IReadOnlyCollection<int>? Seconds = null);
