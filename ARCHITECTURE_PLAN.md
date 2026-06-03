# Architecture Improvement Plan

Generated from architectural analysis on 2026-05-07. Tracks follow-up work after the
`EditControlBase`/`EditControlListBase` refactor (commits a356e83 → 9338dca).

> **Status (2026-06): implemented and shipped.** The core refactor (Phases 1–3 — centralized
> `ShouldShowComponent`/`IsValueDefault`, field registration in `InitState`, and the bUnit
> coverage) is in the codebase; see the "Architecture: `EditControlBase<TValue>`" entry in
> `README.md`. This document is retained as the historical design record. (Unrelated later work —
> the ported AntDesign-style Select + UI-kit controls — is documented in `CLAUDE.md` and `README.md`,
> not here.)

---

## Goals

1. Eliminate the ~400 lines of duplicated `ShouldShowComponent()` logic across scalar controls.
2. Collapse three divergent `isReadOnly` expressions into one base-class property.
3. Lift list controls to parity with scalar controls in test coverage.
4. Tidy the remaining inheritance/side-effect smells (EditRadio outlier, FieldValidationDisplay registration).

Out of scope for this round: localization-proof ValidationHelper, ReadOnlyValue validation styling.

---

## Phase 1 — Centralize Hiding & Read-Only Logic (highest leverage)

### 1a. Collapse the three read-only expressions onto `ShowEditor`

**Problem.** Three separate logic forms in the wild, all equivalent to `!ShowEditor`:

| File | Line | Expression | Reduces to |
|---|---|---|---|
| `Controls/EditString.razor.cs` | 53 | `(FormOptions?.IsEditMode ?? true) && IsEditMode` | `ShowEditor` |
| `Controls/EditDate.razor.cs` | 94 | `!IsEditMode \|\| (FormOptions != null && !FormOptions.IsEditMode)` | `!ShowEditor` |
| `Controls/EditNumber.razor.cs` | 66 | `!((IsEditMode && FormOptions == null) \|\| (IsEditMode && FormOptions!.IsEditMode))` | `!ShowEditor` |

`EditControlBase.cs:71` already exposes `ShowEditor`, defined via `EditControlInit.ShowEditor`
(`Helpers/EditControlInit.cs:35`) as `isEditMode && (formOptions?.IsEditMode ?? true)`. By De
Morgan's, `!ShowEditor` is exactly the "is in read-only mode" expression each control
re-derives — verified across the FormOptions null/true/false matrix.

**Change.** Replace the three inline expressions with `ShowEditor` / `!ShowEditor` at the call
sites. **Do not** introduce a separate `IsInReadOnlyMode` property — a second name for an
identical concept is itself the duplication this refactor targets. If a positive-form name turns
out to read better at call sites once Phase 1b lands, add it as a one-line alias:

```csharp
// Only if call sites genuinely read better — never re-derive from IsEditMode / FormOptions.
protected bool IsInReadOnlyMode => !ShowEditor;
```

**Touch.** Every `ShouldShowComponent()` and any inline checks in `.razor` files. Grep:
`(IsEditMode|FormOptions.*IsEditMode)` across `Controls/Edit*.razor*`.

### 1b. Pull `ShouldShowComponent()` into the base classes

**Problem.** Each scalar control reimplements HidingMode logic with a type-specific "is default"
check. Examples:

- `Controls/EditString.razor.cs:46–64` — default = `null || ""`
- `Controls/EditNumber.razor.cs:56–77` — default = `Convert.ToDouble(value) == 0`
- `Controls/EditDate.razor.cs:74–104` — default = `null || default(DateTime)`

**Change.** In `EditControlBase<T>`:

