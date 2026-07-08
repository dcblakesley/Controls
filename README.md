# WssBlazorControls

[![NuGet Version](https://img.shields.io/nuget/v/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)

A comprehensive library of form controls for Blazor applications providing consistent, feature-rich input components with built-in validation, accessibility support, and flexible styling options.

## Features

- **Rich Form Controls**: String, Number, Date, Boolean, Select, Radio, Checkbox lists, and TextArea components
- **Searchable & Multi-Select**: AntDesign-style `EditSelectSearch` / `EditMultiSelect` — type-to-search, tags, virtualized dropdown
- **AntDesign-style UI Kit**: dependency-free Alert, Modal, Drawer, Table, Pagination, Popover, Popconfirm, Skeleton, and toasts
- **Data Annotations Integration**: Full support for validation attributes (Required, Range, MinLength, etc.)
- **Validator-Agnostic Core**: messages, invalid-state ARIA, and the validation summary work with any `EditContext` validator; a form-level `RequiredResolver` bridges required-star/`aria-required` for FluentValidation and other stacks
- **Accessibility First**: ARIA attributes, screen reader support, and keyboard navigation
- **Flexible Display Modes**: Edit mode and read-only views for all controls
- **Consistent Styling**: CSS classes and customizable appearance
- **TypeScript/JavaScript Interop**: Enhanced client-side functionality
- **Cross-Platform**: Works with both Blazor Server and Blazor WebAssembly

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
@using Controls
```

2. **Include the CSS** in your `App.razor` or `index.html`:

```html
<link href="_content/WssBlazorControls/edit-controls.css" rel="stylesheet" />
<!-- Only needed if you use the AntDesign-style UI-kit controls (Select, Alert, Modal, Table, ...) -->
<link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
```

3. **Include the JS helpers** (next to your Blazor script tag):

```html
<script src="_content/WssBlazorControls/edit-controls.js"></script>
```

   Required by `JsInteropEc.FocusFirstInvalidField` (focus the first invalid field on a failed
   submit). The UI-kit controls load their own JS modules lazily — no extra tags needed for them.

4. **Use the controls** in your Blazor components:

```razor
@using System.ComponentModel
@using System.ComponentModel.DataAnnotations

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />

    @* No Label needed: "Name", "Age", and "Birth Date" are derived correctly
       from the property names, and the required star comes from [Required]. *@
    <EditString @bind-Value="model.Name"     Field="@(() => model.Name)" />
    <EditNumber @bind-Value="model.Age"       Field="@(() => model.Age)" />
    <EditDate   @bind-Value="model.BirthDate" Field="@(() => model.BirthDate)" />

    @* "Is Active" would be wrong, so the constant label lives on the model. *@
    <EditBool   @bind-Value="model.IsActive"  Field="@(() => model.IsActive)" />

    @* Label is set in markup only because the text is dynamic at runtime. *@
    <EditString @bind-Value="model.Answer"    Field="@(() => model.Answer)" Label="@_currentQuestion" />

    <button type="submit">Submit</button>

    <ValidationSummary />
</EditForm>

@code {
    private string _currentQuestion = "Your favorite color?";
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

        [DisplayName("Active Status")]
        public bool IsActive { get; set; } = true;

        public string? Answer { get; set; }
    }
}
```

### Labeling: how to choose

Pick the label source by how the text is determined, in this order of preference:

1. **Let the label auto-generate** from the property name when that's already correct. The name is split on camel-case, so `BirthDate` → "Birth Date". Don't set anything — no `Label`, no attribute, and no manual `<label>`.
2. **Put constant labels on the model** with `[DisplayName("...")]` when the auto-generated text is wrong or awkward (e.g. `IsActive` → "Is Active", but you want "Active Status"). This keeps the label next to the data it describes and reused everywhere the property is rendered.
3. **Set the `Label` parameter in markup only for dynamic / runtime text** — a label that varies per instance or isn't known at compile time. A constant string in `Label="..."` is the wrong tier; move it to `[DisplayName]`.

Under the hood the highest-priority source wins: the `Label` parameter overrides `[DisplayName]`, which overrides the auto-generated property name. Preferring tier 1, then 2, then 3 keeps you from reaching for a higher-priority source than the text actually needs.

## Available Controls

### Input Controls
- **`EditString`** - Text input with masking and URL support
- **`EditTextArea`** - Multi-line text input
- **`EditNumber`** - Numeric input with validation
- **`EditDate`** - Date picker component
- **`EditBool`** - Checkbox for boolean values
- **`EditBoolNullRadio`** - Three-state radio for nullable booleans
- **`EditFile`** - Multi-file upload bound to a `List<IBrowserFile>` (drag-and-drop + click-to-browse, extension filtering, per-file size cap, aggregate size cap, optional max count)

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
- **`ValidationView`** - Validation summary that renders each error as a link jumping to its field
- **`ReadOnlyValue`** - Read-only value presentation
- **`EditDisplay`** - Static label+value pair (no model binding)
- **`FormDefaults`** - Render-tree-scoped defaults for the controls (see below)

#### `FormDefaults`

Wrap your app root (or each micro-frontend's root) in `FormDefaults` to set control defaults for every form underneath it:

```razor
<FormDefaults IsRequiredStarHidden="true" ShowFieldNameInValidation="false">
    <Router AppAssembly="@typeof(App).Assembly">...</Router>
</FormDefaults>
```

Resolution per setting (highest wins): the form's `FormOptions` instance value → the cascaded `FormDefaults` → the static `FormOptions.Default*` property. Prefer `FormDefaults` over the statics: the statics are process-wide, so on Blazor Server they're shared by every user/circuit, and when several MFEs share one runtime they're shared across MFEs. `FormDefaults` scopes to the render tree, which matches app/MFE/circuit boundaries. It's intended as set-once root configuration — the cascade is registered as fixed, so runtime changes to its parameters don't propagate.

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
- **`Skeleton`** - Loading placeholder with shimmer; announces `role="status"` / `aria-busy` with a visually-hidden `LoadingText` (default `"Loading"`) for screen readers
- **`Popover`** - Click-triggered popover (4 placements)
- **`Pagination`** - Controlled pager
- **`Modal`** - Dialog with `@bind-Visible`, footer, mask-close
- **`Drawer`** - Slide-in panel (4 placements)
- **`Popconfirm`** - Inline confirm popover
- **`Table<TItem>`** - Data table with `Column` / `PropertyColumn` / `ActionColumn`, row selection, paging (pager placement via `PagerPosition` = Top/Bottom/Both and alignment via `PagerAlign`), and column sorting (`Sortable="true"` on a `PropertyColumn` — non-comparable types degrade to non-sortable; or a `SortBy` comparison on any column). Columns may be conditionally rendered (`@if`)
- **Toasts & notifications** - two paths with identical rendering: **scoped / Server-safe** (`IMessageService` / `INotificationService` via `builder.Services.AddWssControlsToasts()` + `<MessageContainer />` / `<NotificationContainer />`), or **registration-free static for WASM** (`WasmMessageService` / `WasmNotificationService` + `<WasmMessageContainer />` / `<WasmNotificationContainer />`). On Blazor Server use the scoped path — the static `Wasm*` services hold process-static state that would bleed across users.

> `Icon`, `Button`, `Checkbox`, and `Tag` are intentionally **not** part of this library.

### Server-side paging (`Table`)

The `Table`'s built-in pager (`PageSize`) is **in-memory** — it materializes the whole `DataSource` and slices it client-side, so it can't reflect a server-side total. For server-side paging, compose the `Table` with the standalone, fully-controlled `Pagination`: give the `Table` only the current page (omit `PageSize` so it renders exactly what you pass), and drive a `Pagination` yourself.

```razor
<Table TItem="Row" DataSource="_pageRows">
    <PropertyColumn TItem="Row" TProp="int" Title="Id" Property="@(r => r.Id)" />
    <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)" />
