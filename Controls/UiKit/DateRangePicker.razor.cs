using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-style date-range picker: a composite start → end field that opens a dropdown with
/// an optional preset sidebar and a dual-panel calendar whose headers are quick-select
/// dropdowns/nav buttons. <see cref="Mode"/> selects the granularity of both panels together --
/// <c>Date</c> (default) shows two consecutive one-month calendars; <c>Month</c> shows two
/// consecutive years, each a 3x4 grid of month buttons; <c>Quarter</c> shows two consecutive
/// years, each a single row of 4 quarter buttons; <c>Year</c> shows two consecutive decades, each
/// a 3x4 grid of year buttons (10 of the decade plus 2 dimmed adjacent-decade years); <c>Week</c>
/// shows the exact same dual one-month calendars as <c>Date</c> plus a leading week-number column
/// in both -- there the ROW, not the day, is the selection unit: clicking any day commits its
/// week's start (per <see cref="EffectiveFirstDayOfWeek"/>), and the range band/hover-preview tint
/// whole rows instead of individual cells. Picking the second unit of a range (or a preset) commits
/// the range and closes; typed input commits on Enter or blur.
/// </summary>
/// <remarks>
/// <para>
/// <c>DateTime</c>/<c>Time</c> abandon the dual-panel layout the moment time is involved -- AntD
/// does the same, since two independent per-side clocks alongside a linked two-month calendar reads
/// as more controls than the picker actually needs. Instead a SINGLE panel edits one endpoint at a
/// time, matching whichever input (<see cref="_activeInput"/>) is active: <c>DateTime</c> shows a
/// one-month calendar (prev/next nav on BOTH sides, like <see cref="DatePicker"/>'s own single
/// panel) with the time row and an OK button below it; <c>Time</c> shows just the time row and OK.
/// A day click sets the active endpoint's PENDING date (preserving its pending/committed
/// time-of-day); a time-row change sets its pending time part the same way
/// <see cref="DatePicker"/>'s own time row composes one, but writes to the pending value instead of
/// committing immediately -- nothing reaches <see cref="Start"/>/<see cref="End"/> (or fires
/// <see cref="StartChanged"/>/<see cref="EndChanged"/>) until OK. OK confirms the ACTIVE endpoint's
/// pending value (falling back to its already-committed value if the session never touched it) and
/// then: if the OTHER endpoint has neither a pending nor a committed value, switches the session to
/// it (the active underline moves, the panel re-anchors); otherwise both endpoints are now
/// resolved, so it commits them together (swapping a backwards pair, same as every other mode) and
/// closes. Escape/backdrop discards the whole in-progress session and reverts both inputs to their
/// last committed values. Typed input keeps the ordinary per-endpoint immediate-commit route
/// (Enter/blur), bypassing the session entirely.
/// </para>
/// <para>
/// Not a form control (no <c>InputBase</c>/validation wiring) — bind with <c>@bind-Start</c> /
/// <c>@bind-End</c>. JS interop (viewport flip/clamp, form-submit suppression, focus-out close,
/// arrow-key page-scroll suppression, and <c>ArrowDown</c> from either field stepping focus into
/// whichever grid owns the roving tabindex) degrades gracefully: without JS the dropdown opens below
/// the field at the CSS default placement, everything remains clickable, Tab still reaches the
/// grids, and arrow-key grid navigation still updates the roving-tabindex state (just without the
/// DOM focus follow or the native page-scroll suppression). The one degradation that is NOT purely
/// cosmetic is the focus-out dismissal — see <see cref="PickerBase"/>'s own remarks for exactly what
/// that costs and why C# can't substitute for it.
/// </para>
/// </remarks>
public partial class DateRangePicker : PickerBase
{
    // ----- Parameters -------------------------------------------------------

    /// <summary>Start of the bound range (null = empty). Supports <c>@bind-Start</c>. Normalized to
    /// <see cref="Mode"/>'s own granularity on every commit (see <see cref="SetRangeAsync"/>) --
    /// midnight for <c>Date</c>, the 1st of the month for <c>Month</c>, the 1st of the quarter for
    /// <c>Quarter</c>, January 1st for <c>Year</c>.</summary>
    [Parameter] public DateTime? Start { get; set; }
    /// <summary>Raised with the new start when it changes (supports <c>@bind-Start</c>).</summary>
    [Parameter] public EventCallback<DateTime?> StartChanged { get; set; }

    /// <summary>End of the bound range (null = empty). Supports <c>@bind-End</c>. Same
    /// normalization as <see cref="Start"/>.</summary>
    [Parameter] public DateTime? End { get; set; }
    /// <summary>Raised with the new end when it changes (supports <c>@bind-End</c>).</summary>
    [Parameter] public EventCallback<DateTime?> EndChanged { get; set; }

    /// <summary>
    /// Obsolete compile-time guard: no longer used — a stray <c>Field="..."</c> attribute is now a
    /// compile error instead of silently being splatted as an unmatched attribute (this component
    /// captures unmatched values, and a declared parameter always wins over splatting). This
    /// component binds via <c>@bind-Start</c>/<c>@bind-End</c>, never <c>Field</c> — remove the
    /// attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Start/@bind-End are sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<DateTime?>>? Field { get; set; }

    /// <summary>What both panels select together. Defaults to <see cref="DatePickerMode.Date"/>.
    /// <see cref="DatePickerMode.DateTime"/> and <see cref="DatePickerMode.Time"/> replace the
    /// dual-panel layout with a single per-endpoint pick session ending in an OK button -- see the
    /// class remarks for the full flow.</summary>
    [Parameter] public DatePickerMode Mode { get; set; } = DatePickerMode.Date;

    /// <summary>Optional shortcuts rendered as a sidebar in the dropdown. Each consumer supplies its
    /// own list (nothing is built in); clicking one commits its resolved range and closes.</summary>
    [Parameter] public IReadOnlyList<DateRangePreset>? Presets { get; set; }

    /// <summary>Extra content rendered in its own strip (<c>wss-picker-extra-footer</c>) — mirrors
    /// <see cref="DatePicker.ExtraFooter"/>'s placement/markup exactly (AntD's <c>renderExtraFooter</c>).
    /// Renders in EVERY mode: below the dual-panel calendar (this control has no footer of its own
    /// there — see the remark below) in <see cref="DatePickerMode.Date"/>/<see cref="DatePickerMode.Week"/>/
    /// <see cref="DatePickerMode.Month"/>/<see cref="DatePickerMode.Quarter"/>/<see cref="DatePickerMode.Year"/>,
    /// and above the OK footer in the <see cref="DatePickerMode.DateTime"/>/<see cref="DatePickerMode.Time"/>
    /// pick session — the same composition <see cref="DatePicker"/>'s own DateTime mode uses for its
    /// time row + OK footer.</summary>
    /// <remarks>
    /// Deliberately no <c>ShowToday</c>/<c>ShowNow</c> here, unlike <see cref="DatePicker"/>: AntD's
    /// RangePicker has no Today/Now-link equivalent in its own footer — <see cref="Presets"/> is the
    /// range picker's quick-affordance instead, so there's no existing footer row for those links to
    /// share with this one in the dual-panel modes.
    /// </remarks>
    [Parameter] public RenderFragment? ExtraFooter { get; set; }

    /// <summary>The month/year/decade both panels open showing when <see cref="Start"/> and
    /// <see cref="End"/> are both null (AntD's <c>defaultPickerValue</c>) — ignored once either
    /// endpoint is set. <see cref="Open"/>'s view anchor is <c>Start ?? End ?? DefaultViewDate ?? DateTime.Today</c>
    /// in every mode, normalized to Mode's own unit/panel the same way an explicit Start/End anchor
    /// already is (see <see cref="AnchorView"/>).</summary>
    [Parameter] public DateTime? DefaultViewDate { get; set; }

    /// <summary>Earliest selectable value (inclusive), at <see cref="Mode"/>'s own granularity --
    /// same day/month/quarter/year "whole unit before this is disabled" contract
    /// <see cref="DatePicker.Min"/> documents. Presets clamp to it (at day granularity) before
    /// normalizing.</summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>Latest selectable value (inclusive). Same mode-dependent granularity as
    /// <see cref="Min"/>.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Extra disable predicate alongside <see cref="Min"/>/<see cref="Max"/> — a cell (or
    /// a typed/clicked/preset commit) is disabled when either says so. Called with the CELL'S
    /// committed-value representative, at <see cref="Mode"/>'s own granularity: the day at midnight
    /// in <see cref="DatePickerMode.Date"/> (including <see cref="DatePickerMode.Week"/>'s
    /// individual day buttons, which stay day-granularity even though the row's own
    /// selection/commit unit is the week); the 1st of the month in <see cref="DatePickerMode.Month"/>;
    /// January 1st in <see cref="DatePickerMode.Year"/>; the 1st of the quarter in
    /// <see cref="DatePickerMode.Quarter"/>; and the WEEK START (not the individual day) for
    /// <see cref="DatePickerMode.Week"/>'s own commit guard — a partially-disabled week's day
    /// buttons can still be enabled while the row's commit itself is rejected, mirroring how
    /// <see cref="Min"/>/<see cref="Max"/> already split that mode (see <see cref="DatePicker.DisabledDate"/>
    /// for the single-date sibling's identical contract). A preset whose resolved, clamped, and
    /// normalized endpoints land on a disabled unit no-ops instead of committing (see
    /// <see cref="OnPresetClickAsync"/>) — unlike <see cref="Min"/>/<see cref="Max"/>, which a preset
    /// always clamps past, an arbitrary predicate can still reject the clamped result. Called once
    /// per rendered cell on every render (plus once per commit guard) — keep it cheap, no I/O.</summary>
    [Parameter] public Func<DateTime, bool>? DisabledDate { get; set; }

    /// <summary>Display and primary parse format for the two inputs. Typed text is parsed with this
    /// exact format first, then with the current culture's general date parsing. Null (default)
    /// picks <see cref="Mode"/>'s default (same values as <see cref="DatePicker.Format"/>'s):
    /// <c>Date</c> <c>MM/dd/yyyy</c> (the Figma spec) · <c>Month</c> <c>MM/yyyy</c> · <c>Year</c>
    /// <c>yyyy</c>. <c>Quarter</c> and <c>Week</c> have no .NET format token for a quarter number or
    /// an ISO-style week number: left null, they render/parse <c>yyyy-Qn</c> (e.g. "2026-Q3") /
    /// <c>yyyy-Www</c> (e.g. "2026-W08") via a hand-rolled special case instead of
    /// <see cref="DateTime.ToString(string)"/>; set explicitly, it is used verbatim via
    /// <c>ToString</c> and therefore can't render the quarter/week digits itself.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Raised with the offending text when a typed commit (Enter or blur) in the START input can't be
    /// parsed at all -- i.e. the parse itself fails, not merely a well-formed value rejected by
    /// <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<see cref="StartDisabledTime"/>
    /// (that's a valid value this picker simply won't accept, which is a different situation from a
    /// parse failure, and does not raise this callback). This picker has no validation concept of its
    /// own (see the class remarks) -- it exists so a host form control (<see cref="EditDateRange"/>)
    /// can surface a validation message the picker itself can't. Per endpoint rather than one callback
    /// carrying which side failed, matching every other per-input parameter on this control
    /// (<see cref="StartPlaceholder"/>/<see cref="EndPlaceholder"/>, <see cref="StartDisabledTime"/>/
    /// <see cref="EndDisabledTime"/>, the <c>StartAria*</c>/<c>EndAria*</c> pairs) — the wrapper needs
    /// the two apart anyway, to target each field's own <c>FieldIdentifier</c>. Optional: a standalone
    /// <see cref="DateRangePicker"/> with no handler attached behaves exactly as before this parameter
    /// existed -- the unparseable text is still silently reverted to the formatted bound value.
    /// </summary>
    [Parameter] public EventCallback<string> OnStartParseError { get; set; }
    /// <summary>Same contract as <see cref="OnStartParseError"/>, for the END input.</summary>
    [Parameter] public EventCallback<string> OnEndParseError { get; set; }

    /// <summary>
    /// Raised with the offending text when a typed commit (Enter or blur) in the START input parses
    /// into a perfectly well-formed value that this picker nonetheless REFUSES — one rejected by
    /// <see cref="Min"/>, <see cref="Max"/>, <see cref="DisabledDate"/> or
    /// <see cref="StartDisabledTime"/>. Deliberately a separate signal from
    /// <see cref="OnStartParseError"/> (which is parse failure only, and is documented as such): the
    /// two need different messages, and until this existed the rejection was completely silent — the
    /// field reverted, neither bound value changed, so a host form control never learned anything
    /// happened and no validator ran. Same optional contract as the parse callbacks: with no handler
    /// attached the text is still silently reverted. Only the typed-text commit paths raise it — a
    /// CLICK on a rejected cell is already visible as <c>aria-disabled</c>.
    /// </summary>
    [Parameter] public EventCallback<string> OnStartRangeError { get; set; }
    /// <summary>Same contract as <see cref="OnStartRangeError"/>, for the END input (guarded by
    /// <see cref="EndDisabledTime"/> rather than <see cref="StartDisabledTime"/>).</summary>
    [Parameter] public EventCallback<string> OnEndRangeError { get; set; }

    /// <summary>
    /// Raised on every ACCEPTED commit, carrying which endpoint(s) that commit ASSIGNED a value to —
    /// <b>including</b> an assignment whose value equals what that endpoint already holds, which
    /// <see cref="StartChanged"/>/<see cref="EndChanged"/> deliberately drop (each fires only when its
    /// own side actually changed). That per-endpoint dedup is exactly why this callback exists: a host
    /// form control showing an <see cref="OnStartParseError"/>/<see cref="OnStartRangeError"/> message
    /// has to retire it the moment an accepted entry lands in THAT input, and "the user retyped the date
    /// that endpoint already held" is an accepted entry no <c>Changed</c> callback can report.
    /// <see cref="EditDateRange"/> clears each endpoint's validation message from here.
    /// </summary>
    /// <remarks>
    /// One callback with a flags payload rather than the per-endpoint pair every other parameter on this
    /// control uses (<see cref="OnStartParseError"/>/<see cref="OnEndParseError"/>, …): a single commit
    /// can assign both ends, and reporting that as two callback invocations would make "one commit"
    /// unrecoverable. What each path reports: a two-click range pick, a preset click, a session OK, and
    /// <see cref="AllowClear"/>'s clear (a commit of "no range") all assign
    /// <see cref="DateRangeEndpoints.Both"/>; a typed entry — or an emptied input — assigns only its own
    /// side, even though the commit passes the other side's CURRENT value through unchanged (passing a
    /// value through is not assigning it, and clearing the other endpoint's message off the back of it
    /// would retire a message for text that was never revalidated). A commit whose endpoints get swapped
    /// for being backwards still reports only what the caller assigned — the other endpoint necessarily
    /// CHANGED in that case, so its own <c>Changed</c> callback carries the news. Not raised at all by
    /// the paths that commit nothing: an unparseable entry (<see cref="OnStartParseError"/>), a
    /// <see cref="Min"/>/<see cref="Max"/>/<see cref="DisabledDate"/>/<c>*DisabledTime</c> refusal
    /// (<see cref="OnStartRangeError"/>), a click on a rejected cell, the first click of a two-click
    /// pick (which commits nothing yet), and anything at all while <see cref="Disabled"/>. Optional,
    /// like the four error callbacks. Same contract as <see cref="DatePicker.OnValidCommit"/>, split per
    /// endpoint.
    /// </remarks>
    [Parameter] public EventCallback<DateRangeEndpoints> OnValidCommit { get; set; }

