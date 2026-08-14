namespace Controls;

/// <summary>
/// Composite two-field date-range edit control, backed by the <see cref="DateRangePicker"/> UI-kit
/// calendar dropdown. Binds two independent <c>DateTime?</c> model properties — <c>@bind-Start</c> /
/// <c>@bind-End</c> — since <see cref="Microsoft.AspNetCore.Components.Forms.InputBase{TValue}"/> only
/// supports a single bound value. Adds form binding, validation (for both fields), one shared label,
/// a read-only view, and <see cref="FormOptions"/> support on top of DateRangePicker's type-or-pick UX.
/// </summary>
/// <remarks>
/// <para>
/// Shares <see cref="EditControlParametersBase"/> with <see cref="EditControlListBase{TItem}"/> (the
/// <see cref="IEditControl"/> parameters plus the three cascading options) but not
/// <see cref="EditControlListBase{TItem}"/> itself, since that base is hard-wired to a single bound
/// <c>List&lt;TItem&gt;</c>, which doesn't fit two independent scalar fields. Like that base, this is a
/// plain <see cref="ComponentBase"/>, not an <c>InputBase</c>, with hand-rolled <see cref="FormOptions"/>
/// registration and <see cref="EditContext"/> validation-state subscription.
/// </para>
/// <para>
/// One <see cref="FormLabel"/> renders for the whole control, associated (<c>label[for]</c>) with the
/// Start input — the only one <see cref="DateRangePicker"/> exposes an <c>Id</c> for. The composite
/// field additionally takes <c>role="group"</c> named from that label's <c>lbltext-{id}</c> anchor
/// (<see cref="DateRangePicker.GroupLabelledBy"/>), which is what ties the End input back to the
/// label the association can't reach it from. Both fields still register independently with
/// <see cref="FormOptions"/> and each gets its own <see cref="FieldValidationDisplay"/>, so a
/// validation failure on either Start or End shows its own message and links from a
/// <c>ValidationView</c> summary — anchored on the End input in edit mode, and (since no End element
/// exists there) on the single read-only value in read-only mode. Either field being required raises
/// the one shared star, while <c>aria-required</c> stays strictly per-input.
/// </para>
/// <para>
/// Validation-state ARIA reaches both actual <c>&lt;input&gt;</c>s through
/// <see cref="DateRangePicker"/>'s per-input <c>StartAria*</c>/<c>EndAria*</c> parameters, each
/// reflecting its own field's state (a Start error never marks the End input invalid, and vice
/// versa); each input's <c>aria-errormessage</c> references its own
/// <see cref="FieldValidationDisplay"/> message, and each input's <c>aria-describedby</c> starts with
/// that same message and then references the ONE <see cref="IEditControl.Description"/>/
/// <see cref="IEditControl.Tooltip"/> pair this control renders (see <c>BuildEndDescribedBy</c>) —
/// a reference, not ownership, so both inputs get instructions written once. The visible
/// <see cref="FormLabel"/> associates
/// (<c>label[for]</c>) with the Start input, but <c>aria-label</c> wins the accessible-name
/// computation over that association (per the AccName spec) — so both inputs' accessible names come
/// entirely from <see cref="StartInputLabel"/>/<see cref="EndInputLabel"/>, which default to the
/// resolved <see cref="IEditControl.Label"/> plus a " start"/" end" suffix (falling back to each field's own
/// auto-derived label when <see cref="IEditControl.Label"/> isn't set). The suffix keeps the two names unique from
/// each other while both still containing the visible label text (WCAG 2.5.3 Label in Name). The End
/// input also carries its own id for <c>ValidationView</c> links.
/// </para>
/// </remarks>
public partial class EditDateRange : IDisposable
{
    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Start</c>/<c>@bind-End</c> alone supply
    /// the accessors those used to require. This inert stub exists only so a leftover
    /// <c>Field="..."</c> attribute is a compile error instead of silently building and throwing at
    /// runtime. Remove the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Start/@bind-End are sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<DateTime?>>? Field { get; set; }

    /// <summary> Start of the bound range. Supports <c>@bind-Start</c>.</summary>
    [Parameter] public DateTime? Start { get; set; }
    /// <summary> Raised with the new start when it changes (supports <c>@bind-Start</c>).</summary>
    [Parameter] public EventCallback<DateTime?> StartChanged { get; set; }
    /// <summary>
    /// Compiler-populated by <c>@bind-Start</c> — supplies the accessor <see cref="OnInitialized"/>
    /// needs to derive the Start <see cref="FieldIdentifier"/>, the control's resolved id, and its label.
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<DateTime?>>? StartExpression { get; set; }

    /// <summary> End of the bound range. Supports <c>@bind-End</c>.</summary>
    [Parameter] public DateTime? End { get; set; }
    /// <summary> Raised with the new end when it changes (supports <c>@bind-End</c>).</summary>
    [Parameter] public EventCallback<DateTime?> EndChanged { get; set; }
    /// <summary>
    /// Compiler-populated by <c>@bind-End</c> — supplies the accessor <see cref="OnInitialized"/> needs
    /// to derive the End <see cref="FieldIdentifier"/> (its own validation messages and attribute scan
    /// are independent of Start's).
    /// </summary>
    [Parameter, EditorRequired] public Expression<Func<DateTime?>>? EndExpression { get; set; }

    /// <summary>
    /// Optional label used in the End field's own validation messages (e.g. "End Date is required").
    /// Null (default) auto-generates from the End property's <c>[DisplayName]</c>/name, same precedence
    /// as the primary <see cref="IEditControl.Label"/>. The visible <see cref="FormLabel"/> for the whole control
    /// always derives from Start, never this.
    /// </summary>
    [Parameter] public string? EndLabel { get; set; }

