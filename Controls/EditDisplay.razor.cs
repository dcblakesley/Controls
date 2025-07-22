namespace Controls;

/// <summary>
/// Read-only display component for displaying text with styling and format being consistent with all other "Edit" controls
/// Useful in situations such as displaying combined values such "15.3 Ounces per can" double "volume" + enum "measurement type"
/// </summary>
public partial class EditDisplay 
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public string Id { get; set; } = "NoId";
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; }
    [Parameter] public string? Class { get; set; }
    [Parameter] public string Text { get; set; } = "";

    [Parameter] public bool IsHidden { get; set; }
}