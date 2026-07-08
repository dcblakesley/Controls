# Evaluation — Round 2: Hardening-Pass Review

**Date:** 2026-07-07 · **Scope reviewed:** `f84a947..e9bb383` (everything since the 10.3.0 cut)
**Baseline at review time:** green — 319 bUnit tests × net8/net9/net10, 75/75 Playwright E2E.

Method: adversarial diff review of the hardening commits themselves (regressions, half-fixes,
inconsistencies), live hand-testing in Chromium (keyboard-only passes, overlay stacking, Select
engine, EditFile), plus dimensions the last pass skipped: Blazor Server (circuit) behavior,
render/allocation performance under large data, RTL/zoom/reduced-motion, and API ergonomics.

This is a multi-session working document. Check items off as they land; each entry is
self-contained (file:line, failure scenario, fix direction) so any session can pick one up cold.
Items marked **⚖ decision** need a call from Dave before implementation.

Legend: ☐ open · ☑ done · ✗ won't fix

---

## High

### ☑ H1 — `EditFile` silently loses every file except the last selection batch
- **Where:** `Controls/EditFile.razor.cs` (accumulation in `LoadFiles`), `Controls/EditFile.razor:10` (`InputFile` unmounts at `MaxFiles`)
- **Verified live:** added `batch1.pdf`, then `batch2.pdf` in a second pick. UI listed both;
  the browser's `_blazorFilesById` map and native `input.files` held **only `batch2.pdf`**.
- **Mechanism:** Blazor's `InputFile` JS resets `elem._blazorFilesById = {}` on **every** `change`
  event, and the native file input replaces its `FileList` wholesale per selection. Any
  `IBrowserFile` from an earlier batch → `OpenReadStream()` throws. Unmounting the `InputFile`
  (MaxFiles reached) kills even the last batch. Applies to Server and WASM alike.
- **Origin:** shipped with EditFile in 10.3.0 (not introduced by this pass; the pass's tests
  didn't catch it — bUnit can't see the JS file map).
- **Fix direction (recommended):** buffer file bytes at selection time (the UX is built around
  accumulation, so append semantics should become real). Alternatives: replace-not-append per
  change; or document single-batch semantics. Whatever lands, also fix the MaxFiles-unmount case
  and document `OpenReadStream(maxAllowedSize:)` (see L8).
- **Done (uncommitted):** ⚖ Dave chose *buffer at selection*. New `internal sealed BufferedBrowserFile : IBrowserFile`
  serves an in-memory buffer; `LoadFiles` reads each accepted file's bytes (`OpenReadStream(MaxFileSizeBytes)`
  + `ReadExactlyAsync`) at pick time and stores a wrapper. Public API stays `List<IBrowserFile>`.
  MaxFiles-unmount is fixed by construction (bytes no longer depend on the input element). A per-file
  read failure is caught and reported, not fatal. **Folds in L8** — `OpenReadStream()` ignores its size
  arg (buffer already bounded by `MaxFileSizeBytes`), so a bare call works; memory trade-off documented
  on the class + `MaxFileSizeBytes`. Tests: `Selected_files_are_buffered_into_memory_and_stay_readable`,
  `Files_from_multiple_selection_batches_all_survive_and_stay_readable`,
  `Buffered_file_reads_without_a_size_argument_even_above_the_500KB_framework_default`.

### ☑ H2 — Scalar controls crash outside an `EditForm` (regression from the `IsInvalid` fix, 2ed6416)
- **Where:** `Controls/EditControlBase.cs:67` — `IsInvalid => EditContext.GetValidationMessages(FieldIdentifier).Any()`
  with no null guard; same pattern in `Controls/EditRadio.razor.cs:75`.
- **Failure:** `InputBase` supports standalone use (null `EditContext`) since .NET 8. Any
  `<EditString Field=... @bind-Value=... />` outside an `EditForm` NREs on first render at
  `aria-invalid=@(IsInvalid ...)`.
- **Inconsistency:** the *same commit* added the null guard to the list base
  (`EditControlListBase.cs:69`) and declared out-of-form use supported in
  `FieldValidationDisplay.razor:11`.
- **Fix:** same guard as the list base — `EditContext is not null && ...` — in both spots.
- **Done (uncommitted):** guard added to both accessors; regression tests
  `ValidationStateTests.Scalar_control_renders_standalone_without_an_EditForm` +
  `EditRadio_renders_standalone_without_an_EditForm` render each control with no `EditForm`.

---

## Medium

### ☑ M1 — Modal/Drawer leak scroll lock + document listeners if disposed while opening
- **Where:** `Controls/UiKit/Modal.razor` / `Drawer.razor` `OnAfterRenderAsync` activation block.
- **Failure:** the new `_activationSeq` token fixes close→reopen, but `DisposeAsync` neither bumps
  the sequence nor sets a disposed flag. A component removed from the tree while `activateModal`
  is in flight stores a handle nothing releases: `body{overflow:hidden}` + the trap's document
  keydown/mousedown/focusin listeners persist for the circuit's life (no page reload on Server).
- **Fix:** replicate `Table.razor`'s `_disposed` guard (it solves this exact race): check after the
  await, dispose the late handle; or bump `_activationSeq` in `DisposeAsync`.
