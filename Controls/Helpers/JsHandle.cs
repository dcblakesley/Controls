namespace Controls.Helpers;

/// <summary>
/// One component's activation handle from a JS module — the <c>IJSObjectReference</c> that a function
/// like <c>activateModal</c> or <c>activateFixedDropdown</c> returns so whatever it wired up (document
/// listeners, a ref-counted body-scroll lock, window scroll/resize tracking) can be torn down again —
/// with the whole lifecycle such a handle needs baked in: the sequence token that keeps a
/// close-then-reopen from orphaning one, the two-step release (<c>dispose</c> on the JS object, then
/// the .NET reference), and idempotent double-release/dispose.
/// </summary>
/// <remarks>
/// <para>
/// Companion to <see cref="JsModule"/>, one level down: that class owns the module <c>import</c>, this
/// one owns a handle obtained by invoking on it. The idiom was hand-rolled at
/// <see cref="OverlayActivationBase"/> (correct) and at <see cref="Table{TItem}"/>'s column filter
/// (drifted — it re-checked "still open" after the await but had no sequence token, so a
/// close→reopen across one in-flight round trip stored the wrong handle and orphaned the other,
/// leaking its window scroll/resize listeners for the rest of the circuit). Owning both guards here
/// means a call site cannot express that bug: neither the token nor the release is something the site
/// writes.
/// </para>
/// <para>
/// Not thread-safe, and doesn't need to be — same reasoning as <see cref="JsModule"/>: a component's
/// lifecycle callbacks and event handlers are serialized onto its renderer's synchronization context,
/// so only the awaits inside these methods interleave. That interleaving is the whole problem this
/// class exists to solve: an activation's JS round trip can complete after the component has closed,
/// reopened, or been disposed.
/// </para>
/// </remarks>
internal sealed class JsHandle
{
    IJSObjectReference? _handle;
    // Bumped by every release/dispose and captured before the activation await: a release that lands
    // while an activation is in flight makes that activation stale, so its late-arriving handle is
    // released instead of stored. This is what makes ReleaseAsync the *only* thing a caller has to do
    // on its close transition.
    int _seq;
    // The _seq of an activation currently awaiting its round trip, or -1 for none (_seq only ever
    // counts up from 0). Blocks a second activation within the same generation: a caller that
    // re-renders while the first is in flight would otherwise get a SECOND JS-side handle, and this
    // object can only hold one — whichever it didn't store would be orphaned. Generation-scoped rather
    // than a plain bool so that a release-then-reopen across an in-flight activation still activates:
    // the reopen belongs to a new generation, and the stale one releases its own handle on arrival.
    int _activatingSeq = -1;
    bool _disposed;

    /// <summary>
    /// Ensures a live handle: invokes <paramref name="identifier"/> on <paramref name="module"/> with
    /// <paramref name="args"/> and stores the returned reference, then reports whether this object now
    /// holds a handle — which is also how a caller that tracks its own "already activated" flag learns
    /// to retry on a later render. Idempotent while a handle is held (returns <c>true</c> without
    /// invoking again) and while one is already in flight for the same generation (returns
    /// <c>false</c>; that call's own result decides). Never throws: a missing JS runtime, a torn-down
    /// element, or a dropped circuit all report <c>false</c>, leaving the caller's no-JS fallback in
    /// place.
    /// </summary>
    /// <param name="module">The imported module to invoke on (from <see cref="JsModule.GetAsync"/>).</param>
    /// <param name="identifier">The exported function to call; it must return a disposable handle
    /// object exposing a <c>dispose</c> method.</param>
    /// <param name="args">Arguments for that function.</param>
    /// <param name="stillWanted">The caller's own "the thing this activates is still open" test,
    /// evaluated only <em>after</em> the round trip. A handle that arrives once the answer is no is
    /// released rather than stored, so nothing is left holding listeners for something the user has
    /// already dismissed.</param>
    internal async ValueTask<bool> ActivateAsync(
        IJSObjectReference module, string identifier, object?[] args, Func<bool> stillWanted)
    {
        if (_disposed) return false;
        if (_handle is not null) return true;

        var seq = _seq;
        if (_activatingSeq == seq) return false;
        _activatingSeq = seq;
        IJSObjectReference? handle;
        try
        {
            handle = await module.InvokeAsync<IJSObjectReference>(identifier, args);
        }
        catch
        {
            // No JS runtime / module, the element is gone, or the circuit dropped mid-call. The
            // caller's own fallback stands, and leaving nothing stored lets a later render retry.
            return false;
        }
        finally
        {
            // Only clear the marker if it is still this activation's: a newer generation may have
            // started one meanwhile, and that one is the live claim now.
            if (_activatingSeq == seq) _activatingSeq = -1;
        }

        if (_disposed || seq != _seq || !stillWanted())
        {
            // Released, released-and-re-activated, disposed, or closed while the call was in flight.
            // Storing this handle now would orphan it (the release that already ran found nothing),
            // leaking whatever it wired up for the rest of the circuit — so release it here instead.
            await ReleaseHandleAsync(handle);
            return false;
        }
        _handle = handle;
        // A null handle only happens where there is no real JS engine (bUnit's loose interop returns
        // default for every InvokeAsync): report success anyway, since the activation is as done as it
        // can be there and re-invoking every render would be worse than holding nothing.
        return true;
    }

    /// <summary>
    /// Releases the handle, if any: invokes its JS-side <c>dispose</c> (the teardown the activate
    /// function returned it for) and then disposes the .NET reference, swallowing the throw from a
    /// circuit that is already gone. Also invalidates any activation still in flight, so its handle is
    /// released on arrival instead of stored. Idempotent, and a no-op when nothing is held — safe to
    /// call unconditionally on a close transition.
    /// </summary>
    internal async ValueTask ReleaseAsync()
    {
        _seq++;
        if (_handle is null) return;
        var handle = _handle;
        _handle = null; // nulled before awaiting so a re-entrant release can't double-dispose it
        await ReleaseHandleAsync(handle);
    }

    /// <summary>
    /// Releases the handle and closes this object permanently, so an <see cref="ActivateAsync"/> from
    /// a render that raced the component's own disposal does nothing (and a round trip already in
    /// flight releases its own late-arriving handle). Safe to call unconditionally from a component's
    /// <c>DisposeAsync</c>, and idempotent.
    /// </summary>
    internal async ValueTask DisposeAsync()
    {
        _disposed = true;
        await ReleaseAsync();
    }

    // Bare catch, matching every call site this consolidates: by the time a handle is being released
    // there is nothing left to fall back to, and the throw is a teardown artifact of a circuit/runtime
    // that is already going away (JSDisconnectedException on a dropped Blazor Server circuit, but also
    // ObjectDisposedException / a cancellation from the JS runtime itself). The JS-side dispose and the
    // .NET reference share one catch: if the first throws, the second is pointless.
    static async ValueTask ReleaseHandleAsync(IJSObjectReference? handle)
    {
        if (handle is null) return; // no real JS engine — see ActivateAsync's closing comment
        try
        {
            await handle.InvokeVoidAsync("dispose");
            await handle.DisposeAsync();
        }
        catch { /* circuit may be gone */ }
    }
}
