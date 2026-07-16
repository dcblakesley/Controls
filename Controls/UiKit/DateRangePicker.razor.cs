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
/// <c>@bind-End</c>. JS interop (viewport flip/clamp, form-submit suppression, focus-out close)
/// degrades gracefully: without JS the dropdown opens below the field at the CSS default placement
/// and everything remains clickable.
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
    IJSObjectReference? _module;
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
    // First-of-month shown in the left panel; the right panel is always _viewMonth + 1 month.
    DateTime _viewMonth = FirstOfMonth(DateTime.Today);
    // In-progress typed text per input (null = show the formatted bound value).
    string? _startEdit;
    string? _endEdit;

    // ----- Inline icons (AntD glyphs, no icon-font dependency; matches Select) ----

    static readonly MarkupString CalendarIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M880 184H712v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H384v-64c0-4.4-3.6-8-8-8h-56c-4.4 0-8 3.6-8 8v64H144c-17.7 0-32 14.3-32 32v664c0 17.7 14.3 32 32 32h736c17.7 0 32-14.3 32-32V216c0-17.7-14.3-32-32-32zm-40 656H184V460h656v380zM184 392V256h128v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h256v48c0 4.4 3.6 8 8 8h56c4.4 0 8-3.6 8-8v-48h128v136H184z\"/></svg>");

    static readonly MarkupString SwapRightIcon = new(
        "<svg viewBox=\"0 0 1024 1024\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M873.1 596.2l-164-208A32 32 0 00684 376h-64.8c-6.7 0-10.4 7.7-6.3 13l144.3 183H152c-4.4 0-8 3.6-8 8v60c0 4.4 3.6 8 8 8h695.9c26.8 0 41.7-30.8 25.2-51.8z\"/></svg>");

    static readonly MarkupString CloseCross = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M563.8 512l262.5-312.9c4.4-5.2.7-13.1-6.1-13.1h-79.8c-4.7 0-9.2 2.1-12.3 5.7L511.6 449.8 295.1 191.7c-3-3.6-7.5-5.7-12.3-5.7H203c-6.8 0-10.5 7.9-6.1 13.1L459.4 512 196.9 824.9A7.95 7.95 0 00203 838h79.8c4.7 0 9.2-2.1 12.3-5.7l216.5-258.1 216.5 258.1c3 3.6 7.5 5.7 12.3 5.7h79.8c6.8 0 10.5-7.9 6.1-13.1L563.8 512z\"/></svg>");

    static readonly MarkupString DownIcon = new(
        "<svg class=\"wss-picker-select-arrow\" viewBox=\"64 64 896 896\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M884 256h-75c-5.1 0-9.9 2.5-12.9 6.6L512 654.2 227.9 262.6c-3-4.1-7.8-6.6-12.9-6.6h-75c-6.5 0-10.3 7.4-6.5 12.7l352.6 486.1c12.8 17.6 39 17.6 51.7 0l352.6-486.1c3.9-5.3.1-12.7-6.4-12.7z\"/></svg>");

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
        return (from, to);
    }

    DayOfWeek EffectiveFirstDayOfWeek =>
        FirstDayOfWeek ?? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    // A fixed 6-row (42-cell) grid — covers every month/first-day combination, so the panel height
    // never jumps while navigating. Leading/trailing cells are the adjacent months' days.
    IEnumerable<DateTime> GridDays(DateTime month)
    {
        var lead = ((int)month.DayOfWeek - (int)EffectiveFirstDayOfWeek + 7) % 7;
        var start = month.AddDays(-lead);
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
        _startEdit = _endEdit = null;
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
        _startEdit = _endEdit = null;
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
            _activeInput = 1;
            return;
        }

        var start = _pendingStart!.Value;
        _selecting = false;
        _pendingStart = null;
        await SetRangeAsync(start, day);
        await CloseAsync();
    }

    async Task OnPresetClickAsync(DateRangePreset preset)
    {
        _startEdit = _endEdit = null;
        var (start, end) = preset.Resolve();
        start = start.Date;
        end = end.Date;
        if (end < start) (start, end) = (end, start);
        // Clamp into Min/Max so a preset can never commit days the calendar itself would disable.
        if (Min is { } min && start < min.Date) start = min.Date;
        if (Max is { } max && end > max.Date) end = max.Date;
        if (end < start) end = start;

        _selecting = false;
        _pendingStart = null;
        await SetRangeAsync(start, end);
        await CloseAsync();
    }

    async Task ClearAsync()
    {
        if (Disabled) return;
        _selecting = false;
        _pendingStart = null;
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
            return;
        }
        // Invalid or out-of-range text reverts to the formatted bound value (edit state cleared above).
        if (!TryParseDate(text, out var day) || IsDayDisabled(day)) return;
        await SetRangeAsync(day, End);
        _viewMonth = ClampView(FirstOfMonth(day));
    }

    async Task CommitEndTextAsync()
    {
        if (_endEdit is null) return;
        var text = _endEdit.Trim();
        _endEdit = null;
        if (text.Length == 0)
        {
            if (End is not null) await SetRangeAsync(Start, null);
            return;
        }
        if (!TryParseDate(text, out var day) || IsDayDisabled(day)) return;
        await SetRangeAsync(Start, day);
        _viewMonth = ClampView(FirstOfMonth(day), -1);
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
    }

    void OnYearSelectChanged(int panel, ChangeEventArgs e)
    {
        if (!int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)) return;
        var shown = _viewMonth.AddMonths(panel);
        _viewMonth = ClampView(new DateTime(year, shown.Month, 1), -panel);
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
    }

    public async ValueTask DisposeAsync()
    {
        // Set first: an import in flight (GetModuleAsync) re-checks this after its await and
        // disposes its own late-assigned module rather than stranding it on this dead instance.
        _disposed = true;
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { /* circuit may be gone */ }
            _module = null;
        }
    }
}
