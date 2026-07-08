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
    public static bool TryParseStringOrConvert<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue>(string? value, string fieldName, out TValue result, out string validationErrorMessage)
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
    /// date/time types are special-cased to their ISO round-trip forms because their invariant *display*
    /// format (<c>DateOnly</c> → <c>06/15/2026</c>, <c>DateTime</c> → <c>06/15/2026 00:00:00</c>) matched no
    /// ISO-authored option like <c>&lt;option value="2026-06-15"&gt;</c> — the same "picks fine, selection
    /// lost" desync as the numeric case; anything else culture-sensitive (numeric types, <c>Guid</c>) is
    /// <see cref="IFormattable"/> and formats under <see cref="CultureInfo.InvariantCulture"/>; enums are
    /// <c>IFormattable</c> but format by name regardless of culture, round-tripping with the invariant parse.
    /// </summary>
    public static string? FormatInvariant<TValue>(TValue? value) => value switch
    {
        null => null,
        string s => s,
        // Date/time arms precede the generic IFormattable arm (all four types implement it) and emit the
        // ISO forms the invariant BindConverter parse above accepts. A non-null Nullable<T> boxes to its
        // underlying type, so these also match DateOnly?/DateTime?/DateTimeOffset?/TimeOnly?.
        DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),           // yyyy-MM-dd
        DateTime dt => dt.ToString("s", CultureInfo.InvariantCulture),         // sortable yyyy-MM-ddTHH:mm:ss — select option values don't carry sub-second precision, so it's dropped
        // Shortest round-trippable form: "O" always emits seven fractional digits, which no authored
        // option value (`2026-06-15T09:30:00+02:00`) ever contains, so the match failed for every
        // hand-written option. Whole-second values (the only kind an option can express) format
        // without the fraction; a sub-second value falls back to "O" so nothing is silently lost.
        DateTimeOffset dto => dto.Ticks % TimeSpan.TicksPerSecond == 0
            ? dto.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK", CultureInfo.InvariantCulture)
            : dto.ToString("O", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture),
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
