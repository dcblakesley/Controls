namespace Controls;

/// <summary> Select a string from Options (List of strings)</summary>
public partial class EditSelectString<TValue> : EditControlBase<TValue>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    /// <summary> List of string options to display in the select dropdown.</summary>
    [Parameter] public required List<string> Options { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // Strings pass through; anything else round-trips via BindConverter (shared with EditSelect).
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage) =>
        SelectParsing.TryParseStringOrConvert(value, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    // Empty stringified value counts as "default" — matches the prior behavior where
    // value.ToString() != "" gated the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue?.ToString());
}
