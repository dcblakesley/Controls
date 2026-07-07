# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```bash
# Build entire solution
dotnet build FormTesting.sln

# Build just the Controls library
dotnet build Controls/Controls.csproj

# Build just the Demo library
dotnet build Controls.Demo/Controls.Demo.csproj

# Run the test app (Blazor Server + WebAssembly)
dotnet run --project FormTesting/FormTesting/FormTesting.csproj

# Pack NuGet packages (Release)
dotnet pack Controls/Controls.csproj -c Release -o ./nupkg
dotnet pack Controls.Demo/Controls.Demo.csproj -c Release -o ./nupkg
```

Automated tests live in `FormTesting/FormTesting.Client.Tests/` (xUnit + bUnit) and multi-target net8.0/net9.0/net10.0, so `dotnet test FormTesting/FormTesting.Client.Tests/FormTesting.Client.Tests.csproj` runs the whole bUnit suite once per TFM (the library ships on all three, so the smoke tests guard each). Coverage focuses on the helpers (`EnumHelpers`, `AttributesHelper`, `EditControlInit`, `ValidationHelper`) and bUnit smoke tests for the controls — particularly the parsing logic ported in the `EditControlBase` refactor. The ported AntDesign-style controls have coverage too: `EditSelectControlsTests` (the form selects), `UiKitLeafControlsTests`, `UiKitDialogControlsTests`, `UiKitTableTests`, and `WasmToastTests`. Add new tests alongside any non-trivial helper or control change.

End-to-end tests live in `FormTesting/FormTesting.Client.E2ETests/` (xUnit + Playwright .NET, net10 only). Run with `dotnet test FormTesting/FormTesting.Client.E2ETests/FormTesting.Client.E2ETests.csproj`. There's one test class per Edit* control (render, interaction, read-only toggle, and a visual-regression baseline screenshot per `section.demo-section`), plus `EditSelectSearchE2ETests` / `EditMultiSelectE2ETests` for the searchable selects and `UiKitGalleryE2ETests`, which drives the standalone `/uikit` gallery page (Drawer, Popconfirm, Popover, Pagination, Modal, toasts — with baselines for the visually-distinct ones). The `AppFixture` launches the `FormTesting` Blazor Server host out-of-process on a free port for the duration of the run; the `BrowserFixture` shares one headless Chromium across all tests. Set `PWTEST_HEADED=1` to watch the browser locally.

**Visual regression workflow.** Baseline PNGs live in `FormTesting/FormTesting.Client.E2ETests/Snapshots/` and are committed. After an intentional UI change, regenerate with `UPDATE_SNAPSHOTS=1 dotnet test ...E2ETests.csproj` and commit the updated PNGs. On a failure, `*-actual.png` (what the test saw) and `*-diff.png` (red highlights on differing pixels) are written next to the baseline — both are gitignored. First-time setup also requires a one-time `pwsh FormTesting/FormTesting.Client.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium` (the MSBuild target attempts this automatically after first build).

## Project Structure

- **Controls/** — Main Razor Class Library (`WssBlazorControls` NuGet package). Multi-targets net8.0, net9.0, net10.0. Form `Edit*` controls live at the root; the ported AntDesign-style controls live in `Controls/Select/` (searchable selects + the `Select<T>` engine) and `Controls/UiKit/` (general widgets).
- **Controls.Demo/** — Demo components library (`WssBlazorControls.Demo` NuGet package). References Controls. Same multi-targeting.
- **FormTesting/FormTesting/** — Blazor Server host (net10.0).
- **FormTesting/FormTesting.Client/** — Blazor WebAssembly client that references both Controls and Controls.Demo.
- **FormTesting/FormTesting.Client.Tests/** — xUnit + bUnit unit tests (net10).
- **FormTesting/FormTesting.Client.E2ETests/** — xUnit + Playwright .NET e2e tests + visual regression baselines (net10).

## Architecture

### Component Pattern

Every edit control (EditString, EditNumber, EditDate, EditBool, EditSelect*, EditRadio*, EditChecked*, EditFile) follows the same structure:

1. **Inherits** one of two shared bases: `EditControlBase<T>` (scalar controls, an `InputBase<T>`) or `EditControlListBase<T>` (list-bound controls — `EditChecked*`, `EditMultiSelect`, `EditFile` — a `ComponentBase` binding `List<T>`). Both hoist the `IEditControl` params + cascading options; derived controls declare `Field` and call `InitState(Field)`. (`EditRadio<T>` inherits `InputRadioGroup<T>`; `EditDisplay` is a plain `ComponentBase`.)
2. **Implements** `IEditControl` (common properties: Label, Description, Tooltip, IsEditMode, IsHidden, IsDisabled, etc.)
3. **Receives** `FormOptions` and `FormGroupOptions` as `[CascadingParameter]`s
4. **Split** into `.razor` (markup) + `.razor.cs` (code-behind)
5. **Wraps** content in a `div.edit-control-wrapper` containing `<FormLabel>`, the input element, and `<FieldValidationDisplay>`
6. **Supports** edit/read-only toggle — edit mode shows the input; read-only mode shows `<ReadOnlyValue>`

**`EditFile`** is the file-upload control: it binds a `List<IBrowserFile>` (drag-and-drop + click-to-browse, with `AllowedExtensions` filtering, a `MaxFileSizeBytes` per-file cap, and optional `MaxFiles` count), rides `EditControlListBase<IBrowserFile>`, and follows the same wrapper pattern. Prefer it over a hand-rolled `<InputFile>`. Demoed via `DemoEditFile.razor`.

### Key Types

- **`IEditControl`** — Interface defining all common control properties (Id, Label, Description, Tooltip, IsEditMode, IsHidden, IsDisabled, Hiding, ContainerClass, etc.)
- **`FormOptions`** — Cascading parameter for form-wide settings (IsEditMode, HidingMode, ShowBoundValues, IsRequiredStarHidden)
- **`HidingMode`** — Enum controlling conditional display (None, WhenReadOnlyAndNull, WhenNull, WhenNullOrDefault, etc.)
- **`FormLabel`** — Shared label component handling display name extraction, required star, description, and tooltip
- **`FieldValidationDisplay`** — Validation message display with accessibility attributes

### Helpers (Controls/Helpers/)

- **`AttributesHelper`** — Extracts `[DisplayName]`, `[Description]`, `[ToolTip]`, `[Range]`, `[MinLength]`, `[MaxLength]` from model properties via reflection. Also generates element IDs from property names.
- **`ValidationHelper`** — Rewrites default DataAnnotation error messages into shorter forms (e.g., "The X field is required" → "Required").
- **`EnumHelpers`** — Resolves `[EnumDisplayName]` attributes and converts values to valid HTML IDs.

### Custom Attributes (defined in AttributesHelper.cs, namespace `Controls.Helpers`)

- `[ToolTip("text")]` — Popover tooltip on info icon
- `[EnumDisplayName("text")]` — Custom display name for enum values
- `[MustBeTrue]` — Validation requiring a bool to be true

### CSS Conventions

Form controls use the `edit-` prefix (in `Controls/wwwroot/edit-controls.css`). The ported UI-kit controls use the `wss-` prefix + `--wss-*` theme tokens (in `Controls/wwwroot/wss-controls.css`). Key form classes: `edit-control-wrapper`, `edit-label`, `edit-label-required-star`, `edit-validation-message`, `edit-readonly-value`, `edit-tooltip-*`.

**The library styles via global stylesheets, not CSS isolation.** There is no scoped-CSS bundle link in the host/consumers (no `{App}.styles.css` reference anywhere), so `.razor.css` files do **not** load for library components. Put all control CSS in the appropriate global file (`edit-controls.css` for form controls, `wss-controls.css` for UI-kit). Consumers link both from `_content/WssBlazorControls/`. The `--wss-*` tokens default to AntDesign 4.x and bridge to the consumer's `--color-primary` / `--color-danger` / `--border-color` where present.

### UI Kit controls (ported AntDesign-style — `Controls/Select/` + `Controls/UiKit/`)

A second category of controls ported from `Standalone.Controls` (dependency-free AntDesign look-alikes), in two flavors:

- **Form selects** (`Controls/Select/`): `EditSelectSearch<T>` (single, searchable) and `EditMultiSelect<T>` (multiple/tags) follow the standard `Edit*` pattern above but render the `Select<T>` engine instead of a native `<select>`. They sit alongside the existing `EditSelect*` (unchanged). `EditSelectSearch` rides `EditControlBase<T>`; `EditMultiSelect` rides `EditControlListBase<T>`. Both set the value via the engine's callback (no string parsing — `TryParseValueFromString` throws, like `EditBool`).
- **General widgets** (`Controls/UiKit/`): `Select<T>` (the engine), `Alert`, `Skeleton`, `Popover`, `Pagination`, `Modal`, `Drawer`, `Popconfirm`, `Table<TItem>` (+ `Column`/`PropertyColumn`/`ActionColumn`), and WASM-only `WasmMessageService`/`WasmNotificationService` (+ containers). These are plain components — **not** `IEditControl`/form-bound.

Conventions for this category: namespace `Controls` (pin `@namespace Controls` on each `.razor`); `wss-` classes; `--wss-*` tokens; nullable enabled; CSS lives in the global `wss-controls.css`. Three tiny RCL JS modules live at `_content/WssBlazorControls/`: `wss-select.js` (Select keyboard scroll-into-view **+ viewport-aware dropdown flip** — a Select near the bottom of the screen opens upward), `wss-overlay.js` (Popover/Popconfirm viewport-aware flip + cross-axis shift via the `--wss-shift` CSS variable, **plus the Modal/Drawer focus trap + body-scroll lock**), and `wss-table.js` (sets the native `indeterminate` property on the Table's "select all" checkbox — the mixed state has no HTML attribute). All import lazily (path tied to PackageId) and degrade gracefully when JS is unavailable (prerender/tests) — the controls fall back to their default CSS placement / a plain checkbox.

The placement enum for `Popover` / `Popconfirm` is `PopupPlacement` (Top/Right/Bottom/Left); `Drawer` uses `DrawerPlacement`. All UI-kit enums live in `Controls/UiKit/UiKitEnums.cs` (`AlertType`, `PopupPlacement`, `DrawerPlacement`, `TableSize`, `PagerAlign`, `PagerPosition`, `SortDirection`).

`Table<TItem>` reads its columns from child `Column` / `PropertyColumn` / `ActionColumn` components — these are declarative metadata that emit no markup of their own. The Table re-collects them in document order on every render (each registers via a cascading `Table` reference; `Column` is `IDisposable`), so columns may be conditionally rendered (`@if`) — a hidden column drops out and a re-shown one returns to its declared position, with no zombie or duplicate. Beyond row selection (controlled `SelectedItems` + `SelectedItemsChanged`, or uncontrolled), the Table supports:
- **Column sorting** — `Sortable="true"` on a `PropertyColumn` (comparison derived from `Property` via `Comparer<T>.Default`; a non-comparable `TProp` degrades to non-sortable rather than throwing on click — use `SortBy` to sort any type), or a `SortBy` `Comparison<TItem>` on any `Column` for custom/template columns. A header click cycles ascending → descending → unsorted (original `DataSource` order); the sort is stable (index tie-break), survives a `DataSource` swap, and drops if its column is removed. Headers are keyboard-focusable `<button>`s exposing `aria-sort` (a title-less sortable column's button falls back to `aria-label="Sort"`).
- **Paging** — `PageSize > 0` enables the built-in pager; `PagerPosition` (Bottom/Top/Both) and `PagerAlign` (Left/Center/Right) place it. This pager is **in-memory** — the Table materializes the whole `DataSource` and slices client-side, so it can't reflect a server-side total. For server-side paging, compose the Table (pass only the current page, omit `PageSize`) with a standalone fully-controlled `Pagination` (`Total`/`Current`/`PageSize` + `CurrentChanged`). The Table treats `DataSource`/`SelectedItems` as immutable parameters (reference-guarded in `OnParametersSet`) — reassign a new reference to refresh rather than mutating in place. Demoed (with a simulated server) in `/uikit`.

- **`Icon`, `Button`, `Checkbox`, `Tag` are intentionally excluded.** Modal/Popconfirm footers use native `<button>`s with the shared `wss-dialog-btn` class, not a Button component.
- **Toasts come in two variants** (identical rendering). **Scoped / Server-safe:** `IMessageService` / `INotificationService` (register via `AddWssControlsToasts()`) + `MessageContainer` / `NotificationContainer`. **Registration-free static (WASM only):** `WasmMessageService` / `WasmNotificationService` + `WasmMessageContainer` / `WasmNotificationContainer` — process-`static` state, unsafe on Server. Logic lives in `MessageService` / `NotificationService` (the `Wasm*` services are static facades over it); all four containers render the shared `MessageListView` / `NotificationListView`.
- **Overlays are keyboard- and screen-reader-accessible:** Modal/Drawer trap focus, restore it to the trigger on close, close on `Escape`, and set `role="dialog"`/`aria-modal`/`aria-labelledby`; Popover/Popconfirm triggers are focusable buttons (`Enter`/`Space` open, `Escape` closes) with `aria-haspopup`/`aria-expanded`; `Select` exposes combobox ARIA (`role`/`aria-expanded`/`aria-activedescendant`); `Pagination` is a semantic `<nav>` of `<button>`s with `aria-current`; toasts route by severity into two always-present live regions — a polite `role="status"` and an assertive `role="alert"` (errors) — so an error isn't downgraded by a politeness flip on a shared region; `Skeleton` exposes `role="status"`/`aria-busy` with a visually-hidden `LoadingText`.
- Demoed at `/uikit` (`Controls.Demo/UiKitGallery.razor`); the searchable selects appear in the main form demo (`SelectSearch` / `MultiSelect` views).

### Global Usings

The Controls project defines global usings in `Controls/GlobalUsings.cs`. The Controls.Demo project uses `_Imports.razor` for razor-file usings and explicit `using` statements in `.cs` code-behind files (since `_Imports.razor` doesn't apply to `.cs` files).

## Git Workflow

**Work directly on `master`.** This project does not use feature branches — commits land on the default branch. This overrides the Claude Code default that creates a branch first; do not run `git checkout -b` before editing.

**Commit and push often.** After each logical chunk of work that builds and tests cleanly, commit it and push to origin. Don't wait to be asked — the user prefers many small focused commits over one big end-of-session dump. Group changes into meaningful commits (one phase / one feature / one fix per commit, not one mega-commit). If a chunk leaves the build or tests broken, finish it first rather than committing a broken state. This overrides the Claude Code default that says "commit or push only when the user asks."

Never push to NuGet from an agent (see Release Workflow).

## Release Workflow

### Versioning

Version is set via `<AssemblyVersion>` in each `.csproj`. Both Controls and Controls.Demo should be kept in sync. `FileVersion` and `Version` derive from `AssemblyVersion`. Update the changelog in `README.md` when bumping versions.

**Convention: the major version tracks the latest supported .NET version, *not* semver.** Library code currently multi-targets net8/net9/net10, so we sit on `10.x.x` until net11 ships. Within that:
- **Minor** (`10.X.0`) — new features, behavioral changes, or anything a consumer might need to read about before upgrading. Bump even for technically-breaking changes that semver would call major.
- **Patch** (`10.x.X`) — pure bug fixes, internal refactors, doc tweaks.

When bumping to a new .NET major (`11.0.0`), the bump is for the .NET upgrade itself — pair it with raising `<TargetFrameworks>`.

**Bump only at publish time.** Do not bump `<AssemblyVersion>` for in-progress work — accumulate changes against the current version and bump (with the README changelog entry) only when the next NuGet release is being cut. This keeps git history clean and avoids meaningless intermediate version numbers.

**Never push to NuGet from an agent.** `dotnet nuget push` is the human's responsibility — agents may pack (`dotnet pack -c Release -o ./nupkg`) so the artifacts are ready, but never push. The user owns the API key and the publish decision.

### Publishing to NuGet

```bash
# Pack both packages
dotnet pack Controls/Controls.csproj -c Release -o ./nupkg
dotnet pack Controls.Demo/Controls.Demo.csproj -c Release -o ./nupkg

