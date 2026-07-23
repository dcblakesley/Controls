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

    /// <summary> The options to choose from.</summary>
    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = Array.Empty<SelectOption<TValue>>();

    /// <summary> Placeholder text shown when nothing is selected.</summary>
    [Parameter] public string Placeholder { get; set; } = "Please select";

    /// <summary> Show the clear (x) button when a value is selected. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary> Allow type-to-search filtering. Defaults to true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.ShowArrow"/>
    [Parameter] public bool ShowArrow { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.Loading"/>
    [Parameter] public bool Loading { get; set; }

    /// <summary> Visual size (small / default / large).</summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <inheritdoc cref="Select{TValue}.Variant"/>
    [Parameter] public SelectVariant Variant { get; set; } = SelectVariant.Outlined;

    /// <inheritdoc cref="Select{TValue}.Prefix"/>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional CSS width (e.g. "240px", "100%").</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary> Text shown when no options match. Defaults to "No data".</summary>
    [Parameter] public string EmptyText { get; set; } = "No data";

    /// <inheritdoc cref="Select{TValue}.EmptyContent"/>
    [Parameter] public RenderFragment? EmptyContent { get; set; }

    /// <inheritdoc cref="Select{TValue}.FilterOption"/>
    [Parameter] public Func<string, SelectOption<TValue>, bool>? FilterOption { get; set; }

    /// <inheritdoc cref="Select{TValue}.DropdownFooter"/>
    [Parameter] public RenderFragment? DropdownFooter { get; set; }

    /// <summary> Debounce (ms) before filtering after a keystroke; 0 = immediate.</summary>
    [Parameter] public int DebounceMilliseconds { get; set; }

    /// <summary> Raised with the current search text whenever it changes.</summary>
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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditSelectSearch<TValue>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Label for the read-only view: the matching option's label, else the value's own ToString.
    // Cached and recomputed only when the value or Options change, not on every render.
    string _selectedLabel = "";
    TValue? _labelValue;
    IEnumerable<SelectOption<TValue>>? _labelOptions;
    bool _labelInit;

    string SelectedLabel => _selectedLabel;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_labelInit
            && ReferenceEquals(Options, _labelOptions)
            && EqualityComparer<TValue>.Default.Equals(CurrentValue, _labelValue))
            return;
        _labelInit = true;
        _labelOptions = Options;
        _labelValue = CurrentValue;
        _selectedLabel =
            Options?.FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, CurrentValue))?.Label
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