</Table>

<div style="display:flex; justify-content:flex-end; margin-top:16px;">
    <Pagination Total="_total" PageSize="PageSize" Current="_page" CurrentChanged="GoToPageAsync" />
</div>

@code {
    const int PageSize = 20;
    List<Row> _pageRows = new();
    int _total, _page = 1;

    protected override Task OnInitializedAsync() => GoToPageAsync(1);

    async Task GoToPageAsync(int page)
    {
        _page = page;
        var result = await Api.GetRows(page, PageSize /*, sortField, sortDir */);
        _pageRows = result.Items.ToList(); // a NEW reference — the Table only re-copies when DataSource changes ref
        _total    = result.TotalCount;     // the server's overall count drives the pager
    }
}
```

`Pagination` is a controlled component (`Total` / `Current` / `PageSize` + `CurrentChanged`), so it shows the correct page count from the server total and raises `CurrentChanged` when the user picks a page. Handle **sorting** the same way — pass the sort field/direction into your request rather than using the `Table`'s built-in `Sortable`, which only orders the page already loaded. A runnable example (with a simulated server) is in the `/uikit` gallery.

## Component Features

All form controls implement the `IEditControl` interface and provide:

- **Identity Management**: `Id`, `IdPrefix` for unique identification
- **Display Control**: `IsEditMode`, `IsDisabled`, `IsHidden`
- **Labeling**: auto-generated from the property name, `[DisplayName]` for constant labels, or the `Label` parameter for dynamic text — see [Labeling: how to choose](#labeling-how-to-choose). `Description` supports markup.
- **Styling**: `ContainerClass` for custom CSS
- **Validation**: required-ness from `[Required]`, the three-state `IsRequired` parameter, or `FormOptions.RequiredResolver` — see [Validation stacks](#validation-stacks-dataannotations-fluentvalidation-custom)
- **Conditional Display**: `Hiding` modes and `HidingMode` enum

## Validation stacks (DataAnnotations, FluentValidation, custom)

The runtime validation plumbing is **validator-agnostic**: validation messages, `aria-invalid`, `aria-errormessage`, the invalid icon/red styling, and the `ValidationView` summary all read from the cascading `EditContext`, so anything that writes a `ValidationMessageStore` (DataAnnotations, [Blazored.FluentValidation](https://github.com/Blazored/FluentValidation), a hand-rolled validator) works out of the box. Labels are also independent of the validation stack — `[DisplayName]`/`[Display]` and the auto-generated property-name fallback keep working.

What *is* DataAnnotations-specific is required-ness discovery (the required star and `aria-required` come from reflecting `[Required]` off the model) and the short-message rewrite (only the stock .NET DataAnnotations message templates are rewritten — e.g. "The X field is required." → "Required"; messages from other validators display verbatim, which is normally what you want since FluentValidation's defaults are already human-readable).

Required-ness resolves per control in this order:

1. **`IsRequired` parameter** (three-state `bool?`) — when explicitly set it wins outright: `true` forces the star/`aria-required` on (e.g. a RequiredIf condition that's currently active), `false` forces them off (e.g. a `RequiredAttribute`-derived conditional whose condition is off, which would otherwise show a permanent star).
2. **`[Required]` attribute** on the model property.
3. **`FormOptions.RequiredResolver`** — a form-level `Func<FieldIdentifier, bool>` for stacks that don't use attributes.

### FluentValidation bridge

Build the resolver once from your validator's own rules, so the star, `aria-required`, and the messages all share one source of truth:

```razor
<EditForm Model="model">
    <FluentValidationValidator />  @* Blazored.FluentValidation *@
    <CascadingValue Value="_formOptions">
        <EditString @bind-Value="model.Name" Field="@(() => model.Name)" />
        ...
    </CascadingValue>
</EditForm>

@code {
    FormOptions _formOptions = new();

    protected override void OnInitialized()
    {
        // Fields with a NotNull/NotEmpty rule are "required" — no [Required] attributes needed.
        var required = new PersonValidator().CreateDescriptor()
            .GetMembersWithValidators()
            .Where(m => m.Any(v => v.Validator is INotNullValidator or INotEmptyValidator))
            .Select(m => m.Key)
            .ToHashSet();
        _formOptions.RequiredResolver = f => required.Contains(f.FieldName);
    }
}
```

The resolver is keyed by `FieldIdentifier`, so if two nested objects have same-named properties, compare `f.Model` too instead of just the field name. Set the resolver before the form renders — controls consult it on init and on parameter changes.

Two caveats for mixed estates:

- `ShowFieldNameInValidation="false"` (the short "Required"-style messages) only affects rewritten DataAnnotations messages; FluentValidation messages always embed their own property name.
- For **nested models**, plain `DataAnnotationsValidator` validates nested fields on edit but *skips them on submit* — use `ObjectGraphDataAnnotationsValidator` + `[ValidateComplexType]` (requires the `Microsoft.AspNetCore.Components.DataAnnotations.Validation` package) or FluentValidation, which handles nesting natively.

## Examples

### Dropdown with Enum

```razor
<EditSelectEnum @bind-Value="model.Priority" 
                Field="@(() => model.Priority)"
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
                 Field="@(() => model.Department)"
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
                       Field="@(() => model.Skills)"
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
            Field="@(() => model.Name)"
            Label="Name" 
            ContainerClass="my-custom-style" />
