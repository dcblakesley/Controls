# UiKit Accessibility Audit — 2026-08-11

Scope: every component under `Controls/UiKit/` plus its supporting assets (`wss-controls.css`, `wss-overlay.js`, `wss-tooltip.js`, `wss-table.js`, `wss-picker.js`). Audited against WCAG 2.2 AA and the WAI-ARIA Authoring Practices (APG).

Method: six parallel source audits by subsystem produced 58 raw findings; every critical/serious finding was then adversarially re-verified by an independent reviewer instructed to refute it — re-reading the cited code, hunting for mitigations elsewhere (base classes, JS modules, CSS, docs, tests), recomputing contrast ratios from relative luminance, and tracing keyboard event flows. Severities below are **post-verification**. Findings marked *(unverified)* came from the first pass with exact code citations but did not go through the refutation step.

Line numbers reflect the working tree as of 2026-08-11 (commit `cb66459` + local changes).

---

## Remediation status (2026-08-11)

- **Serious (S1-S7):** all seven fixed.
- **Moderate (M1-M11):** fixed, except:
  - PKR-5 — resolved as documented-intentional (combobox-like focus model; comment in source).
  - PKR-9 / PKR-10 — resolved as documentation.
  - CSS-12 — resolved as a doc note.
  - **Correction (second wave, below):** M11's "prefer `min-height`" px-sizing sub-item was **not**
    actually fixed by this first wave despite the blanket "fixed" above — the height→`min-height`
    sweep across `wss-controls.css` didn't happen until the second wave, which also lists its
    exceptions. And M8's "loading mask blocks pointer but not keyboard" clause was only ever fixed
    for **kit-owned** controls (sort/filter/expand/select-all, the embedded pagers, the clickable
    row) — a consumer's own `ActionColumn`/`Column` template content is not, and cannot generically
    be, disabled by the mask; this is now a documented consumer obligation rather than a code fix.
- **Minor, fixed:** OVR-5, OVR-7, TBL-11, CSS-5, CSS-6 (outside-month/decade days), CSS-7, CMP-10,
  PKR-2, PKR-4a (describedby hint; placeholders unchanged), PKR-8 (committed ranges; session preview
  intentionally unmarked).
- **Minor, deliberately unchanged:** CSS-4 (best practice), CSS-10 (spacing exception), CSS-13,
  TST-5 (watch item), OVR-8 (button-child recommendation), CMP-2 (Blazor `preventDefault`
  limitation), TBL-10 (documented JS-only indeterminate).

---

## Serious — verified, fix first

### S1. `data-tooltip` tooltips are invisible to assistive technology
**WCAG 4.1.2 / 1.1.1** — `Controls/wwwroot/wss-controls.css:1146-1171`, `wss-tooltip.js` (whole file), `README.md:318-338`

The tooltip text exists only as CSS generated content (`[data-tooltip]::after { content: attr(data-tooltip) }`). There is no `role="tooltip"` node and no `aria-describedby`; `wss-tooltip.js` computes placement classes only and writes zero ARIA. The pseudo-element is `display: none` at rest, so it is excluded from accessible-name computation, and the touch media query (`wss-controls.css:1451-1458`) hides it with `!important` — on mobile screen readers the text can *never* exist. Worst case is the library's own documented example (`README.md:323-325` and the edit-controls skill, `ui-kit-controls.md:1029`): `<button data-tooltip="Refresh the list"><RefreshIcon /></button>` — an icon-only button whose only labeling is the tooltip, i.e. **no accessible name at all**.

**Fix:** implement the pattern `LabelTooltip` already uses (real `role="tooltip"` element + `aria-describedby`), or at minimum document that `data-tooltip` on icon-only triggers must be paired with `aria-label` — and fix the README/skill example either way.

### S2. `data-tooltip` fails WCAG 1.4.13 (not hoverable, not dismissable)
**WCAG 1.4.13** — `wss-controls.css:127, 1174-1186`; `wss-tooltip.js:149-150`

