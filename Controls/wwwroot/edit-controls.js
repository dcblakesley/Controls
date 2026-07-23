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

    // Auto-size a <textarea> to fit its content, clamped between minRows and maxRows (maxRows
    // null/0 = unbounded). Stateless: no listeners are attached here, and nothing is cached between
    // calls -- EditTextArea re-invokes this on every input event and once after first render while
    // AutoSize is true. Silently returns if the element isn't found (stale id, unmounted mid-call).
    ns.autoSizeTextArea = function (id, minRows, maxRows) {
        const el = document.getElementById(id);
        if (!el) return;

        const style = getComputedStyle(el);
        // getComputedStyle reports the initial "normal" (or any other non-numeric value) when no
        // line-height is set -- fall back to the standard ~1.5x font-size ratio used elsewhere.
        let lineHeight = parseFloat(style.lineHeight);
        if (!lineHeight || Number.isNaN(lineHeight)) {
            const fontSize = parseFloat(style.fontSize) || 14;
            lineHeight = fontSize * 1.5;
        }

        const paddingTop = parseFloat(style.paddingTop) || 0;
        const paddingBottom = parseFloat(style.paddingBottom) || 0;
        const borderTop = parseFloat(style.borderTopWidth) || 0;
        const borderBottom = parseFloat(style.borderBottomWidth) || 0;

        // scrollHeight always includes padding (both box-sizing modes) but never border. What
        // `style.height` actually controls depends on box-sizing though: content-box height excludes
        // padding/border (the box model adds them on top); border-box height includes them. boxExtra
        // is the amount to add back so every height figure below is expressed in "what style.height
        // should be set to" units, regardless of which box-sizing mode is in play.
        const boxExtra = style.boxSizing === 'border-box' ? paddingTop + paddingBottom + borderTop + borderBottom : 0;
        const scrollPadding = paddingTop + paddingBottom;

        const minHeight = lineHeight * (minRows || 1) + boxExtra;
        const maxHeight = maxRows ? lineHeight * maxRows + boxExtra : null;

        // Reset height first so scrollHeight reflects the content's natural size, not whatever
        // (possibly larger, possibly stale) height is currently set. While the value is empty,
        // Chromium includes the rendered (possibly line-wrapped) placeholder in scrollHeight, which
        // would size the box to the placeholder instead of minRows -- AntD's own autoSize measures a
        // mirror of the value only, so strip the placeholder for the measurement and restore it after.
        const placeholder = el.placeholder;
        if (!el.value && placeholder) el.placeholder = '';
        el.style.height = 'auto';
        const contentHeight = el.scrollHeight - scrollPadding + boxExtra;
        if (!el.value && placeholder) el.placeholder = placeholder;

        let target = Math.max(contentHeight, minHeight);
        let clampedAtMax = false;
        if (maxHeight !== null && target > maxHeight) {
            target = maxHeight;
            clampedAtMax = true;
        }

        el.style.height = target + 'px';
        el.style.overflowY = clampedAtMax ? 'auto' : 'hidden';
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
