namespace Controls;

/// <summary>
/// Used within other edit controls to display the value when EditMode is false.
/// </summary>
public partial class ReadOnlyValue
{
    [Parameter] public required string Id { get; set; }
    [Parameter] public required string IsRequired { get; set; }
    [Parameter] public string? CssClass { get; set; }
    [Parameter] public required string Text { get; set; }
}