    /// <inheritdoc cref="DateRangePicker.Presets"/>
    [Parameter] public IReadOnlyList<DateRangePreset>? Presets { get; set; }
    /// <summary>
    /// Lower bound forwarded to the inner <see cref="DateRangePicker"/>, shared by both panels. Null
    /// (default) falls back to the Start property's own <see cref="MinValueAttribute"/>/
    /// <see cref="RangeAttribute"/> minimum, then to the End property's (see
    /// <see cref="EffectiveMin"/>) -- mirrors <see cref="StartPlaceholder"/>'s per-field resolution
    /// except that Min/Max apply to the single calendar both fields share, so each bound falls back to
    /// the OTHER field's attributes rather than yielding independent per-input values.
    /// </summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>
    /// Upper bound forwarded to the inner <see cref="DateRangePicker"/>, shared by both panels. Null
    /// (default) falls back to the End property's own <see cref="MaxValueAttribute"/>/
    /// <see cref="RangeAttribute"/> maximum, then to the Start property's (see
    /// <see cref="EffectiveMax"/>) -- the mirror image of <see cref="Min"/>'s Start-first preference,
    /// since the natural annotation pairs <c>[MinValue]</c> with Start and <c>[MaxValue]</c> with End.
    /// </summary>
    [Parameter] public DateTime? Max { get; set; }
    /// <summary>
    /// Display and primary parse format forwarded to the inner <see cref="DateRangePicker"/>. Null
    /// (default) falls back to the Start property's <c>[DisplayFormat]</c>, then the End property's
    /// (see <see cref="EffectiveFormat"/>), then <see cref="Mode"/>'s own default there (see
    /// <see cref="DateRangePicker.Format"/>) instead of a fixed literal -- unlike this parameter's own
    /// former hardcoded "MM/dd/yyyy" default, which would otherwise silently override every OTHER
    /// mode's per-mode default (e.g. Month's "MM/yyyy") the moment <see cref="Mode"/> forwarded
    /// anything but <see cref="DatePickerMode.Date"/>.
    /// </summary>
    [Parameter] public string? Format { get; set; }
    /// <summary>
    /// Placeholder text forwarded to the inner <see cref="DateRangePicker"/>'s Start input. Null
    /// (default) falls back to the Start property's <see cref="PlaceholderAttribute"/> or
    /// <see cref="DisplayAttribute"/> <c>Prompt</c> (see <see cref="EffectiveStartPlaceholder"/>), then
    /// to <see cref="DateRangePicker"/>'s own mode-derived default (its internal
    /// <c>DefaultPlaceholder</c>, the uppercased effective format).
    /// </summary>
    [Parameter] public string? StartPlaceholder { get; set; }
    /// <summary>
    /// Placeholder text forwarded to the inner <see cref="DateRangePicker"/>'s End input. Resolves
    /// against the End property's own attributes independently of <see cref="StartPlaceholder"/> (see
    /// <see cref="EffectiveEndPlaceholder"/>) — same fallback chain as <see cref="StartPlaceholder"/>.
    /// </summary>
    [Parameter] public string? EndPlaceholder { get; set; }
    /// <inheritdoc cref="DateRangePicker.AllowClear"/>
    [Parameter] public bool AllowClear { get; set; } = true;
    /// <inheritdoc cref="DateRangePicker.Width"/>
    [Parameter] public string? Width { get; set; }
    /// <inheritdoc cref="DateRangePicker.Size"/>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;
    /// <inheritdoc cref="DateRangePicker.FirstDayOfWeek"/>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <inheritdoc cref="DateRangePicker.Mode"/>
    [Parameter] public DatePickerMode Mode { get; set; } = DatePickerMode.Date;
    /// <inheritdoc cref="DateRangePicker.ShowWeekNumbers"/>
    [Parameter] public bool ShowWeekNumbers { get; set; }
    /// <inheritdoc cref="DateRangePicker.DisabledDate"/>
    [Parameter] public Func<DateTime, bool>? DisabledDate { get; set; }
    /// <inheritdoc cref="DateRangePicker.StartDisabledTime"/>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? StartDisabledTime { get; set; }
    /// <inheritdoc cref="DateRangePicker.EndDisabledTime"/>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? EndDisabledTime { get; set; }
    /// <inheritdoc cref="DateRangePicker.HideDisabledTimeOptions"/>
    [Parameter] public bool HideDisabledTimeOptions { get; set; }
    /// <inheritdoc cref="DateRangePicker.ShowSeconds"/>
    [Parameter] public bool ShowSeconds { get; set; } = true;
    /// <inheritdoc cref="DateRangePicker.HourStep"/>
    [Parameter] public int HourStep { get; set; } = 1;
    /// <inheritdoc cref="DateRangePicker.MinuteStep"/>
    [Parameter] public int MinuteStep { get; set; } = 1;
    /// <inheritdoc cref="DateRangePicker.SecondStep"/>
    [Parameter] public int SecondStep { get; set; } = 1;
    /// <inheritdoc cref="DateRangePicker.Use12Hours"/>
    [Parameter] public bool Use12Hours { get; set; }
    /// <inheritdoc cref="DateRangePicker.OkText"/>
    [Parameter] public string OkText { get; set; } = "OK";
    /// <inheritdoc cref="DateRangePicker.ExtraFooter"/>
    [Parameter] public RenderFragment? ExtraFooter { get; set; }
    /// <inheritdoc cref="DateRangePicker.DefaultViewDate"/>
    [Parameter] public DateTime? DefaultViewDate { get; set; }

    /// <summary>
    /// Error message format string used when a typed entry in EITHER input can't be parsed at all --
    /// i.e. the inner <see cref="DateRangePicker"/> raises <see cref="DateRangePicker.OnStartParseError"/>/
    /// <see cref="DateRangePicker.OnEndParseError"/> (a well-formed value merely rejected by
    /// <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<see cref="StartDisabledTime"/>/
    /// <see cref="EndDisabledTime"/> does not). <c>{0}</c> is replaced with the FAILING field's own name,
    /// so one format string serves both endpoints -- same formatting as
    /// <see cref="EditDate{T}.ParsingErrorMessage"/>. Surfaces as a validation message via a dedicated
    /// <see cref="ValidationMessageStore"/> scoped to that endpoint's own <see cref="FieldIdentifier"/>
    /// (see <see cref="OnStartParseErrorAsync"/>), since this control never parses strings itself -- the
    /// picker sets values through its own per-endpoint value callbacks. Each endpoint's message is
    /// cleared the moment a valid value next commits for THAT endpoint (see <see cref="OnStartChanged"/>/
    /// <see cref="OnEndChanged"/>), independently of the other's.
    /// </summary>
    [Parameter] public string ParsingErrorMessage { get; set; } = "The {0} field must be a date.";

    /// <summary>
    /// Error message format string used when a typed entry in EITHER input parses into a perfectly
    /// well-formed value that the inner <see cref="DateRangePicker"/> nonetheless REFUSES — one
    /// rejected by <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/
    /// <see cref="StartDisabledTime"/>/<see cref="EndDisabledTime"/>
    /// (<see cref="DateRangePicker.OnStartRangeError"/>/<see cref="DateRangePicker.OnEndRangeError"/>).
    /// <c>{0}</c> is replaced with the FAILING field's own name, so one format string serves both
    /// endpoints — same formatting as <see cref="ParsingErrorMessage"/>, same store, and cleared for
    /// that endpoint alone the moment a valid value next commits for it.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ParsingErrorMessage"/> because the two are genuinely different
    /// situations: "that isn't a date" versus "that IS a date, but not one this field accepts".
    /// Before this existed the second case was silent in every channel — the picker reverted the
    /// text, neither bound value changed, <c>NotifyFieldChanged</c> never fired, no validator ran.
    /// Mirrors <see cref="EditDate{T}.RangeErrorMessage"/>.
    /// </remarks>
    [Parameter] public string RangeErrorMessage { get; set; } = "The {0} field must be an allowed date.";

