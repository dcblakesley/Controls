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

    // Sentinel sets — every numeric primitive's MinValue/MaxValue as text. RangeAttribute formats
    // its message under the culture active at validation time, so the sentinels must be built under
    // that same culture: a set frozen at first static touch (the old design) stops matching the
    // moment the culture diverges (de-DE writes "-1,79…E+308", sv-SE uses U+2212 for the minus),
    // and the one-sided "Cannot exceed…" rewrite silently degrades to the raw between-message.
    // Cached per culture name; bounded by the handful of cultures a process actually serves.
    // The "-3.4028234663852886E+38" entry is the textual form Microsoft emits for float.MinValue,
    // which can differ slightly from float.MinValue.ToString() depending on culture / formatter.
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (HashSet<string> Min, HashSet<string> Max)>
        _sentinelsByCulture = new();

    static (HashSet<string> Min, HashSet<string> Max) CurrentCultureSentinels() =>
        _sentinelsByCulture.GetOrAdd(System.Globalization.CultureInfo.CurrentCulture.Name, static _ => (
            new HashSet<string>(StringComparer.Ordinal)
            {
                int.MinValue.ToString(), long.MinValue.ToString(), short.MinValue.ToString(),
                sbyte.MinValue.ToString(), byte.MinValue.ToString(),
                uint.MinValue.ToString(), ulong.MinValue.ToString(), ushort.MinValue.ToString(),
                double.MinValue.ToString(), float.MinValue.ToString(), decimal.MinValue.ToString(),
                "-3.4028234663852886E+38"
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                int.MaxValue.ToString(), long.MaxValue.ToString(), short.MaxValue.ToString(),
                sbyte.MaxValue.ToString(), byte.MaxValue.ToString(),
                uint.MaxValue.ToString(), ulong.MaxValue.ToString(), ushort.MaxValue.ToString(),
                double.MaxValue.ToString(), float.MaxValue.ToString(), decimal.MaxValue.ToString()
            }));

    static bool IsTypeMinSentinel(string value) => CurrentCultureSentinels().Min.Contains(value);
    static bool IsTypeMaxSentinel(string value) => CurrentCultureSentinels().Max.Contains(value);
}