    /// <summary>Placeholder for the start input. Null (default) shows the uppercased
    /// <see cref="EffectiveFormat"/> (e.g. "2026-Q3" mode's own null-Format default, <c>yyyy</c>,
    /// uppercases to "YYYY" -- a placeholder that doesn't hint at the quarter shorthand; override
    /// explicitly if that matters for your consumer).</summary>
    [Parameter] public string? StartPlaceholder { get; set; }
    /// <summary>Placeholder for the end input. Same default as <see cref="StartPlaceholder"/>.</summary>
    [Parameter] public string? EndPlaceholder { get; set; }

    /// <summary>Shows a clear button (over the calendar icon) while a value is set. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary>Disables all interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Field width as a CSS length (e.g. "280px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }

    /// <inheritdoc cref="DatePicker.Size"/>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <summary>First day of the week for the calendar grids (<see cref="DatePickerMode.Date"/> and
    /// <see cref="DatePickerMode.Week"/> only -- the latter also uses it to compute each row's own
    /// week start). Null (default) follows <see cref="CultureInfo.CurrentCulture"/>. (The Figma
    /// mock's Monday start is the AntD kit's default-locale artifact, not a design decision — same
    /// category as its DM Sans font.)</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Shows a leading week-number column (AntD's <c>showWeek</c>) beside every rendered
    /// day grid — both panels in <see cref="DatePickerMode.Date"/>, the single session calendar in
    /// <see cref="DatePickerMode.DateTime"/> — with no other behavior change: a day click still
    /// commits that day, not its week. Defaults to false. <see cref="DatePickerMode.Week"/> always
    /// renders this column regardless of this parameter; <see cref="DatePickerMode.Time"/> has no
    /// calendar, so it never applies.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>HTML id applied to the start input — wires a consumer label / test hook.</summary>
    [Parameter] public string? Id { get; set; }
    /// <summary>HTML id applied to the end input — wires a consumer label / test hook.</summary>
    [Parameter] public string? EndId { get; set; }

    /// <summary>
    /// The start input's <c>autocomplete</c> token. Null (default) renders <c>"off"</c>, the value
    /// both inputs hardcoded before this parameter existed. Set it to the field's real purpose so
    /// browsers and assistive tech can autofill — WCAG 1.3.5 (Identify Input Purpose) is only
    /// satisfiable through this attribute, and consumer attributes can't supply it because
    /// <see cref="AdditionalAttributes"/> lands on the outer wrapper, not the inputs. Per-input
    /// (rather than one shared value) like every other per-endpoint parameter here: the two ends of
    /// a range are different purposes.
    /// </summary>
    [Parameter] public string? StartAutocomplete { get; set; }
    /// <summary>Same contract as <see cref="StartAutocomplete"/>, for the END input.</summary>
    [Parameter] public string? EndAutocomplete { get; set; }

    /// <summary>
    /// Accessible name for the composite field — the <c>.wss-picker-input</c> box holding BOTH
    /// inputs, which takes <c>role="group"</c> whenever this or <see cref="GroupLabelledBy"/> gives
    /// it a name. Without it a form wrapper's single visible label associates (<c>label[for]</c>)
    /// with the start input alone and the two inputs read as unrelated fields, with nothing tying
    /// the second to the first. Null (default) on both this and <see cref="GroupLabelledBy"/> leaves
    /// the box unroled and unnamed, byte-identical to before these existed — an unnamed
    /// <c>role="group"</c> is noise, not structure.
    /// </summary>
    [Parameter] public string? GroupLabel { get; set; }
    /// <summary>
    /// Id reference form of <see cref="GroupLabel"/> (<c>aria-labelledby</c>), for a wrapper that
    /// already renders the naming element — <see cref="EditDateRange"/> points it at its
    /// <c>FormLabel</c>'s <c>lbltext-{id}</c> naming anchor, so the group's name is the label's own
    /// text, live, and without the tooltip trigger that sits inside the same <c>&lt;label&gt;</c>.
    /// Wins over <see cref="GroupLabel"/> when both are set (per the accessible-name spec).
    /// </summary>
    [Parameter] public string? GroupLabelledBy { get; set; }

    // Localizable accessibility strings. Defaults are English, matching Select's convention.

    /// <summary>Accessible name of the start input. Override to localize.</summary>
    [Parameter] public string StartInputLabel { get; set; } = "Start date";
    /// <summary>Accessible name of the end input. Override to localize.</summary>
    [Parameter] public string EndInputLabel { get; set; } = "End date";
    /// <summary>Accessible name of the dropdown dialog. Override to localize.</summary>
    [Parameter] public string DialogLabel { get; set; } = "Choose date range";
    /// <summary>Accessible name of each panel's month select (<see cref="DatePickerMode.Date"/> only). Override to localize.</summary>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <summary>Accessible name of each panel's year select (<see cref="DatePickerMode.Date"/>/
    /// <see cref="DatePickerMode.Month"/>/<see cref="DatePickerMode.Quarter"/>). Override to localize.</summary>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <summary>Accessible name of the clear button. Override to localize.</summary>
    [Parameter] public string ClearLabel { get; set; } = "Clear dates";
    /// <summary>Accessible name of the preset sidebar list. Override to localize.</summary>
    [Parameter] public string PresetsLabel { get; set; } = "Quick ranges";
    /// <summary>Accessible name of the previous-month button (<see cref="DatePickerMode.Date"/>'s
    /// left panel, or <see cref="DatePickerMode.DateTime"/>'s single panel). Override to
    /// localize.</summary>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <summary>Accessible name of the next-month button (<see cref="DatePickerMode.Date"/>'s right
    /// panel, or <see cref="DatePickerMode.DateTime"/>'s single panel). Override to localize.</summary>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";
    /// <summary>Accessible name of the previous-year button (<see cref="DatePickerMode.Month"/>/
    /// <see cref="DatePickerMode.Quarter"/>'s left panel only). Override to localize.</summary>
    [Parameter] public string PrevYearLabel { get; set; } = "Previous year";
    /// <summary>Accessible name of the next-year button (<see cref="DatePickerMode.Month"/>/
    /// <see cref="DatePickerMode.Quarter"/>'s right panel only). Override to localize.</summary>
    [Parameter] public string NextYearLabel { get; set; } = "Next year";
    /// <summary>Accessible name of the previous-decade button (<see cref="DatePickerMode.Year"/>'s
    /// left panel only). Override to localize.</summary>
    [Parameter] public string PrevDecadeLabel { get; set; } = "Previous decade";
    /// <summary>Accessible name of the next-decade button (<see cref="DatePickerMode.Year"/>'s
    /// right panel only). Override to localize.</summary>
    [Parameter] public string NextDecadeLabel { get; set; } = "Next decade";

    /// <summary>Leading text of the week-number row header in <see cref="DatePickerMode.Week"/> —
    /// rendered as the accessible name "<c>{WeekLabel} {number}</c>" on each row's week-number cell,
    /// which is a <c>role="rowheader"</c> there (the row, not the day, is the selection unit in that
    /// mode, and the fields display <c>yyyy-Www</c>, so the grids have to expose the number for the
    /// two to be correlated). Override to localize. In every other mode the same cell stays
    /// <c>aria-hidden</c> decoration — <see cref="ShowWeekNumbers"/> changes nothing about what a day
    /// click commits, so its numbers are context, not structure. Mirrors
    /// <see cref="DatePicker.WeekLabel"/>.</summary>
    [Parameter] public string WeekLabel { get; set; } = "Week";

    // ----- Time/DateTime pick session: time-row options + OK footer -----------------------------
    // Mirror DatePicker's own equivalents one-for-one (defaults included) -- see DatePicker's own
    // doc comments for the full contract; only re-stated here where the per-endpoint (rather than
    // single-Value) framing changes the meaning.

    /// <summary>Whether the <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
    /// time row includes a seconds select. Defaults to true. False drops the seconds select entirely
    /// and normalization zeroes the second on every commit, so a stale second from before the flip
    /// was toggled can never survive a commit.</summary>
    [Parameter] public bool ShowSeconds { get; set; } = true;

    /// <summary>Step between the hour select's offered values (24-hour space, even under
    /// <see cref="Use12Hours"/>): the select lists 0, <c>HourStep</c>, 2*<c>HourStep</c>, and so on up
    /// to 23. Defaults to 1 (every hour). Values less than 1 are clamped to 1 at the point of use, not
    /// thrown. NEVER-JUMP RULE, composing with <see cref="StartDisabledTime"/>/<see cref="EndDisabledTime"/>'s
    /// own (see <see cref="HideDisabledTimeOptions"/>): if the active endpoint's hour isn't on the
    /// step lattice, its own option is still rendered (selected) so the select can never silently
    /// jump to a different hour.</summary>
    [Parameter] public int HourStep { get; set; } = 1;
    /// <summary>Same contract as <see cref="HourStep"/>, for the minute select (0-59). Defaults to 1.</summary>
    [Parameter] public int MinuteStep { get; set; } = 1;
    /// <summary>Same contract as <see cref="HourStep"/>, for the second select (0-59). Defaults to 1.
    /// Has no effect when <see cref="ShowSeconds"/> is false.</summary>
    [Parameter] public int SecondStep { get; set; } = 1;

    /// <summary>Shows the hour select in 12-hour form plus a trailing period select
    /// (<see cref="PeriodSelectLabel"/>), instead of the default single 24-hour select -- see
    /// <see cref="DatePicker.Use12Hours"/> for the full contract (option values stay 24-hour; only
    /// the displayed text and the period select are 12-hour). Defaults to false.</summary>
    [Parameter] public bool Use12Hours { get; set; }

    /// <summary>Disables specific hour/minute/second option values in the START endpoint's time row
    /// (the row shown while it's the active endpoint) -- see <see cref="DatePicker.DisabledTime"/>
    /// for the full per-option contract (invoked with the active endpoint's own date part, disabled
    /// options render disabled or are omitted per <see cref="HideDisabledTimeOptions"/>, and a
    /// pending/typed commit landing on a disabled part is rejected). Every route into this endpoint's
    /// value is guarded: a time select, a typed commit, a preset, a session day click (which carries
    /// the endpoint's own time-of-day onto a date that may disable it) and the session's own OK.</summary>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? StartDisabledTime { get; set; }
    /// <summary>Same contract as <see cref="StartDisabledTime"/>, for the END endpoint.</summary>
    [Parameter] public Func<DateTime?, DisabledTimeParts?>? EndDisabledTime { get; set; }

    /// <summary>When true, an option <see cref="StartDisabledTime"/>/<see cref="EndDisabledTime"/>
    /// disables is omitted from its select entirely instead of rendered disabled (AntD's
    /// <c>hideDisabledOptions</c>). Defaults to false. NEVER-JUMP RULE, in force under either
    /// setting: the select's CURRENT value's own option is always rendered -- selected, and also
    /// marked <c>disabled</c> if the active endpoint's callback says so -- even while every other
    /// disabled option is hidden.</summary>
    [Parameter] public bool HideDisabledTimeOptions { get; set; }

    /// <summary>Accessible name of the hour select (<see cref="DatePickerMode.Time"/>/
    /// <see cref="DatePickerMode.DateTime"/>'s time row). Override to localize.</summary>
    [Parameter] public string HourSelectLabel { get; set; } = "Hour";
    /// <summary>Accessible name of the minute select. Override to localize.</summary>
    [Parameter] public string MinuteSelectLabel { get; set; } = "Minute";
    /// <summary>Accessible name of the second select. Override to localize.</summary>
    [Parameter] public string SecondSelectLabel { get; set; } = "Second";
    /// <summary>Accessible name of the AM/PM period select (<see cref="Use12Hours"/>). Override to
    /// localize.</summary>
    [Parameter] public string PeriodSelectLabel { get; set; } = "AM/PM";
    /// <summary>Visible text of the OK button that confirms the active endpoint and (once both are
    /// resolved) commits and closes the <see cref="DatePickerMode.Time"/>/<see cref="DatePickerMode.DateTime"/>
    /// pick session. Override to localize.</summary>
    [Parameter] public string OkText { get; set; } = "OK";

    /// <summary>Leading text of the visually-hidden format hint BOTH inputs' <c>aria-describedby</c>
    /// points at (they share a single <see cref="Format"/>, so there is one hint, not two) — rendered
    /// as "<c>{FormatHintLabel} {format}</c>", where the format is <see cref="Format"/>'s effective
    /// value (or <c>yyyy-Qn</c>/<c>yyyy-Www</c> in <see cref="DatePickerMode.Quarter"/>/
    /// <see cref="DatePickerMode.Week"/>'s own shorthand modes, which is what the fields actually
    /// display and parse there). Defaults to "Format:"; override to localize, or set to an empty
    /// string to drop the hint (and its <c>aria-describedby</c> token) entirely. Deliberately separate
    /// from <see cref="StartPlaceholder"/>/<see cref="EndPlaceholder"/>.</summary>
    [Parameter] public string FormatHintLabel { get; set; } = "Format:";

    /// <summary>Leading text of the visually-hidden <see cref="Min"/> clause of the range hint BOTH
    /// inputs' <c>aria-describedby</c> points at — rendered as "<c>{RangeHintMinLabel} {Min}</c>",
    /// beside <see cref="FormatHintLabel"/>'s format clause in the same shared element (one calendar,
    /// one pair of bounds, so one hint serves both endpoints). Only rendered when <see cref="Min"/> is
    /// set (and never in <see cref="DatePickerMode.Time"/>, which ignores <see cref="Min"/>/
    /// <see cref="Max"/> entirely — see <see cref="IsCommitDisabled"/>'s Time arm). Defaults to
    /// "Earliest date:"; override to localize, or set to an empty string to drop this clause. The
    /// bound is formatted with the same <see cref="Format"/> the fields themselves display and parse,
    /// so the hint reads in the shape the user is expected to type. Exists because
    /// <see cref="Min"/>/<see cref="Max"/> otherwise reach the user only as per-cell
    /// <c>aria-disabled</c> in the calendars — invisible to someone typing, which is the faster path,
    /// and a range has TWO endpoints to get wrong. Mirrors <see cref="DatePicker.RangeHintMinLabel"/>.</summary>
    [Parameter] public string RangeHintMinLabel { get; set; } = "Earliest date:";
    /// <summary>The <see cref="Max"/> clause of the same hint (see <see cref="RangeHintMinLabel"/>).
    /// Defaults to "Latest date:". With both bounds set the two clauses render together, period-
    /// separated, in <see cref="Min"/>-then-<see cref="Max"/> order.</summary>
    [Parameter] public string RangeHintMaxLabel { get; set; } = "Latest date:";

