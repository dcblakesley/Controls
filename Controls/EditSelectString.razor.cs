namespace Controls;

/// <summary> Select a string from Options (List of strings)</summary>
// TValue is annotated 'All' because parsing goes through SelectParsing.TryParseStringOrConvert →
// BindConverter.TryConvertTo<TValue> (mirrors the framework's InputSelect<TValue>).
public partial class EditSelectString<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : EditControlBase<TValue>
{
    // Component-specific parameters

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

    // Reference types (incl. string — NRT annotations are erased at runtime, so string and string?
    // are indistinguishable here) and Nullable<T> value types can represent "no value". A non-nullable
    // value type (e.g. int) cannot, so a blank there would only map to a spurious default(TValue).
    static readonly bool CanBeNull = !typeof(TValue).IsValueType || Nullable.GetUnderlyingType(typeof(TValue)) is not null;

    /// <summary> Whether the leading blank option renders: suppressed when <see cref="NullOptionText"/>
    /// is null (explicit opt-out) or <typeparamref name="TValue"/> is a non-nullable value type. </summary>
    bool ShowNullOption => NullOptionText is not null && CanBeNull;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditSelectString<TValue>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Strings pass through; anything else round-trips via BindConverter (shared with EditSelect).
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage) =>
        SelectParsing.TryParseStringOrConvert(value, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    // Format invariantly to match the parse side (see EditSelect) — culture-sensitive default formatting
    // desynced the option-value match under cultures with different numeric separators/signs.
    protected override string? FormatValueAsString(TValue? value) => SelectParsing.FormatInvariant(value);

    // Empty stringified value counts as "default" — matches the prior behavior where
    // value.ToString() != "" gated the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue?.ToString());
}
