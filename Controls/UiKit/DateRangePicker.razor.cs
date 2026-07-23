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
/// whole rows instead of individual cells. <c>DateTime</c>/<c>Time</c> render as <c>Date</c> in
/// this release -- time-of-day support arrives in a later phase of this same effort, before any
/// NuGet release ships it, so no consumer ever sees the fallback.
/// Picking the second unit of a range (or a preset) commits the range and closes; typed input
/// commits on Enter or blur.
/// </summary>
/// <remarks>
/// Not a form control (no <c>InputBase</c>/validation wiring) — bind with <c>@bind-Start</c> /
/// <c>@bind-End</c>. JS interop (viewport flip/clamp, form-submit suppression, focus-out close,
/// arrow-key page-scroll suppression) degrades gracefully: without JS the dropdown opens below the
/// field at the CSS default placement, everything remains clickable, and arrow-key grid navigation
/// still updates the roving-tabindex state (just without the DOM focus follow or the native
/// page-scroll suppression).
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
    /// <see cref="DatePickerMode.DateTime"/> and <see cref="DatePickerMode.Time"/> behave as
    /// <c>Date</c> in this release -- time-of-day support is a later phase of this same effort.</summary>
    [Parameter] public DatePickerMode Mode { get; set; } = DatePickerMode.Date;

    /// <summary>Optional shortcuts rendered as a sidebar in the dropdown. Each consumer supplies its
    /// own list (nothing is built in); clicking one commits its resolved range and closes.</summary>
    [Parameter] public IReadOnlyList<DateRangePreset>? Presets { get; set; }

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

    /// <summary>First day of the week for the calendar grids (<see cref="DatePickerMode.Date"/> and
    /// <see cref="DatePickerMode.Week"/> only -- the latter also uses it to compute each row's own
    /// week start). Null (default) follows <see cref="CultureInfo.CurrentCulture"/>. (The Figma
    /// mock's Monday start is the AntD kit's default-locale artifact, not a design decision — same
    /// category as its DM Sans font.)</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

    /// <summary>Shows a leading week-number column (AntD's <c>showWeek</c>) beside BOTH panels'
    /// day grids in <see cref="DatePickerMode.Date"/> (and its <c>DateTime</c>/<c>Time</c>
    /// fallback), with no other behavior change — a day click still commits that day, not its
    /// week. Defaults to false. <see cref="DatePickerMode.Week"/> always renders this column
    /// regardless of this parameter.</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    /// <summary>HTML id applied to the start input — wires a consumer label / test hook.</summary>
    [Parameter] public string? Id { get; set; }
    /// <summary>HTML id applied to the end input — wires a consumer label / test hook.</summary>
    [Parameter] public string? EndId { get; set; }

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
    /// left panel only). Override to localize.</summary>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <summary>Accessible name of the next-month button (<see cref="DatePickerMode.Date"/>'s right
    /// panel only). Override to localize.</summary>
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
    // Shared JS-interop/overlay-lifecycle state (_wrapperRef, _panelRef, _module, _pickerModule,
    // _open, _positioned, _disposed, _inputsWired, _openZIndex, _focusDay, _pendingFocusDate,
    // _pendingInputFocus, _suppressOpenOnFocus) lives on PickerBase.

    ElementReference _startInputRef;
    ElementReference _endInputRef;
    // Index 0 = left panel's grid, 1 = right panel's — see the ant-design-blazor / procurement-hub
    // precedent for @ref into an array element inside a @for loop.
    readonly ElementReference[] _gridRefs = new ElementReference[2];
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
    // First-of-month shown in the left panel in Mode.Date (and its DateTime/Time/Week fallback);
    // the right panel is always _viewMonth + 1 month. In Mode.Month/Quarter, only _viewMonth.Year
    // matters -- it's the left panel's year (see LeftYear/RightYear). In Mode.Year, only
    // _viewMonth.Year matters too -- ClampDecadeStartForRange floors it to the left panel's decade
    // (see LeftDecadeStart/RightDecadeStart). One field serves all four grain because exactly one
    // of these readings is ever active for a given Mode, and each mode's own mutations (nav
    // buttons, selects, keyboard crossing) only ever touch it through the matching property.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text per input (null = show the formatted bound value).
    string? _startEdit;
    string? _endEdit;

    // ----- Mode-derived helpers ----------------------------------------------

    /// <summary><see cref="Mode"/> folded to what this release actually implements:
    /// <see cref="DatePickerMode.DateTime"/>/<see cref="DatePickerMode.Time"/> read as
    /// <see cref="DatePickerMode.Date"/> (see <see cref="Mode"/>'s own doc comment).
    /// <see cref="DatePickerMode.Week"/> is its own mode below -- it reuses every Date-mode
    /// day-grid mechanism (view anchoring, focus/keyboard nav, Min/Max day-granularity cell
    /// disabling) via this switch's default arm, differing only in the markup (a week-number rows
    /// layout instead of the flat grid — see <see cref="ShowWeekRows"/>) and the commit granularity
    /// (a day click resolves to its WEEK START before reaching <see cref="OnUnitClickAsync"/> — see
    /// <see cref="OnWeekDayClickAsync"/>). Every internal branch reads this, never the raw
    /// <see cref="Mode"/> parameter, so a later phase only has to change this one switch.</summary>
    DatePickerMode EffectiveMode => Mode switch
    {
        DatePickerMode.DateTime or DatePickerMode.Time => DatePickerMode.Date,
        _ => Mode,
    };

    // ----- Display helpers (used by the .razor markup) ------------------------

    string WrapperClass
    {
        get
        {
            var classes = "wss-picker";
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
            return ZIndexStyle(width);
        }
    }

    // Format/Placeholder resolution: an explicit value always wins; null falls through to Mode's
    // default -- same per-mode defaults DatePicker's own EffectiveFormat uses (Quarter's bland
    // "yyyy" included; see DatePicker.EffectiveFormat's doc comment for why). All internal
    // display/parse code routes through this (never the raw Format parameter).
    string EffectiveFormat => Format ?? EffectiveMode switch
    {
        DatePickerMode.Month => "MM/yyyy",
        DatePickerMode.Year => "yyyy",
        DatePickerMode.Quarter => "yyyy",
        // Bland placeholder value, same rationale as Quarter's: no .NET format token renders a week
        // number, so FormatDate/TryParseDate bypass ToString(EffectiveFormat) for Week entirely (see
        // their own Week special cases below) -- this only matters as TryParseDate's exact-format
        // fallback attempt, tried after the "yyyy-Www" shorthand.
        DatePickerMode.Week => "yyyy",
        _ => "MM/dd/yyyy",
    };

    string DefaultPlaceholder => EffectiveFormat.ToUpperInvariant();

    // While a fresh pick is in progress the field previews it (start = the pending unit, end
    // empties); a discarded pick falls back to the committed values automatically.
    string StartDisplay => _startEdit ?? FormatDate(_selecting ? _pendingStart : Start);
    string EndDisplay => _endEdit ?? (_selecting ? string.Empty : FormatDate(End));

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

    string DayClass(DateTime day, DateTime month)
    {
        var cls = "wss-picker-day";
        if (day.Month != month.Month) cls += " wss-picker-day-outside";
        if (day == DateTime.Today) cls += " wss-picker-day-today";
        // Week mode suppresses the single-day selected look -- the ROW is the selection unit there
        // (see WeekRowClass/IsWeekRowEndpoint), mirroring DatePicker.DayClass's own suppression.
        if (EffectiveMode != DatePickerMode.Week && IsEndpoint(day)) cls += " wss-picker-day-selected";
        return cls;
    }

    // Week mode's row-wide "is this row an endpoint" check -- every day sharing a week with the
    // start or end endpoint is aria-pressed, not just the single day that happens to equal the week
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
    // -- unlike the day grid's edge-to-edge cells. An endpoint unit needs no modifier class here:
    // aria-pressed="true" (via IsEndpoint) already gives it the filled/primary look through the
    // existing .wss-picker-month-btn[aria-pressed="true"] rule.
    string UnitBtnClass(DateTime unit, bool outside = false)
    {
        var cls = outside ? "wss-picker-month-btn wss-picker-month-btn-outside" : "wss-picker-month-btn";
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

    // DisabledDate is folded into every granularity's Is*Disabled helper below (not called
    // separately anywhere else) so every consumer of them -- the cell disabled attributes, the
    // DefaultFocus*/FirstEnabled* skip logic, and IsUnitDisabled's typed-text/preset commit guards --
    // picks it up automatically and can never disagree about what counts as disabled. Mirrors
    // DatePicker's identical fold.
    bool IsDayDisabled(DateTime day) =>
        (Min is { } min && day < min.Date) || (Max is { } max && day > max.Date) ||
        (DisabledDate?.Invoke(day) ?? false);

    // Month-mode equivalent of IsDayDisabled: a whole month is disabled once it falls entirely
    // outside [Min, Max] at month granularity -- same granularity DatePicker.IsMonthDisabled uses.
    bool IsMonthDisabled(DateTime month) =>
        (Min is { } min && month < FirstOfMonth(min)) || (Max is { } max && month > FirstOfMonth(max)) ||
        (DisabledDate?.Invoke(month) ?? false);

    // Year-mode equivalent, one granularity up.
    bool IsYearDisabled(DateTime year) =>
        (Min is { } min && year < FirstOfYear(min)) || (Max is { } max && year > FirstOfYear(max)) ||
        (DisabledDate?.Invoke(year) ?? false);

    // Quarter-mode equivalent, at quarter granularity. `quarterStart` is already QuarterStart-shaped.
    bool IsQuarterDisabled(DateTime quarterStart) =>
        (Min is { } min && quarterStart < QuarterStart(min)) || (Max is { } max && quarterStart > QuarterStart(max)) ||
        (DisabledDate?.Invoke(quarterStart) ?? false);

    // Week-mode equivalent of IsDayDisabled, at week granularity: a whole week is disabled once its
    // 7-day span falls entirely outside [Min, Max] (or DisabledDate itself rejects the week start) --
    // the individual day buttons stay enabled per IsDayDisabled above (a partially-in-range/disabled
    // week is still clickable; only the commit itself is guarded here) -- mirrors
    // DatePicker.IsWeekDisabledForCommit. `weekStart` is already WeekStart-shaped -- this is the one
    // place DisabledDate sees a week start rather than a day.
    bool IsWeekDisabledForCommit(DateTime weekStart) =>
        (Max is { } max && weekStart > max.Date) || (Min is { } min && weekStart.AddDays(6) < min.Date) ||
        (DisabledDate?.Invoke(weekStart) ?? false);

    // Dispatches to the Mode-appropriate disabled check -- shared by the grid `disabled` attributes,
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

    // PickerCulture lives on PickerBase (shared with DatePicker).

    string MonthName(int month) => PickerMath.MonthName(PickerCulture, month);

    // The years offered by a panel's year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — see PickerMath.YearRange for the full contract (including the [1, 9999]
    // clamp -- OnYearSelectChanged/OnRangeYearSelectChanged apply the matching clamp to the value
    // actually selected).
    (int From, int To) YearRange(int displayedYear) => PickerMath.YearRange(displayedYear, Min, Max);

    DayOfWeek EffectiveFirstDayOfWeek =>
        FirstDayOfWeek ?? PickerCulture.DateTimeFormat.FirstDayOfWeek;

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

    // Quarter mode's null-Format display: no .NET format token renders a quarter number, so this
    // bypasses ToString(EffectiveFormat) entirely for that one case -- mirrors DatePicker.FormatDate.
    // Week mode's null-Format display bypasses it the same way -- no .NET token for a week number
    // either.
    string FormatDate(DateTime? value)
    {
        if (value is not { } v) return string.Empty;
        if (EffectiveMode == DatePickerMode.Quarter && Format is null)
        {
            return PickerMath.FormatQuarterDisplay(v, PickerCulture);
        }
        if (EffectiveMode == DatePickerMode.Week && Format is null)
        {
            return PickerMath.FormatWeekDisplay(v, PickerCulture, EffectiveFirstDayOfWeek);
        }
        return v.ToString(EffectiveFormat, PickerCulture);
    }

    // Exact effective format first, then the current culture's general parse -- then normalizes the
    // parsed result to Mode's own granularity (mirrors SetRangeAsync's normalization so a typed
    // commit and a click/select commit always land on the same shape of value). Quarter mode (with
    // Format left null, mirroring FormatDate's special case above) tries the "yyyy-Qn" shorthand
    // first via PickerMath.TryParseQuarterShorthand -- a plain typed date still falls through to the
    // general parse below and normalizes to its own quarter, same as every other mode's typed-text
    // path -- mirrors DatePicker.TryParseDate. Week mode's "yyyy-Www" shorthand
    // (PickerMath.TryParseWeekShorthand) mirrors that the same way.
    bool TryParseDate(string text, out DateTime value)
    {
        if (EffectiveMode == DatePickerMode.Quarter && Format is null && PickerMath.TryParseQuarterShorthand(text, out value))
        {
            return true;
        }
        if (EffectiveMode == DatePickerMode.Week && Format is null &&
            PickerMath.TryParseWeekShorthand(text, PickerCulture, EffectiveFirstDayOfWeek, out value))
        {
            return true;
        }
        if (DateTime.TryParseExact(text, EffectiveFormat, PickerCulture, DateTimeStyles.None, out value) ||
            DateTime.TryParse(text, PickerCulture, DateTimeStyles.None, out value))
        {
            value = NormalizeForMode(value);
            return true;
        }
        return false;
    }

    // Central per-mode normalization, shared by TryParseDate and SetRangeAsync so every commit path
    // (click, typed text, select change, preset) agrees on the same shape of value.
    DateTime NormalizeForMode(DateTime value) => PickerMath.NormalizeForMode(EffectiveMode, EffectiveFirstDayOfWeek, true, value);

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
        // Falling through to the left panel's 1st like before would park the roving tabindex on a
        // disabled button and make both grids keyboard-unreachable. Land on the first enabled day
        // across either panel instead (left panel checked first); if both panels are entirely
        // disabled there's nothing actionable in either, so any deterministic in-month day is fine.
        return FirstEnabledDay(_viewMonth) ?? FirstEnabledDay(_viewMonth.AddMonths(1)) ?? _viewMonth;
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
    // month on a crossing. A day that lands disabled (Min/Max) still becomes the focus target — only
    // clicking commits, so
    // parking keyboard focus on a disabled day is harmless and lets Left/Right keep stepping
    // day-by-day through it. The actual DOM focus move (needed whenever a grid re-renders with new
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

    DateTime? FirstEnabledMonth(int year)
    {
        for (var m = 1; m <= 12; m++)
        {
            var month = new DateTime(year, m, 1);
            if (!IsMonthDisabled(month)) return month;
        }
        return null;
    }

    DateTime DefaultFocusMonth()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisibleUnit(start) && !IsMonthDisabled(start)) return start;
        if (e is { } end && IsVisibleUnit(end) && !IsMonthDisabled(end)) return end;
        var today = FirstOfMonth(DateTime.Today);
        if (IsVisibleUnit(today) && !IsMonthDisabled(today)) return today;
        return FirstEnabledMonth(LeftYear) ?? FirstEnabledMonth(RightYear) ?? new DateTime(LeftYear, 1, 1);
    }

    DateTime? FirstEnabledQuarter(int year)
    {
        for (var q = 1; q <= 4; q++)
        {
            var quarterStart = QuarterStart(year, q);
            if (!IsQuarterDisabled(quarterStart)) return quarterStart;
        }
        return null;
    }

    DateTime DefaultFocusQuarter()
    {
        var (s, e) = DisplayRange;
        if (s is { } start && IsVisibleUnit(start) && !IsQuarterDisabled(start)) return start;
        if (e is { } end && IsVisibleUnit(end) && !IsQuarterDisabled(end)) return end;
        var today = QuarterStart(DateTime.Today);
        if (IsVisibleUnit(today) && !IsQuarterDisabled(today)) return today;
        return FirstEnabledQuarter(LeftYear) ?? FirstEnabledQuarter(RightYear) ?? QuarterStart(LeftYear, 1);
    }

    // Only scans the decade's own 10 real years (never the two dimmed adjacent-decade cells) --
    // mirrors DatePicker.FirstEnabledYear.
    DateTime? FirstEnabledYear(int decadeStart)
    {
        for (var y = decadeStart; y <= decadeStart + 9; y++)
        {
            var year = new DateTime(y, 1, 1);
            if (!IsYearDisabled(year)) return year;
        }
        return null;
    }

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
    // panel so it's visible on open -- see AnchorView.
    void Open()
    {
        _open = true;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _startEdit = _endEdit = null;
        _focusDay = null;
        _pendingInputFocus = false;
        var anchor = Start ?? End ?? DateTime.Today;
        AnchorView(anchor, Start is null && End is not null ? 1 : 0);
    }

    Task CloseAsync()
    {
        _open = false;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
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
    // commits (swapping a backwards pick) and closes. No disabled guard here -- same convention as
    // DatePicker's grid click handlers: a disabled button's `disabled` attribute already prevents
    // the browser from ever dispatching this click.
    async Task OnUnitClickAsync(DateTime unit)
    {
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
        await SetRangeAsync(start, unit);
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
    Task OnGridDayClickAsync(DateTime day) =>
        EffectiveMode == DatePickerMode.Week ? OnWeekDayClickAsync(day) : OnUnitClickAsync(day);

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
        start = start.Date;
        end = end.Date;
        if (end < start) (start, end) = (end, start);
        start = ClampToMinMax(start);
        end = ClampToMinMax(end);
        if (end < start) end = start;

        var normalizedStart = NormalizeForMode(start);
        var normalizedEnd = NormalizeForMode(end);
        if (normalizedEnd < normalizedStart) (normalizedStart, normalizedEnd) = (normalizedEnd, normalizedStart);
        if (IsUnitDisabled(normalizedStart) || IsUnitDisabled(normalizedEnd)) return;

        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        await SetRangeAsync(start, end);
        _pendingInputFocus = true; // the clicked preset button is about to unmount
        await CloseAsync();
    }

    async Task ClearAsync()
    {
        if (Disabled) return;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _startEdit = _endEdit = null;
        await SetRangeAsync(null, null);
    }

    async Task CommitStartTextAsync()
    {
        if (_startEdit is null) return;
        var text = _startEdit.Trim();
        _startEdit = null;
        if (text.Length == 0)
        {
            if (Start is not null) await SetRangeAsync(null, End);
            FinishTextCommit();
            return;
        }
        // Invalid or out-of-range text reverts to the formatted bound value (edit state cleared above).
        if (!TryParseDate(text, out var unit) || IsUnitDisabled(unit)) return;
        await SetRangeAsync(unit, End);
        AnchorView(unit, 0);
        FinishTextCommit();
    }

    async Task CommitEndTextAsync()
    {
        if (_endEdit is null) return;
        var text = _endEdit.Trim();
        _endEdit = null;
        if (text.Length == 0)
        {
            if (End is not null) await SetRangeAsync(Start, null);
            FinishTextCommit();
            return;
        }
        if (!TryParseDate(text, out var unit) || IsUnitDisabled(unit)) return;
        await SetRangeAsync(Start, unit);
        AnchorView(unit, 1);
        FinishTextCommit();
    }

    // A successful typed commit (a parsed date or an explicit clear) finalizes the field: any
    // in-progress calendar pick is discarded so the display reflects the bound values instead of
    // contradicting them, and so a later day click starts a fresh pick rather than resurrecting the
    // discarded pending start.
    void FinishTextCommit()
    {
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
    }

    // Central commit: normalizes both endpoints to Mode's own granularity, swaps a backwards pair,
    // and raises only the callbacks whose side actually changed.
    async Task SetRangeAsync(DateTime? start, DateTime? end)
    {
        start = start is { } s ? NormalizeForMode(s) : null;
        end = end is { } e ? NormalizeForMode(e) : null;
        if (start is { } a && end is { } b && b < a) (start, end) = (end, start);

        var startChanged = Start != start;
        var endChanged = End != end;
        Start = start;
        End = end;
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

    // ----- PickerBase hooks (JS-interop + overlay lifecycle) ------------------
    // GetModuleAsync/GetPickerNavModuleAsync, the OnAfterRenderAsync template, and DisposeAsync all
    // live on PickerBase -- these three hooks are this control's only customization of that shared
    // template (two inputs to wire, two grids to init, whichever of start/end is active to reclaim
    // focus onto).

    protected override ValueTask WireInputsAsync(IJSObjectReference module) =>
        module.InvokeVoidAsync("initPicker", _wrapperRef, _startInputRef, _endInputRef);

    protected override IEnumerable<ElementReference> GridRefs => _gridRefs;

    protected override ElementReference FocusReclaimTarget => _activeInput == 1 ? _endInputRef : _startInputRef;
}