```

## Accessibility

WssBlazorControls is built with accessibility as a priority:

- **ARIA attributes** for screen readers
- **Keyboard navigation** support
- **Focus management** and indicators
- **Semantic HTML** structure
- **High contrast** color support

## Browser Support

- Modern browsers with WebAssembly support
- Designed for both Blazor Server and Blazor WebAssembly scenarios
- Compatible with .NET 8.0+

## Trimming and AOT

`WssBlazorControls` is trim- and AOT-compatible: the package ships `IsTrimmable`/`IsAotCompatible` metadata, and the trim, AOT, and single-file analyzers run warning-clean on every build (enforced by `TreatWarningsAsErrors`). A default Blazor WebAssembly publish (`dotnet publish -c Release`) trims the library automatically.

Why the attribute-driven features survive trimming:

- **Labels, tooltips, descriptions, length/range extraction** — every control takes `Field="@(() => model.Property)"`; the expression tree roots the property's getter, so the trimmer keeps the property and the attributes on it. The attribute types themselves (`[DisplayName]`, `[Description]`, `[ToolTip]`, `[Range]`, ...) are referenced by the library and kept.
- **Enum display names** — `[EnumDisplayName]`/`[Display]` lookups only reflect over enum types, whose fields the trimmer always preserves.
- **Option building** — enum option lists use `Enum.GetValuesAsUnderlyingType` (no dynamic array creation), safe under WASM AOT.

Consumer notes:

- The generic controls (`EditNumber<T>`, `EditDate<T>`, `EditSelect<TValue>`, `EditRadio*`) annotate their type parameter with `[DynamicallyAccessedMembers(All)]`, mirroring the framework's `InputNumber`/`InputSelect`. Normal usage (binding concrete model properties) compiles warning-free; only forwarding an open generic parameter into them propagates the annotation.
- `<DataAnnotationsValidator>` is the framework's reflection-based validator and warns under full trimming in *your* app — models bound through `Field` expressions are rooted in practice, but validation of unbound/nested models is your app's concern.
- `[MinLength]`/`[MaxLength]` attribute constructors are marked `RequiresUnreferencedCode` by the BCL (they reflect over a `Count` property for exotic collection types). On `List<T>`/`ICollection` — what the list-bound controls use — the reflection path is never hit; suppress or ignore that IL2026 in app code.
- `TrimMode=full` deletes a Blazor WASM app whose routable components are only discovered via the `Router`'s reflection. If you opt into full trimming, root your app assembly: `<TrimmerRootAssembly Include="YourApp.Client" />`.

The e2e suite can run against a trimmed publish to re-verify all of this: publish `FormTesting` with `-p:TrimMode=full -p:WssFullTrimTest=true`, then run the e2e tests with `FORMTESTING_E2E_APP` pointing at the published `FormTesting.dll`.

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

A library-wide bug-fix and hardening pass. Version stays 10.3.0 until the next publish; these notes accumulate for the next release entry.

**Correctness**
- `EditRadio.IsDisabled` actually disables its `InputRadio` children now (a nested `fieldset[disabled]` — `InputRadioGroup` renders no element, so the old attribute vanished). All three radio controls forward `ValueExpression` to their inner group so it notifies/styles the real model field. `EditRadio.Field` is now `required` like every sibling.
- A null bound `List<T>` no longer crashes `EditFile`'s render or the first checkbox toggle in the checked lists — null is treated as empty and the list is created on first add.
- `EditFile` now buffers each selected file's bytes into memory at pick time instead of holding the framework's `IBrowserFile`. Previously, choosing files in more than one batch (or hitting `MaxFiles`, which unmounts the `<InputFile>`) left every earlier file throwing on `OpenReadStream()` — Blazor wipes the browser file map on each change event. Buffered files stay readable for the life of the list, so multi-batch accumulation and the per-file remove buttons behave as the UI implies. A bare `file.OpenReadStream()` (no size argument) now works regardless of file size — the bytes are already in memory, bounded by `MaxFileSizeBytes`. Trade-off: selected files occupy memory until cleared, and on Blazor Server the bytes cross the circuit at selection; the aggregate is bounded by the new `MaxTotalBytes` (default **100 MB** across all selected files, `0` = unlimited), with `MaxFileSizeBytes`/`MaxFiles` bounding per-file size and count.
- `EditNumber` binds on `change` instead of `oninput` (browsers report `type=number` as `""` mid-typing, flashing "must be a number" on partial input like `-` or `3.`), and formats the unsigned/byte types invariantly to match the parse side.
- `EditRadioString`: an options list legitimately containing `"Other"` no longer collides with the built-in other-option sentinel (which silently replaced the model value with the empty other-text). The internal sentinel is now also uniquified against the options list, so no option string whatsoever can collide with it.
- `EditRadioString`'s "Other" free-text box now honors `IsDisabled` — with the Other option selected it used to stay editable (writing to the model per keystroke) while every radio was disabled. Matches `EditRadioEnum`.
- The list-bound controls re-derive their `FieldIdentifier` when the model/`EditContext` is swapped, so validation targets the new model instead of dead state; they also work outside an `EditForm` (no more `FieldValidationDisplay` NRE).
- The scalar controls and `EditRadio` also render standalone (no surrounding `EditForm`) again — `IsInvalid` now guards a null `EditContext` instead of dereferencing it, matching the list base.
- `EditSelectString` renders a leading empty option (`NullOptionText`) — a null value used to display the first option as selected while the model stayed null. Selecting that blank now clears the model to `null`/`default` (a `string?` could previously never return from `""` to null; a non-string `TValue` like `EditSelectString<int?>` reported "not valid" instead of clearing). The blank is now opt-out — set `NullOptionText="@null"` to drop it (e.g. a required field) — and is auto-suppressed for a non-nullable value type (`EditSelectString<int>`), where a blank would only map to a spurious `default`.
- Select parsing **and formatting** (`EditSelect`/`EditSelectString`) are invariant-culture, matching `EditNumber` — `"1.5"` no longer parses as `15` under de-DE, and a bound `double` `1.5` now renders as `value="1.5"` (was `"1,5"`, which matched no `<option>` and left the select visually unselected).
- `Table`: fully-equal duplicate rows no longer crash the render (de-duplicated row keys); new `RowKey` parameter (e.g. `x => x.Id`) gives rows identity, and selection is key-based. Descending sort survives `int.MinValue` from subtraction comparators; a column whose parameters never change (title-only spacer) no longer silently vanishes.
- Toast auto-dismiss durations are capped below `Task.Delay`'s ~24.8-day limit instead of throwing into a fire-and-forget task.
- Performance/leak hardening: `FieldValidationDisplay` memoizes its per-field value-type reflection (a large form re-reflected every field on every keystroke); the list-bound controls unregister their old field on a model/`EditContext` swap (and on dispose) so the validation summary's field list can't accumulate dead entries; the `EnumHelpers` id cache stops calling the lock-acquiring `Count` once saturated instead of on every subsequent call.

**Overlays**
- One Escape no longer closes the whole overlay stack: panels stop keydown propagation, and the Select input does so only while its dropdown is open (so Escape still reaches an enclosing Modal once the dropdown is closed).
- Overlays stack in **open order** via a JS z-index counter (Modal-vs-Drawer DOM-order ties and Popover-above-a-later-Modal are gone); an open Select sits above its own backdrop (clicking your own search input/tags/clear no longer closes the dropdown); toasts are the always-on-top layer.
- Modal/Drawer: neither a close→reopen race **nor disposal while the open animation is in flight** can leak the body-scroll lock / document listeners now (a `_disposed` guard releases the late focus-trap handle instead of orphaning it); the Modal only dismisses when a mask click both **starts and ends** on the mask, so a drag crossing the mask/panel boundary in *either* direction keeps it open, and a press released outside the window can't leave a stale flag that closes a later gesture; the focus trap is document-level and survives focus escaping the panel (nested overlays hand it to the innermost dialog); title-less non-closable dialogs render no empty header and fall back to `aria-label="Dialog"`/`"Drawer"`.
- `Alert`'s close button hides the alert itself (`OnClose` is a notification, not a requirement).

**Select engine**
- Enter picks the highlighted option **without** triggering the enclosing form's implicit submission; arrow keys no longer jump the caret; Enter on a closed combobox opens it; opening highlights the current selection (scrolled into view) and skips disabled options; Tab-away closes the dropdown (its invisible backdrop used to swallow the next click).
- `Options`/`Values` are now explicitly immutable parameters (reference-guarded rebuilds — a parent re-render used to re-copy/re-filter the whole option set per keystroke). Reassign a new instance to refresh.
- Tags mode prunes a user-created tag from the options once deselected; `EditMultiSelect` throws a clear exception on `Mode="Single"` (selections silently reverted — use `EditSelectSearch`).

**Accessibility**
- Hidden labels (`IsLabelHidden`) render a visually-hidden label/legend so controls keep an accessible name — including `EditBool`, whose edit branch renders its own label and had been shipping an unnamed checkbox in the hidden-label case; checked-list fieldsets expose `role=group` + `aria-required`/`-invalid`; each validation message renders in its own element (no more run-together text); dynamic `Label` changes propagate to `EditBool` and validation messages; `label[for]` no longer references the non-labelable read-only div; `LabelTooltip` dismisses on Escape (WCAG 1.4.13) and now stops that Escape from also closing an enclosing Modal/overlay (one Escape, one layer); pagers get distinct landmark names when a Table renders two; the select-all checkbox announces its per-page scope.
- `IsInvalid` is read from `EditContext` messages instead of substring-matching `"invalid"` in `CssClass` (a consumer class like `invalid-style-fix` rendered a permanent red X). **`InvalidIcon` now takes `IsInvalid` (bool) instead of `CssClass`.**
- `[Display(Name = …)]` is honored for labels (after `[DisplayName]`/`[EnumDisplayName]`), keeping labels consistent with DataAnnotations' own messages — and now resolves through `GetName()`, so a localized `[Display(Name = …, ResourceType = …)]` yields the localized text instead of the raw resource key (both for control labels and enum display names).
- `EditFile`: removing a file with the keyboard keeps focus on the control (the file that shifted into the slot, else the new last file, else the drop zone) instead of dropping focus to `<body>`; a disabled drop zone no longer shows the drag-hover highlight for a drop it will refuse.
- `Popover`/`Popconfirm`: the consumer's own trigger element (typically a `<button>` in `ChildContent`) is the trigger now — the wrapper span no longer renders `role="button"`/`tabindex="0"` around it, which nested a button inside a button (two tab stops, invalid ARIA). JS mirrors `aria-haspopup`/`aria-expanded` onto the child and restores focus to it on close; content with nothing focusable (plain text/icon) gets the wrapper promoted to the button role as before. Without JS, a button child still opens/closes via its bubbled click — only the popup ARIA and the plain-content keyboard path need the runtime.

**Validation stacks (FluentValidation support)**
- New `FormOptions.RequiredResolver` (`Func<FieldIdentifier, bool>?`): a form-level source of required-ness for validation stacks that don't use `[Required]` (e.g. FluentValidation). Fields the resolver marks required get the star and `aria-required` exactly as if attributed. See the new **Validation stacks** section for the FluentValidation bridge snippet.
- `IsRequired` is now three-state (`bool?`) on all controls and `FormLabel`: unset defers to the attribute/resolver; `true` forces required (unchanged); **`false` now forces optional** — previously it was a no-op, so a `RequiredAttribute`-derived conditional (RequiredIf) whose condition was off showed a permanent star with no way to remove it. Existing markup (`IsRequired="true"` or a bound `bool`) compiles unchanged.
- The star and `aria-required` are now computed by one shared resolver (`EditControlInit.IsRequired`), so the two signals can never disagree; `FieldValidationDisplay` dropped an unused required-ness field, and `EditControlInit.Init` no longer returns a redundant `IsRequired` tuple member (its value was always recomputed and overwritten).

**API changes to note when upgrading**
- `InvalidIcon.CssClass` → `InvalidIcon.IsInvalid` (bool); `LabelTooltip.TooltipChanged` removed (never invoked); `ValidationView.Model` removed (never read); `EditRadio.Field` is now required; `EditSelectString` gains a leading empty option (opt out with `NullOptionText="@null"`; its type is now `string?`) and selecting the blank now writes `null`/`default` instead of `""`; `EditNumber` commits on change (not per keystroke); `Alert` self-dismisses on close; `Select`/`Table` collection parameters are immutable-by-reference.
- New: `Table.RowKey`, `Pagination.AriaLabel`, `EditSelect.ReadOnlyText`, `EditSelectString.NullOptionText`, `FormLabel.IsForLabelable`.
- `Popover`/`Popconfirm` trigger contract: pass a focusable element (typically a `<button>`) as the trigger content — it is the single tab stop and carries the popup ARIA. Plain-text trigger content still works but is keyboard-accessible only when JS is available. The trigger child is re-resolved on every sync, so conditionally-swapped trigger content (`@if (busy) { spinner } else { button }`) keeps its ARIA and close-focus; focusable non-button children (a `[tabindex]` span, an anchor) get Enter/Space activation, while `input`/`select`/`textarea` children keep their editing semantics (a `<button>` remains the recommended trigger).
- New `FormDefaults` component: render-tree-scoped defaults for `IsRequiredStarHidden` / `ShowFieldNameInValidation` — wrap an app or MFE root to configure its forms without touching the process-wide `FormOptions` statics (which are shared across circuits on Blazor Server). Resolution: `FormOptions` instance value → cascaded `FormDefaults` → static default. Nested instances chain per property (an unset inner setting falls through to the enclosing `FormDefaults` before the static), so host-page defaults and MFE-root overrides compose. Non-breaking; the statics remain as the final fallback.

**Packaging & repo**
- The packages now ship XML docs (IntelliSense), SourceLink + `.snupkg` symbols, deterministic CI builds, package validation, and an SPDX `MIT` license expression; warnings are errors. GitHub Actions CI builds the solution, runs the bUnit suite across net8/net9/net10, packs both packages, and runs the Playwright E2E suite. The E2E project is now part of `FormTesting.sln`. The Quick Start documents the required `edit-controls.js` script tag.

**Trimming / WASM AOT**
- The package is now trim- and AOT-compatible (`IsTrimmable`/`IsAotCompatible` + warning-clean trim/AOT/single-file analyzers, enforced as errors). A default Blazor WASM publish trims the library. See the new **Trimming and AOT** section above for what survives and the consumer caveats.
- Reflection sites were made trim-safe rather than suppressed wholesale: enum option builders use `Enum.GetValuesAsUnderlyingType` (AOT-safe, no `RequiresDynamicCode`); `PropertyColumn`'s comparability probe drops `MakeGenericType` (the one lost corner — `Nullable<T>` whose `T` implements *only* `IComparable<T>` — degrades to non-sortable, `SortBy` unaffected); the generic value-bearing controls annotate `T` with `[DynamicallyAccessedMembers(All)]` exactly like the framework's `InputNumber`/`InputSelect`; the two by-name lookups (validation value-type, enum field) carry justified suppressions with graceful fallbacks.
- Verified end-to-end: the full Playwright e2e suite passes against a `TrimMode=full` publish of the demo host (labels, required stars, length/range message rewrites, `[EnumDisplayName]` options, tooltips, visual baselines).

**Round-3 review fixes** *(post-hardening evaluation — see `EVALUATION.md`)*
- `Popover`/`Popconfirm` re-resolve their trigger child on every ARIA sync, so conditionally-swapped trigger content (`@if (busy) { spinner } else { button }`) no longer strands `aria-haspopup`/`aria-expanded` on a detached element or drops close-focus to `<body>`, and a wrapper promoted around plain content is demoted again when a real button appears (no more button-in-button after a swap). Focusable non-button trigger children (`[tabindex]` spans, anchors) gained Enter/Space activation; a `Disabled` Popconfirm marks an interactive child `aria-disabled`. The per-render JS interop call is now skipped unless `(open, disabled)` changed — a Popconfirm-per-row Table no longer pays one SignalR round trip per row per re-render on Blazor Server (a `focusin` listener repairs ARIA for children swapped while idle).
- `EditFile`: new `MaxTotalBytes` parameter (default **100 MB**, `0` = unlimited) bounds the aggregate buffered footprint across all selected files — buffering at pick time otherwise let a single large multi-file drop allocate unbounded server memory under the default `MaxFiles = 0`.
- Date-typed selects round-trip: `EditSelect<DateOnly>`/`<DateTime>`/`<DateTimeOffset>`/`<TimeOnly>` now format to the ISO forms option values are authored in, so picking an option no longer immediately loses the visual selection while the model holds the value. Author your option values in the matching canonical form — `DateOnly`: `2026-06-15` · `DateTime`: `2026-06-15T14:30:45` · `DateTimeOffset`: `2026-06-15T14:30:45-05:00` (UTC is `+00:00`, not `Z`) · `TimeOnly`: `14:30:45`. Shorter authored forms (`2026-06-15` for a `DateTime`, `14:30` for a `TimeOnly`) still *parse* on pick, but the formatted value won't visually re-match them.
- `EditSelectString` with a suppressed blank option (non-nullable value types, or `NullOptionText="@null"`) renders a hidden placeholder when the current value matches no option — an untouched default (e.g. `0`) displays blank instead of silently showing the first option while the model holds something else.
- The open `Select`'s stacking z-index is mirrored into its C#-owned `style`, so a re-render that changes `Width` mid-open no longer clobbers it and drops the selector below its own backdrop (which made clicks on the select's own input close the dropdown).
- Two controls bound to the same property now share their validation-summary registration safely: disposing one (e.g. closing an edit modal that duplicates a page field) keeps the surviving control's messages — registrations are owner-tracked and dropped only by the last registrant.
- Nested `FormDefaults` chain per property instead of the inner instance shadowing the outer entirely (see the `FormDefaults` note above).
- `Select`, `Modal`, `Drawer`, `Popover`, and `Popconfirm` no longer strand a JS module reference when disposed while their module import is in flight (the same race `Table` was already guarded against).

**Round-4 review fixes** *(post-round-3 evaluation — see `EVALUATION.md`)*
- `EditSelect<DateTimeOffset>` now formats whole-second values without the `.0000000` fraction (`2026-06-15T14:30:45-05:00`), so authored option values actually match and the visual selection survives a pick; sub-second values keep the full round-trip form. The canonical authored forms per date type are documented in the round-3 entry above.
- `Popover`/`Popconfirm` trigger ARIA: a consumer-owned `aria-disabled` on the trigger child is no longer removed when the component's `Disabled` round-trips; when the resolved trigger child changes identity while the old element stays in the DOM, the popup ARIA is stripped off the old element instead of two elements announcing the popup.
- `EditCheckedStringList`/`EditCheckedEnumList` fieldsets no longer emit `aria-required`/`aria-invalid`/`aria-errormessage` — ARIA 1.2 doesn't support them on `role="group"` (assistive tech ignored them; checkers flag them). Required state remains on the legend star and the validation message, invalid state on each checkbox's `aria-invalid`. The radio fieldsets (`role="radiogroup"`, where these attributes are valid) are unchanged.

**Round-5 fixes** *(trim verification, globalization/RTL sweep, measured performance pass — see `EVALUATION.md`)*
- **RTL support:** the direction-sensitive Select geometry (arrow/clear anchoring, search inset, tag/placeholder spacing) and the form controls' trailing invalid-icon/required-star spacing now use CSS logical properties — under `dir="rtl"` tags no longer render beneath the opaque clear button (where a tap cleared the entire selection) and typed search text no longer starts under the arrow. Rendering under LTR is byte-identical. Notification position, `DrawerPlacement` left/right, and Table alignment deliberately keep physical semantics.
- **Localization:** new label parameters with unchanged English defaults — `Pagination` `PreviousPageLabel`/`NextPageLabel`/`PageLabelFormat`; `Select`/`EditSelectSearch`/`EditMultiSelect` `RemoveItemLabelFormat`/`ClearSelectionLabel`/`ClearSelectionsLabel`/`ListboxLabel` — so localized apps can localize what screen readers hear.
- **Culture correctness:** the `[Range]` one-sided message rewrite ("Cannot exceed 100") now works after a runtime culture switch and in mixed-culture Blazor Server processes — the type-min/max sentinels are resolved per current culture instead of being frozen at first touch.
- **Performance:** `Table` no longer rebuilds its row keys and rescans selection state on every parent re-render (the cost was O(rows) with boxing, per keystroke in any sibling input for unpaged tables); `FormLabel`/`FieldValidationDisplay` skip label/attribute re-derivation — and stop re-invoking `FormOptions.RequiredResolver` — unless their inputs actually changed, honoring the resolver's documented "not on every keystroke" contract; `EditMultiSelect`'s read-only label join is O(selected) via a value→label lookup. Measured reality check: for *very* large unpaged tables the remaining cost is Blazor re-rendering the row fragment itself — prefer `PageSize` or the server-side paging composition at that scale.
- Verified this round: the full Playwright suite passes against a `TrimMode=full` publish; Select's dropdown virtualization confirmed (20 DOM rows at 1,000 options).

### 10.3.0

**New: `EditFile` — multi-file upload control**
- `EditFile` is a new form control that binds a `List<IBrowserFile>` via the standard `Value` / `ValueChanged` / `Field` pattern, integrating with `EditContext` validation like every other `Edit*` control.
- Supports drag-and-drop and click-to-browse. An invisible `<InputFile>` overlay covers the entire drop zone so both interactions work natively without extra JS.
- Multiple files are supported. The drop zone stays visible until an optional `MaxFiles` cap is reached; files already chosen appear as a dismissible list below it (hover to reveal the remove button per file).
- `AllowedExtensions` (e.g. `".pdf"`, `".xlsx"`) filters by extension; `MaxFileSizeBytes` caps individual file size (default 10 MB). Validation errors from either check are shown inline below the drop zone.
- The drop zone border turns red when there's a validation error from the format/size check or when the field fails `EditContext` validation; the upload icon switches to its error (red) variant to match.
- Read-only mode shows the selected filenames with a paperclip icon; empty renders a blank `ReadOnlyValue` consistent with the other controls.
- Styled to match the Hatch / Spot drop-zone look: dashed `#b7b7b7` border, `#f3f3f3` background, primary-color hover border. Tokens bridge to `--color-primary` and `--color-danger` so the control follows the consumer's theme.
- Adds four inline-SVG icon classes to `edit-controls.css`: `.edit-icon-upload`, `.edit-icon-upload-error`, `.edit-icon-paperclip`, `.edit-icon-delete`.

