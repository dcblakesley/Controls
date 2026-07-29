namespace Controls.Helpers;

/// <summary>
/// One component's lazily-imported RCL JS module, with the whole lifecycle every such import needs
/// baked in: the once-only <c>import</c>, the disposal race guard, and the no-JS degrade.
/// </summary>
/// <remarks>
/// <para>
/// The four-part idiom this replaces was hand-rolled at eight-plus call sites (both pickers via
/// <see cref="PickerBase"/>, <see cref="Select{TValue}"/>, <see cref="Modal"/>/<see cref="Drawer"/>
/// via <see cref="OverlayActivationBase"/>, <see cref="Popover"/>/<see cref="Popconfirm"/> via
/// <see cref="PopupOverlayBase"/>, <see cref="Table{TItem}"/>, its column filter, and
/// <see cref="EditBool"/>), and the easiest part to leave out — the re-check *after* the awaited
/// import — is the one that matters: a component disposed while its import is in flight has already
/// run <c>DisposeAsync</c> against a still-null field, so the reference that lands afterwards would
/// strand on a dead instance and hold its JS module alive for the rest of the circuit. Owning the
/// field here means a call site cannot express that bug: the guard is not something the site writes.
/// </para>
/// <para>
/// A holder is bound to one file name for its lifetime (constructor argument, not a
/// <see cref="GetAsync"/> parameter) so the same holder can never be asked for two different modules
/// — a component that needs two (PickerBase: <c>wss-overlay.js</c> plus <c>wss-picker.js</c>) declares
/// two fields.
/// </para>
/// <para>
/// Not thread-safe, and doesn't need to be: a component's lifecycle callbacks and event handlers are
/// serialized onto its renderer's synchronization context, so <see cref="GetAsync"/> and
/// <see cref="DisposeAsync"/> never actually overlap — only the awaits inside them interleave. That
/// interleaving is the whole problem this class exists to solve, and it takes both guards below: the
/// disposed re-check for a dispose that lands mid-import, and the cached import <em>task</em> for two
/// <see cref="GetAsync"/> calls that both start before the first import resolves.
/// </para>
/// </remarks>
internal sealed class JsModule(string fileName)
{
    IJSObjectReference? _module;
    // The in-flight import, not just its result: a second GetAsync arriving before the first resolves
    // must await the SAME import. Caching only the resolved reference (a `??=` around the await) let
    // both callers import, and the loser's IJSObjectReference was stranded — never disposed, held for
    // the rest of the circuit.
    Task<IJSObjectReference>? _importTask;
    bool _disposed;

    /// <summary>
    /// The already-imported reference, or <c>null</c> when nothing has been imported yet (no JS
    /// runtime, or nothing has needed the module) or this holder is disposed. For the teardown calls
    /// that are only meaningful against a module that already exists — <c>clearZ</c> on close, say —
    /// and must never trigger an import of their own just to undo something that was never done.
    /// </summary>
    internal IJSObjectReference? Current => _disposed ? null : _module;

    /// <summary>
    /// Imports the module on first use — once, however many callers ask before it resolves — and hands
    /// back the reference to invoke on. Returns <c>null</c> in both of the cases a caller must not
    /// invoke in — there is no JS runtime/module (server prerender, bUnit), or this holder was
    /// disposed, including a dispose that raced the awaited import (whose late-arriving reference is
    /// disposed here rather than stranded) — so every caller's bail-out is the same
    /// <c>if (module is null)</c>, and its own no-JS fallback covers both. A failed import is not
    /// cached: the next render retries.
    /// </summary>
    internal async ValueTask<IJSObjectReference?> GetAsync(IJSRuntime js, FormDefaults? formDefaults)
    {
        if (_disposed) return null;
        Task<IJSObjectReference>? importTask = null;
        try
        {
            // Held in a local as well as the field so this call awaits its own import even if the
            // field is cleared underneath it (the dispose path below, or another caller's retry).
            importTask = _importTask ??= js.InvokeAsync<IJSObjectReference>(
                "import", JsModuleUrl.Resolve(formDefaults, fileName)).AsTask();
            _module = await importTask;
        }
        catch
        {
            // No JS runtime / module (prerender, tests). Uncache so the next render retries — but only
            // this call's own task, so a retry already in flight isn't forgotten (which would let a
            // second import start after all).
            if (ReferenceEquals(_importTask, importTask)) _importTask = null;
            return null;
        }
        if (_disposed)
        {
            // Disposed while the import was in flight: DisposeAsync already ran against a null field,
            // so this reference is ours to clean up.
            await ReleaseAsync();
            return null;
        }
        return _module;
    }

    /// <summary>
    /// Releases the imported module, if any. Idempotent, and flips the holder closed first so an
    /// import racing this call disposes its own late-arriving reference (see <see cref="GetAsync"/>)
    /// instead of stranding it. Safe to call unconditionally from a component's own
    /// <c>DisposeAsync</c>.
    /// </summary>
    internal async ValueTask DisposeAsync()
    {
        _disposed = true;
        await ReleaseAsync();
    }

    // Bare catch, matching every call site this consolidates: by the time a module reference is being
    // released there is nothing left to fall back to, and the throw is a teardown artifact of a
    // circuit/runtime that is already going away (JSDisconnectedException on a dropped Blazor Server
    // circuit, but also ObjectDisposedException / a cancellation from the JS runtime itself, and
    // InvalidOperationException when disposal lands during static prerendering). Nulls the field
    // before awaiting so a re-entrant call can't double-dispose the same reference.
    async ValueTask ReleaseAsync()
    {
        // Drop the cached import as well. This only ever runs with _disposed already set, so nothing
        // can import again; a completed task would otherwise keep holding the very reference being
        // released, and a still-pending one is safe to forget — the GetAsync awaiting it holds its own
        // local and disposes the late-arriving reference itself.
        _importTask = null;
        if (_module is null) return;
        var module = _module;
        _module = null;
        try { await module.DisposeAsync(); } catch { /* circuit may be gone */ }
    }
}
