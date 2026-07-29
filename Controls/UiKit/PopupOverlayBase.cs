using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// Shared floating-panel positioning + trigger-ARIA-sync + focus-restore engine for
/// <see cref="Popover"/> and <see cref="Popconfirm"/>. Imports <c>wss-overlay.js</c> once, mirrors
/// the popup ARIA onto the resolved trigger child (<c>syncTrigger</c>, invoked only when
/// <c>(open, disabled)</c> actually changes so a per-row instance stays cheap on ancestor
/// re-renders), places the panel via <c>place()</c> guarded by an <see cref="_activationSeq"/> race
/// token (mirrors <see cref="OverlayActivationBase"/>'s own), and restores focus to the trigger on
/// close.
/// </summary>
/// <remarks>
/// Subclasses plug in the placement string, the panel class prefix passed to <c>place()</c>, what to
/// pass as <c>syncTrigger</c>'s disabled argument, and what to focus once the panel is positioned
/// (<see cref="FocusPanelAsync"/> — Popover focuses the panel itself; Popconfirm defers focus onto
/// its primary action button). <see cref="ToggleAsync"/>/<see cref="CloseAsync"/> are virtual so
/// Popconfirm can layer its <c>Disabled</c>/pending-confirm-lock guard on top of the shared
/// <see cref="SetOpenAsync"/> behavior instead of reimplementing it. <c>OnParametersSetAsync</c> (the
/// controlled Visible/VisibleChanged sync) stays in each subclass — Popconfirm's extra invariants are
/// interleaved with, not wrapped around, the echo-detection there, so hoisting it here would fit
/// neither control cleanly.
/// </remarks>
public abstract class PopupOverlayBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] protected FormDefaults? FormDefaults { get; set; }

    protected ElementReference _triggerRef;
    protected ElementReference _panelRef;
    // JsModule owns the once-only import, the dispose-raced-the-import guard, and the no-JS degrade;
    // EnsureModuleAsync below is the (subclass-visible) way in.
    readonly JsModule _module = new("wss-overlay.js");
    protected bool _open;
    protected bool _positioned;
    protected bool _pendingFocus;
    protected bool _disposed;
    // Last (open, disabled) pair pushed to syncTrigger. syncTrigger re-resolves the trigger child on
    // every call, so we only re-invoke it when this pair changes (plus the first render) — otherwise
    // every ancestor re-render would cost one interop round trip per instance (e.g. a Popconfirm per
    // Table row).
    (bool Open, bool Disabled)? _lastSyncedTrigger;
    // Side the panel actually rendered on after the viewport flip. Null until measured (falls back to
    // the preferred PlacementName). Owned by C# so the re-render that drops `wss-measuring` keeps it.
    protected string? _resolvedPlacement;
    // Sequence token guarding the place() await below (mirrors OverlayActivationBase's own): a
    // close/reopen while a positioning attempt is in flight starts a NEW attempt with a fresh token,
    // so the stale attempt's continuation recognizes it's superseded and skips writing
    // _positioned/_pendingFocus/_resolvedPlacement.
    protected int _activationSeq;
    // Last Visible value this component has observed OR itself raised via RaiseVisibleChangedAsync.
    // Null until the first time VisibleChanged carries a delegate — mirrors Select's controlled
    // Open/OpenChanged design (_lastOpenParam). Read/written by each subclass's own
    // OnParametersSetAsync (the controlled-sync logic itself isn't shared -- see the class remarks).
    protected bool? _lastVisibleParam;

    /// <summary>The Ant-Design-style placement token (e.g. <c>"top"</c>) passed to <c>place()</c>.</summary>
    protected abstract string PlacementName { get; }

    /// <summary>The <c>"wss-popover"</c>/<c>"wss-popconfirm"</c> prefix passed to <c>place()</c>
    /// (whose backdrop class is <c>"{prefix}-backdrop"</c>).</summary>
    protected abstract string PanelClassPrefix { get; }

    /// <summary>What to pass as <c>syncTrigger</c>'s third (disabled) argument — Popover has no
    /// <c>Disabled</c> parameter (always false); Popconfirm passes its own.</summary>
    protected abstract bool TriggerDisabled { get; }

    /// <summary>Fires the derived control's own <c>VisibleChanged</c> — kept abstract (rather than
    /// this base owning that parameter) for the same reason <see cref="OverlayActivationBase.IsVisible"/>
    /// is: an override can't add a parameter the base doesn't declare.</summary>
    protected abstract Task InvokeVisibleChangedAsync(bool open);

    /// <summary>Moves focus into the now-positioned panel — Popover focuses the panel itself;
    /// Popconfirm defers focus onto its primary action button (see <c>focusDeferred</c>'s own doc
    /// comment in <c>wss-overlay.js</c> for why a plain <c>FocusAsync()</c> isn't enough there).</summary>
    protected abstract Task FocusPanelAsync();

    /// <summary>
    /// Lazily imports <c>wss-overlay.js</c> and hands back the reference to invoke on. Returns
    /// <c>null</c> when there is no JS runtime at all (prerender/unit tests) — callers take their own
    /// no-JS fallback — and when this component was disposed while the import was in flight: the
    /// holder cleaned up that late-arriving reference itself, and the caller must not invoke into a
    /// dead circuit. Every import site — this base's <c>syncTrigger</c>/<c>place</c> calls and a
    /// subclass's <see cref="FocusPanelAsync"/> — goes through here, so none of them can strand a
    /// module. Callers keep their own graceful-degradation <c>catch</c> for the <c>Invoke*</c> calls
    /// that follow.
    /// </summary>
    protected ValueTask<IJSObjectReference?> EnsureModuleAsync() => _module.GetAsync(JS, FormDefaults);

    // Single choke point for every open/close, internal or externally-driven: applies _open (the one
    // source of truth OnAfterRenderAsync's JS placement/focus logic reacts to, regardless of who
    // changed it) and notifies VisibleChanged. Mirrors Select's OpenAsync/CloseAsync +
    // RaiseOpenChangedAsync split.
    protected async Task SetOpenAsync(bool open)
    {
        if (open == _open) return;
        _open = open;
        await RaiseVisibleChangedAsync();
    }

    protected Task RaiseVisibleChangedAsync()
    {
        _lastVisibleParam = _open;
        return InvokeVisibleChangedAsync(_open);
    }

    /// <summary>Default: unconditionally toggles. Popconfirm overrides to add its <c>Disabled</c> guard.</summary>
    protected virtual Task ToggleAsync() => SetOpenAsync(!_open);

    /// <summary>Default: unconditionally closes. Popconfirm overrides to add its pending-confirm-lock guard.</summary>
    protected virtual Task CloseAsync() => SetOpenAsync(false);

    // Escape only: Enter/Space activation comes from the child's native click (or the JS fallback
    // synthesizing one for plain content) bubbling into @onclick — handling the keys here as well
    // would double-toggle when a button child's keystroke both clicks and bubbles. Calls the
    // subclass's own (possibly overridden) CloseAsync.
    protected async Task OnTriggerKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape") await CloseAsync();
    }

    // After the panel opens, nudge it to stay within the viewport (flip/shift). Degrades to the
    // default CSS placement when JS isn't available (server prerender, unit tests).
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Mirror the popup ARIA onto the real trigger element. syncTrigger re-resolves the child on
        // every call, so we only need to invoke it on the first render or when (_open, disabled)
        // changes — skipping the interop otherwise keeps a per-row instance cheap on ancestor re-renders.
        var pair = (_open, TriggerDisabled);
        if (firstRender || _lastSyncedTrigger != pair)
        {
            try
            {
                var module = await EnsureModuleAsync();
                if (_disposed) return; // disposed while the import was in flight
                if (module is not null)
                {
                    await module.InvokeVoidAsync("syncTrigger", _triggerRef, _open, TriggerDisabled);
                    _lastSyncedTrigger = pair; // cache only on success, so a no-JS render retries next time
                }
            }
            catch { /* no JS — a button child still toggles via its bubbled click; plain content is mouse-only */ }
        }

        if (_open && !_positioned)
        {
            // Sequence token: a close (and possibly a reopen, which starts a *new* positioning
            // attempt) while this attempt's place() call is in flight makes this one stale -- see
            // _activationSeq's declaration.
            var seq = ++_activationSeq;
            string? resolved = null;
            try
            {
                // A null module (no JS, or disposed mid-import) leaves `resolved` null, which the
                // guard below and the CSS default placement already handle — same as a throw.
                var module = await EnsureModuleAsync();
                if (module is not null)
                {
                    resolved = await module.InvokeAsync<string>("place", _triggerRef, _panelRef, PanelClassPrefix, PlacementName, 10, 8);
                }
            }
            catch
            {
                // No JS runtime / module — keep the CSS default placement.
            }

            if (_disposed || seq != _activationSeq || !_open)
            {
                // Superseded by a close/reopen (or disposed) while place() was in flight -- the
                // current activation's own OnAfterRenderAsync pass owns writing this state instead.
                return;
            }

            _resolvedPlacement = resolved;
            _positioned = true;
            _pendingFocus = true;
            StateHasChanged(); // reveal the panel now that it's positioned (drops wss-measuring)
        }
        else if (_open && _pendingFocus)
        {
            _pendingFocus = false;
            await FocusPanelAsync();
        }
        else if (!_open && _positioned)
        {
            _positioned = false;
            _resolvedPlacement = null; // re-measure on the next open
            // Restore focus to the real trigger (the interactive child, or the promoted wrapper).
            try
            {
                // Current, not EnsureModuleAsync: if the module never imported there is no JS to
                // restore focus with, and importing one here just to close would be backwards.
                if (_module.Current is { } overlay) await overlay.InvokeVoidAsync("focusTrigger", _triggerRef);
                else await _triggerRef.FocusAsync();
            }
            catch { /* no JS / element gone */ }
        }
    }

    public virtual async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _module.DisposeAsync();
    }
}
