using System.Text.RegularExpressions;
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
/// flip/clamp, form-submit suppression, focus-out close, arrow-key page-scroll suppression)
/// degrades gracefully: without JS the dropdown opens below the field at the CSS default placement,
/// everything remains clickable, and arrow-key grid navigation still updates the roving-tabindex
/// state (just without the DOM focus follow or the native page-scroll suppression).
/// </remarks>
public partial class DatePicker : IAsyncDisposable
{
    [Inject] IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

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
    /// change via <c>ApplyTimePartAsync</c>, or a typed-text commit in either mode). A disabled hour/
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
    /// (see <see cref="TimeFormatString"/>) and normalization (<see cref="NormalizeForMode"/>) zeroes
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
    /// two options are <see cref="PickerCulture"/>'s <c>AMDesignator</c>/<c>PMDesignator</c>, instead
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
    /// this quarter, this year, or this week) and closes the panel. Defaults to FALSE — a DELIBERATE
    /// divergence from AntD's default-true <c>showToday</c>: this control existed before the
    /// footer did, so defaulting it on would grow every existing consumer's panel a new row it never
    /// asked for. Has no effect in <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
    /// — see <see cref="ShowNow"/> for their equivalent. The button renders DISABLED, not hidden,
    /// when the normalized today is rejected by <see cref="Min"/>/<see cref="Max"/>/
    /// <see cref="DisabledDate"/> — the same convention every other disabled cell in this control
    /// follows.</summary>
    [Parameter] public bool ShowToday { get; set; }
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

    // Validation-state ARIA passthrough onto the actual <input>, for form wrappers (EditDatePicker).
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
    /// rest are splatted verbatim.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ----- State ------------------------------------------------------------

    ElementReference _wrapperRef;
    ElementReference _panelRef;
    ElementReference _inputRef;
    ElementReference _gridRef;
    IJSObjectReference? _module;
    IJSObjectReference? _pickerModule;
    bool _open;
    bool _positioned;
    // Set first thing in DisposeAsync so an import that completes after disposal disposes its
    // module instead of stranding it on a dead instance (see GetModuleAsync).
    bool _disposed;
    bool _inputWired;
    // The open-order z-index placePanel assigned this wrapper (null while closed). C# owns it so a
    // Blazor re-render of the bound wrapper style re-asserts the value JS wrote to the DOM.
    int? _openZIndex;
    // First-of-month shown in the panel.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text (null = show the formatted bound value).
    string? _edit;
    // The day the grid's roving tabindex currently targets (null = not yet keyboard-navigated;
    // EffectiveFocusDay computes the AntD-style default in that case). Arrow-key navigation sets
    // this, and it survives a month flip (unlike DOM focus, which the re-rendered grid loses) so
    // subsequent arrow presses keep stepping from the right day.
    DateTime? _focusDay;
    // Set by grid keyboard navigation and consumed by the next OnAfterRenderAsync to move real DOM
    // focus via JS. An ElementReference can't be captured here: a month-crossing move re-renders the
    // grid with brand-new button instances, so the previously focused element is gone by the time
    // OnAfterRenderAsync runs — this hands the *date* across the render instead, and wss-picker.js's
    // focusDay looks up the new button by its data-date attribute.
    DateTime? _pendingFocusDate;
    // Set true right before a CloseAsync() call that was triggered by a panel-originated action
    // (day click/Enter commit/Escape) — anything that means focus was on some now-unmounting element
    // inside the wrapper. Consumed by the very next OnAfterRenderAsync's closing branch to move
    // focus back to the text input, so it doesn't fall through to <body>. Left false (the default)
    // for an outside/backdrop close, which must NOT steal focus from wherever the user clicked.
    bool _pendingInputFocus;
    // The input opens the panel on focus (OnInputFocus), so the programmatic focus-reclaim above
    // would immediately bounce the panel back open. Set around the FocusAsync call and consumed by
    // OnInputFocus to swallow exactly that one reopen; cleared unconditionally after the call as a
    // backstop so a swallowed/never-fired focus event can't eat a later genuine focus-open.
    bool _suppressOpenOnFocus;

    // ----- Inline icons (AntD glyphs, no icon-font dependency; matches DateRangePicker) ----

