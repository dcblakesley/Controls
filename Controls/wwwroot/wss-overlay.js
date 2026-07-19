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

// Popover/popconfirm trigger contract. The consumer's ChildContent normally contains the
// interactive element (typically a <button>): its native click/Enter/Space activation bubbles to
// the wrapper's Blazor @onclick, so the wrapper itself carries no button semantics (it used to be
// role="button", which nested a button inside a button — two tab stops, invalid ARIA).
//
// syncTrigger is split into a one-time wiring step (a single keydown + focusin listener per
// wrapper) and a re-runnable apply step. applyTrigger re-resolves the trigger child on EVERY call,
// so a child that is swapped (a busy <span> ⇄ an idle <button>) or arrives late never leaves stale
// ARIA on a detached node, and wrapper promotion is reversible: the wrapper is promoted to
// role="button" (with keyboard activation) only while the content has nothing focusable, and
// demoted again the moment a focusable child appears.
//
// ARIA: aria-haspopup="dialog" + aria-expanded track the popup on the resolved child; while
// disabled both drop off and (for an interactive child) aria-disabled="true" is mirrored onto it —
// but only the aria-disabled we set is ever removed, so a consumer's own aria-disabled is left
// alone. Keyboard (only the promoted-wrapper and non-button child paths — a <button>'s native
// Enter/Space click already bubbles): a [tabindex] element toggles on Enter and Space, an anchor
// toggles on Space only (Enter is its native click), and input/select/textarea keep their editing
// semantics — a <button> child is the recommended trigger. Because the C# call site skips
// syncTrigger while (open, disabled) is unchanged (avoiding an interop round trip per render), the
// focusin listener re-applies the ARIA just before the user interacts, repairing a child swapped
// while the component was otherwise idle. Degrades gracefully: without JS an interactive child
// still toggles via its bubbled click — only the popup ARIA and the plain-content keyboard path
// are lost.

// The trigger-child selector deliberately differs from WSS_FOCUSABLE: a currently-disabled button
// (etc.) is still the trigger element (the consumer may re-enable it), so it must be found here —
// no :not([disabled]) filter — and the wrapper must not be promoted around it.
const WSS_TRIGGER_SELECTOR = 'button, a[href], input, select, textarea, [tabindex]';

export function syncTrigger(el, open, disabled) {
    if (!el) {
        return;
    }
    if (!el.__wssTriggerWired) {
        el.__wssTriggerWired = true;

        el.addEventListener('keydown', e => {
            const state = el.__wssTriggerState;
            if (!state || state.disabled) {
                return;
            }
            if (e.key !== 'Enter' && e.key !== ' ' && e.key !== 'Spacebar') {
                return;
            }
            const child = el.querySelector(WSS_TRIGGER_SELECTOR);
            const target = child || el;
            if (!child) {
                // Promoted wrapper: only the wrapper itself activates, and Enter/Space synthesize a
                // click through the Blazor handler (Space's page scroll is suppressed).
                if (e.target !== el) {
                    return;
                }
                e.preventDefault();
                el.click();
                return;
            }
            // Interactive child: only act when the key landed on it (or within it).
            if (e.target !== target && !target.contains(e.target)) {
                return;
            }
            const tag = target.tagName;
            if (tag === 'BUTTON') {
                return; // native Enter+Space fire a click that already bubbles to the wrapper
            }
            if (tag === 'INPUT' || tag === 'SELECT' || tag === 'TEXTAREA') {
                return; // editing semantics win — a <button> child is the recommended trigger
            }
            if (tag === 'A' && target.hasAttribute('href')) {
                if (e.key === 'Enter') {
                    return; // an anchor's native click handles Enter
                }
                e.preventDefault(); // Space would scroll the page; synthesize the toggle instead
                el.click();
                return;
            }
            // A generic focusable child (e.g. a [tabindex] span) never fires a native click.
            e.preventDefault();
            el.click();
        });

        // Repairs ARIA on a child swapped while the component was idle (the C# call site skips the
        // sync when open/disabled are unchanged), right before the user interacts with it.
        el.addEventListener('focusin', () => {
            const state = el.__wssTriggerState;
            if (state) {
                applyTrigger(el, state.open, state.disabled);
            }
        });
    }
    el.__wssTriggerState = { open, disabled };
    applyTrigger(el, open, disabled);
}

