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

    static string MaxLengthList(int? max) => $"Cannot exceed {max} selections";
    static string MaxLengthList(int? max, string label) => $"{label} cannot exceed {max} selections";

    static string RangeString(int? min, int? max) => $"Must be between {min} and {max} characters";
    static string RangeString(int? min, int? max, string label) => $"{label} must be between {min} and {max} characters";

    static string MustBeANumberString() => "Must be a number";
    static string MustBeANumberString(string label) => $"{label} must be a number.";

    static string MustBeADateString() => "Must be a date";
    static string MustBeADateString(string label) => $"{label} must be a date.";

    static string MinValueString(string min) => $"Must be at least {min}";
    static string MinValueString(string min, string label) => $"{label} must be at least {min}";

    static string MaxValueString(string max) => $"Cannot exceed {max}";
    static string MaxValueString(string max, string label) => $"{label} cannot exceed {max}";

    static string NumberRangeString(string min, string max) => $"Must be between {min} and {max}";
    static string NumberRangeString(string min, string max, string label) => $"{label} must be between {min} and {max}";

    /// <summary>
    /// Overrides the default validation messages. Matches the framework text under the raw member name
    /// only — use the <c>displayName</c> overload for models that carry <c>[Display(Name = "…")]</c>.
    /// </summary>
    public static string GetValidationMessage(string message, string fieldName, string label, string? valueType, int? max = null, int? min = null, bool includeLabel = false) =>
        GetValidationMessage(message, fieldName, null, label, valueType, max, min, includeLabel);

    /// <summary>
    /// Overrides the default validation messages. <paramref name="displayName"/> is the name
    /// DataAnnotations itself used when it formatted <paramref name="message"/> —
    /// <c>ValidationContext.DisplayName</c>, which resolves <c>[Display(Name = "…")]</c> when the
    /// property carries one; null (or equal to <paramref name="fieldName"/>) when it doesn't. Both
    /// spellings are tried because the rewrites below are exact-string matches and a decorated
    /// property's framework message contains no trace of the member name.
    /// (<c>[DisplayName]</c> needs nothing here — DataAnnotations doesn't read it.)
    /// </summary>
    public static string GetValidationMessage(string message, string fieldName, string? displayName, string label, string? valueType, int? max = null, int? min = null, bool includeLabel = false)
    {
        var rewritten = ExactMatchRewrite(message, fieldName, label, valueType, max, min, includeLabel);
        if (rewritten is null && !string.IsNullOrEmpty(displayName) && !string.Equals(displayName, fieldName))
            rewritten = ExactMatchRewrite(message, displayName, label, valueType, max, min, includeLabel);
        if (rewritten is not null)
            return rewritten;

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
                var isMinSentinel = RangeSentinels.IsMin(minValue);
                var isMaxSentinel = RangeSentinels.IsMax(maxValue);

                if (isMinSentinel && !isMaxSentinel)
                    return includeLabel ? MaxValueString(maxValue, label) : MaxValueString(maxValue);
                if (!isMinSentinel && isMaxSentinel)
                    return includeLabel ? MinValueString(minValue, label) : MinValueString(minValue);
                if (!isMinSentinel && !isMaxSentinel)
                    return includeLabel ? NumberRangeString(minValue, maxValue, label) : NumberRangeString(minValue, maxValue);
            }
        }

        return message;
    }

    // The framework's own message text for a given attribute, reconstructed and compared verbatim: a
    // match proves DataAnnotations (or a control's parse-error path) produced this message for THIS
    // field with THESE bounds, which is what makes replacing it safe. `name` is one candidate spelling
    // of the field — the member name, or the [Display(Name)] the framework formatted with. Returns null
    // when nothing matched, so the caller can try the other spelling.
    static string? ExactMatchRewrite(string message, string name, string label, string? valueType, int? max, int? min, bool includeLabel)
    {
        // Required
        if (string.Equals(message, $"The {name} field is required."))
            return includeLabel ? RequiredString(label) : RequiredString();

        // StringLength with only max
        if (string.Equals(message, $"The field {name} must be a string with a maximum length of {max}."))
            return includeLabel ? MaxLengthString(max, label) : MaxLengthString(max);

        // StringLength with Min
        if (string.Equals(message, $"The field {name} must be a string with a minimum length of {min} and a maximum length of {max}."))
            return includeLabel ? RangeString(min, max, label) : RangeString(min, max);

        // MinLength
        if (string.Equals(message, $"The field {name} must be a string or array type with a minimum length of '{min}'."))
        {
            if (valueType == "System.String")
                return includeLabel ? MinLengthString(min, label) : MinLengthString(min);
            return includeLabel ? MinLengthList(min, label) : MinLengthList(min);
        }

        // MaxLength
        if (string.Equals(message, $"The field {name} must be a string or array type with a maximum length of '{max}'."))
        {
            if (valueType == "System.String")
                return includeLabel ? MaxLengthString(max, label) : MaxLengthString(max);
            return includeLabel ? MaxLengthList(max, label) : MaxLengthList(max);
        }

        // Numeric parse failure — the controls format their ParsingErrorMessage with the raw member
        // name, so this one never sees a [Display(Name)] spelling; harmless to try both.
        if (string.Equals(message, $"The {name} field must be a number."))
            return includeLabel ? MustBeANumberString(label) : MustBeANumberString();

        // Date parse failure — EditDate/EditDateNative/EditDateRange's ParsingErrorMessage default,
        // formatted the same way as the numeric one.
        if (string.Equals(message, $"The {name} field must be a date."))
            return includeLabel ? MustBeADateString(label) : MustBeADateString();

        return null;
    }

    // The sentinel candidates themselves live in RangeSentinels, shared verbatim with
    // AttributesHelper's DOM min/max rendering -- see that type for which extremes count and why. Two
    // private lists here and there is exactly how the two layers drifted apart on 8 of the 12 numeric
    // extremes; there is only the one set now.
}
