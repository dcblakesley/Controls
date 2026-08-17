# Read-Only View Plan — Field Accessibility + Number Formatting — 2026-08-16

**Status: planned. Implementation deferred to the week of 2026-08-17.** Nothing in Part 1 or Part 2 is
built. The only work already shipped is the option-independent remediation listed under
[Already fixed](#already-fixed-shipped-2026-08-16), which is correct regardless of how the open decisions
land.

Scope: how an `Edit*` control renders when it is **not** in edit mode — the accessible semantics of that
view (Part 1) and the formatting of numeric values in it (Part 2). Both came out of a live session on
2026-08-16; both were investigated with a real browser rather than from source alone.

## The goal, in the library owner's words

> "The goal is that a screen reader would go through and show the label/value combination so a blind user
> would understand that it is a read-only **field**. But visual users won't see a textbox or a disabled
> textbox."

Two hard constraints, and any accepted design must satisfy both:

- **AT constraint** — a blind user navigating a form encounters each read-only field, hears its label and
  value, and understands *that field* is not editable. Per-field, not per-form: a form is usually entirely
  read-only, but not always, so **mixed forms are a first-class case**.
- **Visual constraint** — it must not look like a textbox or a disabled textbox. It renders as plain text,
  exactly as it does today.

---

# Part 1 — Read-only field accessibility

## The problem, measured

In read-only mode a control renders its value into a plain `<div>` (`Controls/ReadOnlyValue.razor`) carrying
`aria-labelledby="lbltext-{id}"`, preceded by a `<label id="lbl-{id}">` that has **no `for=`** — because
there is nothing labelable to point at (`Controls/FormLabel.razor:89`, via
`IsForLabelable=@ShowEditor`).

Two consequences:

1. **The `<label>` is genuinely orphaned.** Chrome DevTools' "No label associated with a form field" is
   correct, not a false positive. This is **pre-existing in the published 10.8.2** — A/B against `e508137`
   shows the only `FormLabel` change since is `@DisplayLabel()` → `@LabelFor()`, byte-identical when
   `LabelContent` is unset, with the `for=` expression untouched.
2. **The value is not a named object.** A bare div is `role="generic"`, and ARIA prohibits naming a roleless
   element. Chrome *does* compute `name="Order Number"` on it, but the node is not *presented* as named, so
   the whole form collapses into one anonymous text run:

   ```
   - text: Order Number SO-100244 Ship To …
   ```

   axe-core 4.10.2 flags this as `aria-prohibited-attr` (impact **serious**, reported under `incomplete`
   / needs-review, not `violations`): **20 nodes** across a read-only form.

`EditDisplay` (`Controls/EditDisplay.razor:15-22`) and `EditString`'s masked row
(`Controls/EditString.razor:121-126`) already escape this by hand-rolling `role="group"`; `EditDisplay`'s
own comment cites this exact axe finding. The shared `ReadOnlyValue` never got the fix.

## Why `role="group"` alone is not sufficient

It repairs the naming, and nothing else. Real Tab walk through a **mixed** form (4 read-only + 4 editable,
interleaved):

| Approach | Tab sequence |
|---|---|
| Today (baseline) | `ShipTo → Units → RushOrder → Comments` |
| **+ `role="group"`** | `ShipTo → Units → RushOrder → Comments` — **byte-identical** |
| Native `<input readonly>` | `OrderNumber[ro] → ShipTo → UnitPrice[ro] → Units → MixStatus[ro] → RushOrder → PlacedOn[ro] → Comments` |

`role="group"` adds **zero** tab stops and **zero** form-control nodes. A user navigating by form field lands
on 4 of 8 fields and steps silently over the read-only ones sitting visually between them. In an
all-read-only form that is merely limiting; **in a mixed form it is actively misleading**, because the form
appears to have fewer fields than it does.

What it *does* buy, measured — the browse-mode collapse is genuinely repaired:

```
baseline:  - text: Order Number SO-100244 Ship To      (one anonymous run)
+group:    - text: Order Number
           - group "Order Number": SO-100244
```

## Recommended design: a hybrid

**Scalar single-text-node values → a native `<input readonly>` / `<textarea readonly>`, CSS-reset to look
like plain text. Composite renderings → a properly named `role="group"` / list.**

Measured properties of the native-input route:

- AX node: `role=textbox name="Order Number" value="SO-100244" readonly=True focusable=True` — the
  "Order Number, read only edit, SO-100244" shape.
- **`readonly` is natively focusable and tabbable; `disabled` is not.** Confirmed with real Tab presses:
  focus landed on the readonly field and on a readonly + `aria-disabled` field, and **skipped** the disabled
  one. Forms-mode reachability therefore comes free with **no artificial `tabindex`** — and `disabled` is
  the wrong mechanism for AT as well as visually. (`EditBool`'s `RenderAsCheckboxWhenReadOnly` already
  reached the same conclusion independently: it writes `aria-disabled` and withholds native `disabled`.)
- **Visually indistinguishable** from today with a flat reset (`font/color/line-height: inherit;
  border/padding/margin: 0; background: transparent; appearance: none`). Side-by-side screenshots of a mixed
  form show plain text, no box, no disabled affordance. A focus ring appears only on focus, which is
  required anyway.
- Text remains selectable and copyable.

### The dividing line

Convert **only** nodes whose entire read-only content is a single text node.

**Convert:** `EditString` (plain), `EditNumber`, `EditTextArea`, `EditDate`, `EditDateNative`, `EditSelect`,
`EditSelectEnum`, `EditSelectString`, `EditSelectSearch`, `EditMultiSelect`.

**Stay a named group / list** (composite content — cannot be an input):

- `EditColor` has-color branch (swatch + hex)
- `EditFile` has-files branch (`<ul>` of rows with download buttons)
- `CheckboxOptionList` (`<ul>` of rows)
- `EditString` masked row (value + eye toggle + `role="status"` live region) and URL row (already a named `link`)

**Judgment call, deliberately unresolved — decide during implementation:** `EditBool`, the radio family,
`EditRange`, `EditDateRange` each render a single string and *could* convert. Announcing a boolean as
"read only edit, Yes" is defensible but a `textbox` role for a non-text field is a small semantic fib, and
the radios/checked lists already sit inside a fieldset where the group route is more honest.

### Load-bearing implementation details

- **Keep `aria-labelledby="lbltext-{id}"` even though `<label for>` now works.** With `for` alone the
  accessible name absorbs the tooltip trigger — measured superseded name source
  `"More information about Notes"`, and an earlier run produced the literal name
  `"Notes More information about Notes"`. This is the same failure `lbltext-{id}` was created to prevent.
  `aria-labelledby` supersedes cleanly.
- **`EmptyText` ("Not Set") must be a `placeholder`, not a `value`.** As a `value` it announces identically
  to a real stored value of "Not Set" — a semantic fib. As a placeholder the node announces as blank and the
  greyed text still renders.
- **Multi-line wrapping requires CSS `field-sizing: content`.** With it, `<textarea readonly>` +
  `overflow-wrap: anywhere` reproduced the current div **pixel-identically** in a 220px column (48px 2-line,
  96px 4-line). **Without it the textarea clips to 24px and scrolls horizontally to the end of the token —
  visible data loss, worse than today.** See [Risks](#risks-and-open-questions).
- Do **not** fold `EditDisplay` or `EditString`'s masked row onto the shared component. `EditDisplay` would
  lose its `AttributeSplat.Rest(...)` and `MergeStyle(...)` (consumer `data-*`/`title`/inline style silently
  vanish) and its naming gate differs; the masked row has three children where `ReadOnlyValue` takes one
  string. They belong on the group side of the line — document the divide so it doesn't read as accidental.

## Costs

- **Every visual baseline containing a read-only field moves.** Measured 24px → 32px per field; an 8-field
  mixed section grew 506 → 538px. Tunable, but the PNGs must be regenerated.
- **136 test references** to `edit-readonly-value` (124 bUnit, 12 e2e) across ~28 files. Mechanical — the
  value moves from text content to the `value` property — but not small.
- **More tab stops for sighted keyboard users:** +4 in the 8-field mixed form, +23 in an all-read-only form.
  This is inherent to the goal, not a defect. `tabindex="-1"` removes it and was measured to restore the old
  tab order — but it discards the entire benefit, so it should not be used.
- `.edit-readonly-value` currently carries only 2 declarations (`overflow-wrap`, `min-width`) and would need
  a full input reset. Consumers styling that class are exposed, as are the known consumer button/input
  resets that clamp heights.
- **Version: minor (10.9.0), not a patch** — consumer-visible markup change requiring consumers to read and
  adjust.

## Risks and open questions

1. **`field-sizing: content` cross-browser support is unverified** (Chromium only in this investigation).
   If unsupported elsewhere, multi-line read-only values clip and lose data. The library already ships an
   `AutoSizeTextArea` JS helper (`JsInteropEc`) that is a plausible fallback. **Settle this before converting
   anything multi-line** — it is the one risk that makes the result worse than today.
2. **No real screen reader was run.** All AT claims are Chromium's accessibility tree plus Playwright's aria
   snapshot, which *model* presentation. Specifically unverified: actual NVDA/JAWS/VoiceOver verbalization and
   ordering; whether browse mode de-duplicates a `<label>`'s text next to the field it labels; whether
   quick-nav `F` finds a `role=textbox` node with `tabindex="-1"`. **One pass with NVDA + Chrome and
   VoiceOver + Safari against a mixed form is worth more than any further AX-tree work, and should gate the
   release.**
3. **Chrome's orphaned-label warning could not be reproduced in the harness** — CDP `Audits` reported zero
   form issues even against hand-written canonical bad markup, so headless Chromium's form-issue reporter
   appears inactive. The warning is real in the owner's browser. The native input *should* resolve it (a
   labelable element finally exists) but that is **inferred, not measured** — do not claim it as a benefit
   without confirming in real Chrome.
4. **Duplicate announcement.** With `role="group"`, the label text renders twice — once as the orphan
   `<label>`'s free text, once as the group's name; three times for fieldset-based controls
   (`group "Approved": text: Approved, group "Approved": "Yes"`). Mitigation measured to work:
   `aria-hidden="true"` on `span#lbltext-{id}` (the name still computes via `aria-labelledby`, which resolves
   hidden content; the tooltip trigger survives because it is a sibling, not a child). The span is shared
   with edit mode so it would need gating. **This only applies to the group side of the hybrid** — the native
   input has a real `<label>` and does not need it.
5. **`EditDateRange` renders one value node covering BOTH fields** in read-only, so the End field has no
   value node and no independent label association. Diagnosis requested; shape not yet decided.
6. **`CheckboxOptionList` per-option rows** have `HasLabelElement=false` and would become **anonymous**
   `group` nodes (`listitem: group: Red`) if a role were applied blindly. Any role must be gated on
   `HasLabelElement`.

## Rejected alternatives, with evidence

- **`role="textbox"` + `aria-readonly` on the existing div** — strictly worse than the native input. Requires
  `tabindex="0"` to be reachable (without it the tab order was unchanged; with it, 25 synthetic tab stops
  appeared), and it produced **new hard axe violations**: `aria-input-field-name` serious ×3 on the unnamed
  checked-list rows. All the downside of a form-control role with none of the native behavior.
- **`<dl>` / `<dt>` / `<dd>`** — canonically the most correct HTML for name/value pairs, but it fails the
  primary criterion: tab walk unchanged, no forms-mode presence. Cost would also be real (`FormLabel` would
  emit `<dt>`, losing `for` and the tooltip-trigger placement; `<dl>` restricts child element types,
  complicating the error region).
- **`role="none"` on the `<label>`** to kill the duplicate announcement — axe `aria-allowed-role` minor ×19
  ("ARIA role none is not allowed for given element"). Dead end.
- **Adding `for=` to the read-only label** — invalid HTML (a label may not point at a `<div>`); trades one
  Chrome issue for another ("Incorrect use of `<label for=FORM_ELEMENT>`").

## Already fixed (shipped 2026-08-16)

These were correct regardless of the pending decision and are already done, so they must **not** be
re-planned:

- `EditColor`'s has-color read-only branch rendered its own roleless div carrying `aria-labelledby` — the one
  node a shared `ReadOnlyValue` fix would not reach (axe 20 → 1 without it).
- 13 read-only call sites passed **no** `AriaDescribedBy` despite computing a valid one (`EditDate`,
  `EditDateNative`, `EditDateRange`, `EditBool`, all four radios, all three native selects,
  `EditSelectSearch`, `EditMultiSelect`) — their description/error/tooltip association was lost in read-only.
- `EditCheckedEnumList` / `EditCheckedStringList` dropped `aria-labelledby` from the fieldset in read-only, so
  the accessible name fell back to the native `<legend>` — which folds in the tooltip trigger's name, exactly
  the bug `lbltext-{id}` exists to prevent.

## Verification plan for the implementation

- bUnit for every ARIA wiring change (attributes and ids are assertable there; JS is not).
- e2e with **role- and name-based locators**, not CSS. Note the 2026-08-13 audit's headline finding: the e2e
  suite has 831 CSS locators against 20 `GetByRole` and 0 `GetByLabel`, so a green suite carries no
  correlation with an intact accessibility tree. This work should not repeat that.
- A real Tab-order walk over a mixed form, asserted — that is the measurement that distinguishes success from
  failure here.
- axe-core 4.10.2 before/after counts on a read-only form (`aria-prohibited-attr` baseline: 20).
- Regenerate visual baselines deliberately, in their own commit, after confirming each diff is the expected
  24px → 32px growth and nothing else.
- The NVDA + VoiceOver pass from [Risks](#risks-and-open-questions) item 2 gates the release.

---

# Part 2 — `EditNumber` read-only number formatting

Separate, smaller, and independent of Part 1 — but it lands in the same rendering path, so do them in the
same release if both go ahead.

## What already exists (shipped 10.7.0 — do not rebuild)

`EditNumber<T>` already has both hooks:

- A `Format` parameter (`EditNumber.razor.cs:305`).
- Automatic `[DisplayFormat(DataFormatString = …)]` honoring — `EffectiveFormat => Format ?? _attributes.FormatString()`
  (`:314`), with `AttributesHelper.FormatString` normalizing both `"{0:N2}"` and bare `"N2"`.

Precedence today: **`Format` parameter → `[DisplayFormat]` → bare culture-aware `ToString()`.** All three arms
are locked by tests in `EditNumberModelAttributeTests.cs`.

What is missing is only the last rung: with nothing specified, `1234.5m` renders `"1234.5"` and `9000000000L`
renders `"9000000000"` — culture-aware, separator-free. A null renders `EmptyText` ("Not Set").

**This is a deliberate house rule, not an oversight.** Date controls fall back to a mode-derived default
because a bare `DateTime.ToString()` is unusable; numbers deliberately stop at the culture default, because a
number's bare `ToString()` is already correct and complete. `EditRange.TooltipFormat`'s doc comment explicitly
pins itself to `EditNumber`'s read-only view.

Note also: read-only uses `CurrentCulture` while the editor uses `InvariantCulture` (required — a native
number input cannot round-trip a comma decimal). That is intentional and tested; a richer read-only default
widens the visual jump when a form toggles edit mode.

## The proposal and the recommendation

The owner proposed type-driven defaults: `int → "N0"`, `decimal → "N2"`, etc.

**Recommendation: ship it as an opt-in preset, not as a new built-in default.** The split that matters is
lossless vs lossy:

- `int → "N0"` only *adds* grouping — same number, easier to read. Lossless.
- `decimal → "N2"` **changes the displayed value**: `40.7128m` → `"40.71"`. The read-only view silently
  disagrees with stored data, with no indication rounding occurred.

Failure cases that make a blanket default unsafe, because the CLR type describes storage, not semantics:

| Value | Type default | Result |
|---|---|---|
| `int Year = 2026` | `"N0"` | `"2,026"` |
| `int Id = 10045` | `"N0"` | `"10,045"` |
| `decimal Latitude = 40.7128m` | `"N2"` | `"40.71"` — truncated coordinate |
| `decimal Rate = 0.0725m` (7.25%) | `"N2"` | `"0.07"` — meaning lost |
| `double x = 1.23456e-9` | `"N2"` | `"0.00"` |

Planned shape:

1. **`FormDefaults.NumberFormat` taking an enum preset**, null by default. A `Friendly` preset applies the
   type-driven mapping above; a **lossless grouping preset** adds separators but never drops precision
   (`9000000000` → `"9,000,000,000"`, `1234.5m` → `"1,234.5"`, `40.7128m` → `"40.7128"`). Render-tree-scoped,
   so it is MFE-correct where a DI option or static would not be; null default keeps rendering
   byte-identical. **Patch-safe.** Field-level `Format` and `[DisplayFormat]` continue to win over it.
2. **Honor `[DataType(DataType.Currency)]`.** Not a new default — the consumer already declared the field is
   money and the library ignores it, while honoring the sibling `[DataType]` values (`Password`,
   `Date`/`DateTime`/`Time`) in the same helper. **Open decision:** `"C"` takes its symbol from
   `CurrentCulture`, so a EUR amount under `en-US` renders `"$1,234.50"` — wrong, not merely different.
   Map to `"C"` and document, or map to `"N2"` and drop the symbol? Precedent suggests shipping it as a
   minor with a "worth a look before upgrading" callout, as the last DataAnnotations-honoring wave did
   (`719e1df`, 10.7.0).
3. **Add a demo section.** `DemoEditNumber.razor` has sections for `[Placeholder]` and `[MinValue]`/`[MaxValue]`
   but **nothing for `Format` or `[DisplayFormat]`**, and its model carries no `[DisplayFormat]`. A feature
   shipped since 10.7.0 and documented in three places still read as missing — that is a discoverability bug.
   Append after section 8 so `StepperSection => Nth(8)` (`EditNumberE2ETests.cs:37`) stays valid, or fix the index.

If the owner prefers `Friendly` as the built-in default instead, that is defensible — but it should be a
deliberate "breaking display change, read the changelog" **minor**, not something that slips out in a patch,
and it would silently break consumers' own snapshot and e2e tests, which we cannot see.

**No committed visual baseline captures a read-only `EditNumber`** (the two `EditNumber` snapshots are both
edit-mode; the read-only e2e asserts visibility and takes no screenshot), so none of Part 2 regenerates
baselines.

---

## Provenance

Everything above is from a 2026-08-16 session. Measured in a real browser (Chromium 149, Playwright, CDP
accessibility tree, axe-core 4.10.2 from CDN): tab-order walks, AX role/name/value/readonly/focusable
properties, the readonly-vs-disabled focus test, pixel comparisons, axe counts, and the
`aria-hidden`-on-the-name-source mitigation. Inferred but **not** measured: real screen-reader verbalization,
`field-sizing` support outside Chromium, and Chrome's own DevTools Issues behavior. Those three are called out
inline and each should be confirmed before the corresponding decision is finalized.