**`Table` — robust dynamic columns + graceful sort**
- Columns may now be conditionally rendered (`@if`). The Table re-collects its columns in document order on each render, so a hidden column drops out and a re-shown one returns to its declared position — previously a removed column left a stale header and cells behind, and re-showing it produced a duplicate. Hiding the column that drives the active sort now clears the sort so the indicator and the row order can't disagree.
- A `Sortable` `PropertyColumn` whose property type isn't comparable no longer throws on the first header click (which on Blazor Server tore down the circuit) — the header simply isn't made sortable. Supply a `SortBy` comparison to sort any type.
- A sortable column declared without a `Title` now gives its sort `<button>` an `aria-label="Sort"`, so it isn't an unnamed button for screen-reader users.

**Accessibility**
- `Skeleton` announces its loading state to screen readers: `role="status"` + `aria-busy="true"` and a visually-hidden `LoadingText` (default `"Loading"`); the placeholder bars are `aria-hidden`. New `.wss-sr-only` utility class.
- Toast (`Message`) and `Notification` containers route each toast by severity into two always-present live regions — a polite `role="status"` region and an assertive `role="alert"` region — instead of flipping a single shared region's politeness when an error arrives (a change screen readers don't reliably re-announce, which could swallow the error). The regions are `display:contents`, so the on-screen layout is unchanged (errors group below the polite toasts).

