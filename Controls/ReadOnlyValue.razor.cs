namespace Controls;

/// <summary> 
/// Used within other edit controls to display the value when EditMode is false.
/// </summary>
public partial class ReadOnlyValue
{
    [Parameter] public required string Id { get; set; }

    /// <summary>
    /// When true, suppresses <c>aria-labelledby</c> — the sibling <see cref="FormLabel"/> renders no
    /// <c>lbl-{id}</c> element to point at (label hidden), or this value has no associated label
    /// (e.g. a per-option read-only item in a checked list).
    /// </summary>
    [Parameter] public bool IsLabelHidden { get; set; }

    [Parameter] public string? CssClass { get; set; }
    [Parameter] public string? Text { get; set; }
}