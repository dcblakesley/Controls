namespace Controls;

/// <summary> Select a string from Options (List of strings)</summary>
// TValue is annotated 'All' because parsing goes through SelectParsing.TryParseStringOrConvert →
// BindConverter.TryConvertTo<TValue> (mirrors the framework's InputSelect<TValue>). The parse/format/
// IsValueDefault trio it needs lives on the shared EditSelectBase, together with EditSelect's.
public partial class EditSelectString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : EditSelectBase<TValue>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }

    /// <summary> List of string options to display in the select dropdown.</summary>
    [Parameter] public required List<string> Options { get; set; }

    /// <summary>
    /// Display text for the leading empty option. A null/empty bound value selects it, so the
    /// control shows blank instead of silently displaying the first option while the model holds
    /// null (mirrors <c>EditSelectEnum.NullOptionText</c>). Defaults to empty ("" — a blank-labeled
    /// option). Set to <c>null</c> to suppress the empty option entirely (e.g. a required field that
    /// must always hold one of the options). Has no effect when <typeparamref name="TValue"/> is a
    /// non-nullable value type, where a blank would only map to a spurious <c>default</c> value.
    /// </summary>
    [Parameter] public string? NullOptionText { get; set; } = "";

    /// <summary>
    /// Resolved text for the leading null option. Unlike <see cref="EditSelectEnum{TEnum}"/>'s
    /// equivalent, <c>null</c> and empty string are NOT interchangeable here: <c>null</c> is the
    /// consumer's explicit "suppress the leading option entirely" opt-out (see
    /// <see cref="ShowNullOption"/>) and must never be resurrected into a non-null value just because
    /// a model attribute exists — that would re-show an option the consumer deliberately removed. Only
    /// an empty string (the parameter's own default, meaning "unset") falls through to the model's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> attribute; an explicit non-empty
    /// <see cref="NullOptionText"/> always wins.
    /// </summary>
    string? EffectiveNullOptionText => NullOptionText is null
        ? null
        : NullOptionText.Length == 0 ? _attributes.Placeholder() ?? "" : NullOptionText;

    // Reference types (incl. string — NRT annotations are erased at runtime, so string and string?
    // are indistinguishable here) and Nullable<T> value types can represent "no value". A non-nullable
    // value type (e.g. int) cannot, so a blank there would only map to a spurious default(TValue).
    static readonly bool CanBeNull = !typeof(TValue).IsValueType || Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    /// <summary> Whether the leading blank option renders: suppressed when <see cref="NullOptionText"/>
    /// is null (explicit opt-out) or <typeparamref name="TValue"/> is a non-nullable value type. </summary>
    bool ShowNullOption => NullOptionText is not null && CanBeNull;
}
