namespace Controls;

/// <summary> Edit control for selecting a value using radio buttons. Create options within the markup using InputRadio components.</summary>
public partial class EditRadio<TValue> : InputRadioGroup<TValue>, IEditControl
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
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }
    
    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    FieldIdentifier _fieldIdentifier;
    List<Attribute>? _attributes;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

    protected bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var effectiveHiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        
        if (effectiveHiding == HidingMode.None)
            return true;

        var value = Value;
        var isNull = value == null;
        var isDefault = isNull || EqualityComparer<TValue>.Default.Equals(value, default);

        return effectiveHiding switch
        {
            HidingMode.WhenReadOnlyAndNull => IsEditMode || !isNull,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || !isDefault,
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}
