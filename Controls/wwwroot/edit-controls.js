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

        // Respect the user's reduced-motion preference (both stylesheets in this package honor it
        // elsewhere) -- 'auto' jumps instantly instead of animating the scroll.
        const reduceMotion = typeof window.matchMedia === 'function'
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        field.scrollIntoView({ behavior: reduceMotion ? 'auto' : 'smooth', block: 'center' });
        if (typeof field.focus === 'function') field.focus();
        if (field.tagName === 'INPUT' || field.tagName === 'TEXTAREA') {
            try { field.select(); } catch { /* type="number" etc. doesn't support select */ }
        }
    };

    // ─────────────────────── FormDefaults.FocusFirstField ───────────────────────

    // What counts as "a form field" for the initial-focus feature below. Deliberately NARROWER than
    // wss-overlay.js's WSS_FOCUSABLE (which is "anything Tab can reach", buttons and links included):
    // this feature exists to land the user in the first thing they can TYPE IN, and a heading the
    // router focused, a Skip link, or a dialog's close X must never be mistaken for that. The three
    // non-<input> entries are the library's own field elements that aren't native form controls:
    // EditRange's role="slider" track, EditColor's trigger button (its whole widget IS that button --
    // the popover only exists while open), and any consumer contenteditable. type=hidden is excluded
    // because two controls use hidden inputs purely as a drag/interop channel (EditRange's
    // .edit-range-signal, ColorPicker's .wss-color-picker-signal) and they are not focusable anyway.
    const WSS_FIELD =
        'input:not([type=hidden]),textarea,select,[role=slider],button.wss-color-picker-trigger,'
        + '[contenteditable=""],[contenteditable="true"]';

    // A field the user could actually be put into right now. Mirrors focusGroupInput's "skip disabled
    // options" rule and extends it with the states only the DOM can answer for: readonly (a legal Tab
    // stop, but nothing to type), tabindex="-1" (deliberately out of the Tab order -- EditRange while
    // disabled), an inert or aria-hidden ancestor, and not being rendered at all (display:none from a
    // HidingMode, a collapsed panel, an un-opened dropdown).
    const isFocusableField = function (el) {
        if (el.disabled || el.readOnly) return false;
        if (el.getAttribute('tabindex') === '-1') return false;
        if (el.closest('[inert],[aria-hidden="true"]')) return false;
        // checkVisibility covers display:none, visibility:hidden/collapse and content-visibility in
        // one call; opacity is deliberately NOT checked -- an opacity:0 input (EditFile's drop-zone
        // file input, a styled checkbox) is still focusable and still the right target. The fallback
        // is the classic "has a box or a client rect" test for browsers without checkVisibility.
        if (typeof el.checkVisibility === 'function')
            return el.checkVisibility({ checkVisibilityCSS: true, visibilityProperty: true, contentVisibilityAuto: true });
        return !!(el.offsetWidth || el.offsetHeight || el.getClientRects().length);
    };

    // Focus the first form field inside one FormDefaults scope, when that scope has FocusFirstField
    // on. The scope is delimited by the two empty <template> markers FormDefaults renders around its
    // ChildContent -- and ONLY when the feature is switched on, so a default-off render tree is
    // byte-identical to before. <template> is used because it has no layout box, no accessibility
    // presence, and no content of its own.
    //
    // "First" is resolved HERE, from the rendered DOM, and not from anything C# knows: Blazor notifies
    // non-fixed cascading-value subscribers in CONSTRUCTION order rather than document order, so a
    // registry of fields built on the C# side would confidently return the wrong "first" as soon as a
    // form's markup order and its component-construction order disagreed. document.querySelectorAll
    // returns document order by definition, which is the order the user's Tab key sees.
    //
    // Best-effort throughout: a missing marker (never rendered, torn down mid-call), an empty scope,
    // or an unfocusable target all end as a silent no-op rather than an error.
    ns.focusFirstField = function (scopeId) {
        const start = document.getElementById(scopeId);
        if (!start) return;
        const end = document.getElementById(scopeId + '-end');

        // Strictly between the two markers, so the scope is exactly what FormDefaults rendered --
        // siblings written after </FormDefaults> in the same parent are NOT candidates. A missing end
        // marker (mid-render teardown) degrades to "everything after the start marker".
        const inScope = el =>
            (start.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_FOLLOWING) !== 0
            && (!end || (end.compareDocumentPosition(el) & Node.DOCUMENT_POSITION_PRECEDING) !== 0);

        const candidates = Array.prototype.filter.call(
            document.querySelectorAll(WSS_FIELD), el => inScope(el) && isFocusableField(el));
        if (candidates.length === 0) return;

        // Never take focus off a field that already has it. This is the guard that makes the feature
        // composable rather than a focus war, and it settles three collisions at once:
        //   * A control's own FocusOnFirstRender (or a consumer FocusAsync()) inside this scope --
        //     an explicit, specific request beats "the first one", exactly as wss-overlay.js's
        //     activateModal already defers to it. Whichever of the two runs first, the explicit one
        //     ends up holding focus: if it ran first this returns, and if it ran second it simply
        //     overwrites what this did.
        //   * A second armed scope elsewhere on the page (two forms, two MFE roots) -- the one that
        //     resolves first keeps focus instead of the last one winning by accident.
        //   * The user, who may already have clicked into a field before a lazily-rendered scope
        //     mounts.
        // Buttons and links deliberately do NOT block: the case this feature exists for is a form in
        // a dialog, where wss-overlay's activateModal may have already parked focus on the close X,
        // and a router's focus-on-navigate heading (which gets tabindex="-1") must not block it either.
        // An inert active element doesn't count -- that is the page behind an open dialog.
        const active = document.activeElement;
        if (active && typeof active.matches === 'function'
            && active.matches(WSS_FIELD) && !active.closest('[inert]')) return;

        let target = candidates[0];
        // Real radiogroup Tab semantics, matching focusGroupInput's preferChecked: the tab stop for a
        // group with a selection is the CHECKED radio, not the first one.
        if (target.type === 'radio' && target.name) {
            const checked = candidates.find(el => el.type === 'radio' && el.name === target.name && el.checked);
            if (checked) target = checked;
        }

        // No scrollIntoView and no select(): unlike focusFirstInvalidField (which is repairing an
        // error somewhere down the page) this runs at the top of a freshly rendered form, and
        // .focus()'s own minimal scrolling is all that is ever wanted.
        try { target.focus(); } catch { /* vanished between the query and here */ }
    };

    // Focus an element by id if it exists. Used by EditFile to keep keyboard focus on the file list
    // after a file is removed (its delete button vanishes, otherwise focus falls back to <body>).
    ns.focusById = function (id) {
        const el = document.getElementById(id);
        if (el && typeof el.focus === 'function') el.focus();
    };

    // Move focus INTO a group control whose individual inputs this library can't capture an
    // ElementReference for: the four radio groups all render their options through Microsoft's
    // <InputRadio> (EditRadio's come from consumer markup outright), so there is no element for
    // @ref to bind and no per-option id EditRadio could compute. Focusing the container id the
    // fieldset already carries and resolving the option here is the only channel that reaches them.
    //
    // preferChecked mirrors real radiogroup tab-order semantics: Tab lands on the CHECKED radio, not
    // the first one, so a group with a selection must focus that. Checkbox lists are the opposite --
    // each box is its own tab stop -- so they pass false and take the first enabled box.
    //
    // Disabled options are skipped entirely, and a group with none enabled is left alone rather than
    // focused somewhere the user can't act: `.focus()` on a disabled input is a silent no-op anyway,
    // but it would still have moved focus AWAY from wherever it was.
    ns.focusGroupInput = function (containerId, selector, preferChecked) {
        const container = document.getElementById(containerId);
        if (!container) return;
        const enabled = Array.prototype.filter.call(
            container.querySelectorAll(selector || 'input'), el => !el.disabled);
        if (enabled.length === 0) return;
        const target = (preferChecked && enabled.find(el => el.checked)) || enabled[0];
        if (typeof target.focus === 'function') target.focus();
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

    // Save in-memory bytes as a browser download -- EditFile's AllowDownload, so a user can reopen a
    // file they've already picked. `bytes` arrives as a Uint8Array (.NET's efficient byte[] marshaling,
    // not a Base64 string) and is Blob-able directly. A temporary, auto-clicked <a download> is the
    // standard idiom specifically because it -- unlike window.open -- isn't treated as a popup by
    // browsers even after the async interop round-trip has left the original click's user-gesture window.
    ns.downloadFile = function (bytes, fileName, contentType) {
        const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || 'download';
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    };

    ns.log = function (text) { console.log(text); };
    ns.logError = function (text) { console.log('%c' + text, 'background: red'); };
    ns.logWarn = function (text) { console.log('%c' + text, 'background: orange'); };
    ns.logInfo = function (text) { console.log('%c' + text, 'background: cyan'); };

    // Back-compat shims: expose the old global names so existing apps that call
    // `IJSRuntime.InvokeVoidAsync("focusFirstInvalidField")` keep working. Remove in a
    // future major if you want to fully retire the global namespace. `??=` rather than `=`: this file
    // also loads as a side-effect ES module import for cross-origin MFEs, whose host page may already
    // define its own `window.log`/etc. (e.g. a telemetry wrapper) -- an unconditional assignment would
    // silently clobber it for the whole session.
    window.focusFirstInvalidField ??= ns.focusFirstInvalidField;
    window.log ??= ns.log;
    window.logError ??= ns.logError;
    window.logWarn ??= ns.logWarn;
    window.logInfo ??= ns.logInfo;
})();