// Re-resolves the trigger child and (re)applies the wrapper promotion + popup ARIA. Runs on every
// syncTrigger call and on focusin, so it is idempotent and reversible.
function applyTrigger(el, open, disabled) {
    const child = el.querySelector(WSS_TRIGGER_SELECTOR);
    const target = child || el;
    const fallback = !child;

    // The resolved target can change identity while the old element stays in the DOM (a focusable
    // element inserted before the current trigger, or the current one losing its href/tabindex
    // match). Strip the popup ARIA — and any aria-disabled we set — off the previous target so two
    // elements never announce the popup at once. (The common @if-swap detaches the old node, which
    // needs no cleanup; this covers the attached-survivor case.)
    const prev = el.__wssPrevTarget;
    if (prev && prev !== target) {
        prev.removeAttribute('aria-haspopup');
        prev.removeAttribute('aria-expanded');
        if (prev.__wssAriaDisabledByWss) {
            prev.removeAttribute('aria-disabled');
            prev.__wssAriaDisabledByWss = false;
        }
    }
    el.__wssPrevTarget = target;

    if (fallback) {
        // No focusable content — promote the wrapper itself to the button role.
        el.setAttribute('role', 'button');
        el.tabIndex = disabled ? -1 : 0;
        if (disabled) {
            el.setAttribute('aria-disabled', 'true');
        } else {
            el.removeAttribute('aria-disabled');
        }
    } else if (el.getAttribute('role') === 'button') {
        // A focusable child (re)appeared where the wrapper was previously promoted — demote it so a
        // stray tab stop / role isn't left wrapped around a real interactive element.
        el.removeAttribute('role');
        el.removeAttribute('tabindex');
        el.removeAttribute('aria-disabled');
    }

    if (disabled) {
        target.removeAttribute('aria-haspopup');
        target.removeAttribute('aria-expanded');
        if (!fallback && !target.hasAttribute('aria-disabled')) {
            // Mark an interactive child inert (the wrapper's own click is guarded in C#). Set — and
            // flag as ours — only when the attribute is absent: if the consumer already manages
            // aria-disabled on this element, re-enabling must leave their value untouched. (Once we
            // set it, later passes see our own attribute and the flag stays latched — correct.)
            target.setAttribute('aria-disabled', 'true');
            target.__wssAriaDisabledByWss = true;
        }
    } else {
        target.setAttribute('aria-haspopup', 'dialog');
        target.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (target.__wssAriaDisabledByWss) {
            target.removeAttribute('aria-disabled');
            target.__wssAriaDisabledByWss = false;
        }
    }
}

// Focus restoration when the popup closes — the real trigger element (interactive child or the
// promoted wrapper), re-resolved at call time so a swapped child still receives focus.
export function focusTrigger(el) {
    if (!el) {
        return;
    }
    const child = el.querySelector(WSS_TRIGGER_SELECTOR);
    try { (child || el).focus(); } catch { /* gone */ }
}

