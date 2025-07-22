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
    [Parameter] public string? ContainerClass { get; set; }
    [Parameter] public string? Description { get; set; }
    
    /// <summary> The labels around each radio button </summary>
    [Parameter] public string? LabelClass { get; set; }
    
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }
    [Parameter] public bool Sort { get; set; }

    /// <summary> The enum type to provide the values for, must match the Value Parameter </summary>
    [Parameter] public required Type Type { get; set; }

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

    List<TEnum> GetOptions() => Sort
        ? Enum.GetValues(Type).Cast<TEnum>().OrderBy(x => x).ToList()
        : Enum.GetValues(Type).Cast<TEnum>().ToList();

    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }

    protected override bool TryParseValueFromString(string value, out TEnum result, out string validationErrorMessage)
    {
        // Lets Blazor convert the value for us
        if (BindConverter.TryConvertTo(value, CultureInfo.CurrentCulture, out TEnum parsedValue))
        {
            result = parsedValue;
            validationErrorMessage = null;
            return true;
        }

        // Map null/empty value to null if the bound object is nullable
        if (string.IsNullOrEmpty(value))
        {
            var nullableType = Nullable.GetUnderlyingType(typeof(TEnum));
            if (nullableType != null)
            {
                result = default;
                validationErrorMessage = null;
                return true;
            }
        }

        // The value is invalid => set the error message
        result = default;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }
}