    // Validation-state ARIA passthrough onto the actual inputs, for form wrappers (EditDateRange).
    // Same shape as Select's AriaRequired/AriaInvalid/AriaDescribedBy trio, doubled because the two
    // bound fields validate independently — AdditionalAttributes can't do this job because it lands
    // on the outer wrapper div.

    /// <summary>Value for the start input's <c>aria-required</c>; null (default) omits it.</summary>
    [Parameter] public string? StartAriaRequired { get; set; }
    /// <summary>Renders <c>aria-invalid="true"</c> on the start input when true.</summary>
    [Parameter] public bool StartAriaInvalid { get; set; }
    /// <summary>Value for the start input's <c>aria-describedby</c>; null (default) omits it.</summary>
    [Parameter] public string? StartAriaDescribedBy { get; set; }
    /// <summary>Value for the start input's <c>aria-errormessage</c>; null (default) omits it.
    /// Pair with <see cref="StartAriaInvalid"/>.</summary>
    [Parameter] public string? StartAriaErrorMessage { get; set; }
    /// <summary>Value for the end input's <c>aria-required</c>; null (default) omits it.</summary>
    [Parameter] public string? EndAriaRequired { get; set; }
    /// <summary>Renders <c>aria-invalid="true"</c> on the end input when true.</summary>
    [Parameter] public bool EndAriaInvalid { get; set; }
    /// <summary>Value for the end input's <c>aria-describedby</c>; null (default) omits it.</summary>
    [Parameter] public string? EndAriaDescribedBy { get; set; }
    /// <summary>Value for the end input's <c>aria-errormessage</c>; null (default) omits it.
    /// Pair with <see cref="EndAriaInvalid"/>.</summary>
    [Parameter] public string? EndAriaErrorMessage { get; set; }

    /// <summary>
    /// Unmatched attributes (e.g. a consumer's <c>class</c>, <c>style</c>, or <c>data-*</c>),
    /// applied to the root wrapper (<c>.wss-picker</c>) — never the dropdown panel, whose inline
    /// placement is JS-owned. <c>class</c> and <c>style</c> merge with the component's own; the
    /// rest are splatted verbatim.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // ----- State ------------------------------------------------------------
    // Shared JS-interop/overlay-lifecycle state (_wrapperRef, _panelRef, the two JsModule holders,
    // _open, _positioned, _disposed, _inputsWired, _openZIndex, _focusDay, _pendingFocusDate,
    // _pendingInputFocus, _suppressOpenOnFocus) lives on PickerBase.

    ElementReference _startInputRef;
    ElementReference _endInputRef;
    // Index 0 = left panel's grid, 1 = right panel's — see the ant-design-blazor / procurement-hub
    // precedent for @ref into an array element inside a @for loop.
    readonly ElementReference[] _gridRefs = new ElementReference[2];
    // Lazy fallback for BaseId when the consumer set no Id -- the panel/format-hint ids still have to
    // be unique per instance and stable across renders (aria-controls/aria-describedby resolve by id).
    string? _generatedId;
    // 0 = start, 1 = end. Drives the active-side underline while open.
    int _activeInput;
    // A new range pick is in progress: the first unit is chosen, the second click commits. While
    // true the display shows only _pendingStart (the committed Start/End stay untouched until the
    // pick completes, so Escape/backdrop discards cleanly).
    bool _selecting;
    DateTime? _pendingStart;
    // The unit currently under the pointer while _selecting — drives the hover-range preview tint
    // between _pendingStart and this unit. Never set (or read) outside a pick in progress.
    DateTime? _hoverDay;
    // First-of-month shown in the left panel in Mode.Date (and its Week fallback); the right panel
    // is always _viewMonth + 1 month. In Mode.Month/Quarter, only _viewMonth.Year matters -- it's
    // the left panel's year (see LeftYear/RightYear). In Mode.Year, only _viewMonth.Year matters too
    // -- ClampDecadeStartForRange floors it to the left panel's decade (see LeftDecadeStart/
    // RightDecadeStart). In Mode.DateTime it's the SINGLE pick-session panel's own month (no pair,
    // no offset -- see SessionPrevMonthDisabled/SessionNextMonthDisabled and OnSessionOkAsync's own
    // re-anchor, which is why AnchorView's -panel trick is never used for this mode). Mode.Time has
    // no calendar, so this field is simply unused there. One field serves every grain because
    // exactly one of these readings is ever active for a given Mode, and each mode's own mutations
    // (nav buttons, selects, keyboard crossing) only ever touch it through the matching property.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text per input (null = show the formatted bound value).
    string? _startEdit;
    string? _endEdit;
    // The Start/End last seen on a parameter set -- what OnParametersSetAsync compares against to tell
    // an EXTERNAL change (a parent swapping the bound record, a form reset, a programmatic set) from a
    // re-render that left the endpoint alone. Tracked per side, so typing into one input survives an
    // external change to the OTHER. See OnParametersSetAsync for the full rationale.
    DateTime? _lastStartParam;
    DateTime? _lastEndParam;

    // ----- Time/DateTime pick session state ----------------------------------
    // A session edits ONE endpoint at a time (whichever _activeInput points at). Neither field ever
    // reaches Start/End (or fires StartChanged/EndChanged) until OK resolves both sides -- see
    // OnSessionOkAsync. Null means "this endpoint hasn't been touched THIS session" -- every read
    // site falls back to the endpoint's own committed Start/End (see ActiveSessionValue), so opening
    // on an already-committed pair and hitting OK twice in a row (touching neither field) still
    // resolves and closes correctly. Reset (both to null) on Open/Close/Escape/ClearAsync/
    // FinishTextCommit, mirroring _pendingStart's own reset sites for the date-only two-click flow
    // above -- these two flows never coexist (Mode is fixed per instance), so there's no risk of the
    // two kinds of "in-progress pick" state colliding.
    DateTime? _pendingSessionStart;
    DateTime? _pendingSessionEnd;

    // ----- Mode-derived helpers ----------------------------------------------

    /// <summary><see cref="Mode"/> folded for every CALENDAR-SHAPE concern (which day/month/quarter/
    /// year grid layout renders, its Min/Max/DisabledDate granularity, its keyboard nav): the
    /// <see cref="DatePickerMode.DateTime"/> pick session's single calendar is day-shaped exactly
    /// like <see cref="DatePickerMode.Date"/>'s (see the class remarks), and <see cref="DatePickerMode.Time"/>
    /// renders no calendar at all so this fold is simply never consulted for it. This is NOT used
    /// for the pick-session's own state machine, its per-endpoint time-of-day handling, or
    /// normalization -- those read the raw <see cref="Mode"/> directly (see <see cref="IsSessionMode"/>,
    /// <see cref="NormalizeForMode"/>) since DateTime/Time need genuinely different behavior there,
    /// not a fold onto Date. <see cref="DatePickerMode.Week"/> is its own mode below -- it reuses
    /// every Date-mode day-grid mechanism (view anchoring, focus/keyboard nav, Min/Max
    /// day-granularity cell disabling) via this switch's default arm, differing only in the markup
    /// (a week-number rows layout instead of the flat grid — see <see cref="ShowWeekRows"/>) and the
    /// commit granularity (a day click resolves to its WEEK START before reaching
    /// <see cref="OnUnitClickAsync"/> — see <see cref="OnWeekDayClickAsync"/>). It is also
    /// <see cref="PickerBase.EffectiveMode"/>, the hook the shared display/parse layer
    /// (<see cref="PickerBase.FormatDate"/>/<see cref="PickerBase.TryParseDate"/>) keys off — which is
    /// exactly what this fold already drove for that code before it moved to the base.</summary>
    internal override DatePickerMode EffectiveMode => Mode switch
    {
        DatePickerMode.DateTime or DatePickerMode.Time => DatePickerMode.Date,
        _ => Mode,
    };

    /// <summary>Whether <see cref="Mode"/> is the single-panel pick session
    /// (<see cref="DatePickerMode.DateTime"/>/<see cref="DatePickerMode.Time"/>) rather than one of
    /// the dual-panel modes -- gates the .razor markup's top-level branch between the two entirely
    /// separate layouts, and every session-only helper below assumes this is true.</summary>
    bool IsSessionMode => Mode is DatePickerMode.DateTime or DatePickerMode.Time;

    // ----- Display helpers (used by the .razor markup) ------------------------

