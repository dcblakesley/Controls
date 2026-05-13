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

    // Same parser shape as EditSelect — strings pass through, enums via BindConverter, anything else
    // via BindConverter as a fallback.
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
    {
        var typeOfValue = typeof(TValue);

        if (typeOfValue == typeof(string))
        {
            result = (TValue)(object)value!;
            validationErrorMessage = null!;
            return true;
        }

        if (BindConverter.TryConvertTo<TValue>(value, CultureInfo.CurrentCulture, out var parsedValue))
        {
            result = parsedValue!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }

    // Empty stringified value counts as "default" — matches the prior behavior where
    // value.ToString() != "" gated the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue?.ToString());
}