```csharp
protected virtual bool IsValueDefault() =>
    EqualityComparer<T>.Default.Equals(CurrentValue, default!);

protected virtual bool ShouldShowComponent()
{
    var hiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
    if (IsHidden) return false;
    var isNull = CurrentValue is null;
    var isDefault = isNull || IsValueDefault();
    return hiding switch
    {
        HidingMode.None => true,
        HidingMode.WhenReadOnlyAndNull => !(!ShowEditor && isNull),
        HidingMode.WhenReadOnlyAndNullOrDefault => !(!ShowEditor && isDefault),
        HidingMode.WhenNull => !isNull,
        HidingMode.WhenNullOrDefault => !isDefault,
        _ => true,
    };
}
```

**Per-control overrides only where semantics genuinely differ:**

- `EditString` — override `IsValueDefault()` → `string.IsNullOrEmpty(CurrentValue)` (keeps the
  current "empty string counts as default" behavior).
- `EditNumber` — override `IsValueDefault()` → numeric zero check (covers all numeric T).
- `EditDate` — base implementation already handles `default(DateTime)` etc. via
  `EqualityComparer<T>.Default`; verify and drop the override.
- Others — likely no override needed.

**Risk.** Behavior change for any control whose old `ShouldShowComponent` was subtly different.
Mitigate with the existing test suite + new tests in Phase 3.

### 1c. Same treatment for `EditControlListBase`

`Controls/EditControlListBase.cs:91–112` already centralizes list hiding — confirm it uses
`ShowEditor` after Phase 1a, and that scalar/list logic is symmetric.

---

## Phase 2 — Tidy Side Effects & Inheritance Outlier

### 2a. Move `FieldValidationDisplay`'s field registration to base

**Problem.** `Controls/FieldValidationDisplay.razor.cs:31–36` mutates
`FormOptions.FieldIdentifiers` during its own `OnInitialized`. If the component is rendered
conditionally (inside an `@if`), registration is incomplete and validation-summary linking breaks
silently.

**Change.** Register the field in `EditControlBase.InitState<T>()` instead — every control already
calls it once during init. `FieldValidationDisplay` then only *reads* the list.

**Touch.** `EditControlBase.cs`, `EditControlListBase.cs`, `FieldValidationDisplay.razor.cs`. Add a
test that confirms registration survives a control whose validation display is hidden behind a
boolean.

### 2b. Document why `EditRadio` keeps `InputRadioGroup<TValue>`

`Controls/EditRadio.razor.cs:4` inherits `InputRadioGroup<TValue>` directly instead of
`EditControlBase<TValue>`, then re-runs `EditControlInit.Init` manually
(`EditRadio.razor.cs:54–62`).

**Resolved (see Open Questions §2 and README §10.2.0):** Keep `InputRadioGroup<TValue>`. The
public API exposes `<InputRadio>` items as `ChildContent` (see `DemoEditRadio.razor:14, 41,
66, 84, 107`), and those children consume the cascading `InputRadioContext` that
`InputRadioGroup<T>` is the sole supplier of. Migrating would force a parallel `<InputRadio>`
replacement and break every consumer — explicitly not worth it.

`EditRadioEnum` and `EditRadioString` are unaffected: they render their own `<input
type="radio">` from a backing collection rather than accepting `<InputRadio>` children, so
they already inherit `EditControlBase<T>` without context plumbing.

**Action.** Add an `<remarks>` comment on the `EditRadio<TValue>` class declaration stating
this rationale and pointing readers to README §10.2.0. That's the entirety of Phase 2b.

**Effort.** 15 minutes, not the 1–4h previously estimated.

---

## Phase 3 — Test Coverage

### 3a. bUnit smoke tests for list controls

Add to `FormTesting/FormTesting.Client.Tests/`:

- `EditCheckedStringListTests.cs` — render with options, verify checkbox count, click → value
  update via `ToggleAsync`, read-only mode renders summary, `HidingMode.WhenNullOrDefault` hides
  empty lists.
- `EditCheckedEnumListTests.cs` — same shape against `Animal` enum.

### 3b. Hiding-logic regression tests

