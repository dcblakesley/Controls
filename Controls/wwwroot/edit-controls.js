// Namespaced helpers for WssBlazorControls. Kept on `window` (not exported) so Blazor's standard
// `IJSRuntime.InvokeVoidAsync("WssEditControls...")` can reach them by name. The classic
// `<script src="_content/WssBlazorControls/edit-controls.js">` tag (see README Quick Start) is the
// primary load path. This file has no import/export statements and doesn't rely on sloppy-mode-only
// globals (no bare `this`, no implicit global assignment), so it also works unchanged as a
// side-effect ES module import (`import("...")`) -- the fallback JsInteropEc.cs uses when
// window.WssEditControls is missing (e.g. a cross-origin micro-frontend whose host page never linked
// the script tag). Keep both load paths working if you touch this file.
(function () {
    const ns = window.WssEditControls = window.WssEditControls || {};

    // Find the first invalid form field on the page, scroll it into view, focus it, and select its
    // text where applicable. Skips invalid elements that aren't form fields (e.g. a wrapper div that
    // happens to carry the .invalid CSS class for visual state).
    //
    // Exact class-token match only: a CSS class selector like `.invalid` matches an element whose
    // class attribute contains the literal space-separated token "invalid", on any tag -- so the
    // separate `input.invalid, textarea.invalid, select.invalid` variants added nothing. This used to
    // also list `[class*=" invalid"]`, a substring match that over-matched consumer classes like
    // `class="foo invalid-hint"` (the same false-positive shape InvalidIcon.razor and
    // EditControlBase.IsInvalid fixed for CssClass -- see their comments).
    ns.focusFirstInvalidField = function () {
        const candidate = document.querySelector('.invalid');
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

    // Focus an element by id if it exists. Used by EditFile to keep keyboard focus on the file list
    // after a file is removed (its delete button vanishes, otherwise focus falls back to <body>).
    ns.focusById = function (id) {
        const el = document.getElementById(id);
        if (el && typeof el.focus === 'function') el.focus();
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
