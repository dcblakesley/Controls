namespace Controls;

/// <summary> Edit control for selecting an enum value using radio buttons. Supports sorting and an optional "Other" option with text input.</summary>
// TEnum is annotated 'All' because the markup renders InputRadioGroup<TEnum?>/InputRadio<TEnum?>,
// whose TValue declares that requirement.
public partial class EditRadioEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEnum> : RadioGroupControlBase<TEnum?>
{
    // Component-specific parameters. The shared radio-group surface (IsHorizontal, LabelClass, the
    // OptionType/ButtonStyle/Size trio + ButtonGroupClass, UpdateOn + UpdateEventName) lives on
    // RadioGroupControlBase<TValue>, which EditRadioString inherits too.

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TEnum>>? Field { get; set; }

    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each enum value being rendered (including
    /// the "Other" option's enum value when <see cref="HasOtherOption"/> is set). An option is
    /// disabled when this returns true OR the whole group's <c>IsDisabled</c> is true. Null
    /// (default) disables nothing beyond <c>IsDisabled</c>.
    /// </summary>
    /// <remarks>
    /// RAD-2 hazard: per native radio-group semantics, roving tabindex hands the group's one native
    /// Tab stop to whichever radio is currently checked. If this predicate names exactly the
    /// currently-selected value (a realistic "this choice is now locked" scenario), naively
    /// rendering that option's native <c>disabled</c> strands the <i>entire group</i> out of the Tab
    /// sequence -- no other radio becomes a fallback stop. This control defends against that itself
    /// (see <c>IsOptionLockedFor</c> in the code-behind): the selected option stays natively
    /// focusable and is marked <c>aria-disabled="true"</c> instead, so it never widens into a
    /// whole-group focus trap. Only <see cref="EditControlBase{TValue}.IsDisabled"/> (the
    /// whole-group switch) still natively disables the selected option.
    /// </remarks>
    // Can't hoist to RadioGroupControlBase: this control declares Func<TEnum, bool>? while inheriting
    // RadioGroupControlBase<TEnum?>, so the predicate's type argument isn't the base's TValue.
    [Parameter] public Func<TEnum, bool>? IsOptionDisabled { get; set; }

    // Other Option
    /// <summary> When true, includes an "Other" option with a text input field. The last enum value is treated as the "Other" option.</summary>
    [Parameter] public bool HasOtherOption { get; set; } = false;

    /// <summary> Placeholder text for the "Other" option text input.</summary>
    [Parameter] public string? OtherPlaceholder { get; set; }

    /// <summary> The text value entered in the "Other" option text input.</summary>
    [Parameter] public string? OtherValue { get; set; }

    /// <summary> Event callback that fires when the OtherValue changes.</summary>
    [Parameter] public EventCallback<string?> OtherValueChanged { get; set; }

    /// <summary>
    /// Compiler-populated by <c>@bind-OtherValue</c> alongside <see cref="OtherValue"/>/
    /// <see cref="OtherValueChanged"/> — the accessor for the SECOND model property this control
    /// binds. Supplying it is what makes the "Other" free-text box a first-class field: it gets a
    /// <see cref="FieldIdentifier"/>, a <see cref="FormOptions"/> registration (so
    /// <see cref="ValidationView"/> can link to it), and an <see cref="EditContext"/> notification on
    /// every write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Optional, and the notification is opt-in by binding.</b> Wiring
    /// <see cref="OtherValue"/>/<see cref="OtherValueChanged"/> by hand (rather than through
    /// <c>@bind-OtherValue</c>) leaves this null, and then the box behaves exactly as it always has:
    /// it writes the model through the callback and raises no <c>OnFieldChanged</c>. That silence is
    /// the bug this closes — an <c>OnFieldChanged</c>-driven auto-save (see <see cref="FormAutoSave"/>)
    /// simply never heard the free text — but it is not worth throwing over, since a consumer may have
    /// a deliberate reason to drive the pair manually.
    /// </para>
    /// <para>
    /// <b>Binding it also enrolls the property in the validation summary.</b> That is the intended
    /// consequence, not a side effect: an annotation on the OtherValue property (say
    /// <c>[Required]</c> or <c>[StringLength]</c>) previously produced a message no
    /// <see cref="ValidationView"/> could link to, because nothing had registered the field. It now
    /// registers under the free-text box's own element id (<c>other-{id}</c>), so the summary link
    /// lands on the input the message is about. <see cref="EditRadioString"/> needs none of this: its
    /// Other text IS the bound value, so it already travels the normal <c>InputBase</c> path.
    /// </para>
    /// </remarks>
    [Parameter] public Expression<Func<string?>>? OtherValueExpression { get; set; }

    /// <summary>
    /// Overrides the "Other" free-text box's accessible name
    /// (<see cref="Controls.RadioOtherInput.AriaLabel"/>). Null (default) uses
    /// <see cref="Controls.RadioOtherInput.DefaultAriaLabel"/> ("Custom text value input") -- RAD-4:
    /// that generic literal used to be hard-coded with no parameter, no localization, and no tie back
    /// to this field or its "Other" option. Set this to something field-specific ("Other priority
    /// reason", etc.) or a localized string.
    /// </summary>
    [Parameter] public string? OtherAriaLabel { get; set; }

    // No local _isNullable mirror (unlike EditSelectEnum, whose markup reads one for the leading
    // empty option): nothing in this control's markup needs it, and TryParseValueFromString below
    // reads the cache's own IsNullable.
    readonly EnumOptionCache<TEnum> _cache = new();

    // One distinct id segment per option, positionally aligned with GetOptions() and consumed by the
    // markup as each RadioOptionItem's IdSuffix. Enum member names are C# identifiers, which may be
    // non-ASCII -- and EnumHelpers.ToId strips those, so such an enum gave every radio the same
    // rb-{id}- element id (in Button mode, where the label associates by `for`, every button then
    // toggled the FIRST radio). See EnumHelpers.ToUniqueIds; ASCII member names are unaffected.
    string[] _optionIds = [];

    // Extra init on top of the base's state wiring: seed the option cache. Base first, matching the
    // order this ran in when the InitState call was spelled out here.
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _cache.Initialize(Sort, HasOtherOption);
        // Derived once, like the base's own _fieldIdentifier: @bind-OtherValue emits a fresh lambda
        // instance every render, so a per-cycle re-derive would rebuild it on every keystroke for no
        // gain. Null expression = the consumer wired the pair by hand; everything below stays off.
        if (OtherValueExpression is not null)
        {
            _otherFieldIdentifier = FieldIdentifier.Create(OtherValueExpression);
            _otherFieldBound = true;
        }
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // AFTER base, which is where _id is re-resolved (EditControlInit.SyncResolvedId) -- the Other
        // box's element id derives from it, so registering first would pin the stale one.
        SyncOtherFieldRegistration();
        // The option list is cached, but the parameters that shape it may change at runtime —
        // previously a Sort/HasOtherOption change was silently ignored forever.
        _cache.Refresh(Sort, HasOtherOption);
        // Runs after Refresh so the ids track a reordered/resized option list. Cheap: ToId memoizes.
        _optionIds = EnumHelpers.ToUniqueIds(_cache.Options);

        // Did the bound pair move behind this control's back? Every self-write records what it wrote
        // (see _observedValue), so anything still differing here came from OUTSIDE -- a parent
        // swapping the bound record, a form reset, an async model load, a programmatic set.
        var externalChange =
            !EqualityComparer<TEnum?>.Default.Equals(Value, _observedValue) ||
            !string.Equals(OtherValue, _observedOtherValue, StringComparison.Ordinal);
        _observedValue = Value;
        _observedOtherValue = OtherValue;

        // Remember whatever text the model currently carries, so switching away from Other can take it
        // off the model while still showing it (see OnSelectionChangedAsync). It is dropped in the two
        // cases where keeping it would be a lie:
        //   * an empty OtherValue while Other is STILL the selected option -- a real clear by the
        //     parent (form reset, reload), unlike the empty it holds right after a switch away, which
        //     this control wrote itself and must not read as an instruction to forget the text;
        //   * an EXTERNAL change that leaves Other unselected with no text -- i.e. a different record
        //     entirely. The preserved copy belongs to the record it was typed on, and without this the
        //     new record's disabled Other box displayed the previous record's free text (neither of the
        //     other branches fires for it: OtherValue is empty and Other isn't selected).
        // Residual, and not detectable from here: two records carrying the SAME enum value and the
        // same empty OtherValue are indistinguishable from a switch-away, so the cache survives that
        // swap. EditRadioString has the identical blind spot for the identical reason.
        if (!string.IsNullOrEmpty(OtherValue))
            _otherTextCache = OtherValue;
        else if (IsOtherSelected || externalChange)
            _otherTextCache = null;
    }