Two prongs of the same criterion. (a) Not hoverable: the bubble sits across a deliberate 24px dead gap (`--wss-tooltip-gap`, positioned via `top: calc(100% + var(--wss-tooltip-gap))`) and is `pointer-events: none` — moving the pointer toward the bubble drops `:hover` and hides it. (b) Not dismissable: show/hide is pure CSS `:hover`/`:focus-visible`; `wss-tooltip.js`'s only listeners are `mouseover`/`focusin` — no Escape path exists anywhere.

The repo already solved both problems for `LabelTooltip`: an invisible bridging strip with an explicit "WCAG 1.4.13" comment (`edit-controls.css:861-885`) and an Escape handler with its enforcing CSS rule (`LabelTooltip.razor:86-89`, `edit-controls.css:923-930`).

**Fix:** port the `LabelTooltip` bridge + Escape-dismiss approach, or replace `data-tooltip` usage with a real component.

### S3. Popover/Popconfirm: `role="dialog"` with no accessible name when untitled
**WCAG 4.1.2** (axe `aria-dialog-name`) — `Popover.razor:22-25`, `Popconfirm.razor:20-21`

`aria-label="@(HasTitle ? null : AriaLabel)"` — when neither `Title` nor `AriaLabel` is set, the dialog has no name. `Modal.razor:13` and `Drawer.razor:13` already ship the fallback (`AriaLabel ?? "Dialog"` / `?? "Drawer"`) that these two omit. Aggravator: a title-less **Popconfirm renders no message text at all** — the message div is inside the `@if (HasTitle)` block (`Popconfirm.razor:32-35`), leaving only the warning icon and OK/Cancel buttons. A bUnit test currently locks the unnamed state in as intended (`UiKitDialogControlsTests.cs:405-414`). Popover is partially mitigated by an unconditional `aria-describedby` to its content; Popconfirm has none. Neither README nor the skill documents `AriaLabel` for these two components.

**Fix:** add the same `?? "Popover"` / `?? "Confirm"` fallbacks; consider rendering the Popconfirm message independent of `HasTitle`; update the bUnit test and docs.

### S4. Table `OnRowClick` is mouse-only
**WCAG 2.1.1 / 4.1.2** — `Table.razor:158-159` (row markup), `:1325` (`RowIsClickable`)

The only trigger is `@onclick` on the `<tr>` — no `tabindex`, no keydown handler, no role, no focus style (`wss-table.js` is a 5-line re-export with no listeners; `AdditionalAttributes` splats onto the root div, not the `<tr>`). Keyboard and AT users can neither discover nor operate the affordance. Documentation was checked to rule out a "consumer responsibility" defense: README (`:437-438`) and the skill discuss `OnRowClick` only in terms of click-propagation guards, never keyboard. The `ExpandRowByClick` half of `RowIsClickable` *is* covered by the keyboard-accessible chevron button; `OnRowClick` alone has no keyboard path whatsoever.

**Fix:** when `OnRowClick` is wired, add `tabindex="0"` + Enter/Space keydown + a focus ring on the row (or document that consumers must provide an equivalent in-row focusable control, and say so in README + skill).

### S5. DatePicker: arrow-keying onto a disabled day can strand keyboard access
**WCAG 2.1.1 / 2.4.3 / 2.4.7** — `DatePicker.razor:216-223`, `DatePicker.razor.cs:657-674`, `wss-picker.js:48-80`

Day cells use the native `disabled` attribute, and `OnGridKeyDown` moves the roving-tabindex target onto disabled days by design ("parking keyboard focus on a disabled day is harmless"). It isn't:

