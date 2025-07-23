namespace Controls;

public partial class EditNullableBool : IEditControl
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