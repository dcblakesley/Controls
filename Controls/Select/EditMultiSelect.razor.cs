namespace Controls;

/// <summary>
/// Multiple / tags select form control backed by the <see cref="Select{TValue}"/> engine.
/// Binds to a <see cref="List{T}"/> and adds validation, label, read-only view and
/// <see cref="FormOptions"/> support on top of the AntDesign-style dropdown. Use
/// <see cref="SelectMode.Tags"/> to let users add values that are not in <see cref="Options"/>.
/// </summary>
/// <remarks>
/// The engine pass-through parameters below are declared again on <see cref="EditSelectSearch{TValue}"/>
/// and can't be hoisted to a shared base — see <see cref="SelectDefaults"/>, which holds everything
/// about them that CAN be shared (each one's default value, and the placeholder resolution chain).
/// </remarks>
public partial class EditMultiSelect<TValue> : EditControlListBase<TValue>
{
    // The engine instance, captured so FocusAsync can forward to it. Null in read-only mode and before
    // first render, which FocusAsync below reads as "nothing to focus".
    Select<TValue>? _select;

    /// <inheritdoc cref="EditControlListBase{TItem}.FocusAsync"/>
    /// <remarks>
    /// Forwards to the <see cref="Select{TValue}"/> engine's own <see cref="Select{TValue}.FocusAsync"/>,
    /// which focuses its <c>role="combobox"</c> search input — the widget's single tab stop, in tags
    /// mode too (the tags themselves are not tab stops; only their × buttons are, and those are not
    /// where entering the control should land).
    /// </remarks>
    public override ValueTask FocusAsync() => _select?.FocusAsync() ?? ValueTask.CompletedTask;

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<List<TValue>>>? Field { get; set; }

    /// <inheritdoc cref="Select{TValue}.Options"/>
    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = Array.Empty<SelectOption<TValue>>();

    /// <summary> Multiple (pick from options) or Tags (also allow typed values). Defaults to Multiple.</summary>
    [Parameter] public SelectMode Mode { get; set; } = SelectMode.Multiple;

    /// <summary> Show at most this many tags before collapsing the rest into "+N ...".</summary>
    [Parameter] public int? MaxTagCount { get; set; }

    /// <summary> Tags mode: turn typed text into a TValue. When null and TValue is string, the text is used directly.</summary>
    [Parameter] public Func<string, TValue>? TagValueFactory { get; set; }

    /// <inheritdoc cref="EditSelectSearch{TValue}.Placeholder"/>
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

    /// <inheritdoc cref="Select{TValue}.RemoveItemLabelFormat"/>
    [Parameter] public string RemoveItemLabelFormat { get; set; } = SelectDefaults.RemoveItemLabelFormat;

    /// <inheritdoc cref="Select{TValue}.ClearSelectionsLabel"/>
    [Parameter] public string ClearSelectionsLabel { get; set; } = SelectDefaults.ClearSelectionsLabel;

    /// <inheritdoc cref="Select{TValue}.ListboxLabel"/>
    [Parameter] public string ListboxLabel { get; set; } = SelectDefaults.ListboxLabel;

    /// <inheritdoc cref="Select{TValue}.ResultCountAnnouncementFormat"/>
    [Parameter] public string ResultCountAnnouncementFormat { get; set; } = SelectDefaults.ResultCountAnnouncementFormat;

    /// <inheritdoc cref="Select{TValue}.SelectedAnnouncementFormat"/>
    [Parameter] public string SelectedAnnouncementFormat { get; set; } = SelectDefaults.SelectedAnnouncementFormat;

    /// <inheritdoc cref="Select{TValue}.DeselectedAnnouncementFormat"/>
    [Parameter] public string DeselectedAnnouncementFormat { get; set; } = SelectDefaults.DeselectedAnnouncementFormat;

    /// <inheritdoc cref="Select{TValue}.SelectionClearedAnnouncement"/>
    [Parameter] public string SelectionClearedAnnouncement { get; set; } = SelectDefaults.SelectionClearedAnnouncement;

    /// <inheritdoc cref="Select{TValue}.LoadingAnnouncement"/>
    [Parameter] public string LoadingAnnouncement { get; set; } = SelectDefaults.LoadingAnnouncement;

    /// <inheritdoc cref="Select{TValue}.MaxTagCountLabelFormat"/>
    [Parameter] public string MaxTagCountLabelFormat { get; set; } = SelectDefaults.MaxTagCountLabelFormat;

    // Read-only view: comma-joined option labels (or the value's ToString when unmatched), resolved by
    // the same shared SelectLabelCache EditSelectSearch uses -- it caches both the value -> option
    // lookup (per Options reference) and the joined text (per bound-list reference), so the read-only
    // path never re-scans Options per selected value per render.
    readonly SelectLabelCache<TValue> _labels = new();

    string SelectedLabels => _labels.JoinedLabels(Value);

    protected override void OnParametersSet()
    {
        // Single mode can't work here: this wrapper binds only Values/ValuesChanged, so the
        // engine's ValueChanged would fire into the void — every selection silently reverting.
        // Fail loudly instead.
        if (Mode == SelectMode.Single)
            throw new InvalidOperationException(
                $"{GetType().Name} binds a List<TValue> and supports SelectMode.Multiple or SelectMode.Tags — use EditSelectSearch for single selection.");

        base.OnParametersSet();

        // Options only -- deliberately NOT the engine's effective option set, which also carries the
        // tags a user typed in SelectMode.Tags. Narrow known consequence: with a non-string TValue plus
        // a TagValueFactory, a user-created tag shows its typed text in the editable view but
        // value.ToString() here. Closing that gap needs the engine to surface its tag options (it keeps
        // them private, and this wrapper only ever sees the resulting values come back through
        // ValuesChanged -- never the text they were created from), so it stays as-is. For TValue ==
        // string, tag text and tag value coincide and the two views already agree.
        _labels.Refresh(Options);
    }

    Task OnValuesChanged(IEnumerable<TValue> values) => SetValueAsync(values.ToList());
}
