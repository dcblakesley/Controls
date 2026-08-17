using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-style single-date picker: a text field with a calendar suffix that opens a
/// dropdown panel. <see cref="Mode"/> selects what the panel offers — <c>Date</c> (default) shows
/// a one-month calendar whose header is month/year quick-select dropdowns; <c>Month</c> shows a
/// year header (prev/next-year buttons flanking a year select) over a 3x4 grid of month buttons;
/// <c>Time</c> shows three hour/minute/second selects over an OK button; <c>DateTime</c> shows the
/// day calendar with that same time row and OK button appended below it; <c>Year</c> shows a
/// decade header (prev/next-decade buttons flanking a static decade label) over a 3x4 grid of year
/// buttons (10 of the decade plus 2 dimmed adjacent-decade years); <c>Quarter</c> shows the same
/// year header as <c>Month</c> over a single row of 4 quarter buttons; <c>Week</c> shows the exact
/// same panel as <c>Date</c> (header, weekday header, day grid) plus a leading week-number column —
/// there the row, not the day, is the selection unit: every day in <see cref="Value"/>'s week
/// carries the pressed styling and clicking any one of the 7 commits that row's week start.
/// Picking a day/month/year/quarter/week (or typing text and pressing Enter) commits and closes; in
/// <c>Time</c>/<c>DateTime</c> mode the time selects — and, in <c>DateTime</c> mode, a day click —
/// commit immediately without closing the panel, so the user can keep adjusting; OK is the close
/// signal there instead.
/// </summary>
/// <remarks>
/// The single-date sibling of <see cref="DateRangePicker"/> — it shares the <c>wss-picker-*</c>
/// calendar internals and the <c>wss-overlay.js</c> lifecycle. Not a form control (no
/// <c>InputBase</c>/validation wiring) — bind with <c>@bind-Value</c>. JS interop (viewport
/// flip/clamp, form-submit suppression, focus-out close, arrow-key page-scroll suppression, and
/// <c>ArrowDown</c> from the field stepping focus into the calendar grid) degrades gracefully:
/// without JS the dropdown opens below the field at the CSS default placement, everything remains
/// clickable, Tab still reaches the grid, and arrow-key grid navigation still updates the
/// roving-tabindex state (just without the DOM focus follow or the native page-scroll suppression).
/// The one degradation that is NOT purely cosmetic is the focus-out dismissal — see
/// <see cref="PickerBase"/>'s own remarks for exactly what that costs and why C# can't substitute
/// for it.
/// </remarks>
public partial class DatePicker : PickerBase
{
    // ----- Parameters -------------------------------------------------------

