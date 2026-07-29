// Minimal, self-contained helper for the WssBlazorControls Select component.
// Keeps the keyboard-highlighted option visible inside the virtualized dropdown, flips the
// dropdown above the control when there's no room below, and tames the search input's native
// key defaults (which C# handlers can't do — Blazor has no per-key preventDefault).
// No third-party or Ant Design dependency.
import { applyVerticalFlip, stackWithBackdrop, clearZ, wireDismissOnFocusOut } from './wss-overlay.js';
export { clearZ };

export function scrollActiveIntoView(container, index, itemSize) {
    if (!container || index < 0) {
        return;
    }
    // Rows sit below the dropdown's top padding — include it or the math is off by that amount
    // (the active row's top/bottom edge gets clipped under the dropdown edge).
    const pad = parseFloat(getComputedStyle(container).paddingTop) || 0;
    const top = pad + index * itemSize;
    const bottom = top + itemSize;
    if (top < container.scrollTop) {
        container.scrollTop = top;
    } else if (bottom > container.scrollTop + container.clientHeight) {
        container.scrollTop = bottom - container.clientHeight;
    }
}

// Opens the dropdown upward when there isn't room below (and there's more room above), so a Select
// near the bottom of the viewport doesn't push its list off-screen. The wrapper is the trigger box
// (the dropdown is absolutely positioned, so it doesn't inflate the wrapper's rect). Also stacks the
// backdrop + wrapper above any open overlay via the shared open-order counter, and RETURNS the
// wrapper's z-index so C# can mirror it into the Blazor-bound wrapper `style`: the value is written
// twice — here to the DOM immediately (no flicker) and by Blazor on its next diff — and both agree,
// so a bound-style re-render (e.g. a changed Width while open) can no longer clobber this inline write
// and drop the wrapper below its own backdrop. Degrades to the default downward CSS placement when JS
// is unavailable (the invoke throws, C# leaves its mirrored z null, and the CSS fallback applies).
export function placeDropdown(wrapper, dropdown, gap) {
    if (!wrapper || !dropdown) {
        return 0;
    }
    gap = gap || 4;

    // Stack in open order via the counter shared with wss-overlay.js (a window global under it —
    // separate modules can't share module state): backdrop below, the selector box + dropdown above
    // it, so a select opened inside a modal paints above that modal and clicking the select's own
    // input/tags/clear button doesn't hit the backdrop. clearZ (imported/re-exported above) removes
    // the inline value on close (the wrapper persists in the page, and a stale high z would poke
    // through later overlay masks).
    const z = stackWithBackdrop(wrapper, 'wss-select-backdrop');
    const w = wrapper.getBoundingClientRect();
    const dropdownHeight = dropdown.offsetHeight;
    const dropdownWidth = dropdown.offsetWidth;
    applyVerticalFlip(dropdown, w, dropdownHeight, gap);

    // Horizontal clamp: the dropdown normally hangs from the wrapper's left edge (CSS `left: 0`).
    // A dropdown wider than its trigger (long option labels, or the pill variant's content-driven
    // width) can run off the right edge of the viewport near it — mirror the vertical flip above by
    // anchoring from the wrapper's right edge instead whenever that would happen. Keeps the CSS
    // default (left: 0) whenever there's room, so this only ever moves the panel further on-screen.
    if (w.left + dropdownWidth > window.innerWidth) {
        dropdown.style.left = 'auto';
        dropdown.style.right = '0';
    } else {
        dropdown.style.right = 'auto';
        dropdown.style.left = '0';
    }

    // Hand the wrapper's z-index back so C# can re-assert it on every bound-style re-render.
    return z;
}

// Suppresses the browser defaults that fight the combobox keyboard model. Blazor's @onkeydown
// still receives every event (preventDefault does not stop propagation):
//  - Enter: would trigger the enclosing form's implicit submission while picking an option.
//  - ArrowUp/Down: would jump the caret to the start/end of the search text while moving the
//    list highlight.
//  - Home/End (open only): navigate the list, not the caret. When closed the caret keeps them.
//  - Escape: type="search" natively clears the text and fires an input event, which would
//    re-open the dropdown the component just closed. The component owns the text lifecycle.
// Degrades gracefully: without JS everything still works, minus these polish behaviors.
export function initInput(input, wrapper) {
    if (input && !input.__wssKeysWired) {
        input.__wssKeysWired = true;
        input.addEventListener('keydown', e => {
            const key = e.key;
            if (key === 'Enter' || key === 'ArrowUp' || key === 'ArrowDown' || key === 'Escape') {
                e.preventDefault();
            } else if ((key === 'Home' || key === 'End') && input.getAttribute('aria-expanded') === 'true') {
                e.preventDefault();
            } else if (key === ' ' && input.readOnly) {
                // Space opens a closed non-searchable select (readonly input) — without this the
                // browser's default scrolls the page. A searchable input keeps Space for typing.
                e.preventDefault();
            }
        });
    }

    // Tabbing away used to leave the dropdown open with its invisible backdrop silently swallowing
    // the next click anywhere on the page (routes through the component's own close path).
    wireDismissOnFocusOut(wrapper, 'wss-select-backdrop');
}
