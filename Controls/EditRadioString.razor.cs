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
    // Internal radio value for the built-in "Other" option. Deliberately NOT the display text
    // "Other" — a consumer options list may legitimately contain "Other" as a real option, and the
    // sentinel must never collide with it (the collision silently replaced the model value with
    // the empty other-text). The sentinel never reaches the model; the setter maps it to _otherText.
    const string OtherName = "__wss-other__";
    string? _selectedOption;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
        DeriveSelectionFromValue();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Re-sync the radio selection with an externally-changed value (form reset, async-loaded
        // model, programmatic set). Skip when the current selection already implies CurrentValue,
        // so this never clobbers in-progress "Other" typing (where CurrentValue == _otherText).
        var implied = _selectedOption == OtherName ? _otherText : _selectedOption;
        if (CurrentValue != implied)
        {
            DeriveSelectionFromValue();
        }
    }

    // Maps the bound value back onto the radio selection: a value equal to an option checks that
    // option; any other non-empty value (when HasOther) selects "Other" and fills the text box.
    void DeriveSelectionFromValue()
    {
        var current = CurrentValue;
        if (string.IsNullOrEmpty(current))
        {
            _selectedOption = current;
            _otherText = "";
        }
        else if (Options.Contains(current))
        {
            _selectedOption = current;
            _otherText = "";
        }
        else if (HasOther)
        {
            _selectedOption = OtherName;
            _otherText = current;
        }
        else
        {
            _selectedOption = current; // no Other option to fall back on; renders as no selection
            _otherText = "";
        }
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
            // Assign through CurrentValue (not Value) so InputBase notifies the EditContext and
            // re-runs validation live — matches every other Edit* control.
            if (value == OtherName)
            {
                CurrentValue = _otherText;
            }
            else
            {
                _otherText = "";
                CurrentValue = value;
            }
        }
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);

    void SetOtherText(string? value)
    {
        _otherText = value ?? "";
        CurrentValue = value;
    }
}
