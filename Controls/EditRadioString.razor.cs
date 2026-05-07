namespace Controls;

/// <summary> Edit control for selecting a string value from a list using radio buttons. Supports custom "Other" option.</summary>
public partial class EditRadioString : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the string property in the model.</summary>
    [Parameter] public required Expression<Func<string>> Field { get; set; }

    /// <summary> List of string options to display as radio buttons.</summary>
    [Parameter] public required List<string> Options { get; set; }

    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary> When true, includes an "Other" option with a text input field.</summary>
    [Parameter] public bool HasOther { get; set; }

    /// <summary> The labels around each radio button</summary>
    [Parameter] public string? LabelClass { get; set; }

    string _otherText = "";
    const string OtherName = "Other";
    string? _selectedOption;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
        _selectedOption = Value;
    }

    // Trivial parser — string passes through (matches Microsoft's InputText).
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null!;
        return true;
    }

    string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            _selectedOption = value;
            if (value == "Other")
            {
                Value = _otherText;
                ValueChanged.InvokeAsync(_otherText);
            }
            else
            {
                Value = value;
                _otherText = "";
                ValueChanged.InvokeAsync(value);
            }
        }
    }

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var value = CurrentValue;
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var isEditMode = (FormOptions?.IsEditMode ?? true) && IsEditMode;

        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenReadOnlyAndNull => isEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => isEditMode || !string.IsNullOrEmpty(value),
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => !string.IsNullOrEmpty(value),
            _ => true
        };
    }

    async Task SetOtherTextAsync(string value)
    {
        _otherText = value;
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }
}
