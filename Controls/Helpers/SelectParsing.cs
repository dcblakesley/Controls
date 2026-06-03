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
    /// </summary>
    public static bool TryParseStringOrConvert<TValue>(string? value, string fieldName, out TValue result, out string validationErrorMessage)
    {
        if (typeof(TValue) == typeof(string))
        {
            result = (TValue)(object)value!;
            validationErrorMessage = null!;
            return true;
        }

        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var converted))
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