    /// <summary> Format string for the read-only "start - end" value display. Null (default) picks
    /// <see cref="Mode"/>'s own default (mirrors <see cref="EditDate{T}.DateFormat"/>'s identical
    /// per-mode contract): <c>Date</c> "MM-dd-yyyy" (the original, unchanged default) · <c>Month</c>
    /// "MM-yyyy" · <c>DateTime</c> "MM-dd-yyyy " plus <c>Time</c>'s own string · <c>Time</c> "HH:mm:ss"
    /// (<see cref="ShowSeconds"/> false drops ":ss"; <see cref="Use12Hours"/> switches to the 12-hour
    /// "h:mm tt"/"h:mm:ss tt" forms) · <c>Year</c> "yyyy" · <c>Quarter</c>/<c>Week</c> render the same
    /// "yyyy-Qn"/"yyyy-Www" shorthand the picker itself shows (no .NET format token exists for either)
    /// — set <see cref="DateFormat"/> explicitly in those two modes and it is used verbatim via
    /// <c>ToString</c> instead, which can't render the quarter/week digit. Falls back to the Start
    /// property's <c>[DisplayFormat]</c>, then the End property's, ahead of the mode-derived default
    /// -- see <see cref="EffectiveDateFormat"/>.</summary>
    [Parameter] public string? DateFormat { get; set; }

    /// <summary>
    /// Captures unmatched attributes (a consumer's <c>class</c>/<c>style</c>/<c>data-*</c>) so they can
    /// be forwarded onto <see cref="DateRangePicker"/>'s own <c>AdditionalAttributes</c> splat — the
    /// same role <see cref="EditControlListBase{TItem}.AdditionalAttributes"/> plays for list controls.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // Accessible-name parameters forwarded to the inner DateRangePicker. Defaults mirror
    // DateRangePicker's own literal defaults except StartInputLabel/EndInputLabel, which resolve
    // through EffectiveStartInputLabel/EffectiveEndInputLabel below instead of a literal default.

    /// <summary>
    /// Accessible name of the Start input — the one <see cref="DateRangePicker"/> associates a
    /// <c>label[for]</c> with. Null (default) resolves to <see cref="IEditControl.Label"/> + " start" when
    /// <see cref="IEditControl.Label"/> is set, else the Start field's own auto-derived label (<c>[DisplayName]</c>/
    /// <c>[Display(Name)]</c>/property name) — see the class remarks for why the suffix is needed even
    /// though the visible <see cref="FormLabel"/> associates with this same input. Override to set
    /// something else entirely.
    /// </summary>
    [Parameter] public string? StartInputLabel { get; set; }
    /// <summary>
    /// Accessible name of the End input. Null (default) resolves to <see cref="IEditControl.Label"/> + " end" when
    /// <see cref="IEditControl.Label"/> is set, else the End field's own auto-derived label — mirrors
    /// <see cref="StartInputLabel"/>'s resolution so the two names stay unique from each other while
    /// both contain the visible label text (WCAG 2.5.3 Label in Name). Override to localize or to set
    /// something else entirely.
    /// </summary>
    [Parameter] public string? EndInputLabel { get; set; }
    /// <summary>
    /// Accessible name of the picker's dropdown dialog. Null (default) derives it from the resolved
    /// control label — "Choose Stay Dates" — rather than <see cref="DateRangePicker"/>'s constant
    /// "Choose date range", which made every range popup on a form announce identically no matter
    /// which field opened it. Set explicitly to localize or to name it something else entirely.
    /// </summary>
    [Parameter] public string? DialogLabel { get; set; }
    /// <summary>
    /// The Start input's <c>autocomplete</c> token (see <see cref="DateRangePicker.StartAutocomplete"/>).
    /// Null (default) falls back to the Start property's own <c>[Autocomplete]</c>, then to the
    /// picker's <c>"off"</c>. Per-endpoint, resolved against each field's OWN attributes, exactly
    /// like <see cref="StartPlaceholder"/>.
    /// </summary>
    [Parameter] public string? StartAutocomplete { get; set; }
    /// <summary>Same contract as <see cref="StartAutocomplete"/>, for the End input.</summary>
    [Parameter] public string? EndAutocomplete { get; set; }
    /// <inheritdoc cref="DateRangePicker.MonthSelectLabel"/>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <inheritdoc cref="DateRangePicker.YearSelectLabel"/>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <inheritdoc cref="DateRangePicker.ClearLabel"/>
    [Parameter] public string ClearLabel { get; set; } = "Clear dates";
    /// <inheritdoc cref="DateRangePicker.PresetsLabel"/>
    [Parameter] public string PresetsLabel { get; set; } = "Quick ranges";
    /// <inheritdoc cref="DateRangePicker.PrevMonthLabel"/>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <inheritdoc cref="DateRangePicker.NextMonthLabel"/>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";
    /// <inheritdoc cref="DateRangePicker.PrevYearLabel"/>
    [Parameter] public string PrevYearLabel { get; set; } = "Previous year";
    /// <inheritdoc cref="DateRangePicker.NextYearLabel"/>
    [Parameter] public string NextYearLabel { get; set; } = "Next year";
    /// <inheritdoc cref="DateRangePicker.PrevDecadeLabel"/>
    [Parameter] public string PrevDecadeLabel { get; set; } = "Previous decade";
    /// <inheritdoc cref="DateRangePicker.NextDecadeLabel"/>
    [Parameter] public string NextDecadeLabel { get; set; } = "Next decade";
    /// <inheritdoc cref="DateRangePicker.HourSelectLabel"/>
    [Parameter] public string HourSelectLabel { get; set; } = "Hour";
    /// <inheritdoc cref="DateRangePicker.MinuteSelectLabel"/>
    [Parameter] public string MinuteSelectLabel { get; set; } = "Minute";
    /// <inheritdoc cref="DateRangePicker.SecondSelectLabel"/>
    [Parameter] public string SecondSelectLabel { get; set; } = "Second";
    /// <inheritdoc cref="DateRangePicker.PeriodSelectLabel"/>
    [Parameter] public string PeriodSelectLabel { get; set; } = "AM/PM";
    /// <inheritdoc cref="DateRangePicker.WeekLabel"/>
    [Parameter] public string WeekLabel { get; set; } = "Week";
    /// <inheritdoc cref="DateRangePicker.FormatHintLabel"/>
    [Parameter] public string FormatHintLabel { get; set; } = "Format:";
    /// <inheritdoc cref="DateRangePicker.RangeHintMinLabel"/>
    [Parameter] public string RangeHintMinLabel { get; set; } = "Earliest date:";
    /// <inheritdoc cref="DateRangePicker.RangeHintMaxLabel"/>
    [Parameter] public string RangeHintMaxLabel { get; set; } = "Latest date:";

