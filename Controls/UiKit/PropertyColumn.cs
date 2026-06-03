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

    public override RenderFragment CellFor(TItem item) => builder =>
    {
        object? value = Property != null ? Property(item) : null;
        var text = value is IFormattable formattable && !string.IsNullOrEmpty(Format)
            ? formattable.ToString(Format, null)
            : value?.ToString();
        builder.AddContent(0, text);
    };
}
