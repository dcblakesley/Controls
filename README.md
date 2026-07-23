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
<!-- Only needed if you use the AntDesign-style UI-kit controls (Select, Alert, Modal, Table, ...) -->
<link href="_content/WssBlazorControls/wss-controls.css" rel="stylesheet" />
```

3. **Include the JS helpers** (next to your Blazor script tag):

```html
<script src="_content/WssBlazorControls/edit-controls.js"></script>
```

   Required by `JsInteropEc.FocusFirstInvalidField` (focus the first invalid field on a failed
   submit). The UI-kit controls load their own JS modules lazily — no extra tags needed for them.
   If the script tag isn't linked (e.g. a cross-origin micro-frontend whose host page doesn't serve
   `_content/WssBlazorControls/`), `JsInteropEc`'s methods lazily import the module themselves and
   never throw — see [FormDefaults.AssetBase](#formdefaults) to point that fallback import at the
   right origin.

   Optional: add `<script src="_content/WssBlazorControls/wss-tooltip.js"></script>` if you use
   `data-tooltip` hover tooltips (see [Hover tooltips](#hover-tooltips-data-tooltip) below) and want
   them to auto-place instead of always opening below the element. `LabelTooltip` (the form-label
   help icon) uses the same auto-placement but imports the module itself — no tag needed for it.

4. **Use the controls** in your Blazor components:

```razor
@using System.ComponentModel
@using System.ComponentModel.DataAnnotations

