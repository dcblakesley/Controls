// Sets the native `indeterminate` DOM property on a checkbox. There is no HTML attribute for it,
// so a "some but not all selected" header checkbox can only reach the mixed state from C# via JS.
// Degrades to a plain checked/unchecked box when JS is unavailable (server prerender, unit tests).
export function setIndeterminate(checkbox, value) {
    if (checkbox) {
        checkbox.indeterminate = !!value;
    }
}
