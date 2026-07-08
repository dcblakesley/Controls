namespace Controls;

/// <summary>
/// Searchable single-select form control backed by the <see cref="Select{TValue}"/> engine.
/// Adds form binding, validation, label, read-only view, and <see cref="FormOptions"/> support
/// on top of the AntDesign-style dropdown (type-to-search, clear, keyboard nav, virtualized list).
/// For a plain native <c>&lt;select&gt;</c> use <see cref="EditSelect{TValue}"/> instead.
/// </summary>
public partial class EditSelectSearch<TValue> : EditControlBase<TValue>
{
    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    /// <summary> The options to choose from.</summary>
    [Parameter] public IEnumerable<SelectOption<TValue>> Options { get; set; } = Array.Empty<SelectOption<TValue>>();

    /// <summary> Placeholder text shown when nothing is selected.</summary>
    [Parameter] public string Placeholder { get; set; } = "Please select";

    /// <summary> Show the clear (x) button when a value is selected. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary> Allow type-to-search filtering. Defaults to true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <summary> Visual size (small / default / large).</summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <summary> Optional CSS width (e.g. "240px", "100%").</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary> Text shown when no options match. Defaults to "No data".</summary>
    [Parameter] public string EmptyText { get; set; } = "No data";

    /// <summary> Debounce (ms) before filtering after a keystroke; 0 = immediate.</summary>
    [Parameter] public int DebounceMilliseconds { get; set; }

    /// <summary> Raised with the current search text whenever it changes.</summary>
    [Parameter] public EventCallback<string> OnSearch { get; set; }

    /// <inheritdoc cref="Select{TValue}.ClearSelectionLabel"/>
    [Parameter] public string ClearSelectionLabel { get; set; } = "Clear selection";

    /// <inheritdoc cref="Select{TValue}.ListboxLabel"/>
    [Parameter] public string ListboxLabel { get; set; } = "Options";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
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