    // Standard derived state — mirrors EditControlListBase's fields, duplicated per bound field. (The
    // validation-state subscription and the field-registration sequence that used to sit alongside
    // these are shared with the list base on EditControlParametersBase; only the per-field derived
    // state the markup binds directly stays here.)
    string _id = string.Empty;
    string _endId = string.Empty;
    string? _isRequired;
    List<Attribute>? _attributes;
    List<Attribute>? _endAttributes;
    FieldIdentifier _startFieldIdentifier;
    FieldIdentifier _endFieldIdentifier;
    string _errorMsgId = string.Empty;
    string _describedBy = string.Empty;
    string? _endIsRequired;
    string _endErrorMsgId = string.Empty;
    string _endDescribedBy = string.Empty;
    Func<FieldIdentifier>? _startFieldIdentifierFactory;
    Func<FieldIdentifier>? _endFieldIdentifierFactory;
    // The anchor id the End field is currently registered under (see EndAnchorId) -- tracked so a
    // runtime edit/read-only flip can move the registration, which neither of the two existing
    // re-registration triggers (a resolved-id change, an EditContext swap) would notice on its own.
    string _registeredEndAnchorId = string.Empty;

    /// <summary>
    /// The control's fully-resolved required-ness: true when EITHER bound field resolves as required.
    /// Same resolution as every other control per field (<see cref="IEditControl.IsRequired"/>
    /// parameter → <c>[Required]</c> attribute → <see cref="FormOptions.RequiredResolver"/>), OR-ed
    /// because this control renders ONE shared visible label for two fields. A <c>[Required]</c> on
    /// End alone used to mark the End input <c>aria-required</c> while the label showed no star at
    /// all, so the visual and the programmatic channels disagreed about the very same control — the
    /// sighted user saw an optional field, the screen-reader user heard a required one. The per-input
    /// <c>aria-required</c> stays per-field (see <c>_isRequired</c>/<c>_endIsRequired</c>): only the
    /// shared star is shared.
    /// </summary>
    protected bool? IsRequiredResolved => _isRequired is not null || _endIsRequired is not null;

    /// <summary> True when the Start field currently has a validation error.</summary>
    protected bool IsStartInvalid => EditContext is not null && EditContext.GetValidationMessages(_startFieldIdentifier).Any();
    /// <summary> True when the End field currently has a validation error.</summary>
    protected bool IsEndInvalid => EditContext is not null && EditContext.GetValidationMessages(_endFieldIdentifier).Any();

    /// <summary>
    /// The consumer's <c>class</c> attribute (if any) merged with the EditContext state classes
    /// (<c>modified</c>/<c>valid</c>/<c>invalid</c>) derived from <em>both</em> bound fields — mirrors
    /// <see cref="EditControlListBase{TItem}.FieldCssClass"/>, the list-control analogue, since this
    /// control is likewise not an <c>InputBase</c> and gets no <c>CssClass</c> for free. The base state
    /// comes from the Start field; if <see cref="IsEndInvalid"/> and that base state doesn't already
    /// carry <c>invalid</c>, <c>invalid</c> is added (and any <c>valid</c> token stripped, since the two
    /// are incoherent together) so an End-only validation failure still turns the shared
    /// <c>.wss-picker</c> wrapper red, not just Start's. Also forwarded onto the read-only view's
    /// <c>CssClass</c> so the edit/read-only class-forwarding contract every other control provides
    /// (EditMultiSelect, EditFile, EditChecked*) holds here too.
    /// </summary>
    /// <param name="isEndInvalid">
    /// <see cref="IsEndInvalid"/>, supplied by the caller rather than read here: the markup needs it
    /// several times per render and each read walks the EditContext's message list, so it evaluates
    /// the pair once at the top and threads the answers through. Not cached in a field — validation
    /// state changes outside the parameter lifecycle, so a field would go stale.
    /// </param>
    protected string? FieldCssClass(bool isEndInvalid)
    {
        var fieldClass = EditContext is null ? string.Empty : EditContext.FieldCssClass(_startFieldIdentifier);
        fieldClass = MergeEndInvalidState(fieldClass, isEndInvalid);
        if (AdditionalAttributes is not null &&
            AdditionalAttributes.TryGetValue("class", out var classObj) &&
            Convert.ToString(classObj, CultureInfo.InvariantCulture) is { Length: > 0 } consumerClass)
        {
            return fieldClass.Length > 0 ? $"{consumerClass} {fieldClass}" : consumerClass;
        }
        return fieldClass.Length > 0 ? fieldClass : null;
    }

    /// <summary>
    /// Folds an End-field invalid state into the Start-derived state-class string: adds <c>invalid</c>
    /// when missing and drops any <c>valid</c> token, leaving <c>modified</c> untouched. A no-op when
    /// <paramref name="isEndInvalid"/> is false or <paramref name="fieldClass"/> already has
    /// <c>invalid</c>. Split/join on space rather than a fixed literal so it tolerates whichever token
    /// order/spacing <c>EditContext.FieldCssClass</c> or a future provider produces.
    /// </summary>
    static string MergeEndInvalidState(string fieldClass, bool isEndInvalid)
    {
        if (!isEndInvalid) return fieldClass;
        var tokens = fieldClass.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (tokens.Contains("invalid")) return fieldClass;
        tokens.Remove("valid");
        tokens.Add("invalid");
        return string.Join(' ', tokens);
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    /// <summary>
    /// Both Start and End null count as "default" for the strict Null hiding modes
    /// (<see cref="HidingMode.WhenNull"/>/<see cref="HidingMode.WhenReadOnlyAndNull"/>); both
    /// null-or-<c>default(DateTime)</c> count as "default" for the NullOrDefault modes — mirrors
    /// <see cref="EditDate{T}"/>'s <c>IsValueDefault</c> override applied per field, since there's
    /// no meaningful partial-range default distinct from "nothing entered".
    /// </summary>
    protected bool ShouldShowComponent()
    {
        var isNull = Start is null && End is null;
        var isDefault = IsEmpty(Start) && IsEmpty(End);
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isDefault);
    }

    /// <summary>
    /// True when a range endpoint is either unset or the uninitialized <c>default(DateTime)</c>
    /// (0001-01-01) — the same semantically-empty value <see cref="EditDate{T}"/>'s
    /// <c>IsValueDefault</c> override treats as empty, through the same shared helper (see
    /// <see cref="EditControlBase{TValue}.IsValueDefault"/>'s remarks for why a boxed default DateTime
    /// isn't caught by a plain null check).
    /// </summary>
    static bool IsEmpty(DateTime? value) => EditControlInit.IsDateValueDefault(value);

