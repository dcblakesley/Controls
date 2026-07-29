namespace Controls;

/// <summary>
/// Edit control for date and date/time values, rendered as a native <c>&lt;input type="date"&gt;</c>
/// (or <c>datetime-local</c>/<c>month</c>/<c>time</c>, per <see cref="Type"/>) with a customizable
/// read-only format. Zero JS, styled entirely by <c>edit-controls.css</c>. For the AntD-style calendar
/// dropdown (the default date control), use <see cref="EditDate{T}"/> instead — the two controls
/// support the identical set of bound types and <see cref="Type"/> values, so the choice is purely
/// native input vs. calendar dropdown UX.
/// </summary>
// T is annotated 'All' because TryParseValueFromString feeds it (via EditControlInit.TryConvert<T>)
// to BindConverter.TryConvertTo<T>, which declares that requirement for its TypeConverter fallback
// (mirrors the framework's InputDate<T>).
public partial class EditDateNative<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T> : EditTextControlBase<T>
{
    // Component-specific parameters. Size and UpdateOn (+ UpdateEventName) live on
    // EditTextControlBase<TValue>, shared with EditString/EditTextArea/EditNumber. No Placeholder here
    // by design -- a native date/time input renders its own format hint.

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<T>>? Field { get; set; }

    /// <summary>
    /// Format string for displaying the date in read-only mode. Falls back to the bound property's
    /// <c>[DisplayFormat(DataFormatString = "…")]</c> when unset, then to "MM-dd-yyyy" -- see
    /// <see cref="EffectiveDateFormat"/>.
    /// </summary>
    [Parameter] public string? DateFormat { get; set; }

    /// <summary>
    /// The HTML input type — Date, DateTimeLocal, Month, or Time. Falls back to the bound property's
    /// <c>[DataType(DataType.Date/DateTime/Time)]</c> when unset, then to <see cref="InputDateType.Date"/>
    /// -- see <see cref="EffectiveType"/>.
    /// </summary>
    [Parameter] public InputDateType? Type { get; set; }

    /// <summary>
    /// The format actually used for the read-only display: the <see cref="DateFormat"/> parameter,
    /// else the bound property's <c>[DisplayFormat]</c> (see <see cref="AttributesHelper.FormatString"/>),
    /// else <c>"MM-dd-yyyy"</c> -- the same default the parameter used to carry directly.
    /// </summary>
    string EffectiveDateFormat => DateFormat ?? _attributes.FormatString() ?? "MM-dd-yyyy";

    /// <summary>
    /// The type actually used to select the rendered <c>&lt;input&gt;</c>'s HTML type and the
    /// <see cref="InputFormat"/>/bound granularity: the <see cref="Type"/> parameter, else the bound
    /// property's <c>[DataType(DataType.Date/DateTime/Time)]</c> (see
    /// <see cref="AttributesHelper.DateInputType"/>), else <see cref="InputDateType.Date"/> -- the
    /// same default the parameter used to carry directly.
    /// </summary>
    InputDateType EffectiveType => Type ?? _attributes.DateInputType() ?? InputDateType.Date;

    /// <summary>
    /// Lower bound rendered as the native <c>&lt;input&gt;</c>'s <c>min</c> attribute. Stays
    /// <c>DateTime?</c> regardless of <typeparamref name="T"/> -- only the bound value generalizes,
    /// the same contract as <see cref="EditDate{T}.Min"/> (e.g. a <c>DateOnly</c>-bound instance
    /// still sets this with a <c>DateTime</c>: <c>Min="@d.ToDateTime(TimeOnly.MinValue)"</c>). Null
    /// (default) falls back to the bound property's <see cref="MinValueAttribute"/> or
    /// <see cref="RangeAttribute"/> minimum (see <see cref="EffectiveMin"/>), then to no lower bound
    /// at all -- the attribute is omitted, not rendered as an unbounded floor. Ignored entirely when
    /// <see cref="Type"/> is <see cref="InputDateType.Time"/>: a time-of-day has no date-range
    /// concept, matching <see cref="EditDate{T}"/>'s own documented Time-mode exemption for its inner
    /// <c>DatePicker</c>.
    /// </summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>
    /// Upper bound rendered as the native <c>&lt;input&gt;</c>'s <c>max</c> attribute. Same
    /// <c>DateTime?</c>-regardless-of-<typeparamref name="T"/> contract as <see cref="Min"/>, model-
    /// attribute fallback (via <see cref="MaxValueAttribute"/>/<see cref="RangeAttribute"/>, see
    /// <see cref="EffectiveMax"/>), and <see cref="InputDateType.Time"/> exemption.
    /// </summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>
    /// Resolves <see cref="Min"/> against the bound property's <see cref="MinValueAttribute"/>/
    /// <see cref="RangeAttribute"/> fallback (see <see cref="AttributesHelper.MinDate"/>). Null is
    /// preserved when neither source supplies a bound, so <see cref="MinAttribute"/> omits the
    /// native <c>min</c> attribute entirely rather than rendering an unbounded floor.
    /// </summary>
    DateTime? EffectiveMin => Min ?? _attributes.MinDate();
    /// <summary>
    /// Resolves <see cref="Max"/> against the bound property's <see cref="MaxValueAttribute"/>/
    /// <see cref="RangeAttribute"/> fallback (see <see cref="AttributesHelper.MaxDate"/>). Same
    /// null-preserving contract as <see cref="EffectiveMin"/>.
    /// </summary>
    DateTime? EffectiveMax => Max ?? _attributes.MaxDate();

    /// <summary> Error message format string used when the value can't be parsed. <c>{0}</c> is replaced with the field name.</summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a date.";

