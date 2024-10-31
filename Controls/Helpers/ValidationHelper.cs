namespace Controls.Helpers;

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
// MaxLength Attribute
// The field Id must be a string or array type with a maximum length of '16'.

public static class ValidationHelper
{
    // New default validation messages
    static string RequiredString() => "Required";
    static string MinLengthString(int? min) => $"Must contain at least {min} characters";
    static string MaxLengthString(int? max) => $"Cannot contain more than {max} characters";
    static string RangeString(int? min, int? max) => $"Must be between {min} and {max} characters";

    /// <summary> Overrides the default validation messages. </summary>
    public static string GetValidationMessage(string message, string fieldName, int? maxCharacters = null, int? minCharacters = null)
    {
        var output = message;

        // Required
        if (string.Equals(message, $"The {fieldName} field is required."))
            return RequiredString();

        // StringLength with only max
        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {maxCharacters}."))
            return MaxLengthString(maxCharacters);

        // StringLength with Min
        if (string.Equals(message, $"The field {fieldName} must be a string with a minimum length of {minCharacters} and a maximum length of {maxCharacters}."))
            return RangeString(minCharacters, maxCharacters);

        // MinLength
        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a minimum length of '{minCharacters}'."))
            return MinLengthString(minCharacters);

        // MaxLength
        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {maxCharacters}."))
            return MaxLengthString(maxCharacters);

        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a maximum length of '{maxCharacters}'."))
        {
            return MaxLengthString(maxCharacters);
        }

        return output;
    }
}
