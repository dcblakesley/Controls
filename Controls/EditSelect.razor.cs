namespace Controls;

/// <summary>
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
public partial class EditSelect<TValue> : EditControlBase<TValue>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    /// <summary> The <c>&lt;option&gt;</c> elements to render inside the select.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Optional read-only display text. The options are consumer-supplied markup, so the control
    /// can't resolve a value's display label itself — with <c>&lt;option value="1"&gt;One&lt;/option&gt;</c>
    /// read-only mode would show "1"; pass "One" here (typically resolved from the bound value).
    /// </summary>
    [Parameter] public string? ReadOnlyText { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // Strings pass through; enums and other value types round-trip via BindConverter.
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage) =>
        SelectParsing.TryParseStringOrConvert(value, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    // Format invariantly to match the parse side — the default (value?.ToString()) is culture-sensitive,
    // so a de-DE double 1.5 rendered "1,5" and matched no <option value="1.5">.
    protected override string? FormatValueAsString(TValue? value) => SelectParsing.FormatInvariant(value);

    // Base IsValueDefault covers EqualityComparer<TValue>.Default behavior — no override needed.
}
