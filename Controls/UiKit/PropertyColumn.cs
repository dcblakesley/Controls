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
        // Statically-known interface checks only — the previous MakeGenericType probe was flagged
        // RequiresDynamicCode (IL3050) under AOT. The one shape this no longer detects is a
        // Nullable<T> whose T implements IComparable<T> but not non-generic IComparable (every BCL
        // comparable implements both); such a column degrades to non-sortable, and SortBy still works.
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        return typeof(IComparable).IsAssignableFrom(type)
            || typeof(IComparable<TProp>).IsAssignableFrom(typeof(TProp));
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

        // Ellipsis wraps the text in a title-bearing <span> so the truncated value stays
        // discoverable on hover; left as bare text (unchanged DOM) when Ellipsis is unset.
        if (Ellipsis)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "title", text);
            builder.AddContent(2, text);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(0, text);
        }
    };
}