    /// <summary>The bound date (date-only; null = empty). Supports <c>@bind-Value</c>.</summary>
    [Parameter] public DateTime? Value { get; set; }
    /// <summary>Raised with the new date when it changes (supports <c>@bind-Value</c>).</summary>
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently being splatted as an unmatched attribute (this
    /// component captures unmatched values, and a declared parameter always wins over splatting).
    /// Remove the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<DateTime?>>? Field { get; set; }

    /// <summary>What the picker selects. Defaults to <see cref="DatePickerMode.Date"/>. The bound
    /// <see cref="Value"/> stays <c>DateTime?</c> in every mode; only the commit-time normalization
    /// differs — <c>Date</c> keeps the date, <c>Month</c> normalizes to the 1st of the month at
    /// midnight, <c>DateTime</c> truncates to whole seconds (or zeroes the second entirely when
    /// <see cref="ShowSeconds"/> is false), <c>Time</c> anchors to <see cref="DateTime.Today"/> plus
    /// the time-of-day (same truncation/zeroing), <c>Year</c> normalizes to January 1st at midnight,
    /// <c>Quarter</c> normalizes to the 1st day of the quarter at midnight, <c>Week</c> normalizes to
    /// that week's first day (per <see cref="EffectiveFirstDayOfWeek"/>) at midnight.</summary>
    [Parameter] public DatePickerMode Mode { get; set; } = DatePickerMode.Date;

    /// <summary>Earliest selectable date (inclusive). In <see cref="DatePickerMode.Date"/> and
    /// <see cref="DatePickerMode.DateTime"/> this disables days before it; in
    /// <see cref="DatePickerMode.Month"/> it disables whole months before its month; in
    /// <see cref="DatePickerMode.Year"/> whole years, and in <see cref="DatePickerMode.Quarter"/>
    /// whole quarters, before its own; in <see cref="DatePickerMode.Week"/> a whole week (its 7-day
    /// span entirely before this date) — a day button still enables per its own day-granularity
    /// check, since a partially-in-range week's commit lands on the week start, not the clicked day.
    /// Ignored in <see cref="DatePickerMode.Time"/> (a time-of-day has no date-range concept).</summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>Latest selectable date (inclusive). Same mode-dependent granularity as
    /// <see cref="Min"/>; ignored in <see cref="DatePickerMode.Time"/>.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Extra disable predicate alongside <see cref="Min"/>/<see cref="Max"/> — a cell (or a
    /// typed/clicked commit) is disabled when either says so. Called with the CELL'S
    /// committed-value representative, at Mode's own granularity: the day at midnight in
    /// <see cref="DatePickerMode.Date"/>/<see cref="DatePickerMode.DateTime"/> (including
    /// <see cref="DatePickerMode.Week"/>'s individual day buttons, which stay day-granularity even
    /// though the row's own selection/commit unit is the week); the 1st of the month at midnight in
    /// <see cref="DatePickerMode.Month"/>; January 1st at midnight in <see cref="DatePickerMode.Year"/>;
    /// the 1st of the quarter at midnight in <see cref="DatePickerMode.Quarter"/>; and the WEEK START
    /// (not the individual day) for <see cref="DatePickerMode.Week"/>'s own commit guard — a
    /// partially-disabled week's day buttons can still be enabled while the row's commit itself is
    /// rejected, mirroring how <see cref="Min"/>/<see cref="Max"/> already split that mode. Ignored in
    /// <see cref="DatePickerMode.Time"/> (no calendar cells exist there — see
    /// <see cref="DisabledTime"/> for time-of-day restrictions). Called once per rendered cell on
    /// every render (plus once per commit guard) — keep it cheap, no I/O.</summary>
    [Parameter] public Func<DateTime, bool>? DisabledDate { get; set; }

    /// <summary>Disables specific hour/minute/second option values in the
    /// <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/> time row. Invoked with
    /// the current date part — <see cref="Value"/>'s date, or null when <see cref="Value"/> is null —
    /// once per render of the time row (not once per option) and once per commit guard (a time select
    /// change via <c>ApplyTimePartAsync</c>, a typed-text commit in either mode, or — in
    /// <see cref="DatePickerMode.DateTime"/> — a day click, which carries the current time-of-day onto
    /// a date that may disable it). A disabled hour/
    /// minute/second renders its <c>&lt;option&gt;</c> with the <c>disabled</c> attribute (or is
    /// omitted entirely — see <see cref="HideDisabledTimeOptions"/>) and rejects a commit that would
    /// land on it (the select — or the typed text — reverts, same as a <see cref="Min"/>/<see cref="Max"/>
    /// rejection). A null callback, a null <see cref="DisabledTimeParts"/> return, or a null
    /// collection within it all mean nothing is disabled.</summary>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? DisabledTime { get; set; }

    /// <summary>When true, an option <see cref="DisabledTime"/> disables is omitted from its select
    /// entirely instead of rendered disabled (AntD's <c>hideDisabledOptions</c>). Defaults to false.
    /// NEVER-JUMP RULE, in force under either setting: the select's CURRENT value's own option is
    /// always rendered — selected, and also marked <c>disabled</c> if <see cref="DisabledTime"/> says
    /// so — even while every other disabled option is hidden, so a select can never silently show a
    /// value that isn't the one actually bound.</summary>
    [Parameter] public bool HideDisabledTimeOptions { get; set; }

    /// <summary>Whether the <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
    /// time row includes a seconds select. Defaults to true. False drops the seconds select entirely
    /// (see <see cref="PickerMath.TimeFormatString"/>) and normalization (<see cref="NormalizeForMode"/>) zeroes
    /// the second in both modes, so a stale second from before the flip was toggled can never survive
    /// a commit.</summary>
    [Parameter] public bool ShowSeconds { get; set; } = true;

    /// <summary>Step between the hour select's offered values (24-hour space, even under
    /// <see cref="Use12Hours"/>): the select lists 0, <c>HourStep</c>, 2*<c>HourStep</c>, and so on up
    /// to 23. Defaults to 1 (every hour). Values less than 1 are clamped to 1 at the point of use, not
    /// thrown. NEVER-JUMP RULE, composing with <see cref="DisabledTime"/>'s own (see
    /// <see cref="HideDisabledTimeOptions"/>): if the bound value's hour isn't on the step lattice, its
    /// own option is still rendered (selected) so the select can never silently jump to a different
    /// hour — the two filters compose by applying the step first, then the disabled/hide check.</summary>
    [Parameter] public int HourStep { get; set; } = 1;
    /// <summary>Same contract as <see cref="HourStep"/>, for the minute select (0-59). Defaults to 1.</summary>
    [Parameter] public int MinuteStep { get; set; } = 1;
    /// <summary>Same contract as <see cref="HourStep"/>, for the second select (0-59). Defaults to 1.
    /// Has no effect when <see cref="ShowSeconds"/> is false (the select it would apply to doesn't
    /// exist).</summary>
    [Parameter] public int SecondStep { get; set; } = 1;

    /// <summary>Shows the hour select in 12-hour form — <c>12, 1, 2, ... 11</c> for the currently
    /// selected AM/PM period — plus a trailing period select (<see cref="PeriodSelectLabel"/>) whose
    /// two options are <see cref="PickerBase.PickerCulture"/>'s <c>AMDesignator</c>/<c>PMDesignator</c>, instead
    /// of the default single 24-hour (0-23) select. The bound <see cref="Value"/> always stays a
    /// 24-hour value — only the hour select's displayed text and the period select are 12-hour;
    /// changing the hour still commits its own 24-hour value verbatim (the option VALUES remain the
    /// 24h hours belonging to the current period), and changing the period re-commits the CURRENT hour
    /// shifted into the other one (<c>hour % 12 + (isPM ? 12 : 0)</c>) via the same
    /// <c>ApplyTimePartAsync</c> every other time-row change routes through. <see cref="HourStep"/>
    /// still applies in 24-hour space (a step spanning both periods simply yields fewer options in
    /// each). Defaults to false.</summary>
    [Parameter] public bool Use12Hours { get; set; }

    /// <summary>Display and primary parse format for the input. Typed text is parsed with this
    /// exact format first, then with the current culture's general date parsing. Null (default)
    /// picks <see cref="Mode"/>'s default: <c>Date</c> <c>MM/dd/yyyy</c> (the Figma spec) ·
    /// <c>Month</c> <c>MM/yyyy</c> · <c>DateTime</c> <c>MM/dd/yyyy</c> plus <c>Time</c>'s own string,
    /// space-separated · <c>Time</c> <c>HH:mm:ss</c> (<see cref="ShowSeconds"/> false drops <c>:ss</c>;
    /// <see cref="Use12Hours"/> switches to the 12-hour <c>h:mm tt</c>/<c>h:mm:ss tt</c> forms instead)
    /// · <c>Year</c> <c>yyyy</c>. <c>Quarter</c> and <c>Week</c> have no .NET format
    /// token for a quarter number or an ISO-style week number: left null, they render/parse
    /// <c>yyyy-Qn</c> (e.g. "2026-Q3") / <c>yyyy-Www</c> (e.g. "2026-W08") via a hand-rolled special
    /// case instead of <see cref="DateTime.ToString(string)"/>; set explicitly, it is used verbatim
    /// via <c>ToString</c> and therefore can't render the quarter/week digits itself.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Raised with the offending text when a typed commit (Enter or blur) can't be parsed as a date
    /// at all -- i.e. <see cref="PickerBase.TryParseDate"/> itself fails, not merely a well-formed date rejected
    /// by <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<see cref="DisabledTime"/>
    /// (that's a valid date this picker simply won't accept, which is a different situation from a
    /// parse failure, and does not raise this callback). This picker has no validation concept of its
    /// own (see the class remarks) -- it exists so a host form control (<see cref="EditDate{T}"/>)
    /// can surface a validation message the picker itself can't. Optional: a standalone
    /// <see cref="DatePicker"/> with no handler attached behaves exactly as before this parameter
    /// existed -- the unparseable text is still silently reverted to the formatted bound value.
    /// </summary>
    [Parameter] public EventCallback<string> OnParseError { get; set; }

    /// <summary>
    /// Raised with the offending text when a typed commit (Enter or blur) parses into a perfectly
    /// well-formed date that this picker nonetheless REFUSES — one rejected by <see cref="Min"/>,
    /// <see cref="Max"/>, <see cref="DisabledDate"/> or <see cref="DisabledTime"/>. Deliberately a
    /// separate signal from <see cref="OnParseError"/> (which is parse failure only, and is documented
    /// as such): the two need different messages, and until this existed the rejection was completely
    /// silent — the field reverted, nothing changed, and a keyboard-only user had no way to discover
    /// why. Same optional contract as <see cref="OnParseError"/>: with no handler attached the text is
    /// still silently reverted to the formatted bound value, exactly as before this parameter existed.
    /// The typed-text commit paths are the only ones that raise it — a CLICK on a rejected cell is
    /// already visible as <c>aria-disabled</c>, so it needs no announcement of its own.
    /// </summary>
    [Parameter] public EventCallback<string> OnRangeError { get; set; }

    /// <summary>
    /// Raised on every ACCEPTED commit, <b>including</b> one whose value equals what is already bound,
    /// which <see cref="ValueChanged"/> deliberately drops (see <c>SetValueAsync</c>). That dedup is
    /// exactly why this callback exists: a host form control showing an <see cref="OnParseError"/>/
    /// <see cref="OnRangeError"/> message has to retire it the moment an accepted entry lands, and "the
    /// user retyped the date that was already there" is an accepted entry. <see cref="EditDate{T}"/>
    /// clears its validation message from here.
    /// </summary>
    /// <remarks>
    /// Every accepted-entry path raises it, because they all funnel through <c>SetValueAsync</c>: a
    /// day/month/quarter/year/week cell click, a typed entry, a time select or the time panel's OK, a
    /// preset click, and <see cref="AllowClear"/>'s clear (a commit of "no date" — its
    /// <c>SetValueAsync(null)</c> is an ordinary commit, so an emptied field retires a stale
    /// parse/range message too). The paths that do NOT raise it are the ones that commit nothing: an
    /// unparseable typed entry (<see cref="OnParseError"/>), a well-formed one the
    /// <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<see cref="DisabledTime"/> guards
    /// refuse (<see cref="OnRangeError"/>), a click on a rejected cell, and anything at all while
    /// <see cref="Disabled"/>. Optional, like the two error callbacks. Same contract as
    /// <see cref="ColorPicker.OnValidCommit"/>.
    /// </remarks>
    [Parameter] public EventCallback OnValidCommit { get; set; }

    /// <summary>Input placeholder. Null (default) picks <see cref="Mode"/>'s default: <c>Date</c>/
    /// <c>DateTime</c> "Select date" (the Figma spec) · <c>Month</c> "Select month" · <c>Time</c>
    /// "Select time" · <c>Year</c> "Select year" · <c>Quarter</c> "Select quarter" · <c>Week</c>
    /// "Select week". Override to localize.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Shows a clear button (over the calendar icon) while a value is set. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary>Disables all interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Field width as a CSS length (e.g. "300px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>
    /// Visual size, shared with the <c>Select</c> family's <see cref="SelectSize"/> (Default/Small/
    /// Large) -- adds <c>wss-picker-sm</c>/<c>wss-picker-lg</c> to the outer wrapper.
    /// <see cref="SelectSize.Default"/> adds no class (byte-identical DOM to before this parameter
    /// existed).
    /// </summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <summary>First day of the week for the calendar grid. Null (default) follows
    /// <see cref="CultureInfo.CurrentCulture"/>.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Shows a leading week-number column (AntD's <c>showWeek</c>) beside the day grid in
    /// <see cref="DatePickerMode.Date"/> and <see cref="DatePickerMode.DateTime"/>, with no other
    /// behavior change — a day click still commits that day, not its week. Defaults to false.
    /// <see cref="DatePickerMode.Week"/> always renders this column regardless of this
    /// parameter.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>Adds a "Today"-style link button (<see cref="TodayText"/>) to a footer in
    /// <see cref="DatePickerMode.Date"/>/<see cref="DatePickerMode.Month"/>/<see cref="DatePickerMode.Quarter"/>/
    /// <see cref="DatePickerMode.Year"/>/<see cref="DatePickerMode.Week"/> mode that commits
    /// <see cref="DateTime.Today"/> normalized to Mode's own granularity (today itself, this month,
    /// this quarter, this year, or this week) and closes the panel. Defaults to true, matching
    /// AntD's <c>showToday</c> — set false to drop the footer row entirely. Has no
    /// effect in <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
    /// — see <see cref="ShowNow"/> for their equivalent. The button renders DISABLED, not hidden,
    /// when the normalized today is rejected by <see cref="Min"/>/<see cref="Max"/>/
    /// <see cref="DisabledDate"/> — the same show-the-dead-end convention every rejected calendar
    /// cell follows.</summary>
    [Parameter] public bool ShowToday { get; set; } = true;
    /// <summary>Visible text of the <see cref="ShowToday"/> link button. Override to localize.</summary>
    [Parameter] public string TodayText { get; set; } = "Today";

    /// <summary>Adds a "Now"-style link button (<see cref="NowText"/>) to the EXISTING
    /// <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/> footer, left of OK,
    /// that commits <see cref="DateTime.Now"/> normalized to Mode's own granularity (seconds zeroed
    /// when <see cref="ShowSeconds"/> is false) WITHOUT closing the panel — matching those modes'
    /// incremental commit model, where OK remains the close signal. Defaults to false. Has no effect
    /// in <see cref="DatePickerMode.Date"/>/<see cref="DatePickerMode.Month"/>/
    /// <see cref="DatePickerMode.Quarter"/>/<see cref="DatePickerMode.Year"/>/
    /// <see cref="DatePickerMode.Week"/> — see <see cref="ShowToday"/> for their equivalent.
    /// Disabled, not hidden, under the same guards as <see cref="ShowToday"/>.</summary>
    [Parameter] public bool ShowNow { get; set; }
    /// <summary>Visible text of the <see cref="ShowNow"/> link button. Override to localize.</summary>
    [Parameter] public string NowText { get; set; } = "Now";

    /// <summary>Optional shortcuts rendered as a sidebar in the dropdown (AntD's <c>presets</c>),
    /// mirroring <see cref="DateRangePicker.Presets"/>'s shape and reusing its <c>wss-picker-presets</c>/
    /// <c>wss-picker-preset</c> sidebar classes verbatim. Clicking one resolves it (see
    /// <see cref="DatePickerPreset.Resolve"/>), normalizes to Mode's own granularity, commits — a
    /// guard-rejected result (<see cref="IsDisabledForCommit"/>) no-ops instead — and closes the
    /// panel, in EVERY mode including <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>:
    /// a preset is a complete pick there, unlike those modes' own incremental time selects.</summary>
    [Parameter] public IReadOnlyList<DatePickerPreset>? Presets { get; set; }
    /// <summary>Accessible name of the preset sidebar list. Override to localize.</summary>
    [Parameter] public string PresetsLabel { get; set; } = "Quick picks";

    /// <summary>Extra content rendered in its own strip (<c>wss-picker-extra-footer</c>) above the
    /// footer row — or above the panel's own bottom edge, in a mode that has no footer of its own
    /// (AntD's <c>renderExtraFooter</c>). Renders in every mode.</summary>
    [Parameter] public RenderFragment? ExtraFooter { get; set; }

    /// <summary>The month/year/decade the panel opens showing when <see cref="Value"/> is null
    /// (AntD's <c>defaultPickerValue</c>) — ignored once <see cref="Value"/> is set. <see cref="Open"/>'s
    /// view anchor is <c>Value ?? DefaultViewDate ?? DateTime.Today</c> in every mode.</summary>
    [Parameter] public DateTime? DefaultViewDate { get; set; }

    /// <summary>HTML id applied to the input — wires a consumer label / test hook.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>
    /// The input's <c>autocomplete</c> token. Null (default) renders <c>"off"</c>, the value this
    /// input hardcoded before this parameter existed. Set it to the field's real purpose (e.g.
    /// <c>"bday"</c>, <c>"cc-exp"</c>) so browsers and assistive tech can autofill — WCAG 1.3.5
    /// (Identify Input Purpose) is only satisfiable through this attribute, and consumer attributes
    /// can't supply it because <see cref="AdditionalAttributes"/> lands on the outer wrapper, not the
    /// input. Same role as <c>EditString</c>'s own <c>Autocomplete</c> parameter.
    /// </summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>
    /// Value for the input's <c>aria-labelledby</c>; null (default) omits it, leaving
    /// <see cref="InputLabel"/> as the accessible name. Set by a form wrapper
    /// (<see cref="EditDate{T}"/>) to point at its <c>FormLabel</c>'s <c>lbltext-{id}</c> naming
    /// anchor, so the input's name is the label's own text — live, and excluding the tooltip
    /// trigger that sits inside the same <c>&lt;label&gt;</c>. Wins over <see cref="InputLabel"/>
    /// when both are set (per the accessible-name spec), which is why the wrapper only sets it while
    /// it isn't carrying an explicit name of its own.
    /// </summary>
    [Parameter] public string? AriaLabelledBy { get; set; }

    // Localizable accessibility strings. Defaults are English, matching DateRangePicker's convention.

    /// <summary>Accessible name of the input. Override to localize.</summary>
    [Parameter] public string InputLabel { get; set; } = "Date";
    /// <summary>Accessible name of the dropdown dialog. Override to localize.</summary>
    [Parameter] public string DialogLabel { get; set; } = "Choose date";
    /// <summary>Accessible name of the month select. Override to localize.</summary>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <summary>Accessible name of the year select. Override to localize.</summary>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <summary>Accessible name of the clear button. Override to localize.</summary>
    [Parameter] public string ClearLabel { get; set; } = "Clear date";
    /// <summary>Accessible name of the previous-month button. Override to localize.</summary>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <summary>Accessible name of the next-month button. Override to localize.</summary>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";
    /// <summary>Accessible name of the previous-year button (<see cref="DatePickerMode.Month"/>'s
    /// header). Override to localize.</summary>
    [Parameter] public string PrevYearLabel { get; set; } = "Previous year";
    /// <summary>Accessible name of the next-year button (<see cref="DatePickerMode.Month"/>'s
    /// header). Override to localize.</summary>
    [Parameter] public string NextYearLabel { get; set; } = "Next year";
    /// <summary>Accessible name of the previous-decade button (<see cref="DatePickerMode.Year"/>'s
    /// header). Override to localize.</summary>
    [Parameter] public string PrevDecadeLabel { get; set; } = "Previous decade";
    /// <summary>Accessible name of the next-decade button (<see cref="DatePickerMode.Year"/>'s
    /// header). Override to localize.</summary>
    [Parameter] public string NextDecadeLabel { get; set; } = "Next decade";
    /// <summary>Accessible name of the hour select (<see cref="DatePickerMode.Time"/> and
    /// <see cref="DatePickerMode.DateTime"/>'s time row). Override to localize.</summary>
    [Parameter] public string HourSelectLabel { get; set; } = "Hour";
    /// <summary>Accessible name of the minute select. Override to localize.</summary>
    [Parameter] public string MinuteSelectLabel { get; set; } = "Minute";
    /// <summary>Accessible name of the second select. Override to localize.</summary>
    [Parameter] public string SecondSelectLabel { get; set; } = "Second";
    /// <summary>Accessible name of the AM/PM period select (<see cref="Use12Hours"/>). Override to
    /// localize.</summary>
    [Parameter] public string PeriodSelectLabel { get; set; } = "AM/PM";
    /// <summary>Visible text of the OK button that closes the <see cref="DatePickerMode.Time"/>/
    /// <see cref="DatePickerMode.DateTime"/> panel. Override to localize.</summary>
    [Parameter] public string OkText { get; set; } = "OK";

    /// <summary>Leading text of the visually-hidden format hint the input's <c>aria-describedby</c>
    /// points at — rendered as "<c>{FormatHintLabel} {format}</c>", where the format is
    /// <see cref="Format"/>'s effective value (or <c>yyyy-Qn</c>/<c>yyyy-Www</c> in
    /// <see cref="DatePickerMode.Quarter"/>/<see cref="DatePickerMode.Week"/>'s own shorthand modes,
    /// which is what the field actually displays and parses there). Defaults to "Format:"; override to
    /// localize, or set to an empty string to drop the hint (and its <c>aria-describedby</c> token)
    /// entirely. Deliberately separate from <see cref="Placeholder"/>, which stays the Figma spec's
    /// visible "Select date".</summary>
    [Parameter] public string FormatHintLabel { get; set; } = "Format:";

    /// <summary>Leading text of the visually-hidden <see cref="Min"/> clause of the range hint the
    /// input's <c>aria-describedby</c> points at — rendered as "<c>{RangeHintMinLabel} {Min}</c>",
    /// beside <see cref="FormatHintLabel"/>'s format clause in the same element. Only rendered when
    /// <see cref="Min"/> is set (and never in <see cref="DatePickerMode.Time"/>, which ignores
    /// <see cref="Min"/>/<see cref="Max"/> entirely). Defaults to "Earliest date:"; override to
    /// localize, or set to an empty string to drop this clause. The bound is formatted with the same
    /// <see cref="Format"/> the field itself displays and parses, so the hint reads in the shape the
    /// user is expected to type. Exists because <see cref="Min"/>/<see cref="Max"/> otherwise reach
    /// the user only as per-cell <c>aria-disabled</c> in the calendar — invisible to someone typing,
    /// which is the faster path.</summary>
    [Parameter] public string RangeHintMinLabel { get; set; } = "Earliest date:";
    /// <summary>The <see cref="Max"/> clause of the same hint (see <see cref="RangeHintMinLabel"/>).
    /// Defaults to "Latest date:". With both bounds set the two clauses render together, period-
    /// separated, in <see cref="Min"/>-then-<see cref="Max"/> order.</summary>
    [Parameter] public string RangeHintMaxLabel { get; set; } = "Latest date:";

    /// <summary>Leading text of the week-number row header in <see cref="DatePickerMode.Week"/> —
    /// rendered as the accessible name "<c>{WeekLabel} {number}</c>" on each row's week-number cell,
    /// which is a <c>role="rowheader"</c> there (the row, not the day, is the selection unit in that
    /// mode, and the field displays <c>yyyy-Www</c>, so the grid has to expose the number for the two
    /// to be correlated). Override to localize. In every other mode the same cell stays
    /// <c>aria-hidden</c> decoration — <see cref="ShowWeekNumbers"/> changes nothing about what a day
    /// click commits, so its numbers are context, not structure.</summary>
    [Parameter] public string WeekLabel { get; set; } = "Week";

    // Validation-state ARIA passthrough onto the actual <input>, for form wrappers (EditDate).
    // Same shape as Select's AriaRequired/AriaInvalid/AriaDescribedBy trio (which EditSelectSearch
    // forwards) — AdditionalAttributes can't do this job because it lands on the outer wrapper div.

    /// <summary>Value for the input's <c>aria-required</c>; null (default) omits the attribute.</summary>
    [Parameter] public string? AriaRequired { get; set; }
    /// <summary>Renders <c>aria-invalid="true"</c> on the input when true.</summary>
    [Parameter] public bool AriaInvalid { get; set; }
    /// <summary>Value for the input's <c>aria-describedby</c>; null (default) omits the attribute.</summary>
    [Parameter] public string? AriaDescribedBy { get; set; }
    /// <summary>Value for the input's <c>aria-errormessage</c>; null (default) omits the attribute.
    /// Pair with <see cref="AriaInvalid"/>.</summary>
    [Parameter] public string? AriaErrorMessage { get; set; }

    /// <summary>
    /// Unmatched attributes (e.g. a consumer's <c>class</c>, <c>style</c>, or <c>data-*</c>),
    /// applied to the root wrapper (<c>.wss-picker</c>) — never the dropdown panel, whose inline
    /// placement is JS-owned. <c>class</c> and <c>style</c> merge with the component's own; the
    /// rest are splatted verbatim, except a same-named <c>onkeydown</c>, which is chained from this
    /// component's own wrapper handler (see <see cref="WrapperAttributes"/>).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>
    /// The wrapper's <c>@attributes</c> splat: the consumer's unmatched attributes minus
    /// <c>onkeydown</c>, which the same element binds explicitly
    /// (<see cref="OnWrapperKeyDownAsync"/>) and which is therefore chained from that handler rather
    /// than splatted — Blazor's last-wins duplicate-attribute rule would otherwise delete the
    /// consumer's handler outright. See <see cref="ConsumerEvent"/> for the ordering contract.
    /// </summary>
    IReadOnlyDictionary<string, object>? WrapperAttributes =>
        AttributeSplat.RestExcept(AdditionalAttributes, "onkeydown");

    // ----- State ------------------------------------------------------------
    // Shared JS-interop/overlay-lifecycle state (_wrapperRef, _panelRef, the two JsModule holders,
    // _open, _positioned, _disposed, _inputsWired, _openZIndex, _focusDay, _pendingFocusDate,
    // _pendingInputFocus, _suppressOpenOnFocus) lives on PickerBase.

    ElementReference _inputRef;
    ElementReference _gridRef;
    // Lazy fallback for BaseId when the consumer set no Id -- the panel/format-hint ids still have to
    // be unique per instance and stable across renders (aria-controls/aria-describedby resolve by id).
    string? _generatedId;
    // First-of-month shown in the panel.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text (null = show the formatted bound value).
    string? _edit;
    // The Value last seen on a parameter set -- what OnParametersSetAsync compares against to tell an
    // EXTERNAL change (a parent swapping the bound record, a form reset, a programmatic set) from a
    // re-render that left Value alone. Half-typed text belongs to the value it was typed against, so
    // an external change has to drop it; a re-render carrying the SAME value must not (that would eat
    // the keystrokes of a user typing while the parent happens to re-render).
    DateTime? _lastValueParam;

    // ----- Display helpers (used by the .razor markup) ------------------------

    string WrapperClass
    {
        get
        {
            var classes = "wss-picker wss-picker-single";
            if (_open) classes += " wss-picker-open";
            if (Disabled) classes += " wss-picker-disabled";
            if (Size == SelectSize.Small) classes += " wss-picker-sm";
            if (Size == SelectSize.Large) classes += " wss-picker-lg";
            return classes;
        }
    }

    // While open, C# owns the stack z-index (mirrored from placePanel's return value) and appends
    // it here, so a mid-open re-render re-emits a style that still carries it (see Select's
    // WidthStyle for the full story). Cleared on every close path.
    string? WrapperStyle
    {
        get
        {
            var width = string.IsNullOrEmpty(Width) ? null : $"width:{Width};";
            return ZIndexStyle(width);
        }
    }

    string Display => _edit ?? FormatDate(Value);

    bool ShowClear => AllowClear && !Disabled && Value is not null;

    // This picker has one mode concept, so PickerBase's shared display/parse layer (FormatDate/
    // TryParseDate) keys off Mode directly -- unlike DateRangePicker, which folds DateTime/Time onto
    // Date for its calendar-shape concerns and overrides this with that fold.
    internal override DatePickerMode EffectiveMode => Mode;

    // PickerBase's "was Format explicitly set" hook -- what gates Quarter's/Week's shorthand
    // display/parse (see FormatDate/TryParseDate there).
    internal override string? ExplicitFormat => Format;

    // Format/Placeholder resolution: an explicit value always wins; null falls through to Mode's
    // default (PickerMath.ModeDisplayFormat, shared with DateRangePicker and with EditDate/
    // EditDateRange's read-only display, which pass their own dash-separated bases). All internal
    // display/parse code routes through these (never the raw parameters), so a mode switch changes
    // behavior without a consumer having to also clear a stale Format/Placeholder. Quarter's and
    // Week's null-Format cases are a bland "yyyy" -- FormatDate never actually calls
    // ToString(EffectiveFormat) for either (see PickerBase.FormatDate's special cases); that value
    // only matters as TryParseDate's exact-format fallback attempt, tried after their own regex.
    internal override string EffectiveFormat =>
        Format ?? PickerMath.ModeDisplayFormat(Mode, "MM/dd/yyyy", "MM/yyyy", Use12Hours, ShowSeconds);

    string EffectivePlaceholder => Placeholder ?? Mode switch
    {
        DatePickerMode.Date => "Select date",
        DatePickerMode.Month => "Select month",
        DatePickerMode.DateTime => "Select date",
        DatePickerMode.Time => "Select time",
        DatePickerMode.Year => "Select year",
        DatePickerMode.Quarter => "Select quarter",
        DatePickerMode.Week => "Select week",
        _ => "Select date",
    };

    // The id everything else derives from: the consumer's Id when set (so a consumer-owned id keeps
    // driving the derived ones), otherwise a generated per-instance fallback. Same shape as
    // Select.BaseId/Tabs.BaseId.
    string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-picker-{Guid.NewGuid():N}");

    // The dropdown panel's own id -- what the input's aria-controls points at while open.
    string PanelId => $"{BaseId}-panel";

    // The visually-hidden typing hint's id, appended to the input's aria-describedby. Still named
    // "-format" (a published, test-anchored id) even though the element now also carries the
    // Min/Max clauses -- it is one hint about what may be typed, not two.
    string FormatHintId => $"{BaseId}-format";

    // "Format: MM/dd/yyyy" (or blank, which suppresses that clause).
    string FormatHintText =>
        string.IsNullOrEmpty(FormatHintLabel) ? string.Empty : $"{FormatHintLabel} {DescribedFormat}";

    // "Earliest date: 01/01/2026" / "Latest date: 12/31/2026" / both, period-separated -- the Min/Max
    // bounds as TEXT, which is the only channel someone typing (rather than clicking a cell) has for
    // them. Formatted with FormatDate, so the hint reads in exactly the shape the field parses.
    // Mode.Time contributes nothing: it ignores Min/Max outright (see their doc comments), so naming
    // them there would describe a constraint that isn't enforced.
    string RangeHintText
    {
        get
        {
            if (Mode == DatePickerMode.Time) return string.Empty;
            var min = Min is { } lo && !string.IsNullOrEmpty(RangeHintMinLabel)
                ? $"{RangeHintMinLabel} {FormatDate(lo)}" : string.Empty;
            var max = Max is { } hi && !string.IsNullOrEmpty(RangeHintMaxLabel)
                ? $"{RangeHintMaxLabel} {FormatDate(hi)}" : string.Empty;
            if (min.Length == 0) return max;
            return max.Length == 0 ? min : $"{min}. {max}";
        }
    }

    // The whole visually-hidden typing hint: the format clause, then the range clauses. Blank (both
    // suppressed, or nothing to say) drops the element AND its describedby token, exactly as blanking
    // FormatHintLabel alone always did.
    string HintText
    {
        get
        {
            var format = FormatHintText;
            var range = RangeHintText;
            if (format.Length == 0) return range;
            return range.Length == 0 ? format : $"{format}. {range}";
        }
    }

    // The consumer's own aria-describedby (a form wrapper's error/description ids -- see EditDate)
    // with the hint's id APPENDED, so the wrapper's chain keeps its order and the hint reads
    // last. Null (no hint, no consumer value) omits the attribute exactly as before this existed.
    string? EffectiveAriaDescribedBy => HintText.Length == 0
        ? AriaDescribedBy
        : string.IsNullOrEmpty(AriaDescribedBy) ? FormatHintId : $"{AriaDescribedBy} {FormatHintId}";

    // The week-number cell's ARIA in Mode.Week: a real row header naming the row the field's own
    // yyyy-Www display refers to. Every other mode leaves the cell aria-hidden decoration (see
    // WeekLabel), so both helpers answer null/"true" there and the markup is unchanged for them.
    string? WeekHeaderRole => Mode == DatePickerMode.Week ? "rowheader" : null;
    string? WeekHeaderHidden => Mode == DatePickerMode.Week ? null : "true";
    string? WeekHeaderLabel(DateTime weekStart) =>
        Mode == DatePickerMode.Week ? $"{WeekLabel} {WeekNumberOf(weekStart).ToString(PickerCulture)}" : null;

    // The month/year (or year, or decade) the panel currently displays: the day/month/quarter/year
    // grid's accessible name AND the text of the panel's aria-live region, so the name a screen
    // reader gives the grid and the string it announces after a navigation can never disagree.
    // Time mode has no calendar to name or navigate, so it contributes nothing.
    string ViewLabel => Mode switch
    {
        DatePickerMode.Month or DatePickerMode.Quarter => _viewMonth.Year.ToString(PickerCulture),
        DatePickerMode.Year => DecadeLabel,
        DatePickerMode.Time => string.Empty,
        _ => _viewMonth.ToString("MMMM yyyy", PickerCulture),
    };

    // ----- ARIA grid row grouping --------------------------------------------
    // The day grids get their rows for free (GridWeekRows -- the same 6x7 chunking the week-number
    // layout already rendered); the unit grids need the same treatment, chunked to match each grid's
    // own CSS grid-template-columns so a role="row" never spans a visual line break. The wrappers are
    // display:contents, so the buttons stay the real grid items and the layout is unchanged.

    // .wss-picker-month-grid is repeat(3, 1fr) -- Month mode's 12 months and Year mode's 12 years
    // both land as 4 rows of 3.
    const int MonthGridColumns = 3;
    // .wss-picker-quarter-grid is repeat(4, 1fr) -- one row of 4.
    const int QuarterGridColumns = 4;

    static IEnumerable<DateTime[]> UnitRows(IEnumerable<DateTime> units, int columns) => units.Chunk(columns);

    IEnumerable<DateTime> MonthUnits =>
        Enumerable.Range(1, 12).Select(m => new DateTime(_viewMonth.Year, m, 1));

    // The decade's own 10 years plus the leading/trailing dimmed adjacent-decade cells (ClampDecadeStart
    // guarantees both stay inside DateTime's representable range).
    IEnumerable<DateTime> YearUnits =>
        Enumerable.Range(-1, 12).Select(i => new DateTime(DecadeStart + i, 1, 1));

    IEnumerable<DateTime> QuarterUnits =>
        Enumerable.Range(1, 4).Select(q => QuarterStart(_viewMonth.Year, q));

    // Whether ShowToday's link renders for the CURRENTLY selected Mode -- Date/Month/Quarter/Year/
    // Week only; Time/DateTime have their own ShowNowLink instead (see below). Both booleans exist
    // so a consumer flipping ShowToday/ShowNow has no effect outside their own mode family, matching
    // the parameters' own doc comments.
    bool ShowTodayLink => ShowToday && Mode is not (DatePickerMode.Time or DatePickerMode.DateTime);

    // Whether ShowNow's link renders for the CURRENTLY selected Mode -- Time/DateTime only.
    bool ShowNowLink => ShowNow && Mode is DatePickerMode.Time or DatePickerMode.DateTime;

    // The Time/DateTime footer's class: the existing OK-only "wss-picker-footer" (flex-end) UNLESS
    // ShowNowLink actually renders alongside it, in which case wss-picker-footer-split switches the
    // row to space-between so the Now link lands on the left and OK stays pinned right. Gating this
    // on ShowNowLink (not just ShowNow) keeps an OK-only footer (ShowNow false, or Mode.Time/DateTime
    // never entered) pixel-identical to before this chunk -- the existing snapshot's whole point.
    string TimeFooterClass => ShowNowLink ? "wss-picker-footer wss-picker-footer-split" : "wss-picker-footer";

    // DateTime.Today/.Now normalized to Mode's own granularity -- the exact values ShowToday's/
    // ShowNow's links commit (and the values their disabled attribute/commit guard checks against).
    DateTime TodayForCommit => NormalizeForMode(DateTime.Today);
    DateTime NowForCommit => NormalizeForMode(DateTime.Now);

    string DayClass(DateTime day)
    {
        var cls = "wss-picker-day";
        if (day.Month != _viewMonth.Month) cls += " wss-picker-day-outside";
        if (IsToday(day)) cls += " wss-picker-day-today";
        // Week mode suppresses the single-day selected look -- the row is the selection unit there
        // (see IsDaySelected/wss-picker-week-row-selected), and every cell in the row still carries
        // aria-selected="true" via IsDaySelected below. The Mode guard is what keeps IsDaySelected's
        // whole-row answer from painting all 7 days selected; it is not redundant with it.
        if (Mode != DatePickerMode.Week && IsDaySelected(day)) cls += " wss-picker-day-selected";
        return cls;
    }

    // Whether `day` is today -- the visual channel is wss-picker-day-today (see DayClass), and the
    // accessibility channel is aria-current="date" on the day button (see the .razor markup), matching
    // what IsCurrentMonth/IsCurrentQuarter/IsCurrentYear already expose for the coarser grids.
    static bool IsToday(DateTime day) => day == DateTime.Today;

    // Whether `day`'s gridcell should render aria-selected="true": in every mode but Week, only the
    // exact selected day; in Week mode, every day sharing Value's week (the row is the selection
    // unit -- see DayClass's suppression of the single-day background above).
    bool IsDaySelected(DateTime day) =>
        Mode == DatePickerMode.Week
            ? Value is { } v && WeekStart(v.Date) == WeekStart(day)
            : day == Value?.Date;

    // The five Min/Max/DisabledDate predicates, each binding this instance's own parameters to the
    // matching PickerMath helper -- see those for the per-granularity contracts (DisabledDate is
    // folded into every one of them, so the cell `aria-disabled` attributes, the click guards that
    // back them, the DefaultFocus*/FirstEnabled* skip logic and IsDisabledForCommit's typed-text
    // guard can never disagree about what counts as disabled). They were character-identical to
    // DateRangePicker's own.
    // IsMonthDisabled uses the same month granularity PrevMonthDisabled/NextMonthDisabled use for the
    // day grid's header nav, so the two panels never disagree about where Min/Max stop navigation.
    bool IsDayDisabled(DateTime day) => PickerMath.IsDayDisabled(day, Min, Max, DisabledDate);

    bool IsMonthDisabled(DateTime month) => PickerMath.IsMonthDisabled(month, Min, Max, DisabledDate);

    bool IsYearDisabled(DateTime year) => PickerMath.IsYearDisabled(year, Min, Max, DisabledDate);

    bool IsQuarterDisabled(DateTime quarterStart) =>
        PickerMath.IsQuarterDisabled(quarterStart, Min, Max, DisabledDate);

    // Week granularity -- the one predicate whose bounds check isn't a plain comparison (see
    // PickerMath.IsWeekDisabledForCommit for the overflow-safe week end a typed commit in year 9999's
    // last week needs). `weekStart` is already WeekStart-shaped.
    bool IsWeekDisabledForCommit(DateTime weekStart) =>
        PickerMath.IsWeekDisabledForCommit(weekStart, Min, Max, DisabledDate);

    // Whether `value`'s time-of-day hits a DisabledTime-disabled hour/minute/second, evaluated
    // against `value`'s own date part -- the same argument contract the time row uses at render time
    // (see TimeRowFragment's DisabledParts), but against the value actually being committed rather
    // than the (possibly stale) bound Value. Invokes DisabledTime exactly once, shared by
    // ApplyTimePartAsync's select-change guard and IsDisabledForCommit's typed-text guard below so the
    // two can never disagree. Null callback / null return / null lists = nothing disabled.
    bool IsTimeDisabledForCommit(DateTime value)
    {
        var parts = DisabledTime?.Invoke(value.Date);
        return parts is not null &&
            (IsTimePartDisabled(parts.Hours, value.Hour) ||
             IsTimePartDisabled(parts.Minutes, value.Minute) ||
             IsTimePartDisabled(parts.Seconds, value.Second));
    }

    // Whether `value` is one of `disabled`'s listed values -- null (nothing disabled in that unit)
    // always answers false. Shared by IsTimeDisabledForCommit above and PickerTimeRow's per-option
    // render check so the disabled attribute and the commit guard can never disagree about the same
    // hour/minute/second. See PickerMath.IsTimePartDisabled for the pure implementation, shared with
    // DateRangePicker's own Time/DateTime session.
    static bool IsTimePartDisabled(IReadOnlyCollection<int>? disabled, int value) =>
        PickerMath.IsTimePartDisabled(disabled, value);

    // Whether a parsed/committed value (already mode-normalized) falls outside [Min, Max]/DisabledDate
    // at Mode's own granularity, or (Time/DateTime only) hits a DisabledTime-disabled hour/minute/
    // second. Date/DateTime check the day itself (Min/Max are date-only) via IsDayDisabled (which
    // already folds in DisabledDate); Month/Year/Quarter check their own granularity the same way;
    // Week checks week granularity via IsWeekDisabledForCommit. DateTime and Time both additionally
    // check IsTimeDisabledForCommit -- the other modes have no time-of-day concept, so DisabledTime
    // never applies to them.
    bool IsDisabledForCommit(DateTime value) => Mode switch
    {
        DatePickerMode.Date => IsDayDisabled(value),
        DatePickerMode.Month => IsMonthDisabled(value),
        DatePickerMode.DateTime => IsDayDisabled(value.Date) || IsTimeDisabledForCommit(value),
        DatePickerMode.Time => IsTimeDisabledForCommit(value),
        DatePickerMode.Year => IsYearDisabled(value),
        DatePickerMode.Quarter => IsQuarterDisabled(value),
        DatePickerMode.Week => IsWeekDisabledForCommit(value),
        _ => IsDayDisabled(value),
    };

    // PickerCulture lives on PickerBase (shared with DateRangePicker).

    string MonthName(int month) => PickerMath.MonthName(PickerCulture, month);

    // The value PickerBase's shared time row (Mode.Time/Mode.DateTime) displays and edits: the bound
    // value itself. There is no separate "in-progress" time state here -- a select change commits
    // immediately (see ApplyTimePartAsync below), so the next render always has the answer in Value.
    // (DateRangePicker's own override resolves the ACTIVE endpoint's pending session value instead.)
    internal override DateTime? TimeRowValue => Value;

    // The rest of the time row's inputs, forwarded from this control's own parameters -- see
    // PickerBase's own declarations for what each feeds. DisabledTime is invoked exactly once here,
    // for the whole row, against the bound value's own date part.
    internal override DisabledTimeParts? TimeRowDisabledParts => DisabledTime?.Invoke(Value?.Date);
    internal override bool TimeRowShowSeconds => ShowSeconds;
    internal override bool TimeRowUse12Hours => Use12Hours;
    internal override bool TimeRowHideDisabledOptions => HideDisabledTimeOptions;
    internal override int TimeRowHourStep => HourStep;
    internal override int TimeRowMinuteStep => MinuteStep;
    internal override int TimeRowSecondStep => SecondStep;
    internal override string? TimeRowHourLabel => HourSelectLabel;
    internal override string? TimeRowMinuteLabel => MinuteSelectLabel;
    internal override string? TimeRowSecondLabel => SecondSelectLabel;
    internal override string? TimeRowPeriodLabel => PeriodSelectLabel;

    // The years offered by the year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — see PickerMath.YearRange for the full contract (including the [1, 9999]
    // clamp -- OnYearSelectChanged applies the matching clamp to the value actually selected).
    (int From, int To) YearRange(int displayedYear) => PickerMath.YearRange(displayedYear, Min, Max);

    internal override DayOfWeek EffectiveFirstDayOfWeek =>
        PickerMath.FirstDayOfWeekOrCulture(FirstDayOfWeek, PickerCulture);

    // The weekday header row -- see PickerMath.WeekdayHeaders for the full contract (CLDR/narrow-form
    // note included).
    IEnumerable<string> WeekdayHeaders => PickerMath.WeekdayHeaders(PickerCulture, EffectiveFirstDayOfWeek);

    // The first day of the calendar week containing `day`, per EffectiveFirstDayOfWeek. Shared by
    // GridDays (the 42-cell layout) and Home/End keyboard navigation so they can never disagree.
    DateTime WeekStart(DateTime day) => PickerMath.WeekStart(day, EffectiveFirstDayOfWeek);

    // A fixed 6-row (42-cell) grid — covers every month/first-day combination, so the panel height
    // never jumps while navigating. Leading/trailing cells are the adjacent months' days.
    IEnumerable<DateTime> GridDays(DateTime month) => PickerMath.GridDays(month, EffectiveFirstDayOfWeek);

    // Whether the day grid renders as 6 week-number rows (Mode.Week always; ShowWeekNumbers's
    // column in Date/DateTime mode) instead of the flat 42-cell layout. Only one grid ever renders
    // for a given Mode (Month/Year/Quarter/Time have their own branches in the .razor markup), so
    // this only needs to gate the day-grid's own two layouts.
    bool ShowWeekRows => Mode == DatePickerMode.Week || ShowWeekNumbers;

    // GridDays(month) grouped into 6 rows of 7 -- used by the week-rows layout. Each row's first
    // entry is that row's own week start (GridDays begins on a week boundary and advances a whole
    // week at a time), which the markup reuses directly for the row's week-number cell and its
    // wss-picker-week-row-selected check.
    DateTime[][] GridWeekRows(DateTime month) => [.. GridDays(month).Chunk(7)];

    // The ISO-ish week number of the calendar week starting on `weekStart`, per the current
    // culture's week rule (mirrors WeekdayHeaders/WeekStart in following PickerCulture throughout).
    int WeekNumberOf(DateTime weekStart) => PickerMath.WeekNumberOf(weekStart, PickerCulture, EffectiveFirstDayOfWeek);

    // Is `weekStart` the row containing Value's week? Only meaningful in Mode.Week -- ShowWeekNumbers
    // in Date/DateTime mode renders the same rows layout with NO selection-styling change (day clicks
    // still commit days, so there's no "selected week" to band there).
    bool IsSelectedWeekRow(DateTime weekStart) =>
        Mode == DatePickerMode.Week && Value is { } v && WeekStart(v.Date) == weekStart;

    string WeekRowClass(DateTime weekStart) =>
        IsSelectedWeekRow(weekStart) ? "wss-picker-week-row wss-picker-week-row-selected" : "wss-picker-week-row";

    // FormatDate (Quarter's/Week's hand-rolled displays included) and TryParseDate (their inverse
    // shorthands included) live on PickerBase, over the EffectiveMode/ExplicitFormat/EffectiveFormat/
    // EffectiveFirstDayOfWeek/NormalizeForMode hooks this class supplies -- they were character-
    // identical to DateRangePicker's own apart from which mode each keys off.

    // Central per-mode normalization, shared by PickerBase.TryParseDate and SetValueAsync so every
    // commit path (click, typed text, select change) agrees on the same shape of value. See
    // PickerMath.NormalizeForMode for the per-mode rules (ShowSeconds zeroing, the Time-mode Today
    // anchor, etc.).
    internal override DateTime NormalizeForMode(DateTime value) =>
        PickerMath.NormalizeForMode(Mode, EffectiveFirstDayOfWeek, ShowSeconds, value);

    static DateTime FirstOfMonth(DateTime value) => PickerMath.FirstOfMonth(value);

    static DateTime FirstOfYear(DateTime value) => PickerMath.FirstOfYear(value);

    // The quarter (1-4) `value`'s month falls in.
    static int QuarterOf(DateTime value) => PickerMath.QuarterOf(value);

    // The 1st of `quarter`'s (1-4) first month in `year`.
    static DateTime QuarterStart(int year, int quarter) => PickerMath.QuarterStart(year, quarter);

    // The 1st of the quarter containing `value`.
    static DateTime QuarterStart(DateTime value) => PickerMath.QuarterStart(value);

    // The displayed month, clamped so the 42-cell grid can never overflow DateTime's range.
    static DateTime ClampView(DateTime firstOfMonth) => PickerMath.ClampView(firstOfMonth);

    // ----- Roving-tabindex keyboard navigation -------------------------------

    // Is `day` inside the currently displayed month? (DatePicker shows exactly one.)
    bool IsVisible(DateTime day) => FirstOfMonth(day) == _viewMonth;

    // The day the grid's roving tabindex targets when no keyboard navigation has moved it yet (or
    // the last-moved day scrolled out of view via the month/year selects or the nav buttons): the
    // bound value if it's in the displayed month, else today if it's in the displayed month, else
    // the 1st of the month — mirrors AntD's default calendar focus.
    DateTime DefaultFocusDay()
    {
        if (Value is { } v && IsVisible(v.Date) && !IsDayDisabled(v.Date)) return v.Date;
        if (IsVisible(DateTime.Today) && !IsDayDisabled(DateTime.Today)) return DateTime.Today;
        // Neither natural candidate is usable (disabled — e.g. Min in the future with no value set,
        // so today falls before it). Land on the first enabled in-month day instead; if the whole
        // visible month is disabled there's nothing actionable in it either way, so any deterministic
        // in-month day (the 1st) is fine.
        //
        // This skip used to be load-bearing for keyboard REACHABILITY: with natively `disabled` cells
        // a tabindex="0" that was also disabled gave the grid zero tab stops. Disabled cells are now
        // aria-disabled and stay focusable (see DayButtonFragment), so the grid is reachable either
        // way and this is now purely about where focus LANDS -- the APG/AntD behavior of opening on
        // something the user can actually pick, rather than on a dead cell. Kept for that reason.
        return FirstEnabledDay(_viewMonth) ?? _viewMonth;
    }

    // The first enabled, in-month day in `month`'s grid, or null if every in-month day is disabled --
    // see PickerMath.FirstEnabledDay (shared verbatim with DateRangePicker, which scans both panels'
    // months through it in turn).
    DateTime? FirstEnabledDay(DateTime month) =>
        PickerMath.FirstEnabledDay(month, EffectiveFirstDayOfWeek, Min, Max, DisabledDate);

    // _focusDay once a keyboard move has set it, but only while it's still on-screen — a month/year
    // select change (or a nav button) clears _focusDay explicitly, but this guard also covers any
    // path that doesn't, so the grid is never left with zero tabbable cells.
    DateTime EffectiveFocusDay => _focusDay is { } f && IsVisible(f) ? f : DefaultFocusDay();

    // True for the one day button that carries tabindex="0" — the in-month rendering of
    // EffectiveFocusDay. (A leading/trailing adjacent-month cell showing the same date never wins:
    // day.Month/Year must match the grid's own month.)
    bool IsFocusStop(DateTime day) =>
        day.Month == _viewMonth.Month && day.Year == _viewMonth.Year && day == EffectiveFocusDay;

    // Maps a keydown's Key to the day it should move focus to, or null when the key isn't a
    // navigation key -- see PickerMath.NextFocusDay for the arrow/Home/End/PageUp/PageDown map and
    // its edge-of-range try/catch.
    DateTime? NextFocusDay(DateTime current, string key) => PickerMath.NextFocusDay(current, key, EffectiveFirstDayOfWeek);

    // Grid keydown: moves the roving-tabindex day, retargeting the displayed month when navigation
    // crosses out of it (clamped exactly like the month/year selects). A day that lands disabled
    // (Min/Max/DisabledDate) still becomes the focus target -- the APG grid behavior, and what lets
    // Left/Right keep stepping day-by-day THROUGH a disabled run instead of jumping it. That is only
    // safe because a rejected day now renders aria-disabled rather than natively `disabled` (see
    // DayButtonFragment): it stays focusable, keeps the grid's single tab stop real, and can't blur
    // focus to <body> when a month-crossing re-render lands the roving stop on it. Activation is
    // blocked by OnDayClickAsync's own guard, not by the browser, so Enter/Space on a focused
    // disabled cell no-ops too. The actual DOM focus move (needed whenever the grid re-renders with
    // new button instances, i.e. any month change) happens in OnAfterRenderAsync via
    // _pendingFocusDate. wss-picker.js suppresses the browser's native scroll for these keys when JS
    // is available; without it this state still updates, just without the DOM focus follow or the
    // scroll suppression.
    void OnGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusDay(EffectiveFocusDay, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        var nextMonth = FirstOfMonth(next.Value);
        if (nextMonth != _viewMonth) _viewMonth = ClampView(nextMonth);
        _pendingFocusDate = next.Value;
    }

    // ----- Prev/next month navigation ----------------------------------------

    // Disables at the representable DateTime range (ClampView) as before, and now also at the
    // Min/Max month: prev stops once the view is already on Min's month (one further back would be
    // entirely before Min), next stops once the view is already on Max's month — the same
    // month-level granularity YearRange uses for the year select, so the two header mechanisms never
    // disagree about where navigation runs out.
    bool PrevMonthDisabled =>
        ClampView(_viewMonth.AddMonths(-1)) == _viewMonth ||
        (Min is { } min && _viewMonth <= FirstOfMonth(min));
    bool NextMonthDisabled =>
        ClampView(_viewMonth.AddMonths(1)) == _viewMonth ||
        (Max is { } max && _viewMonth >= FirstOfMonth(max));

    void PrevMonth()
    {
        _viewMonth = ClampView(_viewMonth.AddMonths(-1));
        _focusDay = null; // recompute the roving-focus default against the newly shown month
    }

    void NextMonth()
    {
        _viewMonth = ClampView(_viewMonth.AddMonths(1));
        _focusDay = null;
    }

    // ----- Month mode: grid + year navigation --------------------------------
    // Mirrors the day-grid machinery above one level up (year/month instead of month/day) and
    // shares the same _focusDay/_pendingFocusDate/_pendingInputFocus state -- only one grid ever
    // renders for a given Mode, so there's no risk of the two meanings colliding.

    // Is `month` inside the currently displayed year? (The Month-mode grid always shows all 12.)
    bool IsVisibleMonth(DateTime month) => month.Year == _viewMonth.Year;

    bool IsSelectedMonth(DateTime month) => Value is { } v && FirstOfMonth(v) == month;

    // The selected FILL used to key off the button's own aria-pressed attribute. That state moved to
    // aria-selected on the enclosing role="gridcell" (aria-selected is not a valid attribute on
    // role="button", and an APG grid puts selection on the cell anyway), so the VISUAL state needs a
    // class of its own -- mirroring wss-picker-day-selected, which the day grid has always used for
    // exactly this reason. Shared by Month/Quarter/Year mode (all three render wss-picker-month-btn).
    string MonthButtonClass(DateTime month) =>
        IsSelectedMonth(month) ? "wss-picker-month-btn wss-picker-month-btn-selected" : "wss-picker-month-btn";

    bool IsCurrentMonth(DateTime month) => month == FirstOfMonth(DateTime.Today);

    // The month the grid's roving tabindex targets when no keyboard navigation has moved it yet: the
    // bound value's month if it's in the displayed year, else the current month if it's in the
    // displayed year, else the first enabled month of the year — mirrors DefaultFocusDay.
    DateTime DefaultFocusMonth()
    {
        if (Value is { } v && IsVisibleMonth(FirstOfMonth(v)) && !IsMonthDisabled(FirstOfMonth(v))) return FirstOfMonth(v);
        var today = FirstOfMonth(DateTime.Today);
        if (IsVisibleMonth(today) && !IsMonthDisabled(today)) return today;
        return FirstEnabledMonth(_viewMonth.Year) ?? new DateTime(_viewMonth.Year, 1, 1);
    }

    // The first enabled month of `year`, or null if every month that year is disabled.
    DateTime? FirstEnabledMonth(int year) => PickerMath.FirstEnabledMonth(year, Min, Max, DisabledDate);

    DateTime EffectiveFocusMonth => _focusDay is { } f && IsVisibleMonth(f) ? f : DefaultFocusMonth();

    bool IsMonthFocusStop(DateTime month) => month == EffectiveFocusMonth;

    // Maps a keydown's Key to the month it should move focus to, or null when the key isn't a
    // navigation key -- see PickerMath.NextFocusMonth for the arrow/Home/End/PageUp/PageDown map
    // (shared with DateRangePicker's Month range mode) and its edge-of-range try/catch.
    DateTime? NextFocusMonth(DateTime current, string key) => PickerMath.NextFocusMonth(current, key);

    // Grid keydown: moves the roving-tabindex month, retargeting the displayed year when navigation
    // crosses out of it. The actual DOM focus move happens in OnAfterRenderAsync via
    // _pendingFocusDate, same as the day grid's OnGridKeyDown.
    void OnMonthGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusMonth(EffectiveFocusMonth, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        if (next.Value.Year != _viewMonth.Year) _viewMonth = next.Value;
        _pendingFocusDate = next.Value;
    }

    // Disables at the representable DateTime year range and at the Min/Max year — same shape as
    // PrevMonthDisabled/NextMonthDisabled one granularity up.
    bool PrevYearDisabled =>
        _viewMonth.Year <= 1 ||
        (Min is { } min && _viewMonth.Year <= min.Year);
    bool NextYearDisabled =>
        _viewMonth.Year >= 9999 ||
        (Max is { } max && _viewMonth.Year >= max.Year);

    void PrevYear()
    {
        _viewMonth = new DateTime(Math.Clamp(_viewMonth.Year - 1, 1, 9999), _viewMonth.Month, 1);
        _focusDay = null; // recompute the roving-focus default against the newly shown year
    }

    void NextYear()
    {
        _viewMonth = new DateTime(Math.Clamp(_viewMonth.Year + 1, 1, 9999), _viewMonth.Month, 1);
        _focusDay = null;
    }

    // ----- Year mode: decade grid + navigation -------------------------------
    // Mirrors the Month-mode section above one level up again (decade/year instead of year/month).
    // Reuses wss-picker-month-btn/wss-picker-month-grid (so wss-picker.js's keyboard suppression and
    // focusDay lookup work unchanged) plus a wss-picker-month-btn-outside modifier for the two
    // dimmed adjacent-decade cells. Shares _focusDay/_pendingFocusDate the same way Month mode does.

    // Clamps a decade-start candidate so the decade's own leading/trailing dimmed cells always land
    // inside DateTime's representable year range -- see PickerMath.ClampDecadeStart for the full
    // rationale.
    static int ClampDecadeStart(int year) => PickerMath.ClampDecadeStart(year);

    // The decade the grid currently displays, floored to a multiple of 10.
    int DecadeStart => ClampDecadeStart(_viewMonth.Year);

    // "2020-2029" style, both years in PickerCulture digits.
    string DecadeLabel => $"{DecadeStart.ToString(PickerCulture)}-{(DecadeStart + 9).ToString(PickerCulture)}";

    // Is `year` one of the decade's own 10 years (as opposed to one of the 2 dimmed adjacent-decade
    // cells)?
    bool IsYearInDecade(int year) => year >= DecadeStart && year <= DecadeStart + 9;

    // Same selected-modifier story as MonthButtonClass above, plus the dimmed adjacent-decade cells.
    string YearButtonClass(int year)
    {
        var cls = IsYearInDecade(year) ? "wss-picker-month-btn" : "wss-picker-month-btn wss-picker-month-btn-outside";
        if (IsSelectedYear(year)) cls += " wss-picker-month-btn-selected";
        return cls;
    }

    bool IsSelectedYear(int year) => Value is { } v && v.Year == year;

    bool IsCurrentYear(int year) => year == DateTime.Today.Year;

    // The year the grid's roving tabindex targets when no keyboard navigation has moved it yet: the
    // bound value's year if it's one of the displayed decade's own years, else the current year if
    // so, else the first enabled year of the decade -- mirrors DefaultFocusMonth.
    DateTime DefaultFocusYear()
    {
        if (Value is { } v && IsYearInDecade(v.Year) && !IsYearDisabled(FirstOfYear(v))) return FirstOfYear(v);
        var today = DateTime.Today;
        if (IsYearInDecade(today.Year) && !IsYearDisabled(FirstOfYear(today))) return FirstOfYear(today);
        return FirstEnabledYear() ?? new DateTime(DecadeStart, 1, 1);
    }

    // The first enabled year of the decade's own 10 years (never one of the dimmed adjacent-decade
    // cells), or null if every year in the decade is disabled.
    DateTime? FirstEnabledYear() => PickerMath.FirstEnabledYear(DecadeStart, Min, Max, DisabledDate);

    DateTime EffectiveFocusYear => _focusDay is { } f && IsYearInDecade(f.Year) ? f : DefaultFocusYear();

    bool IsYearFocusStop(int year) => new DateTime(year, 1, 1) == EffectiveFocusYear;

    // Maps a keydown's Key to the year it should move focus to, or null when the key isn't a
    // navigation key -- see PickerMath.NextFocusYear for the arrow/Home/End/PageUp/PageDown map
    // (shared with DateRangePicker's Year range mode, which passes whichever of its two panels'
    // decades the current focus belongs to) and its [1, 9999] clamp.
    DateTime? NextFocusYear(DateTime current, string key) => PickerMath.NextFocusYear(current, key, DecadeStart);

    // Grid keydown: moves the roving-tabindex year, retargeting the displayed decade when
    // navigation crosses out of it. The actual DOM focus move happens in OnAfterRenderAsync via
    // _pendingFocusDate, same as the day/month grids' keydown handlers.
    void OnYearGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusYear(EffectiveFocusYear, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        var nextDecade = ClampDecadeStart(next.Value.Year);
        if (nextDecade != DecadeStart) _viewMonth = new DateTime(nextDecade, _viewMonth.Month, 1);
        _pendingFocusDate = next.Value;
    }

    // Disables at the representable DateTime year range (via ClampDecadeStart's own margin) and at
    // the Min/Max decade -- same shape as PrevYearDisabled/NextYearDisabled one granularity up.
    bool PrevDecadeDisabled =>
        ClampDecadeStart(DecadeStart - 10) == DecadeStart ||
        (Min is { } min && DecadeStart <= ClampDecadeStart(min.Year));
    bool NextDecadeDisabled =>
        ClampDecadeStart(DecadeStart + 10) == DecadeStart ||
        (Max is { } max && DecadeStart >= ClampDecadeStart(max.Year));

    void PrevDecade()
    {
        _viewMonth = new DateTime(ClampDecadeStart(DecadeStart - 10), _viewMonth.Month, 1);
        _focusDay = null; // recompute the roving-focus default against the newly shown decade
    }

    void NextDecade()
    {
        _viewMonth = new DateTime(ClampDecadeStart(DecadeStart + 10), _viewMonth.Month, 1);
        _focusDay = null;
    }

    async Task OnYearClickAsync(int year)
    {
        // Same aria-disabled contract as OnDayClickAsync (see there).
        var yearStart = new DateTime(year, 1, 1);
        if (IsYearDisabled(yearStart)) return;
        // A grid pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(yearStart);
        _pendingInputFocus = true; // the clicked year button is about to unmount
        await CloseAsync();
    }

    // ----- Quarter mode: grid + keyboard navigation --------------------------
    // The header is Month mode's verbatim (YearHeaderFragment in DatePicker.razor) -- only the grid
    // differs: a single row of 4 quarter buttons instead of a 3x4 month grid. Shares
    // _focusDay/_pendingFocusDate the same way Month mode does; only one grid ever renders for a
    // given Mode.

    bool IsSelectedQuarter(int year, int quarter) => Value is { } v && v.Year == year && QuarterOf(v) == quarter;

    // Same selected-modifier story as MonthButtonClass (Quarter mode reuses wss-picker-month-btn).
    string QuarterButtonClass(int year, int quarter) =>
        IsSelectedQuarter(year, quarter) ? "wss-picker-month-btn wss-picker-month-btn-selected" : "wss-picker-month-btn";

    bool IsCurrentQuarter(int year, int quarter) => year == DateTime.Today.Year && quarter == QuarterOf(DateTime.Today);

    // Is `year` the one the quarter grid currently shows? (The grid always shows all 4 quarters of
    // _viewMonth.Year -- no adjacent-year dimmed cells, unlike the year grid's decade.)
    bool IsVisibleQuarterYear(int year) => year == _viewMonth.Year;

    // The quarter the grid's roving tabindex targets when no keyboard navigation has moved it yet --
    // mirrors DefaultFocusMonth/DefaultFocusYear one granularity over.
    DateTime DefaultFocusQuarter()
    {
        if (Value is { } v && IsVisibleQuarterYear(v.Year) && !IsQuarterDisabled(QuarterStart(v))) return QuarterStart(v);
        var today = DateTime.Today;
        if (IsVisibleQuarterYear(today.Year) && !IsQuarterDisabled(QuarterStart(today))) return QuarterStart(today);
        return FirstEnabledQuarter(_viewMonth.Year) ?? QuarterStart(_viewMonth.Year, 1);
    }

    // The first enabled quarter of `year`, or null if every quarter that year is disabled.
    DateTime? FirstEnabledQuarter(int year) => PickerMath.FirstEnabledQuarter(year, Min, Max, DisabledDate);

    DateTime EffectiveFocusQuarter => _focusDay is { } f && IsVisibleQuarterYear(f.Year) ? f : DefaultFocusQuarter();

    bool IsQuarterFocusStop(int year, int quarter) => QuarterStart(year, quarter) == EffectiveFocusQuarter;

    // Maps a keydown's Key to the quarter it should move focus to, or null when the key isn't a
    // navigation key -- see PickerMath.NextFocusQuarter for the arrow/Home/End/PageUp/PageDown map
    // (shared with DateRangePicker's Quarter range mode) and its edge-of-range try/catch.
    DateTime? NextFocusQuarter(DateTime current, string key) => PickerMath.NextFocusQuarter(current, key);

    // Grid keydown: moves the roving-tabindex quarter, retargeting the displayed year when
    // navigation crosses out of it. The actual DOM focus move happens in OnAfterRenderAsync via
    // _pendingFocusDate, same as the other grids' keydown handlers.
    void OnQuarterGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusQuarter(EffectiveFocusQuarter, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        if (next.Value.Year != _viewMonth.Year) _viewMonth = next.Value;
        _pendingFocusDate = next.Value;
    }

    async Task OnQuarterClickAsync(int year, int quarter)
    {
        // Same aria-disabled contract as OnDayClickAsync (see there).
        var quarterStart = QuarterStart(year, quarter);
        if (IsQuarterDisabled(quarterStart)) return;
        // A grid pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(quarterStart);
        _pendingInputFocus = true; // the clicked quarter button is about to unmount
        await CloseAsync();
    }

    // ----- Parameter reconciliation ------------------------------------------

    /// <summary>
    /// Two parameter-driven invariants, both about state this control holds that a changed parameter
    /// invalidates.
    /// <para>
    /// <b>Disabled =&gt; closed</b>, mirroring <c>Select.OnParametersSetAsync</c>'s identical invariant.
    /// Without it, a panel that was open when the consumer flipped <see cref="Disabled"/> stays fully
    /// interactive: every day/month/year/quarter cell, preset, footer link and header select goes on
    /// committing through <see cref="SetValueAsync"/> on a control the consumer has taken out of
    /// service (only the clear button, the field click and the input focus were ever guarded). Routed
    /// through the normal <see cref="CloseAsync"/> so the JS/focus teardown runs exactly as it does
    /// for a user-driven close.
    /// </para>
    /// <para>
    /// <b>An external <see cref="Value"/> change discards the half-typed text</b> (see
    /// <c>_lastValueParam</c>): the buffer belongs to the value it was typed against, so a swapped
    /// bound record must not display -- or, on the next Enter/blur, commit -- the previous record's
    /// keystrokes.
    /// </para>
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (Disabled && _open) await CloseAsync();

        if (Value != _lastValueParam)
        {
            _lastValueParam = Value;
            _edit = null;
        }
    }

