# WssBlazorControls

[![NuGet Version](https://img.shields.io/nuget/v/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)

A comprehensive library of form controls for Blazor applications providing consistent, feature-rich input components with built-in validation, accessibility support, and flexible styling options.

## Features

- Rich Form Controls**: String, Number, Date, Boolean, Select, Radio, Checkbox lists, and TextArea components
- Searchable & Multi-Select**: AntDesign-style `EditSelectSearch` / `EditMultiSelect` — type-to-search, tags, virtualized dropdown
- AntDesign-style UI Kit**: dependency-free Alert, Modal, Drawer, Table, Pagination, Tooltip, Popover, Popconfirm, Skeleton, and WASM toasts
- Data Annotations Integration**: Full support for validation attributes (Required, Range, MinLength, etc.)
- Accessibility First**: ARIA attributes, screen reader support, and keyboard navigation
- Flexible Display Modes**: Edit mode and read-only views for all controls
- Consistent Styling**: CSS classes and customizable appearance
- TypeScript/JavaScript Interop**: Enhanced client-side functionality
- Cross-Platform**: Works with both Blazor Server and Blazor WebAssembly

## Installation

Install the package via NuGet Package Manager:

```bash
dotnet add package WssBlazorControls
```

Or via Package Manager Console:

```powershell
Install-Package WssBlazorControls
```

## Quick Start

1. **Add the using statement** to your `_Imports.razor`:

```razor
@using WssBlazorControls
```

2. **Include the CSS** in your `App.razor` or `index.html`:

```html
<link href="_content/WssBlazorControls/edit-controls.css" rel="stylesheet" />
<!-- Only needed if you use the AntDesign-style UI-kit controls (Select, Alert, Modal, Table, ...) -->
<link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
```

3. **Use the controls** in your Blazor components:

```razor
@using System.ComponentModel.DataAnnotations

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />
    
    <EditString @bind-Value="model.Name" 
                Label="Full Name" 
                IsRequired="true" />
    
    <EditNumber @bind-Value="model.Age" 
                Label="Age" 
                IsRequired="true" />
    
    <EditDate @bind-Value="model.BirthDate" 
              Label="Birth Date" />
    
    <EditBool @bind-Value="model.IsActive" 
              Label="Active Status" />
    
    <button type="submit">Submit</button>
    
    <ValidationSummary />
</EditForm>

@code {
    private PersonModel model = new();
    
    private void HandleSubmit()
    {
        // Handle form submission
    }
    
    public class PersonModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = "";
        
        [Required]
        [Range(1, 120)]
        public int? Age { get; set; }
        
        public DateTime? BirthDate { get; set; }
        
        public bool IsActive { get; set; } = true;
    }
}
```

## Available Controls

### Input Controls
- **`EditString`** - Text input with masking and URL support
- **`EditTextArea`** - Multi-line text input
- **`EditNumber`** - Numeric input with validation
- **`EditDate`** - Date picker component
- **`EditBool`** - Checkbox for boolean values
- **`EditBoolNullRadio`** - Three-state radio for nullable booleans

### Selection Controls
- **`EditSelect`** - Dropdown selection for objects
- **`EditSelectEnum`** - Dropdown for enum values
- **`EditSelectString`** - Dropdown for string values
- **`EditSelectSearch`** - Searchable single-select (AntDesign-style: type-to-search, clear, virtualized)
- **`EditMultiSelect`** - Multiple / tags select bound to a `List<T>` (AntDesign-style)
- **`EditRadio`** - Radio buttons for objects
- **`EditRadioEnum`** - Radio buttons for enums
- **`EditRadioString`** - Radio buttons for strings

### Multi-Selection Controls
- **`EditCheckedStringList`** - Checkbox list for strings
- **`EditCheckedEnumList`** - Checkbox list for enums

### Support Components
- **`FormLabel`** - Consistent labeling with tooltips and descriptions
- **`FieldValidationDisplay`** - Validation message display
- **`ReadOnlyValue`** - Read-only value presentation
- **`EditDisplay`** - Static label+value pair (no model binding)

#### `EditDisplay` vs `ReadOnlyValue`
Both render text in the `edit-readonly-value` style, but their use cases are different:

| | `EditDisplay` | `ReadOnlyValue` |
|---|---|---|
| **When to use** | Standalone label+value pair outside an Edit* control — e.g. a derived value like `"15.3 oz / can"` that's not bound to a model property | Always — it's rendered by the Edit* controls in read-only mode, not typically used directly by consumers |
| **Owns its label** | Yes (`Label`, `Description`, `Tooltip` parameters) | No — sits inside an Edit* control that owns the `FormLabel` |
| **Model binding** | None | None (reads `Text` after the parent has formatted the value) |
| **Validation** | None | None (the parent control's `FieldValidationDisplay` handles it) |

Reach for `EditDisplay` when you want the same visual treatment as a read-only `EditString` but without an `EditForm` / model property behind it.

### UI Kit (non-form) controls

A set of dependency-free, AntDesign-style general UI widgets (ported from `Standalone.Controls`). Unlike the `Edit*` controls these are **not** form-bound — they're plain components. They use the `wss-` CSS prefix and `--wss-*` theme tokens shipped in `wss-controls.css` (link it as shown in Quick Start). No service registration is required.

- **`Select<T>`** - The dropdown engine behind `EditSelectSearch` / `EditMultiSelect`; usable standalone (single / multiple / tags, search, virtualized)
- **`Alert`** - Contextual message banner (success / info / warning / error, closable, description)
- **`Skeleton`** - Loading placeholder with shimmer
- **`Tooltip`** - Pure-CSS hover tooltip (4 placements)
- **`Popover`** - Click-triggered popover (4 placements)
- **`Pagination`** - Controlled pager
- **`Modal`** - Dialog with `@bind-Visible`, footer, mask-close
- **`Drawer`** - Slide-in panel (4 placements)
- **`Popconfirm`** - Inline confirm popover
- **`Table<TItem>`** - Data table with `Column` / `PropertyColumn` / `ActionColumn`, row selection, paging
- **`WasmMessageService` / `WasmNotificationService`** (+ their `*Container` hosts) - Registration-free toasts, **WebAssembly only** (process-static state — do not use on Blazor Server)

> `Icon`, `Button`, `Checkbox`, and `Tag` are intentionally **not** part of this library.

## Component Features

All form controls implement the `IEditControl` interface and provide:

- **Identity Management**: `Id`, `IdPrefix` for unique identification
- **Display Control**: `IsEditMode`, `IsDisabled`, `IsHidden`
- **Labeling**: `Label`, `Description` with markup support
- **Styling**: `ContainerClass` for custom CSS
- **Validation**: `IsRequired` integration with DataAnnotations
- **Conditional Display**: `Hiding` modes and `HidingMode` enum

## Examples

### Dropdown with Enum

```razor
<EditSelectEnum @bind-Value="model.Priority" 
                Label="Priority Level" 
                IsRequired="true" />

@code {
    public enum Priority
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public class TaskModel
    {
        [Required]
        public Priority? Priority { get; set; }
    }
}
```

### Radio Button Group

```razor
<EditRadioString @bind-Value="model.Department" 
                 Label="Department"
                 Options="@departments" />

@code {
    private List<string> departments = new() 
    { 
        "Engineering", 
        "Marketing", 
        "Sales", 
        "Support" 
    };
}
```

### Checkbox List

```razor
<EditCheckedStringList @bind-Value="model.Skills" 
                       Label="Technical Skills"
                       Options="@skills" />

@code {
    private List<string> skills = new() 
    { 
        "C#", 
        "JavaScript", 
        "Blazor", 
        "ASP.NET Core" 
    };
}
```

## Styling and Customization

The library provides default styling through the included CSS file. You can customize the appearance by:

1. **Overriding CSS classes** in your own stylesheets
2. **Using ContainerClass** parameter for component-specific styling
3. **Applying custom CSS** to the `.edit-control-wrapper` class

The AntDesign-style UI-kit controls (Alert, Modal, Table, Select, ...) are themed via `--wss-*` CSS custom properties in `wss-controls.css`. They default to the AntDesign 4.x look and **bridge to your existing `--color-primary` / `--color-danger` / `--border-color`** where those are defined, so they pick up your theme automatically. Override any `--wss-*` variable to re-theme.

```razor
<EditString @bind-Value="model.Name" 
            Label="Name" 
            ContainerClass="my-custom-style" />
```

## Accessibility

WssBlazorControls is built with accessibility as a priority:

- ? **ARIA attributes** for screen readers
- ?? **Keyboard navigation** support
- ?? **Focus management** and indicators
- ?? **Semantic HTML** structure
- ?? **High contrast** color support

## Browser Support

- Modern browsers with WebAssembly support
- Designed for both Blazor Server and Blazor WebAssembly scenarios
- Compatible with .NET 8.0+

## Contributing

Contributions are welcome! Please feel free to submit issues, feature requests, or pull requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- **Documentation**: Check the demo applications in the repository
- **Issues**: Report bugs via GitHub Issues
- **Feature Requests**: Submit enhancement requests via GitHub Issues

## Changelog

### Unreleased

**New: AntDesign-style controls (ported from `Standalone.Controls`)**
- **Form selects:** `EditSelectSearch<T>` (searchable single-select) and `EditMultiSelect<T>` (multiple / tags, binds `List<T>`) — full `Edit*` controls (validation, label, read-only, `FormOptions`) backed by a new dependency-free, virtualized dropdown engine (`Select<T>`). They sit **alongside** the existing `EditSelect` / `EditSelectEnum` / `EditSelectString`, which are unchanged.
- **UI kit (non-form):** `Select<T>`, `Alert`, `Skeleton`, `Tooltip`, `Popover`, `Pagination`, `Modal`, `Drawer`, `Popconfirm`, `Table<TItem>` (+ `Column` / `PropertyColumn` / `ActionColumn`), and WASM-only `WasmMessageService` / `WasmNotificationService` (+ their container components). `Icon`, `Button`, `Checkbox`, and `Tag` were intentionally excluded.
- **New stylesheet:** these controls use the `wss-` class prefix and `--wss-*` theme tokens shipped in `wss-controls.css`. Add a second link alongside `edit-controls.css`:
  ```html
  <link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
  ```
  Tokens default to the AntDesign 4.x look and bridge to your existing `--color-*` / `--border-color` where present. The Select keyboard helper ships as an RCL JS module at `_content/WssBlazorControls/wss-select.js` (auto-imported, degrades gracefully).
- No service registration required (consistent with the rest of the library).

**Breaking dependency change**
- Removed `Microsoft.AspNetCore.Components.DataAnnotations.Validation` (3.2.0-rc1) from the `WssBlazorControls` package — the library itself never used it. Consumers who use `<ObjectGraphDataAnnotationsValidator>` or the `[ValidateComplexType]` attribute for nested-object validation must now add the package to their own project:
  ```bash
  dotnet add package Microsoft.AspNetCore.Components.DataAnnotations.Validation --version 3.2.0-rc1.20223.4
  ```
  This eliminates the prerelease-dependency warning that previously bled through to consumer builds.

**Behavior**
- Validation messages now respect the `Label` parameter override on every control. Previously only `EditCheckedStringList` and `EditCheckedEnumList` passed `Label` through to `FieldValidationDisplay`; the other 12 controls would still derive the label from the model's attribute. Now if you set `<EditString Label="Username" ... />`, the validation message shows "Username is required" instead of falling back to the property name.
- `EditSelectString` `<option>` elements now render the `title` tooltip (consistent with `EditSelectEnum`).
- Cosmetic: `EditDate`'s `ReadOnlyValue` now uses `@_id` / `@_isRequired` like every other control.

**Build / packaging**
- `<GeneratePackageOnBuild>` is now scoped to `Configuration == Release`. Dev / inner-loop builds no longer regenerate `.nupkg` files on every save — `dotnet pack -c Release -o ./nupkg` continues to produce them on demand.
- Package now ships with a 128×128 icon (`icon.png`, white "W" on Blazor purple). Visible in NuGet listings and Visual Studio's Manage NuGet Packages dialog.

**New shared CSS class**
- `.edit-input` is now applied to every editable element (`<input>`, `<textarea>`, `<InputSelect>`, `<InputDate>`) across `EditString`, `EditNumber`, `EditDate`, `EditTextArea`, `EditSelect`, `EditSelectString`, `EditSelectEnum`, plus the "Other" text inputs in `EditRadioString` / `EditRadioEnum`. The bundled `edit-controls.css` ships an empty rule — consumers can now style every editable element with one selector instead of writing per-element CSS for `input` / `textarea` / `select` separately. Per-control classes (`.edit-string-input`, `.edit-textarea-input`, `.edit-select-select`, etc.) remain available for fine-tuning.

**Internal**
- `HidingMode`: dropped the meaningless explicit `= 1, 2, 3, 4, 5` numeric values. Default is now `0` (`None`) which matches the `?? HidingMode.None` fallback already in every control. Consumers don't notice unless they were persisting the enum as an int — in which case existing values shift down by 1.
- `ValidationHelper`: replaced the brittle `message.Split(' ')` + hardcoded array-index parsing of Range messages with a compiled regex. Now tolerates multi-word field names (`"Order Total"`) and small format variations. Type-min/max sentinel detection moved into `HashSet<string>` lookups instead of a long `||` chain.

**Architecture: `EditControlBase<TValue>`**
- 11 of 14 controls now inherit a single `EditControlBase<TValue> : InputBase<TValue>, IEditControl` instead of inheriting one of Microsoft's specialized `Input*` classes (InputText / InputNumber / InputDate / InputCheckbox / InputSelect / etc.). The base hoists every IEditControl parameter, both cascading parameters, the protected derived state (`_id`, `_isRequired`, `_attributes`, `_fieldIdentifier`), and the `ShowEditor` / `ShouldHideLabel` checks — so each derived control's `.razor.cs` shrinks to just its component-specific parameters + parser + helpers. Net ~430 lines removed across the 11 controls.
- The string-input/textarea/number/date/select parsing logic that Microsoft's `Input*` classes used to provide is now ported into each control (typically a 5-15 line `TryParseValueFromString` override that delegates to `BindConverter`). Behavior is preserved — the new parsers route through the same `BindConverter` Microsoft uses internally.
- `EditCheckedStringList` and `EditCheckedEnumList` migrated to a sibling `EditControlListBase<TItem>` (different shape — binds `List<TItem>` instead of a scalar). The `SetAsync(item)` rename to `ToggleAsync(item)` is the only consumer-facing surface change.
- `EditRadio` is the one remaining control still on Microsoft's `InputRadioGroup<T>` — it depends on the cascading-context plumbing that `<InputRadio>` children consume, and replacing the group requires also replacing the public `<InputRadio>` API. Intentional.
- `_Imports.razor` now exposes `Microsoft.AspNetCore.Components.Forms` and `Controls.Helpers` so individual razor files no longer need per-file `@using` directives for `<InputRadioGroup>` / `<InputRadio>` / `.ToId()` / etc.

**Tests**
- `FormTesting/FormTesting.Client.Tests/` (xUnit + bUnit, net10) — 57 tests covering the helpers (`EnumHelpers` cache + attribute precedence, `AttributesHelper.GetId` / `GetLabelText` / `GetMinAndMaxLengths`, `EditControlInit`, `ValidationHelper` regex parsing) and bUnit smoke tests for the controls (rendered DOM, ARIA attributes, edit/read-only switching, EditBool's new `RenderAsCheckboxWhenReadOnly` opt-in). Run with `dotnet test FormTesting/FormTesting.Client.Tests/FormTesting.Client.Tests.csproj`.

### 10.1.0

**Behavioral changes** *(read before upgrading)*
- `EditBool`: read-only mode now renders `ReadOnlyValue` with the new `TrueText` / `FalseText` parameters (default `"Yes"` / `"No"`), matching every other control. Set `RenderAsCheckboxWhenReadOnly="true"` to keep the legacy disabled-checkbox display.
- `EditString`: `aria-required` now reflects the actual required state instead of being hard-coded to `"true"`.

**New CSS class — required for the invalid-icon overlay**
- `.edit-input-with-icon` wraps `<input>` / `<textarea>` / `<InputDate>` together with the optional red-X invalid icon in `EditString`, `EditNumber`, `EditDate`, `EditTextArea`. The bundled `edit-controls.css` ships an empty hook (the icon overlays the input via `.edit-icon-invalid`'s negative margin and needs no positioning here). If you have your own stylesheet, no changes are required unless you want to adjust the input row's layout.

**New parameters**
- `EditBool.TrueText` (default `"Yes"`)
- `EditBool.FalseText` (default `"No"`)
- `EditBool.RenderAsCheckboxWhenReadOnly` (default `false`)

**New components / helpers**
- `<InvalidIcon CssClass="..." />` — reusable red-X SVG, conditional on the host's `CssClass` containing `"invalid"`.
- `EditControlInit` (in `Controls.Helpers`) — static helper that consolidates the `OnInitialized` setup and the `ShowEditor` / `ShouldHideLabel` checks every control was duplicating.

**Markup consistency**
- `EditSelectEnum` switched from `@bind:get` / `@bind:set` to `<InputSelect @bind-Value=...>` so it matches `EditSelect` / `EditSelectString`.
- `EditBoolNullRadio` radio inputs now carry `aria-required`, `aria-invalid`, `aria-describedby`, and `aria-errormessage`.
- `aria-invalid` is now rendered consistently on every editable control.
- `.ToId()` is now applied to enum option `id`s in `EditSelectEnum` and `EditRadioEnum` — fixes invalid HTML ids when an enum's display name contains spaces or punctuation.
- The red-X invalid icon (previously only on `EditString`) now appears on `EditNumber`, `EditDate`, and `EditTextArea` as well.

**Performance**
- `EnumHelpers._nameCache` is now a thread-safe `ConcurrentDictionary<(Type, string), string>` keyed by enum type — fixes potential cross-type collisions and removes a thread-safety hazard on pre-rendering.
- `EnumHelpers.GetName` now honors both `[EnumDisplayName]` *and* `[Display(Name=...)]`. Previously `[Display]` only affected sort order and `[EnumDisplayName]` only affected display, so the two could disagree.
- The reflection-heavy enum sort blocks in `EditSelectEnum` / `EditRadioEnum` / `EditCheckedEnumList` collapsed to `OrderBy(x => x.GetName())` and benefit from the cache.

**Bug fixes**
- Fixed package description typo (`HierarchyAndEmployeeRecordproviding` artifact).
- Removed stray `IsRequiredChanged` parameter that existed only on `EditRadioEnum`.
- `EditCheckedStringList` was silently dropping the `IdPrefix` parameter (`null` was being passed instead). Now consistent with every other control.
- `EditBoolNullRadio` false-radio's `class` attribute incorrectly used `@ContainerClass` instead of `@CssClass`.
- `focusFirstInvalidField` (JS) now correctly handles invalid wrapper elements that aren't form fields, includes `<select>`, and guards `.select()` for input types that don't support it.

**Refactoring (internal)**
- All 14 controls now call `EditControlInit.Init(...)` in `OnInitialized` instead of duplicating the same 4 lines.
- All controls use `EditControlInit.ShowEditor(...)` and `EditControlInit.ShouldHideLabel(...)` for the visibility checks.
- JavaScript helpers namespaced under `window.WssEditControls.*`. Legacy `window.focusFirstInvalidField` / `window.log` / etc. are still exposed for back-compat — safe to migrate at your own pace.
- `JsInteropEc.FocusFirstInvalidField` uses `Task.Yield()` instead of `Task.Delay(1)`.
- `FormLabel._isRequired` changed from `string` (`"true"`/`"false"`) to `bool`.
- `IEditControl.IsDisabled` doc comment fixed (was `"Not used"` despite being used by every control).
- Deleted dead `ExampleJsInterop.cs` template code.
- Removed unused `EditCheckedStringList.hasError` and `ReadOnlyValue._emptyValue` fields.
- Build warnings reduced from 87 → 57.

### 10.0.7
- EditString: Add `Autocomplete` parameter (defaults to `"one-time-code"`) to prevent browser extensions and autofill from intercepting Blazor input events on fields with IDs containing keywords like "email"

### 10.0.2
- Support .net 8,9,10

### 10.0.1
- Upgrade to .net 10
- Add the ability to hide the required star within FormOptions
- Changed editControls.js to edit-controls.js

### 1.13.8
- Exposed xmldoc comments

### 1.13.7
- refactoring

### 1.13.6
- Move the star for non-legends to the left.

### 1.13.5
- Enable tooltips through markup
- Move the required star to the left of the label

### 1.13.4
- EditDate and other controls. Add a null value string to display when the value is null, such as a dash instead of blank space.
- IsRequired parameter on all controls. When set forces the “edit-label-required-star” to show up without being required in the DataAnnotations.
- Accessibility updates for EditCheckedStringList

### 1.13.3
- Current stable release
- Full feature set with comprehensive validation support

### 1.0.13.2
- EditCheckedListEnum
- 
### 1.0.13.1
- Rename icons to have edit- in front of the current names
  - .icon-eye => .edit-icon-eye
 - Icon-invalid, icon-eye-invisible
- EditSelectEnum no longer requires specifying the type.
- Tooltips exist on the controls
 - Only from attributes right now [Tooltip(“My cool tooltip”)
 - 
### 1.0.12.11
- Import js into application in App.razor or index.html
   -     <script src="_content/WssBlazorControls/editControls.js"></script>
  - This is to add the functionality of “When submit is clicked, but invalid, scroll to the first input that is invalid.
  - Use JsInteropEc to access js methods. Use JsInteropEc.FocusFirstInvalidField() when there are validation errors while submitting.
- EditCheckedStringList
  - Error message shows up on each checkbox

### 1.0.12.10
- IsRequired parameter on all controls. When set forces the “edit-label-required-star” to show up without being required in the DataAnnotations.
- Accessibility updates for EditCheckedStringList
- 
### 1.0.12.x
- moved away from utilizing bootstrap css classes such as form-group to using classes that start with edit- to avoid conflicts with other libraries
- New Features
 - IsHidden to hide controls withougt wrapping them in an if statement
 - Hiding allows hiding controls based on their own property for [Never, WhenReadonlyAndNull, WhenReadonly, etc.]
   - This also exists within FormOptions, so the hiding can be controlled over a large group of controls.
- Control Changes
 - EditRadio and EditCheckedList
   - Change parameter from HasHorizontalButtons -> IsHorizontal
   - Removed the need for "Type" parameter, now uses the type of the value passed in.
 - EditSelectEnum
   - Removed the need for "Type" parameter, now uses the type of the value passed in.
- New Controls
  - EditBoolNullRadio

