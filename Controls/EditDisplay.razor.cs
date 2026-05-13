namespace Controls;

/// <summary> 
/// Read-only display component for displaying text with styling and format being consistent with all other "Edit" controls
/// Useful in situations such as displaying combined values such "15.3 Ounces per can" double "volume" + enum "measurement type"
/// </summary>
public partial class EditDisplay 
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    /// <inheritdoc cref="IEditControl.Id"/>
    [Parameter] public string? Id { get; set; }

    /// <inheritdoc cref="IEditControl.Label"/>
    [Parameter] public string Label { get; set; } = "";

    /// <inheritdoc cref="IEditControl.Description"/>
    [Parameter] public string? Description { get; set; }

    /// <inheritdoc cref="IEditControl.Tooltip"/>
    [Parameter] public string? Tooltip { get; set; }

    /// <inheritdoc cref="IEditControl.ContainerClass"/>
    [Parameter] public string? ContainerClass { get; set; }

    /// <inheritdoc cref="IEditControl.IsRequired"/>
    [Parameter] public bool IsRequired { get; set; }

    [Parameter] public string? Class { get; set; }
    [Parameter] public string Text { get; set; } = "";

    /// <inheritdoc cref="IEditControl.IsHidden"/>
    [Parameter] public bool IsHidden { get; set; }

    // Resolved id used by the markup: explicit Id wins, otherwise derive from Label.
    // Replaces the legacy "NoId" sentinel that ended up baked into rendered output.
    string _id = string.Empty;

    protected override void OnParametersSet() =>
        _id = !string.IsNullOrEmpty(Id) ? Id : Label.ToId();
}