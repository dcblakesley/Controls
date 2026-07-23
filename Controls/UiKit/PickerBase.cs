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
    protected IJSObjectReference? _module;
    protected IJSObjectReference? _pickerModule;
    protected bool _open;
    protected bool _positioned;
    // Set first thing in DisposeAsync so an import that completes after disposal disposes its
    // module instead of stranding it on a dead instance (see GetModuleAsync).
    protected bool _disposed;
    // One-time input-wiring guard (initPicker) -- the input(s) are always rendered (not inside an
    // @if), so once is enough regardless of open state.
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
    // Every picker-internal format and the typed-input parse route through this culture.
    protected CultureInfo PickerCulture => GregorianCultureHelper.Gregorian(CultureInfo.CurrentCulture);

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

    // ----- JS interop (module lifecycle) --------------------------------------

    // Imports the RCL-local module once, re-checking _disposed after the awaited import so a
    // dispose that raced an in-flight import cleans up here instead of stranding the reference.
    // Returns null when disposed or JS is unavailable (prerender, bUnit) — callers no-JS degrade.
    protected async Task<IJSObjectReference?> GetModuleAsync()
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
    // grid(s) by keyboard doesn't pay for it, and so this stays decoupled from the unrelated overlay code.
    protected async Task<IJSObjectReference?> GetPickerNavModuleAsync()
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

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // One-time input/wrapper wiring (Enter form-submit suppression + focus-out close).
        if (!_inputsWired)
        {
            _inputsWired = true;
            var module = await GetModuleAsync();
            if (module is not null)
            {
                try
                {
                    await WireInputsAsync(module);
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
                if (_module is not null) await _module.InvokeVoidAsync("clearZ", _wrapperRef);
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
            var navModule = await GetPickerNavModuleAsync();
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
    /// Disposes the imported JS modules. Sets <see cref="_disposed"/> first so an import racing this
    /// call (<see cref="GetModuleAsync"/>/<see cref="GetPickerNavModuleAsync"/>) disposes its own
    /// late-assigned module instead of stranding it on this dead instance. Virtual so a subclass with
    /// its own disposable state can extend it (neither current subclass needs to).
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
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
