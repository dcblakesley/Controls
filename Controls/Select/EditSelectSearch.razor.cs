namespace Controls;

/// <summary>
/// Searchable single-select form control backed by the <see cref="Select{TValue}"/> engine.
/// Adds form binding, validation, label, read-only view, and <see cref="FormOptions"/> support
/// on top of the AntDesign-style dropdown (type-to-search, clear, keyboard nav, virtualized list).
/// For a plain native <c>&lt;select&gt;</c> use <see cref="EditSelect{TValue}"/> instead.
/// </summary>
public partial class EditSelectSearch<TValue> : EditControlBase<TValue>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }

    /// <inheritdoc cref="Select{TValue}.Options"/>
    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = Array.Empty<SelectOption<TValue>>();

    /// <summary>
    /// Placeholder text shown when nothing is selected. Left <c>null</c> (no initializer — deliberately,
    /// so "unset" is distinguishable from an explicit empty string) so <see cref="EffectivePlaceholder"/>
    /// can fall through to the model's <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> attribute; null means
    /// "resolve from the model, else 'Please select'".
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// Resolved placeholder actually forwarded to the inner <see cref="Select{TValue}"/>: the
    /// <see cref="Placeholder"/> parameter (consumer set it explicitly) → the model property's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> attribute → the literal "Please select" fallback.
    /// The literal default lives here rather than on the parameter itself, because a defaulted
    /// non-null parameter can't be told apart from a consumer-set value — see <see cref="Placeholder"/>.
    /// <see cref="Select{TValue}.Placeholder"/> is non-nullable, so this must never be null.
    /// </summary>
    string EffectivePlaceholder => Placeholder ?? _attributes.Placeholder() ?? "Please select";

    /// <inheritdoc cref="Select{TValue}.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.ShowSearch"/>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.ShowArrow"/>
    [Parameter] public bool ShowArrow { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.Loading"/>
    [Parameter] public bool Loading { get; set; }

    /// <inheritdoc cref="Select{TValue}.Size"/>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <inheritdoc cref="Select{TValue}.Variant"/>
    [Parameter] public SelectVariant Variant { get; set; } = SelectVariant.Outlined;

    /// <inheritdoc cref="Select{TValue}.Prefix"/>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <inheritdoc cref="Select{TValue}.Width"/>
    [Parameter] public string? Width { get; set; }

    /// <inheritdoc cref="Select{TValue}.EmptyText"/>
    [Parameter] public string EmptyText { get; set; } = "No data";

    /// <inheritdoc cref="Select{TValue}.EmptyContent"/>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <inheritdoc cref="Select{TValue}.FilterOption"/>
    [Parameter] public Func<string, SelectOption<TValue>, bool>? FilterOption { get; set; }

    /// <inheritdoc cref="Select{TValue}.DropdownFooter"/>
    [Parameter] public RenderFragment? DropdownFooter { get; set; }

    /// <inheritdoc cref="Select{TValue}.DebounceMilliseconds"/>
    [Parameter] public int DebounceMilliseconds { get; set; }

    /// <inheritdoc cref="Select{TValue}.OnSearch"/>
    [Parameter] public EventCallback<string> OnSearch { get; set; }

    /// <inheritdoc cref="Select{TValue}.DefaultOpen"/>
    [Parameter] public bool DefaultOpen { get; set; }

    /// <inheritdoc cref="Select{TValue}.Open"/>
    [Parameter] public bool Open { get; set; }

    /// <inheritdoc cref="Select{TValue}.OpenChanged"/>
    [Parameter] public EventCallback<bool> OpenChanged { get; set; }

    /// <inheritdoc cref="Select{TValue}.ClearSelectionLabel"/>
    [Parameter] public string ClearSelectionLabel { get; set; } = "Clear selection";

    /// <inheritdoc cref="Select{TValue}.ListboxLabel"/>
    [Parameter] public string ListboxLabel { get; set; } = "Options";

    // Label for the read-only view: the matching option's label, else the value's own ToString.
    // Cached and recomputed only when the value or Options change, not on every render. The lookup
    // (rebuilt only when the Options reference changes) replaces a per-render FirstOrDefault scan and
    // agrees with the Select engine's own last-wins tie-break for a duplicate-valued option list --
    // see SelectOptionLookup.
    string _selectedLabel = "";
    TValue? _labelValue;
    IEnumerable<SelectOption<TValue>>? _labelOptions;
    bool _labelInit;
#pragma warning disable CS8714 // TValue stays unconstrained; SelectOptionLookup never inserts a null key.
    Dictionary<TValue, SelectOption<TValue>> _lookup = SelectOptionLookup.Build<TValue>(null);
#pragma warning restore CS8714

    string SelectedLabel => _selectedLabel;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_labelInit
            && ReferenceEquals(Options, _labelOptions)
            && EqualityComparer<TValue>.Default.Equals(CurrentValue, _labelValue))
            return;
        _labelInit = true;
        if (!ReferenceEquals(Options, _labelOptions))
            _lookup = SelectOptionLookup.Build(Options);
        _labelOptions = Options;
        _labelValue = CurrentValue;
        _selectedLabel =
            (CurrentValue is not null && _lookup.TryGetValue(CurrentValue, out var option) ? option.Label : null)
            ?? CurrentValue?.ToString()
            ?? string.Empty;
    }

    // Setting CurrentValue runs the InputBase machinery: NotifyFieldChanged + validation + ValueChanged.
    void OnValueChanged(TValue value) => CurrentValue = value;

    // The engine sets the value through OnValueChanged (an EventCallback), not via string
    // parsing — mirrors EditBool. Binding to CurrentValueAsString is unsupported.
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
        => throw new NotSupportedException(
            "EditSelectSearch does not parse string input; it binds via the Select value callback.");
}
