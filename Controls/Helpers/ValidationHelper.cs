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
    static string MustBeANumberString() => "Must be a number";
    static string MinValueString(string min) => $"Must be at least {min}";
    static string MaxValueString(string max) => $"Cannot exceed {max}";

    static string NumberRangeString(string min, string max) => $"Must be between {min} and {max}";

    /// <summary> Overrides the default validation messages. </summary>
    public static string GetValidationMessage(string message, string fieldName, int? max = null, int? min = null)
    {
        var output = message;

        // Required
        if (string.Equals(message, $"The {fieldName} field is required."))
            return RequiredString();

        // StringLength with only max
        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {max}."))
            return MaxLengthString(max);

        // StringLength with Min
        if (string.Equals(message, $"The field {fieldName} must be a string with a minimum length of {min} and a maximum length of {max}."))
            return RangeString(min, max);

        // MinLength
        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a minimum length of '{min}'."))
            return MinLengthString(min);

        // MaxLength
        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {max}."))
            return MaxLengthString(max);
        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a maximum length of '{max}'."))
            return MaxLengthString(max);

        // Replace numeric validation message 
        if (string.Equals(message, $"The {fieldName} field must be a number."))
            return MustBeANumberString();

        // Numeric
        if (min != null && max != null && string.Equals(message, $"The field {fieldName} must be a number between {min} and {max}."))
            return RangeString(min, max);

        // Numeric range
        // The field Min must be between -2 and 55.
        if (message.Contains($"The field {fieldName} must be between"))
        {
            // The message is in the format "The field Min must be between -2 and 55."
            var parts = message.Split(' ');

            // The field Min must be between -2 and 55.
            var minValue = parts[6];
            var maxValue = parts[8].TrimEnd('.');
            var isMinimumForType = false;
            var isMaximumForType = false;

            // Determine if the min is the min for it's datatype, which can be any numeric type.
            if (minValue == int.MinValue.ToString() || minValue == double.MinValue.ToString() || minValue == "-3.4028234663852886E+38" ||
               minValue == long.MinValue.ToString() || minValue == short.MinValue.ToString() || minValue == byte.MinValue.ToString() ||
               minValue == sbyte.MinValue.ToString() || minValue == uint.MinValue.ToString() || minValue == ulong.MinValue.ToString() ||
               minValue == ushort.MinValue.ToString() || minValue == decimal.MinValue.ToString())
            {
                isMinimumForType = true;
            }

            // Determine if the max is the max for it's datatype, which can be any numeric type.
            if (maxValue == int.MaxValue.ToString() || maxValue == double.MaxValue.ToString() || maxValue == float.MaxValue.ToString() ||
               maxValue == long.MaxValue.ToString() || maxValue == short.MaxValue.ToString() || maxValue == byte.MaxValue.ToString() ||
               maxValue == sbyte.MaxValue.ToString() || maxValue == uint.MaxValue.ToString() || maxValue == ulong.MaxValue.ToString() ||
               maxValue == ushort.MaxValue.ToString() || maxValue == decimal.MaxValue.ToString())
            {
                isMaximumForType = true;
            }

            if (isMinimumForType && !isMaximumForType)
                return MaxValueString(maxValue);
            if (!isMinimumForType && isMaximumForType)
                return MinValueString(minValue);
            if (!isMinimumForType && !isMaximumForType)
                return NumberRangeString(minValue, maxValue);
        }

        return output;
    }
}
