// Roving-tabindex keyboard navigation support for DatePicker/DateRangePicker's day grid.
// C# (OnGridKeyDown in DatePicker.razor.cs / DateRangePicker.razor.cs) owns the actual navigation
// logic and the tabindex="0"/"-1" bookkeeping; this module only does the two things C# can't:
//   - suppress the browser's native scroll-on-arrow-key (and Home/End/PageUp/PageDown) behavior,
//     which would otherwise fight the calendar's own up/down/paging semantics (Blazor has no
//     per-key preventDefault — see wss-select.js's initInput for the established precedent), and
//   - move real DOM focus to the newly-targeted day button after a month-crossing move, where the
//     grid re-renders with brand-new button instances and the previously-focused element is gone.
// Degrades gracefully: without this module the C# state (and the tabindex it drives) still updates
// on every keypress — only the DOM focus follow and the page-scroll suppression are lost.

const WSS_PICKER_NAV_KEYS = new Set([
    'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End', 'PageUp', 'PageDown',
]);

// One-time wiring per grid element. The grid lives inside the dropdown's `@if (_open)` block, so a
// fresh element (and therefore a fresh call here) shows up every time the picker opens; the handler
// itself is torn down for free when that subtree unmounts on close, but dispose() below also
// supports the tidy path if a caller ever wants to unwire without discarding the element.
export function init(gridEl) {
    if (!gridEl || gridEl.__wssPickerNavHandler) {
        return;
    }
    const handler = e => {
        // Only the calendar's own day buttons get the suppression — Tab, Enter and Escape (and
        // anything landing outside a day button) must keep their native behavior.
        if (WSS_PICKER_NAV_KEYS.has(e.key) && e.target instanceof Element && e.target.classList.contains('wss-picker-day')) {
            e.preventDefault();
        }
    };
    gridEl.__wssPickerNavHandler = handler;
    gridEl.addEventListener('keydown', handler);
}

export function dispose(gridEl) {
    if (gridEl && gridEl.__wssPickerNavHandler) {
        gridEl.removeEventListener('keydown', gridEl.__wssPickerNavHandler);
        gridEl.__wssPickerNavHandler = null;
    }
}

// Moves real DOM focus to the day button for `dateStr` (yyyy-MM-dd, invariant) within `root` (the
// dropdown panel — DateRangePicker searches across both grids in one call since either panel could
// currently show the date). Called from OnAfterRenderAsync once the grid has (possibly) re-rendered
// with the new month, so the target button is guaranteed to exist if the date is visible at all.
// Silently no-ops when it isn't (e.g. focus request raced a close) or the button can't take focus
// (a disabled day — the browser itself refuses .focus() on a disabled button; C#'s roving-tabindex
// state still tracks it as the logical target either way).
export function focusDay(root, dateStr) {
    if (!root) {
        return;
    }
    // Don't steal focus the user has since moved elsewhere: this call is a separate, delayed
    // round trip (keydown -> render -> OnAfterRenderAsync -> here), and on Blazor Server it can
    // land after the user has already tabbed to a text input or a month/year select and started
    // typing. Only move focus when it is still on a day button inside this root (the roving case)
    // or nowhere at all (body/null — the previously-focused day button was re-rendered away, which
    // is exactly the situation this function exists to repair).
    const active = document.activeElement;
    const activeIsDay = active instanceof Element
        && active.classList.contains('wss-picker-day')
        && root.contains(active);
    if (active && active !== document.body && !activeIsDay) {
        return;
    }
    // Prefer the cell that actually carries the roving tabindex="0". Near a month boundary the
    // same date can exist twice in the DateRangePicker's two 42-cell grids — once as the real
    // in-month cell, once as the other panel's dimmed leading/trailing duplicate — and a plain
    // data-date match returns whichever comes first in DOM order. That's the left panel's dimmed
    // duplicate whenever a forward move lands on the right panel's 1st-of-month, so an unqualified
    // selector would park DOM focus on the greyed, tabindex="-1" cell instead of the real one. C#
    // always sets the roving stop on the correct in-month cell before invoking focusDay, so the
    // precise match is the normal path; the untargeted fallback only matters if that invariant is
    // ever violated (or for DatePicker's single grid, where the two selectors never differ).
    const el = root.querySelector(`.wss-picker-day[data-date="${dateStr}"][tabindex="0"]`)
        || root.querySelector(`.wss-picker-day[data-date="${dateStr}"]`);
    try { el && el.focus(); } catch { /* not focusable / gone */ }
}
