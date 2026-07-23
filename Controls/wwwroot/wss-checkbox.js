// Sets the native `indeterminate` DOM property on a checkbox. There is no HTML attribute for it, so
// a "some but not all" mixed state can only be reached from C# via JS. `target` may be either an
// element (an ElementReference marshals to the real DOM element) or an element id string -- callers
// pick whichever is more convenient (Table already has an ElementReference via @ref; EditBool looks
// its checkbox up by the id it already computes for every other attribute).
//
// This is the single implementation shared by EditBool.Indeterminate and the UI-kit Table's "select
// all" header checkbox -- see wss-table.js, which re-exports this rather than duplicating it.
// Degrades to a plain checked/unchecked box when JS is unavailable (server prerender, unit tests).
export function setIndeterminate(target, value) {
    const el = typeof target === 'string' ? document.getElementById(target) : target;
    if (el) {
        el.indeterminate = !!value;
    }
}