// --- DateRangePicker ---------------------------------------------------------------------------
// Field-anchored panel placement — wss-select.js/placeDropdown's model (flip above when there's no
// room below, open-order z stacking, return the wrapper z for C# to mirror into the bound style),
// generalized with a horizontal viewport clamp: the picker panel is far wider than its field, so a
// field near the right edge would otherwise push the panel off-screen. Degrades to the default CSS
// placement (below, left-aligned) when JS is unavailable.
export function placePanel(wrapper, panel, backdropClass, gap) {
    if (!wrapper || !panel) {
        return 0;
    }
    gap = gap || 4;

    const z = nextZ();
    const backdrop = wrapper.previousElementSibling;
    if (backdrop && backdropClass && backdrop.classList.contains(backdropClass)) {
        backdrop.style.zIndex = z;
    }
    wrapper.style.zIndex = z + 1;

    const w = wrapper.getBoundingClientRect();
    const ph = panel.offsetHeight;
    const pw = panel.offsetWidth;
    const roomBelow = window.innerHeight - w.bottom;
    const roomAbove = w.top;
    if (roomBelow < ph + gap && roomAbove > roomBelow) {
        panel.style.top = 'auto';
        panel.style.bottom = `calc(100% + ${gap}px)`;
    } else {
        panel.style.bottom = 'auto';
        panel.style.top = `calc(100% + ${gap}px)`;
    }

    // Shift left just enough to stay inside the right viewport edge (8px margin), never past the
    // wrapper's own distance from the left edge — a fully off-screen field stays panel-left-aligned.
    const margin = 8;
    const overflowRight = w.left + pw - (window.innerWidth - margin);
    const shift = overflowRight > 0 ? Math.min(overflowRight, Math.max(0, w.left - margin)) : 0;
    panel.style.left = `${-Math.round(shift)}px`;

    return z + 1;
}

// Removes the open-order z-index applied by placePanel once the panel closes (the wrapper persists
// in the page, and a stale high z would poke through later overlays' masks).
export function clearZ(el) {
    if (el) {
        el.style.zIndex = '';
    }
}

// One-time picker wiring. Enter in a picker input commits the typed date via the component's own
// keydown handler — preventDefault stops it also implicitly submitting an enclosing form (which C#
// can't do; Blazor has no per-key preventDefault). The wrapper focusout mirrors wss-select.js:
// tabbing away would otherwise leave the panel open with its invisible backdrop silently swallowing
// the next click — route through the backdrop's click so the component's own close path runs.
// relatedTarget is null for mouse presses on non-focusable targets; those flows are already owned
// by the backdrop/day-button click handlers, so only act when the keyboard destination is known.
export function initPicker(wrapper, startInput, endInput) {
    for (const input of [startInput, endInput]) {
        if (input && !input.__wssPickerWired) {
            input.__wssPickerWired = true;
            input.addEventListener('keydown', e => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                }
            });
        }
    }
    if (wrapper && !wrapper.__wssPickerFocusWired) {
        wrapper.__wssPickerFocusWired = true;
        wrapper.addEventListener('focusout', e => {
            if (!e.relatedTarget || wrapper.contains(e.relatedTarget)) {
                return;
            }
            const backdrop = wrapper.previousElementSibling;
            if (backdrop && backdrop.classList.contains('wss-picker-backdrop')) {
                backdrop.click();
            }
        });
    }
}

// --- Modal / Drawer focus management ---------------------------------------------------------
// Moves focus into the panel, traps Tab within it, and locks body scroll. Returns a handle whose
// dispose() restores body scroll and returns focus to the element that was focused before opening.
// Degrades to a no-op when JS is unavailable (the component swallows the failure).
const WSS_FOCUSABLE =
    'a[href],area[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])';

// Body-scroll lock is ref-counted so stacked overlays (e.g. a Modal that opens a Drawer) don't
// unlock the page when the first-opened one closes — only the last dispose restores the original.
// Shared with every other instance of this module via a window global (see __wssOverlayZ above for
// why: FormDefaults.AssetBase lets different MFEs import this file from different origin URLs, and
// the browser instantiates the module once per distinct URL — each instance's own module-scoped
// state would otherwise be invisible to the others, so two instances' opens/closes could race and
// leave the page permanently scroll-locked, or unlock it while a dialog from the other instance is
// still open).
// Shape contract: { count: number, saved: string }. `count` is the cross-instance ref-count of
// active locks — whichever activation takes it from 0 to 1 is "first" and whichever dispose takes
// it back to 0 is "last", regardless of which instance either belongs to. `saved` is the
// document.body.style.overflow value captured by that first activation, restored by that last
// dispose. Any instance may read/write these two properties; never add or depend on anything else
// here — a future instance must be able to interoperate using only this shape.
window.__wssOverlayScrollLock = window.__wssOverlayScrollLock || { count: 0, saved: '' };
function wssLockScroll() {
    const lock = window.__wssOverlayScrollLock;
    if (lock.count === 0) {
        lock.saved = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
    }
    lock.count++;
}
function wssUnlockScroll() {
    const lock = window.__wssOverlayScrollLock;
    lock.count = Math.max(0, lock.count - 1);
    if (lock.count === 0) {
        document.body.style.overflow = lock.saved;
    }
}

