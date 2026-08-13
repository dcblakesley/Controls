// Minimal, self-contained helper for the WssBlazorControls Select component.
// Keeps the keyboard-highlighted option visible inside the virtualized dropdown, flips the
// dropdown above the control when there's no room below, and tames the search input's native
// key defaults (which C# handlers can't do — Blazor has no per-key preventDefault).
// No third-party or Ant Design dependency.
import { placeAnchoredPanel, clearZ, wireDismissOnFocusOut } from './wss-overlay.js';
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

// Opens the dropdown upward when there isn't room below, stacks the backdrop + wrapper above any open
// overlay in open order, clamps the dropdown horizontally into the viewport, and returns the wrapper's
// z-index for C# to mirror into the Blazor-bound `style` — the whole sequence lives in
// wss-overlay.js's placeAnchoredPanel, which this and placePanel both drive (they were the same ~15
// lines apart from the three knobs supplied below). clearZ (imported/re-exported above) removes the
// inline z on close: the wrapper persists in the page, and a stale high z would poke through later
// overlay masks.
//
// The two select-specific knobs:
//  - Edge margin drops to 0 for a dropdown too wide to inset. min-width: 100% ties the dropdown to its
//    trigger, so a full-bleed select on a phone legitimately produces one as wide as the screen, and
//    insetting that one would clip options off its right edge where it previously sat aligned and
//    fully visible.
//  - `right: auto` neutralizes the stale right anchor left over from when this right-anchored the
//    dropdown instead of clamping it (right-anchoring kept overflowing whenever the wrapper's own
//    right edge was at/past the viewport's, and for a dropdown wider than the remaining room it pushed
//    the dropdown's LEFT edge off-screen — unreachable content, strictly worse than the right-side
//    clipping it was avoiding, which clampAxis prefers instead).
export function placeDropdown(wrapper, dropdown, gap) {
    return placeAnchoredPanel(wrapper, dropdown, 'wss-select-backdrop', gap,
        (dropdownWidth, viewportWidth) => (dropdownWidth <= viewportWidth - 16 ? 8 : 0), true);
}

// Suppresses the browser defaults that fight the combobox keyboard model. Blazor's @onkeydown
// still receives every event (preventDefault does not stop propagation):
//  - Enter: would trigger the enclosing form's implicit submission while picking an option.
//  - ArrowUp/Down: would jump the caret to the start/end of the search text while moving the
//    list highlight.
//  - Home/End (open only): navigate the list, not the caret. When closed the caret keeps them.
//  - Escape: type="search" natively clears the text and fires an input event, which would
//    re-open the dropdown the component just closed. The component owns the text lifecycle.
//  - Space (select-only combobox): opens the popup when closed and selects the active option when
//    open — both would otherwise also scroll the page.
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
            } else if (key === ' ' && isSelectOnly(input)) {
                // Space carries a real action on a select-only combobox in BOTH states (open: select
                // the active option; closed: open the popup — see Select.razor.cs's " " case), so the
                // page-scroll default has to go in both. A searchable input keeps Space for typing.
                e.preventDefault();
            }
        });
    }

    // Tabbing away used to leave the dropdown open with its invisible backdrop silently swallowing
    // the next click anywhere on the page (routes through the component's own close path).
    wireDismissOnFocusOut(wrapper, 'wss-select-backdrop');
}

// "This combobox takes no typed text" (ShowSearch=false). The `readonly` attribute is what enforces
// that in the DOM, and `.readOnly` reads it directly — deliberately NOT the aria-readonly="false" the
// input also carries, which exists to stop screen readers announcing "read only" about a widget whose
// value the arrows/Enter/Space/type-ahead all change. The two are describing different things and
// must not be conflated: one is "the text box rejects keystrokes", the other "the value is fixed".
// The missing aria-autocomplete is the same signal from the ARIA side, kept as a fallback in case a
// future variant drops the attribute.
function isSelectOnly(input) {
    return input.readOnly || !input.hasAttribute('aria-autocomplete');
}
