// Auto-places [data-tooltip] hover tooltips (see wss-controls.css) AND the LabelTooltip
// component's popover (.edit-tooltip-container, see edit-controls.css) toward the center of their
// container, cursor-aware, so authors never have to pick a direction. "Container" is the nearest
// clipping ancestor or recognized panel boundary (a modal/drawer/popover panel) if there is one,
// else the screen — so a tooltip inside a Modal aims at the modal's center, not the screen's, and
// doesn't run past the modal's own edges. Runs on hover/focus via event delegation, then toggles
// the placement classes both stylesheets understand (wss-tooltip-top / wss-tooltip-left /
// wss-tooltip-right — one shared vocabulary, one placement engine). An element that carries an
// explicit placement class (including the manual-only wss-tooltip-side-left /
// wss-tooltip-side-right) is treated as an override and left untouched.
//
// Optional for data-tooltip: the CSS tooltip works without this script (always opens below the
// element). Link it as a plain <script> tag — no import/export statements, so it also works
// unchanged as a side-effect ES module import — next to your other page scripts:
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
    function nearestBoundsRect(el) {
        var node = el.parentElement;
        while (node && node !== document.documentElement) {
            var cs = getComputedStyle(node);
            if (cs.overflowX !== 'visible' || cs.overflowY !== 'visible') return node.getBoundingClientRect();
            for (var i = 0; i < BOUNDARY_CLASSES.length; i++) {
                if (node.classList.contains(BOUNDARY_CLASSES[i])) return node.getBoundingClientRect();
            }
            node = node.parentElement;
        }
        return null;
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

        var bounds = nearestBoundsRect(el);
        var boundLeft = bounds ? bounds.left : 0;
        var boundTop = bounds ? bounds.top : 0;
        var w = bounds ? bounds.width : (window.innerWidth || document.documentElement.clientWidth);
        var h = bounds ? bounds.height : (window.innerHeight || document.documentElement.clientHeight);
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

    function handle(e) {
        var t = e.target;
        if (!t || t.nodeType !== 1 || !t.closest) return;
        var el = t.closest('[data-tooltip], .edit-tooltip-container');
        if (el) place(el);
    }

    // Capture phase so we run even if a handler stops propagation; mouseover/focusin both bubble,
    // so a single document-level listener covers dynamically-added elements without re-scanning.
    document.addEventListener('mouseover', handle, true);
    document.addEventListener('focusin', handle, true);
})();