    // ----- Interaction ------------------------------------------------------

    Task OnFieldClickAsync()
    {
        // A click on the field's non-input chrome opens too. (A click on the input already opened
        // via its focus event — this is then a no-op.)
        if (!Disabled && !_open) Open();
        return Task.CompletedTask;
    }

    void OnInputFocus()
    {
        if (_suppressOpenOnFocus) { _suppressOpenOnFocus = false; return; }
        if (!Disabled && !_open) Open();
    }

    void Open()
    {
        _open = true;
        _edit = null;
        _focusDay = null;
        _pendingInputFocus = false;
        // DefaultViewDate (AntD's defaultPickerValue) only matters when there's no bound Value to
        // anchor on -- a set Value always wins, same precedence FormDefaults-style parameters use
        // elsewhere in the kit.
        var anchor = Value ?? DefaultViewDate ?? DateTime.Today;
        // Year mode's initial view only needs a year-granularity clamp (ClampDecadeStart already
        // guarantees a safe decade below) -- routing it through ClampView's day-grid-oriented
        // one-month buffer would sacrifice up to a whole year at the DateTime range's edges for a
        // margin this mode doesn't need.
        _viewMonth = Mode == DatePickerMode.Year
            ? new DateTime(Math.Clamp(anchor.Year, 1, 9999), 1, 1)
            : ClampView(FirstOfMonth(anchor));
    }