    // The last "Other" text this control saw -- typed into the box or supplied on the model. Kept
    // through a switch away from the Other option so the box can go on displaying it (and re-commit it
    // if the user comes back), while the MODEL is cleared: see OnSelectionChangedAsync.
    string? _otherTextCache;

    // The bound pair (Value + OtherValue) as this control last observed it OR itself wrote it -- the
    // baseline OnParametersSet's external-change test compares against. Both self-write sites update
    // these at the point of writing, so the two can only differ here when something else moved them.
    TEnum? _observedValue;
    string? _observedOtherValue;

    // The SECOND bound field -- see OtherValueExpression. All three stay at their defaults (and every
    // path below is a no-op) when the consumer wired OtherValue/OtherValueChanged by hand.
    FieldIdentifier _otherFieldIdentifier;
    bool _otherFieldBound;
    string _otherFieldId = string.Empty;

    // Keeps the Other box's FormOptions registration pointing at the element id it actually renders
    // under, across a runtime Id/IdPrefix/group-name change -- the same job SyncResolvedId does for the
    // control's own field, and the same "only when it changed" guard, since the answer is stable on the
    // overwhelmingly common parameter cycle. RegisterField treats a repeat call from the same owner as
    // "this field's id moved" and updates FieldIds in place.
    void SyncOtherFieldRegistration()
    {
        if (!_otherFieldBound) return;
        var id = $"other-{_id}";
        if (string.Equals(id, _otherFieldId, StringComparison.Ordinal)) return;
        _otherFieldId = id;
        EditControlInit.RegisterField(FormOptions, _otherFieldIdentifier, id, this);
    }

