namespace Controls;

/// <summary>
/// Multiple / tags select form control backed by the <see cref="Select{TValue}"/> engine.
/// Binds to a <see cref="List{T}"/> and adds validation, label, read-only view and
/// <see cref="FormOptions"/> support on top of the AntDesign-style dropdown. Use
/// <see cref="SelectMode.Tags"/> to let users add values that are not in <see cref="Options"/>.
/// </summary>
public partial class EditMultiSelect<TValue> : EditControlListBase<TValue>
{
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

    /// <summary> Placeholder text shown when nothing is selected.</summary>
    [Parameter] public string Placeholder { get; set; } = "Please select";

    /// <summary> Show the clear (x) button when there is a selection. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary> Allow type-to-search filtering. Defaults to true.</summary>
    [Parameter] public bool ShowSearch { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.ShowArrow"/>
    [Parameter] public bool ShowArrow { get; set; } = true;

    /// <inheritdoc cref="Select{TValue}.Loading"/>
    [Parameter] public bool Loading { get; set; }

    /// <summary> Visual size (small / default / large).</summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

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

    /// <inheritdoc cref="Select{TValue}.RemoveItemLabelFormat"/>
    [Parameter] public string RemoveItemLabelFormat { get; set; } = "Remove {0}";

    /// <inheritdoc cref="Select{TValue}.ClearSelectionsLabel"/>
    [Parameter] public string ClearSelectionsLabel { get; set; } = "Clear all selections";

    /// <inheritdoc cref="Select{TValue}.ListboxLabel"/>
    [Parameter] public string ListboxLabel { get; set; } = "Options";

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditMultiSelect<TValue>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Read-only view: comma-joined option labels (or the value's ToString when unmatched). Cached
    // and recomputed only when the bound list or Options change by reference — the editable engine
    // builds its own O(1) lookup, so without this the read-only path would re-scan Options for every
    // selected value on every render.
    string _selectedLabels = "";
    List<TValue>? _labelValue;
    IEnumerable<SelectOption<TValue>>? _labelOptions;

    // value -> option, rebuilt only when the Options *reference* changes -- see SelectOptionLookup
    // for the last-wins tie-break on a duplicate value (matching the Select engine's own lookup).
#pragma warning disable CS8714 // TValue stays unconstrained; SelectOptionLookup never inserts a null key.
    Dictionary<TValue, SelectOption<TValue>> _lookup = SelectOptionLookup.Build<TValue>(null);
#pragma warning restore CS8714

    string SelectedLabels => _selectedLabels;

    protected override void OnParametersSet()
    {
        // Single mode can't work here: this wrapper binds only Values/ValuesChanged, so the
        // engine's ValueChanged would fire into the void — every selection silently reverting.
        // Fail loudly instead.
        if (Mode == SelectMode.Single)
            throw new InvalidOperationException(
                $"{GetType().Name} binds a List<TValue> and supports SelectMode.Multiple or SelectMode.Tags — use EditSelectSearch for single selection.");

        base.OnParametersSet();
        if (ReferenceEquals(Value, _labelValue) && ReferenceEquals(Options, _labelOptions)) return;
        if (!ReferenceEquals(Options, _labelOptions))
            _lookup = SelectOptionLookup.Build(Options);
        _labelValue = Value;
        _labelOptions = Options;
        _selectedLabels = string.Join(", ", (Value ?? new List<TValue>())
            .Select(v => v is not null && _lookup.TryGetValue(v, out var option)
                ? option.Label ?? v.ToString() ?? string.Empty
                : v?.ToString() ?? string.Empty));
    }

    async Task OnValuesChanged(IEnumerable<TValue> values)
    {
        Value = values.ToList();
        // Write back to the model (ValueChanged) BEFORE notifying — the validator reads the property
        // live off the model during NotifyFieldChanged, so notifying first validates the stale value.
        await ValueChanged.InvokeAsync(Value);
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
    }
}