// Stack of active traps — only the topmost (most recently activated) one acts, so nested
// overlays behave: the inner dialog owns Tab/focus until it closes, then the outer resumes.
// Shared across module instances via a window global (same cross-instance reasoning as
// __wssOverlayScrollLock above) so two live overlays from different instances don't each believe
// they own the document — only the truly topmost trap (across every instance) acts.
// Shape contract: a plain array. Each instance pushes/splices its own opaque token (whatever object
// it likes) and only ever compares `array[array.length - 1]` against a token *it* pushed for
// reference equality — nobody reads or writes a property on an entry another instance pushed. That
// keeps the array forward-tolerant: an instance never needs to know what shape a foreign instance's
// token has, and the owning instance's own document listeners are the only ones that ever act on
// its own trap (a foreign instance's listeners see the equality check fail and no-op) — the
// callback/element-ref contract of a trap entry never has to cross instance boundaries.
window.__wssOverlayTraps = window.__wssOverlayTraps || [];

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

    const trap = {};
    const traps = window.__wssOverlayTraps;
    const isTopmost = () => traps[traps.length - 1] === trap;

    // Document-level (not panel-level) so the trap keeps working even when focus has somehow
    // landed outside the panel — a Tab from anywhere is pulled back into the cycle. Panel-level
    // listening died the moment focus escaped (e.g. a mask click), taking Escape with it.
    const onKeydown = (e) => {
        if (!isTopmost()) {
            return;
        }
        // Focus can silently land outside the panel with no focusin fired — disabling the focused
        // element (the default OK button during ConfirmLoading) or removing it drops focus to
        // <body>. The panel-scoped Blazor Escape handler then never sees the key, so pull focus
        // back into the trap and re-dispatch the key at the panel so Escape-to-close still works.
        // isTrusted gate: this capture listener also sees the synthetic event dispatched below; if
        // the focus() failed to land (e.g. an unfocusable-but-laid-out first item), re-entering
        // here would recurse without bound.
        if (e.key === 'Escape' && e.isTrusted && !panel.contains(document.activeElement)) {
            const items = focusables();
            try { (items[0] || panel).focus(); } catch { /* not focusable */ }
            panel.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
            return;
        }
        if (e.key !== 'Tab') {
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

    // Register this trap (and this instance's listeners) before grabbing initial focus below — an
    // already-open OLDER trap (this instance's own in a nested-modal case, or — since
    // __wssOverlayTraps is shared, see the contract above — another module instance's) must see
    // itself as no-longer-topmost by the time the very first focusin fires here, or its own
    // onFocusIn would race this activation and steal the initial focus straight back out of the
    // panel that's only now opening.
    document.addEventListener('keydown', onKeydown, true);
    document.addEventListener('mousedown', onMouseDown, true);
    document.addEventListener('focusin', onFocusIn);
    traps.push(trap);

    const initial = focusables();
    try { (initial[0] || panel).focus(); } catch { /* element not focusable yet */ }

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
            const i = traps.indexOf(trap);
            if (i >= 0) {
                traps.splice(i, 1);
            }
            wssUnlockScroll();
            try { if (previouslyFocused && previouslyFocused.focus) previouslyFocused.focus(); } catch { /* gone */ }
        }
    };
}