    string WrapperClass
    {
        get
        {
            var classes = "wss-picker";
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

    // PickerBase's "was Format explicitly set" hook -- what gates Quarter's/Week's shorthand
    // display/parse (see PickerBase.FormatDate/TryParseDate).
    internal override string? ExplicitFormat => Format;

    // Format/Placeholder resolution: an explicit value always wins; null falls through to Mode's
    // default -- the same PickerMath.ModeDisplayFormat defaults DatePicker's own EffectiveFormat uses
    // (Quarter's and Week's bland "yyyy" included; see that helper's doc comment for why). All
    // internal display/parse code routes through this (never the raw Format parameter). Keys off the
    // raw Mode (not EffectiveMode) so DateTime/Time get their own time-aware format instead of
    // EffectiveMode's Date fold's plain "MM/dd/yyyy" -- every other arm is unaffected, since
    // EffectiveMode only ever differs from Mode for those two.
    internal override string EffectiveFormat =>
        Format ?? PickerMath.ModeDisplayFormat(Mode, "MM/dd/yyyy", "MM/yyyy", Use12Hours, ShowSeconds);

    string DefaultPlaceholder => EffectiveFormat.ToUpperInvariant();

    // The id everything else derives from: the START input's consumer Id when set (so a consumer-owned
    // id keeps driving the derived ones), otherwise a generated per-instance fallback. Same shape as
    // DatePicker.BaseId / Select.BaseId / Tabs.BaseId. EndId is deliberately NOT involved: the panel
    // and the format hint belong to the control as a whole, not to one endpoint.
    string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-picker-range-{Guid.NewGuid():N}");

    // The dropdown panel's own id -- what BOTH inputs' aria-controls point at while open.
    string PanelId => $"{BaseId}-panel";

    // The single visually-hidden typing hint's id, appended to BOTH inputs' aria-describedby. Still
    // named "-format" (a published, test-anchored id) even though the element now also carries the
    // Min/Max clauses -- it is one hint about what may be typed, not two.
    string FormatHintId => $"{BaseId}-format";

    // "Format: MM/dd/yyyy" (or blank, which suppresses that clause).
    string FormatHintText =>
        string.IsNullOrEmpty(FormatHintLabel) ? string.Empty : $"{FormatHintLabel} {DescribedFormat}";

    // "Earliest date: 01/01/2026" / "Latest date: 12/31/2026" / both, period-separated -- the Min/Max
    // bounds as TEXT, which is the only channel someone typing (rather than clicking a cell) has for
    // them. ONE pair of clauses for both inputs, like the format clause above: Min/Max bound the
    // single shared calendar, not one endpoint each. Formatted with FormatDate, so the hint reads in
    // exactly the shape the fields parse. Keys off the RAW Mode, not EffectiveMode's Date fold:
    // Mode.Time ignores Min/Max outright (see IsCommitDisabled's Time arm), so naming them there would
    // describe a constraint that isn't enforced. Mirrors DatePicker.RangeHintText.
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

    // The whole visually-hidden typing hint: the format clause, then the range clauses. Blank (every
    // clause suppressed, or nothing to say) drops the element AND both describedby tokens, exactly as
    // blanking FormatHintLabel alone always did.
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

    // Each endpoint's own consumer-supplied aria-describedby (a form wrapper's error/description ids
    // -- see EditDateRange) with the shared hint's id APPENDED, so the wrapper's chain keeps
    // its order and the hint reads last. Null (no hint, no consumer value) omits the attribute exactly
    // as before this existed.
    string? EffectiveStartAriaDescribedBy => WithFormatHint(StartAriaDescribedBy);
    string? EffectiveEndAriaDescribedBy => WithFormatHint(EndAriaDescribedBy);

    string? WithFormatHint(string? describedBy) => HintText.Length == 0
        ? describedBy
        : string.IsNullOrEmpty(describedBy) ? FormatHintId : $"{describedBy} {FormatHintId}";

    // What the panel currently displays, as one string: the text of the panel's aria-live region, so
    // every navigation (nav buttons, header selects, a keyboard move that crosses the view) announces
    // where the calendar now is. The dual-panel modes name BOTH panels -- each grid also carries its
    // own half as its aria-label, and this joins them in reading order. Time mode has no calendar to
    // navigate, so it contributes nothing.
    string ViewLabel => Mode switch
    {
        DatePickerMode.Time => string.Empty,
        DatePickerMode.DateTime => SessionViewLabel,
        _ => EffectiveMode switch
        {
            DatePickerMode.Month or DatePickerMode.Quarter =>
                $"{LeftYear.ToString(PickerCulture)}, {RightYear.ToString(PickerCulture)}",
            DatePickerMode.Year => $"{DecadeLabelFor(LeftDecadeStart)}, {DecadeLabelFor(RightDecadeStart)}",
            _ => $"{_viewMonth.ToString("MMMM yyyy", PickerCulture)}, " +
                 $"{_viewMonth.AddMonths(1).ToString("MMMM yyyy", PickerCulture)}",
        },
    };

    // The Time/DateTime pick session's lone calendar panel: its grid's accessible name, and (via
    // ViewLabel above) its live-region text.
    string SessionViewLabel => _viewMonth.ToString("MMMM yyyy", PickerCulture);

    // role="group" on the composite field, but ONLY once something actually names it -- an unnamed
    // group adds a boundary announcement with no information in it. A standalone picker (no wrapper
    // supplying either name parameter) therefore renders exactly the DOM it always did.
    string? GroupRole =>
        !string.IsNullOrEmpty(GroupLabelledBy) || !string.IsNullOrEmpty(GroupLabel) ? "group" : null;

    // Each panel's own month/year select names, suffixed with the panel's own view label. The two
    // panels' selects are otherwise character-identical ("Month", "Year", twice each), which left the
    // dropdown presenting four combo boxes with two distinct names between them and nothing to say
    // which calendar each drove -- the panels themselves carry no role or name, so only the grids
    // (which already suffix this way) disambiguated anything.
    string PanelMonthSelectLabel(DateTime month) => $"{MonthSelectLabel}, {month.ToString("MMMM yyyy", PickerCulture)}";
    string PanelYearSelectLabel(DateTime month) => $"{YearSelectLabel}, {month.ToString("MMMM yyyy", PickerCulture)}";
    // Month/Quarter mode's panels show a bare year, which is the whole of their own view label.
    string PanelYearSelectLabel(int year) => $"{YearSelectLabel}, {year.ToString(PickerCulture)}";

    // The week-number cell's ARIA in Mode.Week: a real row header naming the row the fields' own
    // yyyy-Www display refers to. Every other mode leaves the cell aria-hidden decoration (see
    // WeekLabel). Keys off EffectiveMode for the same reason every other grid concern does -- the
    // pick session's own calendar is day-shaped, so it never takes the header. Mirrors DatePicker's
    // identical trio.
    string? WeekHeaderRole => EffectiveMode == DatePickerMode.Week ? "rowheader" : null;
    string? WeekHeaderHidden => EffectiveMode == DatePickerMode.Week ? null : "true";
    string? WeekHeaderLabel(DateTime weekStart) =>
        EffectiveMode == DatePickerMode.Week ? $"{WeekLabel} {WeekNumberOf(weekStart).ToString(PickerCulture)}" : null;

    // ----- ARIA grid row grouping --------------------------------------------
    // The day grids get their rows for free (GridWeekRows -- the same 6x7 chunking the week-number
    // layout already rendered); the unit grids need the same treatment, chunked to match each grid's
    // own CSS grid-template-columns so a role="row" never spans a visual line break. The wrappers are
    // display:contents, so the buttons stay the real grid items and the layout is unchanged. Mirrors
    // DatePicker's own set, parameterized by panel (this control renders each grid twice).

    // .wss-picker-month-grid is repeat(3, 1fr); .wss-picker-quarter-grid is repeat(4, 1fr).
    const int MonthGridColumns = 3;
    const int QuarterGridColumns = 4;

    static IEnumerable<DateTime[]> UnitRows(IEnumerable<DateTime> units, int columns) => units.Chunk(columns);

    static IEnumerable<DateTime> MonthUnits(int year) =>
        Enumerable.Range(1, 12).Select(m => new DateTime(year, m, 1));

    // The panel's decade plus the leading/trailing dimmed adjacent-decade cells (the decade start is
    // already ClampDecadeStartForRange'd, which keeps both inside DateTime's representable range).
    static IEnumerable<DateTime> YearUnits(int decadeStart) =>
        Enumerable.Range(-1, 12).Select(i => new DateTime(decadeStart + i, 1, 1));

    static IEnumerable<DateTime> QuarterUnits(int year) =>
        Enumerable.Range(1, 4).Select(q => QuarterStart(year, q));

    // While a fresh pick is in progress the field previews it -- the date-only two-click flow shows
    // just the pending start (end empties, since a fresh pick fully replaces the old range); the
    // Time/DateTime pick session shows EACH side's own resolved value (pending-this-session, or
    // already committed) independently, since a session only ever touches the ACTIVE endpoint and
    // the other side's own display should keep showing whatever it already resolved to (its own
    // preview if picked earlier this session, otherwise its committed value) -- see the class
    // remarks. A discarded pick (Escape/backdrop) falls back to the committed values automatically
    // once _pendingStart/_pendingSessionStart/_pendingSessionEnd are cleared.
    string StartDisplay => _startEdit ?? FormatDate(IsSessionMode ? (_pendingSessionStart ?? Start) : (_selecting ? _pendingStart : Start));
    string EndDisplay => _endEdit ?? (IsSessionMode
        ? FormatDate(_pendingSessionEnd ?? End)
        : (_selecting ? string.Empty : FormatDate(End)));

    bool ShowClear => AllowClear && !Disabled && (Start is not null || End is not null);

    // The range the calendar highlights: the in-progress pick while one is underway, otherwise the
    // committed values, normalized to Mode's own granularity for the comparison -- Start/End are
    // ALWAYS already this shape when set through the control's own commit paths, but a consumer can
    // bind either parameter directly to an arbitrary raw value (e.g. a Month-mode Start with a
    // nonzero day, or a Date-mode Start with a nonzero time-of-day), and every unit button rendered
    // below is exactly Mode-normalized (first-of-month, midnight, etc.) -- without this, such a raw
    // value would never equal any button's own value, silently breaking IsEndpoint/CellClass/
    // UnitBtnClass and (worse) parking the roving tabindex on a value that matches nothing, making
    // the grid keyboard-unreachable. _pendingStart never needs this: it's always the exact value an
    // OnUnitClickAsync button click supplied, already unit-shaped.
    (DateTime? Start, DateTime? End) DisplayRange => _selecting
        ? (_pendingStart, null)
        : (Start is { } s ? NormalizeForMode(s) : null, End is { } e ? NormalizeForMode(e) : null);

    bool IsEndpoint(DateTime unit)
    {
        var (s, e) = DisplayRange;
        return unit == s || unit == e;
    }

    // Strictly between the two endpoints -- the interior of the committed (or in-progress) range, the
    // same span CellClass paints wss-picker-cell-in-range / UnitBtnClass paints
    // wss-picker-month-btn-in-range on.
    bool IsInRange(DateTime unit)
    {
        var (s, e) = DisplayRange;
        return s is { } a && e is { } b && a != b && unit > a && unit < b;
    }

    // What a day/month/quarter/year gridcell renders aria-selected="true" for. The whole range is
    // selected, not just its two ends: an interior day used to have a color band and NO ARIA state at
    // all, so a screen-reader user walking the grid could hear both endpoints but nothing about the 27
    // days between them. (The audit's PKR-8.) Endpoints keep their filled look via
    // wss-picker-day-selected / wss-picker-month-btn-selected, which stay class-driven.
    bool IsUnitAriaSelected(DateTime unit) => IsEndpoint(unit) || IsInRange(unit);

    // Week mode's row-level equivalent: the ROW is the selection unit there, so every one of its 7
    // cells answers with the row's own state (see IsWeekRowEndpoint / WeekRowClass).
    bool IsWeekAriaSelected(DateTime weekStart) => IsWeekRowEndpoint(weekStart) || IsInRange(weekStart);

    string CellClass(DateTime day)
    {
        var cls = "wss-picker-cell";
        // Week mode's range/preview banding paints whole ROWS (see WeekRowClass) -- a per-cell band
        // here would (mis)apply to only whichever single day happens to equal a week-start endpoint
        // (the row's own first cell), so this bypasses the day-granularity classing entirely for
        // that mode, mirroring DayClass's own bypass below.
        if (EffectiveMode == DatePickerMode.Week) return cls;
        var (s, e) = DisplayRange;
        if (s is { } a && e is { } b && a != b)
        {
            if (day == a) cls += " wss-picker-cell-range-start";
            else if (day == b) cls += " wss-picker-cell-range-end";
            else if (day > a && day < b) cls += " wss-picker-cell-in-range";
        }
        else if (_selecting && _pendingStart is { } p && _hoverDay is { } h && p != h)
        {
            // Hover-range preview: while a pick is in progress, tint the inclusive span between the
            // pending start and the hovered day (whichever order the user is dragging in) with a
            // lighter tint than the committed-range band above, so it reads as tentative.
            var lo = p < h ? p : h;
            var hi = p < h ? h : p;
            if (day == lo) cls += " wss-picker-cell-preview-start";
            else if (day == hi) cls += " wss-picker-cell-preview-end";
            else if (day > lo && day < hi) cls += " wss-picker-cell-preview";
        }
        return cls;
    }

    // Whether `day` is today -- the visual channel is wss-picker-day-today (see DayClass/
    // SessionDayClass), and the accessibility channel is aria-current="date" on the day button (see the
    // .razor markup), matching what the Month/Quarter/Year grids already expose inline.
    static bool IsToday(DateTime day) => day == DateTime.Today;

    string DayClass(DateTime day, DateTime month)
    {
        var cls = "wss-picker-day";
        if (day.Month != month.Month) cls += " wss-picker-day-outside";
        if (IsToday(day)) cls += " wss-picker-day-today";
        // Week mode suppresses the single-day selected look -- the ROW is the selection unit there
        // (see WeekRowClass/IsWeekRowEndpoint), mirroring DatePicker.DayClass's own suppression.
        if (EffectiveMode != DatePickerMode.Week && IsEndpoint(day)) cls += " wss-picker-day-selected";
        return cls;
    }

    // Week mode's row-wide "is this row an endpoint" check -- every day sharing a week with the
    // start or end endpoint is aria-selected, not just the single day that happens to equal the week
    // start itself, mirroring DatePicker.IsDaySelected one level up (day -> row).
    bool IsWeekRowEndpoint(DateTime weekStart)
    {
        var (s, e) = DisplayRange;
        return weekStart == s || weekStart == e;
    }

    // Week mode's row-level equivalent of CellClass -- the range/hover-preview band paints the
    // WHOLE row instead of individual cells (a week row has no gap between its own 7 day cells for
    // a continuous band to bridge, unlike Month/Quarter/Year's gapped grid, but a per-cell band
    // would still only touch the single day equal to a week-start endpoint -- see CellClass's own
    // bypass above). Endpoint rows and rows strictly between them get distinct classes so a
    // consumer/test can tell them apart, mirroring CellClass's range-start/range-end/in-range split
    // and UnitBtnClass's committed-vs-hover-preview split.
    string WeekRowClass(DateTime weekStart)
    {
        var cls = "wss-picker-week-row";
        if (EffectiveMode != DatePickerMode.Week) return cls; // ShowWeekNumbers-only: no range styling
        var (s, e) = DisplayRange;
        if (s is { } a && e is { } b && a != b)
        {
            if (weekStart == a) cls += " wss-picker-week-row-start";
            else if (weekStart == b) cls += " wss-picker-week-row-end";
            else if (weekStart > a && weekStart < b) cls += " wss-picker-week-row-in-range";
        }
        else if (_selecting && _pendingStart is { } p && _hoverDay is { } h && p != h)
        {
            var lo = p < h ? p : h;
            var hi = p < h ? h : p;
            if (weekStart == lo) cls += " wss-picker-week-row-preview-start";
            else if (weekStart == hi) cls += " wss-picker-week-row-preview-end";
            else if (weekStart > lo && weekStart < hi) cls += " wss-picker-week-row-preview";
        }
        return cls;
    }

    // Composes the wss-picker-month-btn classes for Month/Quarter/Year range mode -- the same
    // range/preview semantics as CellClass+DayClass above, but painted directly on the button (no
    // wrapping cell div and no half-inset endpoint split) since the month/quarter/year grid's own
    // 8px gap between cells means a continuous cell-spanning band wouldn't visually connect anyway
    // -- unlike the day grid's edge-to-edge cells. The endpoint's filled/primary look used to come
    // from the button's own aria-pressed="true"; that state moved to aria-selected on the enclosing
    // role="gridcell" (aria-selected is not valid on role="button", and an APG grid puts selection on
    // the cell), so the endpoint now carries an explicit modifier class instead -- mirroring
    // wss-picker-day-selected, which the day grid has always used for exactly this reason.
    string UnitBtnClass(DateTime unit, bool outside = false)
    {
        var cls = outside ? "wss-picker-month-btn wss-picker-month-btn-outside" : "wss-picker-month-btn";
        if (IsEndpoint(unit)) cls += " wss-picker-month-btn-selected";
        var (s, e) = DisplayRange;
        if (s is { } a && e is { } b && a != b)
        {
            if (unit > a && unit < b) cls += " wss-picker-month-btn-in-range";
        }
        else if (_selecting && _pendingStart is { } p && _hoverDay is { } h && p != h)
        {
            var lo = p < h ? p : h;
            var hi = p < h ? h : p;
            if (unit >= lo && unit <= hi && unit != p) cls += " wss-picker-month-btn-preview";
        }
        return cls;
    }

    // The five Min/Max/DisabledDate predicates, each binding this instance's own parameters to the
    // matching PickerMath helper -- see those for the per-granularity contracts (DisabledDate is
    // folded into every one of them, so the cell `aria-disabled` attributes, the click guards that
    // back them, the DefaultFocus*/FirstEnabled* skip logic and IsUnitDisabled's typed-text/preset
    // commit guards can never disagree about what counts as disabled). They were character-identical
    // to DatePicker's own.
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

    // Dispatches to the Mode-appropriate disabled check -- shared by the grid `aria-disabled`
    // attributes, the unit-click guard,
    // the DefaultFocus*/FirstEnabled* skip logic, and the typed-text commit guard, so they can never
    // disagree about what counts as disabled. Week's own day CELLS deliberately stay on the default
    // (day-granularity IsDayDisabled) arm below -- only the typed-text commit guard (which receives
    // an already-week-normalized unit from TryParseDate) needs the week-granularity check, so it's
    // called out explicitly.
    bool IsUnitDisabled(DateTime unit) => EffectiveMode switch
    {
        DatePickerMode.Month => IsMonthDisabled(unit),
        DatePickerMode.Quarter => IsQuarterDisabled(unit),
        DatePickerMode.Year => IsYearDisabled(unit),
        DatePickerMode.Week => IsWeekDisabledForCommit(unit),
        _ => IsDayDisabled(unit),
    };

    // ----- Time/DateTime pick session: disabled checks ------------------------------------------
    // A SEPARATE dispatcher from IsUnitDisabled above (which stays exactly as-is for the dual-panel
    // modes it already served) -- DateTime/Time need the raw Mode, not EffectiveMode's Date fold:
    // IsUnitDisabled's default arm would call IsDayDisabled(unit) with `unit` still carrying its
    // time-of-day, and a Max exactly on a day would then spuriously disable that whole day (any
    // nonzero time of day compares greater than Max.Date's own midnight) -- mirrors
    // DatePicker.IsDisabledForCommit's identical `IsDayDisabled(value.Date)` split. Time mode ignores
    // Min/Max/DisabledDate entirely (no date-range concept -- mirrors DatePicker.IsDisabledForCommit's
    // Time arm) and only guards the per-endpoint time-of-day.
    bool IsCommitDisabled(int endpoint, DateTime value) => Mode switch
    {
        DatePickerMode.DateTime => IsDayDisabled(value.Date) || IsEndpointTimeDisabled(endpoint, value),
        DatePickerMode.Time => IsEndpointTimeDisabled(endpoint, value),
        _ => IsUnitDisabled(value),
    };

    // Whether `value`'s time-of-day hits a disabled hour/minute/second for the given endpoint (0 =
    // start, using StartDisabledTime; 1 = end, using EndDisabledTime) -- evaluated against `value`'s
    // own date part, mirroring DatePicker.IsTimeDisabledForCommit's identical single-Value check, one
    // endpoint at a time. Null callback / null return / null lists = nothing disabled.
    bool IsEndpointTimeDisabled(int endpoint, DateTime value)
    {
        var parts = (endpoint == 0 ? StartDisabledTime : EndDisabledTime)?.Invoke(value.Date);
        return parts is not null &&
            (PickerMath.IsTimePartDisabled(parts.Hours, value.Hour) ||
             PickerMath.IsTimePartDisabled(parts.Minutes, value.Minute) ||
             PickerMath.IsTimePartDisabled(parts.Seconds, value.Second));
    }

    // PickerCulture lives on PickerBase (shared with DatePicker).

    string MonthName(int month) => PickerMath.MonthName(PickerCulture, month);

    // The years offered by a panel's year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — see PickerMath.YearRange for the full contract (including the [1, 9999]
    // clamp -- OnYearSelectChanged/OnRangeYearSelectChanged apply the matching clamp to the value
    // actually selected).
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

    // Whether a panel's day grid renders as 6 week-number rows (Mode.Week always; ShowWeekNumbers's
    // column in Date mode and its DateTime/Time fallback) instead of the flat 42-cell layout --
    // mirrors DatePicker.ShowWeekRows. Only the day-grid ("else") branch of the .razor markup ever
    // checks this -- Month/Quarter/Year have their own branches.
    bool ShowWeekRows => EffectiveMode == DatePickerMode.Week || ShowWeekNumbers;

    // GridDays(month) grouped into 6 rows of 7 -- used by the week-rows layout. Each row's first
    // entry is that row's own week start (GridDays begins on a week boundary and advances a whole
    // week at a time), which the markup reuses directly for the row's week-number cell and
    // WeekRowClass's range/preview check -- mirrors DatePicker.GridWeekRows.
    DateTime[][] GridWeekRows(DateTime month) => [.. GridDays(month).Chunk(7)];

    // The ISO-ish week number of the calendar week starting on `weekStart` -- mirrors
    // DatePicker.WeekNumberOf.
    int WeekNumberOf(DateTime weekStart) => PickerMath.WeekNumberOf(weekStart, PickerCulture, EffectiveFirstDayOfWeek);

    // FormatDate (Quarter's/Week's hand-rolled displays included) and TryParseDate (their inverse
    // shorthands included) live on PickerBase, over the EffectiveMode/ExplicitFormat/EffectiveFormat/
    // EffectiveFirstDayOfWeek/NormalizeForMode hooks this class supplies -- they were character-
    // identical to DatePicker's own apart from which mode each keys off (EffectiveMode's Date fold
    // here, the raw Mode there).

    // Central per-mode normalization, shared by PickerBase.TryParseDate and SetRangeAsync so every
    // commit path (click, typed text, select change, preset) agrees on the same shape of value. DateTime/Time
    // both route through PickerMath's DateTime-shaped case (preserve date+time, zero the second when
    // !ShowSeconds) rather than its OWN Time case (which re-stamps to the LITERAL current day,
    // discarding whatever date was composed): the per-endpoint date resolution ("this endpoint's
    // existing committed date, or today when unset") already happens at the compose step
    // (this class's ApplyTimePartAsync override / TryParseTimePart), mirroring DatePicker's own
    // `Value?.Date ?? DateTime.Today` one endpoint at a time -- by the time a value reaches here its
    // date is already correct, so re-stamping it here (as PickerMath's Time case does for the
    // single-Value DatePicker) would discard that per-endpoint resolution instead of preserving it.
    internal override DateTime NormalizeForMode(DateTime value) => PickerMath.NormalizeForMode(
        Mode is DatePickerMode.DateTime or DatePickerMode.Time ? DatePickerMode.DateTime : EffectiveMode,
        EffectiveFirstDayOfWeek, ShowSeconds, value);

    // Time mode's typed-text parse: extracts just the time-of-day from the general parse (.NET's own
    // default-date fallback for a bare time string fills in TODAY, which is exactly wrong here -- see
    // the doc comment below) and composes it against `existingValue`'s own date (or today when
    // null) -- mirrors PickerMath.ComposeTimePart's identical existing-date-or-today resolution (which
    // this class's ApplyTimePartAsync override uses), but for a typed commit instead of a time-select
    // change. Unlike DatePicker's own Time mode (which always re-stamps to the literal current day on
    // every commit, single-Value), each ENDPOINT here keeps its own date across a typed time-only
    // commit -- see NormalizeForMode's doc comment for why routing through PickerMath's DateTime case
    // (not its Time case) is what makes that possible.
    bool TryParseTimePart(string text, DateTime? existingValue, out DateTime value)
    {
        if (DateTime.TryParseExact(text, EffectiveFormat, PickerCulture, DateTimeStyles.None, out var parsed) ||
            DateTime.TryParse(text, PickerCulture, DateTimeStyles.None, out parsed))
        {
            value = NormalizeForMode((existingValue?.Date ?? DateTime.Today) + parsed.TimeOfDay);
            return true;
        }
        value = default;
        return false;
    }

    static DateTime FirstOfMonth(DateTime value) => PickerMath.FirstOfMonth(value);
    static DateTime FirstOfYear(DateTime value) => PickerMath.FirstOfYear(value);
    static int QuarterOf(DateTime value) => PickerMath.QuarterOf(value);
    static DateTime QuarterStart(int year, int quarter) => PickerMath.QuarterStart(year, quarter);
    static DateTime QuarterStart(DateTime value) => PickerMath.QuarterStart(value);

    // The left panel's month, clamped so the +1-month right panel and the 42-cell grids can never
    // overflow DateTime's range (offsetMonths carries the panel adjustment through the clamp) — see
    // PickerMath.ClampView (this is the superset signature; DatePicker calls it with offset 0).
    static DateTime ClampView(DateTime firstOfMonth, int offsetMonths = 0) => PickerMath.ClampView(firstOfMonth, offsetMonths);

    static int ClampDecadeStartForRange(int year) => PickerMath.ClampDecadeStartForRange(year);

    // Clamps a single day into [Min, Max] (each bound applied only when set). Used only by the
    // (day-granularity) preset clamp -- see OnPresetClickAsync's doc comment.
    DateTime ClampToMinMax(DateTime day)
    {
        if (Min is { } min && day < min.Date) day = min.Date;
        if (Max is { } max && day > max.Date) day = max.Date;
        return day;
    }

    // ----- Mode.Month/Quarter: left/right year -------------------------------
    // _viewMonth.Year is the left panel's year in these two modes; the right panel is always +1.
    // Clamped to [1, 9998] so the right panel's year (LeftYear + 1) can never exceed 9999.

    int LeftYear
    {
        get => Math.Clamp(_viewMonth.Year, 1, 9998);
        set => _viewMonth = new DateTime(Math.Clamp(value, 1, 9998), _viewMonth.Month, 1);
    }

    int RightYear => LeftYear + 1;

    // ----- Mode.Year: left/right decade ---------------------------------------
    // _viewMonth.Year floors (via ClampDecadeStartForRange) to the left panel's decade start; the
    // right panel's decade is always +10.

    int LeftDecadeStart
    {
        get => ClampDecadeStartForRange(_viewMonth.Year);
        set => _viewMonth = new DateTime(ClampDecadeStartForRange(value), _viewMonth.Month, 1);
    }

    int RightDecadeStart => LeftDecadeStart + 10;

    string DecadeLabelFor(int decadeStart) => $"{decadeStart.ToString(PickerCulture)}-{(decadeStart + 9).ToString(PickerCulture)}";

    // ----- Roving-tabindex keyboard navigation (Mode.Date and its DateTime/Time/Week fallback) ----

    // Is `day` inside either currently displayed month? (DateRangePicker shows two, consecutive.)
    bool IsVisible(DateTime day)
    {
        var month = FirstOfMonth(day);
        return month == _viewMonth || month == _viewMonth.AddMonths(1);
    }

    // Dispatches to the Mode-appropriate "is this unit inside either currently displayed panel"
    // check -- shared by EffectiveFocusUnit and the Default/FirstEnabled fallback chains.
    bool IsVisibleUnit(DateTime unit) => EffectiveMode switch
    {
        DatePickerMode.Month or DatePickerMode.Quarter => unit.Year == LeftYear || unit.Year == RightYear,
        DatePickerMode.Year => unit.Year >= LeftDecadeStart - 1 && unit.Year <= RightDecadeStart + 10,
        _ => IsVisible(unit),
    };

    // The day the grids' roving tabindex targets when no keyboard navigation has moved it yet (or
    // the last-moved day scrolled out of view via the month/year selects or the nav buttons):
    // whichever endpoint of the displayed range (or the in-progress pending start) is visible, else
    // today if visible, else the 1st of the left panel's month — mirrors AntD's default focus.
    DateTime DefaultFocusDay()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisible(start) && !IsDayDisabled(start)) return start;
        if (e is { } end && IsVisible(end) && !IsDayDisabled(end)) return end;
        if (IsVisible(DateTime.Today) && !IsDayDisabled(DateTime.Today)) return DateTime.Today;
        // No natural candidate is usable (disabled — e.g. Min in the future with nothing set yet).
        // Land on the first enabled day across either panel instead (left panel checked first); if
        // both panels are entirely disabled there's nothing actionable in either, so any deterministic
        // in-month day is fine. This skip used to be load-bearing for keyboard REACHABILITY (with
        // natively `disabled` cells a tabindex="0" that was also disabled gave the grids zero tab
        // stops); disabled cells are now aria-disabled and stay focusable, so this is purely about
        // where focus LANDS -- opening on something the user can actually pick. Kept for that reason.
        return FirstEnabledDay(_viewMonth) ?? FirstEnabledDay(_viewMonth.AddMonths(1)) ?? _viewMonth;
    }

