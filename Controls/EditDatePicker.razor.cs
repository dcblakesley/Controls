namespace Controls;

/// <summary>
/// Edit control for a single date, backed by the <see cref="DatePicker"/> UI-kit calendar dropdown.
/// Adds form binding, validation, label, read-only view, and <see cref="FormOptions"/> support (the
/// same contract every other scalar control provides) on top of DatePicker's type-or-pick UX. For a
/// native <c>&lt;input type="date"&gt;</c> use <see cref="EditDate{T}"/> instead.
/// </summary>
/// <remarks>
/// <para>
/// Validation-state ARIA reaches the picker's actual <c>&lt;input&gt;</c> through
/// <see cref="DatePicker"/>'s <c>AriaRequired</c>/<c>AriaInvalid</c>/<c>AriaDescribedBy</c>/
/// <c>AriaErrorMessage</c> parameters — the same forwarding shape as
/// <see cref="EditSelectSearch{TValue}"/> onto <see cref="Select{TValue}"/>. The consumer's own
/// unmatched attributes still land on the picker's outer <c>.wss-picker</c> wrapper (its documented
/// <c>AdditionalAttributes</c> target), which also carries the EditContext state classes via
/// <c>CssClass</c>.
/// </para>
/// </remarks>
public partial class EditDatePicker : EditControlBase<DateTime?>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<DateTime?>>? Field { get; set; }

    /// <inheritdoc cref="DatePicker.Min"/>
    [Parameter] public DateTime? Min { get; set; }
    /// <inheritdoc cref="DatePicker.Max"/>
    [Parameter] public DateTime? Max { get; set; }
    /// <inheritdoc cref="DatePicker.Format"/>
    [Parameter] public string Format { get; set; } = "MM/dd/yyyy";
    /// <inheritdoc cref="DatePicker.Placeholder"/>
    [Parameter] public string Placeholder { get; set; } = "Select date";
    /// <inheritdoc cref="DatePicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <inheritdoc cref="DatePicker.Width"/>
    [Parameter] public string? Width { get; set; }
    /// <inheritdoc cref="DatePicker.FirstDayOfWeek"/>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary> Format string for the read-only value display. Defaults to "MM-dd-yyyy" (matches <see cref="EditDate{T}"/>'s default).</summary>
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    // Localizable accessibility strings, forwarded to the inner DatePicker. Defaults mirror
    // DatePicker's own literal defaults except InputLabel (see EffectiveInputLabel below).

    /// <summary>
    /// Accessible name of the picker's input. Null (default) uses the resolved field label — the
    /// <see cref="IEditControl.Label"/> parameter, or the property's <c>[DisplayName]</c>/auto-generated
    /// text — so the input's accessible name matches its visible <see cref="FormLabel"/> instead of
    /// DatePicker's generic "Date" default (which would otherwise win the accessible-name computation
    /// over the <c>label[for]</c> association; see the class remarks). Override to set something else.
    /// </summary>
    [Parameter] public string? InputLabel { get; set; }
    /// <inheritdoc cref="DatePicker.DialogLabel"/>
    [Parameter] public string DialogLabel { get; set; } = "Choose date";
    /// <inheritdoc cref="DatePicker.MonthSelectLabel"/>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <inheritdoc cref="DatePicker.YearSelectLabel"/>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <inheritdoc cref="DatePicker.ClearLabel"/>
    [Parameter] public string ClearLabel { get; set; } = "Clear date";
    /// <inheritdoc cref="DatePicker.PrevMonthLabel"/>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <inheritdoc cref="DatePicker.NextMonthLabel"/>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";

    string EffectiveInputLabel => InputLabel ?? _attributes.GetLabelText(_fieldIdentifier);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDatePicker)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // The picker sets the value through its own ValueChanged callback, not string parsing — mirrors
    // EditSelectSearch's contract for a wrapped UI-kit engine. Binding to CurrentValueAsString (the
    // debug bound-value display excepted, which only ever reads it) is unsupported.
    protected override bool TryParseValueFromString(string? value, out DateTime? result, out string validationErrorMessage)
        => throw new NotSupportedException(
            "EditDatePicker does not parse string input; it binds via the DatePicker value callback.");

    // Setting CurrentValue runs the InputBase machinery: NotifyFieldChanged + validation + ValueChanged.
    void OnValueChanged(DateTime? value) => CurrentValue = value;

    // The validation-state ARIA goes through DatePicker's dedicated Aria* parameters (straight onto
    // its actual <input>); this splat carries only the consumer's own attributes plus the state
    // classes, landing on the picker's outer wrapper (its documented AdditionalAttributes target).
    IReadOnlyDictionary<string, object> PickerAttributes
    {
        get
        {
            var attrs = new Dictionary<string, object>();
            if (AdditionalAttributes is not null)
                foreach (var kv in AdditionalAttributes) attrs[kv.Key] = kv.Value;
            // Overwrite the raw consumer "class" (if any) with CssClass — InputBase's own merge of
            // that same raw class with the EditContext's modified/valid/invalid classes — so the
            // wrapper picks up validation-state styling hooks the same way every other control's
            // native input does via `class="edit-input ... @CssClass"`.
            if (!string.IsNullOrEmpty(CssClass)) attrs["class"] = CssClass;
            return attrs;
        }
    }

    string GetDisplayValue()
    {
        if (CurrentValue is not { } value) return string.Empty;
        try
        {
            return value.ToString(DateFormat, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString(CultureInfo.CurrentCulture);
        }
    }
}
