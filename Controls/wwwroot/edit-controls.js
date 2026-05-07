// Namespaced helpers for WssBlazorControls. Kept on `window` (not as an ES module)
// so Blazor's standard `IJSRuntime.InvokeVoidAsync(...)` can reach them by name.
(function () {
    const ns = window.WssEditControls = window.WssEditControls || {};

    // Find the first invalid form field on the page, scroll it into view, focus it,
    // and select its text where applicable. Skips invalid elements that aren't form fields
    // (e.g. a wrapper div that happens to carry the .invalid CSS class for visual state).
    ns.focusFirstInvalidField = function () {
        const candidate = document.querySelector(
            'input.invalid, textarea.invalid, select.invalid, [class*=" invalid"], .invalid'
        );
        if (!candidate) return;

        // Resolve to an actual form field. If `candidate` is one already, use it; otherwise
        // try to find an input/textarea/select inside it (or matching its id).
        let field = null;
        if (candidate.matches('input, textarea, select')) {
            field = candidate;
        } else if (candidate.id) {
            field = document.getElementById(candidate.id);
            if (field && !field.matches('input, textarea, select')) {
                field = candidate.querySelector('input, textarea, select');
            }
        } else {
            field = candidate.querySelector('input, textarea, select');
        }

        if (!field) return;

        field.scrollIntoView({ behavior: 'smooth', block: 'center' });
        if (typeof field.focus === 'function') field.focus();
        if (field.tagName === 'INPUT' || field.tagName === 'TEXTAREA') {
            try { field.select(); } catch { /* type="number" etc. doesn't support select */ }
        }
    };

    ns.log = function (text) { console.log(text); };
    ns.logError = function (text) { console.log('%c' + text, 'background: red'); };
    ns.logWarn = function (text) { console.log('%c' + text, 'background: orange'); };
    ns.logInfo = function (text) { console.log('%c' + text, 'background: cyan'); };

    // Back-compat shims: expose the old global names so existing apps that call
    // `IJSRuntime.InvokeVoidAsync("focusFirstInvalidField")` keep working. Remove in a
    // future major if you want to fully retire the global namespace.
    window.focusFirstInvalidField = ns.focusFirstInvalidField;
    window.log = ns.log;
    window.logError = ns.logError;
    window.logWarn = ns.logWarn;
    window.logInfo = ns.logInfo;
})();