    // The first enabled, in-month day in `month`'s grid, or null if every in-month day is disabled --
    // see PickerMath.FirstEnabledDay (shared verbatim with DatePicker; the callers above pass each
    // panel's month in turn).
    DateTime? FirstEnabledDay(DateTime month) =>
        PickerMath.FirstEnabledDay(month, EffectiveFirstDayOfWeek, Min, Max, DisabledDate);

    // _focusDay once a keyboard move has set it, but only while it's still on-screen — a month/year
    // select change (or a nav button) clears _focusDay explicitly, but this guard also covers any
    // path that doesn't, so the grids are never left with zero tabbable cells.
    DateTime EffectiveFocusDay => _focusDay is { } f && IsVisible(f) ? f : DefaultFocusDay();

    // True for the one day button (across both grids) that carries tabindex="0" — the in-month
    // rendering of EffectiveFocusDay in the given panel's month. (A leading/trailing adjacent-month
    // cell showing the same date never wins: day.Month/Year must match the grid's own month, and
    // EffectiveFocusDay's month matches at most one of the two panels.)
    bool IsFocusStop(DateTime day, DateTime month) =>
        day.Month == month.Month && day.Year == month.Year && day == EffectiveFocusDay;

    // Maps a keydown's Key to the day it should move focus to, or null when the key isn't a
    // navigation key -- see PickerMath.NextFocusDay for the arrow/Home/End/PageUp/PageDown map and
    // its edge-of-range try/catch.
    DateTime? NextFocusDay(DateTime current, string key) => PickerMath.NextFocusDay(current, key, EffectiveFirstDayOfWeek);

    // Grid keydown (wired to both panels' grids): moves the roving-tabindex day, retargeting
    // _viewMonth (the left panel — the right is always _viewMonth + 1) only when navigation lands
    // outside BOTH currently visible months (so a move that's already covered by the other panel
    // doesn't needlessly re-anchor the view); see the branch below for which panel absorbs the new
    // month on a crossing. A day that lands disabled (Min/Max/DisabledDate) still becomes the focus
    // target -- the APG grid behavior, and what lets Left/Right keep stepping day-by-day THROUGH a
    // disabled run instead of jumping it. That is only safe because a rejected day now renders
    // aria-disabled rather than natively `disabled` (see RangeDayButtonFragment): it stays focusable,
    // keeps the grids' single tab stop real, and can't blur focus to <body> when a view-crossing
    // re-render lands the roving stop on it. Activation is blocked by OnGridDayClickAsync's own guard,
    // not by the browser, so Enter/Space on a focused disabled cell no-ops too.
    // The actual DOM focus move (needed whenever a grid re-renders with new
    // button instances, i.e. any view change) happens in OnAfterRenderAsync via _pendingFocusDate.
    // wss-picker.js suppresses the browser's native scroll for these keys when JS is available;
    // without it this state still updates, just without the DOM focus follow or scroll suppression.
    void OnGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusDay(EffectiveFocusDay, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        if (!IsVisible(next.Value))
        {
            // A crossing that lands past the right panel should anchor the RIGHT panel on the new
            // month (offset -1, the same trick CommitEndTextAsync uses), so a one-day move past the
            // end of the right panel is a one-month view shift — not two. A crossing that lands
            // before the left panel anchors the left panel directly, as before (offset 0).
            var forward = FirstOfMonth(next.Value) > _viewMonth.AddMonths(1);
            _viewMonth = forward
                ? ClampView(FirstOfMonth(next.Value), -1)
                : ClampView(FirstOfMonth(next.Value));
        }
        _pendingFocusDate = next.Value;
    }

    // ----- Prev/next month navigation (Mode.Date) -----------------------------

    // Disables at the representable DateTime range (ClampView) as before, and now also at the
    // Min/Max month — checked against whichever panel the button is adjacent to (prev sits on the
    // left panel, next on the right): prev stops once the left panel is already on Min's month, next
    // stops once the right panel is already on Max's month. Same month-level granularity YearRange
    // uses for each panel's year select, so the header mechanisms never disagree.
    bool PrevMonthDisabled =>
        ClampView(_viewMonth.AddMonths(-1)) == _viewMonth ||
        (Min is { } min && _viewMonth <= FirstOfMonth(min));
    bool NextMonthDisabled =>
        ClampView(_viewMonth.AddMonths(1)) == _viewMonth ||
        (Max is { } max && _viewMonth.AddMonths(1) >= FirstOfMonth(max));

    void PrevMonth()
    {
        _viewMonth = ClampView(_viewMonth.AddMonths(-1));
        _focusDay = null; // recompute the roving-focus default against the newly shown months
    }

    void NextMonth()
    {
        _viewMonth = ClampView(_viewMonth.AddMonths(1));
        _focusDay = null;
    }

