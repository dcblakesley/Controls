namespace Controls;

/// <summary>
/// Shared JS-interop and dropdown-overlay lifecycle for the AntD-style picker controls
/// (<see cref="DatePicker"/>, <see cref="DateRangePicker"/>): the <c>wss-overlay.js</c>/<c>wss-picker.js</c>
/// module import/dispose pair, the open/positioned/close <see cref="OnAfterRenderAsync"/> render
/// cycle (panel placement + z-index mirroring, roving-tabindex grid keyboard-nav init, and the
/// focus-reclaim-on-close handoff), and the roving-tabindex DOM-focus follow. Every JS call degrades
/// gracefully to a no-JS fallback (prerender, bUnit) via try/catch, matching each subclass's own
/// documented degrade contract.
/// </summary>
/// <remarks>
/// Subclasses own everything mode/range-specific — panel content, day/cell classing, and
/// commit/selection state — and plug into the shared render cycle through <see cref="WireInputsAsync"/>,
/// <see cref="GridRefs"/>, and <see cref="FocusReclaimTarget"/>. Not a public extensibility point for
/// consumers: both implementations live in this assembly, and the shared template in
/// <see cref="OnAfterRenderAsync"/> is sealed via method-not-virtual (the abstract hooks are the only
/// customization surface).
/// </remarks>
public abstract class PickerBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] protected FormDefaults? FormDefaults { get; set; }

    // ----- Shared JS-interop + overlay state ---------------------------------

    protected ElementReference _wrapperRef;
    protected ElementReference _panelRef;
    // wss-overlay.js (panel placement / z-index) and wss-picker.js (grid keyboard nav) -- two holders,
    // because a consumer that never drives the grid(s) by keyboard shouldn't pay for the second import.
    // JsModule owns the once-only import, the dispose-raced-the-import guard, and the no-JS degrade
    // (null return) that every call site below reads as "take the CSS/no-JS fallback".
    readonly JsModule _module = new("wss-overlay.js");
    readonly JsModule _pickerModule = new("wss-picker.js");
    protected bool _open;
    protected bool _positioned;
    // Set first thing in DisposeAsync, as the instance-is-dead signal for a subclass DisposeAsync
    // override. The module-import race this used to gate is now JsModule's own business.
    protected bool _disposed;
    // One-time input-wiring guard (initPicker) -- the input(s) are always rendered (not inside an
    // @if), so once is enough regardless of open state. Set only after the wiring actually SUCCEEDS
    // (see OnAfterRenderAsync): latching before the awaited import would strand the picker without its
    // focus-out dismiss wiring for good on one transient import failure, when JsModule's own contract
    // is that a failed import retries on the next render.
    protected bool _inputsWired;
    // The open-order z-index placePanel assigned this wrapper (null while closed). C# owns it so a
    // Blazor re-render of the bound wrapper style re-asserts the value JS wrote to the DOM.
    protected int? _openZIndex;
    // The day the grid's roving tabindex currently targets (null = not yet keyboard-navigated;
    // each subclass computes its own AntD-style default in that case). Arrow-key navigation sets
    // this, and it survives a month flip (unlike DOM focus, which the re-rendered grid loses) so
    // subsequent arrow presses keep stepping from the right day.
    protected DateTime? _focusDay;
    // Set by grid keyboard navigation and consumed by the next OnAfterRenderAsync to move real DOM
    // focus via JS. An ElementReference can't be captured here: a month-crossing move re-renders the
    // grid with brand-new button instances, so the previously focused element is gone by the time
    // OnAfterRenderAsync runs — this hands the *date* across the render instead, and wss-picker.js's
    // focusDay looks up the new button by its data-date attribute.
    protected DateTime? _pendingFocusDate;
    // Set true right before a CloseAsync() call that was triggered by a panel-originated action
    // (day click/Enter commit/Escape) — anything that means focus was on some now-unmounting element
    // inside the wrapper. Consumed by the very next OnAfterRenderAsync's closing branch to move
    // focus back onto FocusReclaimTarget, so it doesn't fall through to <body>. Left false (the
    // default) for an outside/backdrop close, which must NOT steal focus from wherever the user clicked.
    protected bool _pendingInputFocus;
    // The input opens the panel on focus (OnInputFocus), so the programmatic focus-reclaim above
    // would immediately bounce the panel back open. Set around the FocusAsync call and consumed by
    // OnInputFocus to swallow exactly that one reopen; cleared unconditionally after the call as a
    // backstop so a swallowed/never-fired focus event can't eat a later genuine focus-open.
    protected bool _suppressOpenOnFocus;

    // The picker is a Gregorian-calendar control — see GregorianCultureHelper for the contract.
    // Every picker-internal format and the typed-input parse route through this culture. Also
    // `internal` so the shared panel pieces (PickerTimeRowSlot) can read it off the picker they
    // render for, without each subclass having to forward it.
    protected internal CultureInfo PickerCulture => GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);

    // Appends the C#-owned open z-index (see _openZIndex) as a trailing CSS declaration onto
    // `prefix` (a subclass's own base inline style, or null) -- shared by DatePicker/
    // DateRangePicker's WrapperStyle, which differ only in what they prepend (DateRangePicker also
    // carries a Width declaration). Cleared on every close path (both _positioned's else-branch
    // here and each subclass's own CloseAsync null it out).
    protected string? ZIndexStyle(string? prefix) =>
        _openZIndex is null ? prefix : $"{prefix}z-index:{_openZIndex};";

    // ----- Abstract hooks for the shared OnAfterRenderAsync template ---------

    /// <summary>Wires the always-rendered input(s) once, via the already-imported <paramref name="module"/>'s
    /// <c>initPicker</c> (Enter form-submit suppression + focus-out close) — called exactly once, on
    /// whichever after-render first succeeds in importing the module, regardless of open state.
    /// Implementations pass their own one or two input <see cref="ElementReference"/>s.</summary>
    protected abstract ValueTask WireInputsAsync(IJSObjectReference module);

    /// <summary>The grid element(s) whose roving-tabindex keyboard navigation the shared
    /// <c>wss-picker.js</c> module initializes on open — one for <see cref="DatePicker"/>, two
    /// (start/end panels) for <see cref="DateRangePicker"/>.</summary>
    protected abstract IEnumerable<ElementReference> GridRefs { get; }

    /// <summary>The input element that should reclaim DOM focus when the panel closes after a
    /// panel-originated action (see <see cref="_pendingInputFocus"/>) — the sole input for
    /// <see cref="DatePicker"/>, or whichever of start/end was active for
    /// <see cref="DateRangePicker"/>.</summary>
    protected abstract ElementReference FocusReclaimTarget { get; }

    // ----- Shared display/parse layer ----------------------------------------
    // FormatDate/TryParseDate are character-identical between the two pickers apart from which mode
    // each keys off, so they live here once, over the five hooks below. Everything in this section
    // and the next is `internal`, not protected: both implementations live in this assembly and
    // PickerBase is not a public extensibility point (see the class remarks), so none of it widens
    // the consumer-facing API. Internal *virtual* with a working default rather than internal
    // abstract, so even a (nominally impossible) external subclass keeps compiling.

    /// <summary>The mode <see cref="FormatDate"/>/<see cref="TryParseDate"/> key off: the raw
    /// <c>Mode</c> parameter for <see cref="DatePicker"/>, and <see cref="DateRangePicker"/>'s own
    /// calendar-shape fold of it (which collapses its DateTime/Time pick-session modes onto Date) for
    /// the range picker. Both subclasses override this; the default is never observed.</summary>
    internal virtual DatePickerMode EffectiveMode => DatePickerMode.Date;

    /// <summary>The subclass's own <c>Format</c> parameter, exactly as the consumer set it — null means
    /// "no explicit format", which is what enables Quarter's/Week's shorthand display/parse below (an
    /// explicit format is always used verbatim, per each picker's <c>Format</c> doc comment). Distinct
    /// from <see cref="EffectiveFormat"/>, which substitutes the mode-derived default for null.</summary>
    internal virtual string? ExplicitFormat => null;

    /// <summary>The format string actually used for <c>ToString</c>/<c>TryParseExact</c>:
    /// <see cref="ExplicitFormat"/> when set, else the subclass's mode-derived default (see
    /// <see cref="PickerMath.ModeDisplayFormat"/>). A subclass hook rather than shared code here
    /// because each picker derives its default from its own raw <c>Mode</c> — deliberately NOT
    /// <see cref="EffectiveMode"/>, so DateTime/Time keep a time-aware format instead of the range
    /// picker's Date fold.</summary>
    internal virtual string EffectiveFormat => "MM/dd/yyyy";

    /// <summary>The first day of the calendar week, per the subclass's own <c>FirstDayOfWeek</c>
    /// parameter with a culture fallback (see <see cref="PickerMath.FirstDayOfWeekOrCulture"/>) —
    /// consumed here by Week mode's shorthand display/parse, and by each subclass's own grid layout
    /// and Home/End navigation.</summary>
    internal virtual DayOfWeek EffectiveFirstDayOfWeek => PickerCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Normalizes a committed value to the subclass's own per-mode granularity, so every
    /// commit path (click, typed text, select change) lands on the same shape of value — see
    /// <see cref="PickerMath.NormalizeForMode"/> and each override's own doc comment (the range
    /// picker's differs: its DateTime/Time endpoints must keep the date they were composed with).</summary>
    internal virtual DateTime NormalizeForMode(DateTime value) => value.Date;

    /// <summary>
    /// The field text for <paramref name="value"/>: Quarter's and Week's null-<see cref="ExplicitFormat"/>
    /// displays bypass <c>ToString(EffectiveFormat)</c> entirely (no .NET format token renders a quarter
    /// or a week number — see <see cref="PickerMath.FormatQuarterDisplay"/>/<see cref="PickerMath.FormatWeekDisplay"/>);
    /// everything else formats through <see cref="EffectiveFormat"/> in <see cref="PickerCulture"/>. An
    /// explicitly-set <c>Format</c> always takes the verbatim <c>ToString</c> path, including in those two
    /// modes.
    /// </summary>
    internal string FormatDate(DateTime? value)
    {
        if (value is not { } v) return string.Empty;
        if (EffectiveMode == DatePickerMode.Quarter && ExplicitFormat is null)
        {
            return PickerMath.FormatQuarterDisplay(v, PickerCulture);
        }
        if (EffectiveMode == DatePickerMode.Week && ExplicitFormat is null)
        {
            return PickerMath.FormatWeekDisplay(v, PickerCulture, EffectiveFirstDayOfWeek);
        }
        return v.ToString(EffectiveFormat, PickerCulture);
    }

    /// <summary>
    /// The exact inverse of <see cref="FormatDate"/> for typed text: Quarter's <c>"yyyy-Qn"</c> and Week's
    /// <c>"yyyy-Www"</c> shorthands first (null-<see cref="ExplicitFormat"/> only, mirroring
    /// <see cref="FormatDate"/>'s own special cases), then <see cref="EffectiveFormat"/> as an exact
    /// format, then <see cref="PickerCulture"/>'s general parse — normalizing whatever the general parse
    /// produced to <see cref="NormalizeForMode"/>'s own granularity, so a typed commit and a click/select
    /// commit always land on the same shape of value. A plain typed date still falls through to the
    /// general parse in Quarter/Week mode and normalizes to its own quarter/week, same as every other
    /// mode's typed-text path.
    /// </summary>
    internal bool TryParseDate(string text, out DateTime value)
    {
        if (EffectiveMode == DatePickerMode.Quarter && ExplicitFormat is null &&
            PickerMath.TryParseQuarterShorthand(text, out value))
        {
            return true;
        }
        if (EffectiveMode == DatePickerMode.Week && ExplicitFormat is null &&
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

    // ----- Shared time-row layer (Mode.Time / Mode.DateTime) ------------------
    // Both pickers render the SAME <PickerTimeRow> (see that component for the three render-time
    // invariants it owns) over the same displayed hour/minute/second/period and the same stepped
    // option lists -- the only difference is WHICH value the row reflects, which is the TimeRowValue
    // hook below: DatePicker's bound Value (a select change commits immediately) vs. DateRangePicker's
    // ACTIVE endpoint's own resolved session value (a select change writes pending state; OK commits).

    /// <summary>The value whose time-of-day the shared time row displays and edits. Null (this default,
    /// and either subclass with nothing resolved yet) reads as midnight — matching AntD's "12 AM /
    /// 00:00" default.</summary>
    internal virtual DateTime? TimeRowValue => null;

    internal int TimeRowHour => TimeRowValue?.Hour ?? 0;
    internal int TimeRowMinute => TimeRowValue?.Minute ?? 0;
    internal int TimeRowSecond => TimeRowValue?.Second ?? 0;

    /// <summary>Whether the displayed hour falls in the PM half of the day — the default
    /// <see cref="TimeRowHour"/> of 0 is AM. Drives <c>Use12Hours</c>' period select and the hour
    /// option list's period filtering (see <see cref="PickerMath.HourOptions"/>).</summary>
    internal bool TimeRowIsPM => TimeRowHour >= 12;

    /// <summary><c>HourStep</c>/<c>MinuteStep</c>/<c>SecondStep</c> clamped to &gt;= 1 at the point of
    /// use (never thrown) — the raw parameters stay whatever a consumer set (even 0 or negative) so
    /// nothing but option-list construction ever second-guesses them.</summary>
    internal static int EffectiveStep(int step) => Math.Max(1, step);

    // ----- The rest of the row's inputs, as subclass hooks --------------------
    // Each of these is one of the two subclasses' own [Parameter]s (each owns its differently-worded
    // public doc comment) forwarded to a single name here, so PickerTimeRowSlot can render the row's
    // 20-argument invocation ONCE for both pickers instead of each .razor transcribing it. The
    // defaults are never observed -- both subclasses override every one.

    /// <summary>The caller's already-invoked <c>DisabledTime</c> result for the row's own date part
    /// (invoked exactly ONCE per row render, never per option — see <see cref="PickerTimeRow"/>'s
    /// first invariant). DatePicker's own <c>DisabledTime</c> against the bound value's date;
    /// DateRangePicker's ACTIVE endpoint's callback against that endpoint's own resolved date.</summary>
    internal virtual DisabledTimeParts? TimeRowDisabledParts => null;

    internal virtual bool TimeRowShowSeconds => true;
    internal virtual bool TimeRowUse12Hours => false;
    internal virtual bool TimeRowHideDisabledOptions => false;
    internal virtual int TimeRowHourStep => 1;
    internal virtual int TimeRowMinuteStep => 1;
    internal virtual int TimeRowSecondStep => 1;
    internal virtual string? TimeRowHourLabel => null;
    internal virtual string? TimeRowMinuteLabel => null;
    internal virtual string? TimeRowSecondLabel => null;
    internal virtual string? TimeRowPeriodLabel => null;

    // The hour/minute/second values each of the row's selects offers, before DisabledTime hides/
    // disables any of them -- see PickerMath.HourOptions/SteppedOptions for the full contract
    // (never-jump rule, Use12Hours period filtering).
    internal IEnumerable<int> TimeRowHourOptions =>
        PickerMath.HourOptions(EffectiveStep(TimeRowHourStep), TimeRowHour, TimeRowUse12Hours);

    internal IEnumerable<int> TimeRowMinuteOptions =>
        PickerMath.SteppedOptions(59, EffectiveStep(TimeRowMinuteStep), TimeRowMinute);

    internal IEnumerable<int> TimeRowSecondOptions =>
        PickerMath.SteppedOptions(59, EffectiveStep(TimeRowSecondStep), TimeRowSecond);

    // The row's four change callbacks, bound with THIS PICKER as the EventCallback receiver rather
    // than the PickerTimeRowSlot that renders the row. The receiver is what the renderer calls
    // StateHasChanged on after the handler runs, and every one of these mutates picker state (an
    // immediate commit, or the pick session's pending value) that the picker's own panel must
    // re-render for -- binding them inside the slot would leave the picker showing stale selects.
    internal EventCallback<ChangeEventArgs> TimeRowHourChanged =>
        EventCallback.Factory.Create<ChangeEventArgs>(this, OnHourSelectChangedAsync);

    internal EventCallback<ChangeEventArgs> TimeRowMinuteChanged =>
        EventCallback.Factory.Create<ChangeEventArgs>(this, OnMinuteSelectChangedAsync);

    internal EventCallback<ChangeEventArgs> TimeRowSecondChanged =>
        EventCallback.Factory.Create<ChangeEventArgs>(this, OnSecondSelectChangedAsync);

    internal EventCallback<ChangeEventArgs> TimeRowPeriodChanged =>
        EventCallback.Factory.Create<ChangeEventArgs>(this, OnPeriodSelectChangedAsync);

    /// <summary>
    /// Applies one changed time part (the other two are null) to <see cref="TimeRowValue"/>'s own
    /// date+time — see <see cref="PickerMath.ComposeTimePart"/> for the compose both overrides share,
    /// and each override for where the composed value goes (an immediate commit vs. pending session
    /// state) and which <c>DisabledTime</c> list guards it. The no-op default is never reached: only the
    /// four handlers below call this, and they only render inside a time row.
    /// </summary>
    internal virtual Task ApplyTimePartAsync(int? hour, int? minute, int? second) => Task.CompletedTask;

    // The time row's four @onchange handlers. A malformed/unparseable event value is a no-op (the
    // select's own displayed value reverts to TimeRowValue's on the next render) -- the same permissive
    // fallback the period select gives anything that isn't exactly "PM".
    internal Task OnHourSelectChangedAsync(ChangeEventArgs e) =>
        TryParseTimePartValue(e, out var hour) ? ApplyTimePartAsync(hour, null, null) : Task.CompletedTask;

    internal Task OnMinuteSelectChangedAsync(ChangeEventArgs e) =>
        TryParseTimePartValue(e, out var minute) ? ApplyTimePartAsync(null, minute, null) : Task.CompletedTask;

    internal Task OnSecondSelectChangedAsync(ChangeEventArgs e) =>
        TryParseTimePartValue(e, out var second) ? ApplyTimePartAsync(null, null, second) : Task.CompletedTask;

    /// <summary><c>Use12Hours</c>' period select: re-applies the CURRENT hour shifted into the other
    /// period, through the same <see cref="ApplyTimePartAsync"/> every other time-row change routes
    /// through (so it gets the same DisabledTime guard and in-progress-text clearing). "PM" is the only
    /// value that flips the shift — anything else, including a malformed event, is treated as AM.</summary>
    internal Task OnPeriodSelectChangedAsync(ChangeEventArgs e)
    {
        var isPM = string.Equals(e.Value?.ToString(), "PM", StringComparison.Ordinal);
        return ApplyTimePartAsync(TimeRowHour % 12 + (isPM ? 12 : 0), null, null);
    }

    static bool TryParseTimePartValue(ChangeEventArgs e, out int value) =>
        int.TryParse(e.Value?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    // ----- Shared render cycle + module lifecycle -----------------------------
    // Both JS modules are held by the JsModule fields declared at the top of the class: every call
    // below is `GetAsync` (import-once, null = no JS or disposed → take the fallback) or `Current`
    // (use it only if it already imported), and DisposeAsync hands both back.

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // One-time input/wrapper wiring (Enter form-submit suppression + focus-out close).
        if (!_inputsWired)
        {
            var module = await _module.GetAsync(JS, FormDefaults);
            if (module is not null)
            {
                try
                {
                    await WireInputsAsync(module);
                    _inputsWired = true; // latch on success only, so a failed attempt retries next render
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
            var module = await _module.GetAsync(JS, FormDefaults);
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

            var navModule = await _pickerModule.GetAsync(JS, FormDefaults);
            if (navModule is not null)
            {
                foreach (var gridRef in GridRefs)
                {
                    try
                    {
                        await navModule.InvokeVoidAsync("init", gridRef);
                    }
                    catch
                    {
                        // No JS — arrow keys still update the roving-tabindex state, just without the
                        // native page-scroll suppression.
                    }
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
                // Current, not GetAsync: nothing was ever assigned if the module never imported, so
                // there is nothing to clear and no reason to import one now just to undo it.
                if (_module.Current is { } overlay) await overlay.InvokeVoidAsync("clearZ", _wrapperRef);
            }
            catch
            {
                // No JS runtime / module — nothing was assigned, nothing to clear.
            }

            if (_pendingInputFocus)
            {
                // The panel subtree (whatever had focus) just unmounted — reclaim focus onto
                // FocusReclaimTarget rather than leaving it stranded on <body>. Best-effort:
                // FocusAsync throws if the element isn't actually focusable yet (prerender/tests).
                _pendingInputFocus = false;
                var target = FocusReclaimTarget;
                _suppressOpenOnFocus = true;
                try { await target.FocusAsync(); } catch { /* not focusable yet (prerender/tests) */ }
                // Normally consumed by OnInputFocus during the call (the focus event outruns the
                // interop ack on both runtimes); this backstop covers a failed/eventless focus.
                _suppressOpenOnFocus = false;
            }
        }

        if (_open && _pendingFocusDate is { } focusDate)
        {
            _pendingFocusDate = null;
            var navModule = await _pickerModule.GetAsync(JS, FormDefaults);
            if (navModule is not null)
            {
                try
                {
                    // Searched against the whole panel (every grid) — whichever one currently shows
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

    /// <summary>
    /// Disposes the imported JS modules — each holder flips itself closed first, so an import racing
    /// this call disposes its own late-arriving module instead of stranding it on this dead instance.
    /// Virtual so a subclass with its own disposable state can extend it (neither current subclass
    /// needs to).
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _module.DisposeAsync();
        await _pickerModule.DisposeAsync();
    }
}