    // Both default to the resolved Label plus a " start"/" end" suffix — aria-label wins the
    // accessible-name computation over the visible FormLabel's label[for] association (see the class
    // remarks), so the suffix is what keeps the two inputs' names unique from each other while each
    // still contains the visible label text (WCAG 2.5.3 Label in Name). Falls back to each field's own
    // auto-derived label (matches EditDate's EffectiveInputLabel) when Label isn't set.
    string EffectiveStartInputLabel => StartInputLabel ?? (Label is not null ? $"{Label} start" : _attributes.GetLabelText(_startFieldIdentifier));
    string EffectiveEndInputLabel => EndInputLabel ?? (Label is not null ? $"{Label} end" : _endAttributes.GetLabelText(_endFieldIdentifier));

    // The control's own resolved label -- what the single visible FormLabel shows, and the base both
    // input names above are composed from. Also the dialog's own name below.
    string ResolvedLabel => Label ?? _attributes.GetLabelText(_startFieldIdentifier);

    // The dialog's name, derived from the SAME resolved control label the visible FormLabel shows,
    // instead of the picker's constant "Choose date range" -- with two range fields on a form, both
    // popups announced identically and nothing said which one had opened. The explicit parameter
    // stays the localization override. Mirrors EditDate.EffectiveDialogLabel.
    string EffectiveDialogLabel => DialogLabel ?? $"Choose {ResolvedLabel}";

    // The naming anchor for the two-input GROUP (see DateRangePicker.GroupLabelledBy): FormLabel's
    // lbltext-{id} span, which holds the label text alone -- not the whole <label>, which also
    // contains the tooltip trigger. The single visible label can only be label[for]-associated with
    // the Start input, so without this the End input read as an unrelated field with nothing tying it
    // back. Only consumed in edit mode -- read-only renders no picker at all.
    string GroupLabelledBy => $"lbltext-{_id}";

    // Each endpoint's autocomplete token resolves against its OWN property's attributes -- an
    // [Autocomplete] on Start must never leak onto End's input, exactly like [Placeholder] above.
    // Null is preserved so the picker's own "off" default still applies.
    string? EffectiveStartAutocomplete => StartAutocomplete ?? _attributes.Autocomplete();
    string? EffectiveEndAutocomplete => EndAutocomplete ?? _endAttributes.Autocomplete();

    // Each end resolves against its OWN property's attributes -- a [Placeholder] on Start must never
    // leak onto End's input, and vice versa. Null is intentional and must be preserved when neither
    // the parameter nor the attribute supplies text: forwarding null (rather than substituting a
    // literal) is what lets the inner DateRangePicker's own DefaultPlaceholder (the uppercased
    // EffectiveFormat) still apply, exactly as it does today.
    string? EffectiveStartPlaceholder => StartPlaceholder ?? _attributes.Placeholder();
    string? EffectiveEndPlaceholder => EndPlaceholder ?? _endAttributes.Placeholder();

    /// <summary>
    /// The format actually forwarded to the inner <see cref="DateRangePicker"/>: the <see cref="Format"/>
    /// parameter, else the Start property's <c>[DisplayFormat]</c>, else the End property's (see
    /// <see cref="AttributesHelper.FormatString"/>) -- mirrors <see cref="EffectiveMin"/>'s Start-first
    /// preference, since Format (like Min/Max) drives the single calendar both fields share rather than
    /// two independent per-field values. Null is intentional and must be preserved when no source
    /// supplies a format -- forwarding null is what lets <see cref="DateRangePicker"/>'s own mode-derived
    /// default still apply, exactly as it does today.
    /// </summary>
    string? EffectiveFormat => Format ?? _attributes.FormatString() ?? _endAttributes.FormatString();

    // The explicit-override surface for the read-only display format: the DateFormat parameter, else
    // the Start property's own [DisplayFormat], else the End property's. Shared by EffectiveDateFormat
    // (as the layer ahead of the mode-derived default) and FormatOne's Quarter/Week gate below -- an
    // attribute-supplied format must behave exactly like an explicit DateFormat (used verbatim,
    // bypassing the quarter/week shorthand), not silently get ignored in those two modes.
    string? FormatOverride => DateFormat ?? _attributes.FormatString() ?? _endAttributes.FormatString();

    // Unlike Start/EndPlaceholder above, Min/Max bound ONE shared calendar rather than two independent
    // inputs, so there's no "leak onto the other field" concern to avoid -- the opposite problem
    // applies instead. The natural annotation is [MinValue] on Start and [MaxValue] on End (the
    // property each bound most obviously constrains), but a single [Range(typeof(DateTime), ...)] on
    // just one property also supplies both bounds at once, so each shared bound is the LOOSER of the
    // two fields' own (min-of-mins, max-of-maxes) -- see UnionMin/UnionMax below. That's what keeps
    // the calendar from being tighter than either field's own annotation and blocking a value that
    // field's own validation would allow.
    //
    // Deliberate limitation: the result is the convex HULL of the two fields' accepted sets, NOT their
    // union. With [Range(2024-03-01 .. 2024-03-31)] on Start and [Range(2024-09-01 .. 2024-09-30)] on
    // End the calendar offers 2024-06-15, which NEITHER field accepts. A single calendar has exactly
    // one min and one max, so disjoint per-field windows can't be expressed there at all; the
    // annotations still reject the pick at validation time, which is the safe direction to err (an
    // over-tight calendar would instead make a legal value unreachable, with no message explaining
    // why). Not a regression -- the first-non-null chain this replaced had the same gap, plus the
    // tighter-of-two bug.
    DateTime? EffectiveMin => Min ?? UnionMin(_attributes.MinDate(), _endAttributes.MinDate());
    DateTime? EffectiveMax => Max ?? UnionMax(_attributes.MaxDate(), _endAttributes.MaxDate());

    // The union of two optional lower bounds: whichever ONE field declares when only one does (there's
    // nothing to compare against, and the "natural pairing" -- [MinValue] on Start alone, say -- must
    // still reach the shared calendar); the EARLIER (more permissive) of the two when BOTH fields
    // declare one, since a shared floor tighter than either field's own minimum would block a value
    // that field's own validation accepts -- the actual bug this replaces: a first-non-null pick that
    // preferred whichever field's attribute happened to be checked first (Start for Min, End for Max)
    // even when the OTHER field's own bound was looser.
    static DateTime? UnionMin(DateTime? start, DateTime? end) =>
        start is null ? end : end is null ? start : (start < end ? start : end);

    /// <inheritdoc cref="UnionMin"/>
    static DateTime? UnionMax(DateTime? start, DateTime? end) =>
        start is null ? end : end is null ? start : (start > end ? start : end);

