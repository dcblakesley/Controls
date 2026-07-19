using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An AntDesign-style single-date picker: a text field with a calendar suffix that opens a
/// dropdown with a one-month calendar whose header is month/year quick-select dropdowns. Picking a
/// day (or typing a date and pressing Enter) commits and closes. Date-only — the committed value is
/// a midnight local date.
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

    /// <summary>Earliest selectable day (inclusive). Days before it are disabled.</summary>
    [Parameter] public DateTime? Min { get; set; }
    /// <summary>Latest selectable day (inclusive). Days after it are disabled.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>Display and primary parse format for the input. Typed text is parsed with this
    /// exact format first, then with the current culture's general date parsing. Defaults to
    /// <c>MM/dd/yyyy</c> (the Figma spec).</summary>
    [Parameter] public string Format { get; set; } = "MM/dd/yyyy";

    /// <summary>Input placeholder. Defaults to "Select date" (the Figma spec). Override to localize.</summary>
    [Parameter] public string Placeholder { get; set; } = "Select date";

    /// <summary>Shows a clear button (over the calendar icon) while a value is set. Defaults to true.</summary>
    [Parameter] public bool AllowClear { get; set; } = true;

    /// <summary>Disables all interaction.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Field width as a CSS length (e.g. "300px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>First day of the week for the calendar grid. Null (default) follows
    /// <see cref="CultureInfo.CurrentCulture"/>.</summary>
    [Parameter] public DayOfWeek? FirstDayOfWeek { get; set; }

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

    string DayClass(DateTime day)
    {
        var cls = "wss-picker-day";
        if (day.Month != _viewMonth.Month) cls += " wss-picker-day-outside";
        if (day == DateTime.Today) cls += " wss-picker-day-today";
        if (day == Value?.Date) cls += " wss-picker-day-selected";
        return cls;
    }

    bool IsDayDisabled(DateTime day) =>
        (Min is { } min && day < min.Date) || (Max is { } max && day > max.Date);

    string MonthName(int month) =>
        CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedMonthNames[month - 1];

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
        if (Value is { } v && IsVisible(v.Date)) return v.Date;
        if (IsVisible(DateTime.Today)) return DateTime.Today;
        return _viewMonth;
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

    bool PrevMonthDisabled => ClampView(_viewMonth.AddMonths(-1)) == _viewMonth;
    bool NextMonthDisabled => ClampView(_viewMonth.AddMonths(1)) == _viewMonth;

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
        if (!Disabled && !_open) Open();
    }

    void Open()
    {
        _open = true;
        _edit = null;
        _focusDay = null;
        _viewMonth = ClampView(FirstOfMonth(Value ?? DateTime.Today));
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
                if (_open) await CloseAsync();
                break;
            case "Enter":
                if (await CommitTextAsync()) await CloseAsync();
                break;
        }
    }

    async Task OnDayClickAsync(DateTime day)
    {
        // A calendar pick supersedes any half-typed input text.
        _edit = null;
        await SetValueAsync(day);
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
        if (!TryParseDate(text, out var day) || IsDayDisabled(day)) return false;
        await SetValueAsync(day);
        _viewMonth = ClampView(FirstOfMonth(day));
        return true;
    }

    // Central commit: normalizes to a date and raises the callback only when it actually changed.
    async Task SetValueAsync(DateTime? value)
    {
        value = value?.Date;
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
