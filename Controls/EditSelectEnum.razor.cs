namespace Controls;

/// <summary> Uses an enum as the options. Defaults to sorted by Id, can be sorted by name using the SortByName parameter  </summary>
public partial class EditSelectEnum<TEnum> : IEditControl
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
    [Parameter] public bool IsRequired { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    [Parameter] public bool Sort { get; set; }

    //  Fields
    string _isRequired = "false";
    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
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

        return enumValues.Cast<TEnum?>().ToList();
    }
    async Task SetAsync(TEnum value) => await ValueChanged.InvokeAsync(value);
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

        // Check if value is null (for nullable enums)
        var isNull = value == null;

        // For non-nullable enums, the default is 0, for nullable enums it's null
        var isDefault = isNull || EqualityComparer<TEnum>.Default.Equals(value, default);

        // Determine if we're in read-only mode
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
}