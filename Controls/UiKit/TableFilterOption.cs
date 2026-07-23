namespace Controls;

/// <summary>
/// One selectable value in a <see cref="Column{TItem}.FilterOptions"/> list (AntD 4.x's
/// <c>filters</c> entries): <see cref="Text"/> is the label shown in the column's filter dropdown,
/// <see cref="Value"/> is what gets passed to <see cref="Column{TItem}.OnFilter"/>.
/// </summary>
public sealed class TableFilterOption
{
    /// <summary>Label shown in the filter dropdown.</summary>
    public string Text { get; }

    /// <summary>Value passed to <see cref="Column{TItem}.OnFilter"/> when this option is selected.</summary>
    public string Value { get; }

    public TableFilterOption(string text, string value)
    {
        Text = text;
        Value = value;
    }
}