    /// <inheritdoc/>
    /// <remarks>
    /// This control's answer is <see cref="UpdateTrigger.Change"/>, and overriding it with
    /// <see cref="UpdateTrigger.Input"/> via <see cref="EditTextControlBase{TValue}.UpdateOn"/> is not
    /// free: browsers report a partially-typed <c>type="date"</c>/<c>datetime-local</c>/<c>month</c>/<c>time</c> input's value
    /// as an empty string until the user finishes typing a valid value, so per-keystroke binding
    /// flashes a spurious <see cref="ParsingErrorMessage"/> validation error on every keystroke --
    /// which is exactly why Change is the default here (matching the framework's own
    /// <c>InputDate&lt;T&gt;</c>).
    /// </remarks>
    protected override UpdateTrigger DefaultUpdateTrigger => UpdateTrigger.Change;

    /// <summary>
    /// The input's <c>class</c> attribute. <see cref="EditTextControlBase{TValue}.Size"/> at its
    /// default reproduces today's exact string (byte-identical legacy DOM); otherwise appends
    /// <see cref="EditInputShell.SizeClass"/>'s token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass("edit-input edit-date-input", Size, CssClass);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditDateNative<T>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Ported from Microsoft.AspNetCore.Components.Forms.InputDate<T>, and identical to EditNumber<T>'s
    // parse — hence the shared body in EditControlInit.TryConvert. BindConverter handles DateTime,
    // DateTime?, DateTimeOffset, DateTimeOffset?, DateOnly, DateOnly?, TimeOnly, TimeOnly?; only
    // ParsingErrorMessage differs between the two controls.
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage) =>
        EditControlInit.TryConvert(value, ParsingErrorMessage, FieldIdentifier.FieldName, out result, out validationErrorMessage);

    // Ported from InputDate<T>: format-string varies with Type so the value round-trips through the
    // browser's <input type="date|datetime-local|month|time"> in the format it expects. Also backs
    // MinAttribute/MaxAttribute below, so the bound value and its native min/max can never disagree
    // on format.
    string InputFormat => EffectiveType switch
    {
        InputDateType.Date => "yyyy-MM-dd",
        InputDateType.DateTimeLocal => "yyyy-MM-ddTHH:mm:ss",
        InputDateType.Month => "yyyy-MM",
        InputDateType.Time => "HH:mm:ss",
        _ => "yyyy-MM-dd"
    };

    protected override string FormatValueAsString(T? value) => value switch
    {
        DateTime dt => BindConverter.FormatValue(dt, InputFormat, CultureInfo.InvariantCulture),
        DateTimeOffset dto => BindConverter.FormatValue(dto, InputFormat, CultureInfo.InvariantCulture),
        DateOnly @do => BindConverter.FormatValue(@do, InputFormat, CultureInfo.InvariantCulture),
        TimeOnly to => BindConverter.FormatValue(to, InputFormat, CultureInfo.InvariantCulture),
        _ => string.Empty
    };

    /// <summary>
    /// The native <c>min</c> attribute value, formatted with the same <see cref="InputFormat"/> the
    /// bound value itself round-trips through (see <see cref="FormatValueAsString"/>) -- so the value
    /// and its bound can never disagree on format. Null when <see cref="EffectiveMin"/> resolves to
    /// nothing, or when <see cref="Type"/> is <see cref="InputDateType.Time"/> (min/max are date-
    /// granularity and meaningless for a time-of-day -- parity with <see cref="EditDate{T}"/>, whose
    /// own Min/Max are documented as ignored in Time mode). Blazor renders no attribute at all for a
    /// null value, same as <see cref="EditNumber{T}"/>'s own <c>min</c> pattern.
    /// </summary>
    string? MinAttribute => FormatBound(EffectiveMin);
    /// <inheritdoc cref="MinAttribute"/>
    string? MaxAttribute => FormatBound(EffectiveMax);

    string? FormatBound(DateTime? value) =>
        EffectiveType == InputDateType.Time || value is not { } dt
            ? null
            : BindConverter.FormatValue(dt, InputFormat, CultureInfo.InvariantCulture);

    // Format the bound value directly by its type with DateFormat. (The old code re-parsed the
    // round-tripped editor string and ran ToUniversalTime().ToLocalTime(), which rendered TimeOnly
    // as a date and could shift dates across midnight in non-UTC zones.) The try/catch falls back
    // to the value's own ToString() if DateFormat is incompatible with the type (e.g. a date format
    // on a TimeOnly), so a mis-set format degrades instead of throwing.
    string GetDisplayValue()
    {
        try
        {
            return CurrentValue switch
            {
                null => string.Empty,
                DateTime dt => dt.ToString(EffectiveDateFormat, CultureInfo.CurrentCulture),
                DateTimeOffset dto => dto.ToString(EffectiveDateFormat, CultureInfo.CurrentCulture),
                DateOnly d => d.ToString(EffectiveDateFormat, CultureInfo.CurrentCulture),
                TimeOnly t => t.ToString(EffectiveDateFormat, CultureInfo.CurrentCulture),
                _ => CurrentValue.ToString() ?? string.Empty
            };
        }
        catch (FormatException)
        {
            return CurrentValue?.ToString() ?? string.Empty;
        }
    }

    // Detect default DateTime / DateTimeOffset even when boxed inside a nullable T —
    // EqualityComparer<DateTime?>.Default.Equals(default(DateTime), null) is false, but the
    // wrapped default value is still semantically empty for hiding purposes.
    protected override bool IsValueDefault() => CurrentValue switch
    {
        DateTime dt => dt == default,
        DateTimeOffset dto => dto == default,
        DateOnly d => d == default,
        TimeOnly t => t == default,
        _ => EqualityComparer<T>.Default.Equals(CurrentValue, default!)
    };
}
