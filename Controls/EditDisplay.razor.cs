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
    [Parameter] public bool? IsRequired { get; set; }

    [Parameter] public string? Class { get; set; }
    [Parameter] public string Text { get; set; } = "";

    /// <inheritdoc cref="IEditControl.IsHidden"/>
    [Parameter] public bool IsHidden { get; set; }

    /// <inheritdoc cref="IEditControl.IsLabelHidden"/>
    [Parameter] public bool IsLabelHidden { get; set; }

    /// <inheritdoc cref="IEditControl.IdPrefix"/>
    [Parameter] public string? IdPrefix { get; set; }

    // Same resolution as every other control (EditControlInit.ShouldHideLabel): the per-control
    // parameter or the cascaded form-wide setting — previously the cascaded FormOptions was
    // declared but ignored here.
    internal bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    // Resolved id used by the markup: explicit Id wins, then a Label-derived id, else a unique
    // fallback so label-less displays don't collide on an empty id (and the markup can omit
    // aria-labelledby rather than point it at an empty label). The derived forms compose with
    // FormGroupOptions.Name / IdPrefix the same way AttributesHelper.GetId does for bound controls,
    // so two "Status" displays in different groups don't collide.
    string _id = string.Empty;
    readonly string _fallbackId = $"ed-{Guid.NewGuid():N}";

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(Id))
        {
            _id = Id;
            return;
        }

        var baseId = !string.IsNullOrEmpty(Label) ? Label.ToId() : _fallbackId;
        if (!string.IsNullOrEmpty(FormGroupOptions?.Name))
            baseId = $"{FormGroupOptions.Name}-{baseId}";
        if (IdPrefix != null)
            baseId = $"{IdPrefix}-{baseId}";
        _id = baseId.Replace(" ", "");
    }
}