    static readonly MarkupString CalendarIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M880 184H712v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H384v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H144c-17.7 0-32 14.3-32 32v664c0 17.7 14.3 32 32 32h736c17.7 0 32-14.3 32-32V216c0-17.7-14.3-32-32-32zm-40 656H184V460h656v380zM184 392V256h128v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h256v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h128v136H184z\"/></svg>");

    static readonly MarkupString CloseCross = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M563.8 512l262.5-312.9c4.4-5.2.7-13.1-6.1-13.1h-79.8c-4.7 0-9.2 2.1-12.3 5.7L511.6 449.8 295.1 191.7c-3-3.6-7.5-5.7-12.3-5.7H203c-6.8 0-10.5 7.9-6.1 13.1L459.4 512 196.9 824.9A7.95 7.95 0 00203 838h79.8c4.7 0 9.2-2.1 12.3-5.7l216.5-258.1 216.5 258.1c3 3.6 7.5 5.7 12.3 5.7h79.8c6.8 0 10.5-7.9 6.1-13.1L563.8 512z\"/></svg>");

    static readonly MarkupString DownIcon = new(
        "<svg class=\"wss-picker-select-arrow\" viewBox=\"64 64 896 896\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M884 256h-75c-5.1 0-9.9 2.5-12.9 6.6L512 654.2 227.9 262.6c-3-4.1-7.8-6.6-12.9-6.6h-75c-6.5 0-10.3 7.4-6.5 12.7l352.6 486.1c12.8 17.6 39 17.6 51.7 0l352.6-486.1c3.9-5.3.1-12.7-6.4-12.7z\"/></svg>");

    // Prev/next chevrons — the same AntD glyphs Pagination.razor uses for its prev/next buttons.
    static readonly MarkupString PrevIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M724 218.3V141c0-6.7-7.7-10.4-12.9-6.3L260.3 486.8a31.86 31.86 0 000 50.3l450.8 352.1c5.3 4.1 12.9.4 12.9-6.3v-77.3c0-4.9-2.3-9.6-6.1-12.6l-360-281 360-281.1c3.8-3 6.1-7.7 6.1-12.6z\"/></svg>");

    static readonly MarkupString NextIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M765.7 486.8L314.9 134.7A7.97 7.97 0 00302 141v77.3c0 4.9 2.3 9.6 6.1 12.6l360 281.1-360 281.1c-3.8 3-6.1 7.7-6.1 12.6V883c0 6.7 7.7 10.4 12.9 6.3l450.8-352.1a31.96 31.96 0 000-50.4z\"/></svg>");

    // ----- Display helpers (used by the .razor markup) ------------------------

    string WrapperClass
    {
        get
        {
            var classes = "wss-picker wss-picker-single";
            if (_open) classes += " wss-picker-open";
            if (Disabled) classes += " wss-picker-disabled";
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
            return _openZIndex is null ? width : $"{width}z-index:{_openZIndex};";
        }
    }

    string Display => _edit ?? FormatDate(Value);

    bool ShowClear => AllowClear && !Disabled && Value is not null;

    // Format/Placeholder resolution: an explicit value always wins; null falls through to Mode's
    // default. All internal display/parse code routes through these (never the raw parameters), so
    // a mode switch changes behavior without a consumer having to also clear a stale Format/Placeholder.
    // Quarter's and Week's null-Format cases are a bland "yyyy" here -- FormatDate never actually
    // calls ToString(EffectiveFormat) for either (see FormatDate's special cases below); this value
    // only matters as TryParseDate's exact-format fallback attempt, tried after their own regex.
    string EffectiveFormat => Format ?? Mode switch
    {
        DatePickerMode.Date => "MM/dd/yyyy",
        DatePickerMode.Month => "MM/yyyy",
        DatePickerMode.DateTime => $"MM/dd/yyyy {TimeFormatString}",
        DatePickerMode.Time => TimeFormatString,
        DatePickerMode.Year => "yyyy",
        DatePickerMode.Quarter => "yyyy",
        DatePickerMode.Week => "yyyy",
        _ => "MM/dd/yyyy",
    };

    // The Time/DateTime portion of EffectiveFormat -- broken out because DateTime mode's format is
    // just "MM/dd/yyyy " plus this exact string. ShowSeconds drops ":ss"; Use12Hours switches the
    // 24-hour "HH" to the unpadded 12-hour "h" plus a trailing "tt" designator (matching
    // HourOptionText's own unpadded 12-hour option text below).
    string TimeFormatString => Use12Hours
        ? (ShowSeconds ? "h:mm:ss tt" : "h:mm tt")
        : (ShowSeconds ? "HH:mm:ss" : "HH:mm");

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
        if (day == DateTime.Today) cls += " wss-picker-day-today";
        // Week mode suppresses the single-day selected look -- the row is the selection unit there
        // (see IsDaySelected/wss-picker-week-row-selected), and every day in the row still carries
        // aria-pressed="true" via IsDaySelected below.
        if (Mode != DatePickerMode.Week && day == Value?.Date) cls += " wss-picker-day-selected";
        return cls;
    }

    // Whether `day`'s button should render aria-pressed="true": in every mode but Week, only the
    // exact selected day; in Week mode, every day sharing Value's week (the row is the selection
    // unit -- see DayClass's suppression of the single-day background above).
    bool IsDaySelected(DateTime day) =>
        Mode == DatePickerMode.Week
            ? Value is { } v && WeekStart(v.Date) == WeekStart(day)
            : day == Value?.Date;

    // DisabledDate is folded into every granularity's Is*Disabled helper below (not called
    // separately anywhere else) so every consumer of them -- the cell disabled attributes, the
    // DefaultFocus*/FirstEnabled* skip logic, and IsDisabledForCommit's typed-text guard -- picks it
    // up automatically and can never disagree about what counts as disabled.
    bool IsDayDisabled(DateTime day) =>
        (Min is { } min && day < min.Date) || (Max is { } max && day > max.Date) ||
        (DisabledDate?.Invoke(day) ?? false);

    // Week-mode equivalent of IsDayDisabled/IsMonthDisabled, at week granularity: a whole week is
    // disabled once its 7-day span falls entirely outside [Min, Max] (or DisabledDate itself rejects
    // the week start) -- the individual day buttons stay enabled per IsDayDisabled above (a
    // partially-in-range week is still clickable; only the commit itself is guarded here, same split
    // DateTime mode uses between day-cell and Min/Max-day checks). `weekStart` is already
    // WeekStart-shaped -- this is the one place DisabledDate sees a week start rather than a day.
    bool IsWeekDisabledForCommit(DateTime weekStart) =>
        (Max is { } max && weekStart > max.Date) || (Min is { } min && weekStart.AddDays(6) < min.Date) ||
        (DisabledDate?.Invoke(weekStart) ?? false);

    // Month-mode equivalent of IsDayDisabled: a whole month is disabled once it falls entirely
    // outside [Min, Max] at month granularity — same granularity PrevMonthDisabled/NextMonthDisabled
    // use for the day-grid's own header nav, so the two panels never disagree about where Min/Max
    // stop navigation.
    bool IsMonthDisabled(DateTime month) =>
        (Min is { } min && month < FirstOfMonth(min)) || (Max is { } max && month > FirstOfMonth(max)) ||
        (DisabledDate?.Invoke(month) ?? false);

    // Year-mode equivalent of IsMonthDisabled, one granularity up: a whole year is disabled once it
    // falls entirely outside [Min, Max] at year granularity. `year` is already FirstOfYear-shaped.
    bool IsYearDisabled(DateTime year) =>
        (Min is { } min && year < FirstOfYear(min)) || (Max is { } max && year > FirstOfYear(max)) ||
        (DisabledDate?.Invoke(year) ?? false);

    // Quarter-mode equivalent of IsMonthDisabled, at quarter granularity: a whole quarter is
    // disabled once it falls entirely outside [Min, Max]'s own quarter. `quarterStart` is already
    // QuarterStart-shaped.
    bool IsQuarterDisabled(DateTime quarterStart) =>
        (Min is { } min && quarterStart < QuarterStart(min)) || (Max is { } max && quarterStart > QuarterStart(max)) ||
        (DisabledDate?.Invoke(quarterStart) ?? false);

    // Whether `value`'s time-of-day hits a DisabledTime-disabled hour/minute/second, evaluated
    // against `value`'s own date part -- the same argument contract TimeRowFragment uses at render
    // time, but against the value actually being committed rather than the (possibly stale) bound
    // Value. Invokes DisabledTime exactly once, shared by ApplyTimePartAsync's select-change guard
    // and IsDisabledForCommit's typed-text guard below so the two can never disagree. Null callback /
    // null return / null lists = nothing disabled.
    bool IsTimeDisabledForCommit(DateTime value)
    {
        var parts = DisabledTime?.Invoke(value.Date);
        return parts is not null &&
            (IsTimePartDisabled(parts.Hours, value.Hour) ||
             IsTimePartDisabled(parts.Minutes, value.Minute) ||
             IsTimePartDisabled(parts.Seconds, value.Second));
    }

    // Whether `value` is one of `disabled`'s listed values -- null (nothing disabled in that unit)
    // always answers false. Shared by IsTimeDisabledForCommit above and TimeRowFragment's per-option
    // render check in DatePicker.razor so the disabled attribute and the commit guard can never
    // disagree about the same hour/minute/second.
    static bool IsTimePartDisabled(IReadOnlyCollection<int>? disabled, int value) =>
        disabled?.Contains(value) ?? false;

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

    // The picker is a Gregorian-calendar control — see GregorianCultureHelper for the contract.
    // Every picker-internal format and the typed-input parse route through this culture.
    CultureInfo PickerCulture => GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);

    string MonthName(int month) =>
        PickerCulture.DateTimeFormat.AbbreviatedMonthNames[month - 1];

    // Time-row display (Mode.Time/Mode.DateTime): the bound value's time-of-day, or midnight when
    // unset. There is no separate "in-progress" time state -- a select change commits immediately
    // (see ApplyTimePartAsync below), so the next render always has the answer in Value itself.
    int DisplayHour => Value?.Hour ?? 0;
    int DisplayMinute => Value?.Minute ?? 0;
    int DisplaySecond => Value?.Second ?? 0;

    // Whether the displayed hour falls in the PM half of the day — the default (Value null)
    // DisplayHour of 0 is AM, matching AntD's/the doc comment's "12 AM / 00:00" default. Drives
    // Use12Hours' period select and hour-option filtering (HourOptions below).
    bool DisplayIsPM => DisplayHour >= 12;

    // HourStep/MinuteStep/SecondStep clamped to >= 1 at the point of use (never thrown) -- the raw
    // parameters stay whatever a consumer set (even 0 or negative) so nothing but option-list
    // construction ever second-guesses them.
    int EffectiveHourStep => Math.Max(1, HourStep);
    int EffectiveMinuteStep => Math.Max(1, MinuteStep);
    int EffectiveSecondStep => Math.Max(1, SecondStep);

    // The years offered by the year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — always including the displayed year itself so the select never shows a
    // value that isn't in its option list.
    (int From, int To) YearRange(int displayedYear)
    {
        var from = Min?.Year ?? displayedYear - 10;
        var to = Max?.Year ?? displayedYear + 10;
        if (displayedYear < from) from = displayedYear;
        if (displayedYear > to) to = displayedYear;
        // DateTime's year range is [1, 9999] — an unclamped ±10 offset (or a Min/Max year near
        // either edge) can offer a year outside it, and constructing `new DateTime(year, ...)` for
        // one throws (circuit-killing on Blazor Server). See OnYearSelectChanged for the matching
        // clamp on the value actually selected.
        return (Math.Clamp(from, 1, 9999), Math.Clamp(to, 1, 9999));
    }

    DayOfWeek EffectiveFirstDayOfWeek =>
        FirstDayOfWeek ?? PickerCulture.DateTimeFormat.FirstDayOfWeek;

    // The weekday header row, ordered to match GridDays' first-day-of-week so the header and grid
    // can never disagree — both derive from WeekStart/EffectiveFirstDayOfWeek. AntD shows the CLDR
    // "short" two-letter form ("Su"), which .NET doesn't expose (ShortestDayNames is the one-letter
    // "narrow" form, ambiguous for Tue/Thu and Sat/Sun), so truncate AbbreviatedDayNames instead —
    // already <= 2 chars in single-glyph cultures (ja, zh). Decorative only: aria-hidden, day
    // buttons carry full "D"-format labels.
    IEnumerable<string> WeekdayHeaders
    {
        get
        {
            var names = PickerCulture.DateTimeFormat.AbbreviatedDayNames;
            for (var i = 0; i < 7; i++)
            {
                var name = names[((int)EffectiveFirstDayOfWeek + i) % 7];
                yield return name.Length <= 2 ? name : name[..2];
            }
        }
    }

    // The first day of the calendar week containing `day`, per EffectiveFirstDayOfWeek. Shared by
    // GridDays (the 42-cell layout) and Home/End keyboard navigation so they can never disagree.
    DateTime WeekStart(DateTime day) => WeekStartCore(day, EffectiveFirstDayOfWeek);

    // The instance-independent core of WeekStart, taking the resolved first-day-of-week directly --
    // shared with FormatWeekDisplay below (EditDatePicker's read-only view calls that one without an
    // instance to resolve EffectiveFirstDayOfWeek from).
    static DateTime WeekStartCore(DateTime day, DayOfWeek firstDayOfWeek)
    {
        var lead = ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return day.AddDays(-lead);
    }

    // A fixed 6-row (42-cell) grid — covers every month/first-day combination, so the panel height
    // never jumps while navigating. Leading/trailing cells are the adjacent months' days.
    IEnumerable<DateTime> GridDays(DateTime month)
    {
        var start = WeekStart(month);
        for (var i = 0; i < 42; i++)
        {
            yield return start.AddDays(i);
        }
    }

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
    int WeekNumberOf(DateTime weekStart) => WeekNumberOfCore(weekStart, PickerCulture, EffectiveFirstDayOfWeek);

    // The instance-independent core of WeekNumberOf -- shared with FormatWeekDisplay below.
    static int WeekNumberOfCore(DateTime weekStart, CultureInfo culture, DayOfWeek firstDayOfWeek) =>
        culture.Calendar.GetWeekOfYear(weekStart, culture.DateTimeFormat.CalendarWeekRule, firstDayOfWeek);

    // Is `weekStart` the row containing Value's week? Only meaningful in Mode.Week -- ShowWeekNumbers
    // in Date/DateTime mode renders the same rows layout with NO selection-styling change (day clicks
    // still commit days, so there's no "selected week" to band there).
    bool IsSelectedWeekRow(DateTime weekStart) =>
        Mode == DatePickerMode.Week && Value is { } v && WeekStart(v.Date) == weekStart;

    string WeekRowClass(DateTime weekStart) =>
        IsSelectedWeekRow(weekStart) ? "wss-picker-week-row wss-picker-week-row-selected" : "wss-picker-week-row";

    // Matches a typed quarter shorthand: "2026-Q3", "2026Q3", "2026 q3" -- 1-4 digit year, optional
    // dash/whitespace, case-insensitive Q, quarter digit 1-4. Compiled because TryParseDate tries it
    // on every keystroke's eventual Enter-commit in Quarter mode.
    static readonly Regex _quarterPattern = new(@"^\s*(\d{1,4})\s*-?\s*[Qq]\s*([1-4])\s*$", RegexOptions.Compiled);

    // Matches a typed week shorthand: "2026-W08", "2026W8", "2026 w08" -- 1-4 digit year, optional
    // dash/whitespace, case-insensitive W, 1-2 digit week number. Compiled for the same reason as
    // _quarterPattern above.
    static readonly Regex _weekPattern = new(@"^\s*(\d{1,4})\s*-?\s*[Ww]\s*(\d{1,2})\s*$", RegexOptions.Compiled);

    // Quarter mode's null-Format display: no .NET format token renders a quarter number, so this
    // bypasses ToString(EffectiveFormat) entirely for that one case. Format explicitly set still
    // routes through the normal ToString(EffectiveFormat) path below (used verbatim, per Format's
    // doc comment).
    string FormatDate(DateTime? value)
    {
        if (value is not { } v) return string.Empty;
        if (Mode == DatePickerMode.Quarter && Format is null)
        {
            return FormatQuarterDisplay(v, PickerCulture);
        }
        // Week mode's null-Format display: same rationale as Quarter's above -- no .NET token for a
        // week number.
        if (Mode == DatePickerMode.Week && Format is null)
        {
            return FormatWeekDisplay(v, PickerCulture, EffectiveFirstDayOfWeek);
        }
        return v.ToString(EffectiveFormat, PickerCulture);
    }

    // The Quarter/Week null-Format display strings, promoted to internal statics so EditDatePicker's
    // read-only view (a different type entirely, with no DatePicker instance to call FormatDate on)
    // can produce the identical "yyyy-Qn"/"yyyy-Www" text instead of duplicating this regex-adjacent
    // formatting logic. Kept as the single source of truth: FormatDate above (and TryParseDate's own
    // Week-mode search loop, via the instance WeekNumberOf/WeekStart wrappers) are the only other
    // callers, so a future format tweak here can't drift between DatePicker and EditDatePicker.

    /// <summary>Quarter mode's null-<see cref="Format"/> display for <paramref name="value"/> —
    /// <c>"yyyy-Qn"</c> (e.g. "2026-Q3") in <paramref name="culture"/>'s digits. Internal: shared
    /// with <see cref="EditDatePicker{T}"/>'s read-only view, not part of this control's public
    /// surface.</summary>
    internal static string FormatQuarterDisplay(DateTime value, CultureInfo culture) =>
        $"{value.Year.ToString(culture)}-Q{QuarterOf(value).ToString(culture)}";

    /// <summary>Week mode's null-<see cref="Format"/> display for <paramref name="value"/> —
    /// <c>"yyyy-Www"</c> (e.g. "2026-W08") in <paramref name="culture"/>'s digits, where the year is
    /// the WEEK START's calendar year (deterministic at year-boundary weeks, unlike
    /// <paramref name="value"/>'s own year, which can disagree with the week it falls in). Internal:
    /// shared with <see cref="EditDatePicker{T}"/>'s read-only view, not part of this control's
    /// public surface.</summary>
    internal static string FormatWeekDisplay(DateTime value, CultureInfo culture, DayOfWeek firstDayOfWeek)
    {
        var weekStart = WeekStartCore(value, firstDayOfWeek);
        return $"{weekStart.Year.ToString(culture)}-W{WeekNumberOfCore(weekStart, culture, firstDayOfWeek).ToString("00", culture)}";
    }

    // Exact effective format first, then the current culture's general parse -- then normalizes the
    // parsed result to Mode's own granularity (mirrors SetValueAsync's normalization so a typed
    // commit and a click/select commit always land on the same shape of value). Quarter mode (with
    // Format left null, mirroring FormatDate's special case above) tries the "yyyy-Qn" shorthand
    // first -- a plain typed date still falls through to the general parse below and normalizes to
    // its own quarter, same as every other mode's typed-text path. Week mode's "yyyy-Www" shorthand
    // mirrors that, resolved as the exact inverse of FormatDate's display: walk the week starts
    // whose calendar year is the typed year and return the one GetWeekOfYear numbers N. Plain
    // arithmetic from WeekStart(Jan 1) can't do this -- under CalendarWeekRule.FirstDay a year that
    // doesn't begin on FirstDayOfWeek numbers its partial first week 1, so every later week start is
    // one ahead of the (N-1)*7 offset and a displayed week wouldn't round-trip. A week number the
    // display never produces for that year (e.g. W01 when Jan 1's week started in December) finds no
    // match and falls through to the general parse below, same as any other malformed text.
    bool TryParseDate(string text, out DateTime value)
    {
        if (Mode == DatePickerMode.Quarter && Format is null)
        {
            var match = _quarterPattern.Match(text);
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
                year is >= 1 and <= 9999)
            {
                var quarter = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                value = QuarterStart(year, quarter);
                return true;
            }
        }
        if (Mode == DatePickerMode.Week && Format is null)
        {
            var match = _weekPattern.Match(text);
            if (match.Success &&
                int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
                year is >= 1 and <= 9999)
            {
                var week = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                try
                {
                    // First week start whose calendar year is `year` (WeekStart(Jan 1) itself may
                    // belong to the prior December), then at most 53 boundary steps.
                    var s = WeekStart(new DateTime(year, 1, 1));
                    if (s.Year < year) s = s.AddDays(7);
                    for (; s.Year == year; s = s.AddDays(7))
                    {
                        if (WeekNumberOf(s) == week)
                        {
                            value = s;
                            return true;
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    // WeekStart/AddDays overflowed at the DateTime range edge (year 1 / 9999) --
                    // fall through to the general parse below, same as any other malformed text.
                }
            }
        }
        if (DateTime.TryParseExact(text, EffectiveFormat, PickerCulture, DateTimeStyles.None, out value) ||
            DateTime.TryParse(text, PickerCulture, DateTimeStyles.None, out value))
        {
            value = NormalizeForMode(value);
            return true;
        }
        return false;
    }

    // Central per-mode normalization, shared by TryParseDate and SetValueAsync so every commit path
    // (click, typed text, select change) agrees on the same shape of value.
    DateTime NormalizeForMode(DateTime value) => Mode switch
    {
        DatePickerMode.Date => value.Date,
        DatePickerMode.Month => FirstOfMonth(value),
        // ShowSeconds false zeroes the second here too (not just in ApplyTimePartAsync's own compose
        // step) so a typed-text commit -- which never goes through ApplyTimePartAsync -- can't leave
        // a stale nonzero second in place.
        DatePickerMode.DateTime => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, ShowSeconds ? value.Second : 0),
        // Anchored to today at commit time -- mirrors what EditDate produces for a DateTime bound to
        // a Time input, where BindConverter/DateTime.TryParse("HH:mm:ss") yields today's date.
        DatePickerMode.Time => DateTime.Today + new TimeSpan(value.Hour, value.Minute, ShowSeconds ? value.Second : 0),
        DatePickerMode.Year => new DateTime(value.Year, 1, 1),
        DatePickerMode.Quarter => QuarterStart(value),
        DatePickerMode.Week => WeekStart(value),
        _ => value.Date,
    };

    static DateTime FirstOfMonth(DateTime value) => new(value.Year, value.Month, 1);

    static DateTime FirstOfYear(DateTime value) => new(value.Year, 1, 1);

    // The quarter (1-4) `value`'s month falls in.
    static int QuarterOf(DateTime value) => (value.Month - 1) / 3 + 1;

    // The 1st of `quarter`'s (1-4) first month in `year`.
    static DateTime QuarterStart(int year, int quarter) => new(year, (quarter - 1) * 3 + 1, 1);

    // The 1st of the quarter containing `value`.
    static DateTime QuarterStart(DateTime value) => QuarterStart(value.Year, QuarterOf(value));

    // The displayed month, clamped so the 42-cell grid can never overflow DateTime's range.
    static DateTime ClampView(DateTime firstOfMonth)
    {
        var index = firstOfMonth.Year * 12 + (firstOfMonth.Month - 1);
        index = Math.Clamp(index, 1 * 12 + 1, 9998 * 12 + 10); // 0001-02 .. 9998-11
        return new DateTime(index / 12, index % 12 + 1, 1);
    }

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
        // so today falls before it). Falling through to the 1st of the month like before would park
        // the roving tabindex on a disabled button and make the whole grid keyboard-unreachable (Tab
        // skips straight past a tabindex="0" that's also disabled). Land on the first enabled
        // in-month day instead; if the whole visible month is disabled there's nothing actionable in
        // it either way, so any deterministic in-month day (the 1st) is fine.
        return FirstEnabledDay(_viewMonth) ?? _viewMonth;
    }

    // The first enabled, in-month day in `month`'s grid, or null if every in-month day is disabled.
    DateTime? FirstEnabledDay(DateTime month)
    {
        foreach (var day in GridDays(month))
        {
            if (day.Month == month.Month && day.Year == month.Year && !IsDayDisabled(day)) return day;
        }
        return null;
    }

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
    // navigation key. AddDays/AddMonths throws at the DateTime.MinValue/MaxValue edge — the caller
    // treats that as the key being a no-op there rather than letting the exception escape.
    DateTime? NextFocusDay(DateTime current, string key)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddDays(-1),
                "ArrowRight" => current.AddDays(1),
                "ArrowUp" => current.AddDays(-7),
                "ArrowDown" => current.AddDays(7),
                "Home" => WeekStart(current),
                "End" => WeekStart(current).AddDays(6),
                "PageUp" => current.AddMonths(-1),
                "PageDown" => current.AddMonths(1),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // Grid keydown: moves the roving-tabindex day, retargeting the displayed month when navigation
    // crosses out of it (clamped exactly like the month/year selects). A day that lands disabled
    // (Min/Max) still becomes the focus target — only clicking commits, so parking keyboard focus on
    // a disabled day is harmless and lets Left/Right keep stepping day-by-day through it. The actual
    // DOM focus move (needed whenever the grid re-renders with new button instances, i.e. any month
    // change) happens in OnAfterRenderAsync via _pendingFocusDate. wss-picker.js suppresses the
    // browser's native scroll for these keys when JS is available; without it this state still
    // updates, just without the DOM focus follow or the scroll suppression.
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
    DateTime? FirstEnabledMonth(int year)
    {
        for (var m = 1; m <= 12; m++)
        {
            var month = new DateTime(year, m, 1);
            if (!IsMonthDisabled(month)) return month;
        }
        return null;
    }

    DateTime EffectiveFocusMonth => _focusDay is { } f && IsVisibleMonth(f) ? f : DefaultFocusMonth();

    bool IsMonthFocusStop(DateTime month) => month == EffectiveFocusMonth;

    // Maps a keydown's Key to the month it should move focus to, or null when the key isn't a
    // navigation key. The 3-column grid makes Up/Down a +/-3 (one row) step; Home/End jump to the
    // first/last month of the focused row. AddMonths/AddYears throws at the DateTime.MinValue/
    // MaxValue edge — the caller treats that as the key being a no-op there.
    DateTime? NextFocusMonth(DateTime current, string key)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddMonths(-1),
                "ArrowRight" => current.AddMonths(1),
                "ArrowUp" => current.AddMonths(-3),
                "ArrowDown" => current.AddMonths(3),
                "Home" => MonthRowStart(current),
                "End" => MonthRowStart(current).AddMonths(2),
                "PageUp" => current.AddYears(-1),
                "PageDown" => current.AddYears(1),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // The 1st of the first month in the 3-month row containing `month` (rows are Jan-Mar, Apr-Jun,
    // Jul-Sep, Oct-Dec) — shared by Home/End so they can never disagree about row bounds.
    static DateTime MonthRowStart(DateTime month) => new(month.Year, (month.Month - 1) / 3 * 3 + 1, 1);

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

    // Clamps a decade-start candidate so the decade's own leading/trailing dimmed cells
    // (decadeStart-1, decadeStart+10) always land inside DateTime's representable [1, 9999] year
    // range -- the year-grid's equivalent of ClampView's one-month buffer for the day grid. The
    // reachable extremes are the 10-19 decade (dimmed leading cell 9) and the 9980-9989 decade
    // (dimmed trailing cell 9990); years 1-9 and 9991-9999 are unreachable via the GRID (though
    // still typeable -- TryParseDate has no such margin), the same trade-off ClampView makes for
    // the very first/last representable month.
    static int ClampDecadeStart(int year) => Math.Clamp(year, 11, 9989) / 10 * 10;

    // The decade the grid currently displays, floored to a multiple of 10.
    int DecadeStart => ClampDecadeStart(_viewMonth.Year);

    // "2020-2029" style, both years in PickerCulture digits.
    string DecadeLabel => $"{DecadeStart.ToString(PickerCulture)}-{(DecadeStart + 9).ToString(PickerCulture)}";

    // Is `year` one of the decade's own 10 years (as opposed to one of the 2 dimmed adjacent-decade
    // cells)?
    bool IsYearInDecade(int year) => year >= DecadeStart && year <= DecadeStart + 9;

    string YearButtonClass(int year) =>
        IsYearInDecade(year) ? "wss-picker-month-btn" : "wss-picker-month-btn wss-picker-month-btn-outside";

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
    DateTime? FirstEnabledYear()
    {
        for (var y = DecadeStart; y <= DecadeStart + 9; y++)
        {
            var year = new DateTime(y, 1, 1);
            if (!IsYearDisabled(year)) return year;
        }
        return null;
    }

    DateTime EffectiveFocusYear => _focusDay is { } f && IsYearInDecade(f.Year) ? f : DefaultFocusYear();

    bool IsYearFocusStop(int year) => new DateTime(year, 1, 1) == EffectiveFocusYear;

    // The 1st year of the 3-year row (within the *displayed* 12-cell decade grid, decadeStart-1
    // through decadeStart+10) containing `year` -- shared by Home/End so they can never disagree
    // about row bounds. Unlike MonthRowStart, this depends on the currently displayed decade
    // (DecadeStart) rather than `year`'s own natural decade: the grid's two dimmed adjacent-decade
    // cells belong to neighboring decades, so grouping purely by each year's own decade would split
    // a row unevenly right at the boundary.
    int YearRowStart(int year)
    {
        var offset = year - (DecadeStart - 1);
        return DecadeStart - 1 + offset / 3 * 3;
    }

    // Maps a keydown's Key to the year it should move focus to, or null when the key isn't a
    // navigation key. Plain int arithmetic (unlike NextFocusDay/NextFocusMonth's DateTime.AddX,
    // this can't throw) -- clamped to DateTime's representable year range instead so a move at the
    // very edge is a no-op there.
    DateTime? NextFocusYear(DateTime current, string key)
    {
        var year = current.Year;
        int? next = key switch
        {
            "ArrowLeft" => year - 1,
            "ArrowRight" => year + 1,
            "ArrowUp" => year - 3,
            "ArrowDown" => year + 3,
            "Home" => YearRowStart(year),
            "End" => YearRowStart(year) + 2,
            "PageUp" => year - 10,
            "PageDown" => year + 10,
            _ => (int?)null,
        };
        return next is { } y && y is >= 1 and <= 9999 ? new DateTime(y, 1, 1) : null;
    }

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
        // A grid pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(new DateTime(year, 1, 1));
        _pendingInputFocus = true; // the clicked year button is about to unmount
        await CloseAsync();
    }

    // ----- Quarter mode: grid + keyboard navigation --------------------------
    // The header is Month mode's verbatim (YearHeaderFragment in DatePicker.razor) -- only the grid
    // differs: a single row of 4 quarter buttons instead of a 3x4 month grid. Shares
    // _focusDay/_pendingFocusDate the same way Month mode does; only one grid ever renders for a
    // given Mode.

    bool IsSelectedQuarter(int year, int quarter) => Value is { } v && v.Year == year && QuarterOf(v) == quarter;

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
    DateTime? FirstEnabledQuarter(int year)
    {
        for (var q = 1; q <= 4; q++)
        {
            var quarterStart = QuarterStart(year, q);
            if (!IsQuarterDisabled(quarterStart)) return quarterStart;
        }
        return null;
    }

    DateTime EffectiveFocusQuarter => _focusDay is { } f && IsVisibleQuarterYear(f.Year) ? f : DefaultFocusQuarter();

    bool IsQuarterFocusStop(int year, int quarter) => QuarterStart(year, quarter) == EffectiveFocusQuarter;

    // Maps a keydown's Key to the quarter it should move focus to, or null when the key isn't a
    // navigation key (Up/Down included -- a no-op in this single-row grid). Left/Right step a
    // quarter (retargeting the view when they cross a year boundary, via the AddMonths(+/-3) below);
    // Home/End jump to the year's first/last quarter; PageUp/PageDown step a year, keeping the same
    // quarter. AddMonths/the DateTime constructor throw at the DateTime.MinValue/MaxValue edge --
    // the caller treats that as the key being a no-op there, same as NextFocusMonth.
    DateTime? NextFocusQuarter(DateTime current, string key)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddMonths(-3),
                "ArrowRight" => current.AddMonths(3),
                "Home" => QuarterStart(current.Year, 1),
                "End" => QuarterStart(current.Year, 4),
                "PageUp" => QuarterStart(current.Year - 1, QuarterOf(current)),
                "PageDown" => QuarterStart(current.Year + 1, QuarterOf(current)),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

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
        // A grid pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(QuarterStart(year, quarter));
        _pendingInputFocus = true; // the clicked quarter button is about to unmount
        await CloseAsync();
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
    async Task OnWrapperKeyDownAsync(KeyboardEventArgs e)
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
        // Week mode's day BUTTON stays at day granularity (IsDayDisabled(day), same as every other
        // mode's day cell -- see IsDayDisabled's doc comment), but the click's actual commit lands on
        // the week START, not the clicked day. With Min/Max alone those two checks can never disagree
        // (a week whose start/end falls outside [Min, Max] means every day in it does too, so a
        // disabled week never has an enabled day button to click), but DisabledDate is an arbitrary
        // predicate -- it can reject a week start while leaving every individual day in that week
        // enabled. Guard it here explicitly, mirroring the typed-text path's IsDisabledForCommit
        // check, so a click can't slip past DisabledDate the way SetValueAsync itself never checks.
        if (Mode == DatePickerMode.Week && IsWeekDisabledForCommit(WeekStart(day))) return;
        // A calendar pick supersedes any half-typed input text.
        _edit = null;
        // Mode.DateTime keeps whatever time-of-day is already committed (or midnight) instead of
        // zeroing it out -- the day calendar only ever supplies the date part there, the time row
        // below it owns the rest. Mode.Date is unaffected: adding TimeSpan.Zero is a no-op.
        var time = Mode == DatePickerMode.DateTime ? Value?.TimeOfDay ?? TimeSpan.Zero : TimeSpan.Zero;
        await SetValueAsync(day + time);
        // Mode.DateTime leaves the panel open -- the user may still want to adjust the time, and OK
        // is that mode's close signal. Mode.Date completes the pick immediately, as before.
        if (Mode == DatePickerMode.DateTime) return;
        _pendingInputFocus = true; // the clicked day button is about to unmount
        await CloseAsync();
    }

    async Task OnMonthClickAsync(DateTime month)
    {
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

    async Task ClearAsync()
    {
        if (Disabled) return;
        _edit = null;
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
        if (!TryParseDate(text, out var day) || IsDisabledForCommit(day)) return false;
        await SetValueAsync(day);
        _viewMonth = ClampView(FirstOfMonth(day));
        return true;
    }

    // Central commit: normalizes to Mode's shape and raises the callback only when it actually changed.
    async Task SetValueAsync(DateTime? value)
    {
        value = value is { } v ? NormalizeForMode(v) : null;
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

    // Composes a new value from the current date part (Value's date, or DateTime.Today when unset --
    // Mode.Time discards this anyway) and the current time-of-day (Value's, or midnight) with one
    // HH/mm/ss part replaced, then commits -- unless DisabledTime rejects the composed H/m/s, in
    // which case this no-ops (the select's own displayed value reverts to Value's on the next
    // render, same revert semantics a Min/Max rejection gets elsewhere). ShowSeconds false zeroes the
    // second here (not just in NormalizeForMode) so the DisabledTime guard below never rejects a
    // hour/minute change over a stale second that no select can even change.
    Task ApplyTimePartAsync(int? hour = null, int? minute = null, int? second = null)
    {
        // A select change supersedes any half-typed input text, same as a day/month click.
        _edit = null;
        var date = Value?.Date ?? DateTime.Today;
        var time = Value?.TimeOfDay ?? TimeSpan.Zero;
        var seconds = ShowSeconds ? second ?? time.Seconds : 0;
        var composed = date + new TimeSpan(hour ?? time.Hours, minute ?? time.Minutes, seconds);
        return IsTimeDisabledForCommit(composed) ? Task.CompletedTask : SetValueAsync(composed);
    }

    Task OnHourSelectChangedAsync(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hour)
            ? ApplyTimePartAsync(hour: hour)
            : Task.CompletedTask;

    Task OnMinuteSelectChangedAsync(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minute)
            ? ApplyTimePartAsync(minute: minute)
            : Task.CompletedTask;

    Task OnSecondSelectChangedAsync(ChangeEventArgs e) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var second)
            ? ApplyTimePartAsync(second: second)
            : Task.CompletedTask;

    // Use12Hours' period select: re-commits the CURRENT hour shifted into the other period, via the
    // same ApplyTimePartAsync every other time-row change routes through (so it gets the same
    // DisabledTime guard and _edit-clearing as a direct hour/minute/second change). "PM" is the only
    // value that flips the shift -- anything else (including a malformed event) is treated as AM, same
    // permissive fallback OnHourSelectChangedAsync's TryParse gives a bad hour string.
    Task OnPeriodSelectChangedAsync(ChangeEventArgs e)
    {
        var isPM = string.Equals(e.Value?.ToString(), "PM", StringComparison.Ordinal);
        return ApplyTimePartAsync(hour: DisplayHour % 12 + (isPM ? 12 : 0));
    }

    // The hour values the hour select offers, before DisabledTime hides/disables any of them: every
    // EffectiveHourStep-th 24-hour value (0, step, 2*step, ... <= 23) via SteppedOptions, further
    // filtered under Use12Hours to just the hours belonging to the CURRENTLY DISPLAYED AM/PM period.
    // SteppedOptions' own never-jump already guarantees DisplayHour survives that filter -- its period
    // (DisplayIsPM) is computed from DisplayHour itself, so it can never be filtered OUT.  The result
    // is ascending 24h order, which -- within one period -- is already exactly the "12, 1, 2, ... 11"
    // 12-hour reading order the Use12Hours doc comment promises (h%12 rises in step with h in both
    // halves of the day; see HourOptionText for the label itself).
    IEnumerable<int> HourOptions()
    {
        var options = SteppedOptions(23, EffectiveHourStep, DisplayHour);
        return Use12Hours ? options.Where(h => (h >= 12) == DisplayIsPM) : options;
    }

    IEnumerable<int> MinuteOptions() => SteppedOptions(59, EffectiveMinuteStep, DisplayMinute);

    IEnumerable<int> SecondOptions() => SteppedOptions(59, EffectiveSecondStep, DisplaySecond);

    // The option values a stepped time select offers before DisabledTime hides/disables any of them:
    // every `step`-th value from 0 to `max` inclusive, plus `current` itself if it isn't naturally on
    // that lattice -- the NEVER-JUMP RULE for HourStep/MinuteStep/SecondStep, composing with
    // DisabledTime's own (see HideDisabledTimeOptions) so a select can never silently show a value
    // that isn't the one actually bound. A SortedSet both dedupes (current may already be on the
    // lattice) and keeps the option list in its natural ascending reading order even though `current`
    // wasn't necessarily added in numeric order. `step` is trusted to already be >= 1 -- see
    // EffectiveHourStep/EffectiveMinuteStep/EffectiveSecondStep.
    static IEnumerable<int> SteppedOptions(int max, int step, int current)
    {
        var options = new SortedSet<int>();
        for (var v = 0; v <= max; v += step) options.Add(v);
        options.Add(current);
        return options;
    }

    // The hour select's option TEXT for `h` (always a 24h value): zero-padded 24h ("00".."23")
    // normally, or the plain (non-zero-padded) 12-hour reading ("12", "1".."11") under Use12Hours --
    // matching the unpadded "h" custom format specifier TimeFormatString uses for the same mode.
    string HourOptionText(int h) => Use12Hours
        ? (h % 12 == 0 ? 12 : h % 12).ToString(PickerCulture)
        : h.ToString("00", CultureInfo.InvariantCulture);

    // The OK button is Time/DateTime mode's close signal -- both modes commit incrementally (time
    // selects, and in DateTime a day click too) without closing, so nothing needs committing here.
    async Task OnPickerOkAsync()
    {
        _pendingInputFocus = true; // the OK button is about to unmount
        await CloseAsync();
    }

    // ----- JS interop (mirrors DateRangePicker's module lifecycle) -------------

    // Imports the RCL-local module once, re-checking _disposed after the awaited import so a
    // dispose that raced an in-flight import cleans up here instead of stranding the reference.
    // Returns null when disposed or JS is unavailable (prerender, bUnit) — callers no-JS degrade.
    async Task<IJSObjectReference?> GetModuleAsync()
    {
        if (_disposed) return null;
        try
        {
            _module ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", JsModuleUrl.Resolve(FormDefaults, "wss-overlay.js"));
        }
        catch
        {
            return null; // no JS runtime / module (prerender, tests)
        }
        if (_disposed)
        {
            try { await _module.DisposeAsync(); } catch { /* circuit may be gone */ }
            _module = null;
            return null;
        }
        return _module;
    }

    // Same contract as GetModuleAsync, for the separate wss-picker.js module (arrow-key page-scroll
    // suppression + post-navigation DOM focus). A distinct module so a consumer that never drives the
    // grid by keyboard doesn't pay for it, and so this stays decoupled from the unrelated overlay code.
    async Task<IJSObjectReference?> GetPickerNavModuleAsync()
    {
        if (_disposed) return null;
        try
        {
            _pickerModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", JsModuleUrl.Resolve(FormDefaults, "wss-picker.js"));
        }
        catch
        {
            return null; // no JS runtime / module (prerender, tests)
        }
        if (_disposed)
        {
            try { await _pickerModule.DisposeAsync(); } catch { /* circuit may be gone */ }
            _pickerModule = null;
            return null;
        }
        return _pickerModule;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // One-time input/wrapper wiring (Enter form-submit suppression + focus-out close). The
        // input is always rendered (not inside the @if), so once is enough. initPicker's second
        // input slot is null-safe — this picker has only the one.
        if (!_inputWired)
        {
            _inputWired = true;
            var module = await GetModuleAsync();
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("initPicker", _wrapperRef, _inputRef, null);
                }
                catch
                {
                    // No JS — Enter may implicitly submit an enclosing form; typing still commits
                    // via the change event, and the backdrop still closes on click.
                }
            }
        }

        if (_open && !_positioned)
        {
            var module = await GetModuleAsync();
            if (module is not null)
            {
                try
                {
                    // placePanel positions/flips the panel AND returns the open-order z-index it
                    // wrote to the wrapper; mirror it so the bound style re-asserts it (see Select).
                    var z = await module.InvokeAsync<int>("placePanel", _wrapperRef, _panelRef, "wss-picker-backdrop", 4);
                    // 0 is the JS null-ref guard value — only positive values are real.
                    _openZIndex = z > 0 ? z : null;
                }
                catch
                {
                    // No JS runtime / module — keep the CSS default (below, left-aligned) placement.
                }
            }

            var navModule = await GetPickerNavModuleAsync();
            if (navModule is not null)
            {
                try
                {
                    await navModule.InvokeVoidAsync("init", _gridRef);
                }
                catch
                {
                    // No JS — arrow keys still update the roving-tabindex state, just without the
                    // native page-scroll suppression.
                }
            }

            _positioned = true;
            StateHasChanged(); // reveal now that it's positioned (drops wss-measuring)
        }
        else if (!_open && _positioned)
        {
            _positioned = false;
            _openZIndex = null;
            try
            {
                if (_module is not null) await _module.InvokeVoidAsync("clearZ", _wrapperRef);
            }
            catch
            {
                // No JS runtime / module — nothing was assigned, nothing to clear.
            }

            if (_pendingInputFocus)
            {
                // The panel subtree (whatever had focus) just unmounted — reclaim focus onto the
                // input rather than leaving it stranded on <body>. Best-effort: FocusAsync throws if
                // the element isn't actually focusable yet (prerender/tests).
                _pendingInputFocus = false;
                _suppressOpenOnFocus = true;
                try { await _inputRef.FocusAsync(); } catch { /* not focusable yet (prerender/tests) */ }
                // Normally consumed by OnInputFocus during the call (the focus event outruns the
                // interop ack on both runtimes); this backstop covers a failed/eventless focus.
                _suppressOpenOnFocus = false;
            }
        }

        if (_open && _pendingFocusDate is { } focusDate)
        {
            _pendingFocusDate = null;
            var navModule = await GetPickerNavModuleAsync();
            if (navModule is not null)
            {
                try
                {
                    await navModule.InvokeVoidAsync("focusDay", _panelRef,
                        focusDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                catch
                {
                    // No JS — the roving-tabindex state still moved; only the DOM focus follow is lost.
                }
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Set first: an import in flight (GetModuleAsync/GetPickerNavModuleAsync) re-checks this
        // after its await and disposes its own late-assigned module rather than stranding it on this
        // dead instance.
        _disposed = true;
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { /* circuit may be gone */ }
            _module = null;
        }
        if (_pickerModule is not null)
        {
            try { await _pickerModule.DisposeAsync(); } catch { /* circuit may be gone */ }
            _pickerModule = null;
        }
    }
}
