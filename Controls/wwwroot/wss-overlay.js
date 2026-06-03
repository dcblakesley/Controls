// Edge-aware positioning for the popover / popconfirm panels. Keeps the CSS placement model
// (panel is absolute, relative to its wrap) and only:
//   * flips to the opposite side when the preferred side has no room, and
//   * shifts along the cross axis (via the --wss-shift CSS variable) so the panel stays in the viewport.
// Pure DOM, no dependencies. When this module isn't available (server prerender, unit tests) the
// component simply keeps the default CSS placement — so it degrades gracefully.
export function place(trigger, panel, prefix, placement, gap, margin) {
    if (!trigger || !panel) {
        return placement;
    }
    gap = gap || 10;
    margin = margin || 8;

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

    // Apply the (possibly flipped) placement class so the arrow ends up on the correct edge.
    if (place !== placement) {
        ['top', 'bottom', 'left', 'right'].forEach(d =>
            panel.classList.toggle(`${prefix}-${d}`, d === place));
    }
    return place;
}
