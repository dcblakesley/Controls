namespace Controls;

/// <summary>
/// One ACTIVE column filter as reported by <see cref="Table{TItem}.OnFiltersChanged"/>: the column,
/// its kind, its applied values in the serialized form <see cref="Table{TItem}.OnFilterChanged"/>
/// uses for that kind, and a short human-readable description of the applied state. A consumer's
/// own "active filters" summary (chips, a clear-all bar, a URL/query-string mirror) reads these
/// instead of tracking every per-column <see cref="Table{TItem}.OnFilterChanged"/> raise itself.
/// </summary>
/// <param name="Column">The filtered column.</param>
/// <param name="Kind">Which editor the column declares -- decides how <paramref name="Values"/> is shaped.</param>
/// <param name="Values">The applied values, serialized per <paramref name="Kind"/> (Options/Custom:
/// the selected keys in option order; Text: the single trimmed text). Never empty -- only active
/// columns are reported.</param>
/// <param name="Description">A short summary of the applied state ("3 selected",
/// <c>contains "abc"</c>), or an empty string when the kind has none.</param>
public sealed record TableColumnFilterSnapshot<TItem>(
    Column<TItem> Column,
    TableFilterKind Kind,
    IReadOnlyList<string> Values,
    string Description);
