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
    static string RequiredString(string label) => $"{label} is required.";

    static string MinLengthString(int? min) => $"Must contain at least {min} characters";
    static string MinLengthString(int? min, string label) => $"{label} must contain at least {min} characters";

    static string MinLengthList(int? min) => $"Must select at least {min} options";
    static string MinLengthList(int? min, string label) => $"{label} requires at least {min} options";

    static string MaxLengthString(int? max) => $"Cannot contain more than {max} characters";
    static string MaxLengthString(int? max, string label) => $"{label} cannot contain more than {max} characters";

    static string MaxLengthList(int? max) => $"Cannot exceed {max} selections.";
    static string MaxLengthList(int? max, string label) => $"{label} cannot exceed {max} selections";

    static string RangeString(int? min, int? max) => $"Must be between {min} and {max} characters";
    static string RangeString(int? min, int? max, string label) => $"{label} must be between {min} and {max} characters";

    static string MustBeANumberString() => "Must be a number";
    static string MustBeANumberString(string label) => $"{label} must be a number.";

    static string MinValueString(string min) => $"Must be at least {min}";
    static string MinValueString(string min, string label) => $"{label} must be at least {min}";

    static string MaxValueString(string max) => $"Cannot exceed {max}";
    static string MaxValueString(string max, string label) => $"{label} cannot exceed {max}";

    static string NumberRangeString(string min, string max) => $"Must be between {min} and {max}";
    static string NumberRangeString(string min, string max, string label) => $"{label} must be between {min} and {max}";

    /// <summary> Overrides the default validation messages. </summary>
    public static string GetValidationMessage(string message, string fieldName, string label, string? valueType, int? max = null, int? min = null, bool includeLabel = false)
    {
        Console.WriteLine($"GetValidationMessage: {message}, {fieldName}, {label}, {valueType}, {max}, {min}, {includeLabel}");

        var output = message;

        // Required
        if (string.Equals(message, $"The {fieldName} field is required."))
            return includeLabel ? RequiredString(label) : RequiredString();

        // StringLength with only max
        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {max}."))
            return includeLabel ? MaxLengthString(max, label) : MaxLengthString(max);

        // StringLength with Min
        if (string.Equals(message, $"The field {fieldName} must be a string with a minimum length of {min} and a maximum length of {max}."))
            return includeLabel ? RangeString(min, max, label) : RangeString(min, max);

        // MinLength
        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a minimum length of '{min}'."))
        {
            if (valueType == "System.String")
                return includeLabel ? MinLengthString(min, label) : MinLengthString(min);
            return includeLabel ? MinLengthList(min, label) : MinLengthList(min);
        }

        if (string.Equals(message, $"The field {fieldName} must be a string or array type with a maximum length of '{max}'."))
        {
            if (valueType == "System.String")
                return includeLabel ? MaxLengthString(max, label) : MaxLengthString(max);
            return includeLabel ? MaxLengthList(max, label) : MaxLengthList(max);
        }

        if (string.Equals(message, $"The field {fieldName} must be a string with a maximum length of {max}."))
            return includeLabel ? MaxLengthString(max, label) : MaxLengthString(max);

        // Replace numeric validation message 
        if (string.Equals(message, $"The {fieldName} field must be a number."))
            return includeLabel ? MustBeANumberString(label) : MustBeANumberString();

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
                return includeLabel ? MaxValueString(maxValue, label) : MaxValueString(maxValue);
            if (!isMinimumForType && isMaximumForType)
                return includeLabel ? MinValueString(minValue, label) : MinValueString(minValue);
            if (!isMinimumForType && !isMaximumForType)
                return includeLabel ? NumberRangeString(minValue, maxValue, label) : NumberRangeString(minValue, maxValue);
        }

        return output;
    }
}