- **Done (uncommitted):** both — added `bool _disposed` (checked in the post-`activateModal` condition
  so a late handle is disposed, not stored) and `DisposeAsync` now sets `_disposed = true` and bumps
  `_activationSeq`. Applied to both Modal and Drawer. Smoke test
  `Modal_disposes_cleanly_after_being_shown` (the leak itself isn't bUnit-observable — no real JS
  module — but the fix mirrors Table's already-proven pattern and disposal must not throw).

### ☑ M2 — Invariant select parsing is a half-fix: format side still CurrentCulture
- **Where:** `Controls/EditSelect.razor.cs` / `EditSelectString.razor.cs` — no `FormatValueAsString`
  override, so `CurrentValueAsString` uses `InputBase`'s default culture-sensitive `ToString()`;
  parse side (`Controls/Helpers/SelectParsing.cs:33`) is invariant.
- **Failure:** de-DE + `EditSelect<double>`, model `1.5` → renders `"1,5"`, matches no
  `<option value="1.5">`, select shows unselected; re-picking parses fine then reformats to
  `"1,5"` and the visual selection is lost again. Also non-ASCII negative signs (sv-SE).
- **Fix:** override `FormatValueAsString` invariantly in both controls (mirror EditNumber, which
  got both sides).
- **Done (uncommitted):** new `SelectParsing.FormatInvariant<T>` (strings pass through; `IFormattable`
  formats under `InvariantCulture`; enums format by name) mirrors the parse; both selects override
  `FormatValueAsString` to call it. Tests: `FormatInvariant_*` unit tests (incl. round-trip under de-DE)
  + `EditSelect_double_selects_the_matching_option_under_a_foreign_culture`.

### ☑ M3 — `EditBool` + `IsLabelHidden` renders an unnamed checkbox
- **Where:** `Controls/EditBool.razor:24-27` — hidden-label edit branch renders bare
  `@CheckboxFragment`; no sr-only label, no `aria-label`.
- **Context:** this is the exact defect commit 2ed6416 claims to have eliminated; every other
  control got the visually-hidden label via `FormLabel`. EditBool's edit branch bypasses FormLabel.
- **Fix:** render an `edit-sr-only` label (or `aria-label=@DisplayLabel()`) in the else branch.
- **Done (uncommitted):** else branch now renders `<label class="edit-sr-only" for=@_id>@DisplayLabel()</label>`
  before the checkbox (matches FormLabel's IsLabelHidden path). Test:
  `EditBool_hidden_label_still_names_the_checkbox_via_a_visually_hidden_label`.

### ☑ M4 — `EditRadioString`'s "Other" text input ignores `IsDisabled`
- **Where:** `Controls/EditRadioString.razor:54` — `disabled="@(_selectedOption != OtherName)"`.
- **Failure:** `IsDisabled="true"` + `HasOther` + model holding a custom value → radios disabled,
  but the Other text box stays editable and writes to the model per keystroke.
- **Inconsistency:** sibling `EditRadioEnum.razor:52` has the `|| IsDisabled` term; this line was
  edited by the pass (sentinel rename) without adding it.
- **Fix:** add `|| IsDisabled`.
- **Done (uncommitted):** `disabled="@(_selectedOption != OtherName || IsDisabled)"`. Test:
  `EditRadioString_Other_text_input_respects_IsDisabled` (Other selected + IsDisabled → box disabled).

### ☑ M5 — Mask→panel drag still closes the Modal (+ stale flag)
- **Where:** `Controls/UiKit/Modal.razor` — `OnMaskMouseDown` / `OnMaskClickAsync`.
- **Verified live:** press-in-panel→release-on-mask correctly stays open; press-on-mask→
  release-in-panel **closes** (click dispatches to the wrap, `_maskMouseDown` is set). AntD
  requires down *and* up on the mask ("changed my mind" gesture keeps the dialog).
- **Also:** `_maskMouseDown` never resets when the click never arrives (release outside the
  window) — a later panel→mask drag consumes the stale flag and closes.
- **Fix:** track mouse*up* target too (close only when both down+up hit the mask), or clear the
  flag on panel mousedown-capture / on a window blur; note the Drawer doesn't need this (its mask
  is a sibling, not an ancestor — click never composes to a handler).
- **Done (uncommitted):** two-flag model — `_maskMouseDown`/`_maskMouseUp` set only by the wrap's
  mousedown/mouseup (the panel now stops mouseup propagation too), close only when both are set; a
  new panel `OnPanelMouseDown` clears both (starts a non-closing gesture AND clears a stale mask-down
  from a release outside the window). Drawer left untouched. Tests: mask-click closes;
  press-mask/release-panel stays open; panel-press/mask-release stays open; stale-press-then-new-gesture
  stays open.

### ☑ M6 — `FormOptions` static defaults bleed across circuits on Blazor Server **⚖ decision**
- **Where:** `Controls/FormOptions.cs:45,53` — `DefaultIsRequiredStarHidden`,
  `DefaultShowFieldNameInValidation` are `static bool { get; set; }` (process-wide).
- **Failure:** per-user/per-tenant assignment from one circuit changes every other user's forms.
  Writes are atomic — semantic sharing problem, not a race.
- **Options:** (a) document "set at startup only, process-wide"; (b) move to a DI options object
  (breaking-ish). Decide before touching.
- **Decision: neither — (c) cascading `FormDefaults` component.** DI (b) was rejected because most
  consumers run as MFEs that don't own the composition root; a render-tree cascade maps onto
  app/MFE/circuit boundaries with no service registration. New root-level `FormDefaults` component
  (nullable `IsRequiredStarHidden`/`ShowFieldNameInValidation`, cascades itself `IsFixed`);
  resolution is FormOptions instance → FormDefaults → static. Statics kept as final fallback
  (non-breaking) and documented as "process-wide, set at startup only". Tests: `FormDefaultsTests`
  covers the full chain including both fall-through directions.

### ☑ M7 — Popover/Popconfirm trigger invites button-in-button **⚖ decision**
- **Where:** `Controls/UiKit/Popover.razor` / `Popconfirm.razor` — `role="button" tabindex="0"`
  trigger span around arbitrary `ChildContent`; demo passes a `<button>`.
- **Verified live:** two tab stops for one control (trigger span, then the inner button); nested
  interactive content inside `role="button"` is invalid ARIA. The `initTrigger` JS deliberately
  tolerates nested interactive children, so the design half-embraces it.
- **Options:** (a) document "pass non-interactive trigger content" + fix the demo/gallery to use
  plain content; (b) rework the trigger contract (attach handlers to the child, AntD-style —
  bigger change). Decide direction first.
- **Decision: (b), Blazor-style — the child element is the trigger.** (a) was rejected because
  consumers (and our own demo) naturally pass a `<button>`. The wrapper span dropped
  `role`/`tabindex`/popup ARIA; the child's native click bubbles to the wrapper's `@onclick`
  (so the C# keydown handler shrank to Escape-only — no double-toggle path exists to filter).
  `initTrigger` became `syncTrigger(el, open, disabled)`, called every render: mirrors
  `aria-haspopup`/`aria-expanded` onto the first interactive child (Blazor can't attribute
  projected content), or promotes the wrapper to `role="button"` + keyboard-click when the content
  has nothing focusable; `focusTrigger` restores close-focus to the real trigger. Degradation:
  without JS a button child fully works; plain content is mouse-only (was: span double-toggled
  nested buttons). bUnit asserts the wrapper is semantics-free; the JS half is e2e-verified
  (`Popover_child_button_owns_the_popup_aria_and_keyboard_path`).

### ☑ M8 — `LabelTooltip` Escape doesn't stop propagation
- **Where:** `Controls/LabelTooltip.razor` — new `OnKeyDown` handles Escape but propagation
  continues; Escape on a tooltip inside a Modal closes **both**.
- **Context:** violates the pass's own "one Escape, one layer" rule (Select/Popover/Modal all got
  the conditional-stopPropagation treatment).
- **Fix:** `@onkeydown:stopPropagation="@isTooltipVisible"` (mirror the Select's `_open` pattern).
- **Done (uncommitted):** added `@onkeydown:stopPropagation="@isTooltipVisible"` to the tooltip button
  (exact Select `_open` mirror). No unit test — bUnit dispatches synthetic events without DOM
  propagation, so stopPropagation isn't observable there; it's the same directive the Modal-vs-Select
  E2E layering already proves works.

---

## Low

### ☑ L1 — `FieldValidationDisplay` re-reflects per parameter cycle
- **Where:** `Controls/FieldValidationDisplay.razor.cs:25-33` —
  `FieldIdentifier.Model.GetType().GetProperty(FieldName)` + attribute scans run on every
  `OnParametersSet`; its parameters (List, FieldIdentifier) defeat Blazor's change skip, so a
  50-field form pays 50 reflections per keystroke. (Flagged independently by both review passes.)
- **Fix:** memoize per `(Type, FieldName)` in a small static `ConcurrentDictionary`, or guard on
  `FieldIdentifier` equality + `Label` change.
- **Done (uncommitted):** the `GetProperty` value-type lookup is memoized in a static
  `ConcurrentDictionary<(Type, string), string>` (the cheap attribute scans left as-is). Behavior
  unchanged (covered by existing validation-message tests).

### ☑ L2 — List-base `EditContext` swap accumulates stale field registrations
- **Where:** `Controls/EditControlListBase.cs:167-171` — new `FieldIdentifier` registered per
  swap; the old one never leaves `FormOptions.FieldIdentifiers` / `FieldIds`.
  `ValidationView.razor:7` iterates all of them every render. Bounded by swap count, not a hard leak.
- **Fix:** unregister the previous identifier on swap (and in Dispose).
- **Done (uncommitted):** new `FormOptions.UnregisterField`; the swap block unregisters the old
  identifier before re-registering, and `Dispose` unregisters too. Test:
  `Swapping_the_model_does_not_accumulate_stale_field_registrations` (5 swaps → 1 identifier).

### ☑ L3 — EditFile keyboard remove strands focus on `<body>`
- **Where:** `Controls/EditFile.razor.cs:96-105` (`RemoveFile` does no focus management).
- **Verified live:** delete button is reachable and visible on focus (the pass's opacity fix
  works); after Enter, focus resets to body — keyboard user returns to top of document.
- **Fix:** after remove, focus the next file's delete button, else previous, else the drop zone.
- **Done (uncommitted):** delete buttons now carry `id="del-{_id}-{i}"`; `RemoveFile` sets a pending
  focus target (shifted-in file → new last → the drop-zone input) and `OnAfterRenderAsync` focuses it
  via a new `WssEditControls.focusById` JS helper (`JsInteropEc.FocusById`), best-effort/catch-guarded.
  Real focus destination isn't bUnit-observable (no DOM focus) — an E2E assertion is the right home.

### ☑ L4 — Disabled EditFile drop zone still shows drag-hover highlight
- **Where:** `Controls/EditFile.razor.cs:30` — `OnDragEnter`/`OnDragOver` set `_hoverClass`
  unconditionally; drop is correctly refused, but the zone lights up as if it accepts.
- **Fix:** guard the hover class on `!IsDisabled` (and `ShowEditor`).
- **Done (uncommitted):** `OnDragEnter` now guards on `!IsDisabled` (the zone only renders in
  `ShowEditor`, so that's the operative guard). Test:
  `Disabled_drop_zone_does_not_show_the_drag_hover_highlight`.

### ☑ L5 — `EditSelectString` empty option: no opt-out, `""` never null, non-string parse error
- **Where:** `Controls/EditSelectString.razor:23-29` — unconditional leading empty option.
- **Issues:** every existing consumer's dropdown gains a selectable blank row; selecting it writes
  `""` (a `string?` model can never return to null via the UI); with non-string `TValue`
  (e.g. `EditSelectString<int>`) the blank yields "The X field is not valid." instead of a blank
  state. Sibling `EditSelectEnum.razor:23` gates its empty option on nullability.
- **Options:** add a `ShowNullOption` (or make `NullOptionText=null` suppress it); write null for
  nullable models. Decide the API shape.
- **Done:** ⚖ Dave chose *clear to null*. Three changes: (1) `SelectParsing.TryParseStringOrConvert`
  now short-circuits empty input to `default(TValue)` with success (null for reference types +
  `Nullable<T>`, zero otherwise) — fixes the "`""` never null" and non-string parse-error issues,
  and applies to `EditSelect` too (an `<option value="">` clears cleanly). (2) `NullOptionText` is
  now `string?`; `null` suppresses the blank option (opt-out for required fields). (3) The blank is
  auto-suppressed when `TValue` is a non-nullable value type (a static `CanBeNull` per closed generic;
  NRT erasure means `string`/`string?` are indistinguishable at runtime, so reference types always
  qualify). Tests: `SelectParsingTests` empty→default for string?/int?/int; `EditSelectParseTests`
  blank-clears-string-to-null, nullable-int blank shows + clears, non-nullable-value-type has no blank,
  `NullOptionText=null` suppresses. Full bUnit suite green (346 × net8/9/10). No visual baseline moves
  (demo binds `string`/`string?` with default `NullOptionText`, so the blank still renders there).

### ☑ L6 — Other-sentinel collision narrowed, not eliminated
- **Where:** `Controls/EditRadioString.razor.cs:28,94` — sentinel is `"__wss-other__"`; an Options
  entry literally equal to it still routes through the Other branch and overwrites the model with
  the (empty) other-text. Vastly less likely than the old `"Other"`, but the design still keys the
  Other radio through the value channel.
- **Fix (if bothered):** key the Other radio out-of-band (index/flag), not by value.
- **Resolved: dynamic sentinel, not the out-of-band rework.** Index/flag keying would break the
  string-typed `ValueExpression` forwarding to `InputRadioGroup` (load-bearing since the radio
  hardening pass) or force hand-rolled radios. Instead the sentinel is uniquified against
  `Options` in `OnParametersSet` (`while (Options.Contains(_otherName)) _otherName += "!"`) —
  collision impossible by construction, selection channel untouched, no API change. An Options
  swap that invalidates the sentinel self-heals via the existing implied-value re-derive. Test:
  `EditRadioString_an_option_equal_to_the_internal_sentinel_binds_its_own_value`.

### ☑ L7 — `EnumHelpers` id cache degrades permanently at saturation
- **Where:** `Controls/Helpers/EnumHelpers.cs:52-70` — no eviction; once the 10k cap is hit, every
  new string pays `ConcurrentDictionary.Count` (acquires **all** internal locks) + the regex, on
  every call, forever. Check-then-add can also overshoot the cap slightly (benign).
- **Fix:** cache saturation in a `volatile bool` (stop counting once full), or approximate counter.
- **Done (uncommitted):** added `static volatile bool _idCacheFull` — `Count` is consulted only on a
  miss while filling and latches the flag at the cap; post-saturation calls skip straight to
  compute-without-cache (never touch `Count` again). Test:
  `ToId_stays_correct_after_the_id_cache_saturates`.

### ☑ L8 — Doc gap: `MaxFileSizeBytes` vs `OpenReadStream`'s 500 KB default
- **Where:** README / EditFile xmldoc — the control validates `file.Size` (10 MB default) but a
  consumer calling bare `OpenReadStream()` throws past 512,000 bytes.
- **Fix:** README + xmldoc: `file.OpenReadStream(maxAllowedSize: <your cap>)`; on Server, read
  before the circuit disconnects. (SignalR chunking is a non-issue.) Fold into H1's fix if that
  changes the contract anyway.
- **Done (uncommitted):** resolved *by* H1 rather than just documented — buffered files serve the whole
  buffer regardless of the size arg, so bare `OpenReadStream()` no longer throws. Class/`MaxFileSizeBytes`
  xmldoc now explains the buffering + memory trade-off; README EditFile section updated (see H1 commit).

### ☑ L9 — `[Display(Name)]` support ignores `ResourceType`
- **Where:** `Controls/Helpers/AttributesHelper.cs` — reads `.Name` instead of `GetName()`, so
  localized display names via `ResourceType` don't resolve.
- **Fix:** call `GetName()`.
- **Done (uncommitted):** both `AttributesHelper.GetLabelText` and `EnumHelpers.GetName` now call
  `DisplayAttribute.GetName()`. Test:
  `GetLabelText_resolves_a_localized_Display_through_its_ResourceType`.

---

## Verified clean (don't re-litigate)

Live-tested in Chromium against this build:
- **Focus trap:** wraps correctly (close → Cancel → OK → close); capture-phase document listeners,
  so the Select's bubble-phase `stopPropagation` can't break it; focus restores to the trigger;
  scroll lock releases (ref-counted, idempotent dispose).
- **Escape layering:** one layer per press everywhere tested (Select-in-form, Popover, Modal).
- **Modal mask:** plain click closes; panel→mask text-selection drag stays open (M5 is the
  reverse direction only).
- **Select engine keyboard suite:** Enter opens closed combobox (no implicit form submit — URL
  unchanged); typing filters with debounce; arrow keys move the highlight without caret jumps
  (caret stayed at end, 2/2); keyboard flush of pending debounce works; Enter selects and closes;
  reopen highlights the current selection; Tab-away closes (the clear button is a legitimate,
  fully-visible intermediate tab stop when a value is present — not a bug).
- **Tags mode:** keyboard add/remove, Backspace removes last tag, Escape closes dropdown without
  clearing tags, remove buttons tabbable.
- **EditNumber:** no validation flash mid-typing (`-`, partial numbers); commits on blur/Enter.
- **EditFile:** delete button visible on focus, Enter removes (L3 is only the focus destination).
- **Razor-comment-in-tag fix (e9bb383) holds** — swept all `.razor` files: every comment sits
  between elements. (A scary `InvalidCharacterError` in the console log was stale output from a
  pre-fix build on port 5199 — zero console errors from this build.)

Code/architecture review came back clean on:
- JS interop: all library call sites are `OnAfterRenderAsync`/event-handler only, catch-guarded,
  degrade gracefully; `DisposeAsync` swallows `JSDisconnectedException`; Select's debounce CTS
  handles cancellation/disposal races; continuations resume on the circuit dispatcher.
- No cross-circuit static mutable state besides M6 (`FormOptions` defaults). Toast services are
  per-circuit (`AddScoped`), mutations lock-guarded, renders marshaled via `InvokeAsync`.
- Table under large data: page-scoped key de-dup (O(page), not O(n)); sort computed on action and
  cached; O(1) keyed selection; `PropertyColumn` compiles once per closed generic; column
  straggler-merge logic holds up (dispose ordering, add+remove same pass, sort-column removal).
- Select under large data: reference-guarded rebuilds verified (one O(n) filter per keystroke,
  no full option-model rebuild); `Virtualize` in the dropdown; `MoveActive` O(1) typical.
- Toast duration cap: `Duration` is `double` — no int overflow; `0 = sticky` guarded.
- Drawer mask needs no drag fix (mask is a sibling of the panel, not an ancestor — the composed
  click never reaches a close handler).
- Reduced motion: `wss-controls.css:1461+` already kills shimmer/spinner/slide-ins;
  `edit-controls.css` has no motion at all.
- Reflow at 320px: overflow comes from the demo page's own layout (`flex-column` at 477px), not
  the library controls; `wss-table-wrapper` scrolls internally.
- RTL: Select dropdown geometry correct under `dir="rtl"`. The library has no `[dir]`-aware styles
  (physical left/right throughout) — icons stay physically right in RTL. Cosmetic; no RTL claim is
  made. If RTL becomes a requirement it's a project, not a fix.
- `[Display(Name)]` precedence order matches the documented resolution exactly (modulo L9).
- `EditMultiSelect` `Mode="Single"` fail-loud guard, write-back-before-notify ordering,
  `InvalidIcon` migration (no straggler call sites), `IsForLabelable` consistency, per-message
  validation elements, `aria-describedby`/`aria-errormessage` never dangle.

---

## Suggested order of work

1. **H1, H2** — data loss + crash class. (H1 is the big one; needs a design decision on buffering
   but the recommendation is written above.)
2. **M1–M5, M8** — small, targeted completions of things the pass started; each is an
   afternoon-sized change with an obvious test.
3. **M6, M7, L5** — need an API/design decision (⚖) before code.
4. **L1–L4, L6–L9** — cheap-wins batch; L1/L2 are quiet perf/leak fixes, L3/L4 polish, L8/L9 docs.

Per CLAUDE.md: work lands directly on `master`, commit+push per logical chunk, no NuGet push from
agents, version stays 10.3.0 until publish (changelog notes accumulate under Unreleased).

---

# Evaluation — Round 3: Post-Hardening Review

**Date:** 2026-07-07 · **Scope reviewed:** the round-2 fixes themselves (all of H1–L9 landed today)
plus fresh-eyes passes on the new subsystems (EditFile buffering, FormDefaults, SelectParsing,
the M7 trigger rework) and the RCL JS modules.
**Baseline at review time:** green — 352 bUnit tests × net8/net9/net10 (Release), 76/76 Playwright
E2E, visual baselines intact.

Method: three parallel adversarial lenses (regression-hunt on today's commits; new-subsystem deep
read; JS + C#↔JS contract), every finding hand-verified against current `master` before recording.
Numbering continues from round 2. No fixes applied yet — this section is the findings tracker.

## Medium

### ☑ M9 — `syncTrigger` latches the trigger child on first sight; a replaced or late-arriving child permanently breaks the M7 trigger contract
- **Where:** `Controls/wwwroot/wss-overlay.js:87-104` (`el.__wssTrigger` memoized once); consumed
  every render by `Controls/UiKit/Popover.razor:90` / `Popconfirm.razor:115`.
- **Failure (child replaced):** trigger content like
  `@if (busy) { <span>…</span> } else { <button>Delete</button> }` recreates the element across
  renders; `target` then points at a detached node — all `aria-haspopup`/`aria-expanded` writes go
  to the dead element (the live trigger never announces the popup) and `focusTrigger`
  (wss-overlay.js:125-130) focuses the detached node, a silent no-op → close-focus drops to
  `<body>` (the defect class L3 fixed for EditFile).
- **Failure (child appears later):** first render has nothing focusable → wrapper is promoted
  (`role="button"`, `tabIndex=0`, keydown listener) and `fallback:true` latches. When a real
  `<button>` appears, promotion is never undone — button-in-button invalid ARIA + two tab stops,
  the exact state M7 exists to eliminate, now permanent (the fallback branch re-applies it every
  render). Flagged independently by two lenses; the historical "stale-mirror field" pattern.
- **Fix direction:** re-run the `querySelector` on every call (it's one cheap query); keep only the
  keydown-listener wiring latched; demote the wrapper (remove role/tabindex, gate the listener on
  `fallback`) when a focusable child appears, re-promote when it disappears.

### ☑ M10 — Focusable-but-not-clickable trigger child gets popup ARIA with no keyboard activation path (deferred Space item survives M7 here)
- **Where:** `Controls/wwwroot/wss-overlay.js:90` (selector includes `input, select, textarea,
  a[href], [tabindex]`); keyboard-click synthesis exists only in the no-focusable fallback branch
  (:92-102); the C# trigger keydown is Escape-only (`Popover.razor:70-73`, deliberate — avoids
  double-toggle with buttons).
- **Failure:** `<Popover><span tabindex="0">ⓘ</span></Popover>` — the span announces
  `aria-haspopup="dialog" aria-expanded="false"` but Enter does nothing and Space scrolls the page;
  the popup is keyboard-unreachable (WCAG 2.1.1) while its ARIA advertises expandability. An
  `<input>` child additionally sends Enter to the enclosing form's implicit submission. Only a
  `<button>` child produces native Enter/Space clicks; the contract conflates "focusable" with
  "keyboard-activatable". The old deferred "Space scrolls on the trigger" item is NOT moot — it
  lives on in exactly this branch.
- **Fix direction:** extend the keydown synthesis (with preventDefault on Space) to focusable
  non-button targets, or narrow the selector to natively-activatable elements and let everything
  else take the promoted-wrapper path; document "prefer a `<button>` child" either way.

### ☑ M11 — `syncTrigger` interop fires on every render of every Popover/Popconfirm instance
- **Where:** `Controls/UiKit/Popover.razor:84-92` / `Popconfirm.razor:108-117` — unconditional
  awaited `InvokeVoidAsync("syncTrigger", …)` in `OnAfterRenderAsync` (was `firstRender`-guarded
  `initTrigger` before M7). `RenderFragment` parameters defeat Blazor's change-skip, so every
  ancestor re-render re-renders every instance.
- **Failure:** the canonical composition — a Table `ActionColumn` with a delete-`Popconfirm` per
  row — pays one SignalR round trip per row per render pass on Blazor Server (100 rows → 100
  sequential interop messages on every sort/page/selection/keystroke re-render, per circuit). And
  per M9 the call doesn't even repair staleness — it rewrites identical values to a cached node.
- **Fix direction:** cache the last-synced `(open, disabled)` pair and skip the call when unchanged
  (first render always syncs) — the exact pattern `Table.razor` already uses for `setIndeterminate`
  via `_lastIndeterminate`. Note M9's fix (re-query every call) pulls the other way; reconcile —
  e.g. sync on transitions + whenever `ChildContent` re-renders isn't detectable, so transitions
  + a MutationObserver-free "re-resolve on each actual call" is still fine.

### ☑ M12 — `EditFile` default configuration allows unbounded server-side memory buffering **⚖ decision**
- **Where:** `Controls/EditFile.razor.cs:35` (`MaxFiles = 0` = unlimited), `:72`
  (`GetMultipleFiles(e.FileCount)` deliberately lifts the framework's 10-file guard), `:103-108`
  (every accepted file eagerly buffered, up to `MaxFileSizeBytes` = 10 MB each — a consequence of
  the H1 buffering decision).
- **Failure:** Blazor Server, bare `<EditFile Field=… @bind-Value=… />` (the demo's own pattern); a
  user drag-drops a 300-photo folder → ~3 GB allocated in one gesture on the circuit's heap, live
  until the bound list is cleared; a few concurrent users OOM the host. Pre-H1 the bytes weren't
  pulled until the consumer streamed; H1 turned the unlimited-count default into a real exhaustion
  vector. The class remarks say "set MaxFileSizeBytes and MaxFiles to bound that footprint" but the
  shipped default leaves the aggregate unbounded.
- **Options:** (a) non-zero `MaxFiles` default (breaking-ish for consumers relying on unlimited);
  (b) an aggregate `MaxTotalBytes` cap (new param, sane default, enforced in the accept loop with
  the same error-reporting shape as the count cap); (c) document-only. Decide before code.

### ☑ M13 — `FormatInvariant` doesn't round-trip date-typed values against authored option values
- **Where:** `Controls/Helpers/SelectParsing.cs:70` — `f.ToString(null, InvariantCulture)` emits
  the invariant *display* format for date types (`DateTime` → `"06/15/2026 00:00:00"`, `DateOnly` →
  `"06/15/2026"`), while the parse side (`:44`, `BindConverter`) accepts ISO.
- **Failure:** `EditSelect<DateOnly>` with the natural `<option value="2026-06-15">` — picking the
  option parses and writes the model, then `FormatValueAsString` renders `"06/15/2026"`, matches no
  option, and the select snaps back to visually-unselected while the model holds the value — the
  same "picks fine, selection lost" symptom M2 fixed for `double`. Numerics/enums round-trip
  because their invariant format equals the natural literal; date types don't.
- **Fix direction:** special-case date types in `FormatInvariant` to round-trip formats
  (`DateOnly`/`TimeOnly` → `"O"`-style ISO, `DateTime`/`DateTimeOffset` → `"O"` or the sortable
  form) — ISO parses fine under the existing invariant parse, numeric behavior untouched.

## Low

### ☑ L10 — `EditSelectString<int>` blank suppression re-opens the "first option displays selected while the model holds default" mismatch **⚖ decision**
- **Where:** `Controls/EditSelectString.razor:21-33` — the L5 auto-suppression (`ShowNullOption`
  false for non-nullable value types) removes the blank that used to absorb the browser's fallback
  selection.
- **Failure:** `EditSelectString<int>` bound to an untouched `Rating = 0`, `Options=["1","2","3"]`:
  `CurrentValueAsString` is `"0"`, no option matches, browser visually selects **"1"** while the
  model holds 0 — submit records 0 after the UI showed 1. This is the exact defect class the blank
  option was added to fix (bc67ec0), and it contradicts the razor comment directly above the
  `@if (ShowNullOption)` block. The recorded L5 decision covered selectability, not this
  display-side consequence.
- **Options:** (a) for suppressed-blank value types, render a hidden+disabled placeholder option
  when the current value matches no option (shows blank, not selectable); (b) accept + document
  ("give non-nullable selects a sentinel first option or a matching default"); (c) revert
  suppression for the unmatched-value case only.

### ☑ L11 — `UnregisterField` isn't ref-counted, breaking the duplicate-registration case `RegisterField` explicitly supports
- **Where:** `Controls/FormOptions.cs:22-37` — `RegisterField` dedups because "two controls bound
  to the same property" is a supported pattern (its own doc comment); the L2 `Dispose`/swap
  unregister (`EditControlListBase.cs`) removes the single shared entry unconditionally.
- **Failure:** the same list-bound field rendered twice (page section + edit modal, both bound to
  `model.Tags`); closing the modal disposes one control → unregisters → `ValidationView` silently
  drops that field's summary messages/anchor while the surviving copy still renders and is invalid.
  Before the L2 fix, dispose left the registration intact so the survivor kept working.
- **Fix direction:** ref-count registrations (`Dictionary<FieldIdentifier, int>`), or on unregister
  only remove when no other live registrant — plus a regression test with two controls sharing a
  field where one is disposed.

### ☑ L12 — `_module ??= import` race with `DisposeAsync` leaks the module reference in five components (Table has the fix; M1 covered the handle, not the module)
- **Where:** `Controls/UiKit/Modal.razor:132` + `DisposeAsync` (`:165-173`), `Drawer.razor` (same
  shape), `Popover.razor:89,98` + `:129-136`, `Popconfirm.razor` (same shape),
  `Select/Select.razor.cs` (three import sites). `Table.razor:403-410` already re-checks
  `_disposed` after the import and disposes the module — the same defect, half-applied.
- **Failure:** `OnAfterRenderAsync` starts the import → component removed (navigation) →
  `DisposeAsync` sees `_module == null`, does nothing → import completes and assigns `_module` on
  the disposed instance → one JS object reference stranded in the circuit's store for the circuit's
  life on Server. Bounded (one per disposed-mid-import instance) but mechanical.
- **Fix direction:** replicate Table's post-import `_disposed` re-check (dispose-and-null the
  module) in all five; Modal/Drawer already have `_disposed` flags from M1, Popover/Popconfirm/
  Select need one.

### ☑ L13 — Select wrapper z-index (JS-written) is clobbered by the Blazor-bound `style` on a `Width` change while open
- **Where:** `Controls/Select/Select.razor:10` (`style="@WidthStyle"` — Blazor-bound) vs
  `Controls/wwwroot/wss-select.js:44` (`wrapper.style.zIndex = z + 1`) — the exact
  attribute-clobber class the overlay panel was fixed for (place() comment, wss-overlay.js:66-69);
  the Select wrapper is the one remaining JS style write sharing an element with a bound `style`.
- **Failure:** dropdown open + parent re-renders with a *different* `Width` → Blazor rewrites the
  whole `style` attribute, wiping the JS z-index → wrapper drops below its own full-screen backdrop
  (z 1040+) → clicks on the select's input/tags/clear button hit the backdrop and close the
  dropdown — the exact bug the z assignment was added to fix. Narrow trigger (dynamic Width while
  open), mechanically certain.
- **Fix direction:** move the open z-index into C#-owned state (append to `WidthStyle` while open,
  value passed back from `placeDropdown`), or write a CSS variable on a non-Blazor-bound ancestor.

### ☑ L14 — Disabled Popconfirm with an interactive child leaves a live-looking, tabbable, silently-inert button
- **Where:** `Controls/wwwroot/wss-overlay.js:114-116` — the disabled branch only strips
  `aria-haspopup`/`aria-expanded` from an interactive child; only the fallback (promoted-wrapper)
  branch gets `aria-disabled="true"` + `tabIndex=-1`. `Popconfirm.razor:73` guards `Toggle`.
- **Failure:** `<Popconfirm Disabled="true"><button>Delete</button></Popconfirm>` — the button is
  tabbable, styled enabled, and clicking does nothing, with no assistive-tech hint. Regresses the
  intent of the earlier "disabled trigger is aria-disabled and out of the tab order" fix, which now
  holds only for plain-content triggers.
- **Fix direction:** mirror `aria-disabled` onto the interactive child in the disabled branch (and
  remove it when re-enabled), or document that consumers must disable their own trigger element
  alongside `Disabled`.

### ☑ L15 — Nested `FormDefaults` don't chain: an inner instance fully shadows an outer one **⚖ decision**
- **Where:** `Controls/FormDefaults.razor(.cs)` + resolution at `FormLabel.razor.cs:57` /
  `FieldValidationDisplay.razor.cs:52` — the nearest cascaded `FormDefaults` wins whole-hog; an
  unset inner property falls through to the process-wide static, not the outer `FormDefaults`.
- **Failure:** the MFE composition this component exists for — host wraps the page in
  `<FormDefaults IsRequiredStarHidden="true">`, an MFE wraps its root in
  `<FormDefaults ShowFieldNameInValidation="false">` (leaving the other property null) → forms
  inside the MFE silently regain required stars (fall to the static, bypassing the host default).
- **Options:** (a) chain — `FormDefaults` takes an outer `FormDefaults` as `[CascadingParameter]`
  and falls through per property (note: `IsFixed="true"` on the cascade is fine, the chain resolves
  at setup); (b) document non-chaining explicitly ("innermost wins per tree, not per property").
  The xmldoc currently says null falls to the static, so (b) is arguably just making the existing
  contract loud.

## Verified clean this round (don't re-litigate)

- **Round-2 fix regressions:** H1 (zero-size/oversize/read-failure paths, MaxFiles-unmount fixed by
  construction), H2 (every remaining `EditContext` dereference null-guarded), M1 (dispose-race
  ordering correct both interleavings, Modal + Drawer), M2 (doubles/enums/strings round-trip — the
  date gap is new M13), M3 (`edit-sr-only` + for/id pairing), M4/M5/M8 (full gesture matrices,
  stale-flag, right-button), M6 (both readers use the full chain; no other consumers), L1/L3/L4/L6/L7
  (sentinel recompute self-heals during Other-typing and Options swaps; the L7 latch keeps cache
  hits; L3's fallback target has a visible focus style).
- **EditFile lifecycle:** per-file failure isolation, write-back-before-notify ordering, pending-
  focus consume-then-null race-free, ContentType/LastModified/Size passthrough, duplicate names via
  reference identity, disabled/read-only closed, `>=` cap check off-by-one-free.
- **SelectParsing other callers:** enum name round-trip exact, `Nullable<T>` boxes through
  `IFormattable` correctly, L5 empty→default behavior is the recorded decision.
- **JS modules:** place() flip/shift math (viewport-relative, no scrollX/Y needed for a delta),
  placeDropdown flip + clearZ pairing, scroll-lock ref-count balance across Escape/mask/rapid-
  toggle/out-of-order stacked dispose, trap listener phase symmetry, focusById timing + guards,
  wss-table.js indeterminate re-sync when `Selectable` toggles, every new call site catch-guarded
  and prerender-safe.
- **Concrete verification:** 352 bUnit × net8/9/10 (Release) and 76/76 Playwright E2E green on
  current `master`.

## Suggested order of work (round 3)

1. **M9 + M11 together** — same function, opposite pressures (re-query every call vs call less);
   one reconciled change to `syncTrigger` + its two call sites, with an e2e for a swapped trigger
   child.
2. **M13, L11, L12, L13** — mechanical fixes with obvious tests, no design decisions.
3. **M12, L10, L15 (⚖)** — need Dave's call on API shape/defaults before code.
4. **M10, L14** — trigger-contract polish; decide alongside M9's rework since they touch the same
   branch of `syncTrigger`.

---

# Evaluation — Round 4: Post-Round-3 Review

**Date:** 2026-07-07 · **Scope reviewed:** `163d1eb..66c8710` — everything landed after the round-3
docs commit: the round-3 fix batch as it actually landed (`bc067d8..163d1eb` re-read in place), the
trim/AOT work (29930bc, 0a7eaa4, 4ac261a), the touch/tooltip fixes (a35e083, 4e1501c), and the new
`FormOptions.RequiredResolver` + three-state `IsRequired` feature (66c8710).
**Baseline at review time:** green — 384 bUnit tests × net8/net9/net10, 79/79 Playwright E2E,
visual baselines intact.

Method: four adversarial lenses run sequentially (regression-hunt on the round-3 fixes; trim/AOT
annotation soundness; touch/tooltip side-effect trace; RequiredResolver deep read), every claim
verified against current `master` before recording. Numbering continues from round 3. No fixes
applied yet — this section is the findings tracker.

## Medium

### ☐ M14 — M13 is a half-fix: `EditSelect<DateTimeOffset>` still can't match any authored option value; `DateTime`/`TimeOnly` match only one authored form
- **Where:** `Controls/Helpers/SelectParsing.cs` `FormatInvariant` date arms (from d62c9f3) —
  `DateTimeOffset` formats with `"O"`; `DateTime` with `"s"`; `TimeOnly` with `HH:mm:ss`.
- **Failure:** `"O"` for `DateTimeOffset` always emits seven fractional digits
  (`2026-06-15T00:00:00.0000000+02:00`). No hand-authored ISO option value
  (`<option value="2026-06-15T00:00:00+02:00">`) ever equals that string, so the visual selection
  is lost immediately after picking — the exact desync M13 set out to fix, still fully present for
  this type. Lesser variants: a `DateTime` bound against a date-only authored value
  (`value="2026-06-15"`, which the parse side happily accepts) mismatches `"s"`'s
  `2026-06-15T00:00:00`; a `TimeOnly` authored as `"13:30"` mismatches `13:30:00`. The parse side
  accepts all these shorter forms, so the model updates while the display snaps back — the
  asymmetry is the trap. README changelog (~line 483) overclaims: "format to the ISO forms option
  values are authored in" is true only for `DateOnly`.
- **Fix direction:** emit the shortest round-trippable form — for `DateTimeOffset` format
  `yyyy-MM-ddTHH:mm:ssK` when sub-second is zero (fall back to `"O"` otherwise); document the one
  canonical authored form per type in the README (and fix the changelog claim). A bUnit case per
  type asserting the formatted value equals the naturally-authored literal.

## Low

### ☐ L16 — L14's `aria-disabled` tracking still clobbers a consumer's own `aria-disabled` (the exact case the tracking exists for)
- **Where:** `Controls/wwwroot/wss-overlay.js` `applyTrigger` disabled branch (from bc067d8):
  `target.setAttribute('aria-disabled', 'true'); target.__wssAriaDisabledByWss = true;` — the flag
  is set without checking whether the attribute already existed.
- **Failure:** consumer keeps their own `aria-disabled="true"` on the trigger child (their busy
  state); the component's `Disabled` goes `true` → `false`. On the false transition the flag says
  "we set it" and removes the attribute — the consumer's own state is wiped. The bc067d8 commit
  message and the code comment both claim "a consumer's own aria-disabled is left alone"; that
  holds only when the component was never disabled.
- **Fix direction:** set the flag only when `!target.hasAttribute('aria-disabled')` at set time
  (one-line guard). bUnit can't see JS; e2e or accept-as-is given the narrow trigger contract.

### ☐ L17 — `applyTrigger` never cleans popup ARIA off a *previous* child target that stays in the DOM
- **Where:** `Controls/wwwroot/wss-overlay.js` `applyTrigger` (from bc067d8) — resolves
  `querySelector(WSS_TRIGGER_SELECTOR)` fresh each call but tracks no previous target; only the
  *wrapper's* promotion is reverted.
- **Failure:** the resolved trigger changes identity while the old element remains attached — a new
  focusable element inserted before the current trigger, or the current one losing its `href`/
  `tabindex` match — and the old element keeps `aria-haspopup`/`aria-expanded` (and possibly the
  L16 `aria-disabled` + flag) indefinitely: two elements announce popup semantics, one is a lie.
  Narrow: the documented contract is a single interactive child, and the common `@if`-swap detaches
  the old node (which is why M9's fix is otherwise correct).
- **Fix direction:** remember `el.__wssPrevTarget`; when the resolved target differs, strip
  `aria-haspopup`/`aria-expanded`/our `aria-disabled` from the previous one before applying to the
  new one.

### ☐ L18 — Checkbox-list fieldsets carry `aria-required`/`aria-invalid`/`aria-errormessage` on `role="group"`, where ARIA 1.2 doesn't support them **⚖ decision**
- **Where:** `Controls/EditCheckedStringList.razor:9-12` / `EditCheckedEnumList.razor` fieldset —
  added by 2ed6416 (round-2 batch; predates this round's window but never recorded, and it silently
  reversed the recorded f77ab9e decision that checkbox-list groups deliberately get *no*
  `aria-required` precisely because `group` doesn't support it).
- **Failure:** ARIA 1.2 lists `aria-required`/`aria-invalid`/`aria-errormessage` as supported on
  `radiogroup` (the radio fieldsets are correct) but not on `group`. Screen readers generally
  ignore unsupported attributes (so the practical harm is silence, not wrong announcements), and
  static checkers (axe `aria-allowed-attr`) may flag every required checkbox list as a violation in
  consumer audits.
- **Options:** (a) drop the three attributes from the checkbox fieldsets — required/invalid state
  stays on the legend star, the per-checkbox `aria-invalid`, and the visible message (the f77ab9e
  position); (b) keep them knowingly and record the reversal as deliberate here. Either way the
  decision should be on the record; it currently isn't.

## What was checked and came back clean

- **RequiredResolver / three-state IsRequired (66c8710):** precedence (`IsRequired` param →
  `[Required]` → resolver) implemented once in `EditControlInit.IsRequired` and consumed
  identically by all three bases' `aria-required` and FormLabel's star (same cascade, same inputs —
  the star/aria disagreement class is structurally closed); resolver null-Model-guarded for
  standalone FormLabel; recompute wired into both `InitState` and `OnParametersSet` in all three
  bases; both source-breaking surfaces (`bool?` on `IEditControl`, the `Init` tuple shape)
  documented in the README changelog; tests pin the force-off override.
- **Trim/AOT (29930bc, 0a7eaa4):** both IL2070 suppression justifications hold (ILLink fully
  preserves fields of marked enum types; the Field-expression rooting argument has a graceful
  fallback either way); `GetValuesAsUnderlyingType`+`ToObject` preserves `Enum.GetValues` ordering
  and duplicate semantics; the `ComputeComparable` rewrite's lost corner (Nullable of a
  generic-only-`IComparable<T>` type) degrades to non-sortable exactly as documented; the trim-test
  root is opt-in-conditional and the e2e fixture's default path is byte-identical.
- **Touch/tooltip (a35e083, 4e1501c):** the trap's capture-phase mousedown `preventDefault`
  genuinely prevents mask focus (and doesn't suppress the synthesized tap-click); Select refocuses
  its input on open so wrapper focus is transient; hover and focus both flip the tooltip's
  `aria-hidden` in C# so the `[aria-hidden="true"]` CSS gate doesn't kill mouse reveals (the
  pre-hydration trade-off is documented in the commit); both `::before` hit areas have a
  `position:relative` anchor.
- **Round-3 fixes:** z-index dual-write agreement (L13) sound including the 0-guard and both close
  paths; `GetJsModuleAsync` covers both dispose interleavings, as do the Modal/Drawer post-import
  re-checks (L12); `MaxTotalBytes` counts only successfully-buffered bytes and screens allocations
  per-file first (M12); owner-set registration handles the shared-binding dispose and the
  model-swap re-registration, and both `FormDefaults` consumers use the chained `Effective*`
  accessors (L11/L15); Popover/Popconfirm sync-skip caches only on success and Popconfirm's pair
  correctly includes `Disabled` (M11).
- **Concrete verification:** 384 bUnit × net8/9/10 and 79/79 Playwright E2E green on current
  `master` before review start.

## Suggested order of work (round 4)

1. **M14** — mechanical, obvious tests; fix the changelog line in the same commit.
2. **L16 + L17 together** — same function; L16 is a one-line guard, L17 a small prev-target field.
3. **L18 (⚖)** — needs Dave's call: drop the attributes or record the reversal as deliberate.
