namespace Controls;

/// <summary> Validation shown under the input field when it doesn't meet the requirements based on DataAnnotations. </summary>
public partial class FieldValidationDisplay
{
    [CascadingParameter] EditContext? EditContext { get; set; }
    [CascadingParameter] FormOptions? FormOptions { get; set; }

    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }

    bool _isRequired;
    int? _minCharacters;
    int? _maxCharacters;
    string _fieldName = string.Empty;
    string _label = string.Empty;
    string? _valueType;

    protected override void OnInitialized()
    {
        _isRequired = Attributes.Any(x => x is RequiredAttribute);
        var minAndMax = AttributesHelper.GetMinAndMaxLengths(Attributes);
        _minCharacters = minAndMax.MinLength;
        _maxCharacters = minAndMax.MaxLength;
        _fieldName = FieldIdentifier.FieldName;
        // Cache the resolved label once at init — every validation-message render used to call
        // GetLabelText() afresh against the attribute list.
        _label = Label ?? Attributes.GetLabelText(FieldIdentifier);
        _valueType = FieldIdentifier.Model.GetType().GetProperty(FieldIdentifier.FieldName)?.PropertyType?.ToString() ?? string.Empty;
        // Field registration with FormOptions.FieldIdentifiers moved to EditControlBase.InitState
        // (and the list/radio equivalents) so it runs once per control regardless of whether
        // this validation display is conditionally rendered.
    }

    // DefaultShowFieldNameInValidation is a *static* member: in `FormOptions.X` the name binds to the
    // type, not the (possibly-null) cascaded instance, so the ?? fallback is null-safe despite appearances.
    bool ShowFieldNameInValidation =>
        FormOptions?.ShowFieldNameInValidation ?? FormOptions.DefaultShowFieldNameInValidation;

    /// <summary> Overrides the default validation messages. </summary>
    string GetValidationMessage(string message, bool showLabel) =>
        ValidationHelper.GetValidationMessage(message, _fieldName, _label, _valueType, _maxCharacters, _minCharacters, showLabel);

    /// <summary> Overrides the default validation messages, using the form option to determine label visibility. </summary>
    string GetValidationMessage(string message) =>
        GetValidationMessage(message, ShowFieldNameInValidation);
}