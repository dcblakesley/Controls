using System.Text.RegularExpressions;

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
    // Matches the .NET Range attribute message: "The field {Name} must be between {min} and {max}."
    // Captures min/max as the two whitespace-delimited tokens around "and"; tolerant of multi-word
    // field names and an optional trailing period. Compiled because we hit it on every render that
    // shows a Range validation error.
    static readonly Regex _numericRangeRegex = new(
        @"^The field .+ must be between (?<min>\S+) and (?<max>\S+?)\.?$",
        RegexOptions.Compiled);

    // New default validation messages
    static string RequiredString() => "Required";
    static string RequiredString(string label) => $"{label} is required.";

    static string MinLengthString(int? min) => $"Must contain at least {min} characters";
    static string MinLengthString(int? min, string label) => $"{label} must contain at least {min} characters";

    static string MinLengthList(int? min) => $"Must select at least {min} options";
    static string MinLengthList(int? min, string label) => $"{label} requires at least {min} options to be selected";

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
        //Console.WriteLine($"GetValidationMessage: {message}, {fieldName}, {label}, {valueType}, {max}, {min}, {includeLabel}");

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

        // Numeric range — e.g. "The field Min must be between -2 and 55."
        // Uses a regex so multi-word field names ("Order Total") and trailing-period variations don't
        // break the parse. When one bound is the type's min/max sentinel we render a one-sided message
        // ("Cannot exceed 100"); otherwise we render the full range.
        if (message.Contains(" must be between "))
        {
            var match = _numericRangeRegex.Match(message);
            if (match.Success)
            {
                var minValue = match.Groups["min"].Value;
                var maxValue = match.Groups["max"].Value;
                var isMinSentinel = IsTypeMinSentinel(minValue);
                var isMaxSentinel = IsTypeMaxSentinel(maxValue);

                if (isMinSentinel && !isMaxSentinel)
                    return includeLabel ? MaxValueString(maxValue, label) : MaxValueString(maxValue);
                if (!isMinSentinel && isMaxSentinel)
                    return includeLabel ? MinValueString(minValue, label) : MinValueString(minValue);
                if (!isMinSentinel && !isMaxSentinel)
                    return includeLabel ? NumberRangeString(minValue, maxValue, label) : NumberRangeString(minValue, maxValue);
            }
        }

        return output;
    }

    // Sentinel checks — every numeric primitive's MinValue/MaxValue as text. RangeAttribute formats
    // its message under the culture active at validation time, so the candidates must be produced
    // under that same culture: a set frozen at first static touch (the original design) stopped
    // matching the moment the culture diverged (de-DE writes "-1,79…E+308", sv-SE uses U+2212 for
    // the minus), silently degrading the one-sided "Cannot exceed…" rewrite. Deliberately NOT
    // cached at all: a per-culture-NAME cache still returns wrong-culture hits for same-name
    // cultures with customized number formats (CultureInfo clones, Windows user-override vs
    // GetCultureInfo instances). This path only runs while a Range message containing
    // " must be between " is being rewritten, where ~a dozen short ToString calls are noise.
    // The "-3.4028234663852886E+38" literal is the textual form Microsoft emits for float.MinValue,
    // which can differ slightly from float.MinValue.ToString() depending on culture / formatter.
    static bool IsTypeMinSentinel(string value) =>
        value == int.MinValue.ToString() || value == long.MinValue.ToString()
        || value == short.MinValue.ToString() || value == sbyte.MinValue.ToString()
        || value == byte.MinValue.ToString() || value == uint.MinValue.ToString()
        || value == ulong.MinValue.ToString() || value == ushort.MinValue.ToString()
        || value == double.MinValue.ToString() || value == float.MinValue.ToString()
        || value == decimal.MinValue.ToString() || value == "-3.4028234663852886E+38";

    static bool IsTypeMaxSentinel(string value) =>
        value == int.MaxValue.ToString() || value == long.MaxValue.ToString()
        || value == short.MaxValue.ToString() || value == sbyte.MaxValue.ToString()
        || value == byte.MaxValue.ToString() || value == uint.MaxValue.ToString()
        || value == ulong.MaxValue.ToString() || value == ushort.MaxValue.ToString()
        || value == double.MaxValue.ToString() || value == float.MaxValue.ToString()
        || value == decimal.MaxValue.ToString();
}
