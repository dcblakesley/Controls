namespace Controls;

/// <summary> Edit control for selecting a string value from a list using radio buttons. Supports custom "Other" option.</summary>
public partial class EditRadioString : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<string?>>? Field { get; set; }

    /// <summary> List of string options to display as radio buttons.</summary>
    [Parameter] public required List<string> Options { get; set; }

    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary> When true, includes an "Other" option with a text input field.</summary>
    [Parameter] public bool HasOther { get; set; }

    /// <summary> The labels around each radio button</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each entry in <see cref="Options"/>. An
    /// option is disabled when this returns true OR the whole group's <c>IsDisabled</c> is
    /// true. Null (default) disables nothing beyond <c>IsDisabled</c>. Does not apply to the
    /// built-in "Other" radio (<see cref="HasOther"/>), which has no corresponding options entry.
    /// </summary>
    [Parameter] public Func<string, bool>? IsOptionDisabled { get; set; }

    string _otherText = "";
    // Internal radio value for the built-in "Other" option. Deliberately NOT the display text
    // "Other" — a consumer options list may legitimately contain "Other" as a real option, and the
    // sentinel must never collide with it (the collision silently replaced the model value with
    // the empty other-text). The sentinel travels through the radio value channel, so it is
    // uniquified against Options rather than hoping no entry matches — collision is impossible by
    // construction. It never reaches the model; the setter maps it to _otherText.
    string _otherName = "__wss-other__";
    string? _selectedOption;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditRadioString)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
        ComputeOtherSentinel();
        DeriveSelectionFromValue();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Recompute first: an Options swap can invalidate the sentinel, and the implied-value check
        // below reads it. If Other was selected under the old sentinel, `implied` no longer matches
        // CurrentValue, so the selection re-derives (and lands on Other under the new sentinel).
        ComputeOtherSentinel();
        // Re-sync the radio selection with an externally-changed value (form reset, async-loaded
        // model, programmatic set). Skip when the current selection already implies CurrentValue,
        // so this never clobbers in-progress "Other" typing (where CurrentValue == _otherText).
        var implied = _selectedOption == _otherName ? _otherText : _selectedOption;
        if (CurrentValue != implied)
        {
            DeriveSelectionFromValue();
        }
    }

    void ComputeOtherSentinel()
    {
        _otherName = "__wss-other__";
        while (Options.Contains(_otherName)) _otherName += "!";
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
            _selectedOption = _otherName;
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
            if (value == _otherName)
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
