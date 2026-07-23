using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-style date-range picker: a composite start → end field that opens a dropdown with
/// an optional preset sidebar and a dual-month calendar whose headers are month/year quick-select
/// dropdowns. Picking the second day of a range (or a preset) commits the range and closes; typed
/// input commits on Enter or blur. Date-only — committed values are midnight local dates.
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

    /// <summary>Start of the bound range (date-only; null = empty). Supports <c>@bind-Start</c>.</summary>
    [Parameter] public DateTime? Start { get; set; }
    /// <summary>Raised with the new start when it changes (supports <c>@bind-Start</c>).</summary>
    [Parameter] public EventCallback<DateTime?> StartChanged { get; set; }

    /// <summary>End of the bound range (date-only; null = empty). Supports <c>@bind-End</c>.</summary>
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

    /// <summary>Optional shortcuts rendered as a sidebar in the dropdown. Each consumer supplies its
    /// own list (nothing is built in); clicking one commits its resolved range and closes.</summary>
    [Parameter] public IReadOnlyList<DateRangePreset>? Presets { get; set; }

    /// <summary>Earliest selectable day (inclusive). Days before it are disabled; presets clamp to it.</summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>Latest selectable day (inclusive). Days after it are disabled; presets clamp to it.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Display and primary parse format for the two inputs. Typed text is parsed with this
    /// exact format first, then with the current culture's general date parsing. Defaults to
    /// <c>MM/dd/yyyy</c> (the Figma spec); the placeholders derive from it unless overridden.</summary>
    [Parameter] public string Format { get; set; } = "MM/dd/yyyy";

    /// <summary>Placeholder for the start input. Null (default) shows the uppercased <see cref="Format"/>.</summary>
    [Parameter] public string? StartPlaceholder { get; set; }
    /// <summary>Placeholder for the end input. Null (default) shows the uppercased <see cref="Format"/>.</summary>
    [Parameter] public string? EndPlaceholder { get; set; }

    /// <summary>Shows a clear button (over the calendar icon) while a value is set. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary>Disables all interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Field width as a CSS length (e.g. "280px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>First day of the week for the calendar grids. Null (default) follows
    /// <see cref="CultureInfo.CurrentCulture"/>. (The Figma mock's Monday start is the AntD kit's
    /// default-locale artifact, not a design decision — same category as its DM Sans font.)</summary>
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
    /// <summary>Accessible name of each panel's month select. Override to localize.</summary>
    [Parameter] public string MonthSelectLabel { get; set; } = "Month";
    /// <summary>Accessible name of each panel's year select. Override to localize.</summary>
    [Parameter] public string YearSelectLabel { get; set; } = "Year";
    /// <summary>Accessible name of the clear button. Override to localize.</summary>
    [Parameter] public string ClearLabel { get; set; } = "Clear dates";
    /// <summary>Accessible name of the preset sidebar list. Override to localize.</summary>
    [Parameter] public string PresetsLabel { get; set; } = "Quick ranges";
    /// <summary>Accessible name of the previous-month button (left panel only). Override to localize.</summary>
    [Parameter] public string PrevMonthLabel { get; set; } = "Previous month";
    /// <summary>Accessible name of the next-month button (right panel only). Override to localize.</summary>
    [Parameter] public string NextMonthLabel { get; set; } = "Next month";

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
    // A new range pick is in progress: the first day is chosen, the second click commits. While
    // true the display shows only _pendingStart (the committed Start/End stay untouched until the
    // pick completes, so Escape/backdrop discards cleanly).
    bool _selecting;
    DateTime? _pendingStart;
    // The day currently under the pointer while _selecting — drives the hover-range preview tint
    // between _pendingStart and this day. Never set (or read) outside a pick in progress.
    DateTime? _hoverDay;
    // First-of-month shown in the left panel; the right panel is always _viewMonth + 1 month.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text per input (null = show the formatted bound value).
    string? _startEdit;
    string? _endEdit;

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

    string DefaultPlaceholder => Format.ToUpperInvariant();

    // While a fresh pick is in progress the field previews it (start = the pending day, end
    // empties); a discarded pick falls back to the committed values automatically.
    string StartDisplay => _startEdit ?? FormatDate(_selecting ? _pendingStart : Start);
    string EndDisplay => _endEdit ?? (_selecting ? string.Empty : FormatDate(End));

    bool ShowClear => AllowClear && !Disabled && (Start is not null || End is not null);

    // The range the calendar highlights: the in-progress pick while one is underway, otherwise the
    // committed values.
    (DateTime? Start, DateTime? End) DisplayRange =>
        _selecting ? (_pendingStart, null) : (Start?.Date, End?.Date);

    bool IsEndpoint(DateTime day)
    {
        var (s, e) = DisplayRange;
        return day == s || day == e;
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

    bool IsDayDisabled(DateTime day) =>
        (Min is { } min && day < min.Date) || (Max is { } max && day > max.Date);

    // PickerCulture lives on PickerBase (shared with DatePicker).

    string MonthName(int month) => PickerMath.MonthName(PickerCulture, month);

    // The years offered by a panel's year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — see PickerMath.YearRange for the full contract (including the [1, 9999]
    // clamp -- OnYearSelectChanged applies the matching clamp to the value actually selected).
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

    string FormatDate(DateTime? value) =>
        value?.ToString(Format, PickerCulture) ?? string.Empty;

    bool TryParseDate(string text, out DateTime value)
    {
        if (DateTime.TryParseExact(text, Format, PickerCulture, DateTimeStyles.None, out value) ||
            DateTime.TryParse(text, PickerCulture, DateTimeStyles.None, out value))
        {
            value = value.Date;
            return true;
        }
        return false;
    }

    static DateTime FirstOfMonth(DateTime value) => PickerMath.FirstOfMonth(value);

    // The left panel's month, clamped so the +1-month right panel and the 42-cell grids can never
    // overflow DateTime's range (offsetMonths carries the panel adjustment through the clamp) — see
    // PickerMath.ClampView (this is the superset signature; DatePicker calls it with offset 0).
    static DateTime ClampView(DateTime firstOfMonth, int offsetMonths = 0) => PickerMath.ClampView(firstOfMonth, offsetMonths);

    // Clamps a single day into [Min, Max] (each bound applied only when set).
    DateTime ClampToMinMax(DateTime day)
    {
        if (Min is { } min && day < min.Date) day = min.Date;
        if (Max is { } max && day > max.Date) day = max.Date;
        return day;
    }

    // ----- Roving-tabindex keyboard navigation -------------------------------

    // Is `day` inside either currently displayed month? (DateRangePicker shows two, consecutive.)
    bool IsVisible(DateTime day)
    {
        var month = FirstOfMonth(day);
        return month == _viewMonth || month == _viewMonth.AddMonths(1);
    }

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

    // ----- Prev/next month navigation ----------------------------------------

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
    void Open()
    {
        _open = true;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _startEdit = _endEdit = null;
        _focusDay = null;
        _pendingInputFocus = false;
        // Anchor the left panel on the start of the current value; with only an end set, put that
        // end in the right panel so it's visible on open.
        var anchor = Start ?? End ?? DateTime.Today;
        _viewMonth = ClampView(FirstOfMonth(anchor), Start is null && End is not null ? -1 : 0);
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
                // Reaching the wrapper's keydown at all means some descendant (an input, a day
                // button, a month/year select, a preset) had focus — restore it to the active input
                // on close.
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

    async Task OnDayClickAsync(DateTime day)
    {
        // A calendar pick supersedes any half-typed input text — drop it so the field previews
        // the pick instead of the stale keystrokes.
        _startEdit = _endEdit = null;

        if (!_selecting)
        {
            // First click starts a fresh range (matching AntD — the old range is replaced, not
            // extended) and moves the active underline to the end input.
            _selecting = true;
            _pendingStart = day;
            _hoverDay = null;
            _activeInput = 1;
            _focusDay = day;
            return;
        }

        var start = _pendingStart!.Value;
        _selecting = false;
        _pendingStart = null;
        _hoverDay = null;
        _focusDay = day;
        await SetRangeAsync(start, day);
        _pendingInputFocus = true; // the clicked day button is about to unmount
        await CloseAsync();
    }

    // Hover-range preview: only tracked while a pick is in progress, so hovering the other 83 cells
    // of an idle grid never triggers a render.
    void OnDayPointerEnter(DateTime day)
    {
        if (!_selecting || _hoverDay == day) return;
        _hoverDay = day;
    }

    void OnGridPointerLeave()
    {
        if (_hoverDay is not null) _hoverDay = null;
    }

    async Task OnPresetClickAsync(DateRangePreset preset)
    {
        _startEdit = _endEdit = null;
        var (start, end) = preset.Resolve();
        start = start.Date;
        end = end.Date;
        if (end < start) (start, end) = (end, start);
        // Clamp BOTH endpoints into [Min, Max] so a preset can never commit days the calendar itself
        // would disable — a preset resolving entirely beyond Max (or entirely before Min) collapses
        // to Max..Max (or Min..Min) instead of committing out-of-range days at one end.
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
        if (!TryParseDate(text, out var day) || IsDayDisabled(day)) return;
        await SetRangeAsync(day, End);
        _viewMonth = ClampView(FirstOfMonth(day));
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
        if (!TryParseDate(text, out var day) || IsDayDisabled(day)) return;
        await SetRangeAsync(Start, day);
        _viewMonth = ClampView(FirstOfMonth(day), -1);
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

    // Central commit: normalizes to dates, swaps a backwards pair, and raises only the callbacks
    // whose side actually changed.
    async Task SetRangeAsync(DateTime? start, DateTime? end)
    {
        start = start?.Date;
        end = end?.Date;
        if (start is { } s && end is { } e && e < s) (start, end) = (end, start);

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
