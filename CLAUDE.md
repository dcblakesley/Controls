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

There are no automated tests. Manual testing is done via the FormTesting app.

## Project Structure

- **Controls/** — Main Razor Class Library (`WssBlazorControls` NuGet package). Multi-targets net8.0, net9.0, net10.0.
- **Controls.Demo/** — Demo components library (`WssBlazorControls.Demo` NuGet package). References Controls. Same multi-targeting.
- **FormTesting/FormTesting/** — Blazor Server host (net10.0).
- **FormTesting/FormTesting.Client/** — Blazor WebAssembly client that references both Controls and Controls.Demo.

## Architecture

### Component Pattern

Every edit control (EditString, EditNumber, EditDate, EditBool, EditSelect*, EditRadio*, EditChecked*) follows the same structure:

1. **Inherits** from a Blazor `InputBase<T>` variant
2. **Implements** `IEditControl` (common properties: Label, Description, Tooltip, IsEditMode, IsHidden, IsDisabled, etc.)
3. **Receives** `FormOptions` and `FormGroupOptions` as `[CascadingParameter]`s
4. **Split** into `.razor` (markup) + `.razor.cs` (code-behind)
5. **Wraps** content in a `div.edit-control-wrapper` containing `<FormLabel>`, the input element, and `<FieldValidationDisplay>`
6. **Supports** edit/read-only toggle — edit mode shows the input; read-only mode shows `<ReadOnlyValue>`

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

All CSS classes use the `edit-` prefix (defined in `Controls/wwwroot/edit-controls.css`). Key classes: `edit-control-wrapper`, `edit-label`, `edit-label-required-star`, `edit-validation-message`, `edit-readonly-value`, `edit-tooltip-*`. Component-scoped CSS uses Blazor CSS isolation (`.razor.css` files).

### Global Usings

The Controls project defines global usings in `Controls/GlobalUsings.cs`. The Controls.Demo project uses `_Imports.razor` for razor-file usings and explicit `using` statements in `.cs` code-behind files (since `_Imports.razor` doesn't apply to `.cs` files).

## Release Workflow

### Versioning

Version is set via `<AssemblyVersion>` in each `.csproj`. Both Controls and Controls.Demo should be kept in sync. `FileVersion` and `Version` derive from `AssemblyVersion`. Update the changelog in `README.md` when bumping versions.

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
- Label resolution chain: auto-generated from property name → `[DisplayName]` attribute → `Label` parameter
- Generic components: `EditDate<T>`, `EditSelectEnum<TEnum>`, `EditRadioEnum<TEnum>`, `EditCheckedEnumList<TEnum>`
- JavaScript interop is in `Controls/wwwroot/edit-controls.js` with C# wrappers in `JsInteropEc.cs`
