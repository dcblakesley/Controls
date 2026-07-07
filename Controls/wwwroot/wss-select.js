// Minimal, self-contained helper for the WssBlazorControls Select component.
// Keeps the keyboard-highlighted option visible inside the virtualized dropdown, flips the
// dropdown above the control when there's no room below, and tames the search input's native
// key defaults (which C# handlers can't do — Blazor has no per-key preventDefault).
// No third-party or Ant Design dependency.

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
// (the dropdown is absolutely positioned, so it doesn't inflate the wrapper's rect). Degrades to the
// default downward CSS placement when JS is unavailable.
export function placeDropdown(wrapper, dropdown, gap) {
    if (!wrapper || !dropdown) {
        return;
    }
    gap = gap || 4;
    const w = wrapper.getBoundingClientRect();
    const dropdownHeight = dropdown.offsetHeight;
    const roomBelow = window.innerHeight - w.bottom;
    const roomAbove = w.top;
    if (roomBelow < dropdownHeight + gap && roomAbove > roomBelow) {
        dropdown.style.top = 'auto';
        dropdown.style.bottom = `calc(100% + ${gap}px)`;
    } else {
        dropdown.style.bottom = 'auto';
        dropdown.style.top = `calc(100% + ${gap}px)`;
    }
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
export function initInput(input) {
    if (!input || input.__wssKeysWired) {
        return;
    }
    input.__wssKeysWired = true;
    input.addEventListener('keydown', e => {
        const key = e.key;
        if (key === 'Enter' || key === 'ArrowUp' || key === 'ArrowDown' || key === 'Escape') {
            e.preventDefault();
        } else if ((key === 'Home' || key === 'End') && input.getAttribute('aria-expanded') === 'true') {
            e.preventDefault();
        }
    });
}