    // ----- Time/DateTime pick session: single-panel prev/next-disabled -------
    // PrevMonthDisabled/NextMonthDisabled above assume _viewMonth is the LEFT of a pair (Next stops
    // one month early so the +1 right panel doesn't cross Max) -- wrong for this session's own lone
    // panel, which shows exactly _viewMonth with no partner. Mirrors DatePicker.PrevMonthDisabled/
    // NextMonthDisabled (also a single panel) instead. PrevMonth()/NextMonth() themselves are reused
    // verbatim -- they only ever shift _viewMonth, with no pair-aware logic to get wrong.
    bool SessionPrevMonthDisabled =>
        ClampView(_viewMonth.AddMonths(-1)) == _viewMonth ||
        (Min is { } min && _viewMonth <= FirstOfMonth(min));
    bool SessionNextMonthDisabled =>
        ClampView(_viewMonth.AddMonths(1)) == _viewMonth ||
        (Max is { } max && _viewMonth >= FirstOfMonth(max));

    // ----- Time/DateTime pick session: single-panel day-grid focus -----------
    // Mirrors DatePicker's own IsVisible/DefaultFocusDay/EffectiveFocusDay/IsFocusStop/OnGridKeyDown
    // one-for-one, but scoped to the ONE panel this session's calendar shows. The dual-panel
    // IsVisible/DefaultFocusDay/EffectiveFocusDay/IsFocusStop/OnGridKeyDown above all check BOTH
    // _viewMonth and _viewMonth.AddMonths(1) (there being two panels) -- reusing them here would let
    // the roving tabindex land on a day this single panel never renders (the "next" month), leaving
    // the grid with zero tabbable cells (a keyboard trap), so this session gets its own single-month
    // versions instead.

    bool IsSessionVisible(DateTime day) => FirstOfMonth(day) == _viewMonth;

    DateTime SessionDefaultFocusDay()
    {
        if (ActiveSessionValue is { } active && IsSessionVisible(active.Date) && !IsDayDisabled(active.Date)) return active.Date;
        if (IsSessionVisible(DateTime.Today) && !IsDayDisabled(DateTime.Today)) return DateTime.Today;
        return FirstEnabledDay(_viewMonth) ?? _viewMonth;
    }

    DateTime SessionEffectiveFocusDay => _focusDay is { } f && IsSessionVisible(f) ? f : SessionDefaultFocusDay();

    bool IsSessionFocusStop(DateTime day) =>
        day.Month == _viewMonth.Month && day.Year == _viewMonth.Year && day == SessionEffectiveFocusDay;