### 10.2.0

*Headline release: debuts the dependency-free AntDesign-style UI-kit controls (`Select`, `Alert`, `Modal`, `Drawer`, `Table`, `Pagination`, `Popover` / `Popconfirm`, `Skeleton`, toasts) and the searchable form selects (`EditSelectSearch` / `EditMultiSelect`), alongside a library-wide accessibility & architecture overhaul (the `EditControlBase` refactor). Adds `Table` column sorting and configurable pager placement. Includes one **breaking dependency change** — see below.*

**New: `Table` column sorting**
- Columns can now sort. Set `Sortable="true"` on a `PropertyColumn` (the comparison is derived from its `Property` via `Comparer<T>.Default`), or supply a `SortBy` comparison on any `Column` for custom / template columns. Clicking a sortable header cycles ascending -> descending -> unsorted (restoring the original `DataSource` order); the sort is stable (ties keep their original order). Headers expose `aria-sort` (`ascending` / `descending` / `none`) and a keyboard-focusable `<button>` so the feature is screen-reader- and keyboard-accessible. Sorting resets to page 1 and survives a `DataSource` swap.

**`Table` / `Pagination` polish**
- The table pager is now configurable: `PagerPosition="Top | Bottom | Both"` (default `Bottom`) places it above, below, or both above and below the table, and `PagerAlign="Left | Center | Right"` (default `Right`, matching AntD) aligns it horizontally. When `Both`, the two pagers stay synced to the same page.
- The pager buttons now hold a consistent 32px square via a `min-height` floor, so an aggressive consumer reset such as `button { max-height: fit-content }` can no longer collapse them to content height (which made the icon-only prev/next buttons render shorter than the numbered ones).
- The `Table` now renders its grid and pager inside a single root element, so a parent's flex/grid `gap` doesn't stack on top of the pager's margin and inflate the space between the table and its pager.

