# CLAUDE.md

Guidance for Claude Code in this repository. This file carries only what is *not* derivable from the source — commands, policies, and constraints. Do not re-add API surveys, type/file inventories, or per-control behavior descriptions here: the source is authoritative for internals, and the `edit-controls` skill is the consumer-facing reference.

## What this repo is

`WssBlazorControls` — a Razor Class Library of Blazor form controls (`Edit*` prefix) plus an AntDesign-style non-form UI kit (`wss-` prefix), published to NuGet and consumed by every Blazor app in this workspace. Targets net10.0 only. **Consumers get changes only via a new published package** — editing this repo does not affect them until a release is cut.

## Build and Test

```bash
dotnet build FormTesting.sln                                                      # whole solution
dotnet run --project FormTesting/FormTesting/FormTesting.csproj                   # demo host (Blazor Server + WASM)
dotnet test FormTesting/FormTesting.Client.Tests/FormTesting.Client.Tests.csproj  # bUnit suite
dotnet test FormTesting/FormTesting.Client.E2ETests/FormTesting.Client.E2ETests.csproj  # Playwright e2e
dotnet pack Controls/Controls.csproj -c Release -o ./nupkg                        # pack (also Controls.Demo)
```

CI (`.github/workflows/ci.yml`) runs the Release build, bUnit suite, pack, and the e2e suite on `windows-latest` (visual baselines are Windows-rendered). The library projects set `TreatWarningsAsErrors` — a new warning is a build break.

- **Add bUnit tests alongside any non-trivial helper or control change** (`FormTesting/FormTesting.Client.Tests/`). One e2e test class per control is the convention in `FormTesting/FormTesting.Client.E2ETests/`.
- **JS interop changes need e2e coverage, not bUnit** — bUnit does not execute JavaScript.
- **Run one test project at a time.** The e2e `AppFixture` launches the FormTesting host out-of-process, so a parallel `dotnet test`/`dotnet run` collides on the host DLLs. Set `PWTEST_HEADED=1` to watch the browser locally.

### Visual regression

Baseline PNGs in `FormTesting/FormTesting.Client.E2ETests/Snapshots/` are committed. After an intentional UI change, regenerate with `UPDATE_SNAPSHOTS=1 dotnet test ...E2ETests.csproj` and commit the PNGs. On failure, gitignored `*-actual.png` / `*-diff.png` land next to the baseline. First-time setup: `pwsh FormTesting/FormTesting.Client.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium` (the MSBuild target attempts this automatically after first build).

### Trimming / AOT

`Controls.csproj` sets `IsAotCompatible`, so the trim/AOT/single-file analyzers run on every build and any new IL2xxx/IL3xxx is a build break. When adding code: no `Enum.GetValues(Type)` (use `EnumHelpers.GetValues<T>`), no `MakeGenericType`/`MakeGenericMethod`, and by-name reflection needs `[DynamicallyAccessedMembers]` or a named method with a justified `[UnconditionalSuppressMessage]` (pattern: `FieldValidationDisplay.GetPropertyTypeName` — the justification must say why the target is rooted and what the graceful fallback is). Generic parameters flowing into `BindConverter.TryConvertTo` or framework `Input*` components need `[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]`. Controls.Demo is intentionally *not* trimmable (its demo models hit a consumer-side non-issue IL2026).

To re-verify runtime behavior under full trimming:

```bash
dotnet publish FormTesting/FormTesting/FormTesting.csproj -c Release -o <dir> -p:TrimMode=full -p:WssFullTrimTest=true
FORMTESTING_E2E_APP=<dir>/FormTesting.dll dotnet test FormTesting/FormTesting.Client.E2ETests/FormTesting.Client.E2ETests.csproj
```