    protected override void OnInitialized()
    {
        // Captured into locals (rather than closing over the nullable StartExpression/EndExpression
        // properties directly) so the factories below close over a provably non-null Expression —
        // nullable flow analysis doesn't carry a property's null-check narrowing into a lambda.
        var startExpression = EditControlInit.RequireBinding(
            StartExpression, this, "@bind-Start", nameof(StartExpression));
        var endExpression = EditControlInit.RequireBinding(
            EndExpression, this, "@bind-End", nameof(EndExpression));

        (_id, _attributes, _startFieldIdentifier) = EditControlInit.Init(startExpression, Id, FormGroupOptions, IdPrefix);
        _startFieldIdentifierFactory = () => FieldIdentifier.Create(startExpression);

        _endAttributes = AttributesHelper.GetExpressionCustomAttributes(endExpression);
        _endFieldIdentifier = FieldIdentifier.Create(endExpression);
        _endFieldIdentifierFactory = () => FieldIdentifier.Create(endExpression);
        _endId = $"{_id}-end";

        RegisterFields();
        RefreshAriaState();
    }

    /// <summary>
    /// The element a <c>ValidationView</c> summary link for the END field should anchor on: the End
    /// input's own id in edit mode, so an End-only error lands on the End input rather than Start's —
    /// but the Start/read-only id in READ-ONLY mode, where no End element exists at all. Read-only
    /// renders a single <c>ReadOnlyValue</c> carrying <c>_id</c> and showing "start - end" together;
    /// registering <c>_endId</c> there pointed the summary link at an id nothing rendered, so
    /// clicking it silently did nothing. The one element that actually shows the End value is the
    /// right target.
    /// </summary>
    string EndAnchorId => ShowEditor ? _endId : _id;

    /// <summary>
    /// Registers both bound fields, each under its own anchor id (see <see cref="EndAnchorId"/>), and
    /// records which anchor End landed on. Paired with <see cref="Dispose"/> — see
    /// <see cref="EditControlInit.RegisterField"/>'s remarks. Called from
    /// <see cref="OnInitialized"/>, from <see cref="SyncResolvedIds"/> when the resolved id moves, and
    /// from <see cref="OnParametersSet"/> when an edit/read-only flip moves End's anchor; a repeat
    /// call from the same owner updates <c>FormOptions.FieldIds</c> in place, so the link follows the
    /// element instead of pointing at one the control no longer renders.
    /// </summary>
    void RegisterFields()
    {
        EditControlInit.RegisterField(FormOptions, _startFieldIdentifier, _id, this);
        _registeredEndAnchorId = EndAnchorId;
        EditControlInit.RegisterField(FormOptions, _endFieldIdentifier, _registeredEndAnchorId, this);
    }

    // Both bound fields' ARIA state through the shared helpers, once per field (see
    // EditControlBase.RefreshAriaState). The End field's ERROR message id is its own; its
    // description/tooltip refs are Start's, because Description/Tooltip belong to the whole control
    // and the single start-anchored FormLabel renders exactly one desc-/tooltip- element for both.
    // That is why the second call passes a NULL attribute list: ResolveAriaRefs derives its
    // description from `description ?? attributes.Description()` and builds the ids from the id it is
    // GIVEN, so handing it _endAttributes/_endId would emit desc-{endId} — an element nothing
    // renders. The required-ness resolution still reads _endAttributes (a [Required] there is the End
    // field's own business). No-op until OnInitialized has run — _attributes is null before then.
    void RefreshAriaState()
    {
        if (_attributes is null) return;
        (_isRequired, _errorMsgId, _describedBy) = EditControlInit.ResolveAriaState(
            _id, ShouldHideLabel, Description, Tooltip, _attributes, IsRequired, FormOptions, _startFieldIdentifier);
        _endIsRequired = EditControlInit.AriaRequired(_endAttributes, null, FormOptions, _endFieldIdentifier);
        (_endErrorMsgId, _) = EditControlInit.ResolveAriaRefs(_endId, true, null, null, null);
        _endDescribedBy = BuildEndDescribedBy();
    }

