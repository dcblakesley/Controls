namespace Controls;

public partial class EditBoolNullRadio : IEditControl
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

    // Control-specific properties
    [Parameter] public bool IsHorizontal { get; set; } = true;
    [Parameter] public required Expression<Func<bool?>> Field { get; set; }
    [Parameter] public bool ShowNullOption { get; set; } = true;

    // Text customization parameters
    [Parameter] public string TrueText { get; set; } = "Yes";
    [Parameter] public string FalseText { get; set; } = "No";
    [Parameter] public string NullText { get; set; } = "Not Set";

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    // Methods
    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    void OnValueChanged(bool? value)
    {
        CurrentValue = value;
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

    protected override bool TryParseValueFromString(string? value, out bool? result, out string? validationErrorMessage)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = null;
            validationErrorMessage = null;
            return true;
        }

        if (bool.TryParse(value, out bool boolValue))
        {
            result = boolValue;
            validationErrorMessage = null;
            return true;
        }

        result = null;
        validationErrorMessage = "The value must be either true, false, or empty.";
        return false;
    }
    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;
        
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => CurrentValue.HasValue,
            HidingMode.WhenNullOrDefault => CurrentValue.HasValue && CurrentValue.Value,
            HidingMode.WhenReadOnlyAndNull => IsEditMode || CurrentValue.HasValue,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || (CurrentValue.HasValue && CurrentValue.Value),
            _ => true
        };
    }
    string GetDisplayText(bool? value) => value switch
    {
        true => TrueText,
        false => FalseText,
        _ => NullText
    };
}