// Edge-aware positioning for the popover / popconfirm panels. Keeps the CSS placement model
// (panel is absolute, relative to its wrap) and only:
//   * flips to the opposite side when the preferred side has no room, and
//   * shifts along the cross axis (via the --wss-shift CSS variable) so the panel stays in the viewport.
// Pure DOM, no dependencies. When this module isn't available (server prerender, unit tests) the
// component simply keeps the default CSS placement — so it degrades gracefully.
// Open-order z-index counter, shared with wss-select.js via a window global (separate modules
// can't share module state). Each overlay activation takes the next slot, so "opened later" always
// paints above "opened earlier" regardless of DOM order — fixing Modal-vs-Drawer ties and a page
// Popover out-ranking a later Modal's mask. Starts above every static CSS band (the no-JS
// fallback values) and resets at 4000 to stay under the 5000 toast layer; a reset only matters if
// overlays are stacked at that exact moment, and self-heals on the next open.
function nextZ() {
    const current = window.__wssOverlayZ;
    window.__wssOverlayZ = (current && current < 4000 ? current : 1048) + 2;
    return window.__wssOverlayZ;
}

export function place(trigger, panel, prefix, placement, gap, margin) {
    if (!trigger || !panel) {
        return placement;
    }
    gap = gap || 10;
    margin = margin || 8;

    // Stack in open order: invisible backdrop below, panel above it.
    const z = nextZ();
    const backdrop = panel.previousElementSibling;
    if (backdrop && backdrop.classList.contains(`${prefix}-backdrop`)) {
        backdrop.style.zIndex = z;
    }
    panel.style.zIndex = z + 1;

    // Start from a clean shift so measurements reflect the un-shifted panel.
    panel.style.setProperty('--wss-shift', '0px');

    const t = trigger.getBoundingClientRect();
    const pw = panel.offsetWidth;
    const ph = panel.offsetHeight;
    const vw = window.innerWidth;
    const vh = window.innerHeight;

    // Flip on the main axis only when the opposite side has room.
    let place = placement;
    if (place === 'top' && t.top - gap - ph < margin && t.bottom + gap + ph <= vh - margin) {
        place = 'bottom';
    } else if (place === 'bottom' && t.bottom + gap + ph > vh - margin && t.top - gap - ph >= margin) {
        place = 'top';
    } else if (place === 'left' && t.left - gap - pw < margin && t.right + gap + pw <= vw - margin) {
        place = 'right';
    } else if (place === 'right' && t.right + gap + pw > vw - margin && t.left - gap - pw >= margin) {
        place = 'left';
    }

    // Shift along the cross axis to keep the panel within the viewport (clamped to a margin).
    let shift = 0;
    if (place === 'top' || place === 'bottom') {
        const left = t.left + t.width / 2 - pw / 2;        // centred on the trigger
        shift = Math.max(margin, Math.min(left, vw - margin - pw)) - left;
    } else {
        const top = t.top + t.height / 2 - ph / 2;
        shift = Math.max(margin, Math.min(top, vh - margin - ph)) - top;
    }
    panel.style.setProperty('--wss-shift', `${Math.round(shift)}px`);

    // Return the resolved side; the caller owns the placement class. We deliberately don't mutate
    // classList here — the panel's class is a Blazor-bound attribute, so the re-render that drops
    // `wss-measuring` would clobber any class we set. (--wss-shift is safe: it's an inline style
    // property the markup never binds.) The `prefix` arg is kept for call-site/back-compat clarity.
    return place;
}

// Tames the popover/popconfirm trigger's native key behavior (a role="button" span, so nothing
// comes free): Space on the trigger itself must toggle without also scrolling the page, and
// Enter/Space bubbling out of interactive content nested in the trigger must not reach the toggle
// handler (Blazor's KeyboardEventArgs can't see the event target, so that filtering happens here).
// Degrades gracefully: without JS, Space additionally scrolls and nested buttons double-toggle.
export function initTrigger(el) {
    if (!el || el.__wssTriggerWired) {
        return;
    }
    el.__wssTriggerWired = true;
    el.addEventListener('keydown', e => {
        const isSpace = e.key === ' ' || e.key === 'Spacebar';
        if (e.target === el && isSpace) {
            e.preventDefault();
        } else if (e.target !== el && (isSpace || e.key === 'Enter')) {
            e.stopPropagation();
        }
    });
}

