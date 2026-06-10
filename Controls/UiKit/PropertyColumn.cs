namespace Controls;

/// <summary>
/// A table column bound to a property of the row item. Set <see cref="Property"/>
/// to a selector and (optionally) <see cref="Format"/> for formatting.
/// </summary>
public class PropertyColumn<TItem, TProp> : Column<TItem>
{
    // Whether Comparer<TProp>.Default can actually order TProp values. Computed once per closed
    // generic type. Covers Nullable<T> (the default comparer orders a comparable underlying type)
    // and any type implementing IComparable / IComparable<T>; for everything else (e.g. a plain
    // class) Comparer<T>.Default.Compare throws, so we treat the column as non-sortable instead.
    static readonly bool TPropIsComparable = ComputeComparable();

    static bool ComputeComparable()
    {
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        return typeof(IComparable).IsAssignableFrom(type)
            || typeof(IComparable<>).MakeGenericType(type).IsAssignableFrom(type);
    }

    [Parameter] public Func<TItem, TProp>? Property { get; set; }

    /// <summary>Optional format string for <see cref="IFormattable"/> values.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Makes the column header sortable. The comparison is derived from <see cref="Property"/>
    /// via <see cref="Comparer{T}.Default"/>, so <typeparamref name="TProp"/> must be comparable —
    /// if it isn't, the header silently stays non-sortable rather than throwing on click. Set
    /// <c>SortBy</c> to supply a custom comparison, which works for any type.
    /// </summary>
    [Parameter] public bool Sortable { get; set; }

    public override bool CanSort =>
        SortBy is not null || (Sortable && Property is not null && TPropIsComparable);

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
