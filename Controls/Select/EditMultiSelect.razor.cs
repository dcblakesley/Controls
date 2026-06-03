namespace Controls;

/// <summary>
/// Multiple / tags select form control backed by the <see cref="Select{TValue}"/> engine.
/// Binds to a <see cref="List{T}"/> and adds validation, label, read-only view and
/// <see cref="FormOptions"/> support on top of the AntDesign-style dropdown. Use
/// <see cref="SelectMode.Tags"/> to let users add values that are not in <see cref="Options"/>.
/// </summary>
public partial class EditMultiSelect<TValue> : EditControlListBase<TValue>
{
    /// <summary> Expression that binds to the list property in the model.</summary>
    [Parameter] public required Expression<Func<List<TValue>>> Field { get; set; }

    /// <summary> The options to choose from.</summary>
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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }

    // The list base isn't an InputBase, so derive the validation state from the EditContext.
    bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(_fieldIdentifier).Any();
    string FieldCssClass => IsInvalid ? "invalid" : string.Empty;

    // Read-only view: comma-joined option labels (or the value's ToString when unmatched).
    string SelectedLabels =>
        string.Join(", ", (Value ?? new List<TValue>())
            .Select(v => Options?.FirstOrDefault(o => EqualityComparer<TValue>.Default.Equals(o.Value, v))?.Label
                         ?? v?.ToString()
                         ?? string.Empty));

    async Task OnValuesChanged(IEnumerable<TValue> values)
    {
        Value = values.ToList();
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
        await ValueChanged.InvokeAsync(Value);
    }
}