**Accessibility, theming & performance (audit follow-up)**
- **Grouped controls now surface validation state.** The radio controls (`EditRadio`, `EditRadioEnum`, `EditRadioString`, `EditBoolNullRadio`) expose `aria-invalid` / `aria-required` / `aria-describedby` on a `role="radiogroup"` `<fieldset>` named by its legend (previously splatted onto `<InputRadioGroup>`, which renders no element — so they didn't reliably appear). The checkbox lists (`EditCheckedStringList`, `EditCheckedEnumList`) mark each checkbox `aria-invalid` (they had none). And because the list controls are `ComponentBase` (not `InputBase`), they now subscribe to the `EditContext` so their invalid state updates live on validation — matching the scalar controls. This completes "`aria-invalid` on every editable control".
- **`aria-describedby` no longer dangles** — it references only the `desc-` / `tooltip-` ids that actually render, and is resolved once per control rather than re-interpolated on every render. `aria-errormessage` is emitted only while the field is invalid (per the ARIA spec).
- **Form controls are self-sufficient out of the box.** `edit-controls.css` now ships a `:focus-visible` ring for the editable elements (WCAG 2.4.7 — no longer dependent on the browser default the consumer may have reset) and an `.invalid` border, so keyboard focus and the validation error state are visible without the consumer supplying their own styles. The validation X icon and the tooltip info icon use `currentColor` driven by `--color-danger` / `--color-text`, so they follow the consumer theme.
- **`wss-controls.css`:** the `Select` sizing now uses the existing `--wss-*` tokens (overriding a token rescales the control as intended), and the classes the markup referenced but the stylesheet never defined (`wss-popconfirm-title`, `wss-table-caption`, `wss-select-selection-item-rest`, …) are now declared.
- **Fewer per-render allocations.** `Select` caches its visible tags and `Table` caches the current page (it was materializing the page twice per render). `Table` now treats `DataSource` / `SelectedItems` as immutable parameters (reference-guarded) — reassign them to refresh rather than mutating in place.
- **Removed the unused `ReadOnlyValue.IsRequired` parameter** (it was `required` but never rendered).
- **Nullable enum selects can represent null.** `EditSelectEnum<TEnum?>` now renders a leading empty/placeholder option (label via the new `NullOptionText` parameter) so a null value shows blank instead of silently displaying the first member, and the user can clear the field. Non-nullable enums are unchanged.
- **More ARIA correctness.** All bool-bound ARIA booleans (`aria-expanded` / `aria-hidden` / `aria-disabled`) now render lowercase `"true"`/`"false"`; `Alert` announces by severity (`role`/`aria-live`: error = assertive, otherwise polite) instead of always `role="alert"`; the radio `<fieldset>` itself is the `role="radiogroup"` (no nested double-group) with its id gated to edit mode so it doesn't collide with the read-only value; read-only `aria-labelledby` is suppressed when the label is hidden; the `Select` gets a focus ring before it opens; and `Escape` closes `Popover` / `Popconfirm` from inside the panel.
- **Correctness fixes.** `Select` now shows the selected label and clear button even when a single value equals `default(TValue)` (e.g. a non-nullable enum's `0` member — previously mis-rendered as the empty placeholder); `ValidationView` summary links now target each control's actual id, honoring `IdPrefix` / an explicit `Id` (the resolved id is captured at field registration) instead of a recomputed guess; the checkbox lists no longer throw in read-only mode when the bound list is `null`, and sanitize their read-only per-option ids via `ToId()`; a disabled `Popconfirm` trigger is now `aria-disabled` and removed from the tab order.
- **More correctness & a11y fixes.** `EditRadioString` now follows an externally-changed value (form reset, async-loaded model, programmatic set) instead of caching the selection once — and a custom initial value correctly resolves to the "Other" radio with its text box pre-filled; `EditRadioEnum`'s "Other" free-text input gained an accessible name (`aria-label`), matching its `EditRadioString` sibling; the `Select` clear button is now revealed on keyboard focus (`:focus-within`), not only on hover, so a keyboard user can see the control they've tabbed to; the `Table`'s "select all" checkbox enters the native `indeterminate` (mixed) state when only some rows on the page are selected, so screen readers announce the partial selection; and the length-attribute helper takes the *tighter* (smaller) upper bound when both `[StringLength]` and `[MaxLength]` apply.
- **Checkbox-list validation links resolve.** `EditCheckedStringList` / `EditCheckedEnumList` now render their resolved id on the `<fieldset>` in edit mode (gated like the radio groups), so a `ValidationView` summary link for one of these fields actually jumps to the control — their checkboxes/label/error elements all carry *decorated* ids, so the bare id previously had nowhere to land, leaving the link dangling.
- **Visual & robustness fixes.** `Pagination` clamps an out-of-range `Current` to the valid range, so Previous/Next enable correctly instead of looking clickable but doing nothing; a long `Popconfirm` title now wraps inside the panel instead of overflowing it; and the loading `Skeleton` shows a flat fill under `prefers-reduced-motion` rather than a frozen, off-centre shimmer band.
- **Overlay focus-trap & scroll-lock hardening.** The Modal / Drawer focus trap no longer lets Shift+Tab escape when focus is on the panel itself (e.g. after clicking an empty area of the body) — focus is pulled back into the dialog. The body-scroll lock is now ref-counted, so stacked overlays don't unlock the page when the first-opened one closes, and the focus handle's disposal is idempotent.

**New: AntDesign-style controls (ported from `Standalone.Controls`)**
- **Form selects:** `EditSelectSearch<T>` (searchable single-select) and `EditMultiSelect<T>` (multiple / tags, binds `List<T>`) — full `Edit*` controls (validation, label, read-only, `FormOptions`) backed by a new dependency-free, virtualized dropdown engine (`Select<T>`). They sit **alongside** the existing `EditSelect` / `EditSelectEnum` / `EditSelectString`, which are unchanged.
- **UI kit (non-form):** `Select<T>`, `Alert`, `Skeleton`, `Popover`, `Pagination`, `Modal`, `Drawer`, `Popconfirm`, `Table<TItem>` (+ `Column` / `PropertyColumn` / `ActionColumn`), and toasts/notifications in two flavors — **scoped/Server-safe** (`IMessageService` / `INotificationService` via `AddWssControlsToasts()` + `MessageContainer` / `NotificationContainer`) and **registration-free static for WASM** (`WasmMessageService` / `WasmNotificationService` + their containers). `Icon`, `Button`, `Checkbox`, and `Tag` were intentionally excluded.
- **New stylesheet:** these controls use the `wss-` class prefix and `--wss-*` theme tokens shipped in `wss-controls.css`. Add a second link alongside `edit-controls.css`:
  ```html
  <link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
  ```
  Tokens default to the AntDesign 4.x look and bridge to your existing `--color-*` / `--border-color` where present. The Select keyboard helper ships as an RCL JS module at `_content/WssBlazorControls/wss-select.js` (auto-imported, degrades gracefully).
- No service registration required (consistent with the rest of the library).

**Accessibility & correctness (library audit)**
- **Modal / Drawer:** trap focus while open, restore focus to the trigger on close, close on `Escape`, lock body scroll, and expose `role="dialog"` + `aria-modal="true"` + `aria-labelledby` (the title). OK/confirm still never auto-closes — the caller decides.
- **Popover / Popconfirm:** the trigger is a real focusable control (`role="button"`, `tabindex="0"`, `aria-haspopup`, `aria-expanded`) operable from the keyboard — `Enter` / `Space` to open, `Escape` to close. Both flip to the opposite side and shift along the cross axis to stay within the viewport, rendering hidden for one frame so the placement is never seen to jump.
- **`Select` / `EditSelectSearch` / `EditMultiSelect`:** full combobox ARIA (`role="combobox"` / `listbox` / `option`, `aria-expanded`, `aria-controls`, `aria-activedescendant`); the dropdown now opens **upward** when it would otherwise run off the bottom of the viewport.
- **`Pagination`:** rewritten as a semantic `<nav aria-label="Pagination">` of `<button>`s with `aria-current="page"` on the active page and `aria-label`s on the prev/next controls (was `<ul>` / `<li>` / `<a>`).
- **Toasts / notifications:** the live region is announced via `role="status"` + `aria-live="polite"`.
- **`ReadOnlyValue` now HTML-encodes** the value it displays instead of rendering it as raw markup — bound user data can no longer inject markup.
- **`EditDate` read-only** formats the bound value by its own type with `DateFormat`. The old code round-tripped through the editor string, which could shift the date across midnight in non-UTC zones and rendered a `TimeOnly` as a date; an incompatible format now degrades to the value's own `ToString` rather than throwing.
- **`EditCheckedEnumList` / `EditCheckedStringList`** build a new list when toggling instead of mutating the caller's bound collection in place.
- The placement enum for `Popover` / `Popconfirm` is named `PopupPlacement` (it positions popups, not tooltips). The library builds with **0 warnings** across net8 / net9 / net10.

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
- `FormTesting/FormTesting.Client.Tests/` (xUnit + bUnit, multi-targeted net8/9/10) — 270 tests (run once per TFM) covering the helpers (`EnumHelpers` cache + attribute precedence, `AttributesHelper.GetId` / `GetLabelText` / `GetMinAndMaxLengths`, `EditControlInit`, `ValidationHelper` regex parsing), bUnit smoke tests for the form controls (rendered DOM, ARIA, edit/read-only switching), the AntDesign-style selects, and the UI-kit widgets (Table, dialogs, toasts) — plus regression tests for the audit fixes (`ReadOnlyValue` HTML-encoding, `EditDate` read-only formatting, checked-list immutability). Run with `dotnet test FormTesting/FormTesting.Client.Tests/FormTesting.Client.Tests.csproj`.
- `FormTesting/FormTesting.Client.E2ETests/` (xUnit + Playwright .NET, net10) — a 67-test end-to-end suite (one class per `Edit*` control plus the searchable selects and a driver for the `/uikit` gallery) with committed visual-regression baselines. Run with `dotnet test FormTesting/FormTesting.Client.E2ETests/FormTesting.Client.E2ETests.csproj`.

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
- `EditBoolNullRadio` radio inputs now carry `aria-required`, `aria-invalid`, `aria-describedby`, and `aria-errormessage`. *(Moved to a group-level `role="radiogroup"` container in the next release — see Unreleased.)*
- `aria-invalid` is now rendered on every **scalar** editable control. *(The grouped radio / checkbox-list controls are brought to parity in the next release — see Unreleased.)*
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

