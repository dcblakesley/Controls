namespace Controls;

/// <summary>
/// Shared focus-trap/scroll-lock activation lifecycle for <see cref="Modal"/> and <see cref="Drawer"/>:
/// imports <c>wss-overlay.js</c> once, calls its <c>activateModal</c> on the transition to visible
/// (racing the JS call against a close-then-reopen via <see cref="_activationSeq"/>, mirroring
/// <see cref="PickerBase"/>'s own sequence-token pattern), and releases the returned handle on the
/// transition to hidden or on dispose. Every JS call degrades gracefully to a no-JS fallback
/// (prerender, bUnit) via try/catch.
/// </summary>
/// <remarks>
/// Mirrors <see cref="PickerBase"/>'s shape: subclasses plug in only <see cref="IsVisible"/> and the
/// panel <see cref="ElementReference"/> (<c>_panelRef</c>, inherited directly — markup binds
/// <c>@ref="_panelRef"</c> as before); the render-cycle template itself isn't meant to be overridden
/// again (not sealed at the compiler level, matching <see cref="PickerBase.OnAfterRenderAsync"/>'s own
/// convention, but there is no second customization point).
/// </remarks>
public abstract class OverlayActivationBase : ComponentBase, IAsyncDisposable
{
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] protected FormDefaults? FormDefaults { get; set; }

    protected ElementReference _panelRef;
    // JsModule owns the once-only import, the dispose-raced-the-import guard, and the no-JS degrade
    // (a null return, which reads the same as the import throwing did).
    readonly JsModule _module = new("wss-overlay.js");
    IJSObjectReference? _focusHandle;
    bool _active;
    bool _disposed;
    int _activationSeq;

    /// <summary>
    /// Whether the overlay is currently shown. Delegates to the derived control's own two-way-bindable
    /// <c>Visible</c> parameter — kept as a separate abstract property (rather than this base owning
    /// <c>Visible</c> itself) because an override can't add the setter <c>@bind-Visible</c> needs to a
    /// base property whose abstract declaration is get-only.
    /// </summary>
    protected abstract bool IsVisible { get; }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsVisible && !_active)
        {
            _active = true;
            // Sequence token: a close (and possibly a reopen, which starts a *new* activation)
            // while this activation's JS call is in flight makes this one stale. Without the token
            // a close→reopen race left the first handle orphaned — its ref-counted body-scroll
            // lock was never released, permanently freezing page scroll.
            var seq = ++_activationSeq;
            try
            {
                // Null = no JS at all (prerender/tests), or disposed while the import itself was in
                // flight — in which case the holder already cleaned up its own late-arriving reference
                // (it would otherwise strand for the circuit's life). Either way there is nothing to
                // activate; the seq/!IsVisible checks below cover the later activateModal handle.
                var module = await _module.GetAsync(JS, FormDefaults);
                if (module is null) return;
                var handle = await module.InvokeAsync<IJSObjectReference>("activateModal", _panelRef);
                // Disposed (or closed/reopened) while activateModal was in flight? DisposeAsync already
                // ran — storing this handle would orphan it, leaking the body-scroll lock + document
                // listeners for the circuit's life. Release it here instead.
                if (_disposed || seq != _activationSeq || !IsVisible)
                {
                    try { await handle.InvokeVoidAsync("dispose"); await handle.DisposeAsync(); } catch { }
                }
                else
                {
                    _focusHandle = handle;
                }
            }
            catch { /* no JS — overlay still usable, just no focus trap/scroll lock */ }
        }
        else if (!IsVisible && _active)
        {
            _active = false;
            _activationSeq++; // invalidate any in-flight activation
            await ReleaseFocusAsync();
        }
    }

    async Task ReleaseFocusAsync()
    {
        if (_focusHandle is not null)
        {
            try { await _focusHandle.InvokeVoidAsync("dispose"); await _focusHandle.DisposeAsync(); } catch { }
            _focusHandle = null;
        }
    }

    /// <summary>
    /// Releases the JS module and any active focus-trap handle. Sets <see cref="_disposed"/> first so
    /// an in-flight <c>activateModal</c> releases its handle rather than storing it; the module holder
    /// flips itself closed for the same reason (an import racing this call disposes its own
    /// late-arriving module instead of stranding it on this dead instance). Virtual so a subclass with
    /// its own disposable state can extend it (neither current subclass needs to).
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        _disposed = true;
        _activationSeq++; // invalidate any in-flight activation so its handle is released, not stored
        await ReleaseFocusAsync();
        await _module.DisposeAsync();
    }
}
