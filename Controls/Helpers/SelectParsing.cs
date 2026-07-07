using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Controls.Helpers;

/// <summary>
/// Shared <c>TryParseValueFromString</c> logic for the form selects. Replaces the near-identical
/// parser bodies that <c>EditSelect</c> / <c>EditSelectString</c> and <c>EditSelectEnum</c> /
/// <c>EditRadioEnum</c> each carried. Messages are the raw DataAnnotation-style strings the rest of
/// the library emits (<see cref="ValidationHelper"/> rewrites them for display).
/// </summary>
public static class SelectParsing
{
    static string NotValid(string fieldName) => $"The {fieldName} field is not valid.";
    static string Required(string fieldName) => $"The {fieldName} field is required.";

    /// <summary>
    /// Strings pass through unchanged; anything else round-trips via <see cref="BindConverter"/>
    /// (covers enums and primitive value types). Used by <c>EditSelect</c> / <c>EditSelectString</c>.
    /// Empty input (a leading blank option, or an <c>&lt;option value=""&gt;</c>) clears to
    /// <c>default(TValue)</c> — <c>null</c> for reference types and <see cref="Nullable{T}"/>, the zero
    /// value otherwise — instead of storing <c>""</c> (so a <c>string?</c> can return to null) or feeding
    /// <c>""</c> to <see cref="BindConverter"/> for a non-string type (which would fail with "not valid").
    /// </summary>
    public static bool TryParseStringOrConvert<TValue>(string? value, string fieldName, out TValue result, out string validationErrorMessage)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = default!;
            validationErrorMessage = null!;
            return true;
        }

        if (typeof(TValue) == typeof(string))
        {
            result = (TValue)(object)value!;
            validationErrorMessage = null!;
            return true;
        }

        // Invariant, matching EditNumber's convention: option values are markup/code-authored
        // literals ("1.5"), and CurrentCulture parsing read "1.5" as 15 under de-DE (thousands-
        // separator tolerance) while the value formatted back as "1,5" — matching no option.
        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.InvariantCulture, out var converted))
        {
            result = converted!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = NotValid(fieldName);
        return false;
    }

    /// <summary>
    /// Formats a select's bound value back to its <c>&lt;option value&gt;</c> string invariantly — the
    /// mirror of <see cref="TryParseStringOrConvert{TValue}"/>. <see cref="InputBase{TValue}"/>'s default
    /// <c>FormatValueAsString</c> is <c>value?.ToString()</c>, which uses the current culture: under de-DE
    /// a <c>double</c> <c>1.5</c> rendered <c>"1,5"</c>, matched no <c>&lt;option value="1.5"&gt;</c>, and the
    /// select showed unselected (sv-SE's non-ASCII minus sign had the same effect). Strings pass through;
    /// anything culture-sensitive (numeric types, <c>DateTime</c>, <c>Guid</c>) is <see cref="IFormattable"/>
    /// and formats under <see cref="CultureInfo.InvariantCulture"/>; enums are <c>IFormattable</c> but
    /// format by name regardless of culture, round-tripping with the invariant parse.
    /// </summary>
    public static string? FormatInvariant<TValue>(TValue? value) => value switch
    {
        null => null,
        string s => s,
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    };

    /// <summary>
    /// Parses an enum (or nullable enum) by name. Empty input is valid (null) for a nullable enum and
    /// "required" otherwise. Used by <c>EditSelectEnum</c> / <c>EditRadioEnum</c>.
    /// </summary>
    public static bool TryParseEnum<TValue>(string? value, Type underlyingType, bool isNullable, string fieldName, out TValue result, out string validationErrorMessage)
    {
        if (string.IsNullOrEmpty(value))
        {
            result = default!;
            if (isNullable)
            {
                validationErrorMessage = null!;
                return true;
            }
            validationErrorMessage = Required(fieldName);
            return false;
        }

        if (Enum.TryParse(underlyingType, value, out var parsed))
        {
            result = (TValue)parsed!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = NotValid(fieldName);
        return false;
    }
}
