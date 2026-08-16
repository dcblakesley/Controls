// Pointer dragging + arrow-key scroll suppression for EditRange's horizontal slider track. Pure
// DOM, no dependencies. Modelled on wss-color.js's initTrack (the library's existing slider
// primitive), reduced to the one axis a range control needs and extended with a press flag so the
// component can show its value tooltip for the whole gesture -- including the part of a drag that
// happens off the track, where CSS :hover has already stopped applying.
//
// Why the value comes back through a hidden <input> rather than a DotNetObjectReference: the same
// reason wss-color.js does it (see that file's header). Nothing else in this library calls back into
// .NET, and a by-name [JSInvokable] callback is resolved reflectively at runtime, so it would need
// explicit rooting to survive a consumer's TrimMode=full publish -- the one thing Controls.csproj's
// IsAotCompatible contract exists to keep out. Writing the normalized position into an input Blazor
// already has an @oninput handler on reuses the framework's own event channel instead.
//
// Degrades gracefully: when this module is unavailable (server prerender, bUnit) the component's own
// @onclick fallback positions the handle from MouseEventArgs.OffsetX, and the keyboard path works
// with no JS at all -- only the drag (and the arrow-key page-scroll suppression, which Blazor cannot
// express per-key) is lost.

// Keys the component handles itself and whose native behavior (scrolling the page, jumping to the
// top/bottom of the document) would otherwise fire alongside. Blazor has no per-key preventDefault
// -- @onkeydown:preventDefault is unconditional and would swallow Tab, trapping focus on the track
// -- so the filtering happens here.
const WSS_SLIDER_SCROLL_KEYS = new Set([
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'
]);

function clamp01(value) {
    return value < 0 ? 0 : value > 1 ? 1 : value;
}

// Wires one horizontal track. `signal` is the hidden input the component listens to; the value
// written is "x,pressed" -- x normalized to 0..1 of the track's own box (physical left-based, so it
// does NOT mirror under dir="rtl"; the control has no reverse mode), and pressed 1 while the pointer
// is down, 0 for the release report that ends the gesture. Idempotent: the caller may re-invoke on
// every render, and only the first call attaches listeners.
export function initSlider(track, signal) {
    if (!track || !signal || track.__wssSliderWired) {
        return;
    }
    track.__wssSliderWired = true;

    let dragging = false;
    let frame = 0;
    let pending = null;
    // The last normalized x measured during the current press, so the release report can repeat it
    // rather than measuring a pointerup that may have landed outside the track.
    let lastX = null;
    // Last value pushed to Blazor during the CURRENT press. Reset on every pointerdown, so a second
    // click on the exact same pixel still reports (the bound value may have been changed from
    // elsewhere in between) while a drag that jitters within one device pixel doesn't spam the
    // circuit.
    let last = '';

    const flush = () => {
        frame = 0;
        const value = pending;
        pending = null;
        if (value === null || value === last) {
            return;
        }
        last = value;
        signal.value = value;
        // Bubbles so Blazor's delegated document-level listener sees it; the component reads
        // ChangeEventArgs.Value, which is this input's own value.
        signal.dispatchEvent(new Event('input', { bubbles: true }));
    };

    // requestAnimationFrame-throttled: a pointermove stream can fire far faster than the browser
    // paints, and each report costs a render (plus a network round trip on Blazor Server).
    const queue = e => {
        const rect = track.getBoundingClientRect();
        if (rect.width <= 0) {
            return; // not laid out (mid-measure, display:none) -- nothing meaningful to compute
        }
        lastX = clamp01((e.clientX - rect.left) / rect.width);
        pending = `${lastX.toFixed(4)},1`;
        if (!frame) {
            frame = requestAnimationFrame(flush);
        }
    };

    track.addEventListener('pointerdown', e => {
        if (e.pointerType === 'mouse' && e.button !== 0) {
            return; // primary button only; a right-click must not move the handle
        }
        dragging = true;
        last = '';
        lastX = null;
        // Capture keeps the stream coming while the pointer leaves the track -- dragging past the
        // edge is the normal way to reach either extreme.
        try { track.setPointerCapture(e.pointerId); } catch { /* capture unsupported */ }
        // Suppress the press's default text selection / focus handling, then focus the track
        // ourselves so the keyboard path is available immediately after a drag. (click still fires
        // per the Pointer Events spec, which is why the component's own @onclick fallback is gated
        // on whether this wiring succeeded.)
        e.preventDefault();
        try { track.focus(); } catch { /* gone */ }
        queue(e);
    });

    track.addEventListener('pointermove', e => {
        if (dragging) {
            queue(e);
        }
    });

    const end = e => {
        if (!dragging) {
            return;
        }
        dragging = false;
        try { track.releasePointerCapture(e.pointerId); } catch { /* already released */ }
        if (lastX === null) {
            return; // never measured a position (an unlaid-out track) -- nothing to report
        }
        // Deliver the final POSITION before the release flag, or a gesture whose last move is still
        // sitting in the rAF queue would be overwritten by the release report and never commit.
        if (frame) {
            cancelAnimationFrame(frame);
            flush();
        }
        pending = `${lastX.toFixed(4)},0`;
        flush();
    };
    track.addEventListener('pointerup', end);
    track.addEventListener('pointercancel', end);

    // preventDefault only -- propagation is untouched, so the component's own @onkeydown handler
    // (registered by Blazor on the document) still runs and does the actual stepping.
    track.addEventListener('keydown', e => {
        if (WSS_SLIDER_SCROLL_KEYS.has(e.key)) {
            e.preventDefault();
        }
    });
}
