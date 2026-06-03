// Minimal, self-contained helper for the WssBlazorControls Select component.
// The only thing it does is keep the keyboard-highlighted option visible
// inside the virtualized, scrollable dropdown (which cannot be done from C#
// without touching scrollTop). No third-party or Ant Design dependency.

export function scrollActiveIntoView(container, index, itemSize) {
    if (!container || index < 0) {
        return;
    }
    const top = index * itemSize;
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
