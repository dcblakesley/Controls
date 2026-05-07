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

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var effectiveHiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var value = Value;
        var isEditMode = (FormOptions == null) || FormOptions.IsEditMode;

        return effectiveHiding switch
        {
            HidingMode.None => true,
            HidingMode.WhenReadOnlyAndNull => isEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => isEditMode || (value != null && value.ToString() != ""),
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => value != null && value.ToString() != "",
            _ => true
        };
    }
}
