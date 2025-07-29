namespace Controls;

public partial class EditRadioEnum<TEnum> : IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }
    [Parameter] public bool Sort { get; set; }

    /// <summary> The labels around each radio button </summary>
    [Parameter] public string? LabelClass { get; set; }

    // Other Option
    [Parameter] public bool HasOtherOption { get; set; } = false;
    [Parameter] public string OtherPlaceholder { get; set; }
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

    bool ShouldShowComponent()
    {
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

        if (_isNullable)
        {
            // Add null option for nullable enums if not required
            if (!_attributes.Any(x => x is RequiredAttribute))
            {
                var result = new List<TEnum?>();
                result.Add(default(TEnum?));
                result.AddRange(enumValues.Cast<TEnum?>());
                return result;
            }
        }

        return enumValues.Cast<TEnum?>().ToList();
    }

    protected override bool TryParseValueFromString(string? value, out TEnum result, out string? validationErrorMessage)
    {
        // Handle null/empty for nullable enums
        if (string.IsNullOrEmpty(value))
        {
            if (_isNullable)
            {
                result = default;
                validationErrorMessage = null;
                return true;
            }
            result = default;
            validationErrorMessage = $"The {FieldIdentifier.FieldName} field is required.";
            return false;
        }

        // Try parsing the enum value
        if (Enum.TryParse(_underlyingType, value, out object? parsedValue))
        {
            result = (TEnum)parsedValue;
            validationErrorMessage = null;
            return true;
        }

        result = default;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }

    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    private async Task OnOtherValueChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (OtherValue != value)
        {
            await OtherValueChanged.InvokeAsync(value);
        }
    }
}