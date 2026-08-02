# WssBlazorControls

[![NuGet Version](https://img.shields.io/nuget/v/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)

A comprehensive library of form controls for Blazor applications providing consistent, feature-rich input components with built-in validation, accessibility support, and flexible styling options.

## Features

- **Rich Form Controls**: String, Number, Date, Boolean, Select, Radio, Checkbox lists, and TextArea components
- **Searchable & Multi-Select**: AntDesign-style `EditSelectSearch` / `EditMultiSelect` — type-to-search, tags, virtualized dropdown
- **AntDesign-style UI Kit**: dependency-free Alert, Modal, Drawer, Table, Pagination, Popover, Popconfirm, DateRangePicker, Skeleton, and toasts
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
<link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
```

   **Both are required as of 10.7.0.** `wss-controls.css` used to be needed only if you used the
   AntDesign-style UI-kit controls (`Select`, `Alert`, `Modal`, `Table`, ...) — but `EditDate` (the
   default date control since 10.7.0) is built on the UI-kit `DatePicker`, so its `wss-picker-*`
   styling now ships from this stylesheet too. Omit it only if every date field in your app uses
   `EditDateNative` instead of `EditDate`.

3. **Include the JS helpers** (next to your Blazor script tag):

```html
<script src="_content/WssBlazorControls/edit-controls.js"></script>
```

   Required by `JsInteropEc.FocusFirstInvalidField` (focus the first invalid field on a failed
   submit). The UI-kit controls — including the `DatePicker` that now backs `EditDate` — load their
   own JS modules (`wss-select.js`, `wss-picker.js`, `wss-overlay.js`, ...) lazily; no extra
   `<script>` tags needed for them. If the script tag isn't linked (e.g. a cross-origin
   micro-frontend whose host page doesn't serve `_content/WssBlazorControls/`), `JsInteropEc`'s
   methods lazily import the module themselves and never throw — see
   [FormDefaults.AssetBase](#formdefaults) to point that fallback import (and the UI-kit modules'
   own lazy imports, `wss-picker.js` included) at the right origin.

   Optional: add `<script src="_content/WssBlazorControls/wss-tooltip.js"></script>` if you use
   `data-tooltip` hover tooltips (see [Hover tooltips](#hover-tooltips-data-tooltip) below) and want
   them to auto-place instead of always opening below the element. `LabelTooltip` (the form-label
   help icon) uses the same auto-placement but imports the module itself — no tag needed for it.

4. **Use the controls** in your Blazor components:

```razor
@using System.ComponentModel
@using System.ComponentModel.DataAnnotations
@using Controls.Helpers

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />

    @* No Label needed: "Name", "Age", and "Birth Date" are derived correctly
       from the property names, and the required star comes from [Required].
       Name's hint text ("e.g. Jane Doe") comes from [Placeholder] on the model too. *@
    <EditString @bind-Value="model.Name" />
    <EditNumber @bind-Value="model.Age" />
    <EditDate   @bind-Value="model.BirthDate" />

    @* "Is Active" would be wrong, so the constant label lives on the model. *@
    <EditBool   @bind-Value="model.IsActive" />

    @* Label is set in markup only because the text is dynamic at runtime. *@
    <EditString @bind-Value="model.Answer" Label="@_currentQuestion" />

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
        [Placeholder("e.g. Jane Doe")]
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
- **`EditString`** - Text input with masking and URL support; also supports `Prefix`/`Suffix` affix content, `AllowClear`, `MaxLength`/`ShowCount`, and an `IsPassword` show/hide toggle (independent of the read-only `MaskText` feature) — these switch the input into an AntD-style affix layout via the shared internal `EditInputShell`, while plain markup stays byte-identical to the classic rendering. `Size` (`SelectSize`: `Default`/`Small`/`Large`, shared with the `Select` family) adds a size class to the input (and the affix wrapper, in affix mode); inert unless [`.edit-theme`](#opt-in-antd-theme-for-the-classic-edit-inputs-edit-theme) is opted into. `Placeholder` falls back to the bound property's `[Placeholder]`/`[Display(Prompt)]` when unset — see [Model-declared placeholders](#model-declared-placeholders-placeholder). `Autocomplete`, `MaxLength`, and `IsPassword` (now `string?`/`int?`/`bool?`) fall back to `[Autocomplete]`, `[StringLength]`/`[MaxLength]`, and `[DataType(Password)]` respectively — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditTextArea`** - Multi-line text input; also supports `AllowClear`, `MaxLength`/`ShowCount` (the count renders below the box, right-aligned — AntD `TextArea`'s placement, unlike `EditString`'s inline count), and `AutoSize`/`MinRows`/`MaxRows` (JS-driven grow/shrink to fit content, clamped between the two, degrading to the fixed `Rows` height with no JS) — the affix parameters switch the input into the shared `EditInputShell` layout, while plain markup stays byte-identical to the classic rendering. `Size` behaves the same as `EditString`'s — only padding/font change; height is never locked, so `Rows`/`AutoSize` still govern it. `Placeholder` resolves the same model-attribute fallback as `EditString`'s. `Rows`/`MinRows`/`MaxRows`/`AutoSize` (now `int?`/`int?`/`int?`/`bool?`) and `MaxLength` fall back to `[Rows]` and `[StringLength]`/`[MaxLength]` respectively — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditNumber`** - Numeric input with validation; also supports `Min`/`Max` (InvariantCulture, same type discipline as the existing `Step`; unset, they fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]` — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue)), `Placeholder` (same model-attribute fallback as `EditString`'s), and `Prefix`/`Suffix` affix content via the shared `EditInputShell` (no `AllowClear`/`ShowCount`/`IsPassword` — no AntD equivalent for a numeric field; native spinners stay, a documented deviation). `Size` behaves the same as `EditString`'s. `Step` (now `decimal?`) falls back to `[Step]`; `Format` falls back to `[DisplayFormat]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditDate`** - Form-bound calendar-dropdown date field (the UI-kit `DatePicker` with full `EditForm` validation), the default date control; full type parity with `EditDateNative` — binds `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` (and their nullable variants), with a `Type` parameter (`InputDateType`: `Date`/`DateTimeLocal`/`Month`/`Time`, same default as `EditDateNative`) selecting what the calendar picks, mapped onto the picker's `Mode`. A separate `Mode` parameter (`DatePickerMode?`, default null) overrides that mapping outright to reach `Week`/`Quarter`/`Year` — the one intentional asymmetry with `EditDateNative`, which has no such escape hatch since its native `<input>` types have no week/quarter/year equivalent to reach. `Min`/`Max` (`DateTime?`, date-granularity, ignored in `Time` mode) fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]`, the same resolution `EditNumber` uses — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue). Forwards the picker's full phase-2 surface too: `ShowWeekNumbers`, `DisabledDate`, `DisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`, `ShowToday`/`ShowNow`/`Presets`/`ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params — same defaults as the picker itself. `Size` (`SelectSize`: `Default`/`Small`/`Large`) renders `wss-picker-sm`/`wss-picker-lg` on the picker wrapper, mirroring `Select`'s own size classes. `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) surfaces a validation message when a typed entry can't be parsed as a date at all — a well-formed date merely rejected by `Min`/`Max`/`DisabledDate`/`DisabledTime` does not trigger it; previously a bad typed entry was silently reverted with no feedback. Unlike `EditDateNative`, `EditDate` has no `UpdateOn`: a calendar picker commits on selection or on parse-at-blur/Enter, so there's no per-keystroke commit to opt into — use `EditDateNative` if you need that axis. `Placeholder` (null default) falls back to the bound property's `[Placeholder]`/`[Display(Prompt)]`, then to the inner picker's own mode-derived default (e.g. "Select date") — see [Model-declared placeholders](#model-declared-placeholders-placeholder). `Type` (now `InputDateType?`) falls back to the bound property's `[DataType(DataType.Date/DateTime/Time)]`; `Format`/`DateFormat` fall back to `[DisplayFormat]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditDateNative`** - Native `<input type="date">` (or `datetime-local`/`month`/`time`, per `Type`) date field, zero JS, styled entirely by `edit-controls.css`. New in 10.7.0: `Min`/`Max` (`DateTime?`, same shape as `EditDate`'s — its first bounds support ever) render the native input's own `min`/`max` attribute formatted to match `Type`, omitted entirely in `Time` mode for parity with `EditDate`, and fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]`, when unset — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue). `Size` behaves the same as `EditString`'s, though `EditDateNative` never enters affix mode itself (no `Prefix`/`Suffix`/`AllowClear`/etc.), so only the input itself carries the size class. `Type` (now `InputDateType?`) falls back to the bound property's `[DataType(DataType.Date/DateTime/Time)]`; `DateFormat` (now `string?`) falls back to `[DisplayFormat]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditDateRange`** - Form-bound date-range field (`@bind-Start`/`@bind-End`, per-field validation, backed by `DateRangePicker`); forwards `DateRangePicker`'s full surface — `Mode` (`DatePickerMode`, default `Date`; dual linked panels at `Date`/`Week`/`Month`/`Quarter`/`Year` granularity, or a single-panel OK-confirm session for `DateTime`/`Time`), `Min`/`Max` (`Min` resolves param → Start's `[MinValue]`/`[Range]` → End's; `Max` resolves param → End's `[MaxValue]`/`[Range]` → Start's, so a single `[Range]` on `Start` alone supplies both ends — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue)), `DisabledDate`, `StartDisabledTime`/`EndDisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`/`OkText`, `ShowWeekNumbers`, `Presets`, `ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params. `Format` (the picker's own display/parse format) and `DateFormat` (the read-only display format) are both nullable with `Mode`-aware defaults — mirroring `EditDate`'s own `DateFormat` contract — instead of a fixed literal, so switching `Mode` alone still gets that mode's own default rather than silently keeping `Date`'s. Read-only display is `Mode`-aware too: `Quarter`/`Week` render the same `yyyy-Qn`/`yyyy-Www` shorthand the picker itself shows. `StartPlaceholder`/`EndPlaceholder` each fall back to their own bound property's `[Placeholder]`/`[Display(Prompt)]` independently — a `[Placeholder]` on `Start` never leaks onto `End` — then to the picker's own default; see [Model-declared placeholders](#model-declared-placeholders-placeholder). `Format`/`DateFormat` fall back to a `[DisplayFormat]` on `Start`'s attributes first, then `End`'s — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints). `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) surfaces a validation message against whichever endpoint's typed text can't be parsed as a date at all (`{0}` is that endpoint's own field name; a well-formed value merely rejected by `Min`/`Max`/`DisabledDate`/`*DisabledTime` does not trigger it), each endpoint's message clearing independently as soon as that endpoint next commits a valid value
- **`EditBool`** - Checkbox for boolean values. `TrueText`/`FalseText` (now `string?`) fall back to the bound property's `[BoolText]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditBoolNullRadio`** - Three-state radio for nullable booleans. `TrueText`/`FalseText`/`NullText` (now `string?`) fall back to the same `[BoolText]` attribute
- **`EditFile`** - Multi-file upload bound to a `List<IBrowserFile>` (drag-and-drop + click-to-browse, extension filtering, per-file size cap, aggregate size cap, optional max count). `AllowedExtensions` also accepts MIME types (`"application/pdf"`) and MIME wildcards (`"image/*"`), not just extensions; `BeforeAdd` is an optional async per-file gate before buffering; each listed file shows its formatted size; `Variant="EditFileVariant.Button"` swaps the dashed dropzone for a compact plain button (see [File upload parity features](#file-upload-parity-features-editfile)). `AllowedExtensions`/`MaxFileSizeBytes`/`MaxFiles`/`MaxTotalBytes` (the last three now `long?`/`int?`/`long?`) fall back to the bound property's `[FileConstraints]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)

### Selection Controls
- **`EditSelect`** - Dropdown selection for objects
- **`EditSelectEnum`** - Dropdown for enum values. A model-declared `[Placeholder]`/`[Display(Prompt)]` supplies the leading blank option's text (nullable enum) and the closed select's displayed text for an unmatched value (see [Model-declared placeholders](#model-declared-placeholders-placeholder))
- **`EditSelectString`** - Dropdown for string values. Same model-declared placeholder support as `EditSelectEnum`, with the same caveat: an explicit `NullOptionText="null"` still suppresses the leading option outright — a model attribute never resurrects it
- **`EditSelectSearch`** - Searchable single-select (AntDesign-style: type-to-search, clear, virtualized, option groups, loading state). `Placeholder` (now `string?`, no default) shows when nothing is selected, falling back to the bound property's `[Placeholder]`/`[Display(Prompt)]`, then the literal "Please select"
- **`EditMultiSelect`** - Multiple / tags select bound to a `List<T>` (AntDesign-style, same parity features as `EditSelectSearch`, including its `Placeholder` resolution and `string?` signature, and its `Variant` — `Outlined` default / `Pill` / `Borderless`)
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
<FormDefaults IsRequiredStarHidden="true" ShowFieldNameInValidation="false" UseStyledCheckbox="true">
    <Router AppAssembly="@typeof(App).Assembly">...</Router>
</FormDefaults>
```

Resolution per setting (highest wins): the form's `FormOptions` instance value → the cascaded `FormDefaults` → the static `FormOptions.Default*` property. Prefer `FormDefaults` over the statics: the statics are process-wide, so on Blazor Server they're shared by every user/circuit, and when several MFEs share one runtime they're shared across MFEs. `FormDefaults` scopes to the render tree, which matches app/MFE/circuit boundaries. It's intended as set-once root configuration — the cascade is registered as fixed, so runtime changes to its parameters don't propagate.

`UseStyledCheckbox` follows this same chain and additionally reaches the UI-kit `Table`'s row-selection checkboxes (which have no `FormOptions` of their own) — see [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).

`FormDefaults` also carries `AssetBase` (`string?`), which has no `FormOptions` counterpart: an absolute URL prefixed onto the RCL's lazy `wss-*.js` module imports (see the UI Kit section below), for a micro-frontend whose host page doesn't serve/proxy `_content/WssBlazorControls/*`. Unset (the default) keeps today's relative import path.

`FormDefaults` also carries `UpdateOn` (`UpdateTrigger?`) — a per-form-tree default for the commit-timing parameter on `EditString`/`EditTextArea`/`EditNumber`/`EditDateNative`/`EditRadioString`/`EditRadioEnum`. Like `AssetBase`, it has no `FormOptions` counterpart: the chain is just the control's own `UpdateOn` → the nearest enclosing `FormDefaults.UpdateOn` (`FormDefaults.EffectiveUpdateOn` walks nested `FormDefaults` the same way the other settings do) → that control's built-in default. See [Commit timing](#commit-timing-updateon).

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

- **`Select<T>`** - The dropdown engine behind `EditSelectSearch` / `EditMultiSelect`; usable standalone (single / multiple / tags, search, virtualized). `Prefix` renders leading content (typically an icon) in the trigger; `Variant="SelectVariant.Pill"` restyles the trigger as a rounded filter button, `SelectVariant.Borderless` shows no border/background until hover/focus (see below). `Loading`/`ShowArrow` control the arrow slot; `SelectOption.Group` renders AntD-style `OptGroup` headers; `FilterOption`, `EmptyContent`, `DropdownFooter`, and a controlled `Open`/`OpenChanged` round out the parity with Ant Design's `Select` (see [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect))
- **`Alert`** - Contextual message banner (success / info / warning / error, closable, description). `Banner` (full-width, borderless AntD banner mode) and `Action` (a trailing slot before the close button) round out AntD 4.x parity
- **`Skeleton`** - Loading placeholder with shimmer; announces `role="status"` / `aria-busy` with a visually-hidden `LoadingText` (default `"Loading"`) for screen readers. `Avatar`/`AvatarShape` add an avatar placeholder block; the standalone `SkeletonElement` (`Kind`: `Button`/`Input`) covers AntD's `Skeleton.Button`/`Skeleton.Input` shapes
- **`Popover`** - Click-triggered popover (4 placements); controlled `Visible`/`VisibleChanged` (`@bind-Visible`) mirrors `Select`'s controlled `Open` design
- **`Pagination`** - Controlled pager. `ShowTotal`, a `PageSizeOptions` size-changer (`@bind-PageSize`), `ShowQuickJumper`, and `Small` round out AntD 4.x parity (see [Pagination parity features](#pagination-parity-features-pagination))
- **`Modal`** - Dialog with `@bind-Visible`, footer, mask-close. `Centered` vertically centers it; `Keyboard` (default true) independently governs Escape-to-close (`Closable` only shows/hides the header X)
- **`Drawer`** - Slide-in panel (4 placements). `Extra` renders a header-right slot beside the close button; `Keyboard` (default true) independently governs Escape-to-close, same as `Modal`
- **`Popconfirm`** - Inline confirm popover. A genuinely-async `OnConfirm` keeps the popup open with a spinner until it resolves; `OkDanger` styles the OK button as danger; controlled `Visible`/`VisibleChanged` respects `Disabled`
- **`DatePicker`** - Single-value field with a calendar suffix opening a dropdown panel. Bind with `@bind-Value` (`DateTime?`); `Mode` (`DatePickerMode`: `Date` default — a one-month calendar with a month/year quick-select header; `Month` — a year header over a 3x4 month-button grid; `Time` — hour/minute/second selects over an OK button; `DateTime` — the day calendar with that same time row and OK button appended below it; `Year` — a decade header (prev/next-decade nav + a static "2020-2029" label) over a 3x4 year-button grid, 10 of the decade plus 2 dimmed adjacent-decade years, all reusing the month grid's `wss-picker-month-btn`/`wss-picker-month-grid` classes so `wss-picker.js` needed no changes; `Quarter` — `Month` mode's header verbatim over a single `wss-picker-quarter-grid` row of 4 quarter buttons; `Week` — the same panel as `Date` plus a leading week-number column, where a whole row, not a single day, is the selection unit) selects the panel and the commit-time normalization (`Date` keeps the date, `Month` normalizes to the 1st of the month at midnight, `DateTime` truncates to whole seconds, `Time` anchors to `DateTime.Today` plus the time-of-day, `Year` normalizes to January 1st at midnight, `Quarter` normalizes to the quarter's 1st day at midnight, `Week` normalizes to that week's first day, per `FirstDayOfWeek`, at midnight). Picking a day/month/year/quarter/week (or typing text + Enter) commits and closes; in `Time`/`DateTime` mode the time selects — and, in `DateTime` mode, a day click — commit immediately without closing, since the user may still want to adjust the other part, and the new small primary OK button (`wss-picker-ok`) is the close signal instead; `Min` / `Max` (checked at each mode's own granularity — month/year/quarter/week respectively for `Month`/`Year`/`Quarter`/`Week`, date granularity in `DateTime` mode, ignored entirely in `Time` mode; in `Week` mode this only guards the commit — a day button inside a merely partially-out-of-range week still enables and clicks normally, since the click commits that week's start, not the clicked day), `DisabledDate` (`Func<DateTime, bool>?`, an extra predicate folded into the same per-mode granularity as `Min`/`Max` — day midnight in `Date`/`DateTime`/`Week`'s own day buttons, the month/quarter/year start in those modes, the WEEK START (not the clicked day) for `Week`'s own commit guard; a `Week`-mode day click additionally re-checks that week-start guard explicitly, since an arbitrary predicate — unlike `Min`/`Max` — can reject the week start while leaving individual day buttons enabled), `DisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`, disables specific hour/minute/second VALUES in `Time`/`DateTime` mode's time row via a `DisabledTimeParts` record of `Hours`/`Minutes`/`Seconds` collections — invoked once per render of the row with `Value`'s date part, or null when `Value` is null, and once per commit guard; a listed value rejects a select-change or typed-text commit, reverting like a `Min`/`Max` rejection), `HideDisabledTimeOptions` (default false — omits a `DisabledTime`-disabled option from its select entirely instead of rendering it `disabled`; the select's own CURRENT value always renders regardless, selected and `disabled` too if applicable, so a select can never silently show a value that isn't the one actually bound), `ShowSeconds` (default true — false drops the seconds select from `Time`/`DateTime` mode's time row entirely and normalization zeroes the second on every commit), `HourStep`/`MinuteStep`/`SecondStep` (default 1 — steps the matching select's option list to 0/step/2×step/... up to 23/59/59, clamped to a minimum of 1; NEVER-JUMP: an off-lattice bound value's own option still renders, selected, composing with `DisabledTime`'s own never-jump the same way — step-filter first, then disable/hide), `Use12Hours` (default false — renders the hour select in 12-hour form, `12, 1, 2, ... 11` for the currently displayed AM/PM period with option VALUES still 24h, plus a trailing period select; `Value` always stays 24-hour, changing the hour commits its own 24h value, changing the period re-commits the current hour shifted into the other one via `hour % 12 + (isPM ? 12 : 0)`; `HourStep` still applies in 24h space), `Format` / `Placeholder` (both `string?`, null picks `Mode`'s default — `MM/dd/yyyy`/"Select date" for `Date`, `MM/yyyy`/"Select month" for `Month`, `MM/dd/yyyy` plus `Time`'s own string/"Select date" for `DateTime`, `HH:mm:ss`/"Select time" for `Time` (`ShowSeconds` false drops `:ss`; `Use12Hours` switches to `h:mm tt`/`h:mm:ss tt`), `yyyy`/"Select year" for `Year`, "Select quarter" for `Quarter`, "Select week" for `Week`), `AllowClear`, `Width`, `Size` (`SelectSize`: `Default`/`Small`/`Large`, adds `wss-picker-sm`/`wss-picker-lg` to the outer wrapper, mirroring `Select`'s own size classes; `Default` adds no class), `FirstDayOfWeek` (`Date`/`DateTime`/`Week` modes only), `ShowWeekNumbers` (default false — adds the same week-number column to `Date`/`DateTime` mode with no other behavior change; a day click there still commits that day, not its week; `Week` mode always shows the column regardless), `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel` (accessible names for the time/period selects, the last defaulting to "AM/PM"), `PrevDecadeLabel`/`NextDecadeLabel` (default "Previous decade"/"Next decade", `Year` mode's header), `OkText` (default "OK") — the single-value sibling of `DateRangePicker`, sharing its calendar internals and outside-click/Escape close behavior. `Quarter` mode has no .NET format token for its quarter digit: with `Format` left null the input displays/parses `yyyy-Qn` (e.g. "2026-Q3", also accepting "2026Q3" and a case-insensitive "q") via a hand-rolled special case instead of `ToString`/`TryParseExact`; a plain typed date still normalizes to its own quarter; setting `Format` explicitly falls back to formatting the raw bound value verbatim, so a custom format can't render the quarter number itself. `Week` mode is the same kind of special case for its week number: with `Format` left null the input displays/parses `yyyy-Www` (e.g. "2026-W07", the week-start's own calendar year; also accepting "2026W7" and a case-insensitive "w"); a plain typed date still normalizes to its own week start; setting `Format` explicitly is the same verbatim fallback as `Quarter`. Footer affordances: `ShowToday` (default **true**, matching AntD's `showToday`; set false to drop the footer row; `Date`/`Month`/`Quarter`/`Year`/`Week` mode only) adds a `TodayText` (default "Today") link button that commits `DateTime.Today`, mode-normalized, and closes; `ShowNow` (default false, `Time`/`DateTime` mode only) adds a `NowText` (default "Now") link into the EXISTING time-row footer, left of OK, committing `DateTime.Now` mode-normalized WITHOUT closing (OK remains that footer's close signal); both render DISABLED, not hidden, when `Min`/`Max`/`DisabledDate` rejects the normalized commit. `Presets` (`IReadOnlyList<DatePickerPreset>?`, `DatePickerPreset(label, resolveFunc)` — same resolved-at-click-time contract as `DateRangePreset`) renders the SAME `wss-picker-presets`/`wss-picker-preset` sidebar `DateRangePicker` uses; clicking one resolves, mode-normalizes, commits (a guard rejection no-ops), and ALWAYS closes — even in `Time`/`DateTime` mode, where a preset is a complete pick unlike those modes' own incremental time selects. `ExtraFooter` (`RenderFragment?`) renders arbitrary content in its own `wss-picker-extra-footer` strip above the footer row (or alone, in a mode with no footer of its own) in every mode — AntD's `renderExtraFooter`. `DefaultViewDate` (`DateTime?`, AntD's `defaultPickerValue`) sets the panel's initial view when `Value` is null; a set `Value` always wins
- **`DateRangePicker`** - Composite start → end date-range field opening a dropdown with an optional preset sidebar. Bind with `@bind-Start` / `@bind-End` (`DateTime?`); `Mode` (`DatePickerMode`: `Date` default, `Week`, `Month`, `Quarter`, `Year` — a pair of consecutive LINKED panels at that granularity (two one-month calendars, two years of months, two years of quarters, or two decades of years), both endpoints normalizing to the unit's own start — midnight/1st-of-month/1st-of-quarter/January 1st/week-start per `FirstDayOfWeek`; `DateTime`/`Time` abandon the dual-panel layout for a SINGLE panel that edits one endpoint at a time (AntD's `showTime` shape): a day click (`DateTime`) or a time-row change sets the ACTIVE endpoint's pending value without committing, and an OK button confirms it — once both endpoints are resolved it commits them together (swapping a backwards pair) and closes) selects the panel layout and per-endpoint normalization; `Min` / `Max` and `DisabledDate` (`Func<DateTime, bool>?`, checked at `Mode`'s own granularity, same contract as `DatePicker.DisabledDate`); `StartDisabledTime` / `EndDisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`, per-endpoint hour/minute/second restrictions for `DateTime`/`Time` mode's time row — the START/END split lets each side reject different values) and `HideDisabledTimeOptions`; `ShowSeconds` (default true), `HourStep`/`MinuteStep`/`SecondStep` (default 1), `Use12Hours` (default false), `OkText` (default "OK") for that time row — same contracts as `DatePicker`'s own; `Format` (`string?`, null picks `Mode`'s default — same per-mode values as `DatePicker.Format`, including the `yyyy-Qn`/`yyyy-Www` shorthand for `Quarter`/`Week`) with `StartPlaceholder` / `EndPlaceholder` (null default: the uppercased effective format); `AllowClear`, `Width`, `Size` (`SelectSize`, same contract as `DatePicker.Size`), `FirstDayOfWeek`, `ShowWeekNumbers` (default false — adds a week-number column beside the day grid(s) in `Date` mode with no change to day-click semantics; `Week` mode always shows it); `Presets` (`IReadOnlyList<DateRangePreset>?` — a label plus a range-resolving `Func` evaluated at click time, or a fixed-dates overload; a click clamps both ends into `Min`/`Max`, normalizes to `Mode`'s granularity, preserves time-of-day in `DateTime`/`Time` mode, and no-ops instead of committing if the normalized result is `DisabledDate`-rejected); `ExtraFooter` and `DefaultViewDate` (mirror `DatePicker`'s own — `ExtraFooter` renders in every mode, including above the `DateTime`/`Time` session's OK footer). Deliberately has no `ShowToday`/`ShowNow`: AntD's `RangePicker` has neither — `Presets` is its quick-pick affordance instead. Picking the second unit of a range (two-click, swapping a backwards pick) or a preset commits and closes; typed input in either field commits on Enter/blur; a `Time`-mode commit keeps each endpoint's own already-committed date part (today when unset) rather than re-stamping to the literal current day. Accessible names mirror `DatePicker`'s convention, doubled per endpoint where relevant: `StartInputLabel`/`EndInputLabel`, `DialogLabel`, `MonthSelectLabel`/`YearSelectLabel`, `ClearLabel`, `PresetsLabel`, `PrevMonthLabel`/`NextMonthLabel`, `PrevYearLabel`/`NextYearLabel`, `PrevDecadeLabel`/`NextDecadeLabel`, `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel`. `OnStartParseError` / `OnEndParseError` (`EventCallback<string>`) are raised with the offending text when a typed commit in that endpoint's input fails to parse at all — never for a well-formed value the picker merely rejects on `Min`/`Max`/`DisabledDate`/`*DisabledTime` grounds; with no handler attached the text is silently reverted as before (`EditDateRange` uses them to raise a validation message the picker itself has no concept of). Shares `DatePicker`'s calendar internals, JS-degradation contract, and outside-click/Escape close behavior
- **`Table<TItem>`** - Data table with `Column` / `PropertyColumn` / `ActionColumn`, row selection, paging (pager placement via `PagerPosition` = Top/Bottom/Both and alignment via `PagerAlign`), and column sorting (`Sortable="true"` on a `PropertyColumn` — non-comparable types degrade to non-sortable; or a `SortBy` comparison on any column). Columns may be conditionally rendered (`@if`). `RowDetail` (a `RenderFragment<TItem>`) adds expandable rows: a leading chevron column toggles the template as a full-width row beneath each row (e.g. a nested child `Table`); expansion is keyed by `RowKey` identity so it survives paging/sorting. `Column.TitleContent` replaces a plain `Title` with templated header content (e.g. a title plus a `LabelTooltip` info icon). `Loading`, `IsRowSelectable`, `SelectionMode.Single`, controlled expansion (`ExpandedRowKeys`/`OnExpand`), `ExpandRowByClick`/`OnRowClick`, `Column.Ellipsis`, `EmptyContent`, `FooterContent`, column filtering (`Column.FilterOptions`/`OnFilter`, `Table.OnFilterChanged`), and `ScrollY` (a scrollable body with a sticky header) round out AntD 4.x parity (see [Table parity features](#table-parity-features-tabletitem))
- **`Tabs` / `Tab`** - Underline tab strip with an optional bordered count chip per tab (`Count`); bind with `@bind-ActiveKey` (a `string?`). Tabs with `ChildContent` show the active pane below the strip; content-less tabs act as a bare filter strip. ARIA tabs pattern with automatic activation (arrows move + select with wrapping, roving tabindex; Home/End deliberately unhandled — Blazor can't `preventDefault` per key). `TabBarExtraContent` adds a right-aligned strip slot, `Centered` centers the tab buttons, and `Type="TabsType.Card"` switches to AntD's boxed card-style tabs (CSS-only)
- **`SearchInput`** - Search field: optional leading addon label chip (`AddonLabel`/`AddonContent`), text input (`@bind-Value`, per-keystroke), and a search button — `OnSearch` fires on Enter and on the button. Pill-rounded ends by default (`--wss-search-radius` to square them). `Loading` swaps the button's search glyph for a spinning `LoadingOutlined` icon and sets `disabled` + `aria-busy="true"` on the button (the text input itself stays enabled); Enter and the button both no-op while `Loading` is true. `AllowClear` adds a clear × button; `EnterButtonText` swaps the icon-only button for a labeled primary button (AntD's `enterButton="Search"`)
- **Toasts & notifications** - two paths with identical rendering: **scoped / Server-safe** (`IMessageService` / `INotificationService` via `builder.Services.AddWssControlsToasts()` + `<MessageContainer />` / `<NotificationContainer />`), or **registration-free static for WASM** (`WasmMessageService` / `WasmNotificationService` + `<WasmMessageContainer />` / `<WasmNotificationContainer />`). On Blazor Server use the scoped path — the static `Wasm*` services hold process-static state that would bleed across users. The notification containers accept `Placement` (`TopRight` default / `TopLeft` / `BottomRight` / `BottomLeft`) — set per container instance (render-tree-scoped, MFE-safe), not on the service. On both services every `Success`/`Info`/`Warning`/`Error` (and `Loading`, on messages) returns the toast's `Guid` — pass it to `Remove(id)` to dismiss a sticky (`Duration=0`) toast when the work it announced completes.
- **Hover tooltips (`data-tooltip`)** - not a component: a `data-tooltip="..."` attribute on any element, styled by `wss-controls.css` (arrow + bubble, slide-in animation, keyboard-focus support). Pair with `wss-tooltip.js` for cursor-aware auto-placement — see below.

> `Icon`, `Button`, `Checkbox`, and `Tag` are intentionally **not** part of this library.

#### Hover tooltips (`data-tooltip`)

Add `data-tooltip="Some help text"` to any element for a styled hover/focus tooltip — never the native `title` attribute, so every tooltip in the app gets consistent styling:

```razor
<button data-tooltip="Refresh the list">
    <RefreshIcon />
</button>
```

CSS alone renders it below the element with a slide-in animation, an arrow, and `:focus-visible` support (keyboard users get it too). Link the optional script for automatic placement — it flips above when the element sits in the lower part of its container, and shifts left/right near a side edge, so authors never have to pick a direction by hand:

```html
<script src="_content/WssBlazorControls/wss-tooltip.js"></script>
```

It re-derives placement on every hover/focus (via event delegation, so dynamically-added elements are covered with no extra wiring) and aims at the nearest clipping ancestor or recognized panel boundary (`wss-modal` / `wss-drawer` / `wss-popover`) instead of the screen — so a tooltip inside a `Modal` stays within the modal instead of running past its edges. Two deliberate limits on what counts as that frame: `<body>` is never accepted, however it's styled (`body { overflow-x: hidden }` is near-ubiquitous boilerplate, and body's rect is the whole *document* — as tall as the page, top well above the viewport once scrolled — which would answer the flip test against the document rather than the screen), and whatever frame *is* chosen is intersected with the viewport, since only its visible part can hold the bubble. A page that genuinely wants a body-sized frame gets the viewport, which is the same box for an unscrolled page. To force a specific direction yourself (and opt that element out of auto-placement), apply one of the placement classes directly: `wss-tooltip-top`, `wss-tooltip-left`, `wss-tooltip-right`, or the vertically-centered `wss-tooltip-side-left` / `wss-tooltip-side-right` (manual-only — the auto-placer never assigns these two). Tooltips are hidden entirely on touch devices (`hover: none`), since there is no hover to trigger them.

The same script also places the form controls' `LabelTooltip` popover (the label help icon), using the same placement classes — that's the one shared placement engine for both tooltip kinds. `LabelTooltip` lazily imports the module itself on first render, so the script tag above is only needed for `data-tooltip` usage; the module guards against being loaded both ways.

Theming uses the same `--wss-*` tokens as the rest of the kit (`--wss-color-bg`, `--wss-color-text`, `--wss-color-border`, `--wss-radius`, `--wss-shadow`), plus two tooltip-specific knobs: `--wss-tooltip-gap` (resting distance from the element to the pointer tip, default `24px`) and `--wss-tooltip-z-index` (default `10000`, matching `--edit-tooltip-z-index`).

#### Pill filter variant (`Select` / `EditSelectSearch`)

`Variant="SelectVariant.Pill"` turns the Select trigger into a fully-rounded outlined filter button that hugs its content — the "All shipments ⌄" pattern. Pair it with `Prefix` for a leading icon, and usually `ShowSearch="false"` / `AllowClear="false"` so it reads as a button. The dropdown gets softer corners, content-driven width, and conveys the current value by the bold/tinted row alone (no checkmark). Behavior is unchanged: keyboard navigation, type-ahead, outside-click and Escape close.

```razor
<Select TValue="string" @bind-Value="_shipmentFilter" Options="_shipmentOptions"
        Variant="SelectVariant.Pill" ShowSearch="false" AllowClear="false">
    <Prefix><svg ... aria-hidden="true">...</svg></Prefix>
</Select>
```

Theming: the whole trigger (label, border, chevron, focus ring) derives from one knob — override `--wss-select-pill-color` at any scope (`--wss-select-pill-border` / `--wss-select-pill-bg` are finer-grained overrides). The selected row tint is the kit-wide `--wss-color-bg-selected`:

```css
.my-filters {
    --wss-select-pill-color: #1c4a3f;   /* label, border, chevron, focus ring */
    --wss-color-bg-selected: #d9e8e2;   /* selected dropdown row */
}
```

`Prefix` also works on the outlined variant; `EditSelectSearch` and `EditMultiSelect` both forward `Variant` and `Prefix`, so `Pill` and `Borderless` are reachable in `Multiple`/`Tags` mode too (10.7.0 — `EditMultiSelect.Variant` is new).

#### Select parity features (`Select` / `EditSelectSearch` / `EditMultiSelect`)

All additive — existing markup is unchanged when these parameters go unused. `EditSelectSearch` and `EditMultiSelect` forward every one of them to the engine, `Variant` included as of 10.7.0 (grouping needs no wrapper wiring — it rides along on the `Options` they already forward).

- **`Loading` (`bool`, default false)** and **`ShowArrow` (`bool`, default true)** — `Loading` shows a spinner in the arrow's slot (and marks the control `aria-busy="true"`) even when `ShowArrow="false"`, matching Ant Design; `ShowArrow="false"` alone just hides the chevron. `ShowArrow` defaults to **true** (unlike Ant Design, which hides the arrow by default for a searchable multi-select) — kept always-on here so existing markup's DOM stays byte-identical.
- **`SelectOption.Group` (`string?`)** — options render in `Options` order; a non-interactive header (`role="presentation"`, `aria-hidden`, never `role="option"`) appears once before the first option of each *contiguous* run sharing a `Group` value (the same group name in two separate runs gets two headers, mirroring a flat option list rather than a pre-nested `OptGroup` array). Keyboard navigation (arrows, Home/End, type-ahead) skips header rows entirely; a header is shown only while at least one of its options survives the current filter.
  ```csharp
  var options = new List<SelectOption<string?>>
  {
      new("us", "United States") { Group = "North America" },
      new("ca", "Canada") { Group = "North America" },
      new("gb", "United Kingdom") { Group = "Europe" },
  };
  ```
- **`FilterOption` (`Func<string, SelectOption<TValue>, bool>?`)** — replaces the default case-insensitive `Label.Contains` match in `RebuildFiltered` when set, including when the search text is empty. Pass `(_, _) => true` to disable client-side filtering entirely for a pure server-driven `OnSearch` flow — every option in `Options` stays visible on the assumption the server already filtered them before reassigning `Options`. Tracked by reference like `Options`/`Values`: reassigning it (even mid-open) re-filters immediately. Prefer a cached/readonly delegate — an inline lambda is a new reference every render and re-filters each parameter set, correct but wasteful against a huge option list.
- **`EmptyContent` (`RenderFragment?`)** — a richer alternative to `EmptyText` for the no-match state; wins over `EmptyText` when set.
- **`DropdownFooter` (`RenderFragment?`)** — Ant Design's `dropdownRender` equivalent: renders pinned after the option list, outside the virtualized list and outside listbox/option semantics (`role="presentation"`). Clicks inside it never select an option or close the dropdown on their own (propagation is stopped automatically) — wire your own handler, e.g. a button's `@onclick`, for any action including closing.
  ```razor
  <EditSelectSearch @bind-Value="model.Country" Options="_countries">
      <EmptyContent><em>No matching country.</em></EmptyContent>
      <DropdownFooter>
          <button type="button" @onclick="AddCustomCountry">+ Add country</button>
      </DropdownFooter>
  </EditSelectSearch>
  ```
- **Controlled `Open`/`OpenChanged` (`bool` / `EventCallback<bool>`)** — two-way bindable via `@bind-Open`. While `OpenChanged` has a delegate (the controlled case), an externally-changed `Open` routes through the exact same internal open/close path as user interaction, so JS placement, focus, and scroll-into-view all still run; every open/close (external or internal) raises `OpenChanged` back, and an echo of a value the component just raised is recognized and ignored (no re-open/close loop). With no delegate on `OpenChanged` (the default, uncontrolled case) `Open` is inert and `DefaultOpen` alone governs the initial state, exactly as before. **`Disabled` always wins**: an external `Open="true"` is ignored while `Disabled`, and a `Disabled` flip on an already-open dropdown closes it through the same path (`OpenChanged` still fires) — a disabled `Select` can never render its dropdown open, controlled or not.
  ```razor
  <button @onclick="() => _open = !_open">Toggle</button>
  <EditSelectSearch @bind-Value="model.Country" Options="_countries" @bind-Open="_open" />
  ```

`wss-select.js`'s `placeDropdown` also clamps horizontally now, mirroring the existing above/below flip. The dropdown normally hangs from the wrapper's left edge (CSS `left: 0`); one wider than its trigger — long option labels, or the `Pill` variant's content-driven width up to its 320px max — that would run off the right edge of the viewport is shifted left as a `left` offset from the wrapper, but only as far as the viewport's own left margin. Clamped on **both** sides deliberately: pushing the dropdown's left edge off-screen to fit its right edge in makes the start of every option unreachable, which is strictly worse than clipping the right. There is no movement at all whenever there's room, so the plain CSS default still describes the common case, and the inset margin drops to zero for a dropdown too wide to inset (a full-bleed select on a phone legitimately produces one as wide as the screen). No CSS/markup change — degrades to the `left: 0` default without JS.

#### `Mode` example (`DatePicker` / `DateRangePicker`)

`Mode` (`DatePickerMode`) works the same way on both pickers — pick a granularity and the bound value(s) normalize to it. A month-range picker, with no separate "month range" component needed:

```razor
<DateRangePicker @bind-Start="_periodStart" @bind-End="_periodEnd" Mode="DatePickerMode.Month" />
```

Picking January in the left panel and March in the right commits `_periodStart` = Jan 1 and `_periodEnd` = Mar 1 (both midnight). `EditDateRange`/`EditDate` forward the same `Mode` parameter for a validated form field.

#### Pagination parity features (`Pagination`)

All additive — existing markup is unchanged when these parameters go unused.

- **`ShowTotal` (`Func<(int Start, int End, int Total), string>?`)** — renders the AntD-style leading total text ("1-10 of 200 items") before the prev button, formatted by the callback you supply. Null (default) renders nothing.
  ```razor
  <Pagination Total="95" PageSize="10" @bind-Current="_page"
              ShowTotal="@(w => $"{w.Start}-{w.End} of {w.Total} items")" />
  ```
- **`PageSizeOptions` (`int[]?`)** — renders a dependency-free native `<select>` size-changer after the next-page button (no `Select<T>`/JS module pulled in). `PageSize`/`PageSizeChanged` are two-way bindable (`@bind-PageSize`) to support it — existing one-way `PageSize="10"` usage with no handler is unaffected. The current `PageSize` is folded into the option list even when absent from `PageSizeOptions`, so the select never shows a mismatched value. Changing the size re-clamps `Current` to keep roughly the same data window in view: new `Current` = the old first-visible item's 0-based index ÷ the new size, + 1. `PageSizeLabelFormat` (default `"{0} / page"`) and `PageSizeSelectLabel` (accessible name, default "Page size") localize it.
  ```razor
  <Pagination Total="95" @bind-PageSize="_pageSize" @bind-Current="_page" PageSizeOptions="@(new[] { 10, 20, 50 })" />
  ```
- **`ShowQuickJumper` (`bool`)** — adds a "Go to [ ]" native text input after the size-changer (or the next-page button, if no size-changer). Enter commits the typed page number (clamped to `[1, PageCount]`) via `CurrentChanged` and clears the input. `QuickJumperLabel` (default "Go to") and `QuickJumperInputLabel` (accessible name, default "Go to page") localize it.
- **`Small` (`bool`)** — AntD's compact pagination size: smaller buttons and tighter spacing, CSS-only (`wss-pagination-sm`).

#### Table parity features (`Table<TItem>`)

All additive — existing markup is unchanged when these parameters go unused.

- **`Loading` (`bool`)** — shows a translucent mask + spinner over the whole component: both pagers plus the table body (rows stay rendered beneath it, and the pagers are dimmed and click-inert while the mask is up) — and sets `aria-busy="true"` on the root element. Pure CSS/markup, no JS.
- **`IsRowSelectable` (`Func<TItem, bool>?`)** — per-row selection predicate; null (default) means every row is selectable. A rejected row's checkbox/radio renders `disabled` and is excluded from the header "select all" — both which rows it toggles and the indeterminate/all-selected math count only selectable rows on the page. The header checkbox itself renders `disabled` only once `IsRowSelectable` rejects every row on the page (never when `IsRowSelectable` is unset).
- **`SelectionMode` (`Multiple` default / `Single`)** — `Single` renders radio-semantics selection instead of the checkbox column (one native `<input type="radio">` per row, all sharing one group so picking a row deselects any other) and an empty header cell in place of "select all" (kept only for column alignment — there's no "select all" for an exclusive choice). `SelectedItems`/`SelectedItemsChanged` are unchanged either way (0-or-1 items in `Single` mode).
- **Controlled expansion: `ExpandedRowKeys`/`ExpandedRowKeysChanged`** — layers over the existing uncontrolled expansion set (keyed by `RowKey`); reassign a new collection to drive expansion from the parent, same immutable-parameter contract as `SelectedItems`. **`OnExpand`** (`EventCallback<(TItem Item, bool Expanded)>`) raises on every toggle regardless of control mode.
- **`ExpandRowByClick` (`bool`)** — clicking anywhere on a row (other than the selection checkbox/radio, the expand chevron, or inside an `ActionColumn` cell — all of which stop propagation) toggles that row's `RowDetail` expansion, the same toggle the chevron performs.
- **`OnRowClick` (`EventCallback<TItem>`)** — raised on a row click with the same propagation guards as `ExpandRowByClick`. Always raised regardless of `ExpandRowByClick`; when both are set, a click toggles expansion *and* raises `OnRowClick`.
  > **Note:** those propagation guards only cover the selection checkbox/radio cell, the expand chevron cell, and `ActionColumn` cells. Interactive content placed in a plain `Column`'s `ChildContent` does **not** get an automatic guard — its clicks bubble up and reach `OnRowClick`/`ExpandRowByClick` like any other cell click. Put row-action buttons in `ActionColumn`, or add `@onclick:stopPropagation="true"` yourself, when mixing interactive content into a plain `Column` alongside `OnRowClick`/`ExpandRowByClick`.
- **`Column.Ellipsis` (`bool`, on the `Column<TItem>` base)** — truncates overflowing cell text with an ellipsis (CSS-only: `white-space: nowrap; overflow: hidden; text-overflow: ellipsis`). Since AntD's ellipsis needs a bounded column width to actually clip, the `Table` switches to `table-layout: fixed` automatically once ≥1 column requests it (untouched tables keep the existing auto layout). `PropertyColumn` additionally wraps its computed text in a `title`-bearing `<span>` so the truncated value stays discoverable on hover; a custom `Column`/`ActionColumn`'s `ChildContent` is arbitrary markup, not a string the base class computed, so it gets the truncation styling only, no `title`.
  ```razor
  <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)" Ellipsis="true" />
  ```
- **`EmptyContent` (`RenderFragment?`)** — richer alternative to `EmptyText` for the no-rows placeholder; wins over `EmptyText` when set.
- **`FooterContent` (`RenderFragment?`)** — a summary/footer row rendered in a `<tfoot>` after the body, unaffected by paging/sorting. You supply the full `<tr>`/`<td>` structure (typically reusing the `wss-table-cell` class) and own the `colspan`.
  ```razor
  <Table TItem="Row" DataSource="_rows">
      <ChildContent>
          <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)" />
          <PropertyColumn TItem="Row" TProp="decimal" Title="Price" Property="@(r => r.Price)" Format="C2" />
      </ChildContent>
      <FooterContent>
          <tr><td class="wss-table-cell">Total</td><td class="wss-table-cell">@_rows.Sum(r => r.Price).ToString("C2")</td></tr>
      </FooterContent>
  </Table>
  ```
  As with `RowDetail`, once any explicit fragment element (`FooterContent`, `EmptyContent`, `RowDetail`) is present alongside loose column children, the columns must sit inside a `<ChildContent>` wrapper — Razor rejects mixing explicit parameter elements with loose child content.
- **Column filtering: `Column.FilterOptions`/`OnFilter`/`FilterMultiple`** — a funnel icon appears in the header cell (after the sort control, when both are present — clicking it never triggers a sort) whenever a column sets `FilterOptions` (`IReadOnlyList<TableFilterOption>`, each a `Text`/`Value` pair) **and** `OnFilter` (`Func<TItem, string, bool>`, given one selected value); either alone renders no filter UI. A sortable + filterable header stays inside its cell even when the column is narrow (`table-layout: fixed`, e.g. via `Ellipsis` elsewhere in the table) — the sort label truncates with an ellipsis instead of pushing the filter button out. Clicking the funnel opens a checkbox dropdown (`FilterMultiple` true, the default) or a single-select radio dropdown (`FilterMultiple` false); **OK** applies the checked/selected values and closes, **Reset** clears that column's filter immediately, and clicking outside the dropdown closes it *without* applying whatever was checked (AntD only applies on OK) — neither OK nor Reset resets the current page when the applied selection doesn't actually change (e.g. OK with nothing (re-)ticked, or Reset on an already-empty filter). A row passes a column's filter when `OnFilter` returns true for **any** of its selected values (OR within a column); a row must pass **every** filterable column to render (AND across columns). Filtering runs before sorting and paging, and — like paging — a selected row that a filter narrows out of view stays in `SelectedItems`, it just isn't rendered. Client-side only: like `Sortable`, `FilterOptions`/`OnFilter` narrow whatever's currently in `DataSource` — under the server-paging compose pattern below, filtering server-side is on you (send the selected values in your own request). This is uncontrolled filter state only (no `filteredValue`-style fully-controlled equivalent): observe changes via **`Table.OnFilterChanged`** (`EventCallback<(Column<TItem> Column, IReadOnlyList<string> SelectedValues)>`), raised after every apply/reset that actually changes the applied selection, and also when a column that was actively filtering rows drops out of the rendered set (e.g. an `@if` hiding it) — its filter is force-cleared along with it, so the same "now empty" payload fires there too. Not raised on open, an outside-click/Escape discard, or a no-op OK/Reset. `FilterButtonLabelFormat` (default `"Filter {0}"`), `FilterResetLabel` (`"Reset"`), and `FilterOkLabel` (`"OK"`) localize the button/dropdown text.
  ```razor
  <Table TItem="Row" DataSource="_rows" OnFilterChanged="@(e => Console.WriteLine($"{e.Column.HeaderText}: {string.Join(", ", e.SelectedValues)}"))">
      <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)"
                      FilterOptions="@(new[] { new TableFilterOption("Alice", "Alice"), new TableFilterOption("Bob", "Bob") })"
                      OnFilter="@((Row r, string v) => r.Name == v)" />
  </Table>
  ```
- **`ScrollY` (`string?`)** — bounds the table body to a fixed height with its own vertical scrollbar and a sticky header (AntD's `scroll.y` equivalent): any CSS length (`"320px"`). Null (default) renders the existing unconstrained wrapper. Deliberately scoped to the table's own wrapper only — a header that stays fixed while the whole *page* scrolls (viewport-level sticky) is out of scope. A column filter dropdown that would otherwise be clipped by the `ScrollY` wrapper's overflow escapes it via `position: fixed` (JS), keeps tracking its trigger across page scroll/resize while it stays open, and paints above `Loading`'s mask even while both are active together; without JS it stays CSS-anchored (may clip in that combination — the documented no-JS fallback).
  ```razor
  <Table TItem="Row" DataSource="_rows" ScrollY="320px">
      <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)" />
  </Table>
  ```

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

`Pagination` is a controlled component (`Total` / `Current` / `PageSize` + `CurrentChanged`), so it shows the correct page count from the server total and raises `CurrentChanged` when the user picks a page. Handle **sorting** the same way — pass the sort field/direction into your request rather than using the `Table`'s built-in `Sortable`, which only orders the page already loaded; **column filtering** (`FilterOptions`/`OnFilter`) has the same limitation — it only narrows the current page's `DataSource`, so send the selected values in your own request too. A runnable example (with a simulated server) is in the `/uikit` gallery.

## Component Features

All form controls implement the `IEditControl` interface and provide:

- **Identity Management**: `Id`, `IdPrefix` for unique identification
- **Display Control**: `IsEditMode`, `IsDisabled`, `IsHidden`
- **Labeling**: auto-generated from the property name, `[DisplayName]` for constant labels, or the `Label` parameter for dynamic text — see [Labeling: how to choose](#labeling-how-to-choose). `Description` is plain text (HTML-encoded when rendered).
- **Placeholders** (where the control renders one): the control's own `Placeholder` parameter → the bound property's `[Placeholder]`/`[Display(Prompt)]` → the control's built-in default — see [Model-declared placeholders](#model-declared-placeholders-placeholder)
- **Bounds** (where the control renders one): the control's own `Min`/`Max` parameter → the bound property's `[MinValue]`/`[MaxValue]` → `[Range]` → none — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue)
- **Other field-semantic constants** (autocomplete token, numeric step, boolean display text, textarea sizing, file constraints, max length, password mode, display format, date input type): the control's own parameter → a model attribute (`[Autocomplete]`/`[Step]`/`[BoolText]`/`[Rows]`/`[FileConstraints]`, or a standard DataAnnotations attribute the control already honors as a fallback — `[StringLength]`/`[MaxLength]`, `[DataType(Password)]`, `[DisplayFormat]`, `[DataType(Date/DateTime/Time)]`) → the control's built-in default — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
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
        <EditString @bind-Value="model.Name" />
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

#### Button-style radio group (`OptionType="Button"`)

`EditRadioString` and `EditRadioEnum` also render as Ant Design's segmented "button" look — joined bordered buttons instead of plain radios:

```razor
<EditRadioString @bind-Value="model.Department"
                 Options="@departments"
                 OptionType="RadioOptionType.Button"
                 ButtonStyle="RadioButtonStyle.Solid"
                 Size="SelectSize.Large" />
```

- `OptionType` (`RadioOptionType`, default `Default`) — `Button` switches to the segmented look. Same `InputRadio`/`InputRadioGroup` plumbing and keyboard behavior underneath (a visually-hidden input + a sibling `<label>` styled as the button, matching the styled-checkbox technique — native `for`/`id`, not `:has()`).
- `ButtonStyle` (`RadioButtonStyle`, default `Outline`) — only applies in button mode. `Outline` tints the checked button's border/text with the primary color; `Solid` fills its background instead.
- `Size` (`SelectSize`, default `Default`) — only applies in button mode; reuses the same enum as `Select`/`EditString`/etc. (`Small`/`Default`/`Large`).
- Button mode is inherently horizontal — `IsHorizontal` is ignored when `OptionType="Button"`.
- Composes with `HasOther`/`HasOtherOption` (the Other button joins the row; its free-text input still renders as a normal `<input>` below, not inside the button row) and with `IsOptionDisabled` (a disabled option's button dims and refuses interaction, same as a disabled plain radio).
- Default mode (`OptionType` unset) renders byte-identical markup to before — this is a fully opt-in mode, not gated behind `.edit-theme` (it carries its own styling, like `Select`/`EditDate`).

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

### Per-option disabling

`EditRadioString`, `EditRadioEnum`, `EditCheckedStringList`, and `EditCheckedEnumList` all take an `IsOptionDisabled` predicate — called once per rendered option, disabling that option in addition to (not instead of) the whole-group `IsDisabled`:

```razor
<EditRadioString @bind-Value="model.Department"
                 Options="@departments"
                 IsOptionDisabled="@(d => d == "Sales")" />
```

The predicate's parameter type matches the control: `Func<string, bool>?` for the string controls, `Func<TEnum, bool>?` for the enum controls. Null (the default) disables nothing beyond `IsDisabled`. `EditRadioString`'s built-in "Other" radio has no corresponding `Options` entry, so the predicate never applies to it; `EditRadioEnum`'s "Other" (the last enum value, when `HasOtherOption` is set) is a real enum value and composes normally.

### Custom-Styled Checkbox (border-radius)

No current browser (Chromium or Safari/WebKit) honors `border-radius` on a native `<input type="checkbox">` once `accent-color` is set (see [caniuse: accent-color](https://caniuse.com/mdn-css_properties_accent-color)). When a design spec calls for a shaped checkbox, opt in with `UseStyledCheckbox`:

```razor
<EditBool @bind-Value="model.AcceptedTerms" UseStyledCheckbox="true" />
```

The real `<input>` stays in the DOM — focusable, keyboard-operable, full native semantics — but is visually hidden; a sibling element draws the box, checked fill, checkmark, and focus ring in CSS (`.edit-checkbox-box` in `edit-controls.css`), styleable like any other element. Defaults to `null` (falls through to the app-wide switch below, ultimately `false`), so every existing `EditBool` renders exactly as before unless something in that chain turns it on.

`EditCheckedStringList` and `EditCheckedEnumList` take the same `UseStyledCheckbox` (`bool?`) parameter and apply it to every option's checkbox; the UI-kit `Table` (see [UI Kit](#ui-kit-non-form-controls)) takes it too, applied to the header/row selection checkboxes including the indeterminate "mixed" glyph.

#### Indeterminate ("mixed") state

`EditBool.Indeterminate` (`bool`, default `false`) is AntD's visual-only "mixed" checkbox state — useful for a "select all" checkbox whose children are only partially selected:

```razor
<EditBool @bind-Value="model.SelectAll" Indeterminate="@someButNotAllSelected" />
```

It never changes the bound value — only a real click does, exactly like AntD's `indeterminate` prop. There is no HTML attribute for it (it's a DOM property), so it's applied via JS after render through the same `wss-checkbox.js` helper the UI-kit `Table`'s header "select all" checkbox uses; with no JS runtime (server prerender, unit tests) the checkbox just shows its plain checked/unchecked state. Works with or without `UseStyledCheckbox` — the native checkbox gets the browser's own mixed-state dash, the styled checkbox draws the same unfilled-box-plus-centered-square look as `Table`'s.

#### Turning it on for a whole app or MFE

Setting `UseStyledCheckbox="true"` on every control individually doesn't scale. Instead, set it once — either on the cascaded `FormOptions` for one form/section, or on [`FormDefaults`](#formdefaults) for everything under an app or micro-frontend root — and leave the per-control parameter unset everywhere:

```razor
<FormDefaults UseStyledCheckbox="true">
    <Router AppAssembly="@typeof(App).Assembly">...</Router>
</FormDefaults>
```

Resolution per control (first non-null wins): the control's own `UseStyledCheckbox` parameter → the cascaded `FormOptions.UseStyledCheckbox` → the nearest enclosing `FormDefaults.UseStyledCheckbox` → the process-wide `FormOptions.DefaultUseStyledCheckbox` static (default `false`). `Table` has no `FormOptions` of its own, so it resolves through `FormDefaults` then the static only.

#### File upload parity features (`EditFile`)

```razor
<EditFile @bind-Value="model.Documents"
          AllowedExtensions="@([".pdf", "image/*"])"
          BeforeAdd="CheckServerSideDuplicate"
          Variant="EditFileVariant.Button" />

@code {
    async Task<bool> CheckServerSideDuplicate(IBrowserFile file) =>
        !await DocumentService.ExistsAsync(file.Name);
}
```

- **MIME types and wildcards.** `AllowedExtensions` now accepts three token shapes, mirroring the native `<input accept>`/Ant Design's `accept`: a bare/dotted extension (`"pdf"`/`".pdf"`, the original behavior), a full MIME type (`"application/pdf"`), or a MIME wildcard (`"image/*"`) — detected by whether the token contains `/`. Both the `<InputFile accept="...">` attribute and the validation logic honor all three; MIME matching reads `IBrowserFile.ContentType` (the browser-reported type) case-insensitively, not the file extension. Previously every token was dot-prefixed regardless of shape, so a MIME token like `"image/*"` became the meaningless `.image/*"` — silently rejecting every file and emitting an invalid `accept` attribute.
- **`BeforeAdd`** (`Func<IBrowserFile, Task<bool>>?`) — an optional async gate run once per file, after the built-in format/size/count/duplicate checks and before its bytes are buffered. Return `false` to reject the file; the rejection is reported the same way as the built-in checks, via the new `BeforeAddRejectedMessageFormat` (`"{0} was rejected."` by default, `{0}` = file name). Use it for checks the cheap built-in ones can't do — a server-side dedupe lookup, content sniffing beyond the extension/MIME check. An exception thrown by the hook propagates uncaught: that's a bug in the consumer's code, not a file rejection, so it's never swallowed into an upload-error message.
- **File size in the list.** Every selected file's row (both the edit-mode removable list and the read-only list) now shows its formatted size (the same `"10 MB"`/`"512 KB"`/`"900 B"` formatting the size-cap messages already used) in a muted span next to the file name. The empty-list state (no files selected) is unaffected — this only adds markup to rows that already exist once files are present.
- **`Variant="EditFileVariant.Button"`** (`EditFileVariant`: `Dropzone` default / `Button`) — swaps the dashed drag-and-drop card for a compact plain button (Ant Design's plain `Upload`, as opposed to `Upload.Dragger`), sized and styled like a normal button rather than a full-width dropzone card. `ButtonText` (`string`, default `"Select Files"`) sets its label. Built on the same invisible-`<InputFile>`-overlay technique as the dropzone, so keyboard/focus/click behavior match — Tab reaches the real file input, Enter/Space opens the file picker, and it unmounts at the `MaxFiles` cap exactly like the dropzone does. Drag-and-drop is intentionally not supported in this variant, matching Ant Design's plain `Upload`. All validation, caps, and messages apply identically to both variants; `Dropzone` (the default, unset `Variant`) renders byte-identical markup to before.

### Commit timing (`UpdateOn`)

`EditString`, `EditTextArea`, `EditNumber`, `EditDateNative`, and the "Other" free-text box on `EditRadioString`/`EditRadioEnum` take an `UpdateOn` (`UpdateTrigger?`) parameter controlling when the control writes back to the bound value:

```razor
<EditString @bind-Value="model.Notes" UpdateOn="UpdateTrigger.Change" />

<FormDefaults UpdateOn="UpdateTrigger.Change">
    @* every text control underneath now commits on blur *@
</FormDefaults>
```

- **`UpdateTrigger.Input`** — writes back on every keystroke (DOM `oninput`).
- **`UpdateTrigger.Change`** — writes back only on commit — blur or Enter — and only when the value actually changed (DOM `onchange`). Fewer render cycles, and on Blazor Server far fewer round-trips.

Per-control defaults: `Input` for `EditString`, `EditTextArea`, and the Other box on `EditRadioString`/`EditRadioEnum`; `Change` for `EditNumber` and `EditDateNative`. Resolution (first non-null wins): the control's own `UpdateOn` → the nearest enclosing [`FormDefaults.UpdateOn`](#formdefaults) → the control's built-in default above. No `FormOptions` counterpart, same as `AssetBase`.

`EditDate` (the calendar-dropdown date control, default since 10.7.0) deliberately has no `UpdateOn` — a picker commits on selection, or on parse at blur/Enter, so there's no per-keystroke commit to trade off. If you relied on `UpdateOn` for date fields before 10.7.0 (back when `EditDate` was the native input), switch that field to `EditDateNative`, which still carries the parameter.

Three things worth knowing before reaching for it:
- **Only `Input`/`Change` are offered — not `onblur`/`onkeydown`.** Blazor's value binder is an `EventCallback<ChangeEventArgs>`; those two DOM events dispatch `FocusEventArgs`/`KeyboardEventArgs` instead, which would throw an invalid-cast exception at dispatch. In practice `Change` already covers "commit on blur" for text inputs, since a text `<input>` fires its `change` event on blur whenever the value changed.
- **`EditNumber`/`EditDateNative` default to `Change`, not `Input`, because of it.** Choosing `Input` on either makes the browser report `type="number"`/`type="date"` as an empty string while a partial value is mid-type (`-`, `3.`, `1e`, a half-typed date) — a spurious validation error would flash on every keystroke. `Change` sidesteps it entirely, which is why it's the default for both.
- **`EditTextArea` with `AutoSize` still grows live while typing under `UpdateOn="Change"`.** A separate measure-only `oninput` handler drives the resize independently of the value commit, so the two features compose with no extra configuration.

This is a different axis from `DebounceMilliseconds` on `Select`/`EditSelectSearch`/`EditMultiSelect` (see [UI Kit](#ui-kit-non-form-controls)): that debounces the dropdown's option-filter keystrokes, not the bound value commit — no debouncing was added to the text controls.

### Model-declared placeholders (`[Placeholder]`)

Like `[Description]`/`[ToolTip]`, a control's placeholder/hint text can live on the model next to the field it describes instead of being repeated at every markup site:

```csharp
public class ContactModel
{
    [Placeholder("e.g. Jane Doe")]
    public string? Name { get; set; }

    // DataAnnotations' own [Display(Prompt = "...")] works too -- a model already annotated for
    // MVC/Razor Pages needs no second attribute, and a localized Prompt resolves through its
    // ResourceType the same way [Display(Name = ..., ResourceType = ...)] already does for labels.
    [Display(Prompt = "you@example.com")]
    public string? Email { get; set; }
}
```

```razor
<EditString @bind-Value="model.Name" />
<EditString @bind-Value="model.Email" />
```

Both inputs show their model-declared hint with no `Placeholder` markup attribute at all. Resolution (highest wins): the control's own `Placeholder` parameter → `[Placeholder]` on the bound property → `[Display(Prompt)]` → the control's built-in default (e.g. `EditSelectSearch`'s "Please select"). A markup `Placeholder` still overrides the model whenever one particular instance needs different text.

Honored by `EditString`, `EditTextArea`, `EditNumber<T>` (the rendered `placeholder` attribute); `EditDate<T>` (forwarded to the inner picker, falling through to its own mode-derived default, e.g. "Select date", when nothing resolves); `EditDateRange`'s `StartPlaceholder`/`EndPlaceholder` (each resolves independently against its own bound property's attributes — a `[Placeholder]` on `Start` never leaks onto `End`, and vice versa); and `EditSelectSearch<TValue>`/`EditMultiSelect<TValue>` (shown only while nothing is selected, falling back to the literal "Please select").

`EditSelectEnum<TEnum>` and `EditSelectString<TValue>` render a native `<select>`, which has no `placeholder` attribute — the model text instead goes on the leading blank option (when one renders) and on a hidden "unmatched value" option that supplies the closed select's own displayed text. Two caveats: on `EditSelectString`, an explicit `NullOptionText="null"` still suppresses the leading option entirely — a model attribute never resurrects an option the consumer deliberately turned off — and on a **non-nullable enum** whose current value is already a defined member, no blank option renders at all, so there is nothing for the model's placeholder text to display.

Deliberately not wired: `EditDateNative<T>` (browsers ignore `placeholder` on native `date`/`time` inputs); the `EditRadio*` "Other" free-text box (`EditRadioEnum.OtherPlaceholder` describes that sub-input, not the bound property, and `EditRadioString`'s Other box has no placeholder parameter at all); `EditFile`; `EditSelect<TValue>`; the checkbox lists; `EditBool*`; `EditDisplay`; and every UI-kit widget (none are model-bound, so there's no attribute to read).

### Model-declared Min/Max (`[MinValue]`/`[MaxValue]`)

Like `[Placeholder]`, a control's bounds can live on the model next to the field they constrain instead of being repeated at every markup site — but unlike `[Placeholder]`, they double as validation:

```csharp
public class EventModel
{
    [MinValue(0)]
    public int? Capacity { get; set; }

    [MaxValue("2100-12-31")]
    public DateTime? ScheduledFor { get; set; }

    // DataAnnotations' own [Range] works too -- a model already annotated for other consumers
    // needs no second attribute. A [Range] bound spelling "no bound" (double/int/long/decimal
    // extremes) is treated as unbounded, so the one-sided idiom below renders a min with no max.
    [Range(0, double.MaxValue)]
    public double? Weight { get; set; }
}
```

```razor
<EditNumber @bind-Value="model.Capacity" />
<EditDate @bind-Value="model.ScheduledFor" />
<EditNumber @bind-Value="model.Weight" />
```

All three render their bound `min`/`max` attributes with no `Min`/`Max` markup parameter at all. `[MinValue]`/`[MaxValue]` (`Controls.Helpers`, next to `[Placeholder]`) are themselves `ValidationAttribute`s: `[MinValue(0)]` both renders the browser-side bound and rejects an out-of-range submitted value at validation time (a null value passes — that's `[Required]`'s job, not this attribute's). Default messages follow the DataAnnotations convention — "The {0} field must be at least {1}." / "The {0} field must be no more than {1}." — override with `ErrorMessage` as usual. Three constructors cover every bound type: `(int)`, `(double)`, and `(string)` (invariant-culture text — the only way to express a date bound like `[MinValue("2024-01-01")]`, or a precise `decimal`).

Resolution (highest wins), every wired control: the control's own `Min`/`Max` parameter → `[MinValue]`/`[MaxValue]` on the bound property → `[Range]` → none. A markup `Min`/`Max` still overrides the model whenever one particular instance needs different bounds. On the `[Range]` fallback, a bound spelling "no bound" is treated as unbounded rather than clamped, thrown, or rendered: anything unrepresentable as `decimal` (the ubiquitous `[Range(0, double.MaxValue)]` idiom), and the integer-typed spellings of the same idiom (`int`/`long`/`decimal` extremes, e.g. `[Range(int.MinValue, 100)]`) — the very sentinels the library's validation-message rewrite presents as one-sided ("Cannot exceed 100"). One shared predicate decides it for both layers, so the rendered bound and the message can never disagree. The narrower integer extremes (`sbyte`/`byte`/`short`/`ushort`/`uint`/`ulong`) are deliberately **not** sentinels on either layer: `255`, `32767`, `127`, `-128` are overwhelmingly real bounds (`[Range(1, 255)]` on an `int Quantity`), and neither layer can see the bound property's type to tell a real ceiling from a vacuous one — so `[Range(1, 255)]` renders `max="255"` *and* says "Must be between 1 and 255", at the price of a genuinely vacuous `[Range(0, 255)]` on a `byte` naming both bounds too. An explicit `[MinValue]`/`[MaxValue]` is never sentinel-suppressed — those are one-sided by design, so whatever you write there is intentional and renders. An unparseable or otherwise misconfigured bound degrades gracefully — no rendered bound, no validation error, never a render-time exception.

Honored by `EditNumber<T>` (the rendered `min`/`max` attributes); `EditDate<T>` (forwarded to the inner picker, date-granularity, ignored in `Time` mode, same as its own `Min`/`Max` parameters); `EditDateNative<T>` (new `Min`/`Max` parameters as of 10.7.0 — its first bounds support ever — rendering the native input's own `min`/`max` formatted to match its `Type`, also omitted in `Time` mode); and `EditDateRange` (both bounds drive the ONE calendar its two inputs share, so each resolves param → the *looser* of the two fields' own attributes — `Min` takes the earlier minimum, `Max` the later maximum, each falling back to whichever single field declares one. A natural `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` annotation works as-is, a single `[Range(typeof(DateTime), ...)]` on `Start` alone supplies both ends, and two conflicting bounds never leave the shared calendar tighter than either field's own annotation. The result is the convex **hull**, not the union: with `[Range(2024-03-01 .. 2024-03-31)]` on `Start` and `[Range(2024-09-01 .. 2024-09-30)]` on `End` the calendar offers 2024-06-15, which neither field accepts — one calendar has exactly one min and one max, and the annotations still reject the pick at validation time).

Deliberately not wired: `EditString`/`EditTextArea` (length limits already come from `[StringLength]`/`[MaxLength]`, a different axis), the select/radio/checkbox-list controls, `EditBool*`, `EditFile`, `EditDisplay`, and every UI-kit widget (none are model-bound, so there's no attribute to read; `DatePicker`/`DateRangePicker` keep their plain `Min`/`Max` parameters with no model to resolve against).

### Model-declared field attributes (`[Autocomplete]`/`[Step]`/`[BoolText]`/`[Rows]`/`[FileConstraints]`)

Extending the same `[Placeholder]`/`[MinValue]`/`[MaxValue]` pattern, every remaining field-semantic markup parameter now has a model-attribute counterpart (`Controls.Helpers`) — plus four standard DataAnnotations attributes newly honored as rendering fallbacks, the same way `[Display(Prompt)]` and `[Range]` already were. Resolution is uniform everywhere: the control's own markup parameter → the model attribute → the control's built-in default.

```csharp
public class ProfileModel
{
    [Autocomplete("email")]
    public string? Email { get; set; }

    [Step(0.01)]
    public decimal? Price { get; set; }

    [BoolText(TrueText = "Enabled", FalseText = "Disabled")]
    public bool IsActive { get; set; }

    [Rows(2, AutoSize = true, MinRows = 2, MaxRows = 10)]
    public string? Notes { get; set; }

    [FileConstraints(AllowedExtensions = new[] { ".pdf", ".png" }, MaxFileSizeBytes = 5_242_880, MaxFiles = 3)]
    public List<IBrowserFile> Attachments { get; set; } = new();

    // Standard DataAnnotations, now also read for their control-rendering effect, not just validation:
    [StringLength(100)]
    public string? Nickname { get; set; }

    [DataType(DataType.Password)]
    public string? Secret { get; set; }

    [DisplayFormat(DataFormatString = "{0:N2}")]
    public double? Weight { get; set; }

    [DataType(DataType.Time)]
    public TimeOnly? OpensAt { get; set; }
}
```

```razor
<EditString @bind-Value="model.Email" />
<EditNumber @bind-Value="model.Price" />
<EditBool @bind-Value="model.IsActive" />
<EditTextArea @bind-Value="model.Notes" />
<EditFile @bind-Value="model.Attachments" />
<EditString @bind-Value="model.Nickname" />
<EditString @bind-Value="model.Secret" />
<EditNumber @bind-Value="model.Weight" />
<EditDate @bind-Value="model.OpensAt" />
```

None of the nine inputs above sets the corresponding markup parameter at all — everything comes from the model.

- **`[Autocomplete("...")]`** → `EditString.Autocomplete`. Unset falls back to the control's built-in `"one-time-code"` (which suppresses browser autofill).
- **`[Step(...)]`** → `EditNumber<T>.Step`. Three constructors — `(int)`, `(double)`, `(string)` (invariant-culture decimal text, for a step like `0.01` a `double` literal could drift on) — mirroring `[MinValue]`/`[MaxValue]`'s shape. A non-positive or unconvertible value is ignored (falls through to the built-in default of `1`), same lenient philosophy as the Min/Max bounds.
- **`[BoolText(...)]`** → `EditBool`'s `TrueText`/`FalseText` and `EditBoolNullRadio`'s `TrueText`/`FalseText`/`NullText` (all read-only-view text). Each of the three named properties is independently optional; an unset one falls through to that control's own default (`"Yes"`/`"No"`/`"Not Set"`).
- **`[Rows(...)]`** → `EditTextArea`'s `Rows`/`MinRows`/`MaxRows`/`AutoSize`. `0` means "unset" for the three ints (attributes can't hold nullable ints), so an unset one falls through to the control's own default; `AutoSize = false` is likewise indistinguishable from unset, which is harmless since `false` already is the control default.
- **`[FileConstraints(...)]`** → `EditFile`'s `AllowedExtensions`/`MaxFileSizeBytes`/`MaxFiles`/`MaxTotalBytes` — also drives the rendered `accept` attribute and the `"Supported formats"` hint text. `0`/`null` mean "unset"; the control's own defaults (10 MB per file, 100 MB total, unlimited count, any extension) apply when nothing resolves.

Newly-honored standard DataAnnotations — no new attribute needed if the model is already annotated for other purposes (MVC, EF, OpenAPI, ...):

- **`[StringLength(100)]`/`[MaxLength(100)]`** → the rendered `maxlength` attribute (and the `"n / 100"` `ShowCount` text) on `EditString` and `EditTextArea`, via the same length reconciliation the length-*validation* messages already use — the browser-side cap and the enforced bound can never disagree.
- **`[DataType(DataType.Password)]`** → `EditString` renders `type="password"` with the reveal toggle, same as setting `IsPassword="true"`.
- **`[DisplayFormat(DataFormatString = "{0:N2}")]`** (a bare `"N2"` works too — both normalize to the same token) → `EditNumber.Format`, `EditDate`'s `Format`/`DateFormat`, `EditDateNative.DateFormat`, and `EditDateRange`'s `Format`/`DateFormat` (which reads the **Start** field's attributes first, then **End**'s, so annotating either endpoint alone supplies both).
- **`[DataType(DataType.Date)]`/`[DataType(DataType.DateTime)]`/`[DataType(DataType.Time)]`** → `EditDate.Type`/`EditDateNative.Type` (`Date`/`DateTimeLocal`/`Time` respectively; `Month` has no `[DataType]` equivalent, so it's only ever reachable via the `Type` parameter).

These four read the DataAnnotations attribute directly — there's no bespoke `Controls.Helpers` attribute in the middle, since DataAnnotations already owns that slot. Resolution is the same shape as everywhere else: the control's own parameter → the DataAnnotations attribute → the control's built-in default.

**Breaking-ish, worth a look before upgrading:** the parameters above changed from non-nullable to nullable so "unset" is detectable — markup usage (`Rows="4"`, `Step="0.01m"`, `Type="InputDateType.Time"`, ...) is unaffected; only C# code that reads a component *instance's* property directly needs to account for the type change: `EditString.IsPassword`/`.Autocomplete` (`bool?`/`string?`), `EditTextArea.Rows`/`.AutoSize` (`int?`/`bool?`), `EditNumber.Step` (`decimal?`), `EditDate.Type`/`EditDateNative.Type` (`InputDateType?`), `EditDateNative.DateFormat` (`string?`), `EditBool.TrueText`/`.FalseText` (`string?`), `EditBoolNullRadio.TrueText`/`.FalseText`/`.NullText` (`string?`), and `EditFile.MaxFileSizeBytes`/`.MaxFiles`/`.MaxTotalBytes` (`long?`/`int?`/`long?`). Defaults are unchanged — each is resolved in an `Effective*`/`Resolved*` property, not the parameter itself.

Deliberately has no model-attribute counterpart: delegates/`RenderFragment`s/`EventCallback`s, runtime state (`IsDisabled`, `Open`, `Indeterminate`), view composition (`Size`, `Width`, CSS classes, `IsHorizontal`), form-level localization strings (picker labels, `*MessageFormat` strings — use `FormDefaults`/markup instead), and runtime data (`Options`, `Presets`). Model attributes are for constant, field-semantic metadata only — not everything a control parameter could ever hold.

## Styling and Customization

The library provides default styling through the included CSS file. You can customize the appearance by:

1. **Overriding CSS classes** in your own stylesheets
2. **Using ContainerClass** parameter for component-specific styling
3. **Applying custom CSS** to the `.edit-control-wrapper` class

The AntDesign-style UI-kit controls (Alert, Modal, Table, Select, ...) are themed via `--wss-*` CSS custom properties in `wss-controls.css`. They default to the AntDesign 4.x look and **bridge to your existing `--color-primary` / `--color-danger` / `--border-color`** where those are defined, so they pick up your theme automatically. Override any `--wss-*` variable to re-theme.

The form controls in `edit-controls.css` read the same generic bridge directly, with the AntD default as each `var()`'s own fallback — so an app that sets these at `:root` re-themes the form controls with no `edit-`-prefixed variable at all: `--color-primary`, `--color-danger`, `--border-color`, `--color-bg` (control backgrounds), `--color-bg-disabled`, `--color-page-background` (the `EditFile` drop zone and its file rows' hover), `--color-text` / `--color-text-secondary` (body and muted text, including a file row's size), `--color-on-primary` (the styled checkbox's check glyph), and `--color-tooltip-bg` / `--color-tooltip-text` (`LabelTooltip`). One `edit-`-prefixed token is declared at `:root` rather than only under `.edit-theme` — `--edit-color-border` (bridges to `--border-color`, default `#d9d9d9`) — so the styled checkbox, the `EditRadio*` "Other" free-text input, and the button-mode radio borders can be retargeted independently of the generic bridge.

### Opt-in AntD theme for the classic edit inputs (`.edit-theme`)

`EditString`, `EditNumber`, `EditTextArea`, `EditDateNative`, and `EditSelect`'s native `<select>` render completely unstyled by default (a consumer-owned `.edit-input` class, no border/background/radius) — every existing consumer already styles that class itself, and that behavior **never changes**. Wrap any element you own in `class="edit-theme"` to opt everything beneath it into the same AntD 4.x box chrome (border, radius, height, hover tint, focus glow) that the `--wss-*`-themed UI kit already uses for `Select`:

```razor
<div class="edit-theme">
    <EditString @bind-Value="model.Name" Label="Name" />
    <EditNumber @bind-Value="model.Age" Label="Age" />
</div>
```

- **Opt-in and render-tree-scoped, not global** — only descendants of a `.edit-theme` ancestor are affected; nothing outside it changes, and you can nest a second `.edit-theme` with its own overridden tokens (each scope resolves independently). This is the deliberate design for micro-frontends: wrap an MFE's own root, not `:root`, if it needs its own theme independent of the host page.
- **Radio/checkbox/`EditFile` are untouched** — `EditRadio`/`EditCheckedStringList`/`EditCheckedEnumList` carry their own `edit-radio-input`/`edit-checkedList-checkbox` classes (never `.edit-input`), `EditBool`'s checkbox is excluded by `type="checkbox"`, and `EditFile` carries no `.edit-input`-family class at all — none of them get boxed like a text field. Native `<select>` keeps its default `appearance: auto` (its own dropdown arrow); `EditNumber` keeps native spinner buttons — both documented deviations from a literal AntD port.
- **Tokens** (declare/override on `.edit-theme` or any nested scope): `--edit-color-primary` (default `#1890ff`, bridging to your `--color-primary`), `--edit-color-border` (bridges to `--border-color`), `--edit-radius` (`2px`), `--edit-control-height` / `-sm` / `-lg` (`32px` / `24px` / `40px` — used as `min-height`, so a multi-row `EditTextArea` or `AutoSize` is never clipped), `--edit-color-bg-disabled` (bridges to `--color-bg-disabled`), `--edit-color-placeholder`, `--edit-color-text-disabled`, `--edit-color-text-secondary` (the affix suffix's clear/count/password icons and `EditTextArea`'s below-the-box count). Derived hover/focus colors are pure override knobs, computed at each usage site rather than baked into a token (same rule as the `--wss-*` tokens above) — override `--edit-color-primary-hover`, `--edit-primary-shadow`, or `--edit-error-shadow` directly if the computed `color-mix` isn't what you want.
- **Size** — see `Size` on `EditString`/`EditNumber`/`EditTextArea`/`EditDateNative` above; the size classes are inert hooks unthemed, and `.edit-theme` is what actually sizes them.

**Where to set the variables.** The `--wss-*` / `--edit-*` tokens can be overridden at **any scope** — `:root`, `body`, a theme class, or a micro-frontend's root container — and derived states (hover borders, focus shadows, focus rings) follow the override, because they derive from the base token at each usage site. The generic `--color-primary` / `--color-danger` / `--border-color` bridge, by contrast, is resolved **once, at `:root`** (a CSS custom property substitutes the `var()`s in its value where the property is declared): a `--color-primary` set on a nested container is not seen. Rule of thumb: app-wide theme → set `--color-*` at `:root` and everything follows; scoped/per-area theme (e.g. an MFE that doesn't own the host page) → set the `--wss-*` / `--edit-*` tokens themselves on your container. A directly-set `--wss-*` token always wins over the `--color-*` bridge.

The UI-kit components also accept regular `class` / `style` / `data-*` attributes (applied to the component's root element; `class` and `style` merge with the component's own), so one-off tweaks don't require CSS variables at all.

```razor
<EditString @bind-Value="model.Name" 
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
- Requires .NET 10.0+

## Trimming and AOT

`WssBlazorControls` is trim- and AOT-compatible: the package ships `IsTrimmable`/`IsAotCompatible` metadata, and the trim, AOT, and single-file analyzers run warning-clean on every build (enforced by `TreatWarningsAsErrors`). A default Blazor WebAssembly publish (`dotnet publish -c Release`) trims the library automatically.

Why the attribute-driven features survive trimming:

- **Labels, tooltips, descriptions, placeholders, length/range extraction** — every control resolves its accessor from `@bind-Value`'s compiler-synthesized `ValueExpression`; the expression tree roots the property's getter, so the trimmer keeps the property and the attributes on it. The attribute types themselves (`[DisplayName]`, `[Description]`, `[ToolTip]`, `[Placeholder]`, `[Range]`, ...) are referenced by the library and kept.
- **Enum display names** — `[EnumDisplayName]`/`[Display]` lookups only reflect over enum types, whose fields the trimmer always preserves.
- **Option building** — enum option lists use `Enum.GetValuesAsUnderlyingType` (no dynamic array creation), safe under WASM AOT.

Consumer notes:

- The generic controls (`EditNumber<T>`, `EditDateNative<T>`, `EditSelect<TValue>`, `EditRadio*`) annotate their type parameter with `[DynamicallyAccessedMembers(All)]`, mirroring the framework's `InputNumber`/`InputSelect`. Normal usage (binding concrete model properties) compiles warning-free; only forwarding an open generic parameter into them propagates the annotation.
- `<DataAnnotationsValidator>` is the framework's reflection-based validator and warns under full trimming in *your* app — models bound through `@bind-Value` are rooted in practice, but validation of unbound/nested models is your app's concern.
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

### 10.7.0

**Breaking**
- **The AntD-style calendar-dropdown date control, formerly `EditDatePicker<T>`, is renamed to `EditDate<T>` and is now the default date control.** The previous native-`<input>` `EditDate<T>` is renamed to `EditDateNative<T>`.

  | Old | New |
  |---|---|
  | `EditDatePicker<T>` (calendar dropdown) | `EditDate<T>` |
  | `EditDate<T>` (native `<input type="date">`/`datetime-local`/`month`/`time`) | `EditDateNative<T>` |

  A compile-time `[Obsolete(error: true)]` `EditDatePicker<T>` stub (`Controls/EditDatePicker.cs`, inheriting `EditDate<T>` so every parameter still resolves through Razor's type inference — the obsolete diagnostic is the only error a consumer sees, not a cascade of "parameter does not exist" errors) catches stale `<EditDatePicker>` markup with an actionable build error.

  **Two hazards the stub can't catch, since the name it fires on doesn't cover them:**
  1. **A bare `<EditDate>` that meant the old native input still compiles after upgrading — and silently switches to the calendar dropdown**, since the class name now means something different. There is no way to make a plain, still-valid identifier fail the build. Audit every existing `<EditDate>` usage before upgrading: keep it as `<EditDate>` only if the calendar dropdown is actually what you want there, otherwise change it to `<EditDateNative>`.
  2. **The new default needs assets the old one didn't.** `EditDate` is now built on the UI-kit `DatePicker`, so it requires `wss-controls.css` *and* the lazily-imported `wss-picker.js` — the old native-input `EditDate` needed only `edit-controls.css`. An app that links just `edit-controls.css` (the pre-10.7.0 minimum for a date-only form) renders an unstyled date field with no dropdown at all after upgrading. See [Installation](#installation).

  Both controls still support the identical set of bound types (`DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, and their nullable variants) and `Type` values, so parameters carry over unchanged whichever way you migrate. One more reason to choose `EditDateNative` deliberately, beyond the two hazards above: it's the one that hands off to the browser's native date/time input, which on a phone means the OS's own picker (a wheel/calendar sheet, digit-only keyboards) — `EditDate`'s calendar dropdown renders the same custom UI on every device, phone included.

- **`INotificationService.Success`/`Info`/`Warning`/`Error` now return `Guid` instead of `void`**, matching `IMessageService`'s own signatures; `NotificationService` and the static `WasmNotificationService` forward the id through. Pass it to `Remove(Guid)` to dismiss a sticky (`Duration=0`) notification programmatically — previously the only handle on a notification was the user's own close button. A source break only for a custom `INotificationService` implementation (change the four return types) or a C# expression-bodied lambda whose inferred type was `void`; every ordinary `Notifications.Error("...")` call site compiles unchanged. See [UI Kit (non-form) controls](#ui-kit-non-form-controls).
- **`ReadOnlyValue.IsLabelHidden` is removed, replaced by `HasLabelElement` (`bool`, default `true`)** — the parameter's meaning inverted along with the accessible-name fix below. `IsLabelHidden` gated `aria-labelledby` on "is the label hidden", which was the wrong question: `FormLabel` renders the `lbl-{id}` element either way (visually hidden), so the reference never dangles. `HasLabelElement="false"` now means the narrower, actually-correct thing — there is no label element for this value to be named by at all — and only the per-option rows of a read-only checked list pass it. Affects consumers who use `ReadOnlyValue` directly (it is public, though intended for use inside the controls).
- **`FormLabel.IdPrefix` and `FieldValidationDisplay.IdPrefix` are removed.** Both were inert — nothing read them, and neither component composes an id of its own (each takes the host control's already-resolved `Id`). The per-control `IdPrefix` on every `Edit*` control is unaffected and remains the way to prefix a generated id.
- **`CheckboxOptionList<TItem>.IsLabelHidden` is removed** — same inertness. The type is technically public but documented internal-use (the shared checkbox-per-option body behind `EditCheckedStringList`/`EditCheckedEnumList`), and the parameter became meaningless once its read-only rows switched to `HasLabelElement`.

**New** (Edit Controls)
- `EditDate<T>` gains `Size` (`SelectSize`: `Default`/`Small`/`Large`) and `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) — both added to the picker *before* the rename above, so the renamed `EditDate<T>` is a genuine superset of the old `EditDatePicker<T>`, not a regression. `Size` renders `wss-picker-sm`/`wss-picker-lg` on the picker wrapper (mirroring `Select`'s own size classes; `Default` adds no class). `ParsingErrorMessage` is genuinely new behavior, not a ported parameter: the underlying `DatePicker` gained an `OnParseError` callback, and `EditDate<T>` now surfaces unparseable typed text (something that isn't a date at all) as a validation message via its own `ValidationMessageStore`, cleared the moment a valid value next commits and on the control's dispose. Previously a bad typed entry was silently reverted to the last valid value with no feedback whatsoever. Only fires for text that fails to parse as a date — a well-formed date merely rejected by `Min`/`Max`/`DisabledDate`/`DisabledTime` does not trigger it. See [Available Controls](#input-controls).

- New `UpdateTrigger` enum (`Input`/`Change`) + `UpdateOn` (`UpdateTrigger?`) parameter on `EditString`/`EditTextArea` (default `Input`), `EditNumber`/`EditDateNative` (default `Change`), and `EditRadioString`/`EditRadioEnum<TEnum>` (default `Input`, affecting only the "Other" free-text box) — controls whether the bound value commits on every keystroke (`oninput`) or only on blur/Enter and only when changed (`onchange`), trading per-keystroke reactivity for fewer render cycles (and, on Blazor Server, far fewer round-trips) for consumers who don't need it. `FormDefaults` gains a matching `UpdateOn` (plus a public `EffectiveUpdateOn` chaining through nested `FormDefaults`, same pattern as the other settings); there's no `FormOptions` counterpart, same as `AssetBase`. `onblur`/`onkeydown` aren't offered as trigger options: Blazor's value binder is an `EventCallback<ChangeEventArgs>`, but those two DOM events dispatch `FocusEventArgs`/`KeyboardEventArgs` instead, which would throw an invalid cast at dispatch — `Change` already covers "commit on blur" for text inputs since a text `<input>`'s own `change` event fires on blur whenever the value changed. `EditNumber`/`EditDateNative` default to `Change` rather than `Input` because a partial value (`-`, `3.`, `1e`, a half-typed date) makes the browser report `type="number"`/`type="date"` as an empty string mid-type, which would flash a spurious validation error on every keystroke under `Input`. `EditTextArea`'s `AutoSize` still grows live while typing under `Change`, via a separate measure-only `oninput` handler that runs independently of the value commit. A different axis from the existing `DebounceMilliseconds` on `Select`/`EditSelectSearch`/`EditMultiSelect`, which debounces the option-filter, not the value commit. See [Commit timing](#commit-timing-updateon).
- New `[Placeholder("...")]` attribute (`Controls.Helpers`, alongside the existing `[Description]`/`[ToolTip]`) so a control's placeholder/hint text can live on the model property next to the field it describes instead of being repeated at every markup site. `AttributesHelper.Placeholder()` resolves it first, then falls back to DataAnnotations' own `[Display(Prompt = "...")]` (via `GetPrompt()`, so a localized `[Display(Prompt = ..., ResourceType = ...)]` resolves too) — universal precedence, every control that has one: its own `Placeholder` parameter → `[Placeholder]` → `[Display(Prompt)]` → the control's built-in default. Honored by `EditString`/`EditTextArea`/`EditNumber<T>` (the rendered `placeholder` attribute), `EditDate<T>` (forwarded to the inner picker, still falling through to its own mode-derived default when nothing resolves), `EditDateRange` (`StartPlaceholder`/`EndPlaceholder` resolve independently against each bound property's own attributes), and `EditSelectSearch<TValue>`/`EditMultiSelect<TValue>` (shown while nothing is selected, falling back to the literal "Please select"). `EditSelectEnum<TEnum>`/`EditSelectString<TValue>` have no native `placeholder` attribute to render onto — the text instead goes on the leading blank option (when one renders) and on a hidden "unmatched value" option that supplies the closed `<select>`'s displayed text; on a non-nullable enum whose current value is already a defined member, no blank option renders at all, so nothing shows. Deliberately not wired: `EditDateNative<T>` (browsers ignore `placeholder` on native date/time inputs), the `EditRadio*` "Other" free-text box, `EditFile`, `EditSelect<TValue>`, the checkbox lists, `EditBool*`, `EditDisplay`, and the UI-kit widgets (none are model-bound). Carries one signature change on the two searchable selects — see **Changed** below. See [Model-declared placeholders](#model-declared-placeholders-placeholder).
- New `[MinValue(...)]`/`[MaxValue(...)]` attributes (`Controls.Helpers`, alongside `[Placeholder]`) declare a control's bounds on the model property they constrain. Three constructors — `(int)`, `(double)`, `(string)` (invariant-culture text, the only way to express a date bound like `[MinValue("2024-01-01")]` or a precise `decimal`) — cover every bound type. Unlike `[Placeholder]`, they're genuine `ValidationAttribute`s: `[MinValue(0)]` both renders the browser-side `min` and rejects an out-of-range value at validation time (null passes — that's `[Required]`'s job), with default messages "The {0} field must be at least {1}."/"The {0} field must be no more than {1}." (override via `ErrorMessage` as usual). DataAnnotations' own `[Range]` is honored as a fallback with no second attribute needed — including `[Range(typeof(DateTime), "2024-01-01", "2024-12-31")]` — and a `[Range]` bound spelling "no bound" — anything unrepresentable as `decimal` (the ubiquitous `[Range(0, double.MaxValue)]` idiom) plus the `int`/`long`/`decimal` extremes (`[Range(int.MinValue, 100)]`) — is treated as unbounded rather than clamped, so those attributes alone render the one real bound and omit the other, agreeing with the one-sided message `ValidationHelper` already rewrites those sentinels into. An explicit `[MinValue]`/`[MaxValue]` is never sentinel-suppressed (one-sided by design, so an extreme written there is intentional). An unparseable/misconfigured bound degrades to no rendered bound and no validation error. Uniform precedence, every wired control: its own `Min`/`Max` parameter → `[MinValue]`/`[MaxValue]` → `[Range]` → none. Wired: `EditNumber<T>` (the rendered `min`/`max`); `EditDate<T>` (forwarded to the inner picker, date-granularity, ignored in `Time` mode); `EditDateNative<T>` (**new** `Min`/`Max` parameters, `DateTime?`, same shape as `EditDate`'s — its first bounds support ever — rendering the native input's own `min`/`max` formatted to match `Type`, also omitted in `Time` mode); and `EditDateRange` (`Min` resolves param → Start's attributes → End's; `Max` resolves param → End's attributes → Start's, so a `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` pairing, or a single `[Range]` on `Start`, supplies both ends). Not wired: `EditString`/`EditTextArea` (length limits are `[StringLength]`/`[MaxLength]`'s job), the selects/radios/checkbox lists, `EditBool*`, `EditFile`, `EditDisplay`, and the UI-kit widgets (not model-bound). See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
- **Every remaining field-semantic markup parameter now has a model-attribute counterpart, completing the `[Placeholder]`/`[MinValue]`/`[MaxValue]` pattern.** Five new `Controls.Helpers` attributes: `[Autocomplete("email")]` → `EditString.Autocomplete` (falls back to the built-in `"one-time-code"`); `[Step(0.01)]`/`[Step(1)]`/`[Step("0.01")]` (same `(int)`/`(double)`/`(string)` three-constructor shape as `[MinValue]`/`[MaxValue]`, string = invariant decimal text) → `EditNumber.Step` (default `1.0`; a non-positive or unconvertible value is ignored, same lenient philosophy as the Min/Max bounds); `[BoolText(TrueText = "Enabled", FalseText = "Disabled", NullText = "Unknown")]` → `EditBool` (`TrueText`/`FalseText`, read-only view) and `EditBoolNullRadio` (all three radio labels + read-only) — defaults stay `"Yes"`/`"No"`/`"Not Set"`, each property independently optional; `[Rows(4)]` or `[Rows(2, AutoSize = true, MinRows = 2, MaxRows = 10)]` → `EditTextArea` `Rows`/`MinRows`/`MaxRows`/`AutoSize` (`0` = unset for the ints, since an attribute can't hold a nullable int; `AutoSize = false` is indistinguishable from unset, which is harmless since `false` is already the default); `[FileConstraints(AllowedExtensions = new[] { ".pdf", ".png" }, MaxFileSizeBytes = 5242880, MaxFiles = 3, MaxTotalBytes = 10485760)]` → `EditFile` (`0`/`null` = unset; defaults stay 10 MB per file, 100 MB total, unlimited count, any extension — also drives the rendered `accept` attribute and the "Supported formats" hint). Four more standard DataAnnotations attributes are now also honored for a rendering effect, not just validation, no new attribute needed: `[StringLength(100)]`/`[MaxLength(100)]` → rendered `maxlength` (and the "n / 100" `ShowCount` text) on `EditString`/`EditTextArea`; `[DataType(DataType.Password)]` → `EditString` renders `type="password"` with the reveal toggle (`IsPassword` fallback); `[DisplayFormat(DataFormatString = "{0:N2}")]` (a composite `"{0:X}"` or bare `"X"` both accepted) → `EditNumber.Format`, `EditDate`'s `Format`/`DateFormat`, `EditDateNative.DateFormat`, `EditDateRange`'s `Format`/`DateFormat` (reads the **Start** field's attributes first, then **End**'s); `[DataType(DataType.Date/DateTime/Time)]` → `EditDate.Type`/`EditDateNative.Type` (`Date`/`DateTimeLocal`/`Time`). Uniform precedence throughout, same shape as `[Placeholder]`/`[MinValue]`/`[MaxValue]`: the control's own markup parameter → the model attribute (custom or DataAnnotations) → the control's built-in default. Deliberately has no model-attribute counterpart: delegates/`RenderFragment`s/`EventCallback`s, runtime state (`IsDisabled`, `Open`, `Indeterminate`), view composition (`Size`, `Width`, CSS classes, `IsHorizontal`), form-level localization strings (picker labels, `*MessageFormat` strings — use `FormDefaults`/markup instead), and runtime data (`Options`, `Presets`). Carries a set of nullable-parameter signature changes — see **Changed** below. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).

- `EditDateRange` gains `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) — parity with the `EditDate<T>` parameter above, applied to a two-field control. `{0}` is the **failing endpoint's** own field name, so one format string serves both inputs; the message goes into a dedicated `ValidationMessageStore` scoped to that endpoint's `FieldIdentifier`, and each endpoint's message clears independently the moment a valid value next commits for *that* endpoint. Same trigger contract as `EditDate`'s: only text that fails to parse as a date at all, never a well-formed value merely rejected by `Min`/`Max`/`DisabledDate`/`StartDisabledTime`/`EndDisabledTime`. Previously an unparseable entry in either input was silently reverted with no feedback. Built on two new `DateRangePicker` callbacks — see **New (UI Kit)** below. See [Available Controls](#input-controls).
- `EditDateRange` gains `Size` (`SelectSize`: `Default`/`Small`/`Large`) — the parameter `EditDate<T>`, `DatePicker` and `DateRangePicker` all gained above, now declared on the form control too and forwarded to the inner `DateRangePicker` (`wss-picker-sm`/`wss-picker-lg` on the wrapper; `Default` adds no class). Also closes a footgun: with no `Size` parameter of its own, a consumer's `Size="Small"` fell into `AdditionalAttributes` and splatted the raw string onto the picker's *enum*-typed `Size` parameter instead of sizing the control.
- `EditMultiSelect<TValue>` gains `Variant` (`SelectVariant`, default `Outlined`) — the parameter `EditSelectSearch` already forwarded, now reachable for `Multiple`/`Tags` mode too, so the `Pill` and `Borderless` trigger looks are no longer single-select-only. Purely additive: `Outlined` renders exactly as before. See [Pill filter variant](#pill-filter-variant-select--editselectsearch).

**New** (UI Kit)
- `DatePicker` and `DateRangePicker` both gain `Size` (`SelectSize`) — the same parameter `Select` already has, rendering `wss-picker-sm`/`wss-picker-lg` on the outer wrapper (`Default` byte-identical to before). `EditDate<T>` forwards it (see above).
- `DateRangePicker` gains `OnStartParseError` / `OnEndParseError` (`EventCallback<string>`, raised with the offending text) — per endpoint rather than one callback carrying which side failed, matching every other per-input parameter on this control (`StartPlaceholder`/`EndPlaceholder`, `StartDisabledTime`/`EndDisabledTime`, the `StartAria*`/`EndAria*` pairs), since a host form control needs the two apart to target each field's own `FieldIdentifier`. Raised only on a genuine parse failure at a typed commit (Enter or blur), never for a well-formed value the picker rejects on `Min`/`Max`/`DisabledDate`/`*DisabledTime` grounds — that's a different situation the picker has always handled by reverting. The picker itself has no validation concept; these exist so `EditDateRange` can surface a message it can't (see **New (Edit Controls)** above). Optional and additive: with no handler attached a standalone `DateRangePicker` behaves exactly as before, silently reverting the unparseable text to the formatted bound value.
- `Select` gains an `AriaErrorMessage` parameter (same shape as its existing `AriaRequired`/`AriaInvalid`/`AriaDescribedBy` trio) — forwarded by `EditSelectSearch`/`EditMultiSelect` as `IsInvalid ? _errorMsgId : null`, the same pattern `EditDate` already uses onto `DatePicker`. `EditFile`'s `<InputFile>` gains an `aria-errormessage` too, but keyed off `IsInvalid` (EditContext validation) rather than the `_hasError` flag that drives its `aria-invalid`: a pure upload-time-only rejection (bad extension, duplicate, over a cap) sets `_hasError` without ever populating the `error-msg-{id}` element `FieldValidationDisplay` renders, so pairing `aria-errormessage` with `_hasError` would point assistive tech at that element while it's empty.

**Changed** (Edit Controls)
- **`UpdateOn` was deliberately not carried over to the new `EditDate<T>`.** It remains on `EditDateNative<T>` (choosing `oninput` vs. `onchange` for a text input) but has no equivalent on the calendar dropdown: a picker commits on selection, or on parse at blur/Enter, so there is no per-keystroke commit to opt into. If you set `UpdateOn` on the pre-10.7.0 `EditDate` (the native input) and want that behavior back, switch that field to `EditDateNative`. See [Commit timing](#commit-timing-updateon).
- **Default checkbox/radio label spacing now matches AntD's 8px gap — a visible default-rendering change every consumer will see.** `.edit-checkbox-label` / `.edit-radio-label` previously relied on native whitespace-collapse spacing between the `<input>` and its label text unless `UseStyledCheckbox` opted into the flex/gap layout; that 8px flex-row gap (AntD's checkbox/radio spec) is now the default for **every** checkbox and radio label — `EditBool`, `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, `EditRadioString`, `EditRadioEnum<TEnum>`, and `EditRadio`'s consumer-authored `<label>`s that carry `edit-radio-label` — regardless of `UseStyledCheckbox`. Every checkbox/radio list in every consuming app will render with a touch more space between the box and its text after upgrading; nothing to opt into or configure, and no markup changes are required. `.edit-checkbox-label-styled` no longer carries its own layout — it's now an empty marker class kept only as a stable hook for consumers who target the styled variant specifically.
- **Signature change:** `EditSelectSearch.Placeholder` and `EditMultiSelect.Placeholder` are now `string?` (previously `string`, defaulted to `"Please select"`) as part of the new `[Placeholder]` resolution chain above — markup usage is unaffected, but C# code reading `.Placeholder` should account for the type change.
- **Signature change:** the parameters newly resolved via the model-attribute pattern above changed from non-nullable to nullable so "unset" is detectable — markup usage (`Rows="4"`, `Step="0.01m"`, `Type="InputDateType.Time"`, ...) is unaffected, only C# code reading the component instance's property directly needs to account for the type change: `EditString.IsPassword` (`bool?`), `EditString.Autocomplete` (`string?`), `EditTextArea.Rows`/`AutoSize` (`int?`/`bool?`), `EditNumber.Step` (`decimal?`), `EditDate.Type`/`EditDateNative.Type` (`InputDateType?`), `EditDateNative.DateFormat` (`string?`), `EditBool.TrueText`/`FalseText` (`string?`), `EditBoolNullRadio.TrueText`/`FalseText`/`NullText` (`string?`), and `EditFile.MaxFileSizeBytes`/`MaxFiles`/`MaxTotalBytes` (`long?`/`int?`/`long?`). Defaults are unchanged — each is resolved in an `Effective*`/`Resolved*` property, not the parameter itself.
- **A required `EditBool` now shows the required star — a visible change wherever a checkbox is bound to a `[Required]` property or sets `IsRequired`.** Checkbox mode's hand-rolled label had drifted from `FormLabel` by omitting the star while still announcing `aria-required` to assistive tech, violating the library's documented "the star and `aria-required` can never disagree" invariant; the checkbox label now renders through `FormLabel` itself (via two new additive optional `FormLabel` parameters, `NestedInput` and `LabelClass`), so the star, description, tooltip and hidden-label behavior are structurally identical to every other control. The label also carries `id="lbl-{id}"` now, like every sibling.
- **`EditRadioString`'s "Other" free-text box now renders styled and laid out like `EditRadioEnum`'s — a visible change wherever `HasOther` is set.** Its input had drifted onto the empty `.edit-string-input` class (no border, min-width, or disabled affordance) and lacked the flex row wrapper, so it rendered bare and stacked below its radio. Both controls now share one internal `RadioOtherInput`; DOM ids and each control's commit wiring are unchanged, and the long-standing `.edit-radio-other-option-container` consumer hook is retained.
- **`HidingMode.WhenNullOrDefault`/`WhenReadOnlyAndNullOrDefault` now treat "default" uniformly on the native selects, per the mode's documented contract.** `EditSelect<string>` bound to `""` previously stayed visible (every other string-bound control hid), and `EditSelectString<TValue>` with a non-string `TValue` at `0`/`false` previously stayed visible (its check stringified the value). Both now union the base value-type default check with the empty-string case. Only affects consumers using these hiding modes with those exact type/value combinations. `EditSelectSearch<TValue>` is now aligned too — see **Fixes (Select engine)** below.
- **A read-only control whose label is hidden now keeps its accessible name — a change wherever `IsLabelHidden` (or `FormOptions.IsLabelHidden`) is combined with read-only mode.** The read-only value previously dropped `aria-labelledby` in that case, on the premise that a hidden label has no element to point at. It does: `FormLabel` still renders the `lbl-{id}` element, visually hidden, so the reference never dangled — omitting it just left the value with no accessible name at all, which assistive tech reads as an unlabeled blob. Now applied consistently across every read-only view: each `ReadOnlyValue` host (via the new `HasLabelElement` parameter — see **Breaking** above), `EditString`'s masked and link views, and `EditFile`'s read-only file list (whose stand-in `aria-label="Selected files"` is dropped in favor of the real field name; the *edit*-mode list keeps that literal, since it isn't the field's own value display). The four radio fieldsets (`EditRadio`, `EditRadioEnum<TEnum>`, `EditRadioString`, `EditBoolNullRadio`) gain the same unconditional `aria-labelledby` in edit mode, from one shared attribute block. Nothing visual changes; screen-reader output for those combinations does.
- **`EditMultiSelect<TValue>`, `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, and `EditFile` now apply unmatched attributes to their root `.edit-control-wrapper`.** A `style`, `data-*`, `title`, or any other stray attribute written on one of these four was previously captured and silently dropped — they are `ComponentBase`-derived (not `InputBase`), so nothing splatted the captured dictionary anywhere. It now lands on the wrapper, with the component's own inline `style` hand-merged (consumer last, so its declarations win) and the splat emitted first so the explicit `class` still wins. **`class` is unchanged** — it keeps flowing to the field element (the select engine / checkbox fieldset / drop zone) alongside Blazor's field-state classes, same as every other control; `ContainerClass` remains the wrapper's class channel. The only visible difference is that attributes which used to vanish now render.

**Fixes** (Edit Controls)
- **Controls now unregister from `FormOptions` when disposed.** A scalar control (or `EditRadio`) removed from the render tree behind an `@if` — a tab switch, a collapsed section, a closed modal — left its field registered forever: `ValidationView` kept rendering a summary link to an element no longer in the DOM, and the per-form registration list grew with every mount/unmount cycle. The list controls already unregistered; every control now does, through one shared register/unregister pair. Relatedly, `EditBool` — the only `InputBase`-derived control implementing `IAsyncDisposable` — never ran its synchronous dispose at all (Blazor treats the two dispose interfaces as exclusive), which also skipped its validation-event unsubscribe; its `DisposeAsync` now invokes the synchronous chain.
- Guarded the new flex checkbox/radio labels against two common consumer CSS resets exposed by the 8px-gap change above: an app-level `input { flex: ... }` rule no longer stretches the plain checkbox/radio `<input>` across the row (the direct-child input is now pinned `flex: none; margin: 0`), and an `input[type=checkbox] { margin-top: -3px }` baseline nudge written for the old whitespace-based layout no longer shoves the box off-center against the row's `align-items: center` — both guards carry an `[type=...]` specificity bump so load order can't decide it. Demo `EditRadio`'s hand-rolled `<label>`s now carry `edit-radio-label` to match what the control's own markup produces.
- **`Size="Small"`/`"Large"` now actually sizes a legacy-mode (non-affix) input inside `.edit-theme`** — a visible change wherever the two were combined. `.edit-theme`'s `edit-input-sm`/`-lg` rules lost a specificity tie to the base chrome rule (`0,3,0` vs `0,4,0`), so the size class was a silent no-op there for `EditString`/`EditNumber`/`EditTextArea`/`EditDateNative` unless an affix parameter had already switched the control into the wrapper-sized affix layout. The size selectors now carry the same `:not([type="checkbox"])` qualifier as the base rule, tying at `0,4,0` and winning on source order. Affix-mode and un-themed rendering are unchanged; see [Opt-in AntD theme](#opt-in-antd-theme-for-the-classic-edit-inputs-edit-theme).
- **An enum display name declared with `[Display(Name = ..., ResourceType = ...)]` now re-resolves per culture on every render.** `EnumHelpers.GetName` memoizes per (enum type, member name), which froze a resource-backed name at whichever culture happened to render first — process-wide, so on Blazor Server one circuit's language leaked into every other user's. Only the "this member is localized" decision is cached now; the name itself goes back through `DisplayAttribute.GetName()` (and therefore `CultureInfo.CurrentUICulture`) each call. Non-localized `[EnumDisplayName]`/`[Display(Name = "literal")]` names stay fully memoized — no per-render cost added for the common case. Affects every enum-driven control: `EditSelectEnum<TEnum>`, `EditRadioEnum<TEnum>`, `EditCheckedEnumList<TEnum>`, and the read-only displays derived from them.
- **A property decorated with `[Display(Name = "...")]` now gets its validation messages rewritten like every other property.** `ValidationHelper`'s rewrites (`Required`, `StringLength`, `MinLength`, `MaxLength`, "must be a number") are deliberate exact-string matches against the framework's own message text — proof that DataAnnotations produced *this* message for *this* field with *these* bounds, which is what makes replacing it safe. DataAnnotations formats with `ValidationContext.DisplayName`, i.e. the `[Display(Name)]` spelling, so a decorated property's message ("The Given Name field is required.") matched none of the member-name candidates and rendered as the raw framework sentence instead of "Required". `FieldValidationDisplay` now also passes that spelling — resolved via `GetName()`, so a localized `[Display(Name = ..., ResourceType = ...)]` works too — and both spellings are tried. `ValidationHelper.GetValidationMessage` gained an overload taking a `displayName` argument for this; the existing overload is kept and forwards `null` (member name only), so external callers are unaffected. `[DisplayName]` needs nothing here and is unchanged — DataAnnotations doesn't read it, so those messages always carried the member name already.
- `IdPrefix=""` no longer produces ids like `-FirstName`. The prefix (and the `FormGroupOptions.Name` prefix beside it) is applied only when non-empty; an empty string is now indistinguishable from unset. A leading hyphen makes an id that `document.querySelector("#-FirstName")` rejects outright, which broke the `ValidationView` summary links and `FocusFirstInvalidField` for the whole form.
- **Checkbox and radio option lists now de-duplicate their option ids.** Each option's id segment comes from a sanitizer that strips everything outside `[A-Za-z0-9-_]`, so a list of non-ASCII labels (all-CJK options, say) collapsed to the same empty segment for every entry — and with duplicate ids, every `<label for>` resolved to the **first** input, so clicking any label toggled the first option. `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, `EditRadioString`, and `EditRadioEnum<TEnum>` now route their whole option list through a new public `EnumHelpers.ToUniqueIds<T>(IReadOnlyList<T> options, string? reserved = null)` helper (`reserved` keeps `EditRadioString`'s built-in `"other"` segment verbatim). **Ids change only for colliding options** — the first option to claim a sanitized segment keeps it, so an ordinary ASCII list produces exactly the ids it always did (bUnit selectors, e2e locators, and visual baselines all pin those); a collision or an empty segment falls back to the option's index.
- An empty enum (no members) combined with `HasOtherOption` no longer fabricates a phantom `"0"` option. The "Other always sorts last" logic pulls the last member aside before sorting and re-adds it afterwards, gated on a null check — but `TEnum` is unconstrained, so for a value-type enum `default` is the zero *member*, not null, and the guard passed even with nothing to re-add. The extraction is tracked with an explicit flag now. `EditRadioEnum<TEnum>`'s read-only "is Other selected" check is guarded for the same empty list (it indexed the last element unconditionally).
- `EditSelectEnum<TEnum>` ports `EditSelectString`'s no-JS `selected=` fallback: under static/no-JS rendering (prerender, bUnit) a nullable `EditSelectEnum` with a null or unmatched value used to visually show the first enum member selected — no `<option>` carried a `selected` attribute, relying entirely on JS to set the DOM value — until JS attached, even though the bound value was null/unmatched. A hidden, disabled placeholder `<option>` now also covers a non-nullable enum whose current value has no defined member (e.g. a removed enum value read back from storage), the same gap `EditSelectString` already guarded against.
- **Security: `EditString`'s read-only link mode now mirrors the browser's full href preprocessing — trimming leading/trailing C0 control-or-space, then stripping ASCII tab/CR/LF — before checking the URL scheme, closing two `javascript:` bypasses of the http/https/mailto allow-list.** `Uri.TryCreate(Url, UriKind.Absolute, ...)` fails to parse a URL carrying a tab/newline inside or right after its scheme (e.g. `java<TAB>script:alert(1)`), *or* a URL with a leading C0 control byte (e.g. a leading `U+0001` before `javascript:alert(1)`) — a C0 control isn't a valid scheme-start character either. The old code treated anything unparseable as a safe relative URL, rendering it into `href` verbatim in both cases. A browser's own URL parser trims leading/trailing C0-control-or-space and strips embedded tab/CR/LF before parsing, then re-forms and runs the `javascript:` URL on click — `SafeUrl` now applies both preprocessing steps, in the browser's order, first, so the allow-list check sees exactly what the browser will see, and renders the fully-preprocessed value (never the raw `Url`) when it passes.
- **`EditNumber<T>` no longer renders `step="1.0"` by default — a documented behavior change.** A fractional value (`12.34m`) arriving with neither an explicit `Step` parameter nor a `[Step]` attribute was natively invalid on arrival under the old hardcoded default, silently blocking a native form submit (`EditForm` emits no `novalidate`) before `OnValidSubmit`/`OnSubmit` ever fired. With neither set, non-integral `T` (`float`/`double`/`decimal` and their nullable forms) now renders `step="any"`, and integral `T` renders no `step` attribute at all (the native default of 1 is already correct) — matching the framework's own `InputNumber<T>`, which never renders `step`. An explicit `Step` parameter or `[Step]` attribute is unaffected and still renders exactly as before.
- `EditTextArea`'s `AutoSize` now re-measures when the bound value changes from outside the control (e.g. a parent loading a record into the model) or when `AutoSize` itself flips on at runtime. Both previously left the textarea clipped at its old height until the next keystroke — measurement only ran on first render and on the user's own typing.
- `EditBool`'s `Indeterminate` now survives a genuine user click. A checkbox click resets the DOM `indeterminate` property to `false` as part of the browser's own pre-click handling, but the control's internal mirror didn't notice, so a checkbox whose `Indeterminate` parameter stayed `true` throughout lost its dash permanently after the first click instead of it being reapplied on the next render.
- `EditFile`'s file input and drop zone no longer carry a hardcoded `aria-label`, letting the associated `<label for>` supply the field's actual accessible name. `aria-label="Choose files"` won by accessible-name precedence over the field's own label, so no `EditFile` ever announced its bound field's name (also a WCAG 2.5.3 Label in Name failure); the role-less drop zone's `aria-label="File upload area"` was also inert (prohibited on an element with no ARIA role) and is removed too.
- `EditFile`'s drop zone no longer wires a managed `dragover` handler — `dragenter`/`dragleave` alone drive the hover highlight, and a handler-less `@ondragover:preventDefault` still lets the drop event fire. `dragover` fires continuously (~60/s) while a file is dragged over the zone; on Blazor Server each one used to ship a serialized `DataTransfer` payload over SignalR for a no-op re-render.
- `edit-controls.js`'s back-compat `window.log`/`window.logError`/`window.logWarn`/`window.logInfo`/`window.focusFirstInvalidField` shims now use `??=` instead of an unconditional assignment, so they no longer clobber a host page's own same-named globals — relevant to a cross-origin MFE that links this file as an ES module import rather than a `<script>` tag.
- `focusFirstInvalidField` now scrolls with `behavior: 'auto'` instead of a hardcoded `'smooth'` when the user has `prefers-reduced-motion: reduce` set, matching the reduced-motion handling both stylesheets already apply elsewhere.
- **A `[Range]` bound at a narrow integer type's extreme is now named in the validation message instead of being suppressed as a "no bound" sentinel — a visible message change.** The rendered `min`/`max` attributes and the range-message rewrite each kept their own sentinel list and disagreed on 8 of the 12 numeric extremes: `[Range(1, 255)]` on an `int Quantity` rendered `max="255"` while the message said only "Must be at least 1" — vacuous for an entry of 300, and silent about the ceiling the user had just violated; `[Range(-32768, 100)]` rendered `min="-32768"` while the message claimed there was no floor. Both layers now resolve through one shared predicate, and the `sbyte`/`byte`/`short`/`ushort`/`uint`/`ulong` extremes are out of it on both sides (neither layer can see the bound property's CLR type, and at those magnitudes a real bound is far likelier than a vacuous one — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue)). Messages under an `int`/`long`/`decimal`/`double`/`float` extreme are unchanged; a genuinely vacuous `[Range(0, 255)]` on a `byte` now names both bounds.
- **`EditDateRange`'s shared calendar bounds are now the looser of the two fields' own, not a first-non-null pick.** `Min` used to resolve param → `Start`'s attributes → `End`'s, and `Max` param → `End`'s → `Start`'s, so whenever both fields declared the same kind of bound the "natural" field won even when the *other* field's bound was looser — the calendar blocked dates that other field's own validation accepts, with no message explaining why. `Min` now takes the earlier of the two minimums and `Max` the later of the two maximums, each still falling back to whichever single field declares one (so the natural `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` pairing, and a single `[Range]` on one property, behave exactly as before). The result is the convex hull rather than the union — disjoint per-field windows leave the gap selectable, which the annotations still reject at validation time. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
- The `float` range sentinels are computed under the validation-time culture like every other candidate instead of matching a frozen invariant-format literal. `[Range(-100f, float.MaxValue)]` under a `,`-decimal culture (de-DE) matched no branch, so the raw `3,4028234663852886E+38` reached the user — exactly the scientific notation the one-sided rewrite exists to suppress. The mirror `[Range(float.MinValue, 100f)]` failed the same way.

**Fixes** (Select engine — `Select` / `EditSelectSearch` / `EditMultiSelect`)
- **A string-bound `Select`/`EditSelectSearch` holding `""` now shows the placeholder and no clear button.** `default(string)` is null, not `""`, so an empty string counted as a real selection: the trigger rendered an empty label where the placeholder belonged, alongside a live clear button that appeared to do nothing. The "is something selected" test now excludes the empty string — **unless an option literally carries `""`**, in which case the option lookup wins and it stays a genuine selection, which is what makes an explicit "None"/"Any" option work. The same verdict the native selects reach in `IsValueDefault` and the one `HidingMode` documents, so all three now agree; `EditSelectSearch` also picks up the `HidingMode.WhenNullOrDefault` empty-string treatment here, making it the last string-capable control to align (see **Changed** above).
- Clicks on the dropdown panel's own chrome no longer close a `ShowSearch="false"` select. The parts of the panel carrying no click handler of their own — group headers, the empty/"No data" row, the panel's padding and scrollbar gutter — bubbled into the wrapper's click handler, which reads any wrapper click as a toggle when there's no search input to focus instead. The panel now stops propagation itself; option rows and `DropdownFooter` already did, so their own handlers still run and still commit.
- Fixed label drift on a duplicate-`Value` option list: `Select`, `EditSelectSearch`, and `EditMultiSelect` each built their own value→option lookup with a different tie-break on a duplicate `Value` (last-wins dictionary vs. first-wins `FirstOrDefault` vs. first-wins `TryAdd`) — the same bound value could render a different label in the interactive dropdown than in a read-only view. A new shared `SelectOptionLookup.Build` helper (last-wins, matching the engine's own tie-break) is now the single source of truth for all three; also removes `EditSelectSearch`'s O(n) per-render label scan.
- Tags mode: deselecting a tag no longer erases the label of a same-valued option still supplied by `Options`. A user-created tag that stops being selected also leaves the option list (matching AntD, so a removed typo-tag doesn't stay selectable forever) — but the removal deleted that value's lookup entry outright, including a live entry `Options` provided, after which the value's label fell back to `ToString()`. The lookup is derived from `Options` + the tag list, so it is now rebuilt from what's left rather than having one key removed. Same treatment on the clear-all path. Matters most for the server-echo pattern: commit a tag, the server returns it as a real, better-labelled option, then the user deselects and reselects it.

**Fixes** (UI Kit)
- **The loading-toast spinner rendered visibly cropped** — `MessageListView`'s local copy of the spinner glyph had drifted from the canonical one in both path data and `viewBox`. All inline SVG glyphs now come from the single icon registries, which also fixed the `Table` expand chevron's silently corrupted path coordinates (a hand-retyped copy) and removed a duplicated XSS-invariant helper.
- **Week-mode pickers no longer throw on `default(DateTime)`/`DateTime.MinValue`.** `DatePicker`/`DateRangePicker` at `Mode="Week"` build their grid from six week starts, and the week-start subtraction underflowed `DateTime.MinValue` for year 1 — so a `Mode="Week"` picker (or an `EditDate`/`EditDateRange` over one) bound to an unset non-nullable date crashed at *first render*, which on Blazor Server takes the circuit down. The lead is now clamped to the days actually available, yielding that partial first week's own start. The mirror case is fixed too: a typed commit landing in year 9999's last week, whose 7-day span runs past `DateTime.MaxValue`, no longer overflows while the commit guard compares the week end against `Min`.
- **`Tabs` now raises `ActiveKeyChanged` when the tab a bound `ActiveKey` names is removed or disabled.** The strip has always silently fallen back to the first enabled tab in that case, but only a user click raised the event — so a bound key kept pointing at a tab that was no longer rendered or no longer usable, and the consumer's own pane/filter state disagreed with the highlighted tab until the next click. The fallback is now reported with the key actually active, once per distinct fallback (a consumer that ignores it isn't told again, which would loop). Never raised for a null `ActiveKey`: null is the documented "activate the first enabled tab", so that's not a desync. Notified from `OnAfterRender` rather than mid-render, so a `Key`/`Disabled` change on an already-registered tab can't report a fallback that isn't real.
- **`Table` column filter: closing a filter now returns focus to its own funnel button** — unless another column's filter just opened, in which case focus stays in the newly-opened panel. Previously the closing filter unconditionally pulled focus back to its trigger, so clicking straight from column A's funnel to column B's left B's panel open but unfocused, and B never saw Escape or any other key. A plain close (Escape, outside click, OK, Reset) leaves no filter open and still returns focus to the funnel, as before.
- **`Table`: the select-all checkbox's mixed (indeterminate) state now survives a `SelectionMode` `Single` → `Multiple` round trip.** `indeterminate` is a DOM property with no HTML attribute, so it is mirrored from C# via JS only when the value changes. `Single` mode renders no select-all `<input>` at all, but the mirror still "applied" the state to a default `ElementReference` (a silent no-op) *and* recorded it — so switching back to `Multiple`, which brings in a freshly-created checkbox with `indeterminate == false`, short-circuited against that stale record and announced a partial selection as "not checked" instead of "mixed". The mirror is now forgotten whenever the element isn't there to mirror onto (the same treatment `Selectable` and a runtime `UseStyledCheckbox` change already had).
- **`Table` column filter: a close→reopen racing the in-flight positioning call no longer leaks window listeners for the circuit's lifetime.** Under `ScrollY` the escaping dropdown is positioned by a JS call that also wires scroll/resize listeners and returns a handle to release them. The call site re-checked "still open" after the await but held no sequence token, so a close-and-reopen across that round trip orphaned the first handle — its listeners were never released, and they accumulated with every such race. The handle now lives in a new internal `JsHandle` holder owning the token, the two-step release, and the no-JS degrade — the same race guard `Modal`/`Drawer` already had, now shared rather than reimplemented, so a call site can't express the bug.
- **Two overlapping lazy JS-module imports no longer import twice and strand an `IJSObjectReference`.** The internal `JsModule` holder cached the resolved reference, not the in-flight import task — so two callers that both started before the first import resolved each got their own module, and the loser's reference was never disposed, held for the rest of the circuit. It caches the task now (a failed import is still uncached, so the next render retries).
- **A transient JS import failure no longer permanently strands a picker or select without its focus-out dismiss wiring.** `DatePicker`/`DateRangePicker`'s `initPicker` and `Select`'s `initInput` are one-time wirings, and the "already wired" flag was latched *before* the awaited import — so a single import failure (a dropped/slow circuit at exactly the wrong moment) marked the control wired forever, leaving it without Enter form-submit suppression and, worse, without the tab-away dismiss that keeps an abandoned dropdown's invisible backdrop from swallowing the next click anywhere on the page. The flag is now set only on success, matching `JsModule`'s own contract that a failed import retries on the next render.
- Day cells in both pickers now carry `aria-current="date"` on today, matching the month/quarter/year cells that already did — "today" was conveyed to sighted users by a CSS ring with nothing in the accessibility tree behind it.
- `DateRangePicker`'s `Week`-mode grids now show the whole-row hover band `DatePicker`'s own `Week` mode already had (a week row, not a day, is the selection unit there, so per-day hover was the wrong affordance). Relatedly, hovering no longer overrides a row's committed-range or hover-preview tint: the week row's own background *is* that tint, so hover now yields to it explicitly rather than painting over it — plain rows in either picker still get the band.
- Week **numbers** are now always Gregorian, even under a culture whose default calendar isn't. The pickers are Gregorian-calendar controls and force Gregorian for every format they emit by swapping `DateTimeFormat.Calendar`, but the week-number lookup read the culture's own default `Calendar` instead — so `ar-SA`, `fa-IR`, and `th-TH` numbered the week in Umm al-Qura / Solar Hijri / Buddhist terms next to an otherwise entirely Gregorian panel. Identical output for every culture that already defaults to Gregorian, `en-US` included.
- `Popconfirm` no longer leaks a JS object reference when disposed while its module import is in flight — its focus path re-imported `wss-overlay.js` without the disposal re-check its own base class documents. All lazy module imports now flow through one internal guarded holder (`JsModule`), so the guard can't be omitted again; post-dispose calls also no longer import a throwaway module.
- `.wss-table-filter-trigger`'s keyboard focus ring gains the same 2px corner radius its sort-trigger sibling already had (the two header buttons were inconsistently styled).
- `edit-controls.css`'s styled-checkbox check glyph now honors the `--color-on-primary` bridge instead of hardcoding `#fff` — dark/high-contrast themes previously got a correct check mark on the Table's styled checkbox but a hardcoded-white one on `EditBool`'s.
- `SearchInput`'s clear (`AllowClear`) and search buttons now pin an explicit `height`/`min-height: var(--wss-control-height)` — previously unset, so a consumer reset like `button { max-height: fit-content }` could collapse them below the input's height and break the seamless pill layout (the same defense already applied to `Pagination`'s prev/next buttons).
- Consolidated the JS overlay-positioning helpers to fix inconsistent flip behavior: `wss-select.js`'s dropdown placement used to flip to the opposite side whenever it merely had *more* room than the preferred side (which could still overflow), while `Popover`/`Popconfirm`'s `place()` and the `ScrollY`-fixed dropdown positioning only flipped when the opposite side actually fit — two different policies answering the same question. A new shared `fits()` helper in `wss-overlay.js` (imported by `wss-select.js`) now makes every flip site agree on the safer "does the opposite side actually fit" policy; new `stackWithBackdrop()` / `wireDismissOnFocusOut()` helpers also dedupe the backdrop z-index stacking and tab-away dismiss wiring that `wss-select.js` and `wss-picker.js` each reimplemented separately.
- **`wss-select.js`'s horizontal dropdown clamp is now two-sided, replacing 10.6.7's "anchor from the wrapper's right edge" behavior.** Right-anchoring keeps overflowing whenever the wrapper's own right edge is already at or past the viewport's, and for a dropdown wider than the room remaining (the `Pill` variant on a narrow viewport) it pushed the dropdown's *left* edge off-screen — unreachable option text, strictly worse than the right-side clipping it was avoiding. It now shifts left only as far as the viewport's left margin, via the shared clamp helper the other overlay sites use. Still no movement whenever there's room. See [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).
- **`wss-tooltip.js` no longer picks `<body>` as its bounds frame, and intersects the chosen frame with the viewport.** `body { overflow-x: hidden }` is near-ubiquitous boilerplate, and it made `<body>` qualify as a clipping ancestor — whose rect is the whole *document*, as tall as the page and with its top well above the viewport once scrolled, so the "am I in the lower half?" test that decides the flip answered against the document instead of the screen and a trigger near the viewport bottom opened downward, off-screen. `<body>` is now skipped explicitly (a page that genuinely wants a body-sized frame gets the viewport, the same box for an unscrolled page), and any frame that *is* accepted — a modal body, a scroll panel, a recognized `wss-modal`/`wss-drawer`/`wss-popover` panel — is intersected with the viewport, since only its visible part can hold the bubble. Affects both `data-tooltip` and the form controls' `LabelTooltip`, which share the module. See [Hover tooltips](#hover-tooltips-data-tooltip).
- Theming and RTL cleanups across both stylesheets, all with unchanged un-themed rendering: `EditFile`'s drop-zone background/border and its file rows' name/size text now route through the `--color-page-background` / `--border-color` / `--color-text` / `--color-text-secondary` bridges instead of bare literals (previously no override hook at all, so a dark theme got a light-grey card); `.edit-radio-other-input` and the indeterminate styled checkbox now consume the `--edit-color-border` token rather than an ad-hoc bridge of their own — the "Other" input's *un-themed* fallback shifts `#ccc` → `#d9d9d9`, the AntD border value the rest of the file already uses; `Alert`'s icon/actions/close offsets and `Pagination`'s size-changer arrow are now logical properties, so they mirror correctly under `dir="rtl"` (identical computed values under LTR); `prefers-reduced-motion` now actually stops the `Table` expand chevron's rotation (the rule listed the button, but the transition is on the `svg` inside it); and a small (`Size="Small"`) searchable select no longer double-insets its typed text by 7px — the search overlay already spans the selector's padding box, so the size rule's own inset stacked on top of it.

**Internal**
- ~14 commits of pure internal refactoring — extracting shared bases/components used by multiple controls (`EditInputShell`'s size/CssClass/count-text assembly, `CheckboxOptionList`, `RadioOptionItem`, `SelectOptionList`, `EnumOptionCache`, `EditControlParametersBase`, `OverlayActivationBase` for `Modal`/`Drawer`, `PopupOverlayBase` for `Popover`/`Popconfirm`, `ToastQueue` for `MessageService`/`NotificationService`, `UiKitIcons`), deduping CSS (`Popover`/`Popconfirm` floating-panel rules) and e2e test scaffolding (hoisted per-control demo-page/baseline tests) — plus dependency and test-infra bumps (ASP.NET Core Components 10.0.0 → 10.0.10, bUnit migrated to 2.8.6, Microsoft.Playwright 1.49.0 → 1.61.0 with baselines regenerated for the newer bundled Chromium's font rendering, Test SDK/xunit-runner/coverlet bumps). No consumer-facing API or behavior change in any of these — one purely cosmetic `<option>` attribute-order difference in `EditSelectString`'s rendered markup aside.
- A repo-wide DRY audit pass (~16 commits): new intermediate bases (`RadioGroupControlBase<TValue>` for the radio pair, `CheckedListControlBase<TItem>` for the checkbox-list pair, `EditTextControlBase<TValue>`/`EditTextInputBase` for the text-shaped scalars) hoisting each family's hand-synced members with parameter names/types/defaults unchanged; the two pickers' verbatim-duplicated 55-line time row extracted into one `PickerTimeRow` (with its DisabledTime/step-filter/never-jump invariants documented once) and their mode→format/first-day-of-week/read-only-display logic consolidated into `PickerMath`; `EditDateRange`'s ~70-line mirrored copy of the list base's validation-subscription/field-registration plumbing hoisted into `EditControlParametersBase`; every lazy JS module import unified on an internal `JsModule` holder; every inline SVG glyph single-sourced from the icon registries; the 17-copy `OnInitialized`/`InitState` boilerplate moved into the two control roots (`InitState` de-genericized — a protected-surface source break only for out-of-repo subclasses of the abstract bases, none known); `SelectLabelCache` for the searchable wrappers' read-only labels; CSS token/grouping consolidation (`--edit-shadow`, `--edit-ease-standard`/`--wss-ease-standard`, `--edit-color-border`, shared backdrop/button-chrome/clear-button groups, the checkbox check-glyph SVG as a per-file token); a shared JS `applyVerticalFlip`; the bUnit `WithForm` builder deduplicated from 41 files into one helper; and a new scaffold conformance suite asserting the shared wrapper/label/star/aria invariants across all 19 form controls (the net that would have caught the `EditBool` star drift). DOM output byte-identical throughout except where the **Changed**/**Fixes** entries above say otherwise.

**Demo** (`WssBlazorControls.Demo`)
- `DemoEditDate.razor` now demos the calendar-dropdown picker and `DemoEditDateNative.razor` the native input, following the rename — swapped content, not new pages.
- `DemoEditRadio`'s eager validation actually runs again (its copy of the shared page boilerplate had been edited to gate on a cascading parameter nothing cascades, so its Required section never showed the invalid state on load); the two checked-list pages drop their `Task.Delay(10)` validation timing hack; and the `EditForm? _form` + eager-validate boilerplate all 22 demo pages repeated now lives once on a `DemoFormPage` base, so a page's copy can't silently drift again.

### 10.6.7

**New** (Edit Controls)
- `EditBool.Indeterminate` (`bool`, default `false`) — AntD's visual-only "mixed" checkbox state (does not change the bound value). Applied via JS after render (there's no HTML attribute for the `indeterminate` DOM property) through a new shared `wss-checkbox.js` module; the UI-kit `Table`'s header "select all" checkbox now imports the same helper instead of its own copy (`wss-table.js` re-exports it, so its module path is unchanged). Works with or without `UseStyledCheckbox`; degrades to a plain checked/unchecked box with no JS runtime. See [Indeterminate ("mixed") state](#indeterminate-mixed-state).
- `IsOptionDisabled` (per-option disabling) on `EditCheckedStringList` (`Func<string, bool>?`), `EditCheckedEnumList<TEnum>` (`Func<TEnum, bool>?`), `EditRadioString` (`Func<string, bool>?`), and `EditRadioEnum<TEnum>` (`Func<TEnum, bool>?`) — disables the matching option in addition to (not instead of) the whole-group `IsDisabled`. Null (default) disables nothing. See [Per-option disabling](#per-option-disabling).
- `EditRadioString`/`EditRadioEnum<TEnum>` gain `OptionType="RadioOptionType.Button"` — Ant Design's segmented "button" radio look (joined bordered buttons instead of plain radios), with `ButtonStyle` (`RadioButtonStyle.Outline`/`Solid`) and `Size` (the existing `SelectSize`) applying only in button mode. Same `InputRadio`/`InputRadioGroup` keyboard semantics; button mode is inherently horizontal (`IsHorizontal` is ignored) and composes with `HasOther`/`HasOtherOption` and `IsOptionDisabled`. New CSS-only mode (`.edit-radio-button-*` in `edit-controls.css`), not gated behind `.edit-theme`; default mode's markup is unchanged. See [Button-style radio group](#button-style-radio-group-optiontypebutton).
- `EditFile` AntD 4.x parity batch (Upload, minus the transport, which stays deliberately out of scope): `AllowedExtensions` now also accepts full MIME types (`"application/pdf"`) and MIME wildcards (`"image/*"`) alongside bare/dotted extensions, honored by both the `accept` attribute and validation (previously every token was dot-prefixed regardless of shape, turning a MIME token into a meaningless, silently-rejecting one); `BeforeAdd` (`Func<IBrowserFile, Task<bool>>?`) is a new async per-file gate run after the built-in format/size/count/duplicate checks and before buffering, with a localizable `BeforeAddRejectedMessageFormat`; every selected file's row (edit-mode and read-only) now shows its formatted size; and `Variant="EditFileVariant.Button"` swaps the dashed dropzone for a compact plain button (`ButtonText`), built on the same invisible-`<InputFile>`-overlay technique so keyboard/focus/click behavior match. All additive — default `Variant="Dropzone"` markup is unchanged, and the empty-list state renders no new markup. See [File upload parity features](#file-upload-parity-features-editfile).

**New** (UI Kit)
- Hover tooltips (`data-tooltip`) — ported from the RPG Assistant app's `data-tooltip` convention. Not a component: a `data-tooltip="..."` attribute on any element gets a styled CSS-only hover/focus tooltip (arrow, slide-in, `:focus-visible` support, hidden under `hover: none`) via new rules in `wss-controls.css`, themed through `--wss-*` tokens plus the new `--wss-tooltip-gap` / `--wss-tooltip-z-index` knobs. The optional new `wss-tooltip.js` (a plain `<script>` tag, no interop) auto-places the bubble — above/below and left/right — based on the trigger's position within its nearest clipping ancestor or panel boundary (`wss-modal` / `wss-drawer` / `wss-popover`), so it stays inside a Modal/Drawer instead of running past the edge. See [Hover tooltips](#hover-tooltips-data-tooltip).
- `Select`/`EditSelectSearch`/`EditMultiSelect` AntD 4.x parity batch: `Loading` (spinner in the arrow's slot + `aria-busy`) and `ShowArrow` (default true, unlike Ant Design's hide-for-searchable-multi default — kept on to preserve byte-identical DOM); `SelectOption.Group` renders an AntD-`OptGroup`-style header before each contiguous run of a shared group name in the flattened, virtualized dropdown (keyboard nav skips header rows; a header shows only while one of its options survives the filter); `FilterOption` replaces the default `Label.Contains` match (including for an empty search — `(_, _) => true` disables client filtering for a pure server-driven `OnSearch` flow); `EmptyContent` (richer alternative to `EmptyText`) and `DropdownFooter` (Ant Design's `dropdownRender`, pinned after the list, its own clicks never select/close); a two-way-bindable `Open`/`OpenChanged` (`@bind-Open`) that routes an externally-driven open/close through the same JS placement/focus path as user interaction, guarded against re-triggering on its own echoed value; `SelectVariant.Borderless` (single-select only — `EditMultiSelect` doesn't forward `Variant`); and `wss-select.js`'s `placeDropdown` now clamps horizontally (mirroring its existing above/below flip) when a wide dropdown would run off the right edge of the viewport. All additive — see [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).
- `Pagination`/`Table` AntD 4.x parity batch, all additive/dependency-free (no new JS module): `Pagination` gains `ShowTotal` (the "1-10 of 200 items" leading text), a `PageSizeOptions` native `<select>` size-changer (`PageSize`/`PageSizeChanged` now two-way bindable; changing size re-clamps `Current` to keep roughly the same data window in view), `ShowQuickJumper` (a "Go to" input, Enter commits and clears), and `Small` (AntD's compact size, CSS-only). `Table` forwards `ShowTotal`/`PageSizeOptions` to its embedded in-memory pager (selection stays keyed by row identity across a page-size change) and adds: `Loading` (a translucent mask + spinner over the body, rows still rendered beneath it, `aria-busy` on the wrapper); `IsRowSelectable` (per-row selection predicate — a rejected row's control renders `disabled` and is excluded from header select-all / indeterminate math); `SelectionMode.Single` (radio-semantics selection, one shared native radio group per `Table`, an empty header cell in place of select-all); controlled expansion via `ExpandedRowKeys`/`ExpandedRowKeysChanged` layered over the existing uncontrolled expansion set, plus `OnExpand` (raises on every toggle regardless of control mode); `ExpandRowByClick` (whole-row click toggles `RowDetail`) and `OnRowClick` (always fires; composes with `ExpandRowByClick`) — both stop propagation from the selection checkbox/radio, the expand chevron, and `ActionColumn` cells, so existing action buttons need no changes; `Column.Ellipsis` (CSS truncation — the table switches to `table-layout: fixed` only once ≥1 column requests it; `PropertyColumn` also adds a hover `title` with the full text); `EmptyContent` (richer alternative to `EmptyText`); and `FooterContent` (a consumer-supplied summary row in a `<tfoot>`, unaffected by paging/sorting). See [Pagination parity features](#pagination-parity-features-pagination) / [Table parity features](#table-parity-features-tabletitem).
- `Table` column filtering + `ScrollY`, additive/opt-in (unset, DOM is unchanged): `Column.FilterOptions` (a new `TableFilterOption` `Text`/`Value` list) + `OnFilter` (`Func<TItem, string, bool>`) render a funnel-icon header button (after the sort control on a sortable column) that opens a checkbox (or, with `FilterMultiple="false"`, single-select radio) dropdown — OK applies and closes, Reset clears immediately, an outside click discards pending checkbox changes without applying them (the same JS-free backdrop pattern as `Popover`/`Select`). A row passes a column's filter when `OnFilter` matches ANY selected value (OR within a column); every filterable column must pass (AND across columns). Filtering runs before sorting/paging, and a selected row filtered out of view stays in `SelectedItems` (same key-based preservation as paging). Uncontrolled filter state only — `Table.OnFilterChanged` (`EventCallback<(Column<TItem> Column, IReadOnlyList<string> SelectedValues)>`) observes every apply/reset; there is no fully-controlled `filteredValue` equivalent. `Table.ScrollY` (`string?` CSS length) bounds the body to a fixed height with its own scrollbar and a sticky header (AntD's `scroll.y`; viewport-level sticky is out of scope) — a filter dropdown that would otherwise be clipped by that wrapper's overflow escapes via `position: fixed` (a new `wss-overlay.js` export, `placeFixedBelow`), falling back to the CSS-anchored position with no JS. See [Table parity features](#table-parity-features-tabletitem).
- `Modal`/`Drawer` AntD 4.x parity batch, all additive (unset, DOM/behavior unchanged): `Modal.Centered` (`bool`) vertically centers the dialog instead of the default fixed 100px-from-top offset (CSS-only, `wss-modal-wrap-centered`). Both gain a `Keyboard` (`bool`, default true) parameter that now solely governs Escape-to-close, decoupled from `Closable` (which now only shows/hides the header X) — matches AntD, where `keyboard`/`closable`/`maskClosable` are three independent knobs; previously Escape was gated by `Closable`, so a consumer who set `Closable="false"` to hide the X also silently lost Escape-to-close (now set `Keyboard="false"` explicitly for that). `Drawer.Extra` (`RenderFragment?`) renders a header-right slot beside the close button (AntD's `extra`) — grouped with the close button in a new `wss-drawer-header-actions` wrapper only when `Extra` is set (unset markup is byte-identical to before); `Extra` alone (with no `Title`/`Closable`) now forces the header to render.
- `Popconfirm` AntD 4.x parity batch, all additive: `OnConfirm` handlers that return a genuinely-pending `Task` (checked via `Task.IsCompleted` immediately after invoking — a still-synchronous/already-completed handler keeps today's immediate-close feel) now keep the popup open with both buttons disabled and a small spinner in the OK button until the task resolves, closing only on completion; an exception closes the popup and rethrows (never swallowed). `OkDanger` (`bool`) applies red/danger primary styling to the OK button (new `wss-dialog-btn-danger` class, alongside the existing `wss-dialog-btn-primary`). Both `Popconfirm` and `Popover` gain a controlled `Visible`/`VisibleChanged` (two-way, `@bind-Visible`) mirroring `Select`'s controlled `Open`/`OpenChanged` design: an external `Visible` change while `VisibleChanged` has a delegate routes through the same open/close path as user interaction (JS placement/focus still runs), every open/close raises `VisibleChanged` back, and a `_lastVisibleParam` guard prevents a `@bind-Visible` echo from re-triggering. `Popconfirm.Disabled` (existing param) now also closes an already-open popup the moment it becomes true (any control mode) and ignores an externally-forced `Visible="true"` while disabled — the same "Disabled ⇒ closed" invariant as `Select`'s controlled `Open`. `Popover` has no `Disabled` parameter, so its controlled `Visible` has no such guard.
- `Alert` gains `Banner` (`bool`) — AntD's banner mode: full-width, no border/radius, and (when `Type` is left at its default `Info`) a `Warning`-style icon/tint to match AntD's banner default; an explicitly-set `Type` is left alone. `Action` (`RenderFragment?`) renders a trailing slot before the close button (new `wss-alert-actions` wrapper only when `Action` is set — unset markup is unchanged).
- `SearchInput` gains `AllowClear` (`bool`) — a clear × button (reusing `EditInputShell`'s `EditIcons.ClearCircle`) rendered as a new flex sibling between the input and the search button whenever there's a non-empty `Value` (never when `Disabled`); and `EnterButtonText` (`string?`) — when set, the search button renders this text (primary-styled, `wss-search-btn-enter`) instead of the search icon (AntD's `enterButton="Search"`).
- `NotificationContainer`/`WasmNotificationContainer`/`NotificationListView` gain `Placement` (`NotificationPlacement`: `TopRight` default / `TopLeft` / `BottomRight` / `BottomLeft`) — a render-tree-scoped parameter on the container components (MFE-safe; not a service-level setting) that repositions the fixed toast stack and its slide-in direction. Bottom placements stack newest-nearest-the-edge automatically (no list reordering needed — the container just anchors from the opposite side, and column layout does the rest).
- `Tabs` gains `TabBarExtraContent` (`RenderFragment?`, a right-aligned strip slot — wrapped in a new `wss-tabs-nav-wrapper` only when set), `Centered` (`bool`, centers the tab buttons via `wss-tabs-nav-centered`), and `Type` (`TabsType`: `Line` default / `Card` — AntD's boxed "card" tabs, CSS-only via `wss-tabs-card`; keyboard/ARIA are identical to `Line`).
- `Skeleton` gains `Avatar` (`bool`) + `AvatarShape` (`SkeletonAvatarShape`: `Circle` default / `Square`) — an avatar placeholder block beside the title/paragraph (wrapped in new `wss-skeleton-header`/`wss-skeleton-content` elements only when `Avatar` is set). A new minimal `SkeletonElement` component (`Kind`: `SkeletonElementKind.Button`/`Input`, plus `Active`) covers AntD's standalone `Skeleton.Button`/`Skeleton.Input` shapes without adding N separate components.

**Changed** (Edit Controls)
- `LabelTooltip` (the form-label help-icon popover) is restyled to AntDesign's dark tooltip look — opaque dark chip, 6px radius, arrow, AntD's layered shadow, fade/slide-in — and now auto-places like `data-tooltip` instead of always opening above: the bubble opens below the trigger by default and aims toward the center of the nearest clipping ancestor / panel (flipping above, aligning left/right near an edge) via `wss-tooltip.js`, which `LabelTooltip` lazily imports itself — consumers add nothing, and without JS the CSS default (below, centered) still renders. Hover shows after the same 0.35s hover-intent delay as `data-tooltip`; keyboard focus stays instant. Theming: `--color-tooltip-bg` / `--color-tooltip-text` / `--edit-tooltip-z-index` still honored (the arrow follows the bubble background automatically); new `--edit-tooltip-gap` (default `24px`, below) and `--edit-tooltip-gap-tight` (`3px`, above) knobs; the bubble no longer draws a `--border-color` border. Anything that relied on the old always-above placement will see the new placement.
- `LabelTooltip`'s reveal is now pure CSS `:hover`/`:focus` instead of a C# round-trip per hover; `aria-hidden` on the bubble now carries only the Escape-dismissed state (starts `"false"`, flips `"true"` on Escape until pointer/focus leaves). Rationale: re-rendering mid-hover mutated the DOM under the pointer, and the browser's rebuilt hover chain fired spurious `mouseleave`s that dismissed the bubble while the pointer traveled onto it. Accessibility of the new look/placement, verified end-to-end: the bubble is **hoverable** (WCAG 1.4.13 — pointer-interactive with an invisible gap bridge that exists only while open, so the pointer can travel from icon to bubble and rest on it; text is selectable), Escape-dismiss kept (1.4.13), `prefers-reduced-motion` drops the fade/slide (2.3.3, mirroring the UI kit), and a transparent border keeps a visible bubble boundary under forced-colors / Windows High Contrast. Hover reveal now also works pre-hydration (it previously waited for interactivity).

**Fixes** (Edit Controls)
- `EditRadioEnum<TEnum>`'s `HasOtherOption` free-text input now honors `IsOptionDisabled`: previously the input's `disabled` expression checked only `IsDisabled`, so a predicate disabling the Other enum value locked the radio button but left its paired text input editable. Both render modes (`Default` and `OptionType="Button"`) were affected. (`EditRadioString`'s `HasOther` input is unaffected — by design, `IsOptionDisabled` never applies to the built-in Other option there, since it has no corresponding `Options` entry.)
- `EditFile`: a second `<InputFile>` change event firing while `LoadFiles` was still suspended inside `BeforeAdd` (or while buffering a file's bytes) used to run concurrently against the same bound list — bypassing `MaxFiles`/`MaxTotalBytes` (both checked against a `Value` snapshot taken at the top of the method) and risking an `ArgumentException` from two overlapping `EditContext.NotifyFieldChanged` calls for the same field. `LoadFiles` now guards itself with a synchronous re-entrancy flag: a re-entrant call while a batch is in flight returns immediately without touching `Value`/`EditContext` (reject, not queue), and the `<InputFile>` is disabled for the duration of the batch so a real user can't trigger it through the UI — only a synthetic double-fire could.
- `EditFile`'s `AllowedExtensions`: a bare `"*"` or full `"*/*"` accept token now means "accept everything" (both previously normalized into a shape that matched no file, silently rejecting every upload) — `"*"` renders as `"*/*"` in the `accept` attribute; both OR normally with any other tokens in the same list. Leading/trailing whitespace on any token (extension or MIME shape) is now trimmed instead of causing every file to be rejected.

**Fixes** (Select engine — `Select` / `EditSelectSearch` / `EditMultiSelect`)
- Disabled ⇒ dropdown closed. An externally forced `Open="true"` (the controlled `@bind-Open` case) previously bypassed `Disabled` entirely — `OnParametersSetAsync` routed it straight into `OpenAsync` with no `Disabled` check, unlike `OnWrapperClickAsync`'s existing gate — so a parent could force open a disabled select and a click on a rendered option would still fire `ValueChanged` (`.wss-select-disabled` has no `pointer-events: none`, so this was real-browser exploitable, not just a controlled-mode curiosity). `OnParametersSetAsync` now ignores an external `Open="true"` while `Disabled` (an external `Open="false"` is still honored) and closes an already-open dropdown through the normal `CloseAsync` path the moment `Disabled` becomes true, controlled or not — `OpenChanged` still fires and JS/focus cleanup still runs. Hardened in depth: `SelectAsync`, `ClearAsync`, `CommitTagAsync`, and the keyboard/search-input handlers now also no-op while `Disabled`, matching `OnWrapperClickAsync`'s whole-method gate, so a mutation can't reach the bound value through those paths either even if the dropdown were somehow still rendered open.
- `FilterOption` is now tracked by reference in the same change-guarded block as `Options`/`Values`: swapping the delegate (with `Options` unchanged) refreshes an already-open dropdown's filtered list immediately, instead of leaving it stale until the next keystroke or reopen.

**Fixes** (UI Kit — `Table`)
- `SelectionMode.Single` now actually enforces "at most one selected" from every entry point, not just a user picking a row. Previously only `SelectSingleAsync` (the radio's own `@onchange`) cleared any prior selection first — a runtime `Multiple` → `Single` mode switch with several rows already checked, or a controlled `SelectedItems` handing in 2+ items while already in `Single` mode, both left multiple checked radios in one native `name` group. `OnParametersSet` now clamps `_selected` down to its first (insertion-order) item whenever `SelectionMode` is `Single` and more than one item is present, and raises `SelectedItemsChanged` with the pruned list when the clamp actually dropped something, so a bound `SelectedItems` reflects reality.
- A runtime `IsRowSelectable` or `SelectionMode` change with nothing else different (same page, same sort, same data) used to leave the header "select all" checkbox's disabled/indeterminate state stale: `RebuildPageItems`'s memo guard compared only the sorted view, page, and page size, so it skipped `RecomputeSelectionFlags` entirely. The guard now also tracks the `IsRowSelectable` delegate reference and `SelectionMode`, so either one changing forces a fresh recompute (and the JS indeterminate re-sync that reads it).
- `Loading`'s translucent mask now covers the whole component — both pagers plus the body, matching AntD's `Spin`-wrapped-table look — instead of just the table body. The mask previously lived inside `.wss-table-wrapper` (`position: relative` there, `inset: 0` against it), so the top/bottom pager blocks — wrapper siblings — stayed visually uncovered and clickable while loading. The positioning context moved to the root `wss-table-root` element and the mask now renders there directly, so it visually and structurally sits above the pagers too; `aria-busy="true"` moved from the wrapper to the root to match (only changes the DOM when `Loading="true"`).
- A `ScrollY` sticky header cell always establishes its own CSS stacking context (`position: sticky` does this regardless of z-index), which trapped an open filter dropdown's z-index below `Loading`'s mask — the mask visually and functionally covered the dropdown's OK/Reset buttons whenever both were active on the same table, no matter how high the dropdown's own z-index was raised (a nested-stacking-context limit, not fixable from the dropdown's side alone). The column whose filter is currently open now gets its own header cell promoted above the mask via a plain CSS class (`Column.FilterOpen`-driven, no JS), scoped to that one column so every other sticky header cell is unaffected.
- A `ScrollY` filter dropdown escaping the wrapper's overflow clip (`position: fixed`, computed once at open) detached from its trigger the moment the page scrolled or the viewport resized — there were no listeners keeping it in sync. `wss-overlay.js` now wires window-level, capture-phase scroll/resize listeners for as long as the dropdown stays open (`activateFixedDropdown`, returning a dispose handle mirroring `activateModal`'s), so it tracks the trigger continuously and cleans up deterministically on close and on component dispose — no leaked listeners.
- Clicking a filter's OK with nothing (re-)ticked, or Reset on a column with no applied filter, no longer resets the current page — only an applied selection that actually changes does. `Table.OnFilterChanged` now also skips firing on that same no-op, and gains a third trigger: a column that was actively filtering rows raises it (with an empty payload) when it drops out of the rendered set (e.g. an `@if` hiding it) — previously its filter state was silently cleared with no notification, leaving a consumer's own filter-summary display stale.
- A sortable + filterable column's header could push its filter button out of (or past) a narrow `table-layout: fixed` column — the sort trigger, a flex item with no `min-width`, refused to shrink below its label's full nowrap width. The label now truncates with an ellipsis instead.

**Fixes** (UI Kit — `Popconfirm` / `Popover` / `Alert`)
- `Popconfirm`: re-enabling after a `Disabled`-forced close (with the consumer still holding `Visible="true"` the whole time) now reopens the popup. `OnParametersSetAsync` previously recorded the suppressed request's value before returning, so once `Disabled` cleared, the still-true `Visible` compared equal against that stale recording and never took effect.
- `Popconfirm`: a genuinely pending `OnConfirm` now locks the popup closed against Escape, a backdrop click, the Cancel button, and an external controlled `Visible="false"` — all of these used to close the popup and (for Escape/backdrop/Cancel) fire `OnCancel` while `OnConfirm` kept running unobserved. All four now wait/no-op until the pending task settles; the popup then closes itself through the normal path, raising `VisibleChanged` exactly once.
- `Popconfirm`/`Popover`: an `OnAfterRenderAsync` positioning attempt (`place()`) that overlapped a close/reopen could leave stale `_positioned`/`_pendingFocus` state, occasionally skipping the next open's own re-measure/focus. Guarded with a sequence token, mirroring `Modal`'s existing `_activationSeq` fix (see the 2026-07-07 entry below).
- `Popconfirm`: the OK button now reliably gains focus when opened via a controlled `Visible="true"` from a separate trigger elsewhere on the page (e.g. `@bind-Visible`) — a same-tick `FocusAsync()` call in that path could lose a race against Blazor's own render-batch focus-restore and silently never stick. Routed through a new `wss-overlay.js` `focusDeferred` helper that retries via `requestAnimationFrame` until the focus is verified to have landed.
- `Alert`: `Type` no longer goes stale across a re-render that stops passing it. Standard Blazor parameter semantics leave an omitted parameter's backing value at whatever a prior render set it to; a non-`Banner` alert that stopped passing `Type` kept rendering the last severity it was ever given instead of the documented `Info` default.

### 10.6.6

**Fixes / polish** (MFE-compatibility follow-up)
- `.edit-sr-only` now uses the clip-based visually-hidden pattern (`clip-path: inset(50%)` + 1px box + `-1px` margin) instead of `left: -10000px` — the offscreen-position pattern could be un-hidden by a consumer MFE shell's CSS resetting `position`/`left`. Matches `.wss-sr-only`'s existing approach; no visible change for anyone not already relying on the bug.
- `EditString`'s masked read-only wrapper (the `<span>` + eye-toggle `<button>` shown when `MaskText` is set and `IsEditMode=false`) now carries a `edit-masked-value` class, styled `display: inline-flex; align-items: center; gap: 4px`. Consumers previously had to target this wrapper with a `:has()` hack; style `.edit-masked-value` directly instead.
- `.edit-tooltip-content`'s `z-index` is now the overridable `var(--edit-tooltip-z-index, 10000)` (was a hardcoded `100`) — tall consumer stacking contexts (drawers, modals) no longer bury the tooltip popover beneath them. Override `--edit-tooltip-z-index` at any scope if 10000 still isn't high enough.

**Demo** (`WssBlazorControls.Demo`)
- New `DemoEditDatePicker`/`DemoEditDateRange` pages (sidebar views `DatePicker`/`DateRange`): basic binding, Label/read-only/`Min`-`Max` variants, fixed-date presets, and `[Required]` validation sections. Both controls also joined the All Controls kitchen-sink view.

**Picker fixes** (post-10.6.5 audit of the new calendar pickers)
- `EditDatePicker`/`EditDateRange` accessible names now honor the `Label` parameter: the input `aria-label` resolves `InputLabel` → `Label` → the field's auto-derived label (previously `Label` was skipped, so a control with `Label` set spoke a different name than it displayed — WCAG 2.5.3). `EditDateRange` composes unique per-input names — `StartInputLabel`/`EndInputLabel` when set, else `Label` + " start"/" end", else each field's own auto-name; `EndInputLabel`'s default is no longer the literal "End date" (it now derives from the End field like Start always did) and the parameter is now nullable.
- The day grid's roving Tab stop skips disabled days: with `Min` in the future (or the bound value outside `Min`/`Max`), the default focus day used to be a `disabled` button, making the whole grid unreachable by keyboard. The stop now falls back to the first enabled day in view.
- Prev/next month buttons actually disable at the `Min`/`Max` view bounds, as documented — previously they only stopped at the representable-date edges and would page into fully-disabled months.
- Panel-originated closes (picking a day, Enter, Escape, preset click) return focus to the picker's text input instead of stranding keyboard focus on `<body>` when the dropdown unmounts; outside-click closes leave focus where the user clicked.
- `DateRangePicker`: arrowing forward past the end of the right panel's month now shifts the view one month (focused month becomes the right panel) instead of leapfrogging two; keyboard focus after a forward month-boundary move lands on the in-month day cell, not the left grid's dimmed adjacent-month duplicate (`wss-picker.js` now prefers the roving-tabindex match).
- Both pickers are now explicitly Gregorian-calendar controls under every culture: cultures whose default calendar isn't Gregorian (th-TH Buddhist, ar-SA Hijri) previously got self-contradictory chrome (Hijri month names over a Gregorian grid; a Buddhist-year input beside a Gregorian year select). All picker-internal formatting and typed-input parsing — including `EditDatePicker`/`EditDateRange`'s read-only display, which must agree with edit mode — now use the culture's language with the calendar forced to Gregorian. Behavior under Gregorian-default cultures (en-US etc.) is unchanged.

**Picker parity fixes** (second post-10.6.5 audit)
- Invalid pickers now show the error-red border every other control gets: new `.wss-picker.invalid` rules mirror `.wss-select.invalid` (red border at rest, re-asserted while open/focused; the single-date variant's focus ring also flips to the error shadow). Previously `EditDatePicker`/`EditDateRange` forwarded the `invalid` state class onto the wrapper but no stylesheet rule consumed it, so an invalid picker was pixel-identical to a valid one.
- `EditDateRange` in read-only mode now forwards the consumer's `class` plus the Start field's EditContext state classes (`modified`/`invalid`/custom `FieldCssClassProvider` output) to the read-only value, matching edit mode and every other control's read-only view — previously both were silently dropped.
- Both pickers now honor the documented `HidingMode.*NullOrDefault` contract for `default(DateTime)` (0001-01-01): `EditDatePicker` overrides `IsValueDefault` like `EditDate`, and `EditDateRange` treats a null-or-default pair as empty. Previously a `default(DateTime)` value kept the control visible where `EditDate` would hide it.

**Fixes** (multi-angle audit of the 10.6.3 UI-kit surface + range picker)
- `Tabs` — the strip rendered one render behind for parameter changes on an existing `Tab`: Blazor builds the strip markup before the child `Tab`s' parameters update, and only a *new* tab triggered the corrective re-render, so a `Count` chip updated after a data load (or a runtime `Title` change, or a `Disabled` flip) kept showing the old value until some later unrelated render — a just-disabled tab also kept its enabled-looking, non-`disabled` button. A `Tab` now detects display-relevant parameter changes and requests the follow-up render (change-guarded, so fragment-bearing tabs don't loop).
- `Tabs` — Home/End are no longer handled by the key switch: Blazor has no per-key `preventDefault`, so the browser also scrolled the document to top/bottom before focus yanked it back. Arrows (with wrapping) remain the ARIA tabs navigation; matches the library's established no-JS keyboard policy.
- `SearchInput` — the input had no accessible name when the addon was supplied as an `AddonContent` template (the `aria-label` fallback only considered `AddonLabel`); with `AddonContent` + `Id` and no labels the input's `aria-labelledby` now points at the addon chip, whose `{Id}-addon` id previously dangled unreferenced.
- Pill `Select` variant (`Variant="Pill"`) — the pill's hover/focus/open rules out-ranked the validation `invalid` rules (same-specificity, later in file), so a focused, hovered, or open invalid pill select showed pill-colored chrome instead of the error red; dedicated `.wss-select-pill.invalid` overrides now keep the error border and ring. The pill focus ring also no longer consults `--wss-primary-shadow` — it derives purely from the pill color, as documented (computed default unchanged).
- `EditDateRange` — the shared wrapper's state classes derived from the Start field only, so an End-only validation error (required End left empty, an "End ≥ Start" rule) rendered a normal border while the error message and the End input's `aria-invalid` were live; the wrapper now folds End invalidity into its class (edit and read-only modes both), completing the 10.6.x "invalid pickers get the error border" fix for the range control's most common failure case.

### 10.6.5

**New features**
- `EditDatePicker` / `EditDateRange` — form-bound versions of the UI-kit calendar pickers. `EditDatePicker` binds a `DateTime?` via `@bind-Value` (an `InputBase`-derived scalar control, same contract as every other `Edit*`); `EditDateRange` binds two model properties via `@bind-Start` / `@bind-End`, registers both fields with the form, and validates each independently with its own message. Both render the standard label/required-star/validation scaffolding around the calendar dropdown, support read-only mode via `DateFormat`, and forward the pickers' parameter surface (`Min`/`Max`, `Format`, `Presets`, placeholders, accessible-name params). To support them, `DatePicker`/`DateRangePicker` gained validation-state ARIA passthrough onto their actual inputs (`AriaRequired`/`AriaInvalid`/`AriaDescribedBy`/`AriaErrorMessage`, doubled as `StartAria*`/`EndAria*` on the range picker — the same forwarding shape as `Select`'s trio) and the range picker gained `EndId` (an id for its end input). Use `EditDate` when the browser-native date input is fine; use these when the form wants the AntD-style calendar UX.
- `DatePicker` / `DateRangePicker` calendar round-out: a weekday header row above each day grid (culture-abbreviated names, ordered by `FirstDayOfWeek`); prev/next month buttons flanking the month/year selects (`PrevMonthLabel` / `NextMonthLabel` localize their accessible names; they disable at the `Min`/`Max` view bounds, and `DateRangePicker` places prev on the left panel and next on the right); roving-tabindex keyboard navigation over the day grid — Arrow keys move by day/week, Home/End jump to the focused week's ends, PageUp/PageDown step a month, and the view follows focus across month edges (page-scroll suppression comes from a new lazily-imported `wss-picker.js`, gracefully absent without JS); `DateRangePicker` tints the prospective span on hover while the second day is being picked (override via `--wss-picker-preview-bg`). A stale `Field="..."` attribute on either picker now fails the build (the same inert `[Obsolete]` guard as the form controls).

**Bug fixes**
- `Table` — a sortable column with `TitleContent` rendered the template inside the sort `<button>`: with interactive template content (the README-advertised `LabelTooltip` composition) that nested a button inside a button (invalid HTML) and clicking the info icon toggled the sort, and an icon-only template left the sort button with no accessible name. The template now renders in its own clickable area beside a caret-only sort button (header clicks still sort; the button is named from `Title`, falling back to "Sort"), and `LabelTooltip`'s trigger stops click propagation so it never triggers a clickable ancestor. Also, changing `UseStyledCheckbox` at runtime no longer loses the header checkbox's indeterminate ("mixed") state across the styled/unstyled DOM swap.
- `DateRangePicker` — a typed commit made mid-pick (click one day, then type a date and press Enter) left the field displaying pending-pick state that contradicted the bound values, and a later day click resurrected the discarded pick; typed commits now finalize the field. Presets were clamped only on one side of `Min`/`Max`, so a preset lying entirely past `Max` (or before `Min`) could commit days the calendar itself disables — both endpoints now clamp into the window. Year selects (both pickers) could offer years beyond `DateTime`'s 1–9999 range and threw an unhandled exception when picked; the offered range and the selection handler now clamp.
- `edit-controls.js`'s `focusFirstInvalidField` DOM query substring-matched `[class*=" invalid"]`, which over-matched an unrelated consumer class like `class="foo invalid-hint"` — it now matches the exact `.invalid` class token only (the same false-positive shape `InvalidIcon.razor` and `EditControlBase.IsInvalid` already fixed for `CssClass`).
- `JsInteropEc` — `edit-controls.js` was the one JS asset `FormDefaults.AssetBase` didn't yet cover: in a cross-origin MFE whose host page doesn't serve/link `_content/WssBlazorControls/edit-controls.js`, `window.WssEditControls` is undefined, and `FocusFirstInvalidField` (unlike `FocusById`) threw instead of degrading gracefully. All three methods (`FocusFirstInvalidField`, `FocusById`, `Log`) are now best-effort and never throw; when the global is missing they lazily `import()` the module (honoring an optional trailing `formDefaults` parameter, resolved through the same `JsModuleUrl` mechanism as the `wss-*.js` imports) and retry once, degrading quietly if that also fails.
- `wss-overlay.js`'s Modal/Drawer body-scroll lock and focus-trap stack were module-scoped, which was fine until `FormDefaults.AssetBase` (10.6.4) made it routine for two MFEs to import this module from different origin URLs — the browser instantiates a module once per distinct URL, so two "instances" could each believe they alone owned the document. An interleaved open/close across instances could leave the page permanently scroll-locked (or unlock it while a dialog from the other instance was still open), and both instances' document-level Tab/Escape/focus listeners could fight over focus. The scroll-lock counter and the trap stack are now shared via `window.__wssOverlayScrollLock` / `window.__wssOverlayTraps` (same pattern as the existing `window.__wssOverlayZ` z-index counter) — ref-counting and topmost-trap ownership now work correctly across instances. No API change; nothing for consumers to configure.

### 10.6.4

**New feature**
- `FormDefaults.AssetBase` — an absolute URL prefixed onto the RCL's lazy `wss-*.js` module imports (`Select`, `Modal`, `Drawer`, `Popover`, `Popconfirm`, `DatePicker`, `DateRangePicker`, `Table`). Fixes a 404 for micro-frontends embedded into a host page that doesn't serve/proxy `_content/WssBlazorControls/*` — the `"./"`-relative import specifier otherwise resolves against the *host document's* origin instead of the MFE's own. Unset (the default) preserves today's relative import path. Cascade it from the MFE's own root the same render-tree-scoped way as `FormDefaults`'s other settings — not a shared JS global — so multiple MFEs composed into one page don't stomp on each other's asset base. See [FormDefaults](#formdefaults).

### 10.6.3

**New features**
- `Table` expandable rows + templated headers (per the Clark Connect Vendor PO Management Figma spec): `RowDetail` (a `RenderFragment<TItem>`) adds a leading chevron column that toggles the template as a full-width row beneath its row — the nested-child-table master/detail pattern; expansion state is keyed by `RowKey` identity (survives paging/sorting, forgotten when a row leaves the data). `Column.TitleContent` renders templated header content in place of the plain `Title` (works in sortable headers too), enabling headers like "ESD ⓘ" composed with `LabelTooltip` — whose `Attributes` parameter is now optional, so it works standalone outside the Edit* form controls.
- `Tabs` / `Tab` — underline tab strip with an optional bordered per-tab count chip (`Count`, the "12 Overdue" pattern). Controlled via `@bind-ActiveKey` (`string?`); a `Tab` with `ChildContent` shows the active pane below the strip (proper `tablist`/`tab`/`tabpanel` wiring), while content-less tabs act as a bare filter strip. ARIA tabs keyboard pattern with automatic activation: Arrow keys select the neighboring enabled tab (skipping disabled, wrapping) and move focus with a roving tabindex; Home/End jump to the ends. Conditionally rendered tabs keep their declared position (the Table-column collect/promote mechanism). Active chip border derives from the primary color (`--wss-tabs-count-active-border` override knob).
- `SearchInput` — the labeled search field from the same spec: optional leading addon chip (`AddonLabel` / `AddonContent`), a per-keystroke `@bind-Value` input, and an icon-only search button; `OnSearch` fires with the current text on Enter and on the button. Pill-rounded ends by default via `--wss-search-radius` (override to square). Not a form control — for validated form text use `EditString`.
- `DatePicker` — the single-date sibling of `DateRangePicker` (per the Clark Connect Vendor PO Management Figma spec): a text field with a calendar suffix opening a one-month calendar whose header is month/year quick-select dropdowns. Bind with `@bind-Value` (`DateTime?`, date-only); picking a day (or typing a date and pressing Enter) commits and closes; Escape and outside clicks close; `Min`/`Max` disable out-of-range days; `Format` drives display/parsing (default `MM/dd/yyyy`); `Placeholder` defaults to "Select date". Shares the `wss-picker-*` calendar internals and `wss-overlay.js` lifecycle (viewport flip/clamp, Enter-submit suppression, focus-out close — all degrade gracefully without JS). Its card carries a hairline border + the new `--wss-picker-radius-lg` (8px) radius, and the focused field shows the spec's primary focus ring. See [UI Kit](#ui-kit-non-form-controls).
- `Select` pill variant + `Prefix` slot — `Variant="SelectVariant.Pill"` restyles the trigger as a fully-rounded outlined filter button that hugs its content ("All shipments ⌄"), and the new `Prefix` `RenderFragment` renders leading content (typically a decorative icon) inside the trigger in any mode/variant. The pill dropdown gains softer corners, content-driven width, roomier rows, and conveys selection by the bold/tinted row alone (checkmark suppressed); the trigger label/border/chevron/focus ring all derive from one override knob, `--wss-select-pill-color` (plus `--wss-select-pill-border` / `--wss-select-pill-bg`). `EditSelectSearch` forwards `Variant` + `Prefix`; `EditMultiSelect` forwards `Prefix`. Internal DOM note: the selector's value/search stack is now wrapped in a `wss-select-selection-wrap` span (so a prefix can sit beside it) — geometry and behavior are unchanged, but CSS/tests targeting direct-child structure inside `.wss-select-selector` may need the extra level. See [Pill filter variant](#pill-filter-variant-select--editselectsearch).

### 10.6.2

**New feature**
- `DateRangePicker` — an AntDesign-style date-range picker: a composite start → end field that opens a dropdown with an optional preset sidebar and a dual-month calendar whose headers are native month/year quick-select dropdowns. Bind with `@bind-Start` / `@bind-End` (`DateTime?`, date-only); picking the second day of a range (or a preset) commits and closes, a backwards pair swaps, and typed input parses by `Format` then culture, committing on Enter/blur. `Presets` resolve their range at click time so relative shortcuts (e.g. "This Week") never go stale in a long-lived page. `Min`/`Max` disable out-of-range days and clamp presets; `FirstDayOfWeek` defaults to the current culture. Not a form control — no `InputBase`/validation wiring. JS interop (viewport flip/clamp placement, Enter-submit suppression, focus-out close) degrades gracefully: without JS the dropdown opens below the field at the CSS default placement and stays fully clickable. New `--wss-picker-*` tokens carry its radii and split-border color. See [UI Kit](#ui-kit-non-form-controls).
- `UseStyledCheckbox` app/MFE-wide switch (shipped in this release but missed in the original changelog) — `FormOptions.UseStyledCheckbox` (`bool?`) and the render-tree-scoped `FormDefaults.UseStyledCheckbox` (`bool?`) resolve the same way as `IsRequiredStarHidden` / `ShowFieldNameInValidation`: instance → nearest enclosing `FormDefaults` → the process-wide `FormOptions.DefaultUseStyledCheckbox` static (default `false`). `EditBool.UseStyledCheckbox` (shipped 10.6.0) changed from `bool` to `bool?` so it participates in this chain instead of being per-control only — existing `UseStyledCheckbox="true"`/`"false"` markup is unaffected, only an unset control now inherits the app-wide default instead of always rendering the native checkbox. Two more controls gained the same opt-in: `EditCheckedStringList.UseStyledCheckbox` / `EditCheckedEnumList.UseStyledCheckbox` (`bool?`) apply the custom-drawn box to every option's checkbox, and the UI-kit `Table.UseStyledCheckbox` (`bool?`) applies it to the header/row selection checkboxes, including the indeterminate "mixed" glyph — `Table` has no `FormOptions` of its own, so it resolves through a cascaded `FormDefaults` then the static only. See [`FormDefaults`](#formdefaults) and [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).
- Styled checkbox visual restyle (also shipped in this release): the checked glyph is now the exact antd check vector via a themeable CSS mask (was a generic rotated-border "L"), the unchecked border fallback moved from `#ccc` to `#d9d9d9` (antd `colorBorder`), the `Table` variant's box corner radius moved from 2px to 4px to match `EditBool`'s, and the indeterminate "mixed" state is now an unfilled box with a centered primary-colored square (was a filled box with a white dash) — also fixing a CSS comment bug (`/* ... edit-*/ ...`) that had been closing the `Table` box-wrapper rule early and letting the box escape its cell. The label row for `EditBool` and each `EditChecked*` option is now a flex row (`align-items: center`, 8px gap) instead of relying on inline whitespace. These restyles apply automatically to every consumer already using `UseStyledCheckbox="true"` since 10.6.0 — there is no separate opt-in for the new look.

### 10.6.0

**New feature**
- `EditBool.UseStyledCheckbox` (default `false`) — opt-in custom-drawn checkbox. No current browser (Chromium or Safari/WebKit) honors `border-radius` on a native `<input type="checkbox">` once `accent-color` is set, so there was previously no way to get a shaped checkbox out of `EditBool`. When enabled, the real `<input>` stays in the DOM (focusable, keyboard-operable, full native semantics) but is visually hidden; a sibling element draws the box, checked fill, checkmark, and focus ring via the plain adjacent-sibling (`+`) CSS selector (not `:has()`, so it still works on older Safari). Existing checkboxes are pixel-identical — nothing changes unless you opt in. See [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).

**Bug fixes**
- `width: 100%` (or any percentage width) on the editor element of `EditString` / `EditNumber` / `EditDate` / `EditTextArea` now works. Previously the `.edit-input-with-icon` wrapper shrink-wrapped to the editor's intrinsic size, which made a percentage width on the editor circular per the CSS sizing spec — it silently resolved to `auto` and the input stayed at its default size. The wrapper is now a flex row that stretches to the control column (so percentages resolve against it), and the red-X invalid icon overlays the editor's trailing edge via a negative flex-item margin instead of absolute positioning — still `dir="rtl"`-correct and still immune to being wrapped onto its own line under a width squeeze.
- `EditFile`: bare `AllowedExtensions` entries without a leading dot (`"pdf"`) are now normalized instead of silently rejecting every file (and emitting an invalid `accept` attribute); the label's `for` no longer dangles at a missing input once the `MaxFiles` cap unmounts the drop zone; the upload icon now turns red for `EditContext` validation failures (not just client-side rejections); the read-only file list is programmatically associated with the field label. Re-selecting a file that's already added (same name, size, and last-modified) is now skipped and reported — via the new `DuplicateFileMessageFormat` parameter — instead of occupying a second `MaxFiles`/`MaxTotalBytes` slot for the same logical file.
- List-bound controls (`EditMultiSelect`, `EditFile`, `EditCheckedStringList`, `EditCheckedEnumList`): a `class` attribute is now captured and merged into the rendered field instead of throwing at render time as an unmatched parameter — onto the select engine (`EditMultiSelect`, matching `EditSelectSearch`), the drop zone and read-only file list (`EditFile`), and every checkbox (`EditChecked*`). These controls also now emit the same `EditContext` field-state classes as the scalar controls (`modified`/`valid`/`invalid` by default, honoring a custom `FieldCssClassProvider`) instead of only `invalid`. `EditRadio` now applies the consumer's `class` to its group fieldset in edit mode (previously it appeared only in the read-only view).
- `EditSelectSearch` / `EditMultiSelect` / `Select`: a disabled multi-select no longer renders focusable tag-remove buttons that silently no-op; Space now opens a closed non-searchable select (ARIA combobox pattern) — searchable inputs keep Space for typing.
- `EditDisplay`: the cascaded `FormOptions` was declared but ignored — form-wide `IsLabelHidden` now applies, and the new `IsLabelHidden` / `IdPrefix` parameters plus `FormGroupOptions.Name` id composition bring it in line with the bound controls (two `EditDisplay`s with the same label in different form groups no longer collide on id).
- Styled checkbox (`UseStyledCheckbox`): the box background is now `var(--color-bg, #fff)` instead of hardcoded white, so dark-theme consumers have an override hook. Default rendering unchanged.
- With `--color-primary` unset, the checked styled-checkbox fill and the `EditFile` drop-zone hover border fell back to a stray teal (`#277c6c`) while the focus rings fell back to blue (`#0066cc`) — two different colors for one interactive role. All three now share a single `--edit-color-primary` token (blue fallback). Note the token is resolved at `:root`, like every other bridging token in both stylesheets — set `--color-primary` at `:root` for it to be picked up (a value scoped to a nested container is not seen, which previously happened to work for these two rules only).
- `Table`: the header checkbox's mixed (indeterminate) state is re-applied after `Selectable` is toggled off and back on while a partial selection exists — the recreated checkbox used to come back plain-unchecked.
- `Modal` / `Drawer`: Escape-to-close no longer goes dead when focus is silently dropped to `<body>` — e.g. the focused default OK button becoming disabled via `ConfirmLoading`, or a conditionally-rendered focused element unmounting. The focus trap now pulls focus back into the panel and re-targets the Escape at it.
- `JsInteropEc.FocusById` now honors its documented best-effort contract (a no-op when JS is unavailable) instead of throwing from a prerender `IJSRuntime`.
- **Theming: scoped token overrides now cascade into derived states.** `--wss-color-primary-hover`, `--wss-primary-shadow`, `--wss-error-shadow`, and `--edit-focus-ring` used to be derived from their base token at `:root`, so overriding `--wss-color-primary` / `--edit-color-primary` / `--wss-color-error` on a nested container (a theme class, an MFE root) changed the base color but left hover borders, focus shadows, and focus rings at the default blue/red. These are now derived at each usage site — a scoped base-token override re-themes the derived states too. All four remain overridable as before (a directly-set value wins over the derivation), the generic `--color-primary-hover` bridge is preserved (and now also works scoped, since it too is consulted at the element); computed defaults are unchanged. `--wss-color-primary-active` (never consumed by any rule) was removed.
- **UI-kit components accept `class` / `style` / arbitrary attributes.** `Alert`, `Skeleton`, `Pagination`, `Modal`, `Drawer`, `Popover`, `Popconfirm`, `Table`, and `EditDisplay` previously threw `InvalidOperationException` on any unmatched attribute. They now capture unmatched attributes onto their root element (`Modal`/`Drawer`: the dialog panel; `Popover`/`Popconfirm`: the trigger wrapper): `class` and `style` merge with the component's own, everything else (`data-*`, `id`, ...) is splatted verbatim. Caveat: parameter matching is case-insensitive, so an attribute sharing a parameter's name binds to the parameter instead — e.g. `title="..."` on `Modal`/`Drawer`/`Popover`/`Popconfirm` sets their `Title`, on `Skeleton` it's a build error (`Skeleton.Title` is a `bool`), and `class` on `EditDisplay` sets its `Class` (same knob).

### 10.5.1

**Bug fixes**
- `EditControlListBase<TItem>.ValueExpression` is now `[EditorRequired]` — a missing/incomplete `@bind-Value` (e.g. one-way `Value="..."` with no binding) is now a build-time `RZ2012` diagnostic instead of only the runtime `InvalidOperationException` each list-bound control's `OnInitialized` already threw.
- Fixed `.edit-icon-invalid` (the validation-error icon overlaid on `EditString`/`EditNumber`/`EditDate`/`EditTextArea`) wrapping onto its own line under a width squeeze. It's now absolutely positioned (`inset-inline-end`, so it still overlays the correct edge under `dir="rtl"`) instead of relying on a negative margin to pull it over the input.

**Demo**
- Added a "Comparison" view to the demo app that renders the same field via WssBlazorControls, hand-rolled Blazor, and React + Ant Design (with and without full accessibility parity) side by side, with reasoned notes on the accessibility and AI-authoring trade-offs of each.

### 10.5.0

**`Field` is gone — `@bind-Value` alone is now enough on every control**

- Every `Edit*` control previously required both `@bind-Value="model.Property"` **and** `Field="@(() => model.Property)"` — the second was pure duplication. Razor's `@bind-Value` directive already populates a `ValueExpression` (the same mechanism Microsoft's own `InputText`/`InputNumber` rely on for validation and labeling without a second parameter); the library just wasn't using it. All 17 controls now resolve their accessor from `ValueExpression` instead.
- This covers the scalar controls (`EditString`, `EditNumber`, `EditDate`, `EditBool`, `EditBoolNullRadio`, `EditSelectEnum`, `EditSelectString`, `EditSelect`, `EditSelectSearch`, `EditRadio`, `EditRadioEnum`, `EditRadioString`, `EditTextArea`) and the list-bound controls (`EditCheckedStringList`, `EditCheckedEnumList`, `EditFile`, `EditMultiSelect`). The list-bound controls aren't `InputBase`-derived, so `EditControlListBase<TItem>` gained its own `ValueExpression` parameter — the compiler synthesizes it from `@bind-Value` for any component with the `Value`/`ValueChanged`/`ValueExpression` parameter shape, not just `InputBase` subclasses.
- **Migration:** delete every `Field="@(() => model.Property)"` attribute — `@bind-Value="model.Property"` alone is sufficient. `Field` still exists on every control as an inert, `[Obsolete(error: true)]`-decorated parameter purely so a leftover `Field=` attribute is a **build error** (`CS0619: 'EditXxx.Field' is obsolete: ...`) instead of a silent runtime failure — Blazor otherwise validates unmatched component parameters at `SetParametersAsync` time, not compile time, so a stale attribute would build cleanly and only throw the first time that component renders. The error message tells you exactly what to remove; this stub carries no other behavior and is planned for physical removal in a future major version.

**Drops net8.0/net9.0 — the package now targets net10.0 only**

- `WssBlazorControls` and `WssBlazorControls.Demo` are single-targeted at `net10.0`; both previously multi-targeted `net8.0;net9.0;net10.0`. **If your app targets net8.0 or net9.0, this version will not install** — stay on `10.4.x` until you upgrade the app to net10.0.
- CI now installs and runs against a single .NET SDK instead of three; the bUnit suite runs once instead of once per TFM.
- No API or behavioral changes for net10.0 consumers — this is purely a supported-platform reduction.

### 10.4.0

A library-wide hardening release: six adversarial review rounds (documented across this release's commit history) spanning correctness, accessibility, performance (measured), globalization/RTL, plus trimming/AOT support, touch support, and validation-stack (FluentValidation) support.

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

**Round-3 review fixes** *(post-hardening evaluation)*
- `Popover`/`Popconfirm` re-resolve their trigger child on every ARIA sync, so conditionally-swapped trigger content (`@if (busy) { spinner } else { button }`) no longer strands `aria-haspopup`/`aria-expanded` on a detached element or drops close-focus to `<body>`, and a wrapper promoted around plain content is demoted again when a real button appears (no more button-in-button after a swap). Focusable non-button trigger children (`[tabindex]` spans, anchors) gained Enter/Space activation; a `Disabled` Popconfirm marks an interactive child `aria-disabled`. The per-render JS interop call is now skipped unless `(open, disabled)` changed — a Popconfirm-per-row Table no longer pays one SignalR round trip per row per re-render on Blazor Server (a `focusin` listener repairs ARIA for children swapped while idle).
- `EditFile`: new `MaxTotalBytes` parameter (default **100 MB**, `0` = unlimited) bounds the aggregate buffered footprint across all selected files — buffering at pick time otherwise let a single large multi-file drop allocate unbounded server memory under the default `MaxFiles = 0`.
- Date-typed selects round-trip: `EditSelect<DateOnly>`/`<DateTime>`/`<DateTimeOffset>`/`<TimeOnly>` now format to the ISO forms option values are authored in, so picking an option no longer immediately loses the visual selection while the model holds the value. Author your option values in the matching canonical form — `DateOnly`: `2026-06-15` · `DateTime`: `2026-06-15T14:30:45` · `DateTimeOffset`: `2026-06-15T14:30:45-05:00` (UTC is `+00:00`, not `Z`) · `TimeOnly`: `14:30:45`. Shorter authored forms (`2026-06-15` for a `DateTime`, `14:30` for a `TimeOnly`) still *parse* on pick, but the formatted value won't visually re-match them.
- `EditSelectString` with a suppressed blank option (non-nullable value types, or `NullOptionText="@null"`) renders a hidden placeholder when the current value matches no option — an untouched default (e.g. `0`) displays blank instead of silently showing the first option while the model holds something else.
- The open `Select`'s stacking z-index is mirrored into its C#-owned `style`, so a re-render that changes `Width` mid-open no longer clobbers it and drops the selector below its own backdrop (which made clicks on the select's own input close the dropdown).
- Two controls bound to the same property now share their validation-summary registration safely: disposing one (e.g. closing an edit modal that duplicates a page field) keeps the surviving control's messages — registrations are owner-tracked and dropped only by the last registrant.
- Nested `FormDefaults` chain per property instead of the inner instance shadowing the outer entirely (see the `FormDefaults` note above).
- `Select`, `Modal`, `Drawer`, `Popover`, and `Popconfirm` no longer strand a JS module reference when disposed while their module import is in flight (the same race `Table` was already guarded against).

**Round-4 review fixes** *(post-round-3 evaluation)*
- `EditSelect<DateTimeOffset>` now formats whole-second values without the `.0000000` fraction (`2026-06-15T14:30:45-05:00`), so authored option values actually match and the visual selection survives a pick; sub-second values keep the full round-trip form. The canonical authored forms per date type are documented in the round-3 entry above.
- `Popover`/`Popconfirm` trigger ARIA: a consumer-owned `aria-disabled` on the trigger child is no longer removed when the component's `Disabled` round-trips; when the resolved trigger child changes identity while the old element stays in the DOM, the popup ARIA is stripped off the old element instead of two elements announcing the popup.
- `EditCheckedStringList`/`EditCheckedEnumList` fieldsets no longer emit `aria-required`/`aria-invalid`/`aria-errormessage` — ARIA 1.2 doesn't support them on `role="group"` (assistive tech ignored them; checkers flag them). Required state remains on the legend star and the validation message, invalid state on each checkbox's `aria-invalid`. The radio fieldsets (`role="radiogroup"`, where these attributes are valid) are unchanged.

**Round-5 fixes** *(trim verification, globalization/RTL sweep, measured performance pass)*
- **RTL support:** the direction-sensitive Select geometry (arrow/clear anchoring, search inset, tag/placeholder spacing) and the form controls' trailing invalid-icon/required-star spacing now use CSS logical properties — under `dir="rtl"` tags no longer render beneath the opaque clear button (where a tap cleared the entire selection) and typed search text no longer starts under the arrow. Rendering under LTR is byte-identical. Notification position, `DrawerPlacement` left/right, and Table alignment deliberately keep physical semantics.
- **Localization:** new label parameters with unchanged English defaults — `Pagination` `PreviousPageLabel`/`NextPageLabel`/`PageLabelFormat`; `Select`/`EditSelectSearch`/`EditMultiSelect` `RemoveItemLabelFormat`/`ClearSelectionLabel`/`ClearSelectionsLabel`/`ListboxLabel` — so localized apps can localize what screen readers hear. `EditFile`'s five upload-error messages are likewise localizable via `*MessageFormat` parameters (`UnsupportedFormat`, `FileTooLarge`, `FileReadFailed`, `MaxFiles`, `TotalSize`); the pluralizing formats receive a pre-pluralized English unit argument that localized formats can ignore.
- **Culture correctness:** the `[Range]` one-sided message rewrite ("Cannot exceed 100") now works after a runtime culture switch and in mixed-culture Blazor Server processes — the type-min/max sentinels are resolved per current culture instead of being frozen at first touch.
- **Performance:** `Table` no longer rebuilds its row keys and rescans selection state on every parent re-render (the cost was O(rows) with boxing, per keystroke in any sibling input for unpaged tables); `FormLabel`/`FieldValidationDisplay` skip label/attribute re-derivation — and stop re-invoking `FormOptions.RequiredResolver` — unless their inputs actually changed, honoring the resolver's documented "not on every keystroke" contract; `EditMultiSelect`'s read-only label join is O(selected) via a value→label lookup. Measured reality check: for *very* large unpaged tables the remaining cost is Blazor re-rendering the row fragment itself — prefer `PageSize` or the server-side paging composition at that scale.
- Verified this round: the full Playwright suite passes against a `TrimMode=full` publish; Select's dropdown virtualization confirmed (20 DOM rows at 1,000 options).

**Round-6 fixes** *(pre-release regression hunt on the round-4/5 fixes)*
- The required star and `aria-required` now share one computation site: each control resolves its required-ness once (`IsRequired` parameter → `[Required]` → `FormOptions.RequiredResolver`) and passes the resolved value to its label, so a conditional resolver that reads model state moves both signals together on re-render (the round-5 label caching had let `aria-required` update while the star stayed frozen).
- The `[Range]` sentinel check compares against the current culture's actual formatting on every call (a per-culture-name cache could serve stale sentinels to same-name cultures with customized number formats).
- `LabelTooltip` resolves its tooltip text once per input change instead of scanning the attribute list twice per render.

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

