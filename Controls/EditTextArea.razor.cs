namespace Controls;

/// <summary> Edit control for multi-line string values, displays as a textarea with configurable row count.</summary>
public partial class EditTextArea : IEditControl
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
    [Parameter] public bool IsHidden { get; set;  }
    
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // EditTextArea specific parameters
    /// <summary> Placeholder text to display in the textarea when empty.</summary>
    [Parameter] public string? Placeholder { get; set; }
    
    /// <summary> Expression that binds to the string property in the model.</summary>
    [Parameter] public required Expression<Func<string>> Field { get; set; }
    
    /// <summary> Number of visible text rows in the textarea. Defaults to 2.</summary>
    [Parameter] public int Rows { get; set; } = 2;

    // Private fields
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

        // Use the Field expression to get the current value
        var value = Field.Compile()();
        var isNull = value == null;
        var isDefault = isNull || string.IsNullOrEmpty(value);
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