- The browser refuses `.focus()` on a disabled button (acknowledged in `wss-picker.js:48-50`), so within a month the focus ring visibly stalls and each step across a disabled run goes unannounced (moderate on its own).
- Worse: the grid's sole `tabindex="0"` now sits on a `disabled` button, so the grid has **zero tab stops** — Tab out and you cannot Tab back in for the rest of the open session. The codebase guards this exact hazard in the *default-focus* path (`DatePicker.razor.cs:626-632`: "would … make the whole grid keyboard-unreachable") but not in the arrow-key path.
- On a month-crossing arrow move, Blazor patches the 42 fixed cells in place; if the focused slot re-renders as `disabled`, the browser blurs focus to `<body>` — from there arrows and Escape no longer reach the picker's handlers, and `wss-overlay.js:515-517` early-returns on the null `relatedTarget`, so the panel doesn't dismiss either. Recovery needs the mouse or a full document Tab cycle. (This last step is inferred from Blazor diffing + HTML disabled-blur semantics, not observed in a browser — worth an e2e repro before fixing.)

Existing bUnit coverage (`DatePickerTests.cs:357-370`) tests only the default-focus guard; no test arrows onto a disabled day.

**Fix:** render candidate cells with `aria-disabled="true"` + a click guard instead of native `disabled` (the APG grid recommendation — keeps them focusable), or make `OnGridKeyDown` skip disabled days. Add bUnit/e2e coverage for both the stall and the month-crossing case.

### S6. White text on primary/error fills fails 4.5:1 (and primary-as-text on white)
**WCAG 1.4.3** — tokens `wss-controls.css:32-33, 115`; verified sites below

Independently recomputed: white on `#1890ff` = **3.24:1**, white on `#ff4d4f` = **3.27:1**; both need 4.5:1 at the 12–14px sizes used (no site qualifies for the large-text exemption — heaviest is 600-weight at 14px). Verified failing sites: `.wss-dialog-btn-primary` (:1646), `.wss-dialog-btn-danger` (:1664), `.wss-table-filter-ok` (:2496, 12px), `.wss-picker-day-selected` (:3233), `.wss-picker-ok` (:3442, 12px), `.wss-search-btn-enter` (:3737). The same `#1890ff` as plain text on white — `.wss-pagination-item-active` (:1518), `.wss-tabs-tab-active` (:3562), `.wss-picker-today-btn` (:3468) — is the identical 3.24:1 shortfall. Note: the stylesheet's own comment at `:109-114` justifies `--wss-color-error` as backing "chrome/borders/backgrounds" needing only 3:1 — but `.wss-dialog-btn-danger` uses it as a button background under white text, violating the stated rationale.

**Fix:** darken the unthemed defaults (breaks AntD-4 brand parity — a release decision), switch on-fill/selected text to dark ink, or document explicitly that consumers theming `--wss-color-primary`/`--color-primary` own this contrast obligation. This is the well-known AntD-blue gap; at minimum it should be a documented known limitation.

### S7. Toast auto-dismiss: no pause on hover/focus, no user control over timing
**WCAG 2.2.1 (Level A)** — `ToastQueue.cs:46-92`; defaults `MessageService.cs:41` (3s), `NotificationService.cs:45` (4.5s)

The timer is a single uncancellable `Task.Delay`; the only cancellation paths are developer APIs (`Remove`/`Clear`/`Dispose`). No hover/focus handlers exist anywhere in the toast pipeline (verified by repo-wide grep), and duration is an author parameter, not a user mechanism — 2.2.1 requires the *user* be able to extend. 3s/4.5s defaults are far below any exception threshold.

