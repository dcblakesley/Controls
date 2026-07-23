using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-style date-range picker: a composite start → end field that opens a dropdown with
/// an optional preset sidebar and a dual-panel calendar whose headers are quick-select
/// dropdowns/nav buttons. <see cref="Mode"/> selects the granularity of both panels together --
/// <c>Date</c> (default) shows two consecutive one-month calendars; <c>Month</c> shows two
/// consecutive years, each a 3x4 grid of month buttons; <c>Quarter</c> shows two consecutive
/// years, each a single row of 4 quarter buttons; <c>Year</c> shows two consecutive decades, each
/// a 3x4 grid of year buttons (10 of the decade plus 2 dimmed adjacent-decade years).
/// <c>DateTime</c>/<c>Time</c> render as <c>Date</c> in this release -- time-of-day support arrives
/// in a later phase of this same effort, before any NuGet release ships it, so no consumer ever
/// sees the fallback. <c>Week</c> is a later sub-phase too and also renders as <c>Date</c> for now.
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
    /// <c>Date</c> in this release -- time-of-day support is a later phase of this same effort.
    /// <see cref="DatePickerMode.Week"/> also behaves as <c>Date</c> for now (a later sub-phase).</summary>
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

    /// <summary>Display and primary parse format for the two inputs. Typed text is parsed with this
    /// exact format first, then with the current culture's general date parsing. Null (default)
    /// picks <see cref="Mode"/>'s default (same values as <see cref="DatePicker.Format"/>'s):
    /// <c>Date</c> <c>MM/dd/yyyy</c> (the Figma spec) · <c>Month</c> <c>MM/yyyy</c> · <c>Year</c>
    /// <c>yyyy</c>. <c>Quarter</c> has no .NET format token for a quarter number: left null, it
    /// renders/parses <c>yyyy-Qn</c> (e.g. "2026-Q3") via a hand-rolled special case instead of
    /// <see cref="DateTime.ToString(string)"/>; set explicitly, it is used verbatim via
    /// <c>ToString</c> and therefore can't render the quarter digit itself.</summary>
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

    /// <summary>First day of the week for the calendar grids (<see cref="DatePickerMode.Date"/>
    /// only). Null (default) follows <see cref="CultureInfo.CurrentCulture"/>. (The Figma mock's
    /// Monday start is the AntD kit's default-locale artifact, not a design decision — same
    /// category as its DM Sans font.)</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

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
    /// <see cref="DatePickerMode.DateTime"/>/<see cref="DatePickerMode.Time"/>/
    /// <see cref="DatePickerMode.Week"/> all read as <see cref="DatePickerMode.Date"/> (see
    /// <see cref="Mode"/>'s own doc comment). Every internal branch reads this, never the raw
    /// <see cref="Mode"/> parameter, so a later phase only has to change this one switch.</summary>
    DatePickerMode EffectiveMode => Mode switch
    {
        DatePickerMode.DateTime or DatePickerMode.Time or DatePickerMode.Week => DatePickerMode.Date,
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
        var (s, e) = DisplayRange;
        var cls = "wss-picker-cell";
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
        if (IsEndpoint(day)) cls += " wss-picker-day-selected";
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

    bool IsDayDisabled(DateTime day) =>
        (Min is { } min && day < min.Date) || (Max is { } max && day > max.Date);

    // Month-mode equivalent of IsDayDisabled: a whole month is disabled once it falls entirely
    // outside [Min, Max] at month granularity -- same granularity DatePicker.IsMonthDisabled uses.
    bool IsMonthDisabled(DateTime month) =>
        (Min is { } min && month < FirstOfMonth(min)) || (Max is { } max && month > FirstOfMonth(max));

    // Year-mode equivalent, one granularity up.
    bool IsYearDisabled(DateTime year) =>
        (Min is { } min && year < FirstOfYear(min)) || (Max is { } max && year > FirstOfYear(max));

    // Quarter-mode equivalent, at quarter granularity. `quarterStart` is already QuarterStart-shaped.
    bool IsQuarterDisabled(DateTime quarterStart) =>
        (Min is { } min && quarterStart < QuarterStart(min)) || (Max is { } max && quarterStart > QuarterStart(max));

    // Dispatches to the Mode-appropriate disabled check -- shared by the grid `disabled` attributes,
    // the DefaultFocus*/FirstEnabled* skip logic, and the typed-text commit guard, so they can never
    // disagree about what counts as disabled.
    bool IsUnitDisabled(DateTime unit) => EffectiveMode switch
    {
        DatePickerMode.Month => IsMonthDisabled(unit),
        DatePickerMode.Quarter => IsQuarterDisabled(unit),
        DatePickerMode.Year => IsYearDisabled(unit),
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

    // Quarter mode's null-Format display: no .NET format token renders a quarter number, so this
    // bypasses ToString(EffectiveFormat) entirely for that one case -- mirrors DatePicker.FormatDate.
    string FormatDate(DateTime? value)
    {
        if (value is not { } v) return string.Empty;
        if (EffectiveMode == DatePickerMode.Quarter && Format is null)
        {
            return PickerMath.FormatQuarterDisplay(v, PickerCulture);
        }
        return v.ToString(EffectiveFormat, PickerCulture);
    }

    // Exact effective format first, then the current culture's general parse -- then normalizes the
    // parsed result to Mode's own granularity (mirrors SetRangeAsync's normalization so a typed
    // commit and a click/select commit always land on the same shape of value). Quarter mode (with
    // Format left null, mirroring FormatDate's special case above) tries the "yyyy-Qn" shorthand
    // first via PickerMath.TryParseQuarterShorthand -- a plain typed date still falls through to the
    // general parse below and normalizes to its own quarter, same as every other mode's typed-text
    // path -- mirrors DatePicker.TryParseDate.
    bool TryParseDate(string text, out DateTime value)
    {
        if (EffectiveMode == DatePickerMode.Quarter && Format is null && PickerMath.TryParseQuarterShorthand(text, out value))
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

    // Hover-range preview: only tracked while a pick is in progress, so hovering the other 83 cells
    // of an idle grid never triggers a render.
    void OnUnitPointerEnter(DateTime unit)
    {
        if (!_selecting || _hoverDay == unit) return;
        _hoverDay = unit;
    }

    void OnGridPointerLeave()
    {
        if (_hoverDay is not null) _hoverDay = null;
    }

    // A preset click (any Mode): resolve, clamp both ends into [Min, Max] at DAY granularity (so a
    // preset can never commit days the calendar itself would disable at that finer grain), then
    // hand off to SetRangeAsync, which normalizes to Mode's own granularity centrally -- unlike a
    // typed/clicked commit, a preset never rejects; it always clamps to something committable
    // (matching the existing Preset_entirely_past_max/before_min clamp tests).
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