// --- Modal / Drawer focus management ---------------------------------------------------------
// Moves focus into the panel, traps Tab within it, and locks body scroll. Returns a handle whose
// dispose() restores body scroll and returns focus to the element that was focused before opening.
// Degrades to a no-op when JS is unavailable (the component swallows the failure).
const WSS_FOCUSABLE =
    'a[href],area[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

// Body-scroll lock is ref-counted so stacked overlays (e.g. a Modal that opens a Drawer) don't
// unlock the page when the first-opened one closes — only the last dispose restores the original.
let wssScrollLocks = 0;
let wssSavedBodyOverflow = '';
function wssLockScroll() {
    if (wssScrollLocks === 0) {
        wssSavedBodyOverflow = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
    }
    wssScrollLocks++;
}
function wssUnlockScroll() {
    wssScrollLocks = Math.max(0, wssScrollLocks - 1);
    if (wssScrollLocks === 0) {
        document.body.style.overflow = wssSavedBodyOverflow;
    }
}

// Stack of active traps — only the topmost (most recently activated) one acts, so nested
// overlays behave: the inner dialog owns Tab/focus until it closes, then the outer resumes.
const wssTraps = [];

export function activateModal(panel) {
    if (!panel) {
        return null;
    }
    const previouslyFocused = document.activeElement;
    wssLockScroll();

    // Stack in open order (see nextZ) — the wrap/root carries the overlay's z-index.
    const root = panel.closest('.wss-modal-wrap, .wss-drawer-root');
    if (root) {
        root.style.zIndex = nextZ();
    }

    const focusables = () =>
        Array.from(panel.querySelectorAll(WSS_FOCUSABLE))
            .filter(el => el.offsetWidth > 0 || el.offsetHeight > 0 || el === document.activeElement);

    const initial = focusables();
    try { (initial[0] || panel).focus(); } catch { /* element not focusable yet */ }

    const trap = {};
    const isTopmost = () => wssTraps[wssTraps.length - 1] === trap;

    // Document-level (not panel-level) so the trap keeps working even when focus has somehow
    // landed outside the panel — a Tab from anywhere is pulled back into the cycle. Panel-level
    // listening died the moment focus escaped (e.g. a mask click), taking Escape with it.
    const onKeydown = (e) => {
        if (e.key !== 'Tab' || !isTopmost()) {
            return;
        }
        const items = focusables();
        if (items.length === 0) {
            e.preventDefault();
            return;
        }
        const first = items[0];
        const last = items[items.length - 1];
        // indexOf is -1 when focus is on the panel itself (tabindex=-1) or outside it entirely —
        // pull it back so Tab can't escape the trap.
        const idx = items.indexOf(document.activeElement);
        if (e.shiftKey) {
            if (idx <= 0) {
                e.preventDefault();
                last.focus();
            }
        } else if (idx === -1 || idx === items.length - 1) {
            e.preventDefault();
            first.focus();
        }
    };

    // A press on the mask (or anything else outside the panel) must not steal focus out of the
    // trap — otherwise the panel-scoped Escape handler and the Tab cycle die until the user
    // clicks back in. preventDefault on mousedown suppresses the focus change, not the click.
    const onMouseDown = (e) => {
        if (isTopmost() && !panel.contains(e.target)) {
            e.preventDefault();
        }
    };

    // Focus that still lands outside (programmatic .focus(), an outer overlay restoring focus to
    // its trigger while this one is open) is routed back into the panel.
    const onFocusIn = (e) => {
        if (isTopmost() && !panel.contains(e.target)) {
            const items = focusables();
            try { (items[0] || panel).focus(); } catch { /* not focusable */ }
        }
    };

    document.addEventListener('keydown', onKeydown, true);
    document.addEventListener('mousedown', onMouseDown, true);
    document.addEventListener('focusin', onFocusIn);
    wssTraps.push(trap);

    let disposed = false;
    return {
        dispose: () => {
            if (disposed) {
                return; // idempotent — a double dispose must not over-decrement the scroll-lock count
            }
            disposed = true;
            document.removeEventListener('keydown', onKeydown, true);
            document.removeEventListener('mousedown', onMouseDown, true);
            document.removeEventListener('focusin', onFocusIn);
            const i = wssTraps.indexOf(trap);
            if (i >= 0) {
                wssTraps.splice(i, 1);
            }
            wssUnlockScroll();
            try { if (previouslyFocused && previouslyFocused.focus) previouslyFocused.focus(); } catch { /* gone */ }
        }
    };
}
