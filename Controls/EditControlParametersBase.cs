namespace Controls;

/// <summary>
/// Shared parameter set for the edit controls that aren't <see cref="EditControlBase{TValue}"/> (i.e.
/// not an <c>InputBase&lt;TValue&gt;</c>): <see cref="EditControlListBase{TItem}"/> (list-bound
/// controls) and <see cref="EditDateRange"/> (two independent scalar fields). Both are plain
/// <see cref="ComponentBase"/>s that hand-roll <see cref="FormOptions"/> registration and
/// <see cref="EditContext"/> validation-state subscription instead of inheriting <c>InputBase</c>, and
/// both need the exact same <see cref="IEditControl"/> parameter set plus the three cascading options
/// — hoisted here so neither redeclares it independently.
/// </summary>
public abstract class EditControlParametersBase : ComponentBase, IEditControl
{
    [CascadingParameter] protected EditContext? EditContext { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool? IsRequired { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }
}
