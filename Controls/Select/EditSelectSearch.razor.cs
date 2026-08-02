namespace Controls;

/// <summary>
/// Searchable single-select form control backed by the <see cref="Select{TValue}"/> engine.
/// Adds form binding, validation, label, read-only view, and <see cref="FormOptions"/> support
/// on top of the AntDesign-style dropdown (type-to-search, clear, keyboard nav, virtualized list).
/// For a plain native <c>&lt;select&gt;</c> use <see cref="EditSelect{TValue}"/> instead.
/// </summary>
/// <remarks>
/// The engine pass-through parameters below are declared again on <see cref="EditMultiSelect{TValue}"/>
/// and can't be hoisted to a shared base — see <see cref="SelectDefaults"/>, which holds everything
/// about them that CAN be shared (each one's default value, and the placeholder resolution chain).
/// </remarks>
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
    /// so "unset" is distinguishable from an explicit empty string) so
    /// <see cref="SelectDefaults.ResolvePlaceholder"/> can fall through to the model's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> attribute; null means "resolve from the model, else
    /// 'Please select'".
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <inheritdoc cref="SelectDefaults.ResolvePlaceholder"/>
    string EffectivePlaceholder => SelectDefaults.ResolvePlaceholder(Placeholder, _attributes);

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
    [Parameter] public string EmptyText { get; set; } = SelectDefaults.EmptyText;

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
    [Parameter] public string ClearSelectionLabel { get; set; } = SelectDefaults.ClearSelectionLabel;

    /// <inheritdoc cref="Select{TValue}.ListboxLabel"/>
    [Parameter] public string ListboxLabel { get; set; } = SelectDefaults.ListboxLabel;

    // Label for the read-only view: the matching option's label, else the value's own ToString --
    // resolved (and cached at both levels: the value -> option lookup and the resolved text) by the
    // shared SelectLabelCache, which EditMultiSelect uses too. Read-only only; the editor's own label
    // comes from the Select engine's interleaved lookup, which agrees on the duplicate-value tie-break.
    readonly SelectLabelCache<TValue> _labels = new();

    string SelectedLabel => _labels.Label(CurrentValue);

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        _labels.Refresh(Options);
    }

    // Union of the base default check and the empty-string case, matching EditSelectBase (which
    // EditSelect/EditSelectString share): default(string) is null, not "", so a string-bound
    // EditSelectSearch at the empty string stayed visible under
    // WhenNullOrDefault/WhenReadOnlyAndNullOrDefault while every sibling string control hid it —
    // contradicting HidingMode's documented "null or its type's default (e.g. empty string, 0, ...)".
    // Unioned rather than replacing the base check, so every other TValue keeps its own default (a
    // nullable enum at null, an int at 0), which stringifying would have silently broken.
    // Restated here rather than inherited: EditSelectBase's other two overrides don't apply (this
    // control binds through a value callback, not string parsing) and its TValue carries a
    // [DynamicallyAccessedMembers] annotation this control's public TValue deliberately does not.
    protected override bool IsValueDefault() => base.IsValueDefault() || CurrentValue is string { Length: 0 };

    // Setting CurrentValue runs the InputBase machinery: NotifyFieldChanged + validation + ValueChanged.
    void OnValueChanged(TValue value) => CurrentValue = value;

    // The engine sets the value through OnValueChanged (an EventCallback), not via string
    // parsing — mirrors EditBool. Binding to CurrentValueAsString is unsupported.
    protected override bool TryParseValueFromString(string? value, out TValue result, out string validationErrorMessage)
        => throw new NotSupportedException(
            "EditSelectSearch does not parse string input; it binds via the Select value callback.");
}