# Push to nuget.org (requires API key)
dotnet nuget push nupkg/WssBlazorControls.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <KEY>
dotnet nuget push nupkg/WssBlazorControls.Demo.<version>.nupkg --source https://api.nuget.org/v3/index.json --api-key <KEY>
```

### README.md

The README serves as both the repo landing page and the NuGet package readme (packed into `WssBlazorControls` via `<PackageReadmeFile>`). It contains installation instructions, usage examples for every control, and a changelog. Update it when adding features or bumping versions.

## Key Conventions

- Component names use `Edit` prefix (EditString, EditSelectEnum, EditRadio, etc.)
- All controls support accessibility: ARIA attributes (`aria-required`, `aria-invalid`, `aria-describedby`), fieldset/legend for groups, screen reader text
- Label preference (in order): (1) let it auto-generate from the property name when correct — the name is camel-case split, so `BirthDate` → "Birth Date"; (2) use `[DisplayName("…")]` on the model for constant labels the auto-name gets wrong; (3) use the `Label` parameter in markup only for dynamic/runtime text. Resolution precedence (highest wins): `Label` parameter → `[DisplayName]` → auto-generated property name.
- Generic components: `EditDate<T>`, `EditSelectEnum<TEnum>`, `EditRadioEnum<TEnum>`, `EditCheckedEnumList<TEnum>`
- JavaScript interop is in `Controls/wwwroot/edit-controls.js` with C# wrappers in `JsInteropEc.cs`