    /// <summary>
    /// Tells the <see cref="EditContext"/> that the OtherValue property just changed. Called AFTER
    /// <see cref="OtherValueChanged"/> has run, never before: the validator reads the property live off
    /// the model during <c>NotifyFieldChanged</c>, so notifying first would validate the stale
    /// (pre-write) value and leave the error state one interaction behind — the same ordering
    /// <c>EditControlListBase.SetValueAsync</c> documents.
    /// </summary>
    void NotifyOtherChanged()
    {
        if (_otherFieldBound)
            EditContext?.NotifyFieldChanged(_otherFieldIdentifier);
    }

    /// <summary>
    /// Drops the Other field's registration alongside the base's own — <see cref="FormOptions"/> is
    /// per-form and long-lived, so an unpaired second registration leaves a dead
    /// <see cref="FieldIdentifier"/> for <see cref="ValidationView"/> to link to and grows on every
    /// mount/unmount cycle. No-op when the pair was wired by hand (nothing was ever registered).
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _otherFieldBound)
            EditControlInit.UnregisterField(FormOptions, _otherFieldIdentifier, this);
        base.Dispose(disposing);
    }

    /// <summary>
    /// The text the "Other" box renders. While Other is selected that is simply
    /// <see cref="OtherValue"/> (the live model value); while it is not, the box is disabled and shows
    /// the preserved text, which the model no longer carries.
    /// </summary>
    string? DisplayedOtherValue => IsOtherSelected ? OtherValue : (_otherTextCache ?? OtherValue);

    /// <summary>
    /// The radio group's commit path. Beyond writing the selected enum value it keeps the separate
    /// <see cref="OtherValue"/> model property honest: an Other text must never be submitted attached
    /// to a non-Other choice (this control used to leave it there forever), but it also must not be
    /// destroyed by a mis-click (<see cref="EditRadioString"/> used to wipe its equivalent) -- so the
    /// text is preserved in <c>_otherTextCache</c>, cleared from the model on the way out, and
    /// re-committed if the user selects Other again.
    /// </summary>
    async Task OnSelectionChangedAsync(TEnum? value)
    {
        var wasOther = IsOtherSelected;
        CurrentValue = value;
        // Record what we just wrote, so the parent's echo of it isn't mistaken for an external change
        // in OnParametersSet (which would drop the very text the branches below are preserving).
        _observedValue = value;
        if (!HasOtherOption)
            return;

        var isOther = IsOtherSelected;
        if (wasOther && !isOther)
        {
            if (!string.IsNullOrEmpty(OtherValue))
                _otherTextCache = OtherValue;
            if (!string.IsNullOrEmpty(_otherTextCache))
            {
                _observedOtherValue = null;
                await OtherValueChanged.InvokeAsync(null);
                // These two branches write the OtherValue property just as surely as typing does --
                // the switch away CLEARS it off the model, the switch back re-commits it -- so both
                // notify. Leaving them silent would have reopened the same gap from the other side:
                // an auto-save would persist the enum change while missing the free text going away.
                NotifyOtherChanged();
            }
        }
        else if (!wasOther && isOther && string.IsNullOrEmpty(OtherValue) && !string.IsNullOrEmpty(_otherTextCache))
        {
            _observedOtherValue = _otherTextCache;
            await OtherValueChanged.InvokeAsync(_otherTextCache);
            NotifyOtherChanged();
        }
    }

    // Read-only "Other" detection; an empty enum (no options) can't have an Other selection —
    // GetOptions().Last() threw on it.
    bool IsOtherSelected => HasOtherOption && _cache.Options is { Count: > 0 } && Value?.Equals(_cache.Options[^1]) == true;

    List<TEnum?> GetOptions() => _cache.Options;

    // GetOptions() carries TEnum? (the cache's nullable-element view) even though every entry is a
    // real enum value -- the "is TEnum" pattern unwraps that safely for the Func<TEnum, bool>?
    // predicate's exact signature. (EditCheckedEnumList needs no unwrap: it reads the cache's
    // OptionsNonNullable view instead.) A null option (shouldn't occur in practice) is simply never
    // disabled by the predicate.
    bool IsOptionDisabledFor(TEnum? option) =>
        IsDisabled || (option is TEnum concrete && IsOptionDisabled?.Invoke(concrete) == true);

    // RAD-2: the whole-group IsDisabled always natively disables every option, selected or not (that
    // strands the WHOLE group out of the Tab sequence, same as any other disabled control -- expected,
    // not a hazard). Only the per-option predicate exempts the currently-selected option from native
    // `disabled` -- see the IsOptionDisabled remarks. Split from IsOptionDisabledFor above (which
    // stays the plain logical answer -- the Other free-text box's own disabled wiring still reads that
    // one directly, unaffected by this split) purely so the markup can pick "native disabled" vs.
    // "aria-disabled only" per option without recomputing the same predicate twice.
    bool IsOptionNativelyDisabledFor(TEnum? option) =>
        IsDisabled || (option is TEnum concrete && IsOptionDisabled?.Invoke(concrete) == true &&
                        !EqualityComparer<TEnum?>.Default.Equals(CurrentValue, option));

    // The complement: logically disabled by the predicate (NOT the whole group) AND selected --
    // rendered as aria-disabled so assistive tech (and any future CSS hook) still sees "locked"
    // without the native attribute stripping the group's one Tab stop.
    bool IsOptionLockedFor(TEnum? option) =>
        !IsDisabled && option is TEnum concrete && IsOptionDisabled?.Invoke(concrete) == true &&
        EqualityComparer<TEnum?>.Default.Equals(CurrentValue, option);

    // No IsValueDefault override here (matching EditSelectEnum): the base's
    // EqualityComparer<TValue>.Default.Equals(CurrentValue, default) already answers "is this the
    // zero-valued enum?" for a non-nullable TEnum and "is this null?" for a nullable one -- the two
    // cases this control's former hand-written override spelled out.
    protected override bool TryParseValueFromString(string? value, out TEnum? result, out string validationErrorMessage) =>
        SelectParsing.TryParseEnum(value, _cache.UnderlyingType, _cache.IsNullable, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    /// <inheritdoc/>
    protected override async Task OnOtherTextCommitted(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        _otherTextCache = value;
        if (OtherValue != value)
        {
            // Same self-write bookkeeping as OnSelectionChangedAsync: the parent echoing this text
            // back is not an external change.
            _observedOtherValue = value;
            await OtherValueChanged.InvokeAsync(value);
            // The gap this closes: typing here used to write the model and raise NOTHING. Guarded by
            // the same `OtherValue != value` test as the write itself, so re-committing identical text
            // stays silent -- matching every other control's dedup.
            NotifyOtherChanged();
        }
    }
}
