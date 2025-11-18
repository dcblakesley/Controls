namespace Controls;

public partial class EditRadioEnum<TEnum> : IEditControl
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
    [Parameter] public EventCallback<bool> IsRequiredChanged { get; set; }
    
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
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }
    [Parameter] public bool Sort { get; set; }

    /// <summary> The labels around each radio button </summary>
    [Parameter] public string? LabelClass { get; set; }

    // Other Option
    [Parameter] public bool HasOtherOption { get; set; } = false;
    [Parameter] public string? OtherPlaceholder { get; set; }
    [Parameter] public string? OtherValue { get; set; }
    [Parameter] public EventCallback<string?> OtherValueChanged { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    Type _type;
    Type _underlyingType;
    bool _isNullable;

    // Methods
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";

        // Handle nullable enum types
        _type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(_type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(_type)! : _type;
    }
    List<TEnum?> GetOptions()
    {
        var enumValues = Enum.GetValues(_underlyingType).Cast<TEnum>().ToList();

        // If HasOtherOption is true, remove the last enum value to add it back later
        TEnum? otherOption = default;
        if (HasOtherOption && enumValues.Count > 0)
        {
            otherOption = enumValues.Last();
            enumValues.RemoveAt(enumValues.Count - 1);
        }

        // Sort remaining values if needed
        if (Sort)
        {
            // Sort by display name that would appear in the UI
            enumValues = enumValues.OrderBy(x =>
            {
                // Get display name from DisplayAttribute if present
                var memberInfo = _underlyingType.GetMember(x.ToString());
                if (memberInfo.Length > 0)
                {
                    var displayAttr = memberInfo[0].GetCustomAttribute<DisplayAttribute>();
                    if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
                    {
                        return displayAttr.Name;
                    }
                }
                // Otherwise use enum name
                return x.ToString();
            }).ToList();
        }

        // Add back the "other" option at the end if it exists
        if (HasOtherOption && otherOption != null)
        {
            enumValues.Add(otherOption);
        }


        return enumValues.Cast<TEnum?>().ToList();
    }
    protected override bool TryParseValueFromString(string? value, out TEnum result, out string validationErrorMessage)
    {
        // Handle null/empty for nullable enums
        if (string.IsNullOrEmpty(value))
        {
            if (_isNullable)
            {
                result = default!;
                validationErrorMessage = null!;
                return true;
            }
            result = default!;
            validationErrorMessage = $"The {FieldIdentifier.FieldName} field is required.";
            return false;
        }

        // Try parsing the enum value
        if (Enum.TryParse(_underlyingType, value, out object? parsedValue))
        {
            result = (TEnum)parsedValue;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }
    async Task OnOtherValueChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (OtherValue != value)
        {
            await OtherValueChanged.InvokeAsync(value);
        }
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var value = Value;

        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => value != null && !value.Equals(default(TEnum)),
            HidingMode.WhenReadOnlyAndNull => IsEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || (value != null && !value.Equals(default(TEnum))),
            _ => true
        };
    }
}