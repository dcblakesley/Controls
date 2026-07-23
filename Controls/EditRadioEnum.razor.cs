namespace Controls;

/// <summary> Edit control for selecting an enum value using radio buttons. Supports sorting and an optional "Other" option with text input.</summary>
// TEnum is annotated 'All' because the markup renders InputRadioGroup<TEnum?>/InputRadio<TEnum?>,
// whose TValue declares that requirement.
public partial class EditRadioEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TEnum> : EditControlBase<TEnum?>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TEnum>>? Field { get; set; }

    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary> The labels around each radio button</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each enum value being rendered (including
    /// the "Other" option's enum value when <see cref="HasOtherOption"/> is set). An option is
    /// disabled when this returns true OR the whole group's <c>IsDisabled</c> is true. Null
    /// (default) disables nothing beyond <c>IsDisabled</c>.
    /// </summary>
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

    Type _type = null!;
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum?>? _cachedOptions;
    bool _lastSort;
    bool _lastHasOther;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditRadioEnum<TEnum>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));

        // Handle nullable enum types
        _type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(_type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(_type)! : _type;
        _cachedOptions = BuildOptions();
        _lastSort = Sort;
        _lastHasOther = HasOtherOption;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // The option list is cached, but the parameters that shape it may change at runtime —
        // previously a Sort/HasOtherOption change was silently ignored forever.
        if (_cachedOptions is not null && (Sort != _lastSort || HasOtherOption != _lastHasOther))
        {
            _lastSort = Sort;
            _lastHasOther = HasOtherOption;
            _cachedOptions = BuildOptions();
        }
    }

    // Read-only "Other" detection; an empty enum (no options) can't have an Other selection —
    // GetOptions().Last() threw on it.
    bool IsOtherSelected => HasOtherOption && _cachedOptions is { Count: > 0 } && Value?.Equals(_cachedOptions[^1]) == true;

    List<TEnum?> GetOptions() => _cachedOptions!;

    // GetOptions() carries TEnum? (see BuildOptions' final Cast<TEnum?>()) even though every entry is
    // a real enum value -- the "is TEnum" pattern unwraps that safely for the Func<TEnum, bool>?
    // predicate's exact signature (matching EditCheckedEnumList's, whose options list has no such
    // wrapper). A null option (shouldn't occur in practice) is simply never disabled by the predicate.
    bool IsOptionDisabledFor(TEnum? option) =>
        IsDisabled || (option is TEnum concrete && IsOptionDisabled?.Invoke(concrete) == true);

    List<TEnum?> BuildOptions()
    {
        var enumValues = EnumHelpers.GetValues<TEnum>(_underlyingType);

        // If HasOtherOption is true, remove the last enum value to add it back later
        TEnum? otherOption = default;
        if (HasOtherOption && enumValues.Count > 0)
        {
            otherOption = enumValues.Last();
            enumValues.RemoveAt(enumValues.Count - 1);
        }

        // Sort by the same display name the UI shows so sort order matches what the user sees.
        // EnumHelpers.GetName caches its lookup, so this stays cheap on subsequent renders.
        if (Sort)
            enumValues = enumValues.OrderBy(x => x!.GetName()).ToList();

        // Add back the "other" option at the end if it exists
        if (HasOtherOption && otherOption != null)
            enumValues.Add(otherOption);

        return enumValues.Cast<TEnum?>().ToList();
    }

    protected override bool TryParseValueFromString(string? value, out TEnum? result, out string validationErrorMessage) =>
        SelectParsing.TryParseEnum(value, _underlyingType, _isNullable, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    async Task OnOtherValueChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (OtherValue != value)
            await OtherValueChanged.InvokeAsync(value);
    }

    // Class inherits EditControlBase<TEnum?>, so default(TValue) is null — but the prior
    // behavior also treated the zero-valued enum as default. Preserve that here. Centralization
    // additionally fixes a latent bug where WhenReadOnly* used bare IsEditMode and ignored
    // form-wide FormOptions.IsEditMode.
    protected override bool IsValueDefault() =>
        CurrentValue == null || CurrentValue.Equals(default(TEnum));
}
