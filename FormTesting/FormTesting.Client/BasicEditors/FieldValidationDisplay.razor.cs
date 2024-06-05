using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.BasicEditors;

// Default validation messages

// String Validations

// Required Attribute
// The Id field is required.
// 
// StringLength Attribute
// The field Id must be a string with a maximum length of 16.
// The field Id must be a string with a minimum length of 3 and a maximum length of 16.
// 
// MinLength Attribute
// The field Id must be a string or array type with a minimum length of '3'.
// The field Id must be a string or array type with a minimum length of '3'.
// 
//
// MaxLength Attribute
// The field Id must be a string or array type with a maximum length of '16'.


public partial class FieldValidationDisplay
{
    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    [Parameter] public required EditContext EditContext { get; set; }
    [Parameter] public required List<Attribute> Attributes { get; set; }
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
    }

    /// <summary> Overrides the default validation messages. </summary>
    string GetValidationMessage(string message, string fieldName)
    {
        var output = message;

        // Required
        if (string.Equals(message, $"The {_fieldName} field is required."))
        {
            return RequiredString();
        }

        // StringLength with only max
        if (string.Equals(message,
                $"The field {_fieldName} must be a string with a maximum length of {_maxCharacters}."))
        {
            return MaxLengthString(_maxCharacters);
        }

        // StringLength with Min
        if (string.Equals(message,
                $"The field {_fieldName} must be a string with a minimum length of {_minCharacters} and a maximum length of {_maxCharacters}."))
        {
            return RangeString(_minCharacters, _maxCharacters);
        }

        // MinLength
        if (string.Equals(message,
                $"The field {_fieldName} must be a string or array type with a minimum length of '{_minCharacters}'."))
        {
            return MinLengthString(_minCharacters);
        }

        // MaxLength
        if (string.Equals(message,
                $"The field {_fieldName} must be a string with a maximum length of {_maxCharacters}."))
        {
            return MaxLengthString(_maxCharacters);
        }

        return output;
    }

    // New default validation messages
    public static string RequiredString() => "Required";
    public static string MinLengthString(int? min) => $"Must contain at least {min} characters";
    public static string MaxLengthString(int? max) => $"Cannot contain more than {max} characters";
    public static string RangeString(int? min, int? max) => $"Must be between {min} and {max} characters";
}