For each `HidingMode`, prove the centralized `ShouldShowComponent` matches the prior per-control
behavior on `EditString`, `EditNumber`, `EditDate`. These tests live in
`ControlSmokeTests.cs` or a new `HidingModeTests.cs`.

### 3c. FieldValidationDisplay registration test

Verify a control rendered inside `@if (false)` does **not** appear in
`FormOptions.FieldIdentifiers` (positive case) and one rendered normally **does** (negative case).
Cements the Phase 2a contract.

---

## Phase 4 — Cleanups (low priority, batch into a single PR)

- `Controls/EditDisplay.razor.cs:13` — drop the magic string `Id="NoId"`; default to `null`.
- Document `EditDisplay` vs `ReadOnlyValue` selection criteria in `README.md`.
- Pass resolved label text from `EditControlBase` into `FormLabel` and `FieldValidationDisplay`
  to avoid double reflection per render.

---

## Sequencing

| Phase | Effort | Risk | Ship |
|---|---|---|---|
| 1a (collapse to `ShowEditor`) | 1h | Low | Standalone PR |
| 1b (`ShouldShowComponent`) | 2h | **Medium** — behavior change | Same PR as 1a or follow-up |
| 1c (list parity) | 30m | Low | Same PR as 1b |
| 2a (registration move) | 2h | Low | Standalone PR |
| 2b (EditRadio doc) | 15m | None | Bundle with Phase 4 |
| 3a (list tests) | 3h | None | Land before Phase 1 if possible |
| 3b (hiding regression) | 2h | None | Land **with** Phase 1 |
| 3c (registration test) | 1h | None | Land with Phase 2a |
| 4 (cleanups) | 2h | None | Bundle |

**Recommended order:** 3a (safety net) → 1a → 1b → 1c → 3b → 2a → 3c → 2b → 4.

No version bump per phase — accumulate against current 10.1.0 and bump (`10.2.0`, since this
includes behavioral changes) only when cutting a NuGet release.

---

## Open Questions

- [x] ~~Does `IsInReadOnlyMode` add value over the existing `ShowEditor`?~~ **Resolved
      2026-05-12:** No. `!ShowEditor` is logically identical (verified by De Morgan over
      `EditControlInit.ShowEditor`). Do not add a separate property; reuse `ShowEditor` at every
      call site. If a positive-form alias proves ergonomic post-1b, define it as
      `protected bool IsInReadOnlyMode => !ShowEditor;` — never re-derive.
- [x] ~~Is there a real reason `EditRadio` uses `InputRadioGroup<T>`?~~ **Resolved 2026-05-13:**
      Yes — group-context cascading. `EditRadio`'s public API takes `<InputRadio>` children as
      `ChildContent` (`DemoEditRadio.razor:14, 41, 66, 84, 107`), and those children resolve a
      cascading `InputRadioContext` that *only* `InputRadioGroup<T>` supplies. Replacing the
      group would force a parallel `<InputRadio>` API and break every consumer. Already noted
      in README §10.2.0 as intentional. `EditRadioEnum` / `EditRadioString` inherit
      `EditControlBase<T>` because they render their own `<input type="radio">` markup and
      never see `<InputRadio>` children. Phase 2b narrows to "add a `<remarks>` comment on the
      class — 15 min."
- [x] ~~Should `IsValueDefault()` be public/protected for consumer overrides, or strictly
      internal?~~ **Resolved 2026-05-13:** `protected virtual`. Matches the existing
      `EditControlListBase.ShouldShowComponent` shape (line 91) and Microsoft's
      `InputBase<T>.FormatValueAsString` convention. Required for the planned `EditString` /
      `EditNumber` overrides and consistent with `EditControlBase<T>` being a documented
      extension point for consumer-defined controls (README §10.2.0). `internal` is rejected —
      it'd block external overrides, defeating the point of `virtual`. `public` adds nothing
      because no legitimate caller invokes `IsValueDefault()` from outside the class. Apply
      the same shape to the new `ShouldShowComponent` on `EditControlBase`.
