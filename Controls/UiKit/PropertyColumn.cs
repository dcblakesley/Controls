namespace Controls;

/// <summary>
/// A table column bound to a property of the row item. Set <see cref="Property"/>
/// to a selector and (optionally) <see cref="Format"/> for formatting.
/// </summary>
public class PropertyColumn<TItem, TProp> : Column<TItem>
{
    [Parameter] public Func<TItem, TProp>? Property { get; set; }

    /// <summary>Optional format string for <see cref="IFormattable"/> values.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Makes the column header sortable. The comparison is derived from <see cref="Property"/>
    /// via <see cref="Comparer{T}.Default"/> (so <typeparamref name="TProp"/> must be comparable);
    /// set <c>SortBy</c> to supply a custom comparison instead.
    /// </summary>
    [Parameter] public bool Sortable { get; set; }

    public override bool CanSort => (Sortable && Property is not null) || SortBy is not null;

    public override int Compare(TItem a, TItem b) =>
        SortBy is not null
            ? SortBy(a, b)
            : Comparer<TProp>.Default.Compare(Property!(a), Property!(b));

    public override RenderFragment CellFor(TItem item) => builder =>
    {
        object? value = Property != null ? Property(item) : null;
        var text = value is IFormattable formattable && !string.IsNullOrEmpty(Format)
            ? formattable.ToString(Format, null)
            : value?.ToString();
        builder.AddContent(0, text);
    };
}
