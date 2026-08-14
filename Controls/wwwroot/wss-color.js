// Pointer dragging + arrow-key scroll suppression for the ColorPicker's three tracks (the 2D
// saturation/value area and the hue/alpha sliders). Pure DOM, no dependencies.
//
// Why the value comes back through a hidden <input> rather than a DotNetObjectReference:
// nothing else in this library calls back into .NET (every other wss-*.js entry point is a one-way
// C# -> JS invoke), and a by-name [JSInvokable] callback is resolved reflectively at runtime, so it
// would need explicit rooting to survive a consumer's TrimMode=full publish -- the one thing
// Controls.csproj's IsAotCompatible contract is there to keep out. Writing the normalized
// coordinates into an input Blazor already has an @oninput handler on reuses the framework's own
// event channel instead: no object reference to create, dispose, or root, and the component's
// handler is an ordinary Blazor event handler.
//
// Degrades gracefully: when this module is unavailable (server prerender, bUnit) the component's
// own @onclick fallback positions the handle from MouseEventArgs.OffsetX/OffsetY, and the keyboard
// path works with no JS at all -- only the drag (and the arrow-key page-scroll suppression, which
// Blazor cannot express per-key) is lost.

// Keys the component handles itself and whose native behavior (scrolling the page, jumping to the
// top/bottom of the document) would otherwise fire alongside. Blazor has no per-key
// preventDefault -- @onkeydown:preventDefault is unconditional and would swallow Tab, trapping
// focus inside the track -- so the filtering happens here.
const WSS_COLOR_SCROLL_KEYS = new Set([
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown'
]);

function clamp01(value) {
    return value < 0 ? 0 : value > 1 ? 1 : value;
}

// Wires one track element. `signal` is the hidden input the component listens to; the value written
// is "x,y" -- both normalized to 0..1 of the track's own box, physical left/top based (the hue and
// alpha gradients paint left-to-right in both writing directions, so this must NOT mirror under
// dir="rtl"; see the RTL note in wss-controls.css). Idempotent: the caller may re-invoke on every
// open, and only the first call attaches listeners.
export function initTrack(track, signal) {
    if (!track || !signal || track.__wssColorWired) {
        return;
    }
    track.__wssColorWired = true;

    let dragging = false;
    let frame = 0;
    let pending = null;
    // Last value pushed to Blazor during the CURRENT press. Reset on every pointerdown, so a
    // second click on the exact same pixel still reports (the bound value may have been cleared or
    // changed from elsewhere in between) while a drag that jitters within one device pixel doesn't
    // spam the circuit.
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
        if (rect.width <= 0 || rect.height <= 0) {
            return; // not laid out (mid-measure, display:none) -- nothing meaningful to compute
        }
        const x = clamp01((e.clientX - rect.left) / rect.width);
        const y = clamp01((e.clientY - rect.top) / rect.height);
        pending = `${x.toFixed(4)},${y.toFixed(4)}`;
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
        // Capture keeps the stream coming while the pointer leaves the track (dragging past the
        // edge is the normal way to reach a saturation/brightness extreme).
        try { track.setPointerCapture(e.pointerId); } catch { /* capture unsupported */ }
        // Suppress the press's default text selection / focus handling, then focus the track
        // ourselves so the keyboard path is available immediately after a drag. (click still fires
        // per the Pointer Events spec, which is why the component's own @onclick fallback is gated
        // on whether this wiring succeeded -- a duplicated report would be harmless anyway, since
        // every report is an absolute position, but it costs a render.)
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
    };
    track.addEventListener('pointerup', end);
    track.addEventListener('pointercancel', end);

    // preventDefault only -- propagation is untouched, so the component's own @onkeydown handler
    // (registered by Blazor on the document) still runs and does the actual stepping.
    track.addEventListener('keydown', e => {
        if (WSS_COLOR_SCROLL_KEYS.has(e.key)) {
            e.preventDefault();
        }
    });
}

// Enter in the HEX box commits the typed color via the component's own change handler -- this
// preventDefault stops it ALSO implicitly submitting an enclosing form, which C# can't express
// (Blazor has no per-key preventDefault). Same contract as wss-overlay.js's initPicker gives the
// date pickers' inputs. Idempotent; without JS the commit still happens, but Enter may submit too.
export function initTextInput(input) {
    if (!input || input.__wssColorInputWired) {
        return;
    }
    input.__wssColorInputWired = true;
    input.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
        }
    });
}
