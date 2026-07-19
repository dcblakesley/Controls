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
public partial class DateRangePicker : IAsyncDisposable
{
    [Inject] IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

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
    ElementReference _startInputRef;
    ElementReference _endInputRef;
    // Index 0 = left panel's grid, 1 = right panel's — see the ant-design-blazor / procurement-hub
    // precedent for @ref into an array element inside a @for loop.
    readonly ElementReference[] _gridRefs = new ElementReference[2];
    IJSObjectReference? _module;
    IJSObjectReference? _pickerModule;
    bool _open;
    bool _positioned;
    // Set first thing in DisposeAsync so an import that completes after disposal disposes its
    // module instead of stranding it on a dead instance (see GetModuleAsync).
    bool _disposed;
    bool _inputsWired;
    // The open-order z-index placePanel assigned this wrapper (null while closed). C# owns it so a
    // Blazor re-render of the bound wrapper style re-asserts the value JS wrote to the DOM.
    int? _openZIndex;
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
    // The day the grids' roving tabindex currently targets (null = not yet keyboard-navigated;
    // EffectiveFocusDay computes the AntD-style default in that case). Arrow-key navigation sets
    // this, and it survives a month flip (unlike DOM focus, which the re-rendered grid loses) so
    // subsequent arrow presses keep stepping from the right day.
    DateTime? _focusDay;
    // Set by grid keyboard navigation and consumed by the next OnAfterRenderAsync to move real DOM
    // focus via JS. An ElementReference can't be captured here: a month-crossing move re-renders the
    // grids with brand-new button instances, so the previously focused element is gone by the time
    // OnAfterRenderAsync runs — this hands the *date* across the render instead, and wss-picker.js's
    // focusDay looks up the new button by its data-date attribute (searched across both panels).
    DateTime? _pendingFocusDate;

    // ----- Inline icons (AntD glyphs, no icon-font dependency; matches Select) ----

    static readonly MarkupString CalendarIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M880 184H712v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H384v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H144c-17.7 0-32 14.3-32 32v664c0 17.7 14.3 32 32 32h736c17.7 0 32-14.3 32-32V216c0-17.7-14.3-32-32-32zm-40 656H184V460h656v380zM184 392V256h128v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h256v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h128v136H184z\"/></svg>");

    static readonly MarkupString SwapRightIcon = new(
        "<svg viewBox=\"0 0 1024 1024\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M873.1 596.2l-164-208A32 32 0 00684 376h-64.8c-6.7 0-10.4 7.7-6.3 13l144.3 183H152c-4.4 0-8 3.6-8 8v60c0 4.4 3.6 8 8 8h695.9c26.8 0 41.7-30.8 25.2-51.8z\"/></svg>");

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
            return _openZIndex is null ? width : $"{width}z-index:{_openZIndex};";
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

    string MonthName(int month) =>
        CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[month - 1];

    // The years offered by a panel's year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — always including the displayed year itself so the select never shows a value
    // that isn't in its option list.
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
        FirstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

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
            var names = CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
            for (var i = 0; i < 7; i++)
            {
                var name = names[((int)EffectiveFirstDayOfWeek + i) % 7];
                yield return name.Length <= 2 ? name : name[..2];
            }
        }
    }

    // The first day of the calendar week containing `day`, per EffectiveFirstDayOfWeek. Shared by
    // GridDays (the 42-cell layout) and Home/End keyboard navigation so they can never disagree.
    DateTime WeekStart(DateTime day)
    {
        var lead = ((int)day.DayOfWeek - (int)EffectiveFirstDayOfWeek + 7) % 7;
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

    string FormatDate(DateTime? value) =>
        value?.ToString(Format, CultureInfo.CurrentCulture) ?? string.Empty;

    bool TryParseDate(string text, out DateTime value)
    {
        if (DateTime.TryParseExact(text, Format, CultureInfo.CurrentCulture, DateTimeStyles.None, out value) ||
            DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out value))
        {
            value = value.Date;
            return true;
        }
        return false;
    }

    static DateTime FirstOfMonth(DateTime value) => new(value.Year, value.Month, 1);

    // The left panel's month, clamped so the +1-month right panel and the 42-cell grids can never
    // overflow DateTime's range (offsetMonths carries the panel adjustment through the clamp).
    static DateTime ClampView(DateTime firstOfMonth, int offsetMonths = 0)
    {
        var index = firstOfMonth.Year * 12 + (firstOfMonth.Month - 1) + offsetMonths;
        index = Math.Clamp(index, 1 * 12 + 1, 9998 * 12 + 10); // 0001-02 .. 9998-11
        return new DateTime(index / 12, index % 12 + 1, 1);
    }

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
        if (s is { } start && IsVisible(start)) return start;
        if (e is { } end && IsVisible(end)) return end;
        if (IsVisible(DateTime.Today)) return DateTime.Today;
        return _viewMonth;
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

    // Grid keydown (wired to both panels' grids): moves the roving-tabindex day, retargeting the
    // left panel's month only when navigation lands outside BOTH currently visible months (so a
    // move that's already covered by the other panel doesn't needlessly re-anchor the view). A day
    // that lands disabled (Min/Max) still becomes the focus target — only clicking commits, so
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
            _viewMonth = ClampView(FirstOfMonth(next.Value));
        }
        _pendingFocusDate = next.Value;
    }

    // ----- Prev/next month navigation ----------------------------------------

    bool PrevMonthDisabled => ClampView(_viewMonth.AddMonths(-1)) == _viewMonth;
    bool NextMonthDisabled => ClampView(_viewMonth.AddMonths(1)) == _viewMonth;

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
                if (_open) await CloseAsync();
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

    // ----- JS interop (mirrors Select's module lifecycle) ---------------------

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
    // grids by keyboard doesn't pay for it, and so this stays decoupled from the unrelated overlay code.
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
        // inputs are always rendered (not inside the @if), so once is enough.
        if (!_inputsWired)
        {
            _inputsWired = true;
            var module = await GetModuleAsync();
            if (module is not null)
            {
                try
                {
                    await module.InvokeVoidAsync("initPicker", _wrapperRef, _startInputRef, _endInputRef);
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
                    await navModule.InvokeVoidAsync("init", _gridRefs[0]);
                    await navModule.InvokeVoidAsync("init", _gridRefs[1]);
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
        }

        if (_open && _pendingFocusDate is { } focusDate)
        {
            _pendingFocusDate = null;
            var navModule = await GetPickerNavModuleAsync();
            if (navModule is not null)
            {
                try
                {
                    // Searched against the whole panel (both grids) — whichever one currently shows
                    // the date is the one that matches.
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
