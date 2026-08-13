namespace Controls;

/// <summary>
/// Edit control for a single date, time, or date+time value, backed by the <see cref="DatePicker"/>
/// UI-kit calendar dropdown — the default date control. Adds form binding, validation, label,
/// read-only view, and <see cref="FormOptions"/> support (the same contract every other scalar
/// control provides) on top of DatePicker's type-or-pick UX. Generic like <see cref="EditDateNative{T}"/>:
/// <typeparamref name="T"/> supports <c>DateTime</c>, <c>DateTime?</c>, <c>DateTimeOffset</c>,
/// <c>DateTimeOffset?</c>, <c>DateOnly</c>, <c>DateOnly?</c>, <c>TimeOnly</c>, and <c>TimeOnly?</c> —
/// any other type throws <see cref="NotSupportedException"/> at render. <see cref="Type"/> selects
/// what the calendar picks (the same parameter, name and meaning, as <see cref="EditDateNative{T}"/>'s)
/// and maps onto the inner <see cref="DatePicker"/>'s <see cref="DatePickerMode"/>: <c>Date</c>→<c>Date</c>,
/// <c>DateTimeLocal</c>→<c>DateTime</c>, <c>Month</c>→<c>Month</c>, <c>Time</c>→<c>Time</c>. The
/// separate <see cref="Mode"/> parameter overrides that mapping outright — set it to reach
/// <see cref="DatePickerMode.Week"/>, <see cref="DatePickerMode.Quarter"/>, or
/// <see cref="DatePickerMode.Year"/>, none of which <see cref="Type"/> has an equivalent for (see the
/// class remarks). For a native <c>&lt;input type="date"&gt;</c> (or <c>datetime-local</c>/<c>month</c>/
/// <c>time</c>) use <see cref="EditDateNative{T}"/> instead — the two controls support the identical
/// set of bound types and <see cref="Type"/> values, so the choice is purely native input vs. this
/// control's AntD-style calendar dropdown UX.
/// </summary>
/// <remarks>
/// <para>
/// Validation-state ARIA reaches the picker's actual <c>&lt;input&gt;</c> through
/// <see cref="DatePicker"/>'s <c>AriaRequired</c>/<c>AriaInvalid</c>/<c>AriaDescribedBy</c>/
/// <c>AriaErrorMessage</c> parameters — the same forwarding shape as
/// <see cref="EditSelectSearch{TValue}"/> onto <see cref="Select{TValue}"/>. The input's NAME comes
/// from <c>AriaLabelledBy</c> pointed at the <see cref="FormLabel"/>'s <c>lbltext-{id}</c> anchor
/// (the label's text alone, without the tooltip trigger that sits in the same <c>&lt;label&gt;</c>),
/// unless <see cref="InputLabel"/> is set explicitly — which suppresses the reference, since
/// <c>aria-labelledby</c> would otherwise win over it. The consumer's own
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
/// <see cref="Mode"/> is the ONE intentional asymmetry between this control and <see cref="EditDateNative{T}"/>:
/// <see cref="EditDateNative{T}"/>'s <c>Type</c> drives a native <c>&lt;input&gt;</c>, and the HTML input
/// types it maps onto (<c>date</c>/<c>datetime-local</c>/<c>month</c>/<c>time</c>) have no
/// week/quarter/year equivalent to reach even in principle — there is nothing there for a
/// <c>Mode</c>-shaped parameter to override. This control's calendar is a UI-kit component with no
/// such ceiling, so it gets the escape hatch <see cref="EditDateNative{T}"/> structurally cannot offer.
/// Week/Quarter/Year values still bind naturally to every one of this control's eight supported
/// shapes with no bridge changes — they're all midnight date starts, exactly like <c>Date</c>/<c>Month</c>.
/// </para>
/// </remarks>
public partial class EditDate<T> : EditControlBase<T>
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<T>>? Field { get; set; }

    /// <summary>
    /// Lower bound forwarded to the inner <see cref="DatePicker"/> (see <see cref="DatePicker.Min"/>
    /// for the exact mode-dependent granularity and the <c>Time</c>-mode exemption). Null (default)
    /// falls back to the bound property's <see cref="MinValueAttribute"/> or <see cref="RangeAttribute"/>
    /// minimum (see <see cref="EffectiveMin"/>), then to <see cref="DatePicker"/>'s own default of no
    /// lower bound.
    /// </summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>
    /// Upper bound forwarded to the inner <see cref="DatePicker"/> (see <see cref="DatePicker.Max"/>).
    /// Null (default) falls back to the bound property's <see cref="MaxValueAttribute"/> or
    /// <see cref="RangeAttribute"/> maximum (see <see cref="EffectiveMax"/>), then to
    /// <see cref="DatePicker"/>'s own default of no upper bound.
    /// </summary>
    [Parameter] public DateTime? Max { get; set; }
    /// <summary>
    /// Display and primary parse format forwarded to the inner <see cref="DatePicker"/> (see
    /// <see cref="DatePicker.Format"/>). Falls back to the bound property's
    /// <c>[DisplayFormat(DataFormatString = "…")]</c> when unset -- see <see cref="EffectiveFormat"/>.
    /// </summary>
    [Parameter] public string? Format { get; set; }
    /// <summary>
    /// Placeholder text forwarded to the inner <see cref="DatePicker"/>. Null (default) falls back to
    /// the bound property's <see cref="PlaceholderAttribute"/> or <see cref="DisplayAttribute"/>
    /// <c>Prompt</c> (see <see cref="EffectivePlaceholder"/>), then to <see cref="DatePicker"/>'s own
    /// mode-derived default (its internal <c>EffectivePlaceholder</c>, e.g. "Select date").
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }
    /// <inheritdoc cref="DatePicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <inheritdoc cref="DatePicker.Width"/>
    [Parameter] public string? Width { get; set; }
    /// <inheritdoc cref="DatePicker.Size"/>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;
    /// <inheritdoc cref="DatePicker.FirstDayOfWeek"/>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>
    /// Error message format string used when a typed entry can't be parsed as a date at all -- i.e.
    /// the inner <see cref="DatePicker"/> raises <see cref="DatePicker.OnParseError"/> (a well-formed
    /// date merely rejected by <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/
    /// <see cref="DisabledTime"/> does not). <c>{0}</c> is replaced with the field name -- same
    /// formatting as <see cref="EditDateNative{T}.ParsingErrorMessage"/>. Surfaces as a validation message
    /// via a dedicated <see cref="ValidationMessageStore"/> scoped to this control's own
    /// <see cref="FieldIdentifier"/> (see <see cref="OnPickerParseErrorAsync"/>), since this control
    /// never routes through <see cref="TryParseValueFromString"/> -- the picker sets values through
    /// its own value callback, not string parsing. Cleared the moment a valid value next commits (see
    /// <see cref="OnValueChanged"/>).
    /// </summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a date.";

    /// <summary>
    /// Error message format string used when a typed entry parses into a perfectly well-formed date
    /// that the inner <see cref="DatePicker"/> nonetheless REFUSES — one rejected by
    /// <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<see cref="DisabledTime"/>
    /// (<see cref="DatePicker.OnRangeError"/>). <c>{0}</c> is replaced with the field name, same
    /// formatting as <see cref="ParsingErrorMessage"/>, and it lands in the same
    /// <see cref="ValidationMessageStore"/> — so it reaches <c>FieldValidationDisplay</c>'s live
    /// region and sets <c>aria-invalid</c> exactly as a parse error does, and is cleared the moment a
    /// valid value next commits (see <see cref="OnValueChanged"/>).
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ParsingErrorMessage"/> because the two are genuinely different
    /// situations: "that isn't a date" versus "that IS a date, but not one this field accepts". Before
    /// this existed the second case was silent in every channel — the picker reverted the text,
    /// <c>CurrentValue</c> never changed, <c>NotifyFieldChanged</c> never fired, no validator ran —
    /// so a user typing a date outside the bounds and tabbing away had no way, keyboard or otherwise,
    /// to find out why the field emptied itself. See also <see cref="DatePicker.RangeHintMinLabel"/>,
    /// which is the same information delivered BEFORE the refusal.
    /// </remarks>
    [Parameter] public string RangeErrorMessage { get; set; } = "The {0} field must be an allowed date.";

    /// <summary> The value shape the calendar picks — Date, DateTimeLocal, Month, or Time. Maps onto
    /// the inner <see cref="DatePicker"/>'s <see cref="DatePickerMode"/>: Date→Date, DateTimeLocal→
    /// DateTime, Month→Month, Time→Time (see the class remarks). Falls back to the bound property's
    /// <c>[DataType(DataType.Date/DateTime/Time)]</c> when unset, then to <see cref="InputDateType.Date"/>
    /// -- see <see cref="EffectiveType"/>.</summary>
    [Parameter] public InputDateType? Type { get; set; }

    /// <summary> Format string for the read-only value display. Null (default) picks the effective
    /// mode's default (<see cref="Mode"/> when set, else <see cref="Type"/>'s mapping): Date
    /// "MM-dd-yyyy" (the original, unchanged default) · Month "MM-yyyy" · DateTimeLocal "MM-dd-yyyy "
    /// plus Time's own string · Time "HH:mm:ss" (<see cref="ShowSeconds"/> false drops ":ss";
    /// <see cref="Use12Hours"/> switches to the 12-hour "h:mm tt"/"h:mm:ss tt" forms) · Year "yyyy" ·
    /// Quarter/Week render the same "yyyy-Qn"/"yyyy-Www" shorthand the picker itself shows (no .NET
    /// format token exists for either) — set <see cref="DateFormat"/> explicitly in those two modes
    /// and it is used verbatim via <c>ToString</c> instead, which can't render the quarter/week digit.
    /// Falls back to the bound property's <c>[DisplayFormat(DataFormatString = "…")]</c> when unset,
    /// ahead of the mode-derived default -- see <see cref="EffectiveDateFormat"/>.</summary>
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
    /// <summary>
    /// Accessible name of the picker's dropdown dialog. Null (default) derives it from the resolved
    /// field label — "Choose Birth Date" — rather than <see cref="DatePicker"/>'s constant "Choose
    /// date", which made every date popup on a form announce identically no matter which field
    /// opened it. Set explicitly to localize or to name it something else entirely.
    /// </summary>
    [Parameter] public string? DialogLabel { get; set; }
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
    /// (default) derives it from <see cref="EffectiveType"/> exactly as before (see <see cref="PickerMode"/>);
    /// set this explicitly to reach <see cref="DatePickerMode.Week"/>, <see cref="DatePickerMode.Quarter"/>,
    /// or <see cref="DatePickerMode.Year"/> — <see cref="InputDateType"/> has no equivalents for those
    /// three (and <see cref="EditDateNative{T}"/>'s own <c>Type</c> stays untouched: it drives a native
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
    [Parameter] public bool ShowToday { get; set; } = true;
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
    /// <inheritdoc cref="DatePicker.WeekLabel"/>
    [Parameter] public string WeekLabel { get; set; } = "Week";
    /// <inheritdoc cref="DatePicker.FormatHintLabel"/>
    [Parameter] public string FormatHintLabel { get; set; } = "Format:";
    /// <inheritdoc cref="DatePicker.RangeHintMinLabel"/>
    [Parameter] public string RangeHintMinLabel { get; set; } = "Earliest date:";
    /// <inheritdoc cref="DatePicker.RangeHintMaxLabel"/>
    [Parameter] public string RangeHintMaxLabel { get; set; } = "Latest date:";

    /// <summary>
    /// The picker input's <c>autocomplete</c> token (see <see cref="DatePicker.Autocomplete"/>).
    /// Null (default) falls back to the bound property's <c>[Autocomplete]</c>, then to the picker's
    /// own <c>"off"</c>. The model-attribute fallback and the parameter are the same pair
    /// <c>EditString</c> offers — a date-of-birth field needs <c>autocomplete="bday"</c> to satisfy
    /// WCAG 1.3.5, and the consumer's own splatted attributes can't supply it (they land on the
    /// picker's outer wrapper, not its input).
    /// </summary>
    [Parameter] public string? Autocomplete { get; set; }

    string EffectiveInputLabel => InputLabel ?? Label ?? _attributes.GetLabelText(_fieldIdentifier);

    // The dialog's name, derived from the SAME resolved field label the input's own name comes from
    // (one property away) instead of the picker's constant "Choose date" -- with three date fields on
    // a form, all three popups announced identically and nothing said which one had opened. The
    // explicit parameter stays the localization override.
    string EffectiveDialogLabel => DialogLabel ?? $"Choose {EffectiveInputLabel}";

    // Null unless the consumer named the input explicitly: pointing at FormLabel's lbltext-{id}
    // naming anchor keeps the input's accessible name identical to the label's own visible text
    // (live, and excluding the tooltip trigger inside the same <label>). aria-labelledby WINS over
    // aria-label, so an explicit InputLabel has to suppress it or the override would be inert.
    string? EffectiveAriaLabelledBy => InputLabel is null ? $"lbltext-{_id}" : null;

    // The autocomplete token actually forwarded: the parameter, else the model's own [Autocomplete].
    // Null is preserved so the picker's "off" default still applies -- see DatePicker.Autocomplete.
    string? EffectiveAutocomplete => Autocomplete ?? _attributes.Autocomplete();

    /// <summary>
    /// Resolves <see cref="Placeholder"/> against the bound property's <see cref="PlaceholderAttribute"/>/
    /// <see cref="DisplayAttribute"/> <c>Prompt</c> fallback (see <see cref="AttributesHelper.Placeholder"/>).
    /// Null is intentional and must be preserved when neither source supplies text: forwarding null
    /// (rather than substituting a literal) is what lets the inner <see cref="DatePicker"/>'s own
    /// mode-derived default (e.g. "Select date", "Select month") still apply, exactly as it does today.
    /// </summary>
    string? EffectivePlaceholder => Placeholder ?? _attributes.Placeholder();

    /// <summary>
    /// The format actually forwarded to the inner <see cref="DatePicker"/>: the <see cref="Format"/>
    /// parameter, else the bound property's <c>[DisplayFormat]</c> (see
    /// <see cref="AttributesHelper.FormatString"/>). Null is intentional and must be preserved when
    /// neither source supplies a format -- forwarding null is what lets <see cref="DatePicker"/>'s own
    /// mode-derived default still apply, exactly as it does today.
    /// </summary>
    string? EffectiveFormat => Format ?? _attributes.FormatString();

    /// <summary>
    /// Resolves <see cref="Min"/> against the bound property's <see cref="MinValueAttribute"/>/
    /// <see cref="RangeAttribute"/> fallback (see <see cref="AttributesHelper.MinDate"/>). Null is
    /// intentional and must be preserved when neither source supplies a bound: forwarding null is
    /// what lets the inner <see cref="DatePicker"/> impose no lower bound at all, exactly as it does
    /// today.
    /// </summary>
    DateTime? EffectiveMin => Min ?? _attributes.MinDate();
    /// <summary>
    /// Resolves <see cref="Max"/> against the bound property's <see cref="MaxValueAttribute"/>/
    /// <see cref="RangeAttribute"/> fallback (see <see cref="AttributesHelper.MaxDate"/>). Same
    /// null-preserving contract as <see cref="EffectiveMin"/>.
    /// </summary>
    DateTime? EffectiveMax => Max ?? _attributes.MaxDate();

    /// <summary>
    /// The type actually used to resolve <see cref="PickerMode"/>: the <see cref="Type"/> parameter,
    /// else the bound property's <c>[DataType(DataType.Date/DateTime/Time)]</c> (see
    /// <see cref="AttributesHelper.DateInputType"/>), else <see cref="InputDateType.Date"/> -- the
    /// same default the parameter used to carry directly.
    /// </summary>
    InputDateType EffectiveType => Type ?? _attributes.DateInputType() ?? InputDateType.Date;

    // Type -> DatePickerMode. The inner DatePicker only knows Mode; Type is EditDate's own
    // parameter name/shape (matching EditDateNative<T>'s Type exactly) so the two controls share one mental
    // model for "what does this field pick" regardless of which UX backs it. Mode (above) overrides
    // this outright when set -- EffectiveMode is what actually reaches the picker and what every
    // read-only-display default below keys off of.
    DatePickerMode PickerMode => EffectiveType switch
    {
        InputDateType.Date => DatePickerMode.Date,
        InputDateType.DateTimeLocal => DatePickerMode.DateTime,
        InputDateType.Month => DatePickerMode.Month,
        InputDateType.Time => DatePickerMode.Time,
        _ => DatePickerMode.Date
    };

    DatePickerMode EffectiveMode => Mode ?? PickerMode;

    // The explicit-override surface for the read-only display format: the DateFormat parameter, else
    // the bound property's own [DisplayFormat]. Shared by EffectiveDateFormat (as the layer ahead of
    // the mode-derived default) and GetDisplayValue's Quarter/Week gate below -- an attribute-supplied
    // format must behave exactly like an explicit DateFormat (used verbatim, bypassing the
    // quarter/week shorthand), not silently get ignored in those two modes.
    string? FormatOverride => DateFormat ?? _attributes.FormatString();

    // The read-only display format: the override above, else the same per-mode default the inner
    // DatePicker derives (PickerMath.ModeDisplayFormat -- shared so read-only and edit mode can't
    // disagree about the Time/DateTime portion), but over this control's own dash-separated bases
    // rather than the picker's slashed ones. Quarter/Week have no .NET format token for their own
    // display -- GetDisplayValue below special-cases them via PickerMath.ReadOnlyDisplay's shared
    // FormatQuarterDisplay/FormatWeekDisplay instead of ever calling ToString(EffectiveDateFormat)
    // for either, so that helper's "yyyy" for those two is never actually rendered here.
    string EffectiveDateFormat =>
        FormatOverride ?? PickerMath.ModeDisplayFormat(EffectiveMode, "MM-dd-yyyy", "MM-yyyy", Use12Hours, ShowSeconds);

    // The picker sets the value through its own ValueChanged callback, not string parsing — mirrors
    // EditSelectSearch's contract for a wrapped UI-kit engine. Binding to CurrentValueAsString (the
    // debug bound-value display excepted, which only ever reads it) is unsupported.
    protected override bool TryParseValueFromString(string? value, out T result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"{nameof(EditDate<T>)} does not parse string input; it binds via the DatePicker value callback.");

    // Dedicated store for OnPickerParseErrorAsync's message -- a separate instance from whatever
    // DataAnnotationsValidator (or any other validator) already maintains for this same EditContext,
    // since this control can't route a parse failure through TryParseValueFromString (see its own
    // NotSupportedException below) the way InputBase's built-in mechanism would. Multiple independent
    // ValidationMessageStores over one EditContext compose fine -- each only ever touches the entries
    // it added itself, so clearing this one can never drop a DataAnnotations message and vice versa.
    ValidationMessageStore? _parseErrorMessages;

    // Raised by the inner DatePicker when a typed commit can't be parsed as a date at all (see
    // DatePicker.OnParseError's doc comment for exactly which failures reach here).
    Task OnPickerParseErrorAsync(string text) => AddFieldErrorAsync(ParsingErrorMessage);

    // Raised by the inner DatePicker when a typed commit parses fine but the Min/Max/DisabledDate/
    // DisabledTime guards refuse it (DatePicker.OnRangeError). Same store, same notify, different
    // message -- the refusal used to reach the form layer through no channel at all, so nothing
    // showed, nothing was announced, and aria-invalid stayed false. The offending text is discarded
    // for the same reason the parse path discards it: {0} is the field name, not the text.
    Task OnPickerRangeErrorAsync(string text) => AddFieldErrorAsync(RangeErrorMessage);

    // Mirrors the shape of InputBase<T>.SetCurrentValueAsStringAsync's own built-in parsing-error
    // path -- clear this field's prior entry, add the formatted message, and notify -- just against a
    // store this control owns instead of InputBase's private one, since that path is never reached
    // here. Shared by both rejection kinds above so they can't drift apart in everything but wording.
    Task AddFieldErrorAsync(string messageFormat)
    {
        if (EditContext is null) return Task.CompletedTask;
        _parseErrorMessages ??= new ValidationMessageStore(EditContext);
        _parseErrorMessages.Clear(FieldIdentifier);
        _parseErrorMessages.Add(FieldIdentifier,
            string.Format(CultureInfo.InvariantCulture, messageFormat, FieldIdentifier.FieldName));
        // CurrentValue never changed (the bad text was reverted, not committed) -- notify explicitly,
        // same as InputBase's own equivalent failure path, so FormOptions/consumers watching field
        // changes still see this as a touch.
        EditContext.NotifyFieldChanged(FieldIdentifier);
        EditContext.NotifyValidationStateChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drops any outstanding parse-error message when the control unmounts. The store's entries live
    /// on the <see cref="EditContext"/>, not on this component, so a control removed while showing a
    /// parse error (an <c>IsHidden</c>/<see cref="HidingMode"/> toggle, a tab switch) would otherwise
    /// leave the message behind for a <c>ValidationView</c> summary to link to a field that no longer
    /// renders. Only ever touches entries this control added -- see <see cref="_parseErrorMessages"/>.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _parseErrorMessages is not null && EditContext is not null)
        {
            _parseErrorMessages.Clear(FieldIdentifier);
            EditContext.NotifyValidationStateChanged();
        }
        base.Dispose(disposing);
    }

    // The inner <DatePicker> is DateTime?-only (a date-only midnight value, or -- in Time/DateTime
    // mode -- a time-of-day/date+time still carried in a DateTime) regardless of T -- these bridge
    // CurrentValue to and from it. Boxing+pattern-match on the runtime type (not typeof(T) == checks)
    // mirrors EditDateNative<T>'s GetDisplayValue/IsValueDefault, which already rely on the CLR boxing a
    // non-null Nullable<T> as its underlying T (so "DateTime dt" matches DateTime? too).
    //
    // A DEFAULT date value reads as empty, not as 0001-01-01: a non-nullable T can't hold null, so
    // "Clear date" writes default(T) back through TryFromPickerValue -- and the picker then displayed
    // "01/01/0001" and went on offering its clear button, so the control claimed to hold a date that
    // clearing had just removed and the clear appeared to do nothing. This is the same "semantically
    // empty" rule IsValueDefault already applies for HidingMode (EditControlInit.IsDateValueDefault),
    // applied one layer further in. TimeOnly is deliberately EXEMPT: default(TimeOnly) is midnight,
    // an entirely legitimate time-of-day, so blanking it would make 00:00 unrepresentable.
    DateTime? PickerValue => CurrentValue switch
    {
        null => null,
        DateTime dt => dt == default ? null : dt,
        // Face value, matching how EditDateNative displays a DateTimeOffset via BindConverter.FormatValue --
        // no UTC/Local conversion, just the same clock time the offset carries.
        DateTimeOffset dto => dto == default ? null : dto.DateTime,
        DateOnly d => d == default ? null : d.ToDateTime(TimeOnly.MinValue),
        TimeOnly t => DateTime.Today.Add(t.ToTimeSpan()),
        _ => throw UnsupportedType()
    };

    // The reverse direction can't pattern-match on the incoming value (it's always DateTime?) --
    // typeof(T) picks which of the eight supported shapes to produce. typeof(T) == checks over a
    // handful of concrete value types are fully trim/AOT-safe (no reflection over T's members).
    void OnValueChanged(DateTime? value)
    {
        if (!TryFromPickerValue(value, out var result))
        {
            // The picker's value is a well-formed DateTime, but the DateTimeOffset it would produce
            // overflows the UTC-instant range (see TryFromPickerValue's DateTimeOffset arm) -- treat
            // this exactly like any other unparseable entry instead of letting the constructor throw
            // an ArgumentOutOfRangeException out of a rendering circuit: add the same ParsingErrorMessage
            // and leave CurrentValue untouched (the picker itself already reverted its own display).
            _ = OnPickerParseErrorAsync(FieldIdentifier.FieldName);
            return;
        }

        // A value only ever reaches here once the picker itself successfully committed it -- clear
        // any stale parse-error message from a prior unparseable entry (see OnPickerParseErrorAsync)
        // so it can never outlive the very next valid commit.
        if (_parseErrorMessages is not null && EditContext is not null)
        {
            _parseErrorMessages.Clear(FieldIdentifier);
            EditContext.NotifyValidationStateChanged();
        }
        CurrentValue = result;
    }

    // Returns false only for the DateTimeOffset/DateTimeOffset? arm, and only when the conversion
    // below would throw -- every other T always succeeds.
    static bool TryFromPickerValue(DateTime? value, out T result)
    {
        if (typeof(T) == typeof(DateTime)) { result = (T)(object)(value ?? default(DateTime)); return true; }
        if (typeof(T) == typeof(DateTime?)) { result = (T)(object)value!; return true; }
        // The picker never sets Kind -- its values carry Kind.Unspecified (or, from the Time-mode
        // DateTime.Today anchor, Kind.Local). Both assume the local offset when constructing a
        // DateTimeOffset, matching BindConverter's parse semantics for datetime-local text. Computed
        // only inside the DateTimeOffset arms: within the local offset of DateTime.MinValue (a typed
        // year-1 date in an east-of-UTC zone) -- or DateTime.MaxValue in a west-of-UTC zone -- the
        // UTC instant falls outside DateTimeOffset's own range, so TryToDateTimeOffset (below) guards
        // it instead of letting the constructor throw, and no other T needs the guard.
        if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
        {
            DateTimeOffset? dto = null;
            if (value is { } vo)
            {
                if (!TryToDateTimeOffset(vo, TimeZoneInfo.Local.GetUtcOffset(vo), out var converted))
                {
                    result = default!;
                    return false;
                }
                dto = converted;
            }
            result = typeof(T) == typeof(DateTimeOffset) ? (T)(object)(dto ?? default(DateTimeOffset)) : (T)(object)dto!;
            return true;
        }
        DateOnly? dateOnly = value is { } v ? DateOnly.FromDateTime(v) : null;
        if (typeof(T) == typeof(DateOnly)) { result = (T)(object)(dateOnly ?? default(DateOnly)); return true; }
        if (typeof(T) == typeof(DateOnly?)) { result = (T)(object)dateOnly!; return true; }
        TimeOnly? timeOnly = value is { } vt ? TimeOnly.FromDateTime(vt) : null;
        if (typeof(T) == typeof(TimeOnly)) { result = (T)(object)(timeOnly ?? default(TimeOnly)); return true; }
        if (typeof(T) == typeof(TimeOnly?)) { result = (T)(object)timeOnly!; return true; }
        throw UnsupportedType();
    }

    /// <summary>
    /// The exact bounds check <c>new DateTimeOffset(DateTime, TimeSpan)</c> performs internally
    /// (<c>value.Ticks - offset.Ticks</c> must stay within <see cref="DateTime.MinValue"/>/
    /// <see cref="DateTime.MaxValue"/>), exposed as a non-throwing TryXxx so a year-1/year-9999 local
    /// value under a non-UTC offset can be treated as an ordinary conversion failure instead of an
    /// unhandled <see cref="ArgumentOutOfRangeException"/>. <paramref name="offset"/> is a parameter
    /// (rather than this reading <see cref="TimeZoneInfo.Local"/> itself) purely so the boundary math
    /// is unit-testable independent of the host machine's own time zone.
    /// </summary>
    static bool TryToDateTimeOffset(DateTime value, TimeSpan offset, out DateTimeOffset result)
    {
        var utcTicks = value.Ticks - offset.Ticks;
        if (utcTicks < DateTime.MinValue.Ticks || utcTicks > DateTime.MaxValue.Ticks)
        {
            result = default;
            return false;
        }
        result = new DateTimeOffset(value, offset);
        return true;
    }

    static NotSupportedException UnsupportedType() => new(
        $"EditDate<{typeof(T)}> is not supported -- supported types are DateTime, DateTime?, " +
        "DateTimeOffset, DateTimeOffset?, DateOnly, DateOnly?, TimeOnly, and TimeOnly?.");

    // The validation-state ARIA goes through DatePicker's dedicated Aria* parameters (straight onto
    // its actual <input>); this splat carries only the consumer's own attributes plus the state
    // classes, landing on the picker's outer wrapper (its documented AdditionalAttributes target).
    // CssClass is InputBase's own merge of the raw consumer "class" with the EditContext's
    // modified/valid/invalid classes -- overwriting the raw consumer value with it is what gives the
    // wrapper the same validation-state styling hooks every other control's native input gets via
    // `class="edit-input ... @CssClass"`. Shared builder: see BuildPickerAttributes's own remarks for
    // why this and EditDateRange's twin differ only in that class source.
    IReadOnlyDictionary<string, object> PickerAttributes => EditControlInit.BuildPickerAttributes(AdditionalAttributes, CssClass);

    string GetDisplayValue()
    {
        if (CurrentValue is null) return string.Empty;
        // DTE-15: a default value on a non-nullable binding reads as "no value" in edit mode, so it has
        // to read that way here too -- otherwise the same model renders "Not Set" in the editor and a
        // date in read-only. This early return is also what keeps the two in step mechanically: the
        // Week/Quarter branch below is driven by PickerValue, which DTE-15 already nulls for a default,
        // and falling through with that null lands on the verbatim ToString path ("0001") rather than
        // on either intended answer. TimeOnly is exempt for the same reason PickerValue exempts it --
        // default(TimeOnly) is midnight, a legitimate time.
        if (CurrentValue is DateTime { Ticks: 0 } or DateOnly { DayNumber: 0 }
            || (CurrentValue is DateTimeOffset dtoDefault && dtoDefault == default))
            return string.Empty;
        // Gregorian-forced like the picker's own display, so read-only and edit mode can never
        // disagree about the year under a non-Gregorian-default culture (th-TH, ar-SA).
        var culture = GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);
        // Quarter/Week's null-DateFormat display has no .NET format token to route through
        // ToString(EffectiveDateFormat) -- PickerMath.ReadOnlyDisplay (shared with EditDateRange)
        // special-cases those two through the very FormatQuarterDisplay/FormatWeekDisplay the inner
        // DatePicker's own display routes through, not duplicated regex/format logic here. The
        // shorthand value is PickerValue, the same DateTime? bridge the picker itself would see, and
        // passing null for it (an explicit DateFormat) is what falls through to the verbatim ToString
        // path below, matching the picker's own Format contract. FirstDayOfWeek is resolved against
        // `culture` inside the helper -- there's no picker instance to ask once this control is in
        // read-only mode (no <DatePicker> renders at all then).
        return PickerMath.ReadOnlyDisplay(EffectiveMode, FormatOverride is null ? PickerValue : null, culture,
            FirstDayOfWeek,
            () => CurrentValue switch
            {
                DateTime dt => dt.ToString(EffectiveDateFormat, culture),
                DateTimeOffset dto => dto.ToString(EffectiveDateFormat, culture),
                DateOnly d => d.ToString(EffectiveDateFormat, culture),
                TimeOnly t => t.ToString(EffectiveDateFormat, culture),
                _ => string.Empty
            },
            // The FormatException fallback for a consumer-supplied DateFormat .NET rejects -- must
            // stay Gregorian-forced too (the same `culture`), or a th-TH/ar-SA consumer would see edit
            // mode's picker disagree with THIS degraded read-only year, the exact mismatch the
            // primary formatter above already guards against. Mirrors EditDateRange.FormatOne's twin.
            () => CurrentValue switch
            {
                DateTime dt => dt.ToString(culture),
                DateTimeOffset dto => dto.ToString(culture),
                DateOnly d => d.ToString(culture),
                TimeOnly t => t.ToString(culture),
                _ => string.Empty
            });
    }

    // default(DateTime)/default(DateTimeOffset)/default(DateOnly)/default(TimeOnly) count as
    // semantically empty for date controls -- mirrors EditDateNative<T>'s IsValueDefault override
    // (and EditDateRange's per-field variant) through the one shared helper, including the same
    // boxed-Nullable<T> pattern-match trick (see PickerValue above) so any of this control's four
    // nullable shapes falls through to the EqualityComparer arm on null.
    protected override bool IsValueDefault() => EditControlInit.IsDateValueDefault(CurrentValue);
}
