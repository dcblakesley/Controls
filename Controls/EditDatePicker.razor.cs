namespace Controls;

/// <summary>
/// Edit control for a single date, time, or date+time value, backed by the <see cref="DatePicker"/>
/// UI-kit calendar dropdown. Adds form binding, validation, label, read-only view, and
/// <see cref="FormOptions"/> support (the same contract every other scalar control provides) on top
/// of DatePicker's type-or-pick UX. Generic like <see cref="EditDate{T}"/>: <typeparamref name="T"/>
/// supports <c>DateTime</c>, <c>DateTime?</c>, <c>DateTimeOffset</c>, <c>DateTimeOffset?</c>,
/// <c>DateOnly</c>, <c>DateOnly?</c>, <c>TimeOnly</c>, and <c>TimeOnly?</c> — any other type throws
/// <see cref="NotSupportedException"/> at render. <see cref="Type"/> selects what the calendar picks
/// (the same parameter, name and meaning, as <see cref="EditDate{T}"/>'s) and maps onto the inner
/// <see cref="DatePicker"/>'s <see cref="DatePickerMode"/>: <c>Date</c>→<c>Date</c>,
/// <c>DateTimeLocal</c>→<c>DateTime</c>, <c>Month</c>→<c>Month</c>, <c>Time</c>→<c>Time</c>. The
/// separate <see cref="Mode"/> parameter overrides that mapping outright — set it to reach
/// <see cref="DatePickerMode.Week"/>, <see cref="DatePickerMode.Quarter"/>, or
/// <see cref="DatePickerMode.Year"/>, none of which <see cref="Type"/> has an equivalent for (see the
/// class remarks). For a native <c>&lt;input type="date"&gt;</c> (or <c>datetime-local</c>/<c>month</c>/
/// <c>time</c>) use <see cref="EditDate{T}"/> instead — the two controls support the identical set of
/// bound types and <see cref="Type"/> values, so the choice is purely native input vs. the AntD-style
/// calendar dropdown UX.
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
/// <c>DateTime</c> (e.g. <c>Min="@d.ToDateTime(TimeOnly.MinValue)"</c>). They're date-granularity and
/// ignored entirely when <see cref="Type"/> is <c>Time</c> (a time-of-day has no date-range concept) —
/// same as the inner <see cref="DatePicker"/>'s own <see cref="DatePicker.Min"/>/<see cref="DatePicker.Max"/>.
/// </para>
/// <para>
/// <see cref="Mode"/> is the ONE intentional asymmetry between this control and <see cref="EditDate{T}"/>:
/// <see cref="EditDate{T}"/>'s <c>Type</c> drives a native <c>&lt;input&gt;</c>, and the HTML input
/// types it maps onto (<c>date</c>/<c>datetime-local</c>/<c>month</c>/<c>time</c>) have no
/// week/quarter/year equivalent to reach even in principle — there is nothing there for a
/// <c>Mode</c>-shaped parameter to override. This control's calendar is a UI-kit component with no
/// such ceiling, so it gets the escape hatch <see cref="EditDate{T}"/> structurally cannot offer.
/// Week/Quarter/Year values still bind naturally to every one of this control's eight supported
/// shapes with no bridge changes — they're all midnight date starts, exactly like <c>Date</c>/<c>Month</c>.
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
    [Parameter] public string? Format { get; set; }
    /// <inheritdoc cref="DatePicker.Placeholder"/>
    [Parameter] public string? Placeholder { get; set; }
    /// <inheritdoc cref="DatePicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <inheritdoc cref="DatePicker.Width"/>
    [Parameter] public string? Width { get; set; }
    /// <inheritdoc cref="DatePicker.FirstDayOfWeek"/>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary> The value shape the calendar picks — Date, DateTimeLocal, Month, or Time. Defaults
    /// to Date. Maps onto the inner <see cref="DatePicker"/>'s <see cref="DatePickerMode"/>:
    /// Date→Date, DateTimeLocal→DateTime, Month→Month, Time→Time (see the class remarks).</summary>
    [Parameter] public InputDateType Type { get; set; } = InputDateType.Date;

    /// <summary> Format string for the read-only value display. Null (default) picks the effective
    /// mode's default (<see cref="Mode"/> when set, else <see cref="Type"/>'s mapping): Date
    /// "MM-dd-yyyy" (the original, unchanged default) · Month "MM-yyyy" · DateTimeLocal "MM-dd-yyyy "
    /// plus Time's own string · Time "HH:mm:ss" (<see cref="ShowSeconds"/> false drops ":ss";
    /// <see cref="Use12Hours"/> switches to the 12-hour "h:mm tt"/"h:mm:ss tt" forms) · Year "yyyy" ·
    /// Quarter/Week render the same "yyyy-Qn"/"yyyy-Www" shorthand the picker itself shows (no .NET
    /// format token exists for either) — set <see cref="DateFormat"/> explicitly in those two modes
    /// and it is used verbatim via <c>ToString</c> instead, which can't render the quarter/week digit.</summary>
    [Parameter] public string? DateFormat { get; set; }

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
    /// <inheritdoc cref="DatePicker.PrevYearLabel"/>
    [Parameter] public string PrevYearLabel { get; set; } = "Previous year";
    /// <inheritdoc cref="DatePicker.NextYearLabel"/>
    [Parameter] public string NextYearLabel { get; set; } = "Next year";
    /// <inheritdoc cref="DatePicker.HourSelectLabel"/>
    [Parameter] public string HourSelectLabel { get; set; } = "Hour";
    /// <inheritdoc cref="DatePicker.MinuteSelectLabel"/>
    [Parameter] public string MinuteSelectLabel { get; set; } = "Minute";
    /// <inheritdoc cref="DatePicker.SecondSelectLabel"/>
    [Parameter] public string SecondSelectLabel { get; set; } = "Second";
    /// <inheritdoc cref="DatePicker.OkText"/>
    [Parameter] public string OkText { get; set; } = "OK";

    /// <summary>
    /// Overrides the inner <see cref="DatePicker"/>'s <see cref="DatePickerMode"/> directly. Null
    /// (default) derives it from <see cref="Type"/> exactly as before (see <see cref="PickerMode"/>);
    /// set this explicitly to reach <see cref="DatePickerMode.Week"/>, <see cref="DatePickerMode.Quarter"/>,
    /// or <see cref="DatePickerMode.Year"/> — <see cref="InputDateType"/> has no equivalents for those
    /// three (and <see cref="EditDate{T}"/>'s own <c>Type</c> stays untouched: it drives a native
    /// <c>&lt;input&gt;</c>, which has no week/quarter/year picker mode to reach either — see the
    /// class remarks for why this is the one intentional asymmetry between the two controls).
    /// <see cref="Type"/> keeps controlling every OTHER default this control resolves (the effective
    /// <see cref="Format"/>/<see cref="Placeholder"/>/<see cref="DateFormat"/>) via the SAME effective
    /// mode this parameter feeds — so a consumer overriding <c>Mode</c> alone (leaving <c>Type</c> at
    /// its default) still gets Week/Quarter/Year's own format/placeholder defaults, not Date's.
    /// </summary>
    [Parameter] public DatePickerMode? Mode { get; set; }

    /// <inheritdoc cref="DatePicker.ShowWeekNumbers"/>
    [Parameter] public bool ShowWeekNumbers { get; set; }
    /// <inheritdoc cref="DatePicker.DisabledDate"/>
    [Parameter] public Func<DateTime, bool>? DisabledDate { get; set; }
    /// <inheritdoc cref="DatePicker.DisabledTime"/>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? DisabledTime { get; set; }
    /// <inheritdoc cref="DatePicker.HideDisabledTimeOptions"/>
    [Parameter] public bool HideDisabledTimeOptions { get; set; }
    /// <inheritdoc cref="DatePicker.ShowSeconds"/>
    [Parameter] public bool ShowSeconds { get; set; } = true;
    /// <inheritdoc cref="DatePicker.HourStep"/>
    [Parameter] public int HourStep { get; set; } = 1;
    /// <inheritdoc cref="DatePicker.MinuteStep"/>
    [Parameter] public int MinuteStep { get; set; } = 1;
    /// <inheritdoc cref="DatePicker.SecondStep"/>
    [Parameter] public int SecondStep { get; set; } = 1;
    /// <inheritdoc cref="DatePicker.Use12Hours"/>
    [Parameter] public bool Use12Hours { get; set; }
    /// <inheritdoc cref="DatePicker.PeriodSelectLabel"/>
    [Parameter] public string PeriodSelectLabel { get; set; } = "AM/PM";
    /// <inheritdoc cref="DatePicker.ShowToday"/>
    [Parameter] public bool ShowToday { get; set; }
    /// <inheritdoc cref="DatePicker.TodayText"/>
    [Parameter] public string TodayText { get; set; } = "Today";
    /// <inheritdoc cref="DatePicker.ShowNow"/>
    [Parameter] public bool ShowNow { get; set; }
    /// <inheritdoc cref="DatePicker.NowText"/>
    [Parameter] public string NowText { get; set; } = "Now";
    /// <inheritdoc cref="DatePicker.Presets"/>
    [Parameter] public IReadOnlyList<DatePickerPreset>? Presets { get; set; }
    /// <inheritdoc cref="DatePicker.PresetsLabel"/>
    [Parameter] public string PresetsLabel { get; set; } = "Quick picks";
    /// <inheritdoc cref="DatePicker.ExtraFooter"/>
    [Parameter] public RenderFragment? ExtraFooter { get; set; }
    /// <inheritdoc cref="DatePicker.DefaultViewDate"/>
    [Parameter] public DateTime? DefaultViewDate { get; set; }
    /// <inheritdoc cref="DatePicker.PrevDecadeLabel"/>
    [Parameter] public string PrevDecadeLabel { get; set; } = "Previous decade";
    /// <inheritdoc cref="DatePicker.NextDecadeLabel"/>
    [Parameter] public string NextDecadeLabel { get; set; } = "Next decade";

    string EffectiveInputLabel => InputLabel ?? Label ?? _attributes.GetLabelText(_fieldIdentifier);

    // Type -> DatePickerMode. The inner DatePicker only knows Mode; Type is EditDatePicker's own
    // parameter name/shape (matching EditDate<T>'s Type exactly) so the two controls share one mental
    // model for "what does this field pick" regardless of which UX backs it. Mode (above) overrides
    // this outright when set -- EffectiveMode is what actually reaches the picker and what every
    // read-only-display default below keys off of.
    DatePickerMode PickerMode => Type switch
    {
        InputDateType.Date => DatePickerMode.Date,
        InputDateType.DateTimeLocal => DatePickerMode.DateTime,
        InputDateType.Month => DatePickerMode.Month,
        InputDateType.Time => DatePickerMode.Time,
        _ => DatePickerMode.Date
    };

    DatePickerMode EffectiveMode => Mode ?? PickerMode;

    // Mirrors DatePicker.TimeFormatString exactly (see its doc comment) rather than sharing it --
    // it's one small string, and sharing it would mean exposing an internal instance member across
    // two otherwise-independent classes for a three-line ternary. This value also feeds
    // EffectiveDateFormat's own Time/DateTime default below.
    string TimeFormatPart => Use12Hours
        ? (ShowSeconds ? "h:mm:ss tt" : "h:mm tt")
        : (ShowSeconds ? "HH:mm:ss" : "HH:mm");

    string EffectiveDateFormat => DateFormat ?? EffectiveMode switch
    {
        DatePickerMode.Date => "MM-dd-yyyy",
        DatePickerMode.Month => "MM-yyyy",
        DatePickerMode.DateTime => $"MM-dd-yyyy {TimeFormatPart}",
        DatePickerMode.Time => TimeFormatPart,
        DatePickerMode.Year => "yyyy",
        // Quarter/Week have no .NET format token for their own display -- GetDisplayValue below
        // special-cases them via DatePicker's shared FormatQuarterDisplay/FormatWeekDisplay instead
        // of ever calling ToString(EffectiveDateFormat) for either. This "yyyy" is never actually
        // rendered; it only matters if some future caller starts using EffectiveDateFormat directly.
        DatePickerMode.Quarter => "yyyy",
        DatePickerMode.Week => "yyyy",
        _ => "MM-dd-yyyy"
    };

    // FirstDayOfWeek resolution mirrors DatePicker's own EffectiveFirstDayOfWeek (culture fallback),
    // computed independently here for GetDisplayValue's Week special case -- there's no picker
    // instance to ask once the control is in read-only mode (no <DatePicker> renders at all then).
    DayOfWeek EffectiveFirstDayOfWeek(CultureInfo culture) => FirstDayOfWeek ?? culture.DateTimeFormat.FirstDayOfWeek;

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

    // The inner <DatePicker> is DateTime?-only (a date-only midnight value, or -- in Time/DateTime
    // mode -- a time-of-day/date+time still carried in a DateTime) regardless of T -- these bridge
    // CurrentValue to and from it. Boxing+pattern-match on the runtime type (not typeof(T) == checks)
    // mirrors EditDate<T>'s GetDisplayValue/IsValueDefault, which already rely on the CLR boxing a
    // non-null Nullable<T> as its underlying T (so "DateTime dt" matches DateTime? too).
    DateTime? PickerValue => CurrentValue switch
    {
        null => null,
        DateTime dt => dt,
        // Face value, matching how EditDate displays a DateTimeOffset via BindConverter.FormatValue --
        // no UTC/Local conversion, just the same clock time the offset carries.
        DateTimeOffset dto => dto.DateTime,
        DateOnly d => d.ToDateTime(TimeOnly.MinValue),
        TimeOnly t => DateTime.Today.Add(t.ToTimeSpan()),
        _ => throw UnsupportedType()
    };

    // The reverse direction can't pattern-match on the incoming value (it's always DateTime?) --
    // typeof(T) picks which of the eight supported shapes to produce. typeof(T) == checks over a
    // handful of concrete value types are fully trim/AOT-safe (no reflection over T's members).
    void OnValueChanged(DateTime? value) => CurrentValue = FromPickerValue(value);

    static T FromPickerValue(DateTime? value)
    {
        if (typeof(T) == typeof(DateTime)) return (T)(object)(value ?? default(DateTime));
        if (typeof(T) == typeof(DateTime?)) return (T)(object)value!;
        // The picker never sets Kind -- its values carry Kind.Unspecified (or, from the Time-mode
        // DateTime.Today anchor, Kind.Local). Both assume the local offset when constructing a
        // DateTimeOffset, matching BindConverter's parse semantics for datetime-local text. Computed
        // only inside the DateTimeOffset arms: within the local offset of DateTime.MinValue (a typed
        // year-1 date in an east-of-UTC zone) the constructor itself throws, and no other T needs it.
        if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
        {
            DateTimeOffset? dto = value is { } vo ? new DateTimeOffset(vo) : null;
            if (typeof(T) == typeof(DateTimeOffset)) return (T)(object)(dto ?? default(DateTimeOffset));
            return (T)(object)dto!;
        }
        DateOnly? dateOnly = value is { } v ? DateOnly.FromDateTime(v) : null;
        if (typeof(T) == typeof(DateOnly)) return (T)(object)(dateOnly ?? default(DateOnly));
        if (typeof(T) == typeof(DateOnly?)) return (T)(object)dateOnly!;
        TimeOnly? timeOnly = value is { } vt ? TimeOnly.FromDateTime(vt) : null;
        if (typeof(T) == typeof(TimeOnly)) return (T)(object)(timeOnly ?? default(TimeOnly));
        if (typeof(T) == typeof(TimeOnly?)) return (T)(object)timeOnly!;
        throw UnsupportedType();
    }

    static NotSupportedException UnsupportedType() => new(
        $"EditDatePicker<{typeof(T)}> is not supported -- supported types are DateTime, DateTime?, " +
        "DateTimeOffset, DateTimeOffset?, DateOnly, DateOnly?, TimeOnly, and TimeOnly?.");

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
        if (CurrentValue is null) return string.Empty;
        // Gregorian-forced like the picker's own display, so read-only and edit mode can never
        // disagree about the year under a non-Gregorian-default culture (th-TH, ar-SA).
        var culture = GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);
        // Quarter/Week's null-DateFormat display has no .NET format token to route through
        // ToString(EffectiveDateFormat) below -- reuses DatePicker's own FormatQuarterDisplay/
        // FormatWeekDisplay (promoted internal statics, not duplicated regex/format logic here) via
        // PickerValue, the same DateTime? bridge the picker itself would see. An explicit DateFormat
        // still falls through to the verbatim ToString path, matching the picker's own Format contract.
        if (DateFormat is null && PickerValue is { } pv)
        {
            if (EffectiveMode == DatePickerMode.Quarter) return DatePicker.FormatQuarterDisplay(pv, culture);
            if (EffectiveMode == DatePickerMode.Week) return DatePicker.FormatWeekDisplay(pv, culture, EffectiveFirstDayOfWeek(culture));
        }
        try
        {
            return CurrentValue switch
            {
                DateTime dt => dt.ToString(EffectiveDateFormat, culture),
                DateTimeOffset dto => dto.ToString(EffectiveDateFormat, culture),
                DateOnly d => d.ToString(EffectiveDateFormat, culture),
                TimeOnly t => t.ToString(EffectiveDateFormat, culture),
                _ => string.Empty
            };
        }
        catch (FormatException)
        {
            return CurrentValue?.ToString() ?? string.Empty;
        }
    }

    // default(DateTime)/default(DateTimeOffset)/default(DateOnly)/default(TimeOnly) count as
    // semantically empty for date controls -- mirrors EditDate<T>'s IsValueDefault override,
    // including the same boxed-Nullable<T> pattern-match trick (see PickerValue above) so any of
    // this control's four nullable shapes falls through to the EqualityComparer arm on null.
    protected override bool IsValueDefault() => CurrentValue switch
    {
        DateTime dt => dt == default,
        DateTimeOffset dto => dto == default,
        DateOnly d => d == default,
        TimeOnly t => t == default,
        _ => EqualityComparer<T>.Default.Equals(CurrentValue, default!)
    };
}
