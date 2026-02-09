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
    string GetLabel() => Label ?? Attributes.GetLabelText(FieldIdentifier);
    string? _valueType;

    protected override void OnInitialized()
    {
        _isRequired = Attributes.Any(x => x is RequiredAttribute);
        var minAndMax = AttributesHelper.GetMinAndMaxLengths(Attributes);
        _minCharacters = minAndMax.MinLength;
        _maxCharacters = minAndMax.MaxLength;
        _fieldName = FieldIdentifier.FieldName;
        _valueType = FieldIdentifier.Model.GetType().GetProperty(FieldIdentifier.FieldName)?.PropertyType?.ToString() ?? string.Empty;

        if (FormOptions != null)
        {
            // Register the field identifier with the form options so we can have a validation summary that provides links to the field that is invalid.
            FormOptions.FieldIdentifiers.Add(FieldIdentifier);
            //Console.WriteLine($"Registered {_fieldName}");
        }
    }

    bool ShowFieldNameInValidation =>
        FormOptions?.ShowFieldNameInValidation ?? FormOptions.DefaultShowFieldNameInValidation;

    /// <summary> Overrides the default validation messages. </summary>
    string GetValidationMessage(string message, bool showLabel) =>
        ValidationHelper.GetValidationMessage(message, _fieldName, GetLabel(), _valueType, _maxCharacters, _minCharacters, showLabel);

    /// <summary> Overrides the default validation messages, using the form option to determine label visibility. </summary>
    string GetValidationMessage(string message) =>
        GetValidationMessage(message, ShowFieldNameInValidation);
}