namespace Controls;

/// <summary>
/// Shared focus-trap/scroll-lock activation lifecycle for <see cref="Modal"/> and <see cref="Drawer"/>:
/// imports <c>wss-overlay.js</c> once, calls its <c>activateModal</c> on the transition to visible
/// (racing the JS call against a close-then-reopen via <see cref="JsHandle"/>'s sequence token, the
/// same pattern <see cref="PickerBase"/> uses for its own overlay), and releases the returned handle on
/// the transition to hidden or on dispose. Every JS call degrades gracefully to a no-JS fallback
/// (prerender, bUnit): the two holders swallow the failure and the overlay stays usable without the
/// trap.
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
    // (a null return, which reads the same as the import throwing did). JsHandle owns activateModal's
    // returned handle: its sequence token, the release, and the no-JS/element-gone degrade.
    readonly JsModule _module = new("wss-overlay.js");
    readonly JsHandle _focusHandle = new();
    bool _active;

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
            // Null = no JS at all (prerender/tests), or disposed while the import itself was in
            // flight — in which case the holder already cleaned up its own late-arriving reference
            // (it would otherwise strand for the circuit's life). Either way there is nothing to
            // activate.
            var module = await _module.GetAsync(JS, FormDefaults);
            if (module is null) return;
            // A close (and possibly a reopen, which starts a *new* activation) while this call is in
            // flight makes it stale, and JsHandle then releases the late-arriving handle instead of
            // storing it. Without that guard the close→reopen race left the first handle orphaned —
            // its ref-counted body-scroll lock was never released, permanently freezing page scroll.
            await _focusHandle.ActivateAsync(module, "activateModal", [_panelRef], () => IsVisible);
        }
        else if (!IsVisible && _active)
        {
            _active = false;
            await _focusHandle.ReleaseAsync(); // also invalidates any activation still in flight
        }
    }

    /// <summary>
    /// Releases the JS module and any active focus-trap handle. Both holders flip themselves closed
    /// first, so an activation or an import racing this call releases its own late-arriving reference
    /// rather than stranding it on this dead instance. Virtual so a subclass with its own disposable
    /// state can extend it (neither current subclass needs to).
    /// </summary>
    public virtual async ValueTask DisposeAsync()
    {
        await _focusHandle.DisposeAsync();
        await _module.DisposeAsync();
    }
}