(`WssFullTrimTest` roots the client assembly — under `TrimMode=full`, Blazor's reflection-based route discovery otherwise lets the trimmer delete the whole app.)

## Layout

- **Controls/** — the library. Form `Edit*` controls at the root; `Controls/Select/` (the `Select<T>` engine + searchable form selects); `Controls/UiKit/` (non-form widgets); `Controls/Helpers/`.
- **Controls.Demo/** — demo components (`WssBlazorControls.Demo` package). `UiKitGallery.razor` serves `/uikit`.
- **FormTesting/** — Blazor Server host + WASM client + the two test projects.

## Design constraint: consumers are often micro-frontends (MFEs)

Most consuming apps are MFEs — independently-built Blazor apps composed into one host page, where the app team does **not** own the DI composition root or the process. That rules out two configuration styles for library features:

- **DI-registered options** (`builder.Services.Configure<...>`) — an MFE can't reliably add or override registrations in a shell it doesn't own.
- **Process-wide statics** — on Blazor Server one process serves every circuit/user, and MFEs sharing a runtime share the static.

Prefer **render-tree-scoped configuration** (cascading values/components): each MFE owns its root component regardless of hosting, so the render tree is the one boundary that always maps onto app/MFE/circuit. `FormDefaults` (resolution: instance → cascaded `FormDefaults` → legacy static) is the reference example. Service-based features that genuinely need DI (e.g. the scoped toast services) stay optional, with a registration-free alternative where feasible — which is why the toasts ship in two variants and the static `Wasm*` variant is documented as WASM-only.

## Architecture conventions

Read an existing control before writing one — the pattern is uniform and the source is the spec. Non-obvious rules:

- **Base class selection:** scalar controls inherit `EditControlBase<T>` (an `InputBase<T>`); list-bound controls inherit `EditControlListBase<T>` (a `ComponentBase` binding `List<T>`). Exceptions: `EditRadio<T>` must inherit `InputRadioGroup<T>` (its child `InputRadio`s need the context), `EditDisplay` is a plain `ComponentBase` (no field). See the `edit-controls` skill's `architecture.md` for the why behind the init pattern and the new-control checklist.
- **Every control declares an inert `Field` parameter marked `[Obsolete(error: true)]`** — a compile-time guard so stale `Field="..."` markup fails the build instead of throwing at first render. Copy the stub verbatim into new controls; `@bind-Value` alone supplies `ValueExpression`.
- **Global usings:** `Controls/GlobalUsings.cs` for the library; Controls.Demo uses `_Imports.razor` plus explicit `using`s in `.cs` files (`_Imports.razor` doesn't apply to code-behind).
- **UI-kit conventions:** namespace `Controls` (pin `@namespace Controls` on each `.razor`); new components capture unmatched attributes via the internal `AttributeSplat` helper (`class`/`style` hand-merged, splat first so explicit attributes win) onto the root element — never onto an element whose inline style is JS-owned. `Icon`, `Button`, `Checkbox`, `Tag` are intentionally excluded from the kit — dialog footers use native `<button>` + `wss-dialog-btn`.
- **JS interop must degrade gracefully.** The RCL modules (`wss-select.js`, `wss-overlay.js`, `wss-table.js`, `edit-controls.js`) import lazily and every JS-dependent behavior needs a no-JS fallback (prerender/tests) — CSS-default placement, plain checkbox, etc.

## CSS

**Global stylesheets, not CSS isolation.** No consumer links a scoped-CSS bundle, so `.razor.css` files never load — put all control CSS in `Controls/wwwroot/edit-controls.css` (form controls, `edit-` prefix) or `wss-controls.css` (UI kit, `wss-` prefix + `--wss-*` tokens).

Theming contract (documented in README "Styling and Customization"): `--wss-*`/`--edit-*` tokens are overridable at any scope, with derived hover/shadow/focus states following because they derive at the usage site, never at `:root` (a `var()` inside a custom property substitutes where declared); the generic `--color-*` bridge resolves at `:root` only. Preserve this when adding styles.

## Git Workflow

**Work directly on `master`.** No feature branches — this overrides the Claude Code default; do not `git checkout -b`.

**Commit and push often.** Each logical chunk that builds and tests cleanly gets its own focused commit, pushed to origin without being asked — this overrides the Claude Code default of waiting for an ask. Don't commit a broken state; finish the chunk first.

## Release Workflow

- **Versioning:** `<AssemblyVersion>` in both `.csproj`s, kept in sync; `FileVersion`/`Version` derive from it. The major tracks the latest supported .NET version, *not* semver (net10.0 → `10.x.x` until net11). Minor (`10.X.0`) = features, behavioral changes, anything a consumer should read about before upgrading — including semver-major breaks and supported-platform changes. Patch = pure fixes/refactors/docs.
- **Bump only at publish time.** Accumulate work against the current version; bump (with a README changelog entry) only when a NuGet release is being cut.
- **Never push to NuGet from an agent.** Packing is fine; `dotnet nuget push` is the human's call (they own the API key).
- **README.md is also the NuGet package readme** (`<PackageReadmeFile>`) — update its examples and changelog when features or versions change.

### `edit-controls` skill is part of the deliverable

`~/.claude/skills/edit-controls/` is the usage reference other repos' agents read instead of this source. Whenever a change touches a control's public API, parameters, behavior, or a documented convention, update the matching skill file(s) in the same commit — do not defer to a cleanup pass. A separately-formatted export for non-Claude agents lives at the workspace root (`C:\Repos\.github\skills\edit-controls\` — `skill.json` + monolithic docs, not a file-for-file mirror, and not in any git repo); keep it in sync for API-affecting changes.

## Key Conventions

- Label preference: (1) let it auto-generate from the property name (camel-case split: `BirthDate` → "Birth Date"); (2) `[DisplayName]` on the model for constant labels the auto-name gets wrong; (3) the `Label` parameter only for dynamic/runtime text. Precedence (highest wins): `Label` param → `[DisplayName]` → `[EnumDisplayName]` → `[Display(Name)]` → auto-generated.
- All controls carry ARIA wiring (`aria-required`, `aria-invalid`, `aria-describedby`, fieldset/legend for groups) — preserve it when touching markup.
