namespace Controls;

/// <summary> Edit control for selecting a string value from a list using radio buttons. Supports custom "Other" option.</summary>
public partial class EditRadioString : RadioGroupControlBase<string?>
{
    // Component-specific parameters. The shared radio-group surface (IsHorizontal, LabelClass, the
    // OptionType/ButtonStyle/Size trio + ButtonGroupClass, UpdateOn + UpdateEventName) lives on
    // RadioGroupControlBase<TValue>, which EditRadioEnum inherits too.

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

    /// <summary> When true, includes an "Other" option with a text input field.</summary>
    [Parameter] public bool HasOther { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each entry in <see cref="Options"/>. An
    /// option is disabled when this returns true OR the whole group's <c>IsDisabled</c> is
    /// true. Null (default) disables nothing beyond <c>IsDisabled</c>. Does not apply to the
    /// built-in "Other" radio (<see cref="HasOther"/>), which has no corresponding options entry.
    /// </summary>
    // Stays on the leaf rather than RadioGroupControlBase: EditRadioEnum's counterpart is typed on
    // its TEnum, not on the base's TValue, so the two aren't the same member.
    [Parameter] public Func<string, bool>? IsOptionDisabled { get; set; }

    /// <summary>
    /// Routes a commit from the shared <see cref="RadioOtherInput"/> to <see cref="SetOtherText"/>.
    /// The event-name plumbing itself (<c>RadioGroupControlBase.OtherInputAttribute</c>) is shared
    /// with <see cref="EditRadioEnum{TEnum}"/>, so both controls render the one shared element
    /// instead of two copies of the markup that had silently drifted apart.
    /// <para>
    /// It replaced an <c>@bind:get</c>/<c>@bind:set</c>/<c>@bind:event</c> trio on an inline input.
    /// Behaviorally identical here: the only thing <c>@bind</c> added was its "re-push the value to
    /// the DOM when the setter didn't change it" guard, and <see cref="SetOtherText"/> always stores
    /// the typed text verbatim, so that guard never had anything to revert.
    /// </para>
    /// </summary>
    protected override Task OnOtherTextCommitted(ChangeEventArgs e)
    {
        SetOtherText(e.Value?.ToString());
        return Task.CompletedTask;
    }

    string _otherText = "";
    // Internal radio value for the built-in "Other" option. Deliberately NOT the display text
    // "Other" — a consumer options list may legitimately contain "Other" as a real option, and the
    // sentinel must never collide with it (the collision silently replaced the model value with
    // the empty other-text). The sentinel travels through the radio value channel, so it is
    // uniquified against Options rather than hoping no entry matches — collision is impossible by
    // construction. It never reaches the model; the setter maps it to _otherText.
    string _otherName = "__wss-other__";
    string? _selectedOption;

    // One distinct id segment per Options entry, positionally aligned with it and consumed by the
    // markup as each RadioOptionItem's IdSuffix. EnumHelpers.ToId strips non-ASCII, so an all-CJK
    // options list gave every radio the same rb-{id}- element id -- and in Button mode, where the
    // label associates by `for`, every button then toggled the FIRST radio. "other" is reserved so the
    // built-in Other radio keeps its exact rb-{id}-other id and a colliding real option yields instead.
    // Ordinary ASCII options are unaffected and render the ids they always did.
    string[] _optionIds = [];

    // Extra init on top of the base's state wiring: the "Other" sentinel and the initial selection.
    // Base first, matching the order these ran in when the InitState call was spelled out here.
    protected override void OnInitialized()
    {
        base.OnInitialized();
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
        _optionIds = EnumHelpers.ToUniqueIds(Options, HasOther ? "other" : null);
        // Re-sync the radio selection with an externally-changed value (form reset, async-loaded
        // model, programmatic set). Skip when the current selection already implies CurrentValue,
        // so this never clobbers in-progress "Other" typing (where CurrentValue == _otherText).
        //
        // Known blind spot, and not fixable from here: a record swap whose NEW value equals the old
        // one is indistinguishable from "nothing happened", so the preserved _otherText from a
        // switch-away on the previous record survives into the new one's disabled Other box. Any
        // other swap re-derives below and clears it. (EditRadioEnum's own preserved copy has exactly
        // this residual for exactly this reason -- see its OnParametersSet -- after the wider case,
        // where the value DID change, was fixed there.)
        var implied = _selectedOption == _otherName ? _otherText : _selectedOption;
        if (CurrentValue != implied)
        {
            DeriveSelectionFromValue();
        }
    }

    // Mirrors EditRadioEnum's IsOptionDisabledFor so the two markup files diff cleanly. No nullable
    // unwrap needed on this side: the predicate takes the same string the option loop yields.
    bool IsOptionDisabledFor(string option) =>
        IsDisabled || IsOptionDisabled?.Invoke(option) == true;

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
                // _otherText is deliberately NOT cleared: the model stops carrying the typed text the
                // moment a real option is selected (CurrentValue below), but the disabled box goes on
                // showing it, so an accidental mis-click is recoverable by clicking Other again --
                // preserve-but-don't-submit, matching EditRadioEnum's separate OtherValue.
                CurrentValue = value;
            }
        }
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);

    // The "Other" box's commit path, reached from OnOtherTextChanged (see OtherInputAttribute).
    // Unlike EditRadioEnum -- whose Other text is a separate OtherValue/OtherValueChanged pair -- the
    // typed text IS this control's bound value, so it goes straight to CurrentValue.
    void SetOtherText(string? value)
    {
        _otherText = value ?? "";
        CurrentValue = value;
    }
}