    /// <summary>
    /// The End input's <c>aria-describedby</c>: its OWN <c>error-msg-</c> id, then whatever
    /// description/tooltip elements Start's chain references. Those elements are rendered ONCE for
    /// the whole control (one <see cref="FormLabel"/>, ids derived from the Start id), and
    /// <c>aria-describedby</c> is a reference, not ownership — so both inputs may and should point at
    /// them. Building End's chain from nulls instead meant a <see cref="IEditControl.Description"/> or
    /// <see cref="IEditControl.Tooltip"/> written once for the control was announced on Start and
    /// silently absent on End, which is where the format/range instructions matter just as much.
    /// Derived by filtering Start's already-resolved chain rather than re-deriving the same
    /// conditions, so the two can never disagree about which elements actually exist.
    /// </summary>
    string BuildEndDescribedBy()
    {
        var shared = _describedBy
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !token.StartsWith("error-msg-", StringComparison.Ordinal));
        return string.Join(' ', shared.Prepend(_endErrorMsgId));
    }

    // The two-field form of EditControlInit.SyncResolvedId (see its remarks for the whole rationale).
    // A no-op until OnInitialized has run -- _attributes is null before then, and the ids would
    // otherwise be resolved against a default FieldIdentifier.
    void SyncResolvedIds()
    {
        if (_attributes is null) return;
        var resolvedId = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, _startFieldIdentifier);
        if (string.Equals(resolvedId, _id, StringComparison.Ordinal)) return;

        _id = resolvedId;
        _endId = $"{_id}-end";
        RegisterFields();
    }

    protected override void OnParametersSet()
    {
        // Captured before SyncValidationSubscription overwrites it (cascading parameters, including
        // EditContext, are already bound to their NEW values by the time OnParametersSet runs -- only
        // the base's own tracking field still holds the OLD one at this point), so the cleanup below
        // can still target the OLD EditContext once a genuine swap is confirmed.
        var previousEditContext = SubscribedEditContext;

        // Re-resolve both element ids from the CURRENT Id/IdPrefix/group name -- BEFORE the
        // registration work below, so a re-registration lands under the current ids rather than
        // re-registering the stale ones. Done by hand rather than through
        // EditControlInit.SyncResolvedId because only the Start id is resolved: _endId is derived
        // from it, and both registrations move together when it changes. Same "the FieldName is
        // unchanged across a model swap" reasoning as the list base's single-field call.
        SyncResolvedIds();

        // A false return means the same EditContext is still cascading, so both cached
        // FieldIdentifiers are still live and there's nothing to re-register.
        if (SyncValidationSubscription())
        {
            // The EditContext changed -- _parseErrorMessages (if this control ever created one) is
            // bound to the OLD context via AddFieldErrorAsync's `??=` and would otherwise keep
            // silently writing/clearing entries there forever: nothing renders from a context that no
            // longer cascades here, so a parse error typed after the swap would show no message and
            // never set aria-invalid. Clear its entries against the OLD context/FieldIdentifiers --
            // BEFORE SyncFieldRegistration below re-derives _startFieldIdentifier/_endFieldIdentifier
            // against the new model -- and drop the store so the very next parse error lazily rebinds
            // a fresh one to the NEW EditContext.
            if (_parseErrorMessages is not null)
            {
                _parseErrorMessages.Clear(_startFieldIdentifier);
                _parseErrorMessages.Clear(_endFieldIdentifier);
                _startParseError = false;
                _endParseError = false;
                previousEditContext?.NotifyValidationStateChanged();
                _parseErrorMessages = null;
            }

            // A changed context is how a parent swapping the model instance (form reset, reload)
            // surfaces — re-derive BOTH FieldIdentifiers against the current model and move each
            // registration onto its own new one, through the same shared helper (on
            // EditControlParametersBase) EditControlListBase.OnParametersSet calls once for its
            // single field.
            SyncFieldRegistration(ref _startFieldIdentifier, _startFieldIdentifierFactory, _id);
            SyncFieldRegistration(ref _endFieldIdentifier, _endFieldIdentifierFactory, EndAnchorId);
        }

        // An edit/read-only flip (FormOptions.IsEditMode, or this control's own IsEditMode) changes
        // WHICH element the End field's summary link should anchor on without touching the resolved
        // id or the EditContext, so neither trigger above sees it. See EndAnchorId.
        if (!string.Equals(_registeredEndAnchorId, EndAnchorId, StringComparison.Ordinal)) RegisterFields();

        // Keep the cached ARIA state current when parameters change (runtime Description/Tooltip or
        // label-hidden toggle) — and deliberately LAST: aria-required resolves through
        // FormOptions.RequiredResolver against the two FieldIdentifiers, so refreshing before the
        // re-registration above left the star and aria-required answering for the swapped-away model.
        // (The parse-error clear still runs first for the mirror-image reason: it must see the OLD
        // identifiers/context.) Same ordering in EditControlListBase.OnParametersSet.
        RefreshAriaState();
    }

    // Write the new value back to the bound model BEFORE notifying the EditContext — the validator
    // reads the property live off the model via reflection during NotifyFieldChanged (see
    // EditControlListBase.ToggleAsync for the full rationale).
    //
    // The parse-error clear does NOT live here (it used to, and only here): the picker raises
    // Start/EndChanged per endpoint and only when that endpoint's value actually changed, so retyping
    // an endpoint's CURRENT value -- a perfectly valid entry -- never reached this method and left that
    // endpoint's message, and the aria-invalid it drives, up permanently with nothing the user could
    // type to clear it. OnPickerValidCommit below owns the retirement now, on the channel that survives
    // the dedup.
    async Task OnStartChanged(DateTime? value)
    {
        Start = value;
        await StartChanged.InvokeAsync(value);
        EditContext?.NotifyFieldChanged(_startFieldIdentifier);
    }

    async Task OnEndChanged(DateTime? value)
    {
        End = value;
        await EndChanged.InvokeAsync(value);
        EditContext?.NotifyFieldChanged(_endFieldIdentifier);
    }

    /// <summary>
    /// Raised by the inner <see cref="DateRangePicker"/> on every accepted commit, carrying which
    /// endpoint(s) that commit assigned a value to — including an assignment equal to what the endpoint
    /// already held, which <see cref="DateRangePicker.StartChanged"/>/<see cref="DateRangePicker.EndChanged"/>
    /// deliberately drop (each fires only when its own side changed). Each named endpoint's stale
    /// <see cref="ParsingErrorMessage"/>/<see cref="RangeErrorMessage"/> is retired here and only here;
    /// the endpoint NOT named keeps its own message, because its own text was never revalidated. A
    /// range-selection click, a preset, a session OK and the clear all name both, so they retire both.
    /// </summary>
    void OnPickerValidCommit(DateRangeEndpoints assigned)
    {
        if (assigned.HasFlag(DateRangeEndpoints.Start)) ClearParseError(_startFieldIdentifier, ref _startParseError);
        if (assigned.HasFlag(DateRangeEndpoints.End)) ClearParseError(_endFieldIdentifier, ref _endParseError);
    }

    // Dedicated store for the two parse-error messages below -- a separate instance from whatever
    // DataAnnotationsValidator (or any other validator) already maintains for this same EditContext,
    // since this control never parses typed text itself (the picker owns that) and so has no
    // InputBase-style built-in parsing-error path to route through. Multiple independent
    // ValidationMessageStores over one EditContext compose fine -- each only ever touches the entries
    // it added itself, so clearing this one can never drop a DataAnnotations message and vice versa.
    // Mirrors EditDate's own _parseErrorMessages, doubled onto two FieldIdentifiers. Bound to whichever
    // EditContext was current the first time AddFieldErrorAsync ran (the `??=` below) -- NOT
    // necessarily forever: OnParametersSet drops it back to null on a genuine EditContext swap (unlike
    // EditDate, this control supports swaps at all -- see OnParametersSet's own remarks), so the next
    // parse error lazily rebinds a fresh store to whatever EditContext is current then.
    ValidationMessageStore? _parseErrorMessages;

    // Whether _parseErrorMessages currently holds a message for the Start / End field. Same purpose as
    // EditColor/EditDate's single _hasParseError, doubled: OnPickerValidCommit runs on every accepted
    // commit (a drag through a preset, a retyped date, a clear), and an unguarded clear would end in
    // NotifyValidationStateChanged whether or not there was anything to retire -- one wasted re-render
    // of every ValidationSummary subscriber per commit, per endpoint, over a network round trip on
    // Blazor Server.
    bool _startParseError;
    bool _endParseError;

    /// <summary>
    /// Raised by the inner <see cref="DateRangePicker"/> when typed text in the START input can't be
    /// parsed at all (see <see cref="DateRangePicker.OnStartParseError"/> for exactly which failures
    /// reach here) — adds <see cref="ParsingErrorMessage"/> against the Start field. The offending text
    /// is deliberately discarded: <see cref="ParsingErrorMessage"/>'s <c>{0}</c> is the field name, not
    /// the text, matching <see cref="EditDate{T}.ParsingErrorMessage"/>'s identical contract.
    /// </summary>
    Task OnStartParseErrorAsync(string _) =>
        AddFieldErrorAsync(_startFieldIdentifier, ParsingErrorMessage, ref _startParseError);

    /// <inheritdoc cref="OnStartParseErrorAsync"/>
    Task OnEndParseErrorAsync(string _) =>
        AddFieldErrorAsync(_endFieldIdentifier, ParsingErrorMessage, ref _endParseError);

    /// <summary>
    /// Raised by the inner <see cref="DateRangePicker"/> when typed text in the START input parses
    /// fine but the Min/Max/DisabledDate/DisabledTime guards refuse it
    /// (<see cref="DateRangePicker.OnStartRangeError"/>) — adds <see cref="RangeErrorMessage"/>
    /// against the Start field. Same store and notify as the parse path, different wording; the
    /// refusal previously reached the form layer through no channel at all.
    /// </summary>
    Task OnStartRangeErrorAsync(string _) =>
        AddFieldErrorAsync(_startFieldIdentifier, RangeErrorMessage, ref _startParseError);

    /// <inheritdoc cref="OnStartRangeErrorAsync"/>
    Task OnEndRangeErrorAsync(string _) =>
        AddFieldErrorAsync(_endFieldIdentifier, RangeErrorMessage, ref _endParseError);

    // Mirrors the shape of InputBase<T>.SetCurrentValueAsStringAsync's own built-in parsing-error path
    // -- clear this field's prior entry, add the formatted message, and notify -- just against a store
    // this control owns, and against whichever of the two fields actually failed. Shared by both
    // rejection kinds so they can't drift apart in everything but wording. `hasError` is that field's
    // own outstanding-message flag, passed by ref alongside its FieldIdentifier so the pair can never be
    // mismatched at a call site (same reason OnParametersSet passes each identifier to
    // SyncFieldRegistration by ref).
    Task AddFieldErrorAsync(FieldIdentifier field, string messageFormat, ref bool hasError)
    {
        if (EditContext is null) return Task.CompletedTask;
        _parseErrorMessages ??= new ValidationMessageStore(EditContext);
        _parseErrorMessages.Clear(field);
        _parseErrorMessages.Add(field,
            string.Format(CultureInfo.InvariantCulture, messageFormat, field.FieldName));
        hasError = true;
        // Neither bound value changed (the bad text was reverted, not committed) -- notify explicitly,
        // same as InputBase's own equivalent failure path, so FormOptions/consumers watching field
        // changes still see this as a touch.
        EditContext.NotifyFieldChanged(field);
        EditContext.NotifyValidationStateChanged();
        return Task.CompletedTask;
    }

    // Drops `field`'s own parse-error entry, if this control has one outstanding. Only ever touches
    // entries it added itself -- see _parseErrorMessages. Returns immediately when that field has
    // nothing outstanding: the notify is the expensive part, and this runs on every accepted commit
    // (see _startParseError). Same ref-pairing as AddFieldErrorAsync above.
    void ClearParseError(FieldIdentifier field, ref bool hasError)
    {
        if (!hasError || _parseErrorMessages is null || EditContext is null) return;
        hasError = false;
        _parseErrorMessages.Clear(field);
        EditContext.NotifyValidationStateChanged();
    }

    // The validation-state ARIA goes through DateRangePicker's dedicated per-input Aria* parameters
    // (straight onto the two actual <input>s, each reflecting its own field's state); this splat
    // carries only the consumer's own attributes plus the state classes, landing on the picker's
    // outer wrapper (its documented AdditionalAttributes target). Overwriting the raw consumer
    // "class" with FieldCssClass is what gives the wrapper the Start field's (and, folded in, the End
    // field's) validation-state styling hooks -- see FieldCssClass's remarks. Shared builder: see
    // BuildPickerAttributes's own remarks for why this and EditDate's twin differ only in that class
    // source. FieldCssClass never returns "" (only null or a non-empty string), so the builder's
    // IsNullOrEmpty guard is exactly the null check this used to make inline. Takes the already-built
    // class string rather than reading FieldCssClass itself, so the markup's one evaluation of the
    // Start/End validity pair serves both this and the read-only view.
    IReadOnlyDictionary<string, object> PickerAttributes(string? fieldCssClass) =>
        EditControlInit.BuildPickerAttributes(AdditionalAttributes, fieldCssClass);

    // Mirrors EditDate.EffectiveDateFormat one-for-one (the same shared PickerMath.ModeDisplayFormat
    // over the same dash-separated bases), keyed off this control's own Mode -- there's no separate
    // Type/Mode fork here, Mode is the only lever. That helper's Quarter/Week "yyyy" is never actually
    // rendered: FormatOne bypasses ToString(EffectiveDateFormat) for both via PickerMath's shared
    // FormatQuarterDisplay/FormatWeekDisplay (see FormatOne below).
    string EffectiveDateFormat =>
        FormatOverride ?? PickerMath.ModeDisplayFormat(Mode, "MM-dd-yyyy", "MM-yyyy", Use12Hours, ShowSeconds);

    string GetDisplayValue()
    {
        // Gregorian-forced like the picker's own display, so read-only and edit mode can never
        // disagree about the year under a non-Gregorian-default culture (th-TH, ar-SA).
        var culture = GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);
        var start = FormatOne(Start, culture);
        var end = FormatOne(End, culture);
        if (start.Length == 0 && end.Length == 0) return string.Empty;
        return $"{start} - {end}";
    }

    string FormatOne(DateTime? value, CultureInfo culture)
    {
        if (value is not { } v) return string.Empty;
        // Quarter/Week's null-DateFormat display has no .NET format token to route through
        // ToString(EffectiveDateFormat) -- PickerMath.ReadOnlyDisplay (shared with EditDate) special-
        // cases those two through the very FormatQuarterDisplay/FormatWeekDisplay the inner
        // DateRangePicker's own display routes through, not duplicated regex/format logic here.
        // Passing null as the shorthand value (an explicit DateFormat) falls through to the verbatim
        // ToString path instead, matching the picker's own Format contract. FirstDayOfWeek is resolved
        // against `culture` inside the helper -- there's no picker instance to ask once this control is
        // in read-only mode (no <DateRangePicker> renders at all then).
        return PickerMath.ReadOnlyDisplay(Mode, FormatOverride is null ? v : (DateTime?)null, culture,
            FirstDayOfWeek,
            () => v.ToString(EffectiveDateFormat, culture),
            () => v.ToString(culture));
    }

    /// <summary> Detaches the validation-state listener, drops any outstanding parse-error messages, and
    /// drops both field registrations so a removed control (e.g. behind a conditional <c>@if</c>) doesn't
    /// leave stale state in the validation summary. The parse-error entries live on the
    /// <see cref="EditContext"/>, not on this component, so a control removed while showing one (an
    /// <c>IsHidden</c>/<see cref="HidingMode"/> toggle, a tab switch) would otherwise leave the message
    /// behind for a <c>ValidationView</c> summary to link to a field that no longer renders. </summary>
    public void Dispose()
    {
        // Both endpoints through the guarded clear, so a control unmounting with nothing outstanding
        // notifies nobody (see ClearParseError); one notification covers both when they do.
        if ((_startParseError || _endParseError) && _parseErrorMessages is not null && EditContext is not null)
        {
            _parseErrorMessages.Clear(_startFieldIdentifier);
            _parseErrorMessages.Clear(_endFieldIdentifier);
            _startParseError = false;
            _endParseError = false;
            EditContext.NotifyValidationStateChanged();
        }
        DetachValidationSubscription();
        EditControlInit.UnregisterField(FormOptions, _startFieldIdentifier, this);
        EditControlInit.UnregisterField(FormOptions, _endFieldIdentifier, this);
    }
}
