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