**Fix:** pause/restart the dismiss timer while a toast is hovered or contains focus (Ant Design's own toasts do this); consider longer defaults.

---

## Moderate

Verified:

- **M1. Message toasts have no close control** — `MessageListView.razor:38-41` renders icon + content only (contrast `NotificationListView.razor:62-64`, which has a labeled close button). Not an independent WCAG failure for transient toasts, but it aggravates S7, and a sticky message (`Loading`, `duration: 0`) is permanently undismissable by the user. Fix: add the same close button, or wire `.wss-msg` removal.
- **M2. Toast/Alert severity is never announced** — WCAG 1.1.1 (not 1.4.1: the four glyphs are structurally distinct shapes, so sighted users are fine). Icons are `aria-hidden` and no severity word is emitted; AT can distinguish error from non-error only via the split live regions, and success/info/warning/loading not at all. Applies equally to `Alert` (success/warning/info all render `role="status"`). `.wss-sr-only` already exists (`wss-controls.css:874-882`). Fix: emit `<span class="wss-sr-only">Warning: </span>` (localizable) before the content in `MessageListView`, `NotificationListView`, and `Alert`.
- **M3. SearchInput button unnamed while `Loading` + `EnterButtonText`** — `SearchInput.razor:41-57`: `aria-label` is suppressed when `EnterButtonText` is set, but during `Loading` the visible text is replaced by an `aria-hidden` spinner → empty accessible name. Downgraded from serious because the button is also `disabled` then (unreachable, browse-mode only). Fix is one expression: suppress only when `HasEnterButtonText && !Loading`. No test or demo covers the combination.
- **M4. Warning/success icon contrast** — `#faad14` = 1.90:1, `#52c41a` = 2.27:1 on white (worse on the Alert tints: 1.83:1 / 2.21:1). Moderate not critical: the glyphs are `aria-hidden` and sit beside full-contrast text. But the warning glyph is Popconfirm's **unconditional, unparameterizable** icon (`Popconfirm.razor:29-31` — no `Icon` parameter exists). Fix: darken the unthemed defaults toward AntD-5 values (~`#d48806`, `#389e0d`).
- **M5. Sub-24px targets that the 2.5.8 spacing exception cannot rescue** — Select tag-remove (10×10, `wss-controls.css:373-384`), Select clear (12×12, `:420-451`), Picker clear (14×14, `:2843-2864`): each is nested *inside* a larger click target (the selector/input that opens the dropdown), so a 24px circle necessarily intersects the enclosing target. The four close buttons (alert/modal/drawer/notification, 12–16px) pass via the spacing exception — isolated placement. No `pointer: coarse` accommodation exists anywhere. Fix: `min-width/min-height: 24px` hit boxes (glyph can stay small) on the three failing controls.

Unverified (first-pass findings with exact citations; spot-check before fixing):

- **M6. Notification container blocks clicks in its gaps** — `.wss-notification-container` (`wss-controls.css:2626-2634`) lacks the `pointer-events: none` / per-item `auto` pattern the message container has (`:2548-2581`); the 16px gaps between stacked notifications sit over the page and swallow pointer events.
- **M7. Modal/Drawer never inert/aria-hide the background** — `wss-overlay.js` `activateModal` (:578-693) implements the focus trap and scroll lock but a screen reader's virtual cursor can still read and activate content behind the dialog. Fix: toggle `inert` on siblings, ref-counted like the scroll lock.
- **M8. Table gaps** — scrollable wrapper not keyboard-focusable (`Table.razor:41-42`; axe `scrollable-region-focusable`); no accessible-name fallback when `Caption` unset; filter trigger lacks `aria-haspopup="dialog"` (every other popup trigger in the kit has it) and its name never reflects applied-filter state (`TableColumnFilter.razor:15-18, 110-111`); `Loading` and the filtered-to-empty state are never announced (`aria-busy` alone isn't; `Table.razor:27, 141-151`); every row-selection checkbox is named the same static "Select row" (`:188, 283-286`) — offer a `Func<TItem,string>` labeler; the loading mask blocks pointer but not keyboard interaction with the controls beneath it (`:321-328`, css `:1925-1934`).
- **M9. Picker structure** — no `grid`/`row`/`gridcell` roles on the calendar (plain divs; `DatePicker.razor:166-188`); month/year/decade navigation is never announced (no `aria-live` anywhere in UiKit; `PickerMonthHeader.razor:20-48`); panel is `role="dialog"` + `tabindex="-1"` but focus never moves into it on open (`PickerBase.cs:391-403` handles close only) — either move focus in or drop the dialog role; no `aria-controls`/panel `id` linking input to popup (`DatePicker.razor:24-55`); standalone (non-`EditDate`) parse errors are silent unless the consumer wires `OnParseError` (`DatePicker.razor.cs:1137-1159`); the hardcoded input `aria-label` overrides a consumer's `<label for>` (WCAG 2.5.3 risk; `DatePicker.razor:30`).
- **M10. Tabs/SearchInput/Alert details** — tabpanel lacks `tabindex="0"` (text-only panels are keyboard-unreachable; `Tabs.razor:28-31`, and `.wss-tabs-panel` sets `outline: none`); SearchInput is `type="text"` not `type="search"` (`SearchInput.razor:24`); SearchInput can ship with no label at all when only `Placeholder` is set (`SearchInput.razor.cs:102-108`), and the `AddonContent` labeling path silently requires `Id` (`:110-116`); `.wss-search-input` is excluded from the shared `:focus-visible` outline rule — its only focus cue is a border-color change on `:focus` (`wss-controls.css:3658, 3675-3679, 3801-3806`); `Alert` applies `role`/`aria-live` unconditionally, over-announcing persistent banners on mount (`Alert.razor:8-9`) — consider a `Live` opt-out.
- **M11. Stylesheet-level** — outside-month/decade day numbers use placeholder gray `#bfbfbf` (1.84:1) though they are operable buttons (`wss-controls.css:39, 3225-3227, 3366-3368`); the resting sort caret and filter funnel reuse the *disabled-text* token (~1.84:1) on fully operational controls (`:2274-2280, 2390`) — give the resting state its own token; zero `@media (forced-colors: active)` support (box-shadow focus rings and the borderless Select variant may vanish under Windows High Contrast; `:199-209, 731-742`); all sizing is px with fixed control heights — page zoom is fine but text-only scaling can clip; prefer `min-height` (`:60-65, 296-299`).

---

## Minor

- DatePicker placeholder says "Select date" with no format hint, and DateRangePicker inconsistently uses the format as its placeholder (`DatePicker.razor.cs:392-402` vs `DateRangePicker.razor.cs:467`). Downgraded: parsing falls through to free-form `DateTime.TryParse` (`PickerBase.cs:189-190`), so no specific format is required. Worth harmonizing + an `aria-describedby` hint.
- DateRangePicker interior in-range days carry no ARIA state (color band only; `DateRangePicker.razor.cs:498-531`). Downgraded: both endpoints are always rendered as text in the two labeled inputs and marked `aria-pressed="true"`.
- Calendar cells use `aria-pressed` where APG grids use `aria-selected` — defensible for the button-list pattern; document, or revisit with M9's grid roles.
- Resting input border `#d9d9d9` = 1.41:1 (hover mix = 2.63:1) — verified as the only boundary cue (white fill on white page), but under 1.4.11's "only visual indicator" test this is best-practice, not a clean AA failure (labels, inner text, and the full-strength focus state identify the field).
- Alert info/error icons on their own tint backgrounds land at 2.95:1/2.99:1 — fractionally under 3:1; trivial hex nudge.
- Table row checkboxes are 16×16 (likely saved by spacing exception at normal row density; re-check under `Small` density).
- `user-select: none` on the Select's single-selection value prevents copying the label (`wss-controls.css:325`).
- Modal/Drawer focus trap and initial focus are JS-only (documented trade-off, `OverlayActivationBase.cs:10`); a `_panelRef.FocusAsync()` C# fallback would preserve initial focus without JS.
- Content-only Popover/Popconfirm triggers are keyboard-reachable only after JS promotes them (`wss-overlay.js:252-260`); the recommended `<button>` child avoids it — consider a static `tabindex="0"` floor.
- Row-expand button lacks `aria-controls` to the detail row; select-all indeterminate state is JS-only (both documented/accepted).
- Tabs omit Home/End (documented Blazor `preventDefault` limitation; APG-optional).
- `Alert`'s close `aria-label="Close"` is not localizable — add a `CloseButtonLabel` parameter matching SearchInput's convention.
- Toast regions override `role="alert"`'s implicit atomicity with `aria-atomic="false"` — deliberate and reasonable; keep as a manual-AT-testing watch item.

---

## What the kit already does well

- **Real semantics throughout:** genuine `<table>/<th scope>`, real `<button>`s everywhere (tabs, sorters, expanders, closes), APG-correct tablist/tab/tabpanel with roving tabindex and disabled-tab-aware arrow cycling, `aria-sort`, `aria-current` (page + date), Modal/Drawer with `role="dialog"`, `aria-modal`, `aria-labelledby` + name fallbacks.
- **A deliberate focus-visibility system:** one consolidated `:focus-visible` block covering nearly every interactive element (`wss-controls.css:3775-3822`) with a tighter offset variant for dense picker cells, a `:focus-within` fallback for closed selects/pickers, and every hover-revealed control paired with `:focus-within` plus a `hover: none` touch fallback.
- **Toast live regions done right at the structural level:** severity-split `role="status"`/`role="alert"` regions that exist from first render (not injected with content), no focus stealing, real labeled close buttons on notifications.
- **Icons can't be misused:** every decorative SVG bakes `aria-hidden="true"` into its literal markup in `UiKitIcons`/`EditIcons`.
- **Motion and print:** a near-exhaustive `prefers-reduced-motion` block (cross-checked against every animation/transition in the file, including a flat-fill skeleton fallback) and a print stylesheet that strips interactive chrome.
- **Overlay JS is genuinely good:** initial focus, Tab wrap, topmost-of-stacked-overlays trapping, Escape recovery when focus escapes, focus restore on close, ref-counted scroll lock.
- **Pickers:** full arrow/Home/End/PageUp/PageDown navigation implemented in C# (works without JS), long-date accessible names on every day cell, `aria-current="date"`, localizable labels on all icon-only controls.
- **Skeleton:** the correct `role="status"` + `aria-busy` + `.wss-sr-only` "Loading" pattern.
- **`LabelTooltip`** is a model 1.4.13 implementation (role, describedby, hover bridge, Escape) — S1/S2 are about the *other* tooltip not reusing it.
- Body text contrast is excellent (≈16.5:1); RTL is handled via logical properties with documented exceptions.

---

## Suggested fix order

1. **Small, high-value, non-breaking:** S3 name fallbacks (+ Popconfirm message outside `HasTitle`, test + docs update); M3 SearchInput one-expression fix; M1 message close button; M2 sr-only severity words; M6 notification `pointer-events`; TBL aria-haspopup.
2. **Keyboard integrity:** S5 picker `aria-disabled` swap (+ e2e repro of the month-crossing blur first); S4 row keyboard support.
3. **Tooltip decision:** S1/S2 — either port the `LabelTooltip` pattern to `data-tooltip` or deprecate `data-tooltip` for icon-only/meaningful content; fix README + skill examples in the same commit.
4. **Timing:** S7 toast hover/focus pause.
5. **Token decisions (possibly breaking / release-noted):** S6 primary/error text contrast, M4 warning/success icon defaults, M5 24px hit boxes.
6. **Batch the remaining moderates** (M7–M11) with bUnit/e2e coverage per the repo convention; JS-dependent behaviors (inert toggling, tooltip Escape) need e2e, not bUnit.

Per CLAUDE.md, any fix that changes a control's public API, parameters, or documented behavior must update the `edit-controls` skill in the same commit — S1 (README example), S3 (`AriaLabel` docs), S4 (OnRowClick guidance), and M8's labeler parameter all qualify.

---

## Post-remediation verification and second wave (2026-08-11, same day)

**Method:** six parallel re-audits by subsystem, each instructed to re-verify the fixes above against
the current working tree rather than trust the "fixed" label, followed by an adversarial verification
pass over every finding either group raised — re-reading the cited code, re-tracing keyboard event
flows, and recomputing contrast ratios from relative luminance, the same discipline the first wave
used on itself.

**All seven Serious fixes (S1-S7) verified genuinely landed** — none regressed and none was a
documentation-only patch over unchanged behavior.

**Confirmed issues found this pass, now fixed:**

- **Table: an Enter-key double-fire regression.** The click-propagation guard on a plain `Column`'s
  cell was never meant to cover keydown, but Enter on a nested control fires a keydown *and* a
  synthesized click, and only the synthesized click was guarded per-column — so Enter on a
  button/link inside a plain `Column` raised `OnRowClick`/`ExpandRowByClick` twice per press. Fixed
  by stopping keydown propagation at **every** `<td>` unconditionally, regardless of column kind; the
  click guard is deliberately left per-column (`ActionColumn`/selection/expand only) since a click
  never double-dispatches the way a keyboard Enter does. Consumers now never need
  `@onkeydown:stopPropagation` in plain-`Column` content — the keydown guard is *stricter* than the
  click one, not merely equivalent to it (README/skill both previously claimed the two paths shared
  "the same propagation guards," which was true before this fix and wrong after it — corrected).
- **Table: `OnRowClickedAsync` now no-ops while `Loading`.** Previously only the row's `tabindex`/
  Enter-handler wiring dropped while masked (the pointer-inertness path); a synthesized click or a
  programmatic dispatch (tests, a consumer raising the event directly) could still reach the handler
  and fire a row activation mid-refresh. The handler itself now re-checks `Loading` and no-ops,
  closing that gap independent of how the call arrives.
- **Kit-wide RTL arrow direction.** `DatePicker`/`DateRangePicker`'s calendar grids and `Tabs`'s strip
  now swap physical `ArrowLeft`/`ArrowRight` to follow the *visual* direction under a right-to-left
  UI culture (`CultureInfo.CurrentUICulture`), the APG rule for horizontal arrows in a mirrored
  layout. One shared translation (`PickerMath`'s `LogicalKey`, and `Tabs.OnKeyDownAsync`'s own
  `RtlSupport.IsRightToLeft` check) covers every grid in both pickers plus the tab strip, so the rule
  is stated once. Vertical arrows, Home/End, and PageUp/PageDown are untouched — logical moves with
  no visual handedness. Culture-driven; no parameter.
- **Deemphasized-token hover contrast.** `--wss-color-text-deemphasized` (the outside-month/decade
  day/year token) was `#737373` — 4.74:1 on white, but only **4.35:1** on `--wss-color-bg-hover`
  (the cell's own hover fill), under AA there. The source comment that shipped with the original
  fix had mis-computed that hover ratio as 4.58:1 (an arithmetic error, not a second measurement) —
  a false "still passing" reading that would have let the shortfall stand. Recomputed and darkened
  to `#696969`: 5.49:1 on white, 5.04:1 on the hover fill, comfortably clear of 4.5:1 in both places.
- **`Pagination.ShowTotal` announces itself; `AnnounceTotal` opts out.** The total-text span now
  carries `role="status"` (WCAG 4.1.3), announcing "1-10 of 200 items" on page/size/filter changes.
  New `AnnounceTotal` (`bool`, default true) drops just the role while still rendering the text;
  `Table` sets it false on the *top* pager under `PagerPosition.Both` so the visually-duplicated
  total isn't announced twice.
- **Alert/toast localization parameters.** `Alert.SeverityLabel` (`string?`) and, on all four toast
  containers (`MessageContainer`/`NotificationContainer`/`WasmMessageContainer`/
  `WasmNotificationContainer`) plus their shared list views, `CloseButtonLabel` (`string`, default
  "Close") and `SeverityLabel` (`Func<MessageType/NotificationType, string>?`) — null keeps the
  built-in English words; a caller returns just the word, the component still appends the trailing
  `": "` separator. Closes the i18n limitation the first wave's severity-announcement fix (M2)
  knowingly left open ("hardcoded English... an i18n limitation to be aware of" — that caveat no
  longer applies and has been removed from the docs).
- **Toast close button `aria-describedby`.** Each toast's close button now points at that toast's own
  content/message element, so a screen reader tabbing through a stack of toasts hears which one a
  bare "Close" belongs to instead of an indistinguishable "Close" repeated for every item.
- **`SearchInput` accessible-name floor.** The name-resolution chain (`InputLabel` → `AddonLabel` →
  `Placeholder`) previously had no floor: a bare `<SearchInput />` with none of those set, and no
  `AddonContent` either, rendered nameless. It now falls back to `SearchButtonLabel` ("Search") as a
  last resort, mirroring the guaranteed-name pattern the buttons already had.
- **`--wss-color-error-strong-hover` generic bridge.** Added alongside the existing
  `--wss-color-primary-strong-hover` bridge so a consumer theming `--color-danger-strong-hover` gets
  the same override path for the danger button's hover state that primary already had.
- **height→min-height sweep.** M11's "prefer `min-height`" sub-item (flagged but not actually applied
  in the first wave — see the corrected Remediation status above) is now applied across
  `wss-controls.css`'s fixed-height controls, so text-only scaling can grow a control instead of
  clipping it — content sizing, not just page zoom.

**Deliberate no-changes, with rationale (re-affirmed, not overlooked):**

- **Untitled `Popconfirm` still renders no message text.** `Title` *is* the confirmation message
  here (there is no separate `Message`/`Description`), so an untitled `Popconfirm` genuinely has
  nothing to render beyond the warning icon and OK/Cancel — S3's `AriaLabel` fallback ("Confirm")
  covers the dialog's *name*; there is no message to independently surface. The documented
  fallback/recommendation ("prefer setting `Title`") stands as the fix.
- **`Table` still has no default `aria-label`.** A generic "Table" name would be noise on top of the
  `role="table"` a screen reader already announces natively — `AriaLabel`/`Caption` remain the
  consumer's call, same as before.
- **`Tabs`' tabpanel keeps its unconditional `tabindex="0"`.** The trade-off (one extra Tab stop when
  a pane's first content is itself focusable) is kept deliberately for simplicity and because it's
  the only way to make a text-only pane reachable at all — APG-sanctioned.
- **Weekday header cells stay outside the day grid** (decorative, `aria-hidden`) rather than becoming
  `columnheader`s inside it — compensated by every day button already carrying a full "D"-format
  accessible name (weekday included), so the information isn't actually missing for AT, just not
  structurally linked.
- **Picker panel live-region first-announce-on-open is left as is.** Whether a region present from
  first render reliably announces its initial content the instant an overlay opens is
  screen-reader-dependent and was not something this pass could verify further; treated as
  speculative rather than reworked without a concrete repro.
- **`ArrowDown`-into-grid (tracked in the first pass as PKR-4) was implemented rather than deferred.**
  `ArrowDown` from either picker's text field, while the panel is open, now moves focus onto the
  calendar's roving-tabindex cell (JS-dependent; without JS the key is inert and Tab still reaches
  the grid). The combobox-like "focus stays on the field on open" model is unchanged — this only
  affects what a subsequent `ArrowDown` does.

**Known residuals (not addressed this wave):**

- `Select`'s search input still has a fixed pixel `line-height` (`wss-controls.css:358`, paired with
  `overflow: hidden`/`text-overflow: ellipsis` on the same rule family) that doesn't grow under
  text-only scaling — pre-existing, and coupled tightly enough to the overflow/ellipsis behavior that
  it wasn't pulled into this pass's height→`min-height` sweep.
- Toast auto-dismiss pause (`Pause`/`Resume`, hover/focus-triggered) has no touch equivalent — there
  is no hover on touch, and the close button is the only focusable element in a toast — so `Duration`
  remains a touch user's only control over toast timing (WCAG 2.2.1 is satisfied for pointer/keyboard
  users, not touch).
- Playwright accessibility-tree assertions in this repo's e2e suite check the **in-page ARIA tree**
  (roles, names, states as computed from markup), not the platform accessibility-API mapping a real
  screen reader consumes — a real AT smoke test (NVDA/JAWS/VoiceOver) is still the only way to close
  that gap, and remains outside this pass's scope.
