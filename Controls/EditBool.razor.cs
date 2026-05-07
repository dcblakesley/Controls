namespace Controls;

/// <summary> Edit control for boolean values, displays as a checkbox.</summary>
public partial class EditBool : IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
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
    [Parameter] public bool IsRequired { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // EditBool specific properties
    /// <summary> Expression that binds to the boolean property in the model.</summary>
    [Parameter] public required Expression<Func<bool>> Field { get; set; }
    
    /// <summary> When true, allows the checkbox to receive focus even when disabled. Defaults to true.</summary>
    [Parameter] public bool AllowFocusWhenDisabled { get; set; } = true;

    /// <summary> Text shown by the read-only view when the value is true. Defaults to "Yes". </summary>
    [Parameter] public string TrueText { get; set; } = "Yes";

    /// <summary> Text shown by the read-only view when the value is false. Defaults to "No". </summary>
    [Parameter] public string FalseText { get; set; } = "No";

    /// <summary>
    /// When true, falls back to the legacy behavior of rendering a disabled checkbox in read-only mode.
    /// Defaults to false — read-only mode now uses <see cref="ReadOnlyValue"/> with <see cref="TrueText"/>/<see cref="FalseText"/> like the other controls.
    /// </summary>
    [Parameter] public bool RenderAsCheckboxWhenReadOnly { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    // Methods
    protected override void OnInitialized()
    {
        (_id, _isRequired, _attributes, _fieldIdentifier) = EditControlInit.Init(Field, Id, FormGroupOptions, IdPrefix);
    }

    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);
    bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);
    bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;
        
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => true,
            HidingMode.WhenNullOrDefault => CurrentValue, 
            HidingMode.WhenReadOnlyAndNull => true,
            HidingMode.WhenReadOnlyAndNullOrDefault => !IsEditMode && CurrentValue,
            _ => true
        };
    }

    void HandleCheckboxChange(ChangeEventArgs args)
    {
        // Only update the value if the checkbox is not disabled
        if (ShowEditor && !IsDisabled)
        {
            CurrentValue = (bool)args.Value;
        }
    }

    string? DisplayDescription() => Description ?? _attributes.Description();
}