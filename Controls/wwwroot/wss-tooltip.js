// Auto-places [data-tooltip] hover tooltips (see wss-controls.css) AND the LabelTooltip
// component's popover (.edit-tooltip-container, see edit-controls.css) toward the center of their
// container, cursor-aware, so authors never have to pick a direction. It also gives [data-tooltip]
// the two things a pure-CSS tooltip cannot have: a real accessible description (WCAG 4.1.2/1.1.1)
// and an Escape dismissal (WCAG 1.4.13) — see the second half of the file.
//
// Placement: "container" is the nearest clipping ancestor or recognized panel boundary (a
// modal/drawer/popover panel) if there is one, else the screen — so a tooltip inside a Modal aims
// at the modal's center, not the screen's, and doesn't run past the modal's own edges. It re-derives
// placement on hover/focus via event delegation, then toggles
// the placement classes both stylesheets understand (wss-tooltip-top / wss-tooltip-left /
// wss-tooltip-right — one shared vocabulary, one placement engine). An element that carries an
// explicit placement class (including the manual-only wss-tooltip-side-left /
// wss-tooltip-side-right) is treated as an override and left untouched.
//
// Optional for data-tooltip: the CSS tooltip works without this script (it always opens below the
// element, has no accessible description, and can't be dismissed — the hover/focus reveal is the
// no-JS floor, everything here is an enhancement on top of it, which is why an icon-only trigger
// still needs its own aria-label). Link it as a plain <script> tag — no import/export statements,
// so it also works unchanged as a side-effect ES module import — next to your other page scripts:
//   <script src="_content/WssBlazorControls/wss-tooltip.js"></script>
// LabelTooltip needs no wiring at all: the component lazily import()s this file itself on first
// render, and the window.__wssTooltipAutoPlace guard below keeps the classic-script + module-
// import combination from double-attaching the listeners.
//
// Why hover-time and not once on load: it re-derives placement every hover, so it follows the
// element as the page scrolls or relayouts, and it survives Blazor re-renders resetting `class`
// (we recompute before the tooltip's 0.35s reveal delay elapses).
(function () {
    'use strict';

    if (window.__wssTooltipAutoPlace) return;
    window.__wssTooltipAutoPlace = true;

    // Classes this helper sets/clears. side-left/side-right are manual-only — their presence marks
    // an explicit override.
    var MANAGED = ['wss-tooltip-top', 'wss-tooltip-left', 'wss-tooltip-right'];
    var MANUAL_ONLY = ['wss-tooltip-side-left', 'wss-tooltip-side-right'];

    function isManualOverride(el) {
        if (el.dataset.wssTooltipAuto === '1') return false; // we placed it — ours to keep managing
        var i;
        for (i = 0; i < MANUAL_ONLY.length; i++) {
            if (el.classList.contains(MANUAL_ONLY[i])) return true;
        }
        for (i = 0; i < MANAGED.length; i++) {
            if (el.classList.contains(MANAGED[i])) return true; // author chose a direction — respect it
        }
        return false;
    }

    // Panel boundaries that don't necessarily clip their own overflow (e.g. Modal's .wss-modal card
    // has visible overflow — only the surrounding .wss-modal-wrap scrolls) but still mark the visual
    // edge a tooltip shouldn't cross. A tooltip trigger in a modal/drawer header or footer has no
    // clipping ancestor of its own at that width, so without this a "below" tooltip on a footer
    // button just runs past the panel into the mask. Extend this list with your own app's panel
    // classes if it has modal/drawer components outside this library.
    var BOUNDARY_CLASSES = ['wss-modal', 'wss-drawer', 'wss-popover'];

    // Walks up from an element to find the nearest ancestor whose box a tooltip should stay inside:
    // either something that actually clips (any overflow other than visible on either axis — a
    // modal body, a scroll panel) or a recognized panel boundary. Its rect becomes the frame
    // tooltips center against instead of the viewport, which is the wrong frame once that box is
    // smaller than the screen — the case that lets tooltips run off a modal's/drawer's edges even
    // while "centered" relative to the screen.
    //
    // <body> is deliberately skipped: `body { overflow-x: hidden }` is near-ubiquitous boilerplate,
    // and body's rect is the whole DOCUMENT (as tall as the page, top well above the viewport once
    // scrolled). Accepting it made every tooltip on such a page measure against a frame whose
    // bottom is thousands of pixels below the screen, so the vertical flip never fired and a
    // trigger near the viewport bottom opened downward, off-screen. A page that genuinely wants a
    // body-sized frame gets the viewport instead, which is the same box for an unscrolled page.
    function nearestBoundsRect(el) {
        var node = el.parentElement;
        while (node && node !== document.documentElement) {
            if (node === document.body) {
                node = node.parentElement;
                continue;
            }
            var cs = getComputedStyle(node);
            if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') return node.getBoundingClientRect();
            for (var i = 0; i < BOUNDARY_CLASSES.length; i++) {
                if (node.classList.contains(BOUNDARY_CLASSES[i])) return node.getBoundingClientRect();
            }
            node = node.parentElement;
        }
        return null;
    }

    // Intersects a bounds rect with the viewport, because only the visible part of a frame is
    // somewhere a tooltip can actually open: a scroll container taller than the screen (or one
    // scrolled partly out of view) otherwise reintroduces exactly the off-screen placement the
    // frame exists to prevent — "centered in its container" is useless if that container's center
    // is below the fold. A no-op for the common cases (a modal panel or a scroll region entirely
    // on screen). Returns a plain {left, top, width, height} — not a DOMRect — and falls back to
    // null (meaning "use the viewport") when the intersection is empty or inverted, i.e. the frame
    // is scrolled completely out of view; the trigger can't be visible either in that case, so
    // there is nothing better to aim at.
    function clipToViewport(rect, vw, vh) {
        if (!rect) return null;
        var left = Math.max(rect.left, 0);
        var top = Math.max(rect.top, 0);
        var right = Math.min(rect.right, vw);
        var bottom = Math.min(rect.bottom, vh);
        if (right <= left || bottom <= top) return null;
        return { left: left, top: top, width: right - left, height: bottom - top };
    }

    function place(el) {
        if (!el.classList.contains('edit-tooltip-container')) {
            // data-tooltip bubbles are hidden entirely under hover:none (touch), so there is
            // nothing to place. LabelTooltip still opens on tap-focus there, so it always places.
            if (window.matchMedia && window.matchMedia('(hover: none)').matches) return;
            if (!el.getAttribute('data-tooltip')) return;
        }
        if (isManualOverride(el)) return;

        var r = el.getBoundingClientRect();
        if (!r.width && !r.height) return;

        var vw = window.innerWidth || document.documentElement.clientWidth;
        var vh = window.innerHeight || document.documentElement.clientHeight;
        var bounds = clipToViewport(nearestBoundsRect(el), vw, vh);
        var boundLeft = bounds ? bounds.left : 0;
        var boundTop = bounds ? bounds.top : 0;
        var w = bounds ? bounds.width : vw;
        var h = bounds ? bounds.height : vh;
        var cx = r.left + r.width / 2 - boundLeft;
        var cy = r.top + r.height / 2 - boundTop;

        MANAGED.forEach(function (c) { el.classList.remove(c); });

        // Vertical: default below (its wider gap clears the cursor, which sits below-and-right of the
        // pointer). Flip above only when the element sits low enough that a below tooltip would run
        // past the bottom of its container — above is inherently clear of the cursor.
        if (cy > h * 0.6) el.classList.add('wss-tooltip-top');

        // Horizontal: near a side edge of its container, open toward center so the bubble doesn't
        // run past that edge.
        if (cx > w * 0.66) el.classList.add('wss-tooltip-left');        // right of center -> open left
        else if (cx < w * 0.34) el.classList.add('wss-tooltip-right');  // left of center  -> open right

        el.dataset.wssTooltipAuto = '1';
    }

    // ────────────────────────────────────────────────────────────────────────────────────────
    // Accessibility layer — [data-tooltip] only.
    //
    // The bubble is CSS generated content (::after { content: attr(data-tooltip) }): there is no
    // node, so there is nothing for assistive tech to reach and nothing aria-describedby could
    // point at — and under (hover: none) the stylesheet removes it outright, so on a phone the
    // text can never exist at all. While a trigger is showing we therefore mirror its text into
    // one shared visually-hidden role="tooltip" node and point the trigger's aria-describedby at
    // it (WCAG 4.1.2 / 1.1.1), and Escape marks the trigger dismissed (WCAG 1.4.13).
    //
    // LabelTooltip is deliberately excluded from all of it: it renders its own real role="tooltip"
    // element, owns its aria-describedby, and handles Escape in C# (LabelTooltip.razor). Every
    // function below bails on a missing/empty data-tooltip attribute, which is exactly the set of
    // elements LabelTooltip's trigger is not in.
    // ────────────────────────────────────────────────────────────────────────────────────────

    var DESC_ID = 'wss-tooltip-desc';

    // The trigger whose tooltip is currently revealed. At most one, since hover and focus are both
    // single-element states; retargeting to another trigger releases this one first.
    var current = null;
    var releaseQueued = false;

    function tooltipText(el) {
        return el && el.nodeType === 1 ? el.getAttribute('data-tooltip') : null;
    }

    // Is the pointer or focus still on the trigger? Drives when the description is released and the
    // dismissed state re-armed. Deliberately looser than isShowing() below — a description that
    // outlives its bubble by a moment is harmless, one that vanishes while the trigger still holds
    // focus can disappear mid-announcement. Both must be gone before a dismissed tooltip re-arms,
    // so moving the mouse off a still-focused trigger doesn't pop the bubble straight back.
    function isEngaged(el) {
        if (!el || !el.isConnected) return false;
        if (el.matches(':hover')) return true;
        var a = document.activeElement;
        return !!a && (a === el || el.contains(a));
    }

    // Is a bubble actually on screen right now? Mirrors the stylesheet's reveal conditions, because
    // Escape may only swallow the keypress when there is really something to dismiss — otherwise a
    // trigger sitting in a Modal would eat the Escape that should have closed the dialog.
    function isShowing(el) {
        if (!isEngaged(el) || !tooltipText(el)) return false; // [data-tooltip=""] is suppressed too
        if (window.matchMedia && window.matchMedia('(hover: none)').matches) return false; // touch: hidden entirely
        try {
            // A mouse click leaves :hover + :focus but not :focus-visible; the stylesheet hides
            // that case ("suppress after a mouse click"), so there is no bubble to dismiss either.
            if (el.matches(':focus:not(:focus-visible)')) return false;
            return el.matches(':hover, :focus-visible');
        } catch (e) {
            return true; // no :focus-visible support — fall back to isEngaged's answer
        }
    }

    // One node for the whole page, created on first show. Visually hidden with inline styles rather
    // than the .wss-sr-only class (same technique, wss-controls.css): this is the only channel AT
    // has to the tooltip text, so it must not depend on a stylesheet the host page might not have
    // linked. getElementById first, so a second evaluation of this module — or a leftover from a
    // prerendered pass — reuses the existing node instead of stacking duplicates.
    function descNode() {
        var n = document.getElementById(DESC_ID);
        if (n) return n;
        n = document.createElement('div');
        n.id = DESC_ID;
        n.setAttribute('role', 'tooltip');
        n.style.cssText = 'position:absolute;width:1px;height:1px;padding:0;margin:-1px;overflow:hidden;clip-path:inset(50%);white-space:nowrap;border:0;';
        document.body.appendChild(n);
        return n;
    }

    // aria-describedby is append-then-restore, never overwrite: the trigger may already point at
    // its own hint or error text. The original is stashed on the element (dataset naming follows
    // the existing wssTooltipAuto) so release() can put it back verbatim.
    function describe(el) {
        var text = tooltipText(el);
        if (!text) return;
        var existing = (el.getAttribute('aria-describedby') || '').trim();
        if (existing.split(/\s+/).indexOf(DESC_ID) >= 0) return; // already ours — stay idempotent
        descNode().textContent = text;
        if (existing) {
            el.dataset.wssTooltipDescribedby = existing;
            el.setAttribute('aria-describedby', existing + ' ' + DESC_ID);
        } else {
            el.setAttribute('aria-describedby', DESC_ID);
        }
    }

    function undescribe(el) {
        if (!el) return;
        var v = el.getAttribute('aria-describedby');
        if (v === null || v.split(/\s+/).indexOf(DESC_ID) < 0) return; // not ours — leave it alone
        var original = el.dataset.wssTooltipDescribedby;
        if (original) {
            el.setAttribute('aria-describedby', original);
            delete el.dataset.wssTooltipDescribedby;
        } else {
            el.removeAttribute('aria-describedby');
        }
        // Nothing references the shared node now — don't leave stale text in the DOM for a screen
        // reader browsing the page linearly.
        var n = document.getElementById(DESC_ID);
        if (n) n.textContent = '';
    }

    function show(el) {
        if (!tooltipText(el)) return;
        // Same trigger (a mouseover on one of its children, or focus following hover): leave its
        // state alone, or moving the pointer inside the trigger would resurrect a tooltip the user
        // just dismissed.
        if (el === current) return;
        if (current) release(current);
        current = el;
        describe(el);
    }

    // Fully re-arms a trigger: drops the description and the dismissed state so its next
    // hover/focus starts clean.
    function release(el) {
        if (!el) return;
        undescribe(el);
        el.classList.remove('wss-tooltip-dismissed');
        if (current === el) current = null;
    }

    // Hover/focus loss is resolved on the next frame instead of straight out of mouseout/focusout:
    // the leave event for the old element fires before the enter event for the new one, and hover
    // state hasn't necessarily settled by then. Deferring lets a retarget (trigger A -> trigger B)
    // resolve itself through show(), leaving this as a pure "is anything still engaged" check.
    function scheduleRelease() {
        if (!current || releaseQueued) return;
        releaseQueued = true;
        requestAnimationFrame(function () {
            releaseQueued = false;
            if (current && !isEngaged(current)) release(current);
        });
    }

    // WCAG 1.4.13 dismissable: Escape hides the bubble without moving the pointer or focus. The
    // class is the whole mechanism — wss-controls.css's .wss-tooltip-dismissed rule out-!importants
    // the :hover/:focus-visible reveal — so a page without this script simply keeps the plain CSS
    // tooltip rather than breaking.
    function onKeyDown(e) {
        if (e.key !== 'Escape' && e.key !== 'Esc') return;
        var el = current;
        if (!el) return;
        // Nothing on screen to dismiss (already dismissed, or the bubble is suppressed): let the
        // key through, so a second Escape still closes an enclosing Modal/Drawer.
        if (el.classList.contains('wss-tooltip-dismissed') || !isShowing(el)) return;
        el.classList.add('wss-tooltip-dismissed');
        undescribe(el); // the description goes with the bubble; the class stays until leave
        // Swallow this one press so the same Escape doesn't also close the Modal/Drawer the trigger
        // sits in — mirrors LabelTooltip.razor's @onkeydown:stopPropagation="@(!_isDismissed)".
        // Capture phase, so the panel-scoped Blazor handler never sees it.
        e.stopPropagation();
        e.preventDefault();
    }

    function handle(e) {
        var t = e.target;
        if (!t || t.nodeType !== 1 || !t.closest) return;
        var el = t.closest('[data-tooltip], .edit-tooltip-container');
        if (!el) return;
        place(el);
        show(el); // no-op for LabelTooltip — it owns its own ARIA and Escape handling in C#
    }

    // Capture phase so we run even if a handler stops propagation; mouseover/focusin (and their
    // mouseout/focusout counterparts) all bubble, so a single document-level listener covers
    // dynamically-added elements without re-scanning. The leave listeners are unconditional but
    // cost nothing while no tooltip is showing (scheduleRelease returns on a null `current`).
    document.addEventListener('mouseover', handle, true);
    document.addEventListener('focusin', handle, true);
    document.addEventListener('mouseout', scheduleRelease, true);
    document.addEventListener('focusout', scheduleRelease, true);
    document.addEventListener('keydown', onKeyDown, true);
})();
