namespace Controls;

/// <summary>
/// Base for the two native-<c>&lt;select&gt;</c> controls that bind an arbitrary
/// <typeparamref name="TValue"/> and round-trip it through <see cref="SelectParsing"/>:
/// <see cref="EditSelect{TValue}"/> (consumer-authored <c>&lt;option&gt;</c> markup) and
/// <see cref="EditSelectString{TValue}"/> (a <c>List&lt;string&gt;</c> of options).
/// </summary>
/// <remarks>
/// <para>
/// The three overrides below were byte-identical in both controls, with only a pair of comments
/// asserting they had to stay in sync. Hoisting them here makes that coupling something the compiler
/// enforces instead of something a reader has to remember. The controls keep everything that genuinely
/// differs (their options source, read-only text, and the leading blank/placeholder option rules).
/// </para>
/// <para>
/// The other two selects deliberately do NOT derive from this. <see cref="EditSelectEnum{TEnum}"/>
/// parses by enum member name (<c>SelectParsing.TryParseEnum</c>) and treats empty input as "required"
/// for a non-nullable enum, so neither the parse nor the format arm applies. <c>EditSelectSearch</c>
/// binds through a value callback rather than string parsing (its <c>TryParseValueFromString</c>
/// throws) and its <typeparamref name="TValue"/> carries no <see cref="DynamicallyAccessedMembersAttribute"/>
/// annotation, which this base requires — adding one is a public-API change for its consumers. It keeps
/// its own copy of the <see cref="IsValueDefault"/> union, with a pointer back here.
/// </para>
/// </remarks>
// TValue is annotated 'All' because parsing goes through SelectParsing.TryParseStringOrConvert ->
// BindConverter.TryConvertTo<TValue> (mirrors the framework's InputSelect<TValue>).
public abstract class EditSelectBase<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : EditControlBase<TValue>
{
    /// <summary>
    /// Strings pass through; enums and other value types round-trip via <c>BindConverter</c>.
    /// </summary>
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage) =>
        SelectParsing.TryParseStringOrConvert(value, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    /// <summary>
    /// Formats invariantly to match the parse side — the default (<c>value?.ToString()</c>) is
    /// culture-sensitive, so a de-DE double 1.5 rendered "1,5" and matched no
    /// <c>&lt;option value="1.5"&gt;</c>.
    /// </summary>
    protected override string? FormatValueAsString(TValue? value) => SelectParsing.FormatInvariant(value);

    /// <summary>
    /// Union of the base default check and the empty-string case. The base's
    /// <c>EqualityComparer&lt;TValue&gt;.Default.Equals(value, default)</c> alone is NOT enough for
    /// <typeparamref name="TValue"/> = <see cref="string"/>: <c>default(string)</c> is null, not "", so a
    /// string-bound select at the empty string stayed visible under
    /// <c>WhenNullOrDefault</c>/<c>WhenReadOnlyAndNullOrDefault</c> while every sibling string control hid
    /// it — contradicting <see cref="HidingMode"/>'s documented "null or its type's default (e.g. empty
    /// string, 0, ...)". Unioned with the base check rather than replacing it, so every other
    /// <typeparamref name="TValue"/> keeps its own default: an <c>int</c> at 0 and a <c>bool</c> at false
    /// still count as default, which stringifying them ("0"/"False") would have silently broken.
    /// </summary>
    protected override bool IsValueDefault() =>
        base.IsValueDefault() || CurrentValue is string { Length: 0 };
}
