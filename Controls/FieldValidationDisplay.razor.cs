using System.ComponentModel.DataAnnotations;
using System.Xml;
using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

/// <summary> Validation shown under the input field when it doesn't meet the requirements based on DataAnnotations. </summary>
public partial class FieldValidationDisplay
{
    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [CascadingParameter] EditContext? EditContext { get; set; }
    [CascadingParameter] FormOptions? FormOptions { get; set; }

    bool _isRequired;
    int? _minCharacters;
    int? _maxCharacters;
    string _fieldName = string.Empty;

    protected override void OnInitialized()
    {
        _isRequired = Attributes.Any(x => x is RequiredAttribute);
        var minAndMax = AttributesHelper.GetMinAndMaxLengths(Attributes);
        _minCharacters = minAndMax.MinLength;
        _maxCharacters = minAndMax.MaxLength;
        _fieldName = FieldIdentifier.FieldName;
        if (FormOptions != null)
        {
            // Register the field identifier with the form options so we can have a validation summary that provides links to the field that is invalid.
            FormOptions.FieldIdentifiers.Add(FieldIdentifier);
            //Console.WriteLine($"Registered {_fieldName}");
        }
    }

    /// <summary> Overrides the default validation messages. </summary>
    string GetValidationMessage(string message) =>
        ValidationHelper.GetValidationMessage(message, _fieldName, _maxCharacters, _minCharacters);
}