<EditForm Model="@model" OnValidSubmit="@HandleSubmit">
    <DataAnnotationsValidator />

    @* No Label needed: "Name", "Age", and "Birth Date" are derived correctly
       from the property names, and the required star comes from [Required]. *@
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
- **`EditString`** - Text input with masking and URL support; also supports `Prefix`/`Suffix` affix content, `AllowClear`, `MaxLength`/`ShowCount`, and an `IsPassword` show/hide toggle (independent of the read-only `MaskText` feature) — these switch the input into an AntD-style affix layout via the shared internal `EditInputShell`, while plain markup stays byte-identical to the classic rendering
- **`EditTextArea`** - Multi-line text input; also supports `AllowClear`, `MaxLength`/`ShowCount` (the count renders below the box, right-aligned — AntD `TextArea`'s placement, unlike `EditString`'s inline count), and `AutoSize`/`MinRows`/`MaxRows` (JS-driven grow/shrink to fit content, clamped between the two, degrading to the fixed `Rows` height with no JS) — the affix parameters switch the input into the shared `EditInputShell` layout, while plain markup stays byte-identical to the classic rendering
- **`EditNumber`** - Numeric input with validation; also supports `Min`/`Max` (InvariantCulture, same type discipline as the existing `Step`), `Placeholder`, and `Prefix`/`Suffix` affix content via the shared `EditInputShell` (no `AllowClear`/`ShowCount`/`IsPassword` — no AntD equivalent for a numeric field; native spinners stay, a documented deviation)
- **`EditDate`** - Date picker component
- **`EditDatePicker`** - Form-bound calendar-dropdown date field (the UI-kit `DatePicker` with full `EditForm` validation); full type parity with `EditDate` — binds `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` (and their nullable variants), with a `Type` parameter (`InputDateType`: `Date`/`DateTimeLocal`/`Month`/`Time`, same default as `EditDate`) selecting what the calendar picks, mapped onto the picker's `Mode`. A separate `Mode` parameter (`DatePickerMode?`, default null) overrides that mapping outright to reach `Week`/`Quarter`/`Year` — the one intentional asymmetry with `EditDate`, which has no such escape hatch since its native `<input>` types have no week/quarter/year equivalent to reach. Forwards the picker's full phase-2 surface too: `ShowWeekNumbers`, `DisabledDate`, `DisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`, `ShowToday`/`ShowNow`/`Presets`/`ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params — same defaults as the picker itself
- **`EditDateRange`** - Form-bound date-range field (`@bind-Start`/`@bind-End`, per-field validation, backed by `DateRangePicker`); forwards `DateRangePicker`'s full surface — `Mode` (`DatePickerMode`, default `Date`; dual linked panels at `Date`/`Week`/`Month`/`Quarter`/`Year` granularity, or a single-panel OK-confirm session for `DateTime`/`Time`), `Min`/`Max`, `DisabledDate`, `StartDisabledTime`/`EndDisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`/`OkText`, `ShowWeekNumbers`, `Presets`, `ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params. `Format` (the picker's own display/parse format) and `DateFormat` (the read-only display format) are both nullable with `Mode`-aware defaults — mirroring `EditDatePicker`'s own `DateFormat` contract — instead of a fixed literal, so switching `Mode` alone still gets that mode's own default rather than silently keeping `Date`'s. Read-only display is `Mode`-aware too: `Quarter`/`Week` render the same `yyyy-Qn`/`yyyy-Www` shorthand the picker itself shows
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
<FormDefaults IsRequiredStarHidden="true" ShowFieldNameInValidation="false" UseStyledCheckbox="true">
    <Router AppAssembly="@typeof(App).Assembly">...</Router>
</FormDefaults>
```

Resolution per setting (highest wins): the form's `FormOptions` instance value → the cascaded `FormDefaults` → the static `FormOptions.Default*` property. Prefer `FormDefaults` over the statics: the statics are process-wide, so on Blazor Server they're shared by every user/circuit, and when several MFEs share one runtime they're shared across MFEs. `FormDefaults` scopes to the render tree, which matches app/MFE/circuit boundaries. It's intended as set-once root configuration — the cascade is registered as fixed, so runtime changes to its parameters don't propagate.

`UseStyledCheckbox` follows this same chain and additionally reaches the UI-kit `Table`'s row-selection checkboxes (which have no `FormOptions` of their own) — see [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).

`FormDefaults` also carries `AssetBase` (`string?`), which has no `FormOptions` counterpart: an absolute URL prefixed onto the RCL's lazy `wss-*.js` module imports (see the UI Kit section below), for a micro-frontend whose host page doesn't serve/proxy `_content/WssBlazorControls/*`. Unset (the default) keeps today's relative import path.

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

- **`Select<T>`** - The dropdown engine behind `EditSelectSearch` / `EditMultiSelect`; usable standalone (single / multiple / tags, search, virtualized). `Prefix` renders leading content (typically an icon) in the trigger; `Variant="SelectVariant.Pill"` restyles the trigger as a rounded filter button (see below)
- **`Alert`** - Contextual message banner (success / info / warning / error, closable, description)
- **`Skeleton`** - Loading placeholder with shimmer; announces `role="status"` / `aria-busy` with a visually-hidden `LoadingText` (default `"Loading"`) for screen readers
- **`Popover`** - Click-triggered popover (4 placements)
- **`Pagination`** - Controlled pager
- **`Modal`** - Dialog with `@bind-Visible`, footer, mask-close
- **`Drawer`** - Slide-in panel (4 placements)
- **`Popconfirm`** - Inline confirm popover
- **`DatePicker`** - Single-value field with a calendar suffix opening a dropdown panel. Bind with `@bind-Value` (`DateTime?`); `Mode` (`DatePickerMode`: `Date` default — a one-month calendar with a month/year quick-select header; `Month` — a year header over a 3x4 month-button grid; `Time` — hour/minute/second selects over an OK button; `DateTime` — the day calendar with that same time row and OK button appended below it; `Year` — a decade header (prev/next-decade nav + a static "2020-2029" label) over a 3x4 year-button grid, 10 of the decade plus 2 dimmed adjacent-decade years, all reusing the month grid's `wss-picker-month-btn`/`wss-picker-month-grid` classes so `wss-picker.js` needed no changes; `Quarter` — `Month` mode's header verbatim over a single `wss-picker-quarter-grid` row of 4 quarter buttons; `Week` — the same panel as `Date` plus a leading week-number column, where a whole row, not a single day, is the selection unit) selects the panel and the commit-time normalization (`Date` keeps the date, `Month` normalizes to the 1st of the month at midnight, `DateTime` truncates to whole seconds, `Time` anchors to `DateTime.Today` plus the time-of-day, `Year` normalizes to January 1st at midnight, `Quarter` normalizes to the quarter's 1st day at midnight, `Week` normalizes to that week's first day, per `FirstDayOfWeek`, at midnight). Picking a day/month/year/quarter/week (or typing text + Enter) commits and closes; in `Time`/`DateTime` mode the time selects — and, in `DateTime` mode, a day click — commit immediately without closing, since the user may still want to adjust the other part, and the new small primary OK button (`wss-picker-ok`) is the close signal instead; `Min` / `Max` (checked at each mode's own granularity — month/year/quarter/week respectively for `Month`/`Year`/`Quarter`/`Week`, date granularity in `DateTime` mode, ignored entirely in `Time` mode; in `Week` mode this only guards the commit — a day button inside a merely partially-out-of-range week still enables and clicks normally, since the click commits that week's start, not the clicked day), `DisabledDate` (`Func<DateTime, bool>?`, an extra predicate folded into the same per-mode granularity as `Min`/`Max` — day midnight in `Date`/`DateTime`/`Week`'s own day buttons, the month/quarter/year start in those modes, the WEEK START (not the clicked day) for `Week`'s own commit guard; a `Week`-mode day click additionally re-checks that week-start guard explicitly, since an arbitrary predicate — unlike `Min`/`Max` — can reject the week start while leaving individual day buttons enabled), `DisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`, disables specific hour/minute/second VALUES in `Time`/`DateTime` mode's time row via a `DisabledTimeParts` record of `Hours`/`Minutes`/`Seconds` collections — invoked once per render of the row with `Value`'s date part, or null when `Value` is null, and once per commit guard; a listed value rejects a select-change or typed-text commit, reverting like a `Min`/`Max` rejection), `HideDisabledTimeOptions` (default false — omits a `DisabledTime`-disabled option from its select entirely instead of rendering it `disabled`; the select's own CURRENT value always renders regardless, selected and `disabled` too if applicable, so a select can never silently show a value that isn't the one actually bound), `ShowSeconds` (default true — false drops the seconds select from `Time`/`DateTime` mode's time row entirely and normalization zeroes the second on every commit), `HourStep`/`MinuteStep`/`SecondStep` (default 1 — steps the matching select's option list to 0/step/2×step/... up to 23/59/59, clamped to a minimum of 1; NEVER-JUMP: an off-lattice bound value's own option still renders, selected, composing with `DisabledTime`'s own never-jump the same way — step-filter first, then disable/hide), `Use12Hours` (default false — renders the hour select in 12-hour form, `12, 1, 2, ... 11` for the currently displayed AM/PM period with option VALUES still 24h, plus a trailing period select; `Value` always stays 24-hour, changing the hour commits its own 24h value, changing the period re-commits the current hour shifted into the other one via `hour % 12 + (isPM ? 12 : 0)`; `HourStep` still applies in 24h space), `Format` / `Placeholder` (both `string?`, null picks `Mode`'s default — `MM/dd/yyyy`/"Select date" for `Date`, `MM/yyyy`/"Select month" for `Month`, `MM/dd/yyyy` plus `Time`'s own string/"Select date" for `DateTime`, `HH:mm:ss`/"Select time" for `Time` (`ShowSeconds` false drops `:ss`; `Use12Hours` switches to `h:mm tt`/`h:mm:ss tt`), `yyyy`/"Select year" for `Year`, "Select quarter" for `Quarter`, "Select week" for `Week`), `AllowClear`, `Width`, `FirstDayOfWeek` (`Date`/`DateTime`/`Week` modes only), `ShowWeekNumbers` (default false — adds the same week-number column to `Date`/`DateTime` mode with no other behavior change; a day click there still commits that day, not its week; `Week` mode always shows the column regardless), `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel` (accessible names for the time/period selects, the last defaulting to "AM/PM"), `PrevDecadeLabel`/`NextDecadeLabel` (default "Previous decade"/"Next decade", `Year` mode's header), `OkText` (default "OK") — the single-value sibling of `DateRangePicker`, sharing its calendar internals and outside-click/Escape close behavior. `Quarter` mode has no .NET format token for its quarter digit: with `Format` left null the input displays/parses `yyyy-Qn` (e.g. "2026-Q3", also accepting "2026Q3" and a case-insensitive "q") via a hand-rolled special case instead of `ToString`/`TryParseExact`; a plain typed date still normalizes to its own quarter; setting `Format` explicitly falls back to formatting the raw bound value verbatim, so a custom format can't render the quarter number itself. `Week` mode is the same kind of special case for its week number: with `Format` left null the input displays/parses `yyyy-Www` (e.g. "2026-W07", the week-start's own calendar year; also accepting "2026W7" and a case-insensitive "w"); a plain typed date still normalizes to its own week start; setting `Format` explicitly is the same verbatim fallback as `Quarter`. Footer affordances: `ShowToday` (default **true**, matching AntD's `showToday`; set false to drop the footer row; `Date`/`Month`/`Quarter`/`Year`/`Week` mode only) adds a `TodayText` (default "Today") link button that commits `DateTime.Today`, mode-normalized, and closes; `ShowNow` (default false, `Time`/`DateTime` mode only) adds a `NowText` (default "Now") link into the EXISTING time-row footer, left of OK, committing `DateTime.Now` mode-normalized WITHOUT closing (OK remains that footer's close signal); both render DISABLED, not hidden, when `Min`/`Max`/`DisabledDate` rejects the normalized commit. `Presets` (`IReadOnlyList<DatePickerPreset>?`, `DatePickerPreset(label, resolveFunc)` — same resolved-at-click-time contract as `DateRangePreset`) renders the SAME `wss-picker-presets`/`wss-picker-preset` sidebar `DateRangePicker` uses; clicking one resolves, mode-normalizes, commits (a guard rejection no-ops), and ALWAYS closes — even in `Time`/`DateTime` mode, where a preset is a complete pick unlike those modes' own incremental time selects. `ExtraFooter` (`RenderFragment?`) renders arbitrary content in its own `wss-picker-extra-footer` strip above the footer row (or alone, in a mode with no footer of its own) in every mode — AntD's `renderExtraFooter`. `DefaultViewDate` (`DateTime?`, AntD's `defaultPickerValue`) sets the panel's initial view when `Value` is null; a set `Value` always wins
- **`DateRangePicker`** - Composite start → end date-range field opening a dropdown with an optional preset sidebar. Bind with `@bind-Start` / `@bind-End` (`DateTime?`); `Mode` (`DatePickerMode`: `Date` default, `Week`, `Month`, `Quarter`, `Year` — a pair of consecutive LINKED panels at that granularity (two one-month calendars, two years of months, two years of quarters, or two decades of years), both endpoints normalizing to the unit's own start — midnight/1st-of-month/1st-of-quarter/January 1st/week-start per `FirstDayOfWeek`; `DateTime`/`Time` abandon the dual-panel layout for a SINGLE panel that edits one endpoint at a time (AntD's `showTime` shape): a day click (`DateTime`) or a time-row change sets the ACTIVE endpoint's pending value without committing, and an OK button confirms it — once both endpoints are resolved it commits them together (swapping a backwards pair) and closes) selects the panel layout and per-endpoint normalization; `Min` / `Max` and `DisabledDate` (`Func<DateTime, bool>?`, checked at `Mode`'s own granularity, same contract as `DatePicker.DisabledDate`); `StartDisabledTime` / `EndDisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`, per-endpoint hour/minute/second restrictions for `DateTime`/`Time` mode's time row — the START/END split lets each side reject different values) and `HideDisabledTimeOptions`; `ShowSeconds` (default true), `HourStep`/`MinuteStep`/`SecondStep` (default 1), `Use12Hours` (default false), `OkText` (default "OK") for that time row — same contracts as `DatePicker`'s own; `Format` (`string?`, null picks `Mode`'s default — same per-mode values as `DatePicker.Format`, including the `yyyy-Qn`/`yyyy-Www` shorthand for `Quarter`/`Week`) with `StartPlaceholder` / `EndPlaceholder` (null default: the uppercased effective format); `AllowClear`, `Width`, `FirstDayOfWeek`, `ShowWeekNumbers` (default false — adds a week-number column beside the day grid(s) in `Date` mode with no change to day-click semantics; `Week` mode always shows it); `Presets` (`IReadOnlyList<DateRangePreset>?` — a label plus a range-resolving `Func` evaluated at click time, or a fixed-dates overload; a click clamps both ends into `Min`/`Max`, normalizes to `Mode`'s granularity, preserves time-of-day in `DateTime`/`Time` mode, and no-ops instead of committing if the normalized result is `DisabledDate`-rejected); `ExtraFooter` and `DefaultViewDate` (mirror `DatePicker`'s own — `ExtraFooter` renders in every mode, including above the `DateTime`/`Time` session's OK footer). Deliberately has no `ShowToday`/`ShowNow`: AntD's `RangePicker` has neither — `Presets` is its quick-pick affordance instead. Picking the second unit of a range (two-click, swapping a backwards pick) or a preset commits and closes; typed input in either field commits on Enter/blur; a `Time`-mode commit keeps each endpoint's own already-committed date part (today when unset) rather than re-stamping to the literal current day. Accessible names mirror `DatePicker`'s convention, doubled per endpoint where relevant: `StartInputLabel`/`EndInputLabel`, `DialogLabel`, `MonthSelectLabel`/`YearSelectLabel`, `ClearLabel`, `PresetsLabel`, `PrevMonthLabel`/`NextMonthLabel`, `PrevYearLabel`/`NextYearLabel`, `PrevDecadeLabel`/`NextDecadeLabel`, `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel`. Shares `DatePicker`'s calendar internals, JS-degradation contract, and outside-click/Escape close behavior
- **`Table<TItem>`** - Data table with `Column` / `PropertyColumn` / `ActionColumn`, row selection, paging (pager placement via `PagerPosition` = Top/Bottom/Both and alignment via `PagerAlign`), and column sorting (`Sortable="true"` on a `PropertyColumn` — non-comparable types degrade to non-sortable; or a `SortBy` comparison on any column). Columns may be conditionally rendered (`@if`). `RowDetail` (a `RenderFragment<TItem>`) adds expandable rows: a leading chevron column toggles the template as a full-width row beneath each row (e.g. a nested child `Table`); expansion is keyed by `RowKey` identity so it survives paging/sorting. `Column.TitleContent` replaces a plain `Title` with templated header content (e.g. a title plus a `LabelTooltip` info icon)
- **`Tabs` / `Tab`** - Underline tab strip with an optional bordered count chip per tab (`Count`); bind with `@bind-ActiveKey` (a `string?`). Tabs with `ChildContent` show the active pane below the strip; content-less tabs act as a bare filter strip. ARIA tabs pattern with automatic activation (arrows move + select with wrapping, roving tabindex; Home/End deliberately unhandled — Blazor can't `preventDefault` per key)
- **`SearchInput`** - Search field: optional leading addon label chip (`AddonLabel`/`AddonContent`), text input (`@bind-Value`, per-keystroke), and an icon-only search button — `OnSearch` fires on Enter and on the button. Pill-rounded ends by default (`--wss-search-radius` to square them)
- **Toasts & notifications** - two paths with identical rendering: **scoped / Server-safe** (`IMessageService` / `INotificationService` via `builder.Services.AddWssControlsToasts()` + `<MessageContainer />` / `<NotificationContainer />`), or **registration-free static for WASM** (`WasmMessageService` / `WasmNotificationService` + `<WasmMessageContainer />` / `<WasmNotificationContainer />`). On Blazor Server use the scoped path — the static `Wasm*` services hold process-static state that would bleed across users.
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

It re-derives placement on every hover/focus (via event delegation, so dynamically-added elements are covered with no extra wiring) and aims at the nearest clipping ancestor or recognized panel boundary (`wss-modal` / `wss-drawer` / `wss-popover`) instead of the screen — so a tooltip inside a `Modal` stays within the modal instead of running past its edges. To force a specific direction yourself (and opt that element out of auto-placement), apply one of the placement classes directly: `wss-tooltip-top`, `wss-tooltip-left`, `wss-tooltip-right`, or the vertically-centered `wss-tooltip-side-left` / `wss-tooltip-side-right` (manual-only — the auto-placer never assigns these two). Tooltips are hidden entirely on touch devices (`hover: none`), since there is no hover to trigger them.

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

`Prefix` also works on the outlined variant and on `EditMultiSelect`; `EditSelectSearch` forwards both `Variant` and `Prefix`.

#### `Mode` example (`DatePicker` / `DateRangePicker`)

`Mode` (`DatePickerMode`) works the same way on both pickers — pick a granularity and the bound value(s) normalize to it. A month-range picker, with no separate "month range" component needed:

```razor
<DateRangePicker @bind-Start="_periodStart" @bind-End="_periodEnd" Mode="DatePickerMode.Month" />
```

Picking January in the left panel and March in the right commits `_periodStart` = Jan 1 and `_periodEnd` = Mar 1 (both midnight). `EditDateRange`/`EditDatePicker` forward the same `Mode` parameter for a validated form field.

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
- **Labeling**: auto-generated from the property name, `[DisplayName]` for constant labels, or the `Label` parameter for dynamic text — see [Labeling: how to choose](#labeling-how-to-choose). `Description` is plain text (HTML-encoded when rendered).
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

### Custom-Styled Checkbox (border-radius)

No current browser (Chromium or Safari/WebKit) honors `border-radius` on a native `<input type="checkbox">` once `accent-color` is set (see [caniuse: accent-color](https://caniuse.com/mdn-css_properties_accent-color)). When a design spec calls for a shaped checkbox, opt in with `UseStyledCheckbox`:

```razor
<EditBool @bind-Value="model.AcceptedTerms" UseStyledCheckbox="true" />
```

The real `<input>` stays in the DOM — focusable, keyboard-operable, full native semantics — but is visually hidden; a sibling element draws the box, checked fill, checkmark, and focus ring in CSS (`.edit-checkbox-box` in `edit-controls.css`), styleable like any other element. Defaults to `null` (falls through to the app-wide switch below, ultimately `false`), so every existing `EditBool` renders exactly as before unless something in that chain turns it on.

`EditCheckedStringList` and `EditCheckedEnumList` take the same `UseStyledCheckbox` (`bool?`) parameter and apply it to every option's checkbox; the UI-kit `Table` (see [UI Kit](#ui-kit-non-form-controls)) takes it too, applied to the header/row selection checkboxes including the indeterminate "mixed" glyph.

#### Turning it on for a whole app or MFE

Setting `UseStyledCheckbox="true"` on every control individually doesn't scale. Instead, set it once — either on the cascaded `FormOptions` for one form/section, or on [`FormDefaults`](#formdefaults) for everything under an app or micro-frontend root — and leave the per-control parameter unset everywhere:

```razor
<FormDefaults UseStyledCheckbox="true">
    <Router AppAssembly="@typeof(App).Assembly">...</Router>
</FormDefaults>
```

Resolution per control (first non-null wins): the control's own `UseStyledCheckbox` parameter → the cascaded `FormOptions.UseStyledCheckbox` → the nearest enclosing `FormDefaults.UseStyledCheckbox` → the process-wide `FormOptions.DefaultUseStyledCheckbox` static (default `false`). `Table` has no `FormOptions` of its own, so it resolves through `FormDefaults` then the static only.

## Styling and Customization

The library provides default styling through the included CSS file. You can customize the appearance by:

1. **Overriding CSS classes** in your own stylesheets
2. **Using ContainerClass** parameter for component-specific styling
3. **Applying custom CSS** to the `.edit-control-wrapper` class

The AntDesign-style UI-kit controls (Alert, Modal, Table, Select, ...) are themed via `--wss-*` CSS custom properties in `wss-controls.css`. They default to the AntDesign 4.x look and **bridge to your existing `--color-primary` / `--color-danger` / `--border-color`** where those are defined, so they pick up your theme automatically. Override any `--wss-*` variable to re-theme.

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

- **Labels, tooltips, descriptions, length/range extraction** — every control resolves its accessor from `@bind-Value`'s compiler-synthesized `ValueExpression`; the expression tree roots the property's getter, so the trimmer keeps the property and the attributes on it. The attribute types themselves (`[DisplayName]`, `[Description]`, `[ToolTip]`, `[Range]`, ...) are referenced by the library and kept.
- **Enum display names** — `[EnumDisplayName]`/`[Display]` lookups only reflect over enum types, whose fields the trimmer always preserves.
- **Option building** — enum option lists use `Enum.GetValuesAsUnderlyingType` (no dynamic array creation), safe under WASM AOT.

Consumer notes:

- The generic controls (`EditNumber<T>`, `EditDate<T>`, `EditSelect<TValue>`, `EditRadio*`) annotate their type parameter with `[DynamicallyAccessedMembers(All)]`, mirroring the framework's `InputNumber`/`InputSelect`. Normal usage (binding concrete model properties) compiles warning-free; only forwarding an open generic parameter into them propagates the annotation.
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

### 10.6.7

**New** (UI Kit)
- Hover tooltips (`data-tooltip`) — ported from the RPG Assistant app's `data-tooltip` convention. Not a component: a `data-tooltip="..."` attribute on any element gets a styled CSS-only hover/focus tooltip (arrow, slide-in, `:focus-visible` support, hidden under `hover: none`) via new rules in `wss-controls.css`, themed through `--wss-*` tokens plus the new `--wss-tooltip-gap` / `--wss-tooltip-z-index` knobs. The optional new `wss-tooltip.js` (a plain `<script>` tag, no interop) auto-places the bubble — above/below and left/right — based on the trigger's position within its nearest clipping ancestor or panel boundary (`wss-modal` / `wss-drawer` / `wss-popover`), so it stays inside a Modal/Drawer instead of running past the edge. See [Hover tooltips](#hover-tooltips-data-tooltip).

**Changed** (Edit Controls)
- `LabelTooltip` (the form-label help-icon popover) is restyled to AntDesign's dark tooltip look — opaque dark chip, 6px radius, arrow, AntD's layered shadow, fade/slide-in — and now auto-places like `data-tooltip` instead of always opening above: the bubble opens below the trigger by default and aims toward the center of the nearest clipping ancestor / panel (flipping above, aligning left/right near an edge) via `wss-tooltip.js`, which `LabelTooltip` lazily imports itself — consumers add nothing, and without JS the CSS default (below, centered) still renders. Hover shows after the same 0.35s hover-intent delay as `data-tooltip`; keyboard focus stays instant. Theming: `--color-tooltip-bg` / `--color-tooltip-text` / `--edit-tooltip-z-index` still honored (the arrow follows the bubble background automatically); new `--edit-tooltip-gap` (default `24px`, below) and `--edit-tooltip-gap-tight` (`3px`, above) knobs; the bubble no longer draws a `--border-color` border. Anything that relied on the old always-above placement will see the new placement.
- `LabelTooltip`'s reveal is now pure CSS `:hover`/`:focus` instead of a C# round-trip per hover; `aria-hidden` on the bubble now carries only the Escape-dismissed state (starts `"false"`, flips `"true"` on Escape until pointer/focus leaves). Rationale: re-rendering mid-hover mutated the DOM under the pointer, and the browser's rebuilt hover chain fired spurious `mouseleave`s that dismissed the bubble while the pointer traveled onto it. Accessibility of the new look/placement, verified end-to-end: the bubble is **hoverable** (WCAG 1.4.13 — pointer-interactive with an invisible gap bridge that exists only while open, so the pointer can travel from icon to bubble and rest on it; text is selectable), Escape-dismiss kept (1.4.13), `prefers-reduced-motion` drops the fade/slide (2.3.3, mirroring the UI kit), and a transparent border keeps a visible bubble boundary under forced-colors / Windows High Contrast. Hover reveal now also works pre-hydration (it previously waited for interactivity).

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

