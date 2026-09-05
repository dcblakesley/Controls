// setIndeterminate now lives in wss-checkbox.js -- EditBool.Indeterminate needed the identical
// checkbox-mixed-state behavior, so the implementation moved to a neutrally-named shared module
// instead of being duplicated here. Re-exported under the original name/path so Table.razor's
// existing "wss-table.js" import keeps resolving unchanged.
export { setIndeterminate } from './wss-checkbox.js';

// Enter in one of TableFilterEditor's raw <input>s applies the filter through that component's own
// keydown handler -- preventDefault stops it ALSO implicitly submitting an enclosing <EditForm>,
// which C# cannot do (Blazor has no per-key preventDefault). Propagation is untouched, so the
// Blazor handler still runs. Same shape as wss-color.js's initTextInput and wss-overlay.js's
// initPicker; idempotent, since the caller re-invokes as elements appear. No JS runtime means no
// suppression -- the filter still applies, the form still submits alongside it.
export function suppressEnterSubmit(input) {
    if (!input || input.__wssEnterSubmitWired) {
        return;
    }
    input.__wssEnterSubmitWired = true;
    input.addEventListener('keydown', e => {
        if (e.key === 'Enter') {
            e.preventDefault();
        }
    });
}
