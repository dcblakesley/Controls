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
    /// Splats <see cref="OnOtherValueChanged"/> onto whichever event name
    /// <see cref="RadioGroupControlBase{TValue}.UpdateEventName"/> resolves to. The "Other" text box
    /// uses a raw event handler rather than a <c>@bind</c>, so <c>@bind:event</c> doesn't apply here --
    /// this dictionary is the mechanism that makes the wired-up event name dynamic instead of a fixed
    /// <c>@oninput</c>. It is handed to the shared <see cref="RadioOtherInput"/> (which
    /// <see cref="EditRadioString"/> also renders) as its <c>CommitAttributes</c>, which is why the
    /// wiring travels as a dictionary rather than a callback.
    /// </summary>
    IReadOnlyDictionary<string, object> OtherInputAttribute => new Dictionary<string, object>(1)
    {
        [UpdateEventName] = EventCallback.Factory.Create<ChangeEventArgs>(this, OnOtherValueChanged)
    };

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
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // The option list is cached, but the parameters that shape it may change at runtime —
        // previously a Sort/HasOtherOption change was silently ignored forever.
        _cache.Refresh(Sort, HasOtherOption);
        // Runs after Refresh so the ids track a reordered/resized option list. Cheap: ToId memoizes.
        _optionIds = EnumHelpers.ToUniqueIds(_cache.Options);
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

    // No IsValueDefault override here (matching EditSelectEnum): the base's
    // EqualityComparer<TValue>.Default.Equals(CurrentValue, default) already answers "is this the
    // zero-valued enum?" for a non-nullable TEnum and "is this null?" for a nullable one -- the two
    // cases this control's former hand-written override spelled out.
    protected override bool TryParseValueFromString(string? value, out TEnum? result, out string validationErrorMessage) =>
        SelectParsing.TryParseEnum(value, _cache.UnderlyingType, _cache.IsNullable, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    async Task OnOtherValueChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (OtherValue != value)
            await OtherValueChanged.InvokeAsync(value);
    }
}