    void OnSessionGridKeyDown(KeyboardEventArgs e)
    {
        var next = NextFocusDay(SessionEffectiveFocusDay, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        var nextMonth = FirstOfMonth(next.Value);
        if (nextMonth != _viewMonth) _viewMonth = ClampView(nextMonth);
        _pendingFocusDate = next.Value;
    }

    // ----- Mode.Month/Quarter: prev/next year navigation ----------------------

    bool PrevYearDisabled =>
        LeftYear <= 1 || (Min is { } min && LeftYear <= min.Year);
    bool NextYearDisabled =>
        RightYear >= 9999 || (Max is { } max && RightYear >= max.Year);

    void PrevYear()
    {
        LeftYear -= 1;
        _focusDay = null;
    }

    void NextYear()
    {
        LeftYear += 1;
        _focusDay = null;
    }

    void OnRangeYearSelectChanged(int panel, ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return;
        LeftYear = year - panel;
        _focusDay = null;
    }

    // ----- Mode.Year: prev/next decade navigation -----------------------------

    bool PrevDecadeDisabled =>
        ClampDecadeStartForRange(LeftDecadeStart - 10) == LeftDecadeStart ||
        (Min is { } min && LeftDecadeStart <= ClampDecadeStartForRange(min.Year));
    bool NextDecadeDisabled =>
        ClampDecadeStartForRange(LeftDecadeStart + 10) == LeftDecadeStart ||
        (Max is { } max && RightDecadeStart >= ClampDecadeStartForRange(max.Year));

    void PrevDecade()
    {
        LeftDecadeStart -= 10;
        _focusDay = null;
    }

    void NextDecade()
    {
        LeftDecadeStart += 10;
        _focusDay = null;
    }

    // ----- Mode.Month/Quarter/Year: shared default-focus/roving-tabindex machinery -----

    DateTime TodayUnit() => EffectiveMode switch
    {
        DatePickerMode.Month => FirstOfMonth(DateTime.Today),
        DatePickerMode.Quarter => QuarterStart(DateTime.Today),
        DatePickerMode.Year => FirstOfYear(DateTime.Today),
        _ => DateTime.Today,
    };

    DateTime? FirstEnabledMonth(int year) => PickerMath.FirstEnabledMonth(year, Min, Max, DisabledDate);

    DateTime DefaultFocusMonth()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisibleUnit(start) && !IsMonthDisabled(start)) return start;
        if (e is { } end && IsVisibleUnit(end) && !IsMonthDisabled(end)) return end;
        var today = FirstOfMonth(DateTime.Today);
        if (IsVisibleUnit(today) && !IsMonthDisabled(today)) return today;
        return FirstEnabledMonth(LeftYear) ?? FirstEnabledMonth(RightYear) ?? new DateTime(LeftYear, 1, 1);
    }

    DateTime? FirstEnabledQuarter(int year) => PickerMath.FirstEnabledQuarter(year, Min, Max, DisabledDate);

    DateTime DefaultFocusQuarter()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisibleUnit(start) && !IsQuarterDisabled(start)) return start;
        if (e is { } end && IsVisibleUnit(end) && !IsQuarterDisabled(end)) return end;
        var today = QuarterStart(DateTime.Today);
        if (IsVisibleUnit(today) && !IsQuarterDisabled(today)) return today;
        return FirstEnabledQuarter(LeftYear) ?? FirstEnabledQuarter(RightYear) ?? QuarterStart(LeftYear, 1);
    }

    // Only scans the decade's own 10 real years (never the two dimmed adjacent-decade cells) -- see
    // PickerMath.FirstEnabledYear; the callers below pass each panel's own decade in turn.
    DateTime? FirstEnabledYear(int decadeStart) =>
        PickerMath.FirstEnabledYear(decadeStart, Min, Max, DisabledDate);

    DateTime DefaultFocusYear()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisibleUnit(start) && !IsYearDisabled(start)) return start;
        if (e is { } end && IsVisibleUnit(end) && !IsYearDisabled(end)) return end;
        var today = FirstOfYear(DateTime.Today);
        if (IsVisibleUnit(today) && !IsYearDisabled(today)) return today;
        return FirstEnabledYear(LeftDecadeStart) ?? FirstEnabledYear(RightDecadeStart) ?? new DateTime(LeftDecadeStart, 1, 1);
    }

    // The unit the grids' roving tabindex targets -- dispatches to the Mode-appropriate
    // Default/Effective pair (mirrors EffectiveFocusDay for Mode.Date's own day grid).
    DateTime EffectiveFocusUnit => EffectiveMode switch
    {
        DatePickerMode.Month => _focusDay is { } fm && IsVisibleUnit(fm) ? fm : DefaultFocusMonth(),
        DatePickerMode.Quarter => _focusDay is { } fq && IsVisibleUnit(fq) ? fq : DefaultFocusQuarter(),
        DatePickerMode.Year => _focusDay is { } fy && IsVisibleUnit(fy) ? fy : DefaultFocusYear(),
        _ => EffectiveFocusDay,
    };

    bool IsMonthFocusStop(DateTime month) => month == EffectiveFocusUnit;

    bool IsQuarterFocusStop(int year, int quarter) => QuarterStart(year, quarter) == EffectiveFocusUnit;

    // The Year grid's two panels overlap by exactly the two years straddling their shared boundary
    // (LeftDecadeStart's own dimmed trailing cell == RightDecadeStart's own real first cell; and
    // RightDecadeStart's dimmed leading cell == LeftDecadeStart's own real last cell) -- both render
    // in BOTH panels, so the REAL (non-outside) occurrence always wins the roving tabindex; an
    // outside occurrence only wins when it has no real counterpart anywhere (the decade grid's own
    // two OUTERMOST dimmed cells, LeftDecadeStart-1 and RightDecadeStart+10).
    bool IsYearFocusStop(int year, bool outsideThisPanel)
    {
        if (new DateTime(year, 1, 1) != EffectiveFocusUnit) return false;
        return !outsideThisPanel || year == LeftDecadeStart - 1 || year == RightDecadeStart + 10;
    }

    // Grid keydown for Mode.Month: moves the roving-tabindex month, sliding the view by exactly one
    // year (mirrors OnGridKeyDown's "shift by one month, not two" rule) when navigation crosses
    // outside both currently visible years -- the largest single-key step (PageUp/PageDown, one
    // year) can only ever land one year beyond either edge, so a one-year slide always suffices to
    // bring it back into view.
    void OnMonthGridKeyDown(KeyboardEventArgs e)
    {
        var next = PickerMath.NextFocusMonth(EffectiveFocusUnit, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        if (next.Value.Year > RightYear) LeftYear += 1;
        else if (next.Value.Year < LeftYear) LeftYear -= 1;
        _pendingFocusDate = next.Value;
    }

    // Grid keydown for Mode.Quarter -- same one-year-slide crossing rule as OnMonthGridKeyDown.
    void OnQuarterGridKeyDown(KeyboardEventArgs e)
    {
        var next = PickerMath.NextFocusQuarter(EffectiveFocusUnit, e.Key);
        if (next is null) return;

        _focusDay = next.Value;
        if (next.Value.Year > RightYear) LeftYear += 1;
        else if (next.Value.Year < LeftYear) LeftYear -= 1;
        _pendingFocusDate = next.Value;
    }

    // Grid keydown for Mode.Year -- same crossing rule one granularity up: sliding the view by
    // exactly one decade always suffices, since the largest single-key step (PageUp/PageDown, 10
    // years) can only ever land one decade beyond either edge. Home/End's row-grouping context
    // (PickerMath.NextFocusYear's decadeStart parameter) picks whichever of the two panels'
    // decades the CURRENT focus is closer to, so a Home/End press while focus sits in the overlap
    // seam still gets a sensible row.
    void OnYearGridKeyDown(KeyboardEventArgs e)
    {
        var current = EffectiveFocusUnit;
        var decadeContext = current.Year >= LeftDecadeStart + 10 ? RightDecadeStart : LeftDecadeStart;
        var next = PickerMath.NextFocusYear(current, e.Key, decadeContext);
        if (next is null) return;

        _focusDay = next.Value;
        if (next.Value.Year > RightDecadeStart + 10) LeftDecadeStart += 10;
        else if (next.Value.Year < LeftDecadeStart - 1) LeftDecadeStart -= 10;
        _pendingFocusDate = next.Value;
    }

    // ----- View anchoring (used by Open/CommitStartTextAsync/CommitEndTextAsync) --------

    // Anchors `unit` into the given panel (0 = left, 1 = right), dispatching to the Mode-appropriate
    // state. Shared by Open() (anchor on Start/End/today) and the typed-text commit paths (re-anchor
    // on the parsed unit) so every "make this unit visible in this panel" call site agrees on the
    // same math -- the Date branch is exactly the pre-existing `ClampView(FirstOfMonth(unit),
    // -panel)` calls it replaces.
    void AnchorView(DateTime unit, int panel)
    {
        switch (EffectiveMode)
        {
            case DatePickerMode.Month:
            case DatePickerMode.Quarter:
                LeftYear = unit.Year - panel;
                break;
            case DatePickerMode.Year:
                LeftDecadeStart = ClampDecadeStartForRange(unit.Year) - panel * 10;
                break;
            default:
                _viewMonth = ClampView(FirstOfMonth(unit), -panel);
                break;
        }
    }

    // ----- Parameter reconciliation ------------------------------------------

    /// <summary>
    /// Two parameter-driven invariants, both about state this control holds that a changed parameter
    /// invalidates -- the range-picker twin of <c>DatePicker.OnParametersSetAsync</c>.
    /// <para>
    /// <b>Disabled =&gt; closed</b>, mirroring <c>Select.OnParametersSetAsync</c>'s identical
    /// invariant. Without it, a panel that was open when the consumer flipped <see cref="Disabled"/>
    /// stays fully interactive: every unit cell, preset, header select and the session OK button goes
    /// on committing through <see cref="SetRangeAsync"/> on a control the consumer has taken out of
    /// service (only the clear button, the field click and the input focus were ever guarded). Routed
    /// through the normal <see cref="CloseAsync"/> so the JS/focus teardown -- and the in-progress
    /// pick/session discard -- runs exactly as it does for a user-driven close.
    /// </para>
    /// <para>
    /// <b>An external <see cref="Start"/>/<see cref="End"/> change discards that side's half-typed
    /// text</b> (see <c>_lastStartParam</c>/<c>_lastEndParam</c>): the buffer belongs to the endpoint
    /// it was typed against, so a swapped bound record must not display -- or, on the next
    /// Enter/blur, commit -- the previous record's keystrokes. Per side, so an external change to one
    /// endpoint leaves the other's in-progress typing alone.
    /// </para>
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        if (Disabled && _open) await CloseAsync();

        if (Start != _lastStartParam)
        {
            _lastStartParam = Start;
            _startEdit = null;
        }
        if (End != _lastEndParam)
        {
            _lastEndParam = End;
            _endEdit = null;
        }
    }

    // ----- Interaction ------------------------------------------------------

    Task OnFieldClickAsync()
    {
        // A click on the field's non-input chrome starts at the start side. (A click on an input
        // already opened via its focus event with the right side active — this is then a no-op.)
        if (!Disabled && !_open)
        {
            _activeInput = 0;
            Open();
        }
        return Task.CompletedTask;
    }

    void OnInputFocus(int which)
    {
        _activeInput = which;
        if (_suppressOpenOnFocus) { _suppressOpenOnFocus = false; return; }
        if (!Disabled && !_open) Open();
    }

    // Callers own _activeInput (the field click and each input's focus set it before opening).
    // Anchors on the start of the current value; with only an end set, put that end in the right
    // panel so it's visible on open -- see AnchorView. IsSessionMode gets its own single-panel
    // anchor: AnchorView's -panel offset assumes a paired right panel one month ahead, which this
    // session's lone calendar doesn't have (Time mode has no calendar at all, so anchoring is
    // skipped entirely there).
    void Open()
    {
        _open = true;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _pendingSessionStart = null;
        _pendingSessionEnd = null;
        _startEdit = _endEdit = null;
        _focusDay = null;
        _pendingInputFocus = false;
        if (IsSessionMode)
        {
            if (Mode == DatePickerMode.DateTime)
            {
                var sessionAnchor = (_activeInput == 0 ? Start : End) ?? (_activeInput == 0 ? End : Start) ?? DefaultViewDate ?? DateTime.Today;
                _viewMonth = ClampView(FirstOfMonth(sessionAnchor));
            }
            return;
        }
        // DefaultViewDate (AntD's defaultPickerValue) only matters when NEITHER endpoint is set to
        // anchor on -- an already-set Start/End always wins, same precedence DatePicker.Open's
        // identical anchor uses.
        var anchor = Start ?? End ?? DefaultViewDate ?? DateTime.Today;
        AnchorView(anchor, Start is null && End is not null ? 1 : 0);
    }

    Task CloseAsync()
    {
        _open = false;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _pendingSessionStart = null;
        _pendingSessionEnd = null;
        _startEdit = _endEdit = null;
        _focusDay = null;
        _pendingFocusDate = null;
        // Give up the C#-owned open z-index on the logical close path (the OnAfterRender close
        // branch also nulls it and runs clearZ as the DOM-side teardown).
        _openZIndex = null;
        // No StateHasChanged: every caller is an event handler, after which Blazor re-renders.
        return Task.CompletedTask;
    }

    // Escape closes (discarding any in-progress pick/edit); Enter commits whichever input is being
    // typed in (its native form-submit default is suppressed by initPicker when JS is available).
    async Task OnWrapperKeyDownAsync(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Escape":
                // Reaching the wrapper's keydown at all means some descendant (an input, a unit
                // button, a select, a preset) had focus — restore it to the active input on close.
                if (_open)
                {
                    _pendingInputFocus = true;
                    await CloseAsync();
                }
                break;
            case "Enter":
                await CommitStartTextAsync();
                await CommitEndTextAsync();
                break;
        }
    }

    // The shared two-click range pick, used by the day/month/quarter/year grids alike: the first
    // click sets the pending start (and moves the active underline to the end input); the second
    // commits (swapping a backwards pick) and closes.
    //
    // The guard is real work now, not belt-and-braces: cells render aria-disabled rather than natively
    // `disabled` (they have to stay focusable -- see RangeDayButtonFragment), so the browser DOES
    // dispatch this click, and an Enter/Space on a keyboard-focused disabled cell synthesizes one too.
    // IsUnitDisabled is exactly the dispatcher each grid's own attribute renders from (Week's day
    // buttons excepted -- they're day-granularity, guarded one level up in OnGridDayClickAsync), so
    // the cell and the commit can never disagree.
    async Task OnUnitClickAsync(DateTime unit)
    {
        if (IsUnitDisabled(unit)) return;
        // A calendar pick supersedes any half-typed input text — drop it so the field previews
        // the pick instead of the stale keystrokes.
        _startEdit = _endEdit = null;

        if (!_selecting)
        {
            // First click starts a fresh range (matching AntD — the old range is replaced, not
            // extended) and moves the active underline to the end input.
            _selecting = true;
            _pendingStart = unit;
            _hoverDay = null;
            _activeInput = 1;
            _focusDay = unit;
            return;
        }

        var start = _pendingStart!.Value;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _focusDay = unit;
        await SetRangeAsync(start, unit, DateRangeEndpoints.Both);
        _pendingInputFocus = true; // the clicked button is about to unmount
        await CloseAsync();
    }

    // Week mode's day-grid click: the button stays at day granularity (IsDayDisabled(day), same as
    // every other mode's day cell), but the click's actual pending-start/commit unit is the week
    // START, not the clicked day -- mirrors DatePicker.OnDayClickAsync's Week guard. With Min/Max
    // alone the day-cell and week-start checks can never disagree (a week whose start/end falls
    // outside [Min, Max] means every day in it does too), but a future DisabledDate predicate could
    // reject a week start while leaving every individual day in that week enabled -- guarded here
    // explicitly so a click can't slip past it. The day-grid markup calls this in Week mode instead
    // of OnUnitClickAsync directly (see OnGridDayClickAsync).
    Task OnWeekDayClickAsync(DateTime day)
    {
        var weekStart = WeekStart(day);
        return IsWeekDisabledForCommit(weekStart) ? Task.CompletedTask : OnUnitClickAsync(weekStart);
    }

    // Shared by both day-grid layouts (flat and week-rows): resolves the clicked day to whatever
    // OnUnitClickAsync should actually receive -- the week START in Week mode (see
    // OnWeekDayClickAsync), or the day itself everywhere else (Date mode, including with
    // ShowWeekNumbers's column -- a day click there still commits that day, not its week).
    //
    // The DAY-granularity guard belongs here, not in OnUnitClickAsync: the day button renders
    // aria-disabled from IsDayDisabled (its own granularity) in EVERY mode, but in Week mode the unit
    // that reaches OnUnitClickAsync is the week START, whose own IsUnitDisabled answer can differ
    // (a DisabledDate predicate can reject one and not the other). Guarding here keeps the button's
    // rendered state and the click honest at the granularity the button was actually rendered at,
    // while OnUnitClickAsync's guard covers the unit that is about to be committed.
    Task OnGridDayClickAsync(DateTime day) =>
        IsDayDisabled(day) ? Task.CompletedTask
            : EffectiveMode == DatePickerMode.Week ? OnWeekDayClickAsync(day) : OnUnitClickAsync(day);

    // Hover-range preview: only tracked while a pick is in progress, so hovering the other 83 cells
    // of an idle grid never triggers a render.
    void OnUnitPointerEnter(DateTime unit)
    {
        if (!_selecting || _hoverDay == unit) return;
        _hoverDay = unit;
    }

    // Week mode's pointer-enter equivalent of OnGridDayClickAsync -- tracks the hovered WEEK START
    // (so WeekRowClass's preview comparison stays at the same granularity as _pendingStart), not the
    // hovered day itself.
    void OnGridDayPointerEnter(DateTime day) =>
        OnUnitPointerEnter(EffectiveMode == DatePickerMode.Week ? WeekStart(day) : day);

    void OnGridPointerLeave()
    {
        if (_hoverDay is not null) _hoverDay = null;
    }

    // A preset click (any Mode): resolve, clamp both ends into [Min, Max] at DAY granularity (so a
    // preset can never commit days the calendar itself would disable at that finer grain), then
    // hand off to SetRangeAsync, which normalizes to Mode's own granularity centrally -- Min/Max
    // alone a preset never rejects; it always clamps to something committable (matching the existing
    // Preset_entirely_past_max/before_min clamp tests). DisabledDate is a different story: an
    // arbitrary predicate can still reject the clamped+normalized result even though it's inside
    // [Min, Max], so the FINAL normalized endpoints (mirroring what SetRangeAsync itself would
    // produce, swap included) are guarded via IsUnitDisabled before committing -- a rejection no-ops
    // instead of committing, mirroring DatePicker.OnPresetClickAsync's own guard.
    async Task OnPresetClickAsync(DateRangePreset preset)
    {
        _startEdit = _endEdit = null;
        var (start, end) = preset.Resolve();

        // Time-of-day matters for DateTime/Time -- unlike every other mode's plain dates, truncating
        // to .Date below would silently discard a resolved time (e.g. a "Last 3 hours" preset).
        // These two modes also skip the day-granularity Min/Max clamp that follows: NormalizeForMode
        // already preserves date+time for them (see its own doc comment), mirroring
        // DatePicker.OnPresetClickAsync's own no-clamp, guard-only shape for every one of its modes --
        // the guard itself becomes IsCommitDisabled (which understands DisabledTime/the time-of-day),
        // not IsUnitDisabled (which folds both into a day-only check via EffectiveMode).
        var isTimeAware = Mode is DatePickerMode.DateTime or DatePickerMode.Time;
        if (!isTimeAware)
        {
            start = start.Date;
            end = end.Date;
        }
        if (end < start) (start, end) = (end, start);
        if (!isTimeAware)
        {
            start = ClampToMinMax(start);
            end = ClampToMinMax(end);
            if (end < start) end = start;
        }

        var normalizedStart = NormalizeForMode(start);
        var normalizedEnd = NormalizeForMode(end);
        if (normalizedEnd < normalizedStart) (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        var disabled = isTimeAware
            ? IsCommitDisabled(0, normalizedStart) || IsCommitDisabled(1, normalizedEnd)
            : IsUnitDisabled(normalizedStart) || IsUnitDisabled(normalizedEnd);
        if (disabled) return;

        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        await SetRangeAsync(start, end, DateRangeEndpoints.Both);
        _pendingInputFocus = true; // the clicked preset button is about to unmount
        await CloseAsync();
    }

    // The clear button unmounts the instant both values go (ShowClear turns false), so without the
    // reclaim below DOM focus fell to <body> -- and the panel is open essentially always (a field
    // opens on focus), which put Escape out of reach of the wrapper's own keydown and left a live
    // role="dialog" behind a full-viewport backdrop that only a mouse could dismiss. Every other
    // panel action that unmounts its own trigger already sets this; the clear was the one that
    // didn't. Reclaiming focus also announces the now-empty field on arrival, which is the clear's
    // own confirmation.
    async Task ClearAsync()
    {
        if (Disabled) return;
        _pendingInputFocus = true;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _pendingSessionStart = null;
        _pendingSessionEnd = null;
        _startEdit = _endEdit = null;
        await SetRangeAsync(null, null, DateRangeEndpoints.Both);
    }

    // Typed input keeps the SAME per-endpoint immediate-commit route in every mode, including
    // DateTime/Time -- Enter/blur parses and commits that one endpoint right away via SetRangeAsync,
    // discarding any in-progress pick SESSION exactly like FinishTextCommit already discards the
    // date-only two-click flow's own pending state (see its doc comment). Time mode's own parse
    // (TryParseTimePart) is a genuinely different shape (time-only text, existing-date-or-today
    // resolution) from the generic TryParseDate path every other mode (DateTime included -- its
    // typed text already carries a full date+time) shares.
    async Task CommitStartTextAsync()
    {
        if (_startEdit is null) return;
        var text = _startEdit.Trim();
        _startEdit = null;
        if (text.Length == 0)
        {
            if (Start is not null) await SetRangeAsync(null, End, DateRangeEndpoints.Start);
            FinishTextCommit();
            return;
        }
        if (Mode == DatePickerMode.Time)
        {
            if (!TryParseTimePart(text, Start, out var time))
            {
                await RaiseTextErrorAsync(OnStartParseError, text);
                return;
            }
            if (IsCommitDisabled(0, time))
            {
                await RaiseTextErrorAsync(OnStartRangeError, text);
                return;
            }
            await SetRangeAsync(time, End, DateRangeEndpoints.Start);
            FinishTextCommit();
            return;
        }
        // Invalid or out-of-range text reverts to the formatted bound value (edit state cleared above).
        if (!TryParseDate(text, out var unit))
        {
            await RaiseTextErrorAsync(OnStartParseError, text);
            return;
        }
        // A well-formed value this picker won't accept -- distinct from the parse failure above, and
        // (until OnStartRangeError existed) a completely silent revert: neither bound value changed,
        // so no host form control ever learned the entry was refused. See OnStartRangeError.
        if (Mode == DatePickerMode.DateTime ? IsCommitDisabled(0, unit) : IsUnitDisabled(unit))
        {
            await RaiseTextErrorAsync(OnStartRangeError, text);
            return;
        }
        await SetRangeAsync(unit, End, DateRangeEndpoints.Start);
        if (Mode == DatePickerMode.DateTime) _viewMonth = ClampView(FirstOfMonth(unit)); else AnchorView(unit, 0);
        FinishTextCommit();
    }

    async Task CommitEndTextAsync()
    {
        if (_endEdit is null) return;
        var text = _endEdit.Trim();
        _endEdit = null;
        if (text.Length == 0)
        {
            if (End is not null) await SetRangeAsync(Start, null, DateRangeEndpoints.End);
            FinishTextCommit();
            return;
        }
        if (Mode == DatePickerMode.Time)
        {
            if (!TryParseTimePart(text, End, out var time))
            {
                await RaiseTextErrorAsync(OnEndParseError, text);
                return;
            }
            if (IsCommitDisabled(1, time))
            {
                await RaiseTextErrorAsync(OnEndRangeError, text);
                return;
            }
            await SetRangeAsync(Start, time, DateRangeEndpoints.End);
            FinishTextCommit();
            return;
        }
        if (!TryParseDate(text, out var unit))
        {
            await RaiseTextErrorAsync(OnEndParseError, text);
            return;
        }
        // See CommitStartTextAsync's twin -- a refused-but-well-formed value, announced through its
        // own callback rather than reverting in silence.
        if (Mode == DatePickerMode.DateTime ? IsCommitDisabled(1, unit) : IsUnitDisabled(unit))
        {
            await RaiseTextErrorAsync(OnEndRangeError, text);
            return;
        }
        await SetRangeAsync(Start, unit, DateRangeEndpoints.End);
        if (Mode == DatePickerMode.DateTime) _viewMonth = ClampView(FirstOfMonth(unit)); else AnchorView(unit, 1);
        FinishTextCommit();
    }

    // Raises whichever of the four typed-text failure callbacks the caller passed -- the endpoint's
    // OnStart/EndParseError for a GENUINE parse failure, or its OnStart/EndRangeError for a
    // well-formed value the Min/Max/DisabledDate/DisabledTime guards reject. The two stay separate
    // callbacks (see OnStartParseError/OnStartRangeError) because they are different situations
    // needing different messages; this only owns the shared HasDelegate check, which keeps a
    // handler-less picker on exactly its old silent-revert path.
    Task RaiseTextErrorAsync(EventCallback<string> callback, string text) =>
        callback.HasDelegate ? callback.InvokeAsync(text) : Task.CompletedTask;

    // A successful typed commit (a parsed date or an explicit clear) finalizes the field: any
    // in-progress calendar pick (or Time/DateTime pick session) is discarded so the display reflects
    // the bound values instead of contradicting them, and so a later day click/time change starts
    // fresh rather than resurrecting the discarded pending state.
    void FinishTextCommit()
    {
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _pendingSessionStart = null;
        _pendingSessionEnd = null;
    }

    // Central commit: normalizes both endpoints to Mode's own granularity, swaps a backwards pair,
    // and raises only the callbacks whose side actually changed.
    // Defense in depth for OnParametersSetAsync's Disabled => closed invariant: that closes the panel
    // (unmounting every cell) the moment Disabled is observed, so nothing reachable gets this far --
    // but every commit path in the control funnels through here, so one guard makes it structurally
    // impossible for a disabled picker to write through, whatever route a caller (or an event queued
    // against the pre-disable render tree) takes to reach it.
    //
    // `assigned` is what the CALLER is actually setting, which the value pair itself cannot say: a
    // typed Start commit passes the current End straight through (SetRangeAsync(unit, End)), and
    // "passed through unchanged" must not read as "assigned". Only the caller knows, so each one
    // declares it -- see OnValidCommit for what every path reports and why.
    async Task SetRangeAsync(DateTime? start, DateTime? end, DateRangeEndpoints assigned)
    {
        if (Disabled) return;
        start = start is { } s ? NormalizeForMode(s) : null;
        end = end is { } e ? NormalizeForMode(e) : null;
        if (start is { } a && end is { } b && b < a) (start, end) = (end, start);

        var startChanged = Start != start;
        var endChanged = End != end;
        Start = start;
        End = end;
        // Before the two per-endpoint callbacks below, and independent of whether either side actually
        // changed: this says "these endpoints received an accepted value", which is true even when the
        // value matches what was already there. A host form control's stale parse/range error for that
        // endpoint has to clear on THAT case too, and the per-side dedup below means neither Changed
        // callback can ever carry the news -- see OnValidCommit.
        if (assigned != DateRangeEndpoints.None && OnValidCommit.HasDelegate) await OnValidCommit.InvokeAsync(assigned);
        if (startChanged) await StartChanged.InvokeAsync(start);
        if (endChanged) await EndChanged.InvokeAsync(end);
    }

    void OnMonthSelectChanged(int panel, ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)) return;
        var shown = _viewMonth.AddMonths(panel);
        _viewMonth = ClampView(new DateTime(shown.Year, month, 1), -panel);
        _focusDay = null;
    }

    void OnYearSelectChanged(int panel, ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return;
        // Clamp before constructing the DateTime below — YearRange can offer (or a caller-supplied
        // Min/Max year can be) outside DateTime's [1, 9999] range, and the constructor throws
        // (circuit-killing on Blazor Server) rather than something ClampView could catch after the
        // fact.
        year = Math.Clamp(year, 1, 9999);
        var shown = _viewMonth.AddMonths(panel);
        _viewMonth = ClampView(new DateTime(year, shown.Month, 1), -panel);
        _focusDay = null;
    }

    // ----- Time/DateTime pick session: interaction ----------------------------
    // See the class remarks for the full session flow. Every member below is IsSessionMode-only --
    // the dual-panel members above (OnUnitClickAsync, CellClass, DayClass, IsEndpoint, ...) are
    // untouched and never invoked while Mode is DateTime/Time (the .razor markup branches on
    // IsSessionMode before choosing which set of members to call).

    // The ACTIVE endpoint's own resolved value: this session's own pending pick if it's touched that
    // endpoint yet, else whatever's already committed. Every session read/write site goes through
    // this (or its `other`-side mirror image, spelled out inline at each call site) so a session that
    // never touches an already-committed endpoint still resolves correctly on OK.
    DateTime? ActiveSessionValue => _activeInput == 0 ? (_pendingSessionStart ?? Start) : (_pendingSessionEnd ?? End);

    // The value PickerBase's shared time row displays and edits: the ACTIVE endpoint's own resolved
    // value. (DatePicker's own override is simply its bound Value.) Everything the row derives from it
    // -- the displayed hour/minute/second/period, the stepped option lists, the four @onchange
    // handlers, and the compose below -- lives on PickerBase; it was duplicated verbatim here.
    internal override DateTime? TimeRowValue => ActiveSessionValue;

    // Whichever endpoint's DisabledTime callback drives the CURRENTLY VISIBLE time row -- the active
    // side's own (see the class remarks: "the active side's callback drives the visible time row").
    Func<DateTime?, DisabledTimeParts?>? ActiveDisabledTime => _activeInput == 0 ? StartDisabledTime : EndDisabledTime;

    // The rest of the time row's inputs, forwarded from this control's own parameters -- see
    // PickerBase's own declarations for what each feeds. The ACTIVE endpoint's DisabledTime is
    // invoked exactly once here, for the whole row, against that endpoint's own resolved date part.
    internal override DisabledTimeParts? TimeRowDisabledParts => ActiveDisabledTime?.Invoke(ActiveSessionValue?.Date);
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

    // PickerBase's ApplyTimePartAsync hook, for the pick session: composes a new PENDING value for the
    // active endpoint from its current date part (its own session/committed value's date, or today when
    // neither exists) and its current time-of-day (ditto) with one H/m/s part replaced -- the identical
    // PickerMath.ComposeTimePart DatePicker's own override uses -- but writes to the pending session
    // value instead of committing immediately (nothing reaches Start/End until OK -- see the class
    // remarks). Rejects (no-ops) when the composed value is one this Mode won't accept for the active
    // endpoint, same guard DatePicker's version applies before ever letting a disabled time become
    // the bound value.
    //
    // The guard is the FULL IsCommitDisabled, not just its per-endpoint time half: Mode.DateTime's
    // composed DATE is not necessarily one anything validated -- ComposeTimePart falls back to
    // DateTime.Today when the endpoint resolves to nothing, so with (say) a Min a month out, an hour
    // change was the one route that could park a pending value on today. (Mode.Time's own arm of the
    // dispatcher IS just the time half -- no date-range concept there -- so nothing changes for it.)
    internal override Task ApplyTimePartAsync(int? hour, int? minute, int? second)
    {
        _startEdit = _endEdit = null;
        var composed = PickerMath.ComposeTimePart(TimeRowValue, ShowSeconds, hour, minute, second);
        if (IsCommitDisabled(_activeInput, composed)) return Task.CompletedTask;
        if (_activeInput == 0) _pendingSessionStart = composed; else _pendingSessionEnd = composed;
        return Task.CompletedTask;
    }

    // DateTime mode's day click: sets the active endpoint's PENDING date, preserving its own
    // pending/committed time-of-day (null time -- no session value and no committed value yet --
    // becomes midnight, the same default DatePicker's own DateTime day-click uses). The DAY needs its
    // own guard, same as every other grid click handler in this file: the cell renders aria-disabled
    // (it has to stay focusable -- see SessionDayButtonFragment), so the click still reaches here.
    // The carried TIME-OF-DAY needs a second one: the active endpoint's DisabledTime is per DATE,
    // so the clicked day can disable the very hour/minute/second this click would move onto it --
    // exactly what ApplyTimePartAsync above and the typed route (IsCommitDisabled) already reject.
    // A rejected click no-ops (nothing pending changes, the panel stays put), same as theirs; the
    // never-jump rule keeps the time row showing the endpoint's own unchanged value. Mirrors
    // DatePicker.OnDayClickAsync's identical DateTime-mode guard.
    //
    // The guard (and the pending value it writes) uses the NORMALIZED composition: this mode's
    // normalization zeroes the second when ShowSeconds is false, and SetRangeAsync will apply it on
    // the way to Start/End anyway. Guarding the raw value instead rejected a pick over a stale second
    // that no select in this row can even change and that the eventual commit would discard -- the
    // exact bug ApplyTimePartAsync's own ComposeTimePart zeroing exists to prevent.
    Task OnSessionDayClickAsync(DateTime day)
    {
        if (IsDayDisabled(day)) return Task.CompletedTask;
        var time = ActiveSessionValue?.TimeOfDay ?? TimeSpan.Zero;
        var composed = NormalizeForMode(day.Date + time);
        if (IsEndpointTimeDisabled(_activeInput, composed)) return Task.CompletedTask;
        _startEdit = _endEdit = null;
        if (_activeInput == 0) _pendingSessionStart = composed; else _pendingSessionEnd = composed;
        _focusDay = day;
        return Task.CompletedTask;
    }

    // The OTHER endpoint's own resolved value (this session's own pending pick for it, if any, else
    // its committed value) -- the mirror image of ActiveSessionValue, used by OK's own resolve/switch
    // decision and by the calendar's range-tint/selected-day classing below.
    DateTime? OtherSessionValue => _activeInput == 0 ? (_pendingSessionEnd ?? End) : (_pendingSessionStart ?? Start);

    // Whether `day` is either endpoint's own resolved day (active or other) -- drives both the
    // wss-picker-day-selected styling and the gridcell's aria-selected on the session's single
    // calendar. The band BETWEEN them is deliberately not aria-selected here (unlike the dual-panel
    // modes' committed range -- see IsUnitAriaSelected): the session's band is a tentative preview of
    // a pair nothing has committed yet, which is exactly why it reuses the -preview classes rather
    // than the committed -in-range ones (see SessionCellClass).
    bool IsSessionEndpoint(DateTime day) =>
        day == ActiveSessionValue?.Date || day == OtherSessionValue?.Date;

    string SessionDayClass(DateTime day)
    {
        var cls = "wss-picker-day";
        if (day.Month != _viewMonth.Month) cls += " wss-picker-day-outside";
        if (IsToday(day)) cls += " wss-picker-day-today";
        if (IsSessionEndpoint(day)) cls += " wss-picker-day-selected";
        return cls;
    }

    // Range tint while picking the second endpoint: bands between the OTHER endpoint's own resolved
    // day and the ACTIVE endpoint's own pending day, reusing CellClass's existing preview classes at
    // day granularity (see the class remarks) -- deliberately simple (no live pointer-hover tracking,
    // unlike the dual-panel Date mode's own two-click pick): the band only appears once a day has
    // actually been picked for the active side THIS session.
    string SessionCellClass(DateTime day)
    {
        var cls = "wss-picker-cell";
        var other = OtherSessionValue?.Date;
        var active = _activeInput == 0 ? _pendingSessionStart?.Date : _pendingSessionEnd?.Date;
        if (other is { } o && active is { } a && o != a)
        {
            var lo = o < a ? o : a;
            var hi = o < a ? a : o;
            if (day == lo) cls += " wss-picker-cell-preview-start";
            else if (day == hi) cls += " wss-picker-cell-preview-end";
            else if (day > lo && day < hi) cls += " wss-picker-cell-preview";
        }
        return cls;
    }

    // Whether the OK button renders DISABLED -- and, being the single predicate OnSessionOkAsync
    // itself gates on, exactly the condition under which OK would do nothing. Rendering the dead end
    // instead of swallowing the click is the convention this file (and DatePicker's Now/Today links,
    // and every disabled day button) already follows: a rejection the user can SEE.
    //
    // Three ways OK does nothing:
    //   * nothing is resolved for the active endpoint yet (the original rule -- a session must pick
    //     something before it can confirm it);
    //   * only the active endpoint is resolved, so OK would ADVANCE to the other one, but the active
    //     value can never commit -- advancing on it would strand the session;
    //   * both endpoints are resolved, so OK would COMMIT, and the pair SetRangeAsync would actually
    //     produce is one the guards reject.
    //
    // The commit case guards exactly what SetRangeAsync produces -- normalized and swapped as it does
    // -- so the start guard always sees the value that lands in Start (the same
    // normalize-then-swap-then-guard order OnPresetClickAsync uses for the identical reason). There
    // is deliberately NO separate pre-swap check of the active endpoint in that case: the swap can
    // move the active value to the OTHER endpoint, where a different DisabledTime applies, so a
    // pre-swap check rejects pairs this one accepts. The advance case has no other endpoint to swap
    // against yet, so there the active value IS checked at its own endpoint.
    //
    // Re-checking at all matters because neither resolved value is necessarily one this session's own
    // guarded paths produced: either side can fall back to a committed Start/End a consumer bound
    // directly, and Min/Max/DisabledDate/Start/EndDisabledTime are ordinary parameters that can change
    // between a pick and the OK that confirms it. Without the re-check OK is the one route that can
    // fire StartChanged/EndChanged with a value every other route rejects. Cost: one extra
    // DisabledDate/DisabledTime invocation per render of the session panel.
    bool SessionOkDisabled
    {
        get
        {
            if (ActiveSessionValue is not { } activeValue) return true;
            if (OtherSessionValue is not { } otherValue)
            {
                return IsCommitDisabled(_activeInput, NormalizeForMode(activeValue));
            }
            var (normalizedStart, normalizedEnd) = NormalizedSessionPair(activeValue, otherValue);
            return IsCommitDisabled(0, normalizedStart) || IsCommitDisabled(1, normalizedEnd);
        }
    }

    // The (Start, End) pair a session OK would actually commit: the active/other values assigned to
    // their sides, each normalized, then swapped if backwards -- mirroring SetRangeAsync exactly.
    // Shared by SessionOkDisabled's own guard and OnSessionOkAsync so the button's disabled state and
    // the handler can never disagree about which pair is being judged.
    (DateTime Start, DateTime End) NormalizedSessionPair(DateTime activeValue, DateTime otherValue)
    {
        var start = NormalizeForMode(_activeInput == 0 ? activeValue : otherValue);
        var end = NormalizeForMode(_activeInput == 0 ? otherValue : activeValue);
        return end < start ? (end, start) : (start, end);
    }

    // The OK button: confirms the ACTIVE endpoint's resolved value (pending, or its already-committed
    // fallback -- see ActiveSessionValue) into this session's own pending state. If the OTHER
    // endpoint has neither a pending nor a committed value, the session isn't done -- switch to it
    // (the active underline moves; DateTime mode re-anchors its single calendar on the target's own
    // value, falling back to the just-confirmed one so the panel always shows something relevant).
    // Otherwise both endpoints are now resolved, so commit them together (SetRangeAsync swaps a
    // backwards pair, same as every other mode) and close.
    //
    // SessionOkDisabled above is the whole guard -- the same predicate the button's own `disabled`
    // attribute renders from (see the .razor markup), re-checked here as defense in depth so a caller
    // that invokes this directly (or a test harness that doesn't honor `disabled`) can't slip past it.
    // Nothing mutates before that guard: the pending write below used to sit ahead of the pair check's
    // own `return`, which turned "fall back to the committed Start/End" into an explicit pending value
    // on a REJECTED OK -- one that then shadows a Start/End the consumer changes while the panel is
    // still open.
    async Task OnSessionOkAsync()
    {
        if (SessionOkDisabled) return;
        var activeValue = ActiveSessionValue!.Value;
        var otherValue = OtherSessionValue;

        if (_activeInput == 0) _pendingSessionStart = activeValue; else _pendingSessionEnd = activeValue;

        if (otherValue is null)
        {
            _activeInput = 1 - _activeInput;
            if (Mode == DatePickerMode.DateTime)
            {
                var anchor = (_activeInput == 0 ? Start : End) ?? activeValue;
                _viewMonth = ClampView(FirstOfMonth(anchor));
            }
            _focusDay = null;
            return;
        }

        await SetRangeAsync(_activeInput == 0 ? activeValue : otherValue.Value,
                            _activeInput == 0 ? otherValue.Value : activeValue,
                            DateRangeEndpoints.Both);
        _pendingInputFocus = true; // the OK button is about to unmount
        await CloseAsync();
    }

    // ----- PickerBase hooks (JS-interop + overlay lifecycle) ------------------
    // The JsModule holders, the OnAfterRenderAsync template, and DisposeAsync all
    // live on PickerBase -- these three hooks are this control's only customization of that shared
    // template (two inputs to wire, two grids to init, whichever of start/end is active to reclaim
    // focus onto).

    protected override ValueTask WireInputsAsync(IJSObjectReference module) =>
        module.InvokeVoidAsync("initPicker", _wrapperRef, _startInputRef, _endInputRef);

    protected override IEnumerable<ElementReference> GridRefs => _gridRefs;

    protected override ElementReference FocusReclaimTarget => _activeInput == 1 ? _endInputRef : _startInputRef;
}