    Task CloseAsync()
    {
        _open = false;
        _edit = null;
        _focusDay = null;
        _pendingFocusDate = null;
        // Give up the C#-owned open z-index on the logical close path (the OnAfterRender close
        // branch also nulls it and runs clearZ as the DOM-side teardown).
        _openZIndex = null;
        // No StateHasChanged: every caller is an event handler, after which Blazor re-renders.
        return Task.CompletedTask;
    }

    // Escape closes (discarding any in-progress edit); Enter commits the typed text and, when it
    // committed something (or cleared), closes — a single-date pick is complete after one commit.
    // (The input's native form-submit default on Enter is suppressed by initPicker when JS is
    // available.)
    //
    // A consumer's own splatted onkeydown is chained AFTER this component's handling, and
    // unconditionally -- every key reaches them, not just the two this picker acts on. See
    // WrapperAttributes.
    async Task OnWrapperKeyDownAsync(KeyboardEventArgs e)
    {
        await OnWrapperKeyDownCoreAsync(e);
        await ConsumerEvent.InvokeAsync(AdditionalAttributes, "onkeydown", e);
    }

    async Task OnWrapperKeyDownCoreAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":
                // Reaching the wrapper's keydown at all means some descendant (the input, a day
                // button, a month/year select) had focus — restore it to the text input on close.
                if (_open)
                {
                    _pendingInputFocus = true;
                    await CloseAsync();
                }
                break;
            case "Enter":
                if (await CommitTextAsync())
                {
                    _pendingInputFocus = true;
                    await CloseAsync();
                }
                break;
        }
    }

    async Task OnDayClickAsync(DateTime day)
    {
        // The cell is aria-disabled, not natively `disabled` (it has to stay focusable -- see
        // DayButtonFragment), so the browser DOES dispatch this click, and an Enter/Space on a
        // keyboard-focused disabled cell synthesizes one too. This guard is what makes the state
        // honest: exactly the predicate the attribute rendered from, so the cell and the commit can
        // never disagree.
        if (IsDayDisabled(day)) return;
        // Week mode's day BUTTON stays at day granularity (IsDayDisabled(day), same as every other
        // mode's day cell -- see IsDayDisabled's doc comment), but the click's actual commit lands on
        // the week START, not the clicked day. With Min/Max alone those two checks can never disagree
        // (a week whose start/end falls outside [Min, Max] means every day in it does too, so a
        // disabled week never has an enabled day button to click), but DisabledDate is an arbitrary
        // predicate -- it can reject a week start while leaving every individual day in that week
        // enabled. Guard it here explicitly, mirroring the typed-text path's IsDisabledForCommit
        // check, so a click can't slip past DisabledDate the way SetValueAsync itself never checks.
        if (Mode == DatePickerMode.Week && IsWeekDisabledForCommit(WeekStart(day))) return;
        // Mode.DateTime keeps whatever time-of-day is already committed (or midnight) instead of
        // zeroing it out -- the day calendar only ever supplies the date part there, the time row
        // below it owns the rest. Mode.Date is unaffected: adding TimeSpan.Zero is a no-op.
        var time = Mode == DatePickerMode.DateTime ? Value?.TimeOfDay ?? TimeSpan.Zero : TimeSpan.Zero;
        var composed = day + time;
        // The guard at the top of this method already covers IsDayDisabled, but nothing covers
        // the carried time-of-day: DisabledTime is evaluated per DATE, so the clicked day can disable
        // the very hour/minute/second the current value carries onto it. Both other commit paths
        // reject exactly that (the typed path via IsDisabledForCommit, the time selects via
        // ApplyTimePartAsync), so guard it here too -- a no-op rejection, same as theirs, leaving the
        // bound value (and the panel) exactly as they were. Same shape as the Week guard above: a
        // rejected click never reaches the _edit clear below either.
        //
        // Guard the NORMALIZED value, not the raw composition: SetValueAsync normalizes before
        // committing, and Mode.DateTime's normalization zeroes the second when ShowSeconds is false.
        // Testing the raw value would reject a click over a stale second that no select in the row
        // can even change and that the commit itself would discard -- the exact bug
        // ApplyTimePartAsync's own ComposeTimePart zeroing exists to prevent. (Only the DateTime arm
        // is normalized here; Mode.Week's normalization would move the value to its week start,
        // which OnDayClick's own Week guard above already handles at week granularity.)
        if (Mode == DatePickerMode.DateTime)
        {
            composed = NormalizeForMode(composed);
            if (IsTimeDisabledForCommit(composed)) return;
        }
        // A calendar pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(composed);
        // Mode.DateTime leaves the panel open -- the user may still want to adjust the time, and OK
        // is that mode's close signal. Mode.Date completes the pick immediately, as before.
        if (Mode == DatePickerMode.DateTime) return;
        _pendingInputFocus = true; // the clicked day button is about to unmount
        await CloseAsync();
    }

    async Task OnMonthClickAsync(DateTime month)
    {
        // Same aria-disabled contract as OnDayClickAsync: the cell stays focusable, so the click
        // (and Enter/Space on it) reaches here and this guard -- not the browser -- rejects it.
        if (IsMonthDisabled(month)) return;
        // A grid pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(month);
        _pendingInputFocus = true; // the clicked month button is about to unmount
        await CloseAsync();
    }

    // ShowToday's link (Date/Month/Quarter/Year/Week): commits today, mode-normalized, and closes --
    // a complete pick, same as a day/month/year/quarter click. Guarded the same way a typed commit
    // is (IsDisabledForCommit) rather than relying solely on the button's own `disabled` attribute, so
    // a caller that invokes this directly (or a test harness that doesn't honor `disabled`) can never
    // slip a rejected value past the guard.
    async Task OnTodayClickAsync()
    {
        var today = TodayForCommit;
        if (IsDisabledForCommit(today)) return;
        _edit = null;
        await SetValueAsync(today);
        _pendingInputFocus = true; // the clicked link is about to unmount
        await CloseAsync();
    }

    // ShowNow's link (Time/DateTime): commits DateTime.Now, mode-normalized, WITHOUT closing --
    // mirrors ApplyTimePartAsync's incremental commit model for those two modes; OK remains the
    // close signal.
    async Task OnNowClickAsync()
    {
        var now = NowForCommit;
        if (IsDisabledForCommit(now)) return;
        _edit = null;
        await SetValueAsync(now);
    }

    // A preset click (any Mode): resolve at click time, normalize to Mode's own granularity, guard
    // exactly like a typed commit, and -- unlike the incremental Time/DateTime selects -- always
    // close, because a preset is a complete pick in every mode.
    async Task OnPresetClickAsync(DatePickerPreset preset)
    {
        var value = NormalizeForMode(preset.Resolve());
        if (IsDisabledForCommit(value)) return;
        _edit = null;
        await SetValueAsync(value);
        _pendingInputFocus = true; // the clicked preset button is about to unmount
        await CloseAsync();
    }

    // The clear button unmounts the instant the value goes (ShowClear turns false), so without the
    // reclaim below DOM focus fell to <body> -- and the panel is open essentially always (the field
    // opens on focus), which put Escape out of reach of the wrapper's own keydown and left a live
    // role="dialog" behind a full-viewport backdrop that only a mouse could dismiss. Every other
    // panel action that unmounts its own trigger already sets this; the clear was the one that
    // didn't. Reclaiming focus also announces the now-empty field on arrival, which is the clear's
    // own confirmation.
    async Task ClearAsync()
    {
        if (Disabled) return;
        _edit = null;
        _pendingInputFocus = true;
        await SetValueAsync(null);
    }

    // Commits the in-progress typed text. Returns true when it changed/kept a committed state
    // (parsed date or explicit clear) — false when there was nothing to commit or the text was
    // invalid/out-of-range (which reverts to the formatted bound value).
    async Task<bool> CommitTextAsync()
    {
        if (_edit is null) return false;
        var text = _edit.Trim();
        _edit = null;
        if (text.Length == 0)
        {
            if (Value is not null) await SetValueAsync(null);
            return true;
        }
        if (!TryParseDate(text, out var day))
        {
            // Unparseable, as opposed to a well-formed date IsDisabledForCommit rejects below -- only
            // this case is a genuine parse failure (see OnParseError's doc comment for why the
            // distinction matters).
            if (OnParseError.HasDelegate) await OnParseError.InvokeAsync(text);
            return false;
        }
        if (IsDisabledForCommit(day))
        {
            // A well-formed date this picker won't accept (Min/Max/DisabledDate/DisabledTime). This
            // used to return in TOTAL SILENCE: the field reverted, Value never changed, so a host
            // form control never learned anything happened and no validator ran -- a keyboard-only
            // user got no signal at all. Distinct from OnParseError above, which is parse failure
            // only, so a wrapper can word the two differently (see EditDate.RangeErrorMessage).
            if (OnRangeError.HasDelegate) await OnRangeError.InvokeAsync(text);
            return false;
        }
        await SetValueAsync(day);
        _viewMonth = ClampView(FirstOfMonth(day));
        return true;
    }

    // Central commit: normalizes to Mode's shape and raises the callback only when it actually changed.
    // Defense in depth for OnParametersSetAsync's Disabled => closed invariant: that closes the panel
    // (unmounting every cell) the moment Disabled is observed, so nothing reachable gets this far --
    // but every commit path in the control funnels through here, so one guard makes it structurally
    // impossible for a disabled picker to write through, whatever route a caller (or an event queued
    // against the pre-disable render tree) takes to reach it.
    async Task SetValueAsync(DateTime? value)
    {
        if (Disabled) return;
        value = value is { } v ? NormalizeForMode(v) : null;
        // Before the dedup below, and unconditionally: this says "an accepted value was committed",
        // which is true even when it matches what is already bound. A host form control's stale
        // parse/range error has to clear on THAT case too, and the dedup means ValueChanged can never
        // carry the news -- see OnValidCommit.
        if (OnValidCommit.HasDelegate) await OnValidCommit.InvokeAsync();
        if (Value == value) return;
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }

    void OnMonthSelectChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)) return;
        _viewMonth = ClampView(new DateTime(_viewMonth.Year, month, 1));
        _focusDay = null;
    }

    void OnYearSelectChanged(ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return;
        // Clamp before constructing the DateTime below — YearRange can offer (or a caller-supplied
        // Min/Max year can be) outside DateTime's [1, 9999] range, and the constructor throws
        // (circuit-killing on Blazor Server) rather than something ClampView could catch after the
        // fact.
        year = Math.Clamp(year, 1, 9999);
        _viewMonth = ClampView(new DateTime(year, _viewMonth.Month, 1));
        _focusDay = null;
    }

    // ----- Time/DateTime mode: time row + OK ---------------------------------
    // Shared by both modes' three hour/minute/second selects -- the only behavioral difference
    // between them is which date part survives normalization, and NormalizeForMode already owns
    // that rule (Mode.Time always re-anchors to today; Mode.DateTime keeps whatever it's given), so
    // this only has to assemble one candidate DateTime and hand it to SetValueAsync. Unlike a day/
    // month click, a select change does not close the panel -- OK is the close signal here.

    // PickerBase's ApplyTimePartAsync hook, for immediate commit: composes a new value from the
    // current date part (Value's date, or DateTime.Today when unset -- Mode.Time discards this anyway)
    // and the current time-of-day (Value's, or midnight) with one HH/mm/ss part replaced (see
    // PickerMath.ComposeTimePart, shared with DateRangePicker's own session override), then commits --
    // unless the composed value is one this Mode won't accept, in which case this no-ops (the select's
    // own displayed value reverts to Value's on the next render, same revert semantics a Min/Max
    // rejection gets elsewhere). ShowSeconds false zeroes the second in the compose (not just in
    // NormalizeForMode) so the DisabledTime guard below never rejects an hour/minute change over a
    // stale second that no select can even change. The three per-select handlers (and Use12Hours'
    // period shift) that call this live on PickerBase -- they were duplicated verbatim.
    //
    // The guard is the FULL IsDisabledForCommit, not just its time half: Mode.DateTime's composed
    // DATE is not necessarily one anything validated -- ComposeTimePart falls back to DateTime.Today
    // when Value is null, so with (say) a Min a month out, an hour change was the one route that
    // could commit today. (Mode.Time's own arm of the dispatcher IS just the time half -- a
    // time-of-day has no date-range concept there -- so nothing changes for it.)
    internal override Task ApplyTimePartAsync(int? hour, int? minute, int? second)
    {
        // A select change supersedes any half-typed input text, same as a day/month click.
        _edit = null;
        var composed = PickerMath.ComposeTimePart(TimeRowValue, ShowSeconds, hour, minute, second);
        return IsDisabledForCommit(composed) ? Task.CompletedTask : SetValueAsync(composed);
    }

    // The OK button is Time/DateTime mode's close signal -- both modes commit incrementally (time
    // selects, and in DateTime a day click too) without closing, so nothing needs committing here.
    async Task OnPickerOkAsync()
    {
        _pendingInputFocus = true; // the OK button is about to unmount
        await CloseAsync();
    }

    // ----- PickerBase hooks (JS-interop + overlay lifecycle) ------------------
    // The JsModule holders, the OnAfterRenderAsync template, and DisposeAsync all
    // live on PickerBase -- these three hooks are this control's only customization of that shared
    // template (one input to wire, one grid to init, one input to reclaim focus onto).

    // initPicker's second input slot is null-safe — this picker has only the one.
    protected override ValueTask WireInputsAsync(IJSObjectReference module) =>
        module.InvokeVoidAsync("initPicker", _wrapperRef, _inputRef, null);

    protected override IEnumerable<ElementReference> GridRefs => new[] { _gridRef };

    protected override ElementReference FocusReclaimTarget => _inputRef;

    // The same input either way -- this picker has only one, so "where the user was" and "where a
    // caller from outside should land" can't diverge the way they do for the range picker.
    protected override ElementReference PrimaryInputRef => _inputRef;
}
