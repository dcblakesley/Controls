namespace Controls;

/// <summary>
/// Edit control for a single date, backed by the <see cref="DatePicker"/> UI-kit calendar dropdown.
/// Adds form binding, validation, label, read-only view, and <see cref="FormOptions"/> support (the
/// same contract every other scalar control provides) on top of DatePicker's type-or-pick UX. For a
/// native <c>&lt;input type="date"&gt;</c> (or a <c>Time</c>/<c>DateTimeLocal</c>/<c>Month</c> input,
/// or a <c>TimeOnly</c>/<c>DateTimeOffset</c> binding) use <see cref="EditDate{T}"/> instead — the
/// underlying <see cref="DatePicker"/> is a date-only (midnight local) value, so <typeparamref name="T"/>
/// is limited to <c>DateTime</c>, <c>DateTime?</c>, <c>DateOnly</c>, and <c>DateOnly?</c>.
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
/// <para>
/// <see cref="Min"/>/<see cref="Max"/> stay <c>DateTime?</c> regardless of <typeparamref name="T"/> —
/// only the bound value generalizes. A <c>DateOnly</c>-bound instance still sets them with a
/// <c>DateTime</c> (e.g. <c>Min="@d.ToDateTime(TimeOnly.MinValue)"</c>).
/// </para>
/// </remarks>
public partial class EditDatePicker<T> : EditControlBase<T>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<T>>? Field { get; set; }

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

    string EffectiveInputLabel => InputLabel ?? Label ?? _attributes.GetLabelText(_fieldIdentifier);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDatePicker<T>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // The picker sets the value through its own ValueChanged callback, not string parsing — mirrors
    // EditSelectSearch's contract for a wrapped UI-kit engine. Binding to CurrentValueAsString (the
    // debug bound-value display excepted, which only ever reads it) is unsupported.
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"{nameof(EditDatePicker<T>)} does not parse string input; it binds via the DatePicker value callback.");

    // The inner <DatePicker> is DateTime?-only (a date-only midnight value) regardless of T -- these
    // bridge CurrentValue to and from it. Boxing+pattern-match on the runtime type (not typeof(T) ==
    // checks) mirrors EditDate<T>'s GetDisplayValue/IsValueDefault, which already rely on the CLR
    // boxing a non-null Nullable<T> as its underlying T (so "DateTime dt" matches DateTime? too).
    DateTime? PickerValue => CurrentValue switch
    {
        null => null,
        DateTime dt => dt,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        _ => throw UnsupportedType()
    };

    // The reverse direction can't pattern-match on the incoming value (it's always DateTime?) --
    // typeof(T) picks which of the four supported shapes to produce. typeof(T) == checks over a
    // handful of concrete value types are fully trim/AOT-safe (no reflection over T's members).
    void OnValueChanged(DateTime? value) => CurrentValue = FromPickerValue(value);

    static T FromPickerValue(DateTime? value)
    {
        if (typeof(T) == typeof(DateTime)) return (T)(object)(value ?? default(DateTime));
        if (typeof(T) == typeof(DateTime?)) return (T)(object)value!;
        DateOnly? dateOnly = value is { } v ? DateOnly.FromDateTime(v) : null;
        if (typeof(T) == typeof(DateOnly)) return (T)(object)(dateOnly ?? default(DateOnly));
        if (typeof(T) == typeof(DateOnly?)) return (T)(object)dateOnly!;
        throw UnsupportedType();
    }

    static NotSupportedException UnsupportedType() => new(
        $"EditDatePicker<{typeof(T)}> is not supported -- supported types are DateTime, DateTime?, DateOnly, and DateOnly?.");

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
        // Gregorian-forced like the picker's own display, so read-only and edit mode can never
        // disagree about the year under a non-Gregorian-default culture (th-TH, ar-SA).
        var culture = GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);
        try
        {
            return CurrentValue switch
            {
                null => string.Empty,
                DateTime dt => dt.ToString(DateFormat, culture),
                DateOnly d => d.ToString(DateFormat, culture),
                _ => string.Empty
            };
        }
        catch (FormatException)
        {
            return CurrentValue?.ToString() ?? string.Empty;
        }
    }

    // default(DateTime)/default(DateOnly) count as semantically empty for date controls -- mirrors
    // EditDate<T>'s IsValueDefault override, including the same boxed-Nullable<T> pattern-match trick
    // (see PickerValue above) so DateTime?/DateOnly? null falls through to the EqualityComparer arm.
    protected override bool IsValueDefault() => CurrentValue switch
    {
        DateTime dt => dt == default,
        DateOnly d => d == default,
        _ => EqualityComparer<T>.Default.Equals(CurrentValue, default!)
    };
}
