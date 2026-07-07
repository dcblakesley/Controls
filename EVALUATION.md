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

### ☐ L6 — Other-sentinel collision narrowed, not eliminated
- **Where:** `Controls/EditRadioString.razor.cs:28,94` — sentinel is `"__wss-other__"`; an Options
  entry literally equal to it still routes through the Other branch and overwrites the model with
  the (empty) other-text. Vastly less likely than the old `"Other"`, but the design still keys the
  Other radio through the value channel.
- **Fix (if bothered):** key the Other radio out-of-band (index/flag), not by value.
- **Deferred:** left open by choice — the collision requires an options entry literally equal to
  `"__wss-other__"`, and keying out-of-band is a non-trivial rework of the selection channel for
  negligible real-world risk. Revisit only if the Other contract is reworked for another reason.

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
