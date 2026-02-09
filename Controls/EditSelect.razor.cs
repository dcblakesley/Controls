namespace Controls;

/// <summary> 
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
public partial class EditSelect<TValue> : IEditControl
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

    // Component specific parameters
    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

    // Methods
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        // Get effective hiding mode (component's setting overrides form's setting)
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        if (hidingMode == HidingMode.None)
            return true;

        var value = Value;
        var isNull = value == null;
        var isDefault = isNull || EqualityComparer<TValue>.Default.Equals(value, default);
        var isReadOnly = !IsEditMode || (FormOptions != null && !FormOptions.IsEditMode);

        return hidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !(isReadOnly && isNull),
            HidingMode.WhenReadOnlyAndNullOrDefault => !(isReadOnly && isDefault),
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}