# WssBlazorControls

[![NuGet Version](https://img.shields.io/nuget/v/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/WssBlazorControls.svg)](https://www.nuget.org/packages/WssBlazorControls/)

A comprehensive library of form controls for Blazor applications providing consistent, feature-rich input components with built-in validation, accessibility support, and flexible styling options.

## Features

- **Rich Form Controls**: String, Number, Date, Color, Boolean, Select, Radio, Checkbox lists, and TextArea components
- **Searchable & Multi-Select**: AntDesign-style `EditSelectSearch` / `EditMultiSelect` — type-to-search, tags, virtualized dropdown
- **AntDesign-style UI Kit**: dependency-free Alert, Modal, Drawer, Table, Pagination, Popover, Popconfirm, DateRangePicker, ColorPicker, Skeleton, and toasts
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
   styling now ships from this stylesheet too, as does `EditColor`'s (`wss-color-picker-*`). Omit it
   only if every date field in your app uses `EditDateNative` instead of `EditDate` and you use no
   `EditColor`.

3. **Include the JS helpers** (next to your Blazor script tag):

```html
<script src="_content/WssBlazorControls/edit-controls.js"></script>
```

   Required by `JsInteropEc.FocusFirstInvalidField` (focus the first invalid field on a failed
   submit). The UI-kit controls — including the `DatePicker` that now backs `EditDate` — load their
   own JS modules (`wss-select.js`, `wss-picker.js`, `wss-color.js`, `wss-overlay.js`, ...) lazily; no extra
   `<script>` tags needed for them. `EditRange` does the same with `wss-slider.js` (its drag support). If the script tag isn't linked (e.g. a cross-origin
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

#### Rich-markup labels (`LabelContent`)

Every control also accepts `LabelContent` (`RenderFragment?`), for a label that needs inline markup a plain string can't hold — the motivating case is a toggle group whose rows need a colored icon plus text, where the icon is load-bearing for identifying the row and the control has to stay a raw `<input type="checkbox">`. Null (the default) renders `Label` exactly as before; setting it replaces the label TEXT with your markup, everywhere `Label` would otherwise render (including `EditBool`'s checkbox row):

```razor
<EditBool @bind-Value="model.IsUrgent">
    <LabelContent>
        <span class="priority-icon priority-icon-urgent" aria-hidden="true"></span>
        Urgent
    </LabelContent>
</EditBool>
```

Two rules to follow:

- **`LabelContent` is phrasing content only — no nested buttons, links, or other interactive elements.** It renders inside the field's naming anchor, and accessible-name computation folds a descendant interactive control's own name into the name built from that content (the same reason the label's own info-tooltip trigger renders as a sibling, not a child, of the label text — a button there once made a tooltipped checkbox announce "Full Name More information about Full Name" instead of "Full Name"). Give any decorative icon `aria-hidden="true"` so it never joins the accessible name.
- **Still set `Label`** (or leave the property name / `[DisplayName]` meaningful) even when you use `LabelContent`. Validation-message text and the accessible-name fallback chain read the resolved `Label` string, never the `LabelContent` fragment — a consumer who sets only `LabelContent` gets validation text built from the auto-generated/attribute-derived name, which may not match what's visually shown.

## Available Controls

### Input Controls
- **`EditString`** — Text input with masking and URL support.
  - Read-only mode picks a masked row, a sanitized link, or plain text, in that order — `MaskText` beats `Url`, a `javascript:` URL never becomes a link, and `UrlTarget` auto-hardens `rel` for named targets as well as `_blank`. See [Read-only views](#read-only-views-editstring).
  - `Prefix`/`Suffix` affix content, `AllowClear`, `MaxLength`/`ShowCount`, and an `IsPassword` secret marker (a show/hide toggle in edit mode; a bullet-masked row, not plain text, in read-only mode — an explicit `MaskText` still wins there) switch the input into an AntD-style affix layout via the shared internal `EditInputShell`; plain markup stays byte-identical to the classic rendering.
  - `Size` (`SelectSize`: `Default`/`Small`/`Large`, shared with the `Select` family) adds a size class to the input (and the affix wrapper, in affix mode); inert unless [`.edit-theme`](#opt-in-antd-theme-for-the-classic-edit-inputs-edit-theme) is opted into.
  - `Placeholder`, `Autocomplete`, `MaxLength`, and `IsPassword` (now `string?`/`int?`/`bool?`) fall back to the bound property's `[Placeholder]`/`[Display(Prompt)]`, `[Autocomplete]`, `[StringLength]`/`[MaxLength]`, and `[DataType(Password)]` respectively. See [Model-declared placeholders](#model-declared-placeholders-placeholder) and [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
  - `Suggestions` (`IEnumerable<string>?`) renders an HTML `<datalist>` wired to the input's `list` attribute — non-binding autofill hints for an **open** vocabulary (any typed value is accepted), unlike `EditSelectSearch`/`EditMultiSelect`'s **closed** vocabulary (the bound value must be one of the supplied options). Pick `Suggestions` for free text you just want to speed up; pick a Select control when the value must come from a known list.
    ```razor
    <EditString @bind-Value="model.City" Suggestions="@(new[] { "Chicago", "Denver", "Portland" })" />
    ```
    Null (the default) renders neither the attribute nor the `<datalist>` — a consumer who already hand-wires `list="myListId"` plus their own `<datalist id="myListId">` keeps working untouched. A non-null but *empty* sequence still renders both (an empty `<datalist>`), so a filtered list that transiently empties mid-fetch doesn't flicker the attribute on and off. `EditNumber` supports the same parameter (`list` is valid on `type="number"` too); `EditTextArea` does not (`list` has no meaning on a `<textarea>`). Three things worth knowing:
    - **The sequence is re-enumerated on every render**, so pass something stable and repeatable — an array, a `List<string>`, or a pure LINQ query over one. A single-pass or side-effecting sequence (a raw iterator, a generator that yields fresh values per call) renders different options on each pass.
    - **Suppressed on a password field** (`IsPassword`, or `[DataType(Password)]`), and it stays suppressed after the user presses the reveal toggle — where `type` has flipped to `text` and `list` *would* apply. The rule is about the field, not the current `type` attribute: a secret is not a place to offer a shared hint list, and gating on the live type would blink a datalist into existence mid-gesture. (In the masked state it would additionally be dead markup — `list` isn't defined for `type="password"`.)
    - **It does not change `autocomplete`.** A field whose property name matches nothing in the `Autocomplete` inference table still renders `autocomplete="one-time-code"` (the built-in suppressor) alongside `list=`. Set `Autocomplete` — or `[Autocomplete]` on the model — to the field's real purpose token (`"organization"`, `"address-level2"`, …), which is better than any suppressor for WCAG 1.3.5, or to `"off"` if the datalist should be the only popup offered. Whether a browser prefers its own autofill affordance over a `<datalist>` popup is UA-specific and is not something this library has verified.

    The `<datalist>`'s own `id` is a generated per-instance value (`dl-{guid}`) and is not part of the public API — don't select on it. It is deliberately *not* derived from the control's element `id`: a list of rows bound to the same property (`@bind-Value="row.Name"`) resolves one shared element id, and a datalist id built from it would collide, at which point every row would display the *first* row's suggestions (browsers resolve `list=` by `getElementById`).
- **`EditTextArea`** — Multi-line text input.
  - `AllowClear`, `MaxLength`/`ShowCount` (the count renders below the box, right-aligned — AntD `TextArea`'s placement, unlike `EditString`'s inline count), and `AutoSize`/`MinRows`/`MaxRows` (JS-driven grow/shrink to fit content, clamped between the two, degrading to the fixed `Rows` height with no JS) switch the input into the shared `EditInputShell` layout; plain markup stays byte-identical to the classic rendering.
  - `Size` behaves the same as `EditString`'s — only padding/font change; height is never locked, so `Rows`/`AutoSize` still govern it. `Placeholder` resolves the same model-attribute fallback as `EditString`'s.
  - `Rows`/`MinRows`/`MaxRows`/`AutoSize` (now `int?`/`int?`/`int?`/`bool?`) and `MaxLength` fall back to `[Rows]` and `[StringLength]`/`[MaxLength]` respectively. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
- **`EditNumber`** — Numeric input with validation.
  - `Min`/`Max` (InvariantCulture, same type discipline as the existing `Step`) fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]`, when unset. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
  - `Placeholder` uses the same model-attribute fallback as `EditString`'s; `Prefix`/`Suffix` affix content goes via the shared `EditInputShell` (no `AllowClear`/`ShowCount`/`IsPassword` — no AntD equivalent for a numeric field; native spinners stay, a documented deviation).
  - `Suggestions` (`IEnumerable<string>?`) — same open-vocabulary `<datalist>` hint feature as `EditString.Suggestions` (see there for the null-vs-empty contract, the stable-sequence requirement, and the generated `dl-{guid}` id); no password mode here to suppress it for. Format the option values the way the native number input round-trips them (InvariantCulture) — a value it can't parse just lands as an empty entry when picked. Composes with `ShowStepper`: the `<datalist>` renders outside the stepper's button group, never as a fourth item inside it.
  - `Size` behaves the same as `EditString`'s. `Step` (now `decimal?`) falls back to `[Step]`; `Format` falls back to `[DisplayFormat]`. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
  - `ShowStepper` (default false) adds a minus button before the input and a plus button after it, joined into one group. A press moves the value by the effective step (`Step` → `[Step]` → 1), clamped to `Min`/`Max`, and each button renders `disabled` once the value already sits at its bound; a null value steps from 0. Native spinners are hidden while the group renders, and `Size` scales the buttons with the editor. Two deliberate deviations from AntD's `InputNumber` handlers: the buttons are horizontal rather than stacked, and there is no press-and-hold auto-repeat — they also carry `tabindex="-1"` (keyboard users step with the input's own arrow keys), with accessible names defaulting to `"Decrease {label}"`/`"Increase {label}"` and overridable via `DecreaseButtonLabel`/`IncreaseButtonLabel`. Leaving it off renders byte-identical markup to before.
- **`EditRange`** — AntDesign-style horizontal slider over a single numeric value (named after the native `<input type="range">` it replaces): rail, filled track, round handle, optional marks/dots, and a value tooltip. Same `@bind-Value` contract and the same `[MinValue]`/`[MaxValue]`/`[Range]`/`[Step]` model-attribute resolution as `EditNumber`.
  - `Min`/`Max` (`decimal?`) fall back to those model attributes and then to **0/100** — unlike `EditNumber`, whose bounds are simply omitted when nothing resolves, a slider always needs both ends to place its handle. `Step` (`decimal?`) falls back to `[Step]`, then 1, and anchors its increments at `Min` (a `Min` of 5 with a step of 10 offers 5/15/25).
  - `Marks` (`IReadOnlyDictionary<decimal, string>?`) renders labeled, clickable points under the rail; a value landing exactly on one announces that mark's label through `aria-valuetext`. `SnapToMarks` restricts the value to those positions — the library's spelling of AntD's `step={null}`, which a `decimal? Step` can't express (null already means "fall back to `[Step]`/1").
  - `Dots` draws a dot at every step increment and every mark, dropping the step dots entirely past 100 of them rather than filling the rail (so `Dots` with the default step of 1 over 0..100 draws the marks alone). `Included` (default true) turns off the filled track and the active dot/mark styling, for AntD's discrete-points presentation.
  - `ShowTooltip` (default true) shows a value bubble on hover, on focus, and for the whole of a drag; `TooltipFormat` (a .NET numeric format string, falling back to `[DisplayFormat]`) drives that bubble, the `aria-valuetext` it implies, and the read-only text.
  - The `role="slider"` tab stop is the **track**, not the handle. Arrow keys move one step (one mark under `SnapToMarks`), PageUp/PageDown ten, Home/End jump to the bounds; a drag commits continuously. Without JavaScript (prerender, or a host that can't reach the lazily-imported `wss-slider.js`) a click still positions the handle and the whole keyboard model still works — only dragging is lost. That click fallback assumes the default `--edit-range-width`, so overriding that token skews a no-JS click (the normal JS path measures the real element).
  - No vertical or reversed orientation, no dual-handle range, and no always-visible tooltip mode — see the `edit-controls` skill for the full deviation list.
- **`EditDate`** — Form-bound calendar-dropdown date field (the UI-kit `DatePicker` with full `EditForm` validation); the default date control.
  - Full type parity with `EditDateNative` — binds `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` (and their nullable variants). `Type` (`InputDateType`: `Date`/`DateTimeLocal`/`Month`/`Time`, same default as `EditDateNative`) selects what the calendar picks, mapped onto the picker's `Mode`.
  - A separate `Mode` parameter (`DatePickerMode?`, default null) overrides that mapping outright to reach `Week`/`Quarter`/`Year` — the one intentional asymmetry with `EditDateNative`, which has no such escape hatch since its native `<input>` types have no week/quarter/year equivalent to reach.
  - `Min`/`Max` (`DateTime?`, date-granularity, ignored in `Time` mode) fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]` — the same resolution `EditNumber` uses. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
  - Forwards the picker's full phase-2 surface too, same defaults as the picker itself: `ShowWeekNumbers`, `DisabledDate`, `DisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`, `ShowToday`/`ShowNow`/`Presets`/`ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params.
  - `Size` (`SelectSize`: `Default`/`Small`/`Large`) renders `wss-picker-sm`/`wss-picker-lg` on the picker wrapper, mirroring `Select`'s own size classes.
  - `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) surfaces a validation message when a typed entry can't be parsed as a date at all — a well-formed date merely rejected by `Min`/`Max`/`DisabledDate`/`DisabledTime` does not trigger it; previously a bad typed entry was silently reverted with no feedback.
  - `RangeErrorMessage` (`string`, default `"The {0} field must be an allowed date."`) covers the opposite case: a well-formed typed date that `Min`/`Max`/`DisabledDate`/`DisabledTime` rejects — previously silent in every channel, same as an unparseable entry used to be before `ParsingErrorMessage` existed.
  - Unlike `EditDateNative`, `EditDate` has no `UpdateOn`: a calendar picker commits on selection or on parse-at-blur/Enter, so there's no per-keystroke commit to opt into — use `EditDateNative` if you need that axis.
  - `Placeholder` (null default) falls back to the bound property's `[Placeholder]`/`[Display(Prompt)]`, then to the inner picker's own mode-derived default (e.g. "Select date"). See [Model-declared placeholders](#model-declared-placeholders-placeholder).
  - `Type` (now `InputDateType?`) falls back to the bound property's `[DataType(DataType.Date/DateTime/Time)]`; `Format`/`DateFormat` fall back to `[DisplayFormat]`. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
- **`EditDateNative`** — Native `<input type="date">` (or `datetime-local`/`month`/`time`, per `Type`) date field, zero JS, styled entirely by `edit-controls.css`.
  - `Min`/`Max` (`DateTime?`, same shape as `EditDate`'s — new in 10.7.0, its first bounds support ever) render the native input's own `min`/`max` attribute formatted to match `Type`, omitted entirely in `Time` mode for parity with `EditDate`, and fall back to the bound property's `[MinValue]`/`[MaxValue]`, then `[Range]`, when unset. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
  - `Size` behaves the same as `EditString`'s, though `EditDateNative` never enters affix mode itself (no `Prefix`/`Suffix`/`AllowClear`/etc.), so only the input itself carries the size class.
  - `Type` (now `InputDateType?`) falls back to the bound property's `[DataType(DataType.Date/DateTime/Time)]`; `DateFormat` (now `string?`) falls back to `[DisplayFormat]`. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
  - `FormatHintLabel` (`string`, default `"Format:"`) adds a visually-hidden format hint to the input's `aria-describedby` chain — relevant only in `Month` mode, the one native type whose typed format isn't otherwise obvious from the rendered control.
- **`EditDateRange`** — Form-bound date-range field (`@bind-Start`/`@bind-End`, per-field validation, backed by `DateRangePicker`); forwards `DateRangePicker`'s full surface.
  - `Mode` (`DatePickerMode`, default `Date`) — dual linked panels at `Date`/`Week`/`Month`/`Quarter`/`Year` granularity, or a single-panel OK-confirm session for `DateTime`/`Time`.
  - `Min`/`Max`: `Min` resolves param → Start's `[MinValue]`/`[Range]` → End's; `Max` resolves param → End's `[MaxValue]`/`[Range]` → Start's — so a single `[Range]` on `Start` alone supplies both ends. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
  - Also forwards `DisabledDate`, `StartDisabledTime`/`EndDisabledTime`/`HideDisabledTimeOptions`, `ShowSeconds`/`HourStep`/`MinuteStep`/`SecondStep`/`Use12Hours`/`OkText`, `ShowWeekNumbers`, `Presets`, `ExtraFooter`/`DefaultViewDate`, and the matching accessible-name params.
  - `Format` (the picker's own display/parse format) and `DateFormat` (the read-only display format) are both nullable with `Mode`-aware defaults — mirroring `EditDate`'s own `DateFormat` contract — instead of a fixed literal, so switching `Mode` alone still gets that mode's own default rather than silently keeping `Date`'s.
  - Read-only display is `Mode`-aware too: `Quarter`/`Week` render the same `yyyy-Qn`/`yyyy-Www` shorthand the picker itself shows.
  - `StartPlaceholder`/`EndPlaceholder` each fall back to their own bound property's `[Placeholder]`/`[Display(Prompt)]` independently — a `[Placeholder]` on `Start` never leaks onto `End` — then to the picker's own default. See [Model-declared placeholders](#model-declared-placeholders-placeholder).
  - `Format`/`DateFormat` fall back to a `[DisplayFormat]` on `Start`'s attributes first, then `End`'s. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
  - `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) surfaces a validation message against whichever endpoint's typed text can't be parsed as a date at all (`{0}` is that endpoint's own field name; a well-formed value merely rejected by `Min`/`Max`/`DisabledDate`/`*DisabledTime` does not trigger it), each endpoint's message clearing independently as soon as that endpoint next commits a valid value.
  - `RangeErrorMessage` (`string`, default `"The {0} field must be an allowed date."`) is `ParsingErrorMessage`'s counterpart for a well-formed typed date that endpoint's own `Min`/`Max`/`DisabledDate`/`*DisabledTime` rejects — same per-endpoint clearing, previously silent.
- **`EditColor`** — Form-bound color field (an AntDesign-5-style swatch trigger opening the UI-kit `ColorPicker`), binding a plain `string?`.
  - Accepts 3/4/6/8-digit hex (with or without `#`) and `rgb()`/`rgba()` text; emits normalized lowercase `#rrggbb`, extended to `#rrggbbaa` only when the color is translucent *and* `ShowAlpha` is on. A value it can't parse (including null/empty) renders as "no color" rather than an error.
  - `ShowAlpha` (`bool`, default `true`) shows the alpha slider and allows an alpha channel in the value; `false` also **strips** the channel from what's emitted. `ShowText` (`bool`, default `false`) renders the normalized hex beside the swatch, on both the edit-mode trigger and the read-only view (which otherwise shows the swatch alone). `AllowClear` (`bool`, default `false`) adds a clear affordance that sets the bound value to `null`.
  - `Presets` (`IReadOnlyList<string>?`) adds a labeled swatch row inside the popup; any form the control accepts works as an entry.
  - `ParsingErrorMessage` (`string`, default `"The {0} field must be a color."`) surfaces a validation message when a typed HEX entry can't be parsed at all — the same `ValidationMessageStore` mechanism `EditDate` uses, since a picker commits through a value callback rather than string parsing.
  - See [Color picking](#color-picking-editcolor--colorpicker) for the keyboard model, the popup's contents, and what happens without JavaScript.
- **`EditBool`** - Checkbox for boolean values. `TrueText`/`FalseText` (now `string?`) fall back to the bound property's `[BoolText]` — see [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints)
- **`EditBoolNullRadio`** - Three-state radio for nullable booleans. `TrueText`/`FalseText`/`NullText` (now `string?`) fall back to the same `[BoolText]` attribute
- **`EditFile`** — Multi-file upload bound to a `List<IBrowserFile>` (drag-and-drop + click-to-browse, extension filtering, per-file size cap, aggregate size cap, optional max count).
  - `AllowedExtensions` also accepts MIME types (`"application/pdf"`) and MIME wildcards (`"image/*"`), not just extensions; `BeforeAdd` is an optional async per-file gate before buffering; each listed file shows its formatted size; `Variant="EditFileVariant.Button"` swaps the dashed dropzone for a compact plain button; `Bordered` wraps the label and picker/file-list in one card; `AllowDownload` turns each file name into a link that re-saves its already-buffered bytes. See [File upload parity features](#file-upload-parity-features-editfile).
  - `AllowedExtensions`/`MaxFileSizeBytes`/`MaxFiles`/`MaxTotalBytes` (the last three now `long?`/`int?`/`long?`) fall back to the bound property's `[FileConstraints]`. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).

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

- **`Select<T>`** — The dropdown engine behind `EditSelectSearch`/`EditMultiSelect`; usable standalone (single/multiple/tags, search, virtualized).
  - `Prefix` renders leading content (typically an icon) in the trigger; `Variant="SelectVariant.Pill"` restyles the trigger as a rounded filter button, `SelectVariant.Borderless` shows no border/background until hover/focus (see below).
  - `Loading`/`ShowArrow` control the arrow slot; `SelectOption.Group` renders AntD-style `OptGroup` headers; `FilterOption`, `EmptyContent`, `DropdownFooter`, and a controlled `Open`/`OpenChanged` round out the parity with Ant Design's `Select`. See [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).
- **`Alert`** — Contextual message banner (success / info / warning / error, closable, description).
  - `Banner` (full-width, borderless AntD banner mode) and `Action` (a trailing slot before the close button) round out AntD 4.x parity.
  - Announces its severity word to assistive tech via a visually-hidden span (the icon alone is `aria-hidden`).
  - `Live` (`bool`, default true) renders `role="status"`/`"alert"` + a matching `aria-live` on mount; set false for a persistently-rendered banner (e.g. one that mounts on every route change) so it doesn't re-announce itself each time — renders `role="group"` instead.
  - `CloseButtonLabel` (default "Close") localizes the close button's accessible name.
  - `SeverityLabel` (`string?`, default null) overrides just that severity word for localization — pass the word alone; the component still appends the trailing `": "` separator.
- **`Skeleton`** — Loading placeholder with shimmer; announces `role="status"` / `aria-busy` with a visually-hidden `LoadingText` (default `"Loading"`) for screen readers.
  - `Avatar`/`AvatarShape` add an avatar placeholder block; the standalone `SkeletonElement` (`Kind`: `Button`/`Input`) covers AntD's `Skeleton.Button`/`Skeleton.Input` shapes.
- **`Popover`** — Click-triggered popover (4 placements); controlled `Visible`/`VisibleChanged` (`@bind-Visible`) mirrors `Select`'s controlled `Open` design.
  - `AriaLabel` (default **"Popover"**) names the `role="dialog"` panel whenever no `Title`/`TitleContent` is set — a dialog must never be nameless (axe `aria-dialog-name`); ignored once a title is present.
  - While open, the trigger's `aria-controls` mirrors the panel's own id.
- **`ColorPicker`** — The swatch-trigger color popup behind `EditColor`; usable standalone, binding a plain `string?` via `@bind-Value` (see [Color picking](#color-picking-editcolor--colorpicker)).
  - A saturation/brightness area, a hue slider, an optional alpha slider (`ShowAlpha`), a HEX/RGB input row, and an optional `Presets` swatch row; `ShowText`, `AllowClear`, `Disabled`, and `Placement` round out the surface.
  - Every track is a `role="slider"` the arrow keys step (Shift or PageUp/PageDown for the larger step); the popup is a `role="dialog"` whose id the trigger mirrors as `aria-controls` while open.
  - `OnParseError` (`EventCallback<string>`) reports a typed HEX entry that can't be parsed — what `EditColor` turns into a validation message.
  - Deliberately **uncontrolled**: unlike `Popover`/`Popconfirm` there is no `Visible`/`VisibleChanged`, because the popup is only ever opened by its own trigger and a controlled open is the shape that can bypass `Disabled`.
  - Needs JS for pointer dragging; without it a single click still positions the handle and the keyboard path is unaffected.
- **`Pagination`** — Controlled pager.
  - `ShowTotal`, a `PageSizeOptions` size-changer (`@bind-PageSize`), `ShowQuickJumper`, and `Small` round out AntD 4.x parity (see [Pagination parity features](#pagination-parity-features-pagination)).
  - `Disabled` makes the whole pager inert (buttons, size-changer, quick-jumper) — `Table.Loading` uses it to give its own masked pagers keyboard, not just pointer, inertness.
- **`Modal`** — Dialog with `@bind-Visible`, footer, mask-close.
  - `Centered` vertically centers it; `Keyboard` (default true) independently governs Escape-to-close (`Closable` only shows/hides the header X).
  - `AriaLabel` (default **"Dialog"**) names the panel when untitled.
  - While open, everything behind the dialog — outside the toast layers, `#components-reconnect-modal`/`#blazor-error-ui`, and anything marked `data-wss-keep-interactive` (an escape hatch for shell chrome you don't own, e.g. an MFE host header) — is made `inert` (out of the a11y tree, tab order, and hit-testing), not just focus-trapped; stacked dialogs let only the topmost own the background, restoring on the last close.
  - The sweep only **recomputes on open/close**, not continuously, so the toast containers and any `data-wss-keep-interactive` element must already be mounted before a dialog opens (the documented app-root placement guarantees this) — one mounted into an already-inert branch while a dialog is open stays inert until the next open/close.
  - Needs JS; without it the focus trap/inert background are absent but initial focus still lands on the panel from C#.
  - **Initial focus goes to the first focusable element in the panel — unless something inside it already has focus.** Because the children render for the first time as the dialog opens, a child's own `FocusOnFirstRender` (or your `FocusAsync()` from `OnAfterRenderAsync`) lands first and the overlay leaves it alone; without that check the close X won the race and swallowed every in-dialog focus request. `Drawer` behaves identically.
- **`Drawer`** — Slide-in panel (4 placements).
  - `Extra` renders a header-right slot beside the close button; `Keyboard` (default true) independently governs Escape-to-close, same as `Modal`.
  - `AriaLabel` (default **"Drawer"**) names the panel when untitled; shares `Modal`'s background-`inert` behavior above.
- **`Popconfirm`** — Inline confirm popover.
  - A genuinely-async `OnConfirm` keeps the popup open with a spinner until it resolves; `OkDanger` styles the OK button as danger; controlled `Visible`/`VisibleChanged` respects `Disabled`.
  - `AriaLabel` (default **"Confirm"**) names the panel when untitled — but note `Title` **is** the confirmation question here, so an untitled `Popconfirm` shows only the warning icon and the OK/Cancel buttons; prefer setting `Title` and treat `AriaLabel` as the fallback for the rare icon-only case.
  - The trigger's `aria-controls` mirrors the open panel's id, same as `Popover`.
- **`DatePicker`** — Single-value field with a calendar suffix opening a dropdown panel. Bind with `@bind-Value` (`DateTime?`).
  - `Mode` (`DatePickerMode`) selects the panel and the commit-time normalization:
    - `Date` (default) — a one-month calendar with a month/year quick-select header; keeps the picked date.
    - `Month` — a year header over a 3×4 month-button grid; normalizes to the 1st of the month at midnight.
    - `Time` — hour/minute/second selects over an OK button; anchors to `DateTime.Today` plus the time-of-day.
    - `DateTime` — the day calendar with that same time row and OK button appended below it; truncates to whole seconds.
    - `Year` — a decade header (prev/next-decade nav + a static "2020-2029" label) over a 3×4 year-button grid (10 of the decade plus 2 dimmed adjacent-decade years, reusing the month grid's `wss-picker-month-btn`/`wss-picker-month-grid` classes so `wss-picker.js` needed no changes); normalizes to January 1st at midnight.
    - `Quarter` — `Month` mode's header verbatim over a single `wss-picker-quarter-grid` row of 4 quarter buttons; normalizes to the quarter's 1st day at midnight.
    - `Week` — the same panel as `Date` plus a leading week-number column, where a whole row (not a single day) is the selection unit; normalizes to that week's first day, per `FirstDayOfWeek`, at midnight.
  - Picking a day/month/year/quarter/week (or typing text + Enter) commits and closes. In `Time`/`DateTime` mode the time selects — and, in `DateTime` mode, a day click — commit immediately without closing, since the user may still want to adjust the other part; the small primary OK button (`wss-picker-ok`) is the close signal instead.
  - `Min`/`Max` are checked at each mode's own granularity (month/year/quarter/week respectively for `Month`/`Year`/`Quarter`/`Week`, date granularity in `DateTime` mode, ignored entirely in `Time` mode). In `Week` mode this only guards the commit — a day button inside a merely partially-out-of-range week still enables and clicks normally, since the click commits that week's start, not the clicked day.
  - `DisabledDate` (`Func<DateTime, bool>?`) is an extra predicate folded into that same per-mode granularity — day midnight in `Date`/`DateTime`/`Week`'s own day buttons, the month/quarter/year start in those modes, the week start (not the clicked day) for `Week`'s own commit guard. A `Week`-mode day click additionally re-checks that week-start guard explicitly, since an arbitrary predicate — unlike `Min`/`Max` — can reject the week start while leaving individual day buttons enabled.
  - `DisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`) disables specific hour/minute/second values in `Time`/`DateTime` mode's time row, via a `DisabledTimeParts` record of `Hours`/`Minutes`/`Seconds` collections — invoked once per render of the row (with `Value`'s date part, or null when `Value` is null) and once per commit guard; a listed value rejects a select-change or typed-text commit, reverting like a `Min`/`Max` rejection.
  - `HideDisabledTimeOptions` (default false) omits a `DisabledTime`-disabled option from its select entirely instead of rendering it `disabled`; the select's own current value always renders regardless (selected and `disabled` too if applicable), so a select can never silently show a value that isn't the one actually bound.
  - `ShowSeconds` (default true) — false drops the seconds select from `Time`/`DateTime` mode's time row entirely, and normalization zeroes the second on every commit.
  - `HourStep`/`MinuteStep`/`SecondStep` (default 1) step the matching select's option list to 0/step/2×step/... up to 23/59/59, clamped to a minimum of 1. Never-jump: an off-lattice bound value's own option still renders, selected, composing with `DisabledTime`'s own never-jump the same way — step-filter first, then disable/hide.
  - `Use12Hours` (default false) renders the hour select in 12-hour form (`12, 1, 2, ... 11` for the currently displayed AM/PM period, with option values still 24h) plus a trailing period select. `Value` always stays 24-hour: changing the hour commits its own 24h value, changing the period re-commits the current hour shifted into the other one via `hour % 12 + (isPM ? 12 : 0)`; `HourStep` still applies in 24h space.
  - `Format`/`Placeholder` (both `string?`, null picks `Mode`'s default): `MM/dd/yyyy`/"Select date" for `Date`, `MM/yyyy`/"Select month" for `Month`, `MM/dd/yyyy` plus `Time`'s own string/"Select date" for `DateTime`, `HH:mm:ss`/"Select time" for `Time` (`ShowSeconds` false drops `:ss`; `Use12Hours` switches to `h:mm tt`/`h:mm:ss tt`), `yyyy`/"Select year" for `Year`, "Select quarter" for `Quarter`, "Select week" for `Week`.
  - `AllowClear`, `Width`, `Size` (`SelectSize`: `Default`/`Small`/`Large`, adds `wss-picker-sm`/`wss-picker-lg` to the outer wrapper, mirroring `Select`'s own size classes; `Default` adds no class), `FirstDayOfWeek` (`Date`/`DateTime`/`Week` modes only).
  - `ShowWeekNumbers` (default false) adds the same week-number column to `Date`/`DateTime` mode with no other behavior change — a day click there still commits that day, not its week; `Week` mode always shows the column regardless.
  - Accessible names: `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel` (for the time/period selects, the last defaulting to "AM/PM"), `PrevDecadeLabel`/`NextDecadeLabel` (default "Previous decade"/"Next decade", `Year` mode's header), `FormatHintLabel` (default "Format:") — a visually-hidden "`{FormatHintLabel} {format}`" hint folded into the input's `aria-describedby`, telling AT users the exact format the field parses; blank it (empty string) to suppress the hint entirely.
  - `OkText` (default "OK") — the single-value sibling of `DateRangePicker`, sharing its calendar internals and outside-click/Escape close behavior.
  - `Quarter` mode has no .NET format token for its quarter digit: with `Format` left null the input displays/parses `yyyy-Qn` (e.g. "2026-Q3", also accepting "2026Q3" and a case-insensitive "q") via a hand-rolled special case instead of `ToString`/`TryParseExact`; a plain typed date still normalizes to its own quarter. Setting `Format` explicitly falls back to formatting the raw bound value verbatim, so a custom format can't render the quarter number itself.
  - `Week` mode is the same kind of special case for its week number: with `Format` left null the input displays/parses `yyyy-Www` (e.g. "2026-W07", the week-start's own calendar year; also accepting "2026W7" and a case-insensitive "w"); a plain typed date still normalizes to its own week start. Setting `Format` explicitly is the same verbatim fallback as `Quarter`.
  - Footer affordances: `ShowToday` (default **true**, matching AntD's `showToday`; set false to drop the footer row; `Date`/`Month`/`Quarter`/`Year`/`Week` mode only) adds a `TodayText` (default "Today") link button that commits `DateTime.Today`, mode-normalized, and closes. `ShowNow` (default false, `Time`/`DateTime` mode only) adds a `NowText` (default "Now") link into the existing time-row footer, left of OK, committing `DateTime.Now` mode-normalized without closing (OK remains that footer's close signal). Both render disabled, not hidden, when `Min`/`Max`/`DisabledDate` rejects the normalized commit.
  - `Presets` (`IReadOnlyList<DatePickerPreset>?`, `DatePickerPreset(label, resolveFunc)` — same resolved-at-click-time contract as `DateRangePreset`) renders the same `wss-picker-presets`/`wss-picker-preset` sidebar `DateRangePicker` uses; clicking one resolves, mode-normalizes, commits (a guard rejection no-ops), and always closes — even in `Time`/`DateTime` mode, where a preset is a complete pick unlike those modes' own incremental time selects.
  - `ExtraFooter` (`RenderFragment?`) renders arbitrary content in its own `wss-picker-extra-footer` strip above the footer row (or alone, in a mode with no footer of its own) in every mode — AntD's `renderExtraFooter`.
  - `DefaultViewDate` (`DateTime?`, AntD's `defaultPickerValue`) sets the panel's initial view when `Value` is null; a set `Value` always wins.
- **`DateRangePicker`** — Composite start → end date-range field opening a dropdown with an optional preset sidebar. Bind with `@bind-Start`/`@bind-End` (`DateTime?`).
  - `Mode` (`DatePickerMode`: `Date` default, `Week`, `Month`, `Quarter`, `Year`) selects the panel layout and per-endpoint normalization:
    - `Date`/`Week`/`Month`/`Quarter`/`Year` — a pair of consecutive **linked** panels at that granularity (two one-month calendars, two years of months, two years of quarters, or two decades of years), both endpoints normalizing to the unit's own start (midnight/1st-of-month/1st-of-quarter/January 1st/week-start per `FirstDayOfWeek`).
    - `DateTime`/`Time` — abandon the dual-panel layout for a **single panel** that edits one endpoint at a time (AntD's `showTime` shape): a day click (`DateTime`) or a time-row change sets the active endpoint's pending value without committing, and an OK button confirms it — once both endpoints are resolved it commits them together (swapping a backwards pair) and closes.
  - `Min`/`Max` and `DisabledDate` (`Func<DateTime, bool>?`, checked at `Mode`'s own granularity — same contract as `DatePicker.DisabledDate`).
  - `StartDisabledTime`/`EndDisabledTime` (`Func<DateTime?, DisabledTimeParts?>?`, per-endpoint hour/minute/second restrictions for `DateTime`/`Time` mode's time row — the start/end split lets each side reject different values) and `HideDisabledTimeOptions`.
  - `ShowSeconds` (default true), `HourStep`/`MinuteStep`/`SecondStep` (default 1), `Use12Hours` (default false), `OkText` (default "OK") for that time row — same contracts as `DatePicker`'s own.
  - `Format` (`string?`, null picks `Mode`'s default — same per-mode values as `DatePicker.Format`, including the `yyyy-Qn`/`yyyy-Www` shorthand for `Quarter`/`Week`) with `StartPlaceholder`/`EndPlaceholder` (null default: the uppercased effective format).
  - `AllowClear`, `Width`, `Size` (`SelectSize`, same contract as `DatePicker.Size`), `FirstDayOfWeek`, `ShowWeekNumbers` (default false — adds a week-number column beside the day grid(s) in `Date` mode with no change to day-click semantics; `Week` mode always shows it).
  - `Presets` (`IReadOnlyList<DateRangePreset>?` — a label plus a range-resolving `Func` evaluated at click time, or a fixed-dates overload); a click clamps both ends into `Min`/`Max`, normalizes to `Mode`'s granularity, preserves time-of-day in `DateTime`/`Time` mode, and no-ops instead of committing if the normalized result is `DisabledDate`-rejected.
  - `ExtraFooter` and `DefaultViewDate` mirror `DatePicker`'s own — `ExtraFooter` renders in every mode, including above the `DateTime`/`Time` session's OK footer.
  - Deliberately has no `ShowToday`/`ShowNow`: AntD's `RangePicker` has neither — `Presets` is its quick-pick affordance instead.
  - Picking the second unit of a range (two-click, swapping a backwards pick) or a preset commits and closes; typed input in either field commits on Enter/blur; a `Time`-mode commit keeps each endpoint's own already-committed date part (today when unset) rather than re-stamping to the literal current day.
  - Accessible names mirror `DatePicker`'s convention, doubled per endpoint where relevant: `StartInputLabel`/`EndInputLabel`, `DialogLabel`, `MonthSelectLabel`/`YearSelectLabel`, `ClearLabel`, `PresetsLabel`, `PrevMonthLabel`/`NextMonthLabel`, `PrevYearLabel`/`NextYearLabel`, `PrevDecadeLabel`/`NextDecadeLabel`, `HourSelectLabel`/`MinuteSelectLabel`/`SecondSelectLabel`/`PeriodSelectLabel`.
  - `OnStartParseError`/`OnEndParseError` (`EventCallback<string>`) are raised with the offending text when a typed commit in that endpoint's input fails to parse at all — never for a well-formed value the picker merely rejects on `Min`/`Max`/`DisabledDate`/`*DisabledTime` grounds; with no handler attached the text is silently reverted as before (`EditDateRange` uses them to raise a validation message the picker itself has no concept of).
  - `FormatHintLabel` (default "Format:") — same shared-format hint as `DatePicker`'s, appended to each endpoint's own `aria-describedby`.
  - Shares `DatePicker`'s calendar internals, JS-degradation contract, and outside-click/Escape close behavior.

> **Accessibility notes for both pickers, used standalone (outside `EditDate`/`EditDateRange`):**
> - An unparseable typed commit silently reverts to the last committed value unless you wire `OnParseError` (or `OnStartParseError`/`OnEndParseError`) **and** render the message somewhere with a live region — the picker itself has no way to announce the failure. `EditDate`/`EditDateRange` already do this for you via `ParsingErrorMessage`.
> - `InputLabel`/`StartInputLabel`/`EndInputLabel` render as the input's `aria-label` and therefore **override** any `<label for>` a consumer supplies — set them to the same text as your visible label (WCAG 2.5.3 Label in Name) rather than leaving the default ("Date"/"Start date"/"End date") in place alongside a differently-worded visible label.
> - Under a right-to-left UI culture (`CultureInfo.CurrentUICulture`), every grid's physical `ArrowLeft`/`ArrowRight` follows the *visual* direction instead of the logical one — physical Right steps to the previous unit in the mirrored layout, the APG rule for horizontal arrows under RTL. Vertical arrows, Home/End, and PageUp/PageDown are unaffected (logical moves with no visual handedness). Culture-driven — there's no parameter to opt in or out.
> - `ArrowDown` from the text field (either field on `DateRangePicker`), while the panel is open, moves focus onto the calendar's roving-tabindex cell — JS-dependent; without JS the key is inert and Tab still reaches the grid. This doesn't change the "focus stays on the field on open" combobox model above, only what a subsequent `ArrowDown` does.
- **`Table<TItem>`** — Data table with `Column`/`PropertyColumn`/`ActionColumn`, row selection, paging (pager placement via `PagerPosition` = Top/Bottom/Both and alignment via `PagerAlign`), and column sorting (`Sortable="true"` on a `PropertyColumn` — non-comparable types degrade to non-sortable; or a `SortBy` comparison on any column). Columns may be conditionally rendered (`@if`).
  - `RowDetail` (a `RenderFragment<TItem>`) adds expandable rows: a leading chevron column toggles the template as a full-width row beneath each row (e.g. a nested child `Table`); expansion is keyed by `RowKey` identity so it survives paging/sorting.
  - `Column.TitleContent` replaces a plain `Title` with templated header content (e.g. a title plus a `LabelTooltip` info icon).
  - `Loading`, `IsRowSelectable`, `SelectionMode.Single`, controlled expansion (`ExpandedRowKeys`/`OnExpand`), `ExpandRowByClick`/`OnRowClick`, `Column.Ellipsis`, `EmptyContent`, `FooterContent`, column filtering (`Column.FilterOptions`/`OnFilter`, `Table.OnFilterChanged`), and `ScrollY` (a scrollable body with a sticky header) round out AntD 4.x parity. See [Table parity features](#table-parity-features-tabletitem).
- **`Tabs`/`Tab`** — Underline tab strip with an optional bordered count chip per tab (`Count`); bind with `@bind-ActiveKey` (a `string?`).
  - Tabs with `ChildContent` show the active pane below the strip; content-less tabs act as a bare filter strip.
  - ARIA tabs pattern with automatic activation (arrows move + select with wrapping, roving tabindex; Home/End deliberately unhandled — Blazor can't `preventDefault` per key). Under a right-to-left UI culture, `ArrowRight` selects the previous tab and `ArrowLeft` the next instead — the strip mirrors under RTL, so the physical Right arrow has to move focus to the tab now on its visual right (same physical-vs-visual swap the pickers use — see the picker accessibility notes above). The tabpanel itself is **unconditionally** a tab stop (`tabindex="0"`), so a text-only pane with nothing else focusable is still keyboard-reachable; the trade-off is one extra Tab stop when a pane's own first content is already focusable — kept deliberately for simplicity and because it's the only way to make a text-only pane reachable at all (APG-sanctioned).
  - `TabBarExtraContent` adds a right-aligned strip slot, `Centered` centers the tab buttons, and `Type="TabsType.Card"` switches to AntD's boxed card-style tabs (CSS-only).
- **`SearchInput`** — Search field: optional leading addon label chip (`AddonLabel`/`AddonContent`), text input (`type="search"`, `@bind-Value`, per-keystroke), and a search button — `OnSearch` fires on Enter and on the button. Pill-rounded ends by default (`--wss-search-radius` to square them).
  - `Loading` swaps the button's search glyph for a spinning `LoadingOutlined` icon and sets `disabled` + `aria-busy="true"` on the button (the text input itself stays enabled); Enter and the button both no-op while `Loading` is true. The button keeps an accessible name (`SearchButtonLabel`) while `Loading`, even when `EnterButtonText` is set — otherwise its visible text is briefly replaced by an `aria-hidden` spinner with nothing left to name it.
  - `AllowClear` adds a clear × button; `EnterButtonText` swaps the icon-only button for a labeled primary button (AntD's `enterButton="Search"`).
  - The input's accessible name resolves `InputLabel` → non-empty `AddonLabel` → `Placeholder` (only when there's no `AddonContent` template) → `SearchButtonLabel` as a guaranteed last-resort floor, so a bare `<SearchInput />` (no `InputLabel`/`AddonLabel`/`AddonContent`/`Placeholder`) is never left nameless. The one exception is an `AddonContent` template with no other naming source: there, `aria-labelledby` points at the addon chip instead of falling through to `SearchButtonLabel`. The addon chip always gets a stable id (generated even when `Id` is unset) so the `aria-labelledby` path works regardless of whether you set `Id`.
- **Toasts & notifications** — two paths with identical rendering: **scoped/Server-safe** (`IMessageService`/`INotificationService` via `builder.Services.AddWssControlsToasts()` + `<MessageContainer />`/`<NotificationContainer />`), or **registration-free static for single-user hosts** (`WasmMessageService`/`WasmNotificationService` + `<WasmMessageContainer />`/`<WasmNotificationContainer />`).
  - On Blazor Server use the scoped path — the static `Wasm*` services hold process-static state that would bleed across users, and (as of 10.7.0) the `Wasm*` containers **throw** on the Server renderer (and on `InteractiveAuto`'s server phase) rather than leaking silently. WebAssembly, Blazor Hybrid (`BlazorWebView`), and the static prerender pass are all permitted — Hybrid runs outside the browser but serves exactly one user per process, which is what makes the static safe.
  - The notification containers accept `Placement` (`TopRight` default/`TopLeft`/`BottomRight`/`BottomLeft`) — set per container instance (render-tree-scoped, MFE-safe), not on the service.
  - On both services every `Success`/`Info`/`Warning`/`Error` (and `Loading`, on messages) returns the toast's `Guid` — pass it to `Remove(id)` to dismiss a sticky (`Duration=0`) toast when the work it announced completes.
  - Both services also expose `Pause(Guid id)`/`Resume(Guid id)` (WCAG 2.2.1): `Pause` cancels a toast's auto-dismiss countdown without removing it, `Resume` restarts it from a fresh full duration (not the time remaining when paused). A no-op for a sticky toast or an id no longer tracked. The containers already wire this to hover and keyboard focus, so most consumers never call it directly.
  - Message toasts now render a close button (matching notifications) — including sticky `Loading` toasts, which previously could only be dismissed via the id returned from `Loading()`. Both toast types announce their severity ("Success: "/"Info: "/"Warning: "/"Error: "/"Loading: ") to assistive tech via a visually-hidden span before the content.
  - `CloseButtonLabel` (`string`, default "Close") and `SeverityLabel` (`Func<MessageType, string>?` on the message containers / `Func<NotificationType, string>?` on the notification ones; null default keeps the built-in English words) localize each toast's close button and severity word — set per **container instance** (render-tree-scoped, MFE-safe), not on the service; a `SeverityLabel` callback returns just the word, the component still appends the trailing `": "` separator.
  - Each toast's close button carries `aria-describedby` pointing at that toast's own content/message element (a stable per-item id), so a screen reader tabbing through several stacked toasts hears which one a bare "Close" belongs to instead of an indistinguishable "Close" repeated for every toast.
- **Hover tooltips (`data-tooltip`)** - not a component: a `data-tooltip="..."` attribute on any **focusable** element (a button, a link, or one you give `tabindex="0"`), styled by `wss-controls.css` (arrow + bubble, slide-in animation, keyboard-focus support). Pair with `wss-tooltip.js` for cursor-aware auto-placement and for a real accessible description + Escape-dismiss — see below.

> `Icon`, `Button`, `Checkbox`, and `Tag` are intentionally **not** part of this library.

#### Hover tooltips (`data-tooltip`)

Add `data-tooltip="Some help text"` to any **focusable** element (a button, a link, or one you give `tabindex="0"`) for a styled hover/focus tooltip — never the native `title` attribute, so every tooltip in the app gets consistent styling. A non-focusable trigger (a bare `<span>`/`<div>`) can still show the CSS bubble on mouse hover, but the accessible-description mechanism below attaches on `mouseover`/`focusin` only, so it never engages and the trigger stays unreachable to keyboard and screen-reader users. `data-tooltip` gives an element only a *description*, never an accessible *name* — an icon-only trigger still needs its own `aria-label`:

```razor
<button data-tooltip="Refresh the list" aria-label="Refresh the list">
    <RefreshIcon />
</button>
```

CSS alone renders it below the element with a slide-in animation, an arrow, and `:focus-visible` support (keyboard users get it too); an invisible bridge spans the gap between the trigger and the bubble so moving the pointer toward the bubble doesn't drop `:hover` and hide it first (WCAG 1.4.13 "hoverable" — no JS needed for this part). That no-JS floor still has no accessible description and can't be dismissed with Escape. Link `wss-tooltip.js` — now recommended for accessibility, not just placement — for automatic placement (it flips above when the element sits in the lower part of its container, and shifts left/right near a side edge, so authors never have to pick a direction by hand) **and** for the two things pure CSS can't provide: a real accessible description and an Escape dismissal:

```html
<script src="_content/WssBlazorControls/wss-tooltip.js"></script>
```

With the script linked, while a tooltip is engaged (hover or focus) its text is mirrored into one shared visually-hidden `role="tooltip"` node and the trigger's `aria-describedby` is pointed at it (appended to, and later restored around, any `aria-describedby` the trigger already had) — including on touch, where the visual bubble itself stays suppressed (WCAG 4.1.2/1.1.1). Escape dismisses the bubble via the `wss-tooltip-dismissed` class without moving focus (WCAG 1.4.13), re-arming the next time the pointer or focus leaves the trigger; a second Escape (nothing left to dismiss) still bubbles to close an enclosing `Modal`/`Drawer`.

It re-derives placement on every hover/focus (via event delegation, so dynamically-added elements are covered with no extra wiring) and aims at the nearest clipping ancestor or recognized panel boundary (`wss-modal` / `wss-drawer` / `wss-popover`) instead of the screen — so a tooltip inside a `Modal` stays within the modal instead of running past its edges. Two deliberate limits on what counts as that frame: `<body>` is never accepted, however it's styled (`body { overflow-x: hidden }` is near-ubiquitous boilerplate, and body's rect is the whole *document* — as tall as the page, top well above the viewport once scrolled — which would answer the flip test against the document rather than the screen), and whatever frame *is* chosen is intersected with the viewport, since only its visible part can hold the bubble. A page that genuinely wants a body-sized frame gets the viewport, which is the same box for an unscrolled page. To force a specific direction yourself (and opt that element out of auto-placement), apply one of the placement classes directly: `wss-tooltip-top`, `wss-tooltip-left`, `wss-tooltip-right`, or the vertically-centered `wss-tooltip-side-left` / `wss-tooltip-side-right` (manual-only — the auto-placer never assigns these two). Tooltips are hidden entirely on touch devices (`hover: none`), since there is no hover to trigger them — the accessible description above is still exposed on touch, but only for a **focusable** trigger: the description mechanism engages on `focusin`, which a non-focusable trigger never receives, touch or not.

The same script also places the form controls' `LabelTooltip` popover (the label help icon), using the same placement classes — that's the one shared placement engine for both tooltip kinds. `LabelTooltip` lazily imports the module itself on first render, so the script tag above is only needed for `data-tooltip` usage; the module guards against being loaded both ways. `LabelTooltip` is excluded from the accessibility layer above — it already renders its own real `role="tooltip"` element and handles `aria-describedby`/Escape in C#.

Theming uses the same `--wss-*` tokens as the rest of the kit (`--wss-color-bg`, `--wss-color-text`, `--wss-color-border`, `--wss-radius`, `--wss-shadow`), plus two tooltip-specific knobs: `--wss-tooltip-gap` (resting distance from the element to the pointer tip, default `24px`) and `--wss-tooltip-z-index` (default `10000`, matching `--edit-tooltip-z-index`).

#### Pill filter variant (`Select` / `EditSelectSearch`)

`Variant="SelectVariant.Pill"` turns the Select trigger into a fully-rounded outlined filter button that hugs its content — the "All shipments ⌄" pattern. Pair it with `Prefix` for a leading icon, and usually `ShowSearch="false"` / `AllowClear="false"` so it reads as a button. The dropdown gets softer corners, content-driven width, and conveys the current value by the bold/tinted row alone (no checkmark). Behavior is unchanged: keyboard navigation, type-ahead, outside-click and Escape close.

```razor
<Select TValue="string"
        @bind-Value="_shipmentFilter"
        Options="_shipmentOptions"
        Variant="SelectVariant.Pill"
        ShowSearch="false"
        AllowClear="false">
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
- **`FilterOption` (`Func<string, SelectOption<TValue>, bool>?`)** — replaces the default case-insensitive `Label.Contains` match in `RebuildFiltered` when set, including when the search text is empty.
  - Pass `(_, _) => true` to disable client-side filtering entirely for a pure server-driven `OnSearch` flow — every option in `Options` stays visible on the assumption the server already filtered them before reassigning `Options`.
  - Tracked by reference like `Options`/`Values`: reassigning it (even mid-open) re-filters immediately. Prefer a cached/readonly delegate — an inline lambda is a new reference every render and re-filters each parameter set, correct but wasteful against a huge option list.
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
- **Controlled `Open`/`OpenChanged` (`bool`/`EventCallback<bool>`)** — two-way bindable via `@bind-Open`.
  - While `OpenChanged` has a delegate (the controlled case), an externally-changed `Open` routes through the exact same internal open/close path as user interaction, so JS placement, focus, and scroll-into-view all still run; every open/close (external or internal) raises `OpenChanged` back, and an echo of a value the component just raised is recognized and ignored (no re-open/close loop).
  - With no delegate on `OpenChanged` (the default, uncontrolled case) `Open` is inert and `DefaultOpen` alone governs the initial state, exactly as before.
  - **`Disabled` always wins**: an external `Open="true"` is ignored while `Disabled`, and a `Disabled` flip on an already-open dropdown closes it through the same path (`OpenChanged` still fires) — a disabled `Select` can never render its dropdown open, controlled or not.
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
  - The `ShowTotal` span carries `role="status"` by default, announcing the window text on every page/size/total change (WCAG 4.1.3). **`AnnounceTotal` (`bool`, default true)** opts a pager out of announcing while still rendering the text — `Table` sets it false on the *top* pager under `PagerPosition.Both` so the visually-duplicated total isn't announced twice.
- **`PageSizeOptions` (`int[]?`)** — renders a dependency-free native `<select>` size-changer after the next-page button (no `Select<T>`/JS module pulled in). `PageSize`/`PageSizeChanged` are two-way bindable (`@bind-PageSize`) to support it — existing one-way `PageSize="10"` usage with no handler is unaffected.
  - The current `PageSize` is folded into the option list even when absent from `PageSizeOptions`, so the select never shows a mismatched value. Changing the size re-clamps `Current` to keep roughly the same data window in view: new `Current` = the old first-visible item's 0-based index ÷ the new size, + 1.
  - `PageSizeLabelFormat` (default `"{0} / page"`) and `PageSizeSelectLabel` (accessible name, default "Page size") localize it.
  ```razor
  <Pagination Total="95" @bind-PageSize="_pageSize" @bind-Current="_page" PageSizeOptions="@(new[] { 10, 20, 50 })" />
  ```
- **`ShowQuickJumper` (`bool`)** — adds a "Go to [ ]" native text input after the size-changer (or the next-page button, if no size-changer). Enter commits the typed page number (clamped to `[1, PageCount]`) via `CurrentChanged` and clears the input. `QuickJumperLabel` (default "Go to") and `QuickJumperInputLabel` (accessible name, default "Go to page") localize it.
- **`Small` (`bool`)** — AntD's compact pagination size: smaller buttons and tighter spacing, CSS-only (`wss-pagination-sm`).

#### Table parity features (`Table<TItem>`)

All additive — existing markup is unchanged when these parameters go unused.

- **`Loading` (`bool`)** — shows a translucent mask + spinner over the whole component: both pagers plus the table body (rows stay rendered beneath it, and the pagers are dimmed and click-inert while the mask is up) — and sets `aria-busy="true"` on the root element. Pure CSS/markup, no JS. Row click/`ExpandRowByClick` handling itself no-ops while `Loading` (not just losing its tab stop — see the `OnRowClick` keyboard note below), so a synthesized click (Enter's native follow-on) or a programmatic dispatch can't sneak a row activation through mid-refresh either.
  > **Note:** the mask only disables controls the `Table` itself renders (sort/filter/expand/select-all, the embedded pagers, the clickable row) — it cannot reach into a consumer's own `ActionColumn`/`Column` template, so a keyboard user can still Tab to and activate a consumer button under the mask. Disable those buttons on the same `Loading` flag yourself if that matters.
- **`IsRowSelectable` (`Func<TItem, bool>?`)** — per-row selection predicate; null (default) means every row is selectable. A rejected row's checkbox/radio renders `disabled` and is excluded from the header "select all" — both which rows it toggles and the indeterminate/all-selected math count only selectable rows on the page. The header checkbox itself renders `disabled` only once `IsRowSelectable` rejects every row on the page (never when `IsRowSelectable` is unset).
- **`SelectionMode` (`Multiple` default / `Single`)** — `Single` renders radio-semantics selection instead of the checkbox column (one native `<input type="radio">` per row, all sharing one group so picking a row deselects any other) and an empty header cell in place of "select all" (kept only for column alignment — there's no "select all" for an exclusive choice). `SelectedItems`/`SelectedItemsChanged` are unchanged either way (0-or-1 items in `Single` mode).
- **Controlled expansion: `ExpandedRowKeys`/`ExpandedRowKeysChanged`** — layers over the existing uncontrolled expansion set (keyed by `RowKey`); reassign a new collection to drive expansion from the parent, same immutable-parameter contract as `SelectedItems`. **`OnExpand`** (`EventCallback<(TItem Item, bool Expanded)>`) raises on every toggle regardless of control mode.
- **`ExpandRowByClick` (`bool`)** — clicking anywhere on a row (other than the selection checkbox/radio, the expand chevron, or inside an `ActionColumn` cell — all of which stop propagation) toggles that row's `RowDetail` expansion, the same toggle the chevron performs.
- **`OnRowClick` (`EventCallback<TItem>`)** — raised on a row click with the same propagation guards as `ExpandRowByClick`. Always raised regardless of `ExpandRowByClick`; when both are set, a click toggles expansion *and* raises `OnRowClick`.
  > **Note:** those propagation guards only cover **clicks**, and only on the selection checkbox/radio cell, the expand chevron cell, and `ActionColumn` cells. Interactive content placed in a plain `Column`'s `ChildContent` does **not** get an automatic click guard — its clicks bubble up and reach `OnRowClick`/`ExpandRowByClick` like any other cell click. Put row-action buttons in `ActionColumn`, or add `@onclick:stopPropagation="true"` yourself, when mixing interactive content into a plain `Column` alongside `OnRowClick`/`ExpandRowByClick`. This restriction is click-only — the **keydown** path guards every cell unconditionally (see the Keyboard note below), so Enter on the same interactive content fires `OnRowClick`/`ExpandRowByClick` exactly once with no `@onkeydown:stopPropagation` needed anywhere.
  - **Keyboard:** wiring `OnRowClick` makes every row a tab stop (`tabindex="0"`) that raises it on **Enter** — the same handler a click runs, including the `ExpandRowByClick` toggle. Every cell's keydown (not just the guarded selection/expand/`ActionColumn` cells above) stops propagation before it reaches the row, so Enter on a button/link inside a *plain* `Column` fires this exactly once — previously it could double-fire, since Enter on a nested control dispatches a keydown **and** a synthesized click, and only the synthesized click was guarded per-column. Space is deliberately not an activation key (suppressing its page-scroll would need `@onkeydown:preventDefault`, which Blazor applies to every keydown on the element and would trap Tab in the row too); rows stay rows (no `role="button"`). While `Table.Loading` is true, rows drop their tab stop and Enter handler (and `OnRowClickedAsync` itself no-ops too — see the `Loading` bullet above), matching the mask's pointer inertness.
- **`AriaLabel` (`string?`)** — accessible name for the `<table>` itself when it has no visible `Caption`; ignored when `Caption` is set (a caption already names the table, and an `aria-label` would silently override the text a sighted user can read).
- **`ScrollRegionLabel` (`string`, default "Table content")** — accessible-name fallback for the `ScrollY` scroll region when neither `Caption` nor `AriaLabel` names the table (name precedence: `Caption` → `AriaLabel` → `ScrollRegionLabel`).
- **`LoadingLabel` (`string`, default "Loading")** — text a persistent visually-hidden `role="status"` region announces while `Loading` is true (and the empty-rows placeholder text when there are no rows) — the mask and its spinner are themselves hidden from assistive tech.
- **`SelectRowLabelFor` (`Func<TItem, string>?`)** — per-row accessible name for the selection checkbox/radio (e.g. `x => $"Select {x.Name}"`), so a screen reader running the selection column doesn't hear the same "Select row" on every row. Falls back to `SelectRowLabel` when unset or when it returns null.
- **`FilterAppliedButtonLabelFormat` (`string`, default "Filter {0} (filter applied)")** / **`FilterAppliedLabel` (`string`, default "Filter (filter applied)")** — accessible name for a column's filter button while that column actually has a filter applied, since the active state is otherwise conveyed only by recoloring the funnel glyph (a visual-only cue). The filter trigger also carries `aria-haspopup="dialog"`, matching every other popup trigger in the kit.
- **`Column.Ellipsis` (`bool`, on the `Column<TItem>` base)** — truncates overflowing cell text with an ellipsis (CSS-only: `white-space: nowrap; overflow: hidden; text-overflow: ellipsis`).
  - Since AntD's ellipsis needs a bounded column width to actually clip, the `Table` switches to `table-layout: fixed` automatically once ≥1 column requests it (untouched tables keep the existing auto layout).
  - `PropertyColumn` additionally wraps its computed text in a `title`-bearing `<span>` so the truncated value stays discoverable on hover; a custom `Column`/`ActionColumn`'s `ChildContent` is arbitrary markup, not a string the base class computed, so it gets the truncation styling only, no `title`.
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
- **Column filtering: `Column.FilterOptions`/`OnFilter`/`FilterMultiple`** — a funnel icon appears in the header cell (after the sort control, when both are present — clicking it never triggers a sort) whenever a column sets `FilterOptions` (`IReadOnlyList<TableFilterOption>`, each a `Text`/`Value` pair) **and** `OnFilter` (`Func<TItem, string, bool>`, given one selected value); either alone renders no filter UI.
  - A sortable + filterable header stays inside its cell even when the column is narrow (`table-layout: fixed`, e.g. via `Ellipsis` elsewhere in the table) — the sort label truncates with an ellipsis instead of pushing the filter button out.
  - Clicking the funnel opens a checkbox dropdown (`FilterMultiple` true, the default) or a single-select radio dropdown (`FilterMultiple` false); **OK** applies the checked/selected values and closes, **Reset** clears that column's filter immediately, and clicking outside the dropdown closes it *without* applying whatever was checked (AntD only applies on OK) — neither OK nor Reset resets the current page when the applied selection doesn't actually change (e.g. OK with nothing (re-)ticked, or Reset on an already-empty filter).
  - A row passes a column's filter when `OnFilter` returns true for **any** of its selected values (OR within a column); a row must pass **every** filterable column to render (AND across columns). Filtering runs before sorting and paging, and — like paging — a selected row that a filter narrows out of view stays in `SelectedItems`, it just isn't rendered.
  - Client-side only: like `Sortable`, `FilterOptions`/`OnFilter` narrow whatever's currently in `DataSource` — under the server-paging compose pattern below, filtering server-side is on you (send the selected values in your own request).
  - This is uncontrolled filter state only (no `filteredValue`-style fully-controlled equivalent): observe changes via **`Table.OnFilterChanged`** (`EventCallback<(Column<TItem> Column, IReadOnlyList<string> SelectedValues)>`), raised after every apply/reset that actually changes the applied selection, and also when a column that was actively filtering rows drops out of the rendered set (e.g. an `@if` hiding it) — its filter is force-cleared along with it, so the same "now empty" payload fires there too. Not raised on open, an outside-click/Escape discard, or a no-op OK/Reset.
  - `FilterButtonLabelFormat` (default `"Filter {0}"`), `FilterResetLabel` (`"Reset"`), and `FilterOkLabel` (`"OK"`) localize the button/dropdown text; a column with no `Title` names its filter button from `Table.FilterLabel` (default `"Filter"`), the parallel of `SortLabel`.
  ```razor
  <Table TItem="Row" DataSource="_rows" OnFilterChanged="@(e => Console.WriteLine($"{e.Column.HeaderText}: {string.Join(", ", e.SelectedValues)}"))">
      <PropertyColumn TItem="Row" TProp="string" Title="Name" Property="@(r => r.Name)"
                      FilterOptions="@(new[] { new TableFilterOption("Alice", "Alice"), new TableFilterOption("Bob", "Bob") })"
                      OnFilter="@((Row r, string v) => r.Name == v)" />
  </Table>
  ```
- **`ScrollY` (`string?`)** — bounds the table body to a fixed height with its own vertical scrollbar and a sticky header (AntD's `scroll.y` equivalent): any CSS length (`"320px"`). Null (default) renders the existing unconstrained wrapper.
  - Deliberately scoped to the table's own wrapper only — a header that stays fixed while the whole *page* scrolls (viewport-level sticky) is out of scope.
  - A column filter dropdown that would otherwise be clipped by the `ScrollY` wrapper's overflow escapes it via `position: fixed` (JS), keeps tracking its trigger across page scroll/resize while it stays open, and paints above `Loading`'s mask even while both are active together; without JS it stays CSS-anchored (may clip in that combination — the documented no-JS fallback).
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
- **Programmatic focus**: `FocusAsync()` on a control held by `@ref`, or the `FocusOnFirstRender` parameter — see [Programmatic focus](#programmatic-focus-focusasync--focusonfirstrender)

> **`HidingMode.WhenNull`/`WhenNullOrDefault` apply in edit mode too**, unlike their `WhenReadOnly*` siblings — so pairing either with a field the user can empty from inside the control (`EditDateRange` with `AllowClear`, a nullable-bound `EditNumber<int?>`/`EditDate<T?>`, or `EditString`'s `AllowClear` under `WhenNullOrDefault`, which clears to `""`) **unmounts the control the moment it's cleared**, taking the only way to put a value back with it. That is the intended reading of the mode — the rule is about the value, and it behaves the same however the value got emptied — but for the usual "hide empty optional fields on a detail view" goal, reach for `WhenReadOnlyAndNull`/`WhenReadOnlyAndNullOrDefault` instead.

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
    <FluentValidationValidator /> @* Blazored.FluentValidation *@
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
<EditRadioString @bind-Value="model.Department" Options="@departments" />

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

On `EditRadioString`/`EditRadioEnum<TEnum>`, disabling the option that's *currently selected* renders `aria-disabled="true"` instead of the native `disabled` attribute — a checked-but-natively-disabled radio would leave the group with no fallback native tab stop at all. It's still logically inert (the commit path rejects it the same as any other disabled option); only the tab stop is preserved. Every other (unselected) disabled option still renders native `disabled`.

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

- **MIME types and wildcards.** `AllowedExtensions` now accepts three token shapes, mirroring the native `<input accept>`/Ant Design's `accept`: a bare/dotted extension (`"pdf"`/`".pdf"`, the original behavior), a full MIME type (`"application/pdf"`), or a MIME wildcard (`"image/*"`) — detected by whether the token contains `/`.
  - Both the `<InputFile accept="...">` attribute and the validation logic honor all three; MIME matching reads `IBrowserFile.ContentType` (the browser-reported type) case-insensitively, not the file extension.
  - Previously every token was dot-prefixed regardless of shape, so a MIME token like `"image/*"` became the meaningless `.image/*"` — silently rejecting every file and emitting an invalid `accept` attribute.
- **`BeforeAdd`** (`Func<IBrowserFile, Task<bool>>?`) — an optional async gate run once per file, after the built-in format/size/count/duplicate checks and before its bytes are buffered.
  - Return `false` to reject the file; the rejection is reported the same way as the built-in checks, via the new `BeforeAddRejectedMessageFormat` (`"{0} was rejected."` by default, `{0}` = file name).
  - Use it for checks the cheap built-in ones can't do — a server-side dedupe lookup, content sniffing beyond the extension/MIME check. An exception thrown by the hook propagates uncaught: that's a bug in the consumer's code, not a file rejection, so it's never swallowed into an upload-error message.
- **File size in the list.** Every selected file's row (both the edit-mode removable list and the read-only list) now shows its formatted size (the same `"10 MB"`/`"512 KB"`/`"900 B"` formatting the size-cap messages already used) in a muted span next to the file name. The empty-list state (no files selected) is unaffected — this only adds markup to rows that already exist once files are present.
- **`Variant="EditFileVariant.Button"`** (`EditFileVariant`: `Dropzone` default/`Button`) — swaps the dashed drag-and-drop card for a compact plain button (Ant Design's plain `Upload`, as opposed to `Upload.Dragger`), sized and styled like a normal button rather than a full-width dropzone card. `ButtonText` (`string`, default `"Select Files"`) sets its label.
  - Built on the same invisible-`<InputFile>`-overlay technique as the dropzone, so keyboard/focus/click behavior match — Tab reaches the real file input, Enter/Space opens the file picker, and it unmounts at the `MaxFiles` cap exactly like the dropzone does. Drag-and-drop is intentionally not supported in this variant, matching Ant Design's plain `Upload`.
  - All validation, caps, and messages apply identically to both variants; `Dropzone` (the default, unset `Variant`) renders byte-identical markup to before. The resolved caps hint (see the live status/caps bullet above) renders for `Button` too, on its own line right below the button — `Button` has no dashed-card instructional-text block to fold it into, but the WCAG 3.3.2 up-front-limits requirement doesn't depend on which `Variant` is chosen. Only the `Dropzone`-only "Supported formats" line stays dropzone-only (pre-existing, out of this fix's scope).
- **`Bordered`** (`bool`, default `false`) — wraps the label and the picker/file-list together in one bordered, padded card (`edit-file-card`), a field-container look some consuming design systems use around an upload field (distinct from the dashed drop-zone card `Variant` already draws). Default `false` renders the existing unboxed layout, byte-identical to before this parameter existed.
- **`AllowDownload`** (`bool`, default `false`) — each selected file's name renders as a clickable link (`edit-file-name-link`, colored via `--edit-color-primary`) instead of plain text, in both the edit-mode removable list and the read-only list. Clicking re-saves that file's already-buffered bytes back to the user's machine (a `Blob` + a temporary `<a download>`, not a network fetch) — useful for letting a user reopen a file they just picked, or Ant Design's own already-uploaded-file link look for a file shown in a read-only view.
  - `Bordered` and `AllowDownload` combine to match Ant Design's `Upload` `defaultFileList` pattern for a file that already exists (`status: 'done'`, a `url`) — one bordered field card with the file name as a link, no dropzone once `MaxFiles` is reached:
    ```razor
    <EditFile @bind-Value="model.Terms" Label="Terms & Conditions" MaxFiles="1" Bordered="true" AllowDownload="true" />
    ```
- **Live status and up-front caps (accessibility).** The drop zone states the resolved caps alongside "Supported formats": per-file size, aggregate size, and — only when finite (`MaxFiles > 0`) — the max file count. `Variant="EditFileVariant.Button"` renders the same caps line on its own row right below the button (it has no dashed-card text block to fold it into, but the up-front-limits requirement is the same regardless of `Variant`); only the `Dropzone`-only "Supported formats" line has no `Button` equivalent. A polite `role="status"` region reads `LoadingStatusText` (default `"Loading files…"`) while a batch is being validated/buffered, then switches to the current selection once it settles: `NoFilesSelectedStatusText` (default `"No files selected."`) when empty, else `FilesSelectedStatusFormat` (default `"{0} {2} selected: {1}."`, `{0}` = count, `{1}` = comma-joined names, `{2}` = `"file"`/`"files"`) — covering additions, removals, and a partial success (some files accepted, others deduped/capped/rejected) uniformly.

### Read-only views (`EditString`)

In read-only mode — `IsEditMode="false"`, or a form-wide `FormOptions.IsEditMode` — `EditString` picks one of three views, in this order:

1. **Masked row** — a `MaskText`, or the bullet mask an `IsPassword` / `[DataType(DataType.Password)]` field supplies on its own. Shows the (partly) hidden value next to an eye toggle that reveals it.
2. **Link** — when `Url` is set *and* clears the sanitization gate below.
3. **Plain text** — everything else.

The order is the contract. **`MaskText` beats `Url`**, so a masked value is never published as link text — and because a password field masks itself, a secret field never renders as a link either, even with a `Url` set. An empty bound value falls through both: an `<a>` with no text is invisible, unannounceable and still clickable, and a masked row with nothing in it is an eye toggle that reveals nothing.

```razor
@* Single-character mask: repeats to cover the whole value -- "123-45-6789" -> "***********" *@
<EditString @bind-Value="model.Ssn" IsEditMode="false" MaskText="*" />

@* Multi-character mask: a PREFIX. It covers the head, and the tail it doesn't cover still shows --
   "123-45-6789" -> "***-45-6789". A mask at least as long as the value renders the mask alone. *@
<EditString @bind-Value="model.Ssn" IsEditMode="false" MaskText="***" />

@* A password field masks itself here -- bullets, never the plaintext secret *@
<EditString @bind-Value="model.Secret" IsPassword="true" IsEditMode="false" />
```

Mask widths count *visible* characters, not UTF-16 code units: a single-character mask spends one glyph per grapheme cluster (an emoji costs one `*`, not two), and a multi-character mask's cut never splits a surrogate pair.

**`Url` is sanitized before it reaches `href`.** It is first preprocessed the way a browser preprocesses an href — leading/trailing C0-control-or-space trimmed, then embedded ASCII tab/CR/LF stripped — *before* the scheme is examined, so a leading control byte or an embedded tab can't hide a `javascript:` scheme from the check. Only `http`, `https`, `mailto`, or a **same-origin relative** (schemeless) URL is allowed through. Anything else renders no `<a>` at all and falls back to plain read-only text: a disallowed scheme (`javascript:`, `data:`, …), a protocol-relative URL (`//host/path`, plus the backslash spellings browsers normalize to it), or a URL that preprocessing empties out. This matters most when the URL comes from model data rather than markup.

**`UrlTarget` hardens `rel` automatically.** Any target that can hand the opened document a `window.opener` handle back to this page gets `rel="noopener noreferrer"` — which means **named targets too** (`UrlTarget="vendor"`), not only `_blank`. Named targets are in fact the case that most needs it: browsers already imply `noopener` for `_blank`, but not for a named context. Only the same-context keywords render without a `rel` — `_self`, `_parent`, `_top` (matched case-insensitively) and no target at all — since there is no opener to sever there, and dropping the referrer on a navigation inside our own frame tree would be gratuitous. `_blank` alone also appends a visually-hidden "(opens in new tab)" inside the link, which joins its accessible name; a named target may reuse a context that is already open, so the control can't make that claim for it.

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

Honored by:
- `EditString`, `EditTextArea`, `EditNumber<T>` (the rendered `placeholder` attribute).
- `EditDate<T>` (forwarded to the inner picker, falling through to its own mode-derived default, e.g. "Select date", when nothing resolves).
- `EditDateRange`'s `StartPlaceholder`/`EndPlaceholder` (each resolves independently against its own bound property's attributes — a `[Placeholder]` on `Start` never leaks onto `End`, and vice versa).
- `EditSelectSearch<TValue>`/`EditMultiSelect<TValue>` (shown only while nothing is selected, falling back to the literal "Please select").

`EditSelectEnum<TEnum>` and `EditSelectString<TValue>` render a native `<select>`, which has no `placeholder` attribute — the model text instead goes on the leading blank option (when one renders) and on a hidden "unmatched value" option that supplies the closed select's own displayed text. Two caveats: on `EditSelectString`, an explicit `NullOptionText="null"` still suppresses the leading option entirely — a model attribute never resurrects an option the consumer deliberately turned off — and on a **non-nullable enum** whose current value is already a defined member, no blank option renders at all, so there is nothing for the model's placeholder text to display.

Deliberately not wired:
- `EditDateNative<T>` (browsers ignore `placeholder` on native `date`/`time` inputs).
- The `EditRadio*` "Other" free-text box (`EditRadioEnum.OtherPlaceholder` describes that sub-input, not the bound property, and `EditRadioString`'s Other box has no placeholder parameter at all).
- `EditFile`, `EditSelect<TValue>`, the checkbox lists, `EditBool*`, `EditDisplay`, and every UI-kit widget (none are model-bound, so there's no attribute to read).

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

Resolution (highest wins), every wired control: the control's own `Min`/`Max` parameter → `[MinValue]`/`[MaxValue]` on the bound property → `[Range]` → none. A markup `Min`/`Max` still overrides the model whenever one particular instance needs different bounds. On the `[Range]` fallback, a bound spelling "no bound" is treated as unbounded rather than clamped, thrown, or rendered: anything unrepresentable as `decimal` (the ubiquitous `[Range(0, double.MaxValue)]` idiom), and the integer-typed spellings of the same idiom (`int`/`long`/`decimal` extremes, e.g. `[Range(int.MinValue, 100)]`) — the very sentinels the library's validation-message rewrite presents as one-sided ("Cannot exceed 100"). One shared predicate decides it for both layers, so the rendered bound and the message can never disagree. The narrower integer extremes (`sbyte`/`byte`/`short`/`ushort`/`uint`/`ulong`) are deliberately **not** sentinels on either layer: `255`, `32767`, `127`, `-128` are overwhelmingly real bounds (`[Range(1, 255)]` on an `int Quantity`) — so `[Range(1, 255)]` renders `max="255"` *and* says "Must be between 1 and 255", at the price of a genuinely vacuous `[Range(0, 255)]` on a `byte` naming both bounds too. The recognized extremes *are* checked against the bound property's own type for reachability, though: `[Range(0, int.MaxValue)]` on a `short` suppresses the ceiling (the type can't reach it), while the same annotation on a `long` keeps it as a real constraint. An explicit `[MinValue]`/`[MaxValue]` is never sentinel-suppressed — those are one-sided by design, so whatever you write there is intentional and renders. An unparseable or otherwise misconfigured bound degrades gracefully — no rendered bound, no validation error, never a render-time exception.

Honored by:
- `EditNumber<T>` (the rendered `min`/`max` attributes).
- `EditDate<T>` (forwarded to the inner picker, date-granularity, ignored in `Time` mode, same as its own `Min`/`Max` parameters).
- `EditDateNative<T>` (new `Min`/`Max` parameters as of 10.7.0 — its first bounds support ever — rendering the native input's own `min`/`max` formatted to match its `Type`, also omitted in `Time` mode).
- `EditDateRange` — both bounds drive the ONE calendar its two inputs share, so each resolves param → the *looser* of the two fields' own attributes: `Min` takes the earlier minimum, `Max` the later maximum, each falling back to whichever single field declares one. A natural `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` annotation works as-is, a single `[Range(typeof(DateTime), ...)]` on `Start` alone supplies both ends, and two conflicting bounds never leave the shared calendar tighter than either field's own annotation. The result is the convex **hull**, not the union: with `[Range(2024-03-01 .. 2024-03-31)]` on `Start` and `[Range(2024-09-01 .. 2024-09-30)]` on `End` the calendar offers 2024-06-15, which neither field accepts — one calendar has exactly one min and one max, and the annotations still reject the pick at validation time.

Deliberately not wired:
- `EditString`/`EditTextArea` (length limits already come from `[StringLength]`/`[MaxLength]`, a different axis).
- The select/radio/checkbox-list controls, `EditBool*`, `EditFile`, `EditDisplay`, and every UI-kit widget (none are model-bound, so there's no attribute to read).
- `DatePicker`/`DateRangePicker` keep their plain `Min`/`Max` parameters with no model to resolve against.

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

- **`[Autocomplete("...")]`** → `EditString.Autocomplete`. Unset, a non-password field first tries to infer a real autofill token from the bound property's own name (`Email`/`FirstName`/`Phone`/`PostalCode`, and about 15 others → `email`/`given-name`/`tel`/`postal-code`, …), falling back to the control's built-in `"one-time-code"` only when the name isn't recognized; a password field (`IsPassword`/`[DataType(Password)]`) instead falls back to `"new-password"`, since `one-time-code` on a real password field makes some mobile browsers offer an OTP keyboard over the password itself.
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

### Color picking (`EditColor` / `ColorPicker`)

```razor
<EditColor @bind-Value="_model.BrandColor" ShowText="true" AllowClear="true" Presets="_swatches" />

@code {
    static readonly IReadOnlyList<string> _swatches = ["#f5222d", "#1890ff", "#52c41a", "rgba(0, 0, 0, 0.35)"];
}
```

**The value contract.** `EditColor` and the standalone `ColorPicker` both bind a plain `string?`.

- **In:** 3-, 4-, 6-, or 8-digit hex, with or without the leading `#`, plus `rgb()`/`rgba()` in the comma, space, and slash spellings, with a numeric or percentage alpha. Out-of-range channels clamp rather than fail — including an infinite one, whether written `Infinity` or reached by an overflowing numeral like `1e400`. A literal `NaN` is the one numeric rejection (nothing to clamp it to).
- **Out:** normalized lowercase `#rrggbb`, extended to `#rrggbbaa` only when the color is translucent **and** `ShowAlpha` is on. `ShowAlpha="false"` therefore *strips* an alpha channel a bound-in value carried.
- **Unusable in:** null, empty, and anything unparseable (a named CSS color like `chartreuse`, an `hsl()` string) all render as "no color" — AntD's white-with-a-red-diagonal empty swatch — rather than throwing. The read-only view falls back to `ReadOnlyValue`'s plain "Not Set" placeholder for the same values. A `[Required]` `string` is still satisfied by unparseable-but-non-empty text; add a `[RegularExpression]` if the exact form matters to your model.
- Only a **typed** entry that fails to parse is an error, surfaced through `ParsingErrorMessage`/`OnParseError`. The RGB row's number boxes clamp or revert silently — a `number` input has no unparseable-text state worth reporting.

**Read-only mode.** Renders the swatch alone by default — the reader sees the actual color instead of decoding hex. `ShowText="true"` adds the normalized hex text beside it, the same swatch-plus-text layout the edit-mode trigger uses.

**Inside the popup.** A saturation (x) / brightness (y) area, a hue slider, an alpha slider (`ShowAlpha`, default on), a HEX/RGB format switch with matching inputs, and an optional `Presets` row. The format switch changes only what the input row *edits* — the bound value is always normalized hex either way. Typed entries commit on Enter or blur.

**Keyboard.** All three tracks are `role="slider"` elements in the tab order:

| Key | Saturation/brightness area | Hue slider | Alpha slider |
|---|---|---|---|
| `←` / `→` | saturation ∓1% | hue ∓1° | opacity ∓1% |
| `↑` / `↓` | brightness ±1% | hue ±1° | opacity ±1% |
| `Shift` + arrow, `PageUp`/`PageDown` | the same, ×10 | ×10 | ×10 |
| `Home` / `End` | saturation 0% / 100% | 0° / 360° | 0% / 100% |

The 2D area's `aria-valuenow` carries saturation, with an `aria-valuetext` naming both axes (no single-axis value can describe a 2D handle) — override its wording with `SaturationValueTextFormat`. `Escape` closes the popup and returns focus to the trigger. The trigger's own accessible name is the field label plus the current value ("Brand Color: #1890ff"), so the value is announced, not just seen.

**Without JavaScript** (static prerender, or a host that can't reach `wss-color.js`): a single click still positions the handle, computed from the click's offset within the track, and the keyboard steps above work with no JS at all. Only *dragging* is lost. That click fallback is the one place the control assumes its default metrics — `MouseEventArgs` reports an offset in pixels but not the element's size — so if you override `--wss-color-picker-width`/`--wss-color-picker-sv-height`, a no-JS click lands proportionally off while the normal (JS) path, which measures the real element, is unaffected.

**Deliberately out of scope**, and not planned: sizes, custom gradient/color-scheme panels, grouped or collapsible preset sections, and AntD's color-picker-inside-an-input variants.

### Programmatic focus (`FocusAsync` / `FocusOnFirstRender`)

Every `Edit*` control exposes **`public ValueTask FocusAsync()`**. Hold the control with `@ref` and call it:

```razor
<EditString @ref="_search" @bind-Value="model.Query" />
<button type="button" @onclick="OpenAsync">Search</button>

@code {
    EditString? _search;

    async Task OpenAsync()
    {
        _isOpen = true;
        await _search!.FocusAsync();
    }
}
```

Each control focuses the element a `Tab` into it would land on:

| Control | Focus target |
|---|---|
| `EditString`, `EditTextArea`, `EditNumber<T>`, `EditDateNative<T>` | its `<input>`/`<textarea>` |
| `EditBool` | its checkbox |
| `EditSelect<T>`, `EditSelectEnum<T>`, `EditSelectString<T>` | its `<select>` |
| `EditRange<T>` | the `role="slider"` track (the tab stop) |
| `EditSelectSearch<T>`, `EditMultiSelect<T>` | the engine's `role="combobox"` search input |
| `EditDate<T>` | the picker's typed-entry input (which opens the calendar, as tabbing in does) |
| `EditDateRange` | the **Start** input — always, never "whichever end was last active" |
| `EditColor` | the swatch trigger button (does **not** open the panel) |
| `EditFile` | the file `<input>` |
| `EditRadio<T>`, `EditRadioEnum<T>`, `EditRadioString`, `EditBoolNullRadio` | the **checked** radio if there is one, else the first enabled radio |
| `EditCheckedEnumList<T>`, `EditCheckedStringList<T>` | the **first enabled** checkbox (each box is its own tab stop, so what's ticked is irrelevant) |

`EditDisplay` has no `FocusAsync` — it binds no field and renders nothing focusable.

**It never throws, and it never parks focus on a control the user can't use.** A control that is `IsDisabled`, read-only (`IsEditMode="false"`, which renders a display value and no editor at all), hidden, or not yet interactive simply doesn't move focus — so you don't have to guard the call against state you can't see (a cascaded `FormOptions.IsEditMode`, a `HidingMode` that just unmounted the field). The same applies once JS is unavailable — see below.

A native `readonly` attribute is **not** one of those states: a read-only `<input>` is still rendered and still a tab stop, so `FocusAsync()` does move focus to it. That's deliberate — read-only fields are meant to be reachable, copyable, and announced.

`EditBool` is the one disabled control that still takes focus, because its `AllowFocusWhenDisabled` (default `true`) deliberately keeps the checkbox in the tab order while disabled — the discoverable-but-inoperable pattern. The rule is "focus can go anywhere the user could `Tab` to"; set `AllowFocusWhenDisabled="false"` and it behaves like every other disabled control.

**Inside a `Modal` or `Drawer` it just works.** Those gate their children on visibility, so a child's first render *is* the open, and the overlay's own "focus the first focusable element" runs in the same cycle. The overlay defers: it only takes initial focus when nothing inside the panel has it, so `FocusOnFirstRender="true"` (or your own `FocusAsync()`) on a field in the dialog wins over the close X.

The UI-kit components the picker/select-backed controls delegate to expose the same method, for use outside a form: **`Select<T>`**, **`DatePicker`**, **`DateRangePicker`**, and **`ColorPicker`**.

**`FocusOnFirstRender`** (`bool`, default `false`) is the declarative form — the control focuses itself once, after its **first** render:

```razor
<EditString @bind-Value="model.Query" FocusOnFirstRender="true" />
```

Setting it `true` later at runtime does not focus the control; that's a state change, and `FocusAsync()` is the call for it. The standard Blazor SSR caveat applies: focus is a DOM operation, so under static SSR — or during the prerender pass of an interactive render mode — nothing happens at prerender time and the focus lands on the first *interactive* render. If you need the browser to do it from server-rendered HTML alone, the native `autofocus` attribute splats through onto the editor like any other unmatched attribute — and the two are independent, so you can use either or both:

```razor
<EditString @bind-Value="model.Query" autofocus />
```

> The parameter is spelled `FocusOnFirstRender`, not `AutoFocus`, on purpose. Blazor matches component parameter names **case-insensitively**, so a parameter named `AutoFocus` would capture a native `autofocus` attribute instead of letting it through — `<EditString autofocus />` would silently stop reaching the DOM, and `autofocus="autofocus"` would fail to compile.

**JavaScript dependency.** The single-element controls use `ElementReference.FocusAsync()` and need nothing from this package's scripts. The four radio groups and the two checked lists are the exception: their per-option `<input>`s are rendered by Microsoft's `InputRadio` (and, for `EditRadio`, by your own markup), so no element reference can be captured and no id computed — they resolve the option inside the group at focus time via `edit-controls.js`, with the same lazy-import fallback the rest of `JsInteropEc` uses. Without that script reachable, focus simply doesn't move.

**`JsInteropEc.FocusById` is still the answer** for focusing a control you don't hold a reference to — a field owned by a different component, or one reached only by id:

```razor
<EditString Id="customer-email" @bind-Value="model.Email" />
@code {
    [Inject] IJSRuntime JS { get; set; } = default!;
    [CascadingParameter] FormDefaults? FormDefaults { get; set; }

    Task FocusEmail() => JsInteropEc.FocusById(JS, "customer-email", FormDefaults);
}
```

## Styling and Customization

The library provides default styling through the included CSS file. You can customize the appearance by:

1. **Overriding CSS classes** in your own stylesheets
2. **Using ContainerClass** parameter for component-specific styling
3. **Applying custom CSS** to the `.edit-control-wrapper` class

The AntDesign-style UI-kit controls (Alert, Modal, Table, Select, ...) are themed via `--wss-*` CSS custom properties in `wss-controls.css`. They default to the AntDesign 4.x look and **bridge to your existing `--color-primary` / `--color-danger` / `--border-color`** where those are defined, so they pick up your theme automatically. Override any `--wss-*` variable to re-theme.

- `--wss-color-primary` / `--wss-color-error` back **chrome** — borders, focus rings, tints, non-text status glyphs — where WCAG 1.4.11 only requires 3:1 against the surface behind it.
- `--wss-color-primary-strong` (bridging your own `--color-primary-strong`) and `--wss-color-error-strong` (bridging `--color-danger-strong`) back **text-grade** sites instead — white text sitting on a primary/danger fill (dialog buttons, the selected day/month cell, the picker/filter OK buttons), and the primary/danger color used as plain text on a light surface — which WCAG 1.4.3 holds to 4.5:1.
  - If you theme only the base `--color-primary`/`--color-danger`, the `-strong` tokens keep their own accessible default rather than silently inheriting a base color that may not clear 4.5:1; set `--color-primary-strong`/`--color-danger-strong` (or the `--wss-*` tokens directly) to also re-theme the text grade.
  - Hover states on `-strong` fills darken toward black (lightening toward white would drop them back under 4.5:1); the derived shades are overridable via `--wss-color-primary-strong-hover`/`--wss-color-error-strong-hover` (bridging `--color-primary-strong-hover`/`--color-danger-strong-hover`) — if you override them, you own the ratio.
- The unthemed `--wss-color-warning`/`--wss-color-success` defaults are likewise darkened off the plain AntD 4 palette so their status icons clear WCAG 1.4.11 on their own — the `-bg`/`-border` tint tokens beside them are intentionally left alone, since backgrounds carry no contrast floor of their own.
- A small `--wss-color-text-deemphasized` token backs *operable* de-emphasized text (the pickers' outside-month/decade cells) at 4.5:1+, distinct from `--wss-color-placeholder`, which stays reserved for true input placeholders.
- `--wss-color-placeholder` (default `rgba(0, 0, 0, 0.55)`, darkened from an earlier `#bfbfbf` that measured only 1.84:1) and the new `--wss-color-text-secondary-strong` (default `rgba(0, 0, 0, 0.65)`, mirroring `edit-controls.css`'s `--edit-color-text-secondary-strong`) back text-grade secondary content in the kit.

- `ColorPicker`'s own geometry lives in `--wss-color-picker-width` (`234px`), `--wss-color-picker-sv-height` (`140px`), `--wss-color-picker-slider-height` (`10px`), `--wss-color-picker-swatch-size` (`24px`), `--wss-color-picker-radius` (`8px`), and `--wss-color-picker-checker` (`#dedede`, the transparency checkerboard's tint). The first two are mirrored as constants in the component, purely for the no-JavaScript click fallback — see [Color picking](#color-picking-editcolor--colorpicker).

UI-kit control chrome (`--wss-control-height`/`-sm`/`-lg`, default `32px`/`24px`/`40px`) is sized in fixed pixels: page zoom scales it fine, but OS-level text-only scaling (no zoom) can clip taller text into that fixed height — override the `--wss-control-height*` tokens if you need to support larger text sizes without zoom.

The form controls in `edit-controls.css` read the same generic bridge directly, with the AntD default as each `var()`'s own fallback — so an app that sets these at `:root` re-themes the form controls with no `edit-`-prefixed variable at all:
- `--color-primary`, `--color-danger`, `--border-color`.
- `--color-bg` (control backgrounds), `--color-bg-disabled`.
- `--color-page-background` (the `EditFile` drop zone and its file rows' hover).
- `--color-text` / `--color-text-secondary` (body text and icon-grade secondary color — just the affix suffix's clear/password-toggle **icons** now; icons only need WCAG 1.4.11's 3:1).
- `--color-text-secondary-strong` (default `#595959`) — the TEXT-grade secondary color WCAG 1.4.3's 4.5:1 requires: `EditString`/`EditTextArea`'s character count, the `ShowBoundValues` debug echo, and `EditFile`'s format/limit hints and each row's file size all read this, not the plain `--color-text-secondary` above.
- `--color-on-primary` (the styled checkbox's check glyph).
- `--color-tooltip-bg` / `--color-tooltip-text` (`LabelTooltip`).

Two `edit-`-prefixed tokens are declared at `:root` rather than only under `.edit-theme`:
- `--edit-color-border` (bridges to `--border-color`, default `#8c8c8c` — darkened from AntD's `#d9d9d9`, which was only 1.41:1 against white and failed WCAG 1.4.11's 3:1 floor for a checkbox box or a `.edit-theme` input's whole visual boundary) — so the styled checkbox, the `EditRadio*` "Other" free-text input, and the button-mode radio borders can be retargeted independently of the generic bridge.
- `--edit-color-primary-strong` (bridges to `--color-primary-strong`, default `#0066cc` unthemed / `#0958d9` under `.edit-theme`, mirroring `--wss-color-primary-strong` above) — the TEXT-grade primary color for sites where the primary IS text (the `AllowDownload` file-name link, the button-mode radio's checked/hover label) or a fill under white text, as opposed to `--edit-color-primary`'s chrome-grade use (borders, rings, fills — WCAG 1.4.11's 3:1).

### Opt-in AntD theme for the classic edit inputs (`.edit-theme`)

`EditString`, `EditNumber`, `EditTextArea`, `EditDateNative`, and `EditSelect`'s native `<select>` render completely unstyled by default (a consumer-owned `.edit-input` class, no border/background/radius) — every existing consumer already styles that class itself, and that behavior **never changes**. Wrap any element you own in `class="edit-theme"` to opt everything beneath it into the same AntD 4.x box chrome (border, radius, height, hover tint, focus glow) that the `--wss-*`-themed UI kit already uses for `Select`:

```razor
<div class="edit-theme">
    <EditString @bind-Value="model.Name" />
    <EditNumber @bind-Value="model.Age" />
</div>
```

- **Opt-in and render-tree-scoped, not global** — only descendants of a `.edit-theme` ancestor are affected; nothing outside it changes, and you can nest a second `.edit-theme` with its own overridden tokens (each scope resolves independently). This is the deliberate design for micro-frontends: wrap an MFE's own root, not `:root`, if it needs its own theme independent of the host page.
- **Radio/checkbox/`EditFile` are untouched** — `EditRadio`/`EditCheckedStringList`/`EditCheckedEnumList` carry their own `edit-radio-input`/`edit-checkedList-checkbox` classes (never `.edit-input`), `EditBool`'s checkbox is excluded by `type="checkbox"`, and `EditFile` carries no `.edit-input`-family class at all — none of them get boxed like a text field. Native `<select>` keeps its default `appearance: auto` (its own dropdown arrow); `EditNumber` keeps native spinner buttons unless you opt into its own `ShowStepper` group (whose chrome is ungated by `.edit-theme`, like the button-mode radio's — the theme only adds the palette) — both documented deviations from a literal AntD port.
- **Tokens** (declare/override on `.edit-theme` or any nested scope):
  - `--edit-color-primary` (default `#1890ff`, bridging to your `--color-primary`), `--edit-color-primary-strong` (default `#0958d9` here — see the TEXT-vs-chrome split above), `--edit-color-border` (bridges to `--border-color`, default `#8c8c8c`), `--edit-radius` (`2px`), `--edit-control-height`/`-sm`/`-lg` (`32px`/`24px`/`40px` — used as `min-height`, so a multi-row `EditTextArea` or `AutoSize` is never clipped).
  - `--edit-color-bg-disabled` (bridges to `--color-bg-disabled`), `--edit-color-placeholder` (default `rgba(0, 0, 0, 0.55)`), `--edit-color-text-disabled`, `--edit-color-text-secondary` (the affix suffix's clear/password-toggle **icons**), `--edit-color-text-secondary-strong` (default `rgba(0, 0, 0, 0.65)` — the darker shade `.edit-input-count`/`.edit-textarea-count` need: count is text, held to WCAG 1.4.3's 4.5:1, not an icon's 3:1).
  - Derived hover/focus colors are pure override knobs, computed at each usage site rather than baked into a token (same rule as the `--wss-*` tokens above) — override `--edit-color-primary-hover`, `--edit-primary-shadow`, or `--edit-error-shadow` directly if the computed `color-mix` isn't what you want.
- **Size** — see `Size` on `EditString`/`EditNumber`/`EditTextArea`/`EditDateNative` above; the size classes are inert hooks unthemed, and `.edit-theme` is what actually sizes them.

**Where to set the variables.** The `--wss-*` / `--edit-*` tokens can be overridden at **any scope** — `:root`, `body`, a theme class, or a micro-frontend's root container — and derived states (hover borders, focus shadows, focus rings) follow the override, because they derive from the base token at each usage site. The generic `--color-primary` / `--color-danger` / `--border-color` bridge, by contrast, is resolved **once, at `:root`** (a CSS custom property substitutes the `var()`s in its value where the property is declared): a `--color-primary` set on a nested container is not seen. Rule of thumb: app-wide theme → set `--color-*` at `:root` and everything follows; scoped/per-area theme (e.g. an MFE that doesn't own the host page) → set the `--wss-*` / `--edit-*` tokens themselves on your container. A directly-set `--wss-*` token always wins over the `--color-*` bridge.

The UI-kit components also accept regular `class` / `style` / `data-*` attributes (applied to the component's root element; `class` and `style` merge with the component's own), so one-off tweaks don't require CSS variables at all.

```razor
<EditString @bind-Value="model.Name" ContainerClass="my-custom-style" />
```

The form `Edit*` controls take arbitrary attributes too, each landing where it belongs:

| Attribute | Where it lands |
|---|---|
| `ContainerClass` | The root `.edit-control-wrapper` — the parameter to reach for when you want to style the whole block |
| `class` | The **field** element (the editor, the select engine, each checkbox/radio, the drop zone — the read-only value in read-only mode), merged with Blazor's `modified`/`valid`/`invalid` field-state classes |
| `style` | The root `.edit-control-wrapper`, merged with the control's own inline style if it has one (yours last, so your declarations win) |
| anything else (`inputmode`, `readonly`, `spellcheck`, `data-*`, `title`, extra `aria-*`, …) | The element it describes: the editor `<input>`/`<textarea>`/`<select>`/checkbox, the `role="radiogroup"` fieldset for the radio groups, the search engine's wrapper for `EditSelectSearch`, and the root wrapper for the list-bound controls (`EditMultiSelect`, `EditCheckedStringList`, `EditCheckedEnumList`, `EditFile`) |

`EditDate<T>` and `EditDateRange` are the one exception: they forward everything — `class` and `style` included — to the inner picker's wrapper, since that's the element their field-state classes have to reach.

A control that renders no editor (read-only mode) renders no editor-targeted attributes either.

```razor
<EditNumber @bind-Value="model.Quantity"
            class="qty-input"
            style="max-width: 8rem"
            inputmode="numeric"
            data-testid="qty" />
```

**Name collisions.** On `EditString` and `EditNumber` the rule is: **the control wins when it has an opinion; your value survives when it doesn't.** So `<EditString @bind-Value="m.Name" disabled />` really does render a disabled input even though `IsDisabled` is left at its default, and a hand-written `aria-required` / `aria-errormessage` / `list` survives on a field where the control has nothing to say — while `IsDisabled="true"`, a `[Required]` property, or a real validation error still wins outright. Three qualifications:

- **`type` is control-owned outright** on both, in every state, and a splatted one is dropped rather than honored. `type` doesn't describe the element, it decides what the element *is* — and on `EditString` it is load-bearing for the whole `IsPassword` bundle (the reveal toggle, the bullet-masked read-only row, the redacted `ShowBoundValues` echo, the `new-password` autocomplete, the `Suggestions` suppression), which a splatted `type="password"` would half-implement. Reach the same goals through the supported channels: `inputmode` (splats through untouched — this is the one for mobile soft keyboards), `Autocomplete`/`[Autocomplete]` for the field's purpose, and `IsPassword`/`[DataType(Password)]` for a secret.
- **`aria-invalid` is framework-owned.** Blazor's own `InputBase<TValue>` inserts it when a field has validation messages and removes it when it doesn't, before any library code runs, so a hand-written `aria-invalid` on a valid field is dropped upstream. The controls additionally guarantee `aria-invalid="true"` on a genuinely invalid field regardless of what was splatted.
- The other controls have not adopted this yet: on `EditTextArea`, `EditDateNative`, the selects and the radio groups, a splatted `disabled`/`aria-required`/`aria-errormessage` is still dropped when the control's own value is empty. Use the control's parameters there.

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

### 10.8.2

An additive, non-breaking release: a new slider control and a stepper mode for the existing numeric input.

**New**
- **New control: `EditRange<T>`** — an AntDesign-style horizontal slider bound via `@bind-Value` to any numeric type: rail, filled track, round handle, optional marks/dots, and a value tooltip.
  - `Min`/`Max` (`decimal?`) resolve param → the bound property's `[MinValue]`/`[MaxValue]`/`[Range]` → **0/100** (unlike `EditNumber`, whose bounds are simply omitted when nothing resolves, a slider always needs both ends to place its handle). `Step` (`decimal?`) resolves param → `[Step]` → 1.
  - `Marks` (`IReadOnlyDictionary<decimal, string>?`) renders labeled, clickable points under the rail. `SnapToMarks` restricts the value to those positions (AntD's `step={null}` equivalent). `Dots` draws a dot at every step increment and every mark, capped at 100 dots. `Included` (default true) fills the track and styles the active mark/dot; off gives AntD's discrete-points presentation.
  - `ShowTooltip` (default true) shows a value bubble on hover, focus, and drag; `TooltipFormat` (a numeric format string, falling back to `[DisplayFormat]`) drives that bubble, the `aria-valuetext` it implies, and the read-only text. `ParsingErrorMessage` covers an unparseable bound value.
  - The `role="slider"` tab stop is the track: arrow keys step one increment (one mark under `SnapToMarks`), PageUp/PageDown step ten, Home/End jump to the bounds. Pointer drag is handled by a new lazily-imported `wss-slider.js` module (24px hit area); without it a click still positions the handle and the keyboard model is untouched. Read-only mode renders the formatted value as text. Forced-colors and reduced-motion are both supported.
  - Deliberate exclusions: no vertical or reversed orientation, no dual-handle range mode, no always-open tooltip. See the `edit-controls` skill for the full deviation list.
- **`EditNumber<T>` gains `ShowStepper`** (`bool`, default false) — adds a minus button before the input and a plus button after it, joined into one group. A press moves the value by the effective step (`Step` → model `[Step]` → 1), clamped to `EffectiveMin`/`EffectiveMax`; each button auto-disables at its bound and when the control itself is disabled. Native number spinners are hidden while the mode is on. Buttons carry `tabindex="-1"` (not a tab stop — keyboard users still step via the input's native arrows) with accessible names defaulting to `"Decrease {label}"`/`"Increase {label}"`, overridable via `DecreaseButtonLabel`/`IncreaseButtonLabel`. No press-and-hold auto-repeat. Leaving it off renders byte-identical markup to 10.8.1.

### 10.8.1

A full accessibility audit of the `Edit*` form controls and the shared label/validation/stylesheet layers they render through (~75 findings — see `A11Y-AUDIT-2026-08-13.md` at the repo root for the complete report and remediation status), the counterpart to the 2026-08-11 UI-kit audit below. The form controls had never been audited before this pass. Landed in two waves — shared label/validation/stylesheet infrastructure first, then per-control fixes across select, date, radio/bool, checked-lists/file, and text — so the entries below span both. Alongside it, unrelated to the audit: a **new color control** (`EditColor` plus the UI-kit `ColorPicker` it wraps), the first addition to the `Edit*` family since `EditDate`'s rename.

**New** (Edit Controls)
- **New control: `EditColor`** — an AntDesign-5-style color field binding a plain `string?`. A swatch trigger over a transparency checkerboard opens a popup with a saturation/brightness area, a hue slider, an optional alpha slider, a HEX/RGB input row, and an optional preset row.
  - Accepts 3/4/6/8-digit hex (with or without `#`) and `rgb()`/`rgba()` text on the way in; emits normalized lowercase `#rrggbb`, extended to `#rrggbbaa` only when the color is translucent **and** `ShowAlpha` is on. A value it can't parse — including a named CSS color like `chartreuse` — renders as "no color" rather than throwing.
  - Read-only mode renders the swatch itself — plus the normalized hex text when `ShowText` is on, the same swatch-plus-text layout as the edit-mode trigger — rather than making the reader decode a hex string. An unset or unparseable value falls back to `ReadOnlyValue`'s plain "Not Set" placeholder.
  - Parameters: **`ShowAlpha`** (`bool`, default `true` — `false` also strips the channel from the emitted value), **`ShowText`** (`bool`, default `false`), **`AllowClear`** (`bool`, default `false`, clears to `null`), **`Presets`**/**`PresetsLabel`**, **`Placement`** (`PopupPlacement`, default `Bottom`), **`ParsingErrorMessage`** (`string`, default `"The {0} field must be a color."`), plus the usual localizable accessible-name set (`TriggerLabel`/`EmptyLabel`/`PanelLabel`/`SaturationLabel`/`SaturationValueTextFormat`/`HueLabel`/`AlphaLabel`/`ClearLabel`/`FormatLabel`/`HexLabel`/`RedLabel`/`GreenLabel`/`BlueLabel`/`AlphaPercentLabel`).
  - Every track is a `role="slider"` the arrow keys step (Shift or PageUp/PageDown for the ×10 step), and the trigger's accessible name carries the current value ("Brand Color: #1890ff"). Dragging needs `wss-color.js`; without it a single click still positions the handle and the keyboard path is untouched.
  - The hue and alpha tracks keep AntD's 10px-tall design but carry an invisible 24px-tall pointer target each (WCAG 2.5.8), added as a pseudo-element so nothing about the layout, the paint, or the click→value mapping changes.
  - Windows High Contrast (`@media (forced-colors: active)`): the swatches, the saturation/brightness area, and the hue/alpha tracks opt out of the forced palette (`forced-color-adjust: none` — this is the one control in the library where the color *is* the information, so forcing it would leave nothing to see or aim at), and the selected preset's ring is re-expressed as a system-colored outline since `box-shadow` is dropped.
  - Needs `wss-controls.css` (and, for dragging, the lazily-imported `wss-color.js`) — same asset requirement as `EditDate`. See [Color picking](#color-picking-editcolor--colorpicker).
- `FormLabel` gains **`TooltipTriggerLabel`** (`string?`, default `null`) — overrides the tooltip trigger's accessible name, which otherwise defaults to `"More information about {label}"` (or the bare "More information" when there's no label text to name it with) so a form with several tooltipped fields doesn't read as a list of identical "More information" buttons — and **`IsRequiredTextIncluded`** (`bool`, default `false`) — adds a visually-hidden "(required)" after a `role="group"` fieldset's legend text, since ARIA 1.2 permits `aria-required` on `radiogroup` but not on `group`, leaving a required checked-list with no channel for its required-ness to reach assistive tech at all (the star alone is `aria-hidden`). Opt-in, since a control whose field already carries `aria-required` must leave it false or the name would announce "required" twice.
- `LabelTooltip` gains **`TriggerLabel`** (`string?`, default `null`) — the parameter `FormLabel.TooltipTriggerLabel` forwards into, falling back to the existing bare `"More information"`.
- `ReadOnlyValue` gains **`EmptyText`** (`string`, default `"Not Set"`) — real, announced text for an empty read-only value, replacing a placeholder that was previously `aria-hidden` **and** `visibility: hidden`, reaching nobody. `EditDisplay` gains its own parallel **`EmptyText`** (`string`, default `"Not Set"`) for the same reason — it hand-rolls its own read-only view rather than using `ReadOnlyValue`, so the two parameters are independent, not inherited.
- `EditDate<T>` gains **`RangeErrorMessage`** (`string`, default `"The {0} field must be an allowed date."`) — a validation message for a well-formed typed date that `Min`/`Max`/`DisabledDate`/`DisabledTime` rejects, landing in the same `ValidationMessageStore` as `ParsingErrorMessage` but for the opposite situation ("that IS a date, just not one this field accepts" vs. "that isn't a date at all") — previously silent in every channel, the picker simply reverted the text. Also gains **`Autocomplete`** (`string?`, default `null`, falling back to the bound property's `[Autocomplete]` then the picker's own `"off"` — simpler than `EditString`'s password/property-name-inference chain, since a date field has no password mode to distinguish), **`FormatHintLabel`**/**`RangeHintMinLabel`**/**`RangeHintMaxLabel`** (defaults `"Format:"`/`"Earliest date:"`/`"Latest date:"`, folded into the input's `aria-describedby` chain), and **`WeekLabel`** (default `"Week"`, names the week-number row header in `Week` mode) — all forwarded straight through to the inner `DatePicker`'s own same-named parameters (see New (UI Kit) below).
- `EditDateRange` gains the same **`RangeErrorMessage`** (identical default, raised against whichever endpoint's well-formed value the picker rejects — built on `DateRangePicker`'s new `OnStartRangeError`/`OnEndRangeError`, see below), plus **`StartAutocomplete`**/**`EndAutocomplete`** (same per-endpoint resolution as `StartPlaceholder`/`EndPlaceholder`), **`WeekLabel`** (default `"Week"`), and **`FormatHintLabel`**/**`RangeHintMinLabel`**/**`RangeHintMaxLabel`** (defaults `"Format:"`/`"Earliest date:"`/`"Latest date:"`, forwarded to the inner `DateRangePicker`'s own same-named parameters below).
- `EditDateNative<T>` gains **`FormatHintLabel`** (`string`, default `"Format:"`) — relevant only in `Month` mode, the one native type whose typed format isn't otherwise obvious from the rendered control.
- `EditRadioEnum<TEnum>`/`EditRadioString` gain **`OtherAriaLabel`** (`string?`, default `null`) overriding the "Other" free-text box's accessible name, falling back to the existing `"Custom text value input"` literal (now `RadioOtherInput.DefaultAriaLabel`) — previously a fixed literal with no override and no tie back to which field's "Other" box it belonged to. `RadioOtherInput` (the internal control both share) exposes the matching **`AriaLabel`** (`string`, default `RadioOtherInput.DefaultAriaLabel`).
- `EditString` gains **`ShowPasswordButtonLabel`**/**`ShowValueButtonLabel`** (`string?`, default `null` each; effective text `"Show {label} password"`/`"Show {label} value"`), and the shared `EditTextInputBase` (backing both `EditString` and `EditTextArea`) gains **`ClearButtonLabel`** (`string?`, default `null`; effective `"Clear {label}"`) — all three were previously fixed literals repeated identically on every field, so a form with two of the same control read as indistinguishable "Clear"/"Show password" buttons to a screen reader browsing by button list.

**New** (UI Kit)
- **New control: `ColorPicker`** — the popup engine behind `EditColor` above, usable standalone via `@bind-Value` (a `string?`). Adds `Value`/`ValueChanged`, `OnParseError`, `OnValidCommit` (every valid commit — *including* one equal to the value already bound, which the dedup keeps out of `ValueChanged`, and including `AllowClear`'s clear, which is a valid commit of "no color"; it's what lets a wrapper retire a stale parse message when the user retypes the color already there or empties the field), `Disabled`, `ShowAlpha`, `ShowText`, `AllowClear`, `Presets`/`PresetsLabel`, `Placement`, `Id`, and the localizable label set. Built on `PopupOverlayBase` — the same placement/dismiss/trigger-ARIA/focus-restore engine behind `Popover`/`Popconfirm`, so it inherits viewport flip/shift, backdrop-and-Escape dismiss, `aria-controls`, and focus restore on close. Deliberately **uncontrolled** (no `Visible`/`VisibleChanged`): a color popup is only ever opened by its own trigger, and a controlled open is the shape that can bypass `Disabled`. New `ColorFormat` enum (`Hex`/`Rgb`) for the input row's format switch — presentation only; the bound value is always normalized hex. New `ColorMath` helper (public, in `Controls.Helpers`) exposes the underlying hex/`rgb()` parsing, normalization, and HSV↔RGB conversions.
- New JS module **`wss-color.js`** (lazily imported, like every other `wss-*.js`) — pointer dragging plus the per-key `preventDefault` Blazor can't express. It reports normalized coordinates by writing them into hidden inputs the component already listens to rather than through a `DotNetObjectReference`, keeping the library's interop one-way and avoiding a by-name `[JSInvokable]` that would need explicit rooting under `TrimMode=full`.
- `Select<TValue>` gains **`InputLabel`** (`string?`, default `null`) and **`AriaLabelledBy`** (`string?`, default `null`, wins over `InputLabel` when both resolve) — names the `role="combobox"` input, which a standalone `<Select>` previously had no way to do at all: no `<label for>`, and a bare `aria-label` used to land on the roleless wrapper `<div>` and get silently ignored (now lifted onto the input automatically too). The `Edit*` wrappers already wire `AriaLabelledBy` internally to `FormLabel`'s `lbltext-{id}` naming anchor, so only a `Select` used standalone needs to set either parameter itself.
- `Select<TValue>` gains its first live region — filtering, selection, deselection, tag removal, "no results", and loading were all previously silent — customizable via **`ResultCountAnnouncementFormat`** (default `"{0} results"`), **`SelectedAnnouncementFormat`** (`"{0} selected"`), **`DeselectedAnnouncementFormat`** (`"{0} deselected"`), **`SelectionClearedAnnouncement`** (`"Selection cleared"`), and **`LoadingAnnouncement`** (`"Loading"`) — all `string`/`string.Format` templates with unchanged-English defaults (`SelectDefaults`), matching the "override to localize" convention the rest of the kit already uses. **`MaxTagCountLabelFormat`** (default `"{0} more selected"`) similarly names the `MaxTagCount` overflow chip's sr-only text. `EditSelectSearch` forwards the single-select subset (`ResultCountAnnouncementFormat`/`SelectedAnnouncementFormat`/`SelectionClearedAnnouncement`/`LoadingAnnouncement` — no `Deselected`/`MaxTagCount`, which don't apply to a single selection); `EditMultiSelect` forwards the full multi-select set (adding `DeselectedAnnouncementFormat`/`MaxTagCountLabelFormat`).
- `DatePicker` gains **`OnRangeError`** (`EventCallback<string>`) — raised for a well-formed typed date that `Min`/`Max`/`DisabledDate`/`DisabledTime` rejects (previously reverted in total silence: no error, no announcement, no validation message of any kind — distinct from the existing `OnParseError`, which covers text that isn't a date at all), plus **`Autocomplete`** (`string?`, default `null`, renders `autocomplete="off"` — the value both this and `DateRangePicker`'s inputs hardcoded before the parameter existed), **`AriaLabelledBy`** (`string?`, default `null`, wins over `InputLabel`), **`RangeHintMinLabel`**/**`RangeHintMaxLabel`** (default `"Earliest date:"`/`"Latest date:"`, rendered only when `Min`/`Max` is set, folded into the same `aria-describedby` element as the format hint — otherwise a bound reaches the user only as a per-cell `aria-disabled` in the calendar, invisible to someone typing), and **`WeekLabel`** (default `"Week"`, names each row's week-number cell — a `role="rowheader"` in `Week` mode, since the row rather than the day is the selection unit there).
- `DateRangePicker` gains **`OnValidCommit`** (`EventCallback<DateRangeEndpoints>`) — the per-endpoint counterpart of `DatePicker.OnValidCommit`, raised on every accepted commit and carrying which endpoint(s) that commit *assigned*, including an assignment equal to the value that endpoint already held (which `StartChanged`/`EndChanged` drop, being both per-endpoint and dedup'd). New **`DateRangeEndpoints`** `[Flags]` enum (`None`/`Start`/`End`/`Both`) — one commit can assign both, so the payload is flags rather than a second callback. A two-click range pick, a preset, a session OK and `AllowClear`'s clear report `Both`; a typed entry reports only its own side, even though the commit passes the other endpoint's current value through unchanged. Wire it if you render `OnStart*Error`/`OnEnd*Error` messages against a standalone `DateRangePicker` — see Fixed below for what it fixes in `EditDateRange`.
- `DateRangePicker` gains the per-endpoint **`OnStartRangeError`**/**`OnEndRangeError`** counterparts (same contract as `DatePicker.OnRangeError`, split like every other per-endpoint parameter on this control), **`StartAutocomplete`**/**`EndAutocomplete`**, **`GroupLabel`**/**`GroupLabelledBy`** (`string?`, both default `null` — names the composite box holding both inputs, which takes `role="group"` only once either is set; without a name a form wrapper's single visible label associated with the Start input alone and the two inputs read as unrelated fields. `EditDateRange` wires `GroupLabelledBy` internally to its own naming anchor, the same pattern `Select`'s wrappers use), and **`WeekLabel`** (default `"Week"`, same contract as `DatePicker`'s). Also gains **`RangeHintMinLabel`**/**`RangeHintMaxLabel`** (same defaults as `DatePicker`'s), rendered as one shared hint for *both* inputs rather than per-endpoint — the two share a single calendar and a single `Min`/`Max` pair, so one pair of clauses (appended to the existing shared format hint, folded into each input's `aria-describedby`) covers both.

**Changed**
- **Auto-generated labels no longer shatter acronyms.** `URLPath` → "URL Path", `ID` → "ID" — previously "U R L Path"/"I D". A visible change to any label, validation message, or enum display name derived from a property/member name with a run of 2+ capitals. Digit-adjacent splitting is unchanged (`Address1Line` still splits the same way it always did). One shared helper (`EnumHelpers.SplitCamelCase`) now backs both the property-name and enum-name label paths, which had drifted into two independently-broken copies of the same rule.
- **A field's accessible name now comes from its label's TEXT alone.** A new `lbltext-{id}` naming anchor wraps just the label string; `aria-labelledby` references across the library — including the new `Select`/picker parameters above — point at it instead of the whole `lbl-{id}` label element, so a field with a tooltip trigger no longer announces "Full Name More information" as its name, just "Full Name". A `role="group"` fieldset's required cue (`FormLabel.IsRequiredTextIncluded`, above) is deliberately kept *inside* the anchor, since that's the only channel requiredness has there.
- **An out-of-range typed date now raises a validation message instead of silently reverting.** Previously, `Min`/`Max`/`DisabledDate`/`DisabledTime` rejecting a well-formed typed value (as opposed to unparseable text, which `ParsingErrorMessage` already covered) produced no error, no announcement, and no validation message — the value just reverted with nothing to explain why. Now surfaced via the new `RangeErrorMessage`/`OnRangeError` family above.
- **A default value on a non-nullable `EditDate<T>` binding now reads as empty, in both edit and read-only mode** — previously the read-only view alone could show a bare "0001" for an unset non-nullable `DateTime`, agreeing with neither the pre-10.7.0 behavior nor the edit view.
- **`IsOptionDisabled` targeting the currently-selected option on `EditRadioString`/`EditRadioEnum<TEnum>` no longer removes the whole group from the tab order.** A disabled-but-selected radio has no other option left to carry a native tab stop; it now renders `aria-disabled="true"` (still logically inert — the commit path still rejects it) instead of the native `disabled` attribute, keeping one focusable stop in the group. Every other disabled option is unaffected. See [Per-option disabling](#per-option-disabling).
- **`EditString`'s `autocomplete` fallback now infers a real token from the bound property's name** (`Email` → `"email"`, `PostalCode` → `"postal-code"`, `Phone`/`PhoneNumber`/`Mobile` → `"tel"`, ~20 common names in total) **before** falling back to `"one-time-code"` — previously *every* unannotated field got `"one-time-code"`, a token reserved for OTP/2FA entry, which could make a mobile browser offer OTP-style autofill over an ordinary email/name/phone field. `"one-time-code"` remains the last-resort fallback for a property name the table doesn't recognize, preserving the autofill-interception protection it was originally added for; a password field still falls back to `"new-password"`, unaffected. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).
- **`DatePicker`/`DateRangePicker` inputs now render `role="combobox"`** (previously `role="textbox"`, which doesn't permit the `aria-expanded` the open/closed state has always relied on as its only signal — an axe-core pass flagged 28 nodes). No parameter or markup change.
- **Signature change:** `EditDate<T>.DialogLabel` and `EditDateRange.DialogLabel` are now `string?` (previously `string`, defaulting to the picker's own constant "Choose date"/"Choose date range" text). Unset, each now derives the dialog's name from the control's own resolved field label instead — "Choose Birth Date", "Choose Stay Dates" — so a renamed field's dialog name follows it automatically instead of every date popup on a form announcing identically. An explicit `DialogLabel` still overrides. Markup usage is unaffected; C# code reading the property directly should account for the type change.
- **Contrast/theming:** `--edit-color-border` (unthemed and `.edit-theme`) darkens from AntD's `#d9d9d9` (1.41:1 against white) to `#8c8c8c` (3.36:1), so an unchecked checkbox box or a `.edit-theme` text input's whole visual boundary clears WCAG 1.4.11's 3:1 floor for a UI component. `--wss-color-placeholder` darkens from `#bfbfbf` (1.84:1) to `rgba(0, 0, 0, 0.55)`. New **`--edit-color-primary-strong`** (bridges `--color-primary-strong`, default `#0066cc` unthemed / `#0958d9` under `.edit-theme`, mirroring `--wss-color-primary-strong`) splits off the TEXT-grade use of the primary color (the `AllowDownload` file-name link, the button-mode radio's checked/hover label) from `--edit-color-primary`, which now backs chrome only (borders, rings, fills — WCAG 1.4.11's 3:1). New **`--color-text-secondary-strong`** generic bridge (default `#595959`; `--wss-color-text-secondary-strong: rgba(0, 0, 0, 0.65)` in the kit) — the character count on `EditString`/`EditTextArea`, the `ShowBoundValues` debug echo, and `EditFile`'s format/limit hints and each row's file size all move to it from the plain `--color-text-secondary` (`#8c8c8c`, 3.36:1 — too low for 0.85em text under WCAG 1.4.3), which now backs only icon-grade uses (the affix clear/password-toggle icons, still fine at 1.4.11's 3:1). See [Styling and Customization](#styling-and-customization).
- **`edit-controls.css` gains its first `@media (forced-colors: active)` block** — a focused `.edit-theme` field's ring, the checked/indeterminate checkbox glyph, `EditFile`'s upload/upload-error/delete icons, and the button-mode radio's checked state are all painted as an author `background`/`box-shadow`, and so were previously invisible in Windows High Contrast (checked and unchecked rendered identically). The [Accessibility](#accessibility) section's existing "High contrast color support" claim now actually holds for the form controls, not only the UI kit.

**Fixed**
- **A stale parse/range error on `EditDate<T>` now clears when the retyped value equals the one already bound.** `DatePicker` skips `ValueChanged` for a commit that doesn't change the value — and that callback was also the only channel clearing the message — so retyping the date the field already held left "must be a date" (and the `aria-invalid` it drives) up permanently, with no entry the user could type to clear it and `OnValidSubmit` blocked. New **`DatePicker.OnValidCommit`** (`EventCallback`, raised on every accepted commit including a no-change one) carries the news; `EditDate` clears from it as well as from `ValueChanged`. Wire it yourself if you render `OnParseError`/`OnRangeError` messages against a standalone `DatePicker`. `AllowClear`'s clear raises it too — an emptied field also retires a stale message.
- **The same stale parse/range error on `EditDateRange` now clears, per endpoint.** `DateRangePicker` raises `StartChanged`/`EndChanged` per endpoint *and* only when that endpoint's own value changed — and those were the only channels clearing these messages — so retyping the date one endpoint already held left its message and `aria-invalid` up permanently, exactly as for `EditDate` above. New **`DateRangePicker.OnValidCommit`** (`EventCallback<DateRangeEndpoints>`, see New (UI Kit)) carries which endpoint(s) a commit assigned, and `EditDateRange` retires exactly those — so a Start entry never silently retires End's message, while one commit that assigns both (a range-selection click, a preset, a session OK, the clear) retires both.
- **A `DatePicker`/`DateRangePicker` field marked invalid no longer loses its error border on hover.** `.wss-picker:not(.wss-picker-disabled):hover .wss-picker-input` is more specific than `.wss-picker.invalid .wss-picker-input`, so merely pointing at an invalid date field replaced the red border with the primary hover one. The invalid state now outranks hover the same way it already outranked open/focus.
- **`Select`'s combobox input now exposes its own selected value.** It was previously bound to the search text (empty unless typing) while the selection lived in an unlinked sibling `<span>`, so a screen reader landing on a completed, closed select read "combo box, blank." Single mode now carries the selected option's label as the input's own `value`; multi/tags mode exposes the joined labels via `aria-describedby`.
- **`Select`'s option groups (`SelectOption.Group`) are now exposed to assistive tech.** The header row itself stays presentational/`aria-hidden` (grouping is conveyed visually there), but each option in a group now carries `aria-describedby` pointing at a dedicated visually-hidden span rendered outside the virtualized list — pointing at the header row directly would dangle once virtualization unmounts it. See [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).

**Deprecated**
- **`SelectOptionList<TItem>.Tooltip` is `[Obsolete]` and no longer rendered.** It put the field's tooltip text on every `<option>`, so a screen reader repeated it once per option in the dropdown. Kept as an inert parameter (not removed, and not `error: true`) rather than a source break in what's otherwise a patch-level sweep — setting it now compiles but does nothing.

**Also from the 2026-08-13 audit** (these were first written under `10.8.0`; that release is already cut, so they belong here)
- `EditFile` gains a polite `role="status"` live region and three new parameters — **`LoadingStatusText`** (`string`, default `"Loading files…"`), **`FilesSelectedStatusFormat`** (`string`, default `"{0} {2} selected: {1}."`), **`NoFilesSelectedStatusText`** (`string`, default `"No files selected."`) — announcing that a batch is being validated/buffered, then the resulting selection (additions, removals, or a partial success alongside a rejection) once it settles. The drop zone's instructional text also now states the resolved per-file/aggregate size caps and, when finite, the max file count, alongside "Supported formats" — previously discoverable only by triggering a rejection. See [File upload parity features](#file-upload-parity-features-editfile).
- **`EditCheckedStringList`/`EditCheckedEnumList<TEnum>`:** the `role="group"` fieldset now carries `aria-labelledby` (pointed at `FormLabel`'s `lbltext-{id}` naming anchor, not the whole legend) and opts into `FormLabel`'s `IsRequiredTextIncluded` — a required group previously had no channel for its required-ness to reach assistive tech at all, since ARIA 1.2 forbids `aria-required` on `role="group"` and the visual star is `aria-hidden`. The read-only view now wraps selected options in a real `<ul>`/`<li>` list (matching `EditFile`'s read-only list) instead of one bare value per option, so a screen reader can report position/count; an empty-string option now displays as `"(blank)"` instead of an unlabeled control. A `[MinLength]`/`[MaxLength]` on the bound list, with no `Description` of its own, now renders a derived up-front hint ("Select at least 2 options.", "Select up to 5 options.", "Select between 2 and 5 options.") instead of surfacing only as a post-validation error.
- **`ReadOnlyValue`/`EditDisplay`/`EditFile`'s read-only list:** `aria-labelledby` now points at `lbltext-{id}` (the naming anchor) instead of `lbl-{id}` (the label/legend element, which can also hold the tooltip trigger) — matches the naming-anchor convention the radio groups already use. `ReadOnlyValue` also gains an `AriaDescribedBy` parameter, wired at the checked-list read-only rows and `EditFile`'s empty-selection row.
- `EditFile`'s plain (non-`AllowDownload`) file-name span gains `title="{name}"` so the full name is recoverable when the ellipsis-truncated text doesn't fit — it previously had no way to recover a name that overflowed.

### 10.8.0

A full accessibility audit of `Controls/UiKit/` (58 findings, adversarially verified — see `UIKIT-A11Y-AUDIT-2026-08-11.md` at the repo root for the complete report and remediation status) drove most of this release, alongside an unrelated `EditFile` addition.

**Breaking**
- **`IMessageService`/`INotificationService` gain `Pause(Guid id)`/`Resume(Guid id)`.** `Pause` cancels a toast's auto-dismiss countdown without removing it; `Resume` restarts it from a fresh full duration, not the time remaining when paused (WCAG 2.2.1 — a user hovering or focused inside a toast shouldn't have it vanish underneath them). A source break only for a hand-written `IMessageService`/`INotificationService` implementation — `MessageService`/`NotificationService` and the static `WasmMessageService`/`WasmNotificationService` already implement both, and `MessageContainer`/`NotificationContainer` (scoped and Wasm) already wire them from hover/focus automatically, so no markup changes are needed. See [UI Kit (non-form) controls](#ui-kit-non-form-controls).

**New** (Edit Controls)
- `EditFile` gains **`Bordered`** (`bool`, default `false`) — wraps the label and picker/file-list together in one bordered card (`edit-file-card`) — and **`AllowDownload`** (`bool`, default `false`) — renders each selected file's name as a link that re-saves its already-buffered bytes via a `Blob` + temporary `<a download>` (no network fetch), in both edit and read-only modes. Combined, they match Ant Design's already-uploaded-file card pattern (bordered field, file name as a link, no dropzone once `MaxFiles` is reached). Both default to `false` and leave existing markup byte-identical. See [File upload parity features](#file-upload-parity-features-editfile).

**New** (UI Kit)
- **Table** gains `AriaLabel`, `ScrollRegionLabel` (names the `ScrollY` wrapper as a focusable region), `LoadingLabel` (a persistent sr-only status region announcing loading/empty), `SelectRowLabelFor` (per-row selection accessible names), and `FilterAppliedButtonLabelFormat`/`FilterAppliedLabel` (the filter trigger's name reflects whether a filter is applied). `OnRowClick` rows are now keyboard tab stops activated with Enter (WCAG 2.1.1, same propagation guards as click, no `role="button"`); `Loading` now disables the controls its mask covers so keyboard matches pointer inertness; filter triggers gain `aria-haspopup="dialog"` and expand buttons gain `aria-controls` to their detail row.
- **Pagination** gains `Disabled` and `AnnounceTotal` — with `AnnounceTotal`, the `ShowTotal` text ("1-10 of 200 items") becomes a `role="status"` region (WCAG 4.1.3) so it's announced without a focus move; `Table` silences the top pager's copy under `PagerPosition.Both` so it isn't announced twice.
- **`DatePicker`/`DateRangePicker`** gain `FormatHintLabel` (a visually-hidden format hint appended to the inputs' `aria-describedby` chain). Every calendar is now a real ARIA grid (`role="grid"/"row"/"gridcell"`; selection moved from `aria-pressed` on buttons to `aria-selected` on cells, including in-range days on `DateRangePicker`), each panel gets an id (`aria-controls` on the inputs) and a polite live region announcing the displayed month/year. Under a right-to-left UI culture the Left/Right arrow keys now follow the visual direction in every grid shape (vertical arrows, Home/End, PageUp/PageDown stay logical); `ArrowDown` from either text field while the panel is open moves focus onto the calendar's roving-tabindex cell.
- **Tabs**: arrow keys follow the visual direction under an RTL UI culture, same rule as the pickers; the tabpanel is now a tab stop so a text-only panel is keyboard-reachable.
- **Alert** gains `SeverityLabel` (localizes the sr-only severity word announced before the content) and `Live` (opts a persistent banner out of live-region semantics); `CloseButtonLabel` on `Alert` and on all four toast containers (scoped + Wasm `Message`/`Notification`) localizes their close buttons, and each toast's close button now carries `aria-describedby` to its own item's content so a stack of toasts no longer reads as indistinguishable "Close" buttons.
- **`SearchInput`** renders `type="search"`, keeps the enter button's `aria-label` while `Loading` (previously nameless), and its accessible-name chain now has a guaranteed floor: with no `InputLabel`/`AddonLabel`/`AddonContent`/`Placeholder` it falls back to `SearchButtonLabel`.
- **Popover/Popconfirm** fall back to `aria-label` `"Popover"`/`"Confirm"` when untitled (matching `Modal`/`Drawer`), and their panels get ids mirrored as `aria-controls` on the trigger.
- **Modal/Drawer** inert the background while open (the topmost of a stack owns it; toast containers, reconnect UI, and a new `data-wss-keep-interactive` escape hatch are exempt), with initial focus falling back to a plain `FocusAsync` when JS isn't available.
- `data-tooltip` bubbles gain full accessibility exposure: a shared `role="tooltip"` description node wired via `aria-describedby` (this is what makes the description reachable on touch, where the visual bubble is suppressed), Escape-dismiss (`wss-tooltip-dismissed`, WCAG 1.4.13), and the bubble itself is now hoverable so pointer users can reach it without it disappearing.
- New `--wss-color-primary-strong`/`--wss-color-error-strong` tokens (+ `-hover` knobs, `--color-*-strong` generic bridges) back white-on-fill button text and primary-as-text sites at ≥4.5:1 while the base tokens stay at chrome contrast (3:1). See [Styling and Customization](#styling-and-customization).

**Changed**
- `--wss-color-text-deemphasized` darkens `#737373` → `#696969` — the old value was 4.35:1 against `--wss-color-bg-hover` (a source-comment arithmetic error claimed 4.58:1), so outside-month/decade picker day numbers dropped under AA while hovered; the new value is 5.49:1 on white / 5.04:1 on the hover tint. `--wss-color-warning`/`--wss-color-success` defaults also darken to a clear 3:1 as icon colors.
- Fixed control heights become `min-height` floors (WCAG 1.4.4 text scaling) on selects, picker fields, search, dialog/pagination buttons, the filter footer, and picker cells — a host page's own `input`/`button` height resets can no longer clamp a control below its intended size. Four visual baselines were regenerated: the old PNGs encoded the FormTesting host's `app.css` resets clamping kit buttons/inputs below their intended size.
- `.wss-dialog-btn-danger:hover` gains the generic `--color-danger-strong-hover` bridge, matching the primary hover chain (theming contract).

**Fixed**
- **Table:** Enter on interactive content inside a plain `Column` no longer double-fires `OnRowClick` — the keydown stop-propagation guard is now unconditional at every `<td>` instead of gated to `ActionColumn`/selection/expand cells (pointer behavior, which was already correct, is unchanged). `OnRowClickedAsync` now respects `Loading` the same way the keyboard path already did, so a synthesized or programmatic click can no longer fire `OnRowClick`/`ExpandRowByClick` mid-refresh.
- **Pickers:** rejected day/month/quarter/year cells now render `aria-disabled="true"` with commit guards instead of native `disabled`, so the roving tabindex can always take DOM focus — arrowing across a disabled run no longer stalls the focus ring, strands the grid with zero tab stops, or blurs focus to `<body>` on a month-crossing move (WCAG 2.1.1/2.4.3/2.4.7).

**Internal**
- New `PickerA11yE2ETests` assert via Playwright ARIA snapshots that the `display: contents` grid rows still expose `grid > row > gridcell` in the browser's accessibility tree, plus real-focus coverage for the `ArrowDown` hand-off.
- New `WasmStaticToastCollection` serializes the test classes that share the process-static Wasm toast services — they previously ran in parallel xUnit collections, and one class's `Clear()` could wipe another's in-flight toast (a schedule-dependent flake surfaced by the new container tests).

### 10.7.1.1 (`WssBlazorControls.Demo` only — `WssBlazorControls` stays on 10.7.1)

**New**
- The "Markup"/"Model"/CSS code-block styling shown throughout the demo pages (`.code-block`, `.code-block-css`, `.code-block-model`, `.code-block-react`) now ships as `wwwroot/demo.css` in the package, instead of living only in this repo's own FormTesting host stylesheet. Consumers hosting these demo components link it once, alongside the main package's stylesheets:
  ```html
  <link href="_content/WssBlazorControls.Demo/demo.css" rel="stylesheet" />
  ```
  Anyone who previously supplied their own `.code-block` CSS to style these pages can drop it.

**Changed**
- `<EditControlsDemo />` now wraps its controls in `<FormDefaults UpdateOn="UpdateTrigger.Input">`, so `EditNumber`/`EditDateNative` (and the Radio "Other" free-text box) validate on every keystroke instead of on blur, matching `EditString`/`EditTextArea`'s existing behavior.

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

- **`INotificationService.Success`/`Info`/`Warning`/`Error` now return `Guid` instead of `void`**, matching `IMessageService`'s own signatures; `NotificationService` and the static `WasmNotificationService` forward the id through.
  - Pass it to `Remove(Guid)` to dismiss a sticky (`Duration=0`) notification programmatically — previously the only handle on a notification was the user's own close button.
  - A source break only for a custom `INotificationService` implementation (change the four return types) or a C# expression-bodied lambda whose inferred type was `void`; every ordinary `Notifications.Error("...")` call site compiles unchanged. See [UI Kit (non-form) controls](#ui-kit-non-form-controls).
- **`ReadOnlyValue.IsLabelHidden` is removed, replaced by `HasLabelElement` (`bool`, default `true`)** — the parameter's meaning inverted along with the accessible-name fix below.
  - `IsLabelHidden` gated `aria-labelledby` on "is the label hidden", which was the wrong question: `FormLabel` renders the `lbl-{id}` element either way (visually hidden), so the reference never dangles.
  - `HasLabelElement="false"` now means the narrower, actually-correct thing — there is no label element for this value to be named by at all — and only the per-option rows of a read-only checked list pass it. Affects consumers who use `ReadOnlyValue` directly (it is public, though intended for use inside the controls).
- **`FormLabel.IdPrefix` and `FieldValidationDisplay.IdPrefix` are removed.** Both were inert — nothing read them, and neither component composes an id of its own (each takes the host control's already-resolved `Id`). The per-control `IdPrefix` on every `Edit*` control is unaffected and remains the way to prefix a generated id.
- **`CheckboxOptionList<TItem>.IsLabelHidden` is removed** — same inertness. The type is technically public but documented internal-use (the shared checkbox-per-option body behind `EditCheckedStringList`/`EditCheckedEnumList`), and the parameter became meaningless once its read-only rows switched to `HasLabelElement`.
- **`<WasmMessageContainer />` and `<WasmNotificationContainer />` now throw on Blazor Server instead of silently leaking one user's toasts to every other circuit.**
  - `WasmMessageService`/`WasmNotificationService` keep their state in process-`static` fields, which the docs have always said meant "WASM only" — but nothing enforced it, so a Server app that dropped one in compiled, worked in single-user dev, and in production rendered user A's notifications on user B's screen.
  - The container now asserts on its first `OnAfterRender` and throws an `InvalidOperationException` naming the scoped replacement (`builder.Services.AddWssControlsToasts()` + `<MessageContainer />`/`<NotificationContainer />`).
  - **Consumer action:** if you host these on Server — including the *server phase* of an `InteractiveAuto` component, where the failure is otherwise nondeterministic (the first load runs on Server and throws, a later one runs on WebAssembly and succeeds) — switch to the scoped path.
  - The check is `RendererInfo.Name == "Server"`, not "am I in the browser": **Blazor Hybrid (MAUI/WPF/WinForms `BlazorWebView`) is explicitly permitted**, since it runs outside the browser but serves exactly one user per process, which is the condition that makes a process static safe. WebAssembly and the static prerender pass are permitted too, so a WASM app's own prerender is unaffected. See [UI Kit (non-form) controls](#ui-kit-non-form-controls).

**New** (Edit Controls)
- `EditDate<T>` gains `Size` (`SelectSize`: `Default`/`Small`/`Large`) and `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) — both added to the picker *before* the rename above, so the renamed `EditDate<T>` is a genuine superset of the old `EditDatePicker<T>`, not a regression.
  - `Size` renders `wss-picker-sm`/`wss-picker-lg` on the picker wrapper (mirroring `Select`'s own size classes; `Default` adds no class).
  - `ParsingErrorMessage` is genuinely new behavior, not a ported parameter: the underlying `DatePicker` gained an `OnParseError` callback, and `EditDate<T>` now surfaces unparseable typed text (something that isn't a date at all) as a validation message via its own `ValidationMessageStore`, cleared the moment a valid value next commits and on the control's dispose. Previously a bad typed entry was silently reverted to the last valid value with no feedback whatsoever. Only fires for text that fails to parse as a date — a well-formed date merely rejected by `Min`/`Max`/`DisabledDate`/`DisabledTime` does not trigger it. See [Available Controls](#input-controls).

- New `UpdateTrigger` enum (`Input`/`Change`) + `UpdateOn` (`UpdateTrigger?`) parameter on `EditString`/`EditTextArea` (default `Input`), `EditNumber`/`EditDateNative` (default `Change`), and `EditRadioString`/`EditRadioEnum<TEnum>` (default `Input`, affecting only the "Other" free-text box) — controls whether the bound value commits on every keystroke (`oninput`) or only on blur/Enter and only when changed (`onchange`), trading per-keystroke reactivity for fewer render cycles (and, on Blazor Server, far fewer round-trips) for consumers who don't need it.
  - `FormDefaults` gains a matching `UpdateOn` (plus a public `EffectiveUpdateOn` chaining through nested `FormDefaults`, same pattern as the other settings); there's no `FormOptions` counterpart, same as `AssetBase`.
  - `onblur`/`onkeydown` aren't offered as trigger options: Blazor's value binder is an `EventCallback<ChangeEventArgs>`, but those two DOM events dispatch `FocusEventArgs`/`KeyboardEventArgs` instead, which would throw an invalid cast at dispatch — `Change` already covers "commit on blur" for text inputs since a text `<input>`'s own `change` event fires on blur whenever the value changed.
  - `EditNumber`/`EditDateNative` default to `Change` rather than `Input` because a partial value (`-`, `3.`, `1e`, a half-typed date) makes the browser report `type="number"`/`type="date"` as an empty string mid-type, which would flash a spurious validation error on every keystroke under `Input`. `EditTextArea`'s `AutoSize` still grows live while typing under `Change`, via a separate measure-only `oninput` handler that runs independently of the value commit.
  - A different axis from the existing `DebounceMilliseconds` on `Select`/`EditSelectSearch`/`EditMultiSelect`, which debounces the option-filter, not the value commit. See [Commit timing](#commit-timing-updateon).
- New `[Placeholder("...")]` attribute (`Controls.Helpers`, alongside the existing `[Description]`/`[ToolTip]`) so a control's placeholder/hint text can live on the model property next to the field it describes instead of being repeated at every markup site.
  - `AttributesHelper.Placeholder()` resolves it first, then falls back to DataAnnotations' own `[Display(Prompt = "...")]` (via `GetPrompt()`, so a localized `[Display(Prompt = ..., ResourceType = ...)]` resolves too) — universal precedence, every control that has one: its own `Placeholder` parameter → `[Placeholder]` → `[Display(Prompt)]` → the control's built-in default.
  - Honored by `EditString`/`EditTextArea`/`EditNumber<T>` (the rendered `placeholder` attribute), `EditDate<T>` (forwarded to the inner picker, still falling through to its own mode-derived default when nothing resolves), `EditDateRange` (`StartPlaceholder`/`EndPlaceholder` resolve independently against each bound property's own attributes), and `EditSelectSearch<TValue>`/`EditMultiSelect<TValue>` (shown while nothing is selected, falling back to the literal "Please select").
  - `EditSelectEnum<TEnum>`/`EditSelectString<TValue>` have no native `placeholder` attribute to render onto — the text instead goes on the leading blank option (when one renders) and on a hidden "unmatched value" option that supplies the closed `<select>`'s displayed text; on a non-nullable enum whose current value is already a defined member, no blank option renders at all, so nothing shows.
  - Deliberately not wired: `EditDateNative<T>` (browsers ignore `placeholder` on native date/time inputs), the `EditRadio*` "Other" free-text box, `EditFile`, `EditSelect<TValue>`, the checkbox lists, `EditBool*`, `EditDisplay`, and the UI-kit widgets (none are model-bound). Carries one signature change on the two searchable selects — see **Changed** below. See [Model-declared placeholders](#model-declared-placeholders-placeholder).
- New `[MinValue(...)]`/`[MaxValue(...)]` attributes (`Controls.Helpers`, alongside `[Placeholder]`) declare a control's bounds on the model property they constrain. Three constructors — `(int)`, `(double)`, `(string)` (invariant-culture text, the only way to express a date bound like `[MinValue("2024-01-01")]` or a precise `decimal`) — cover every bound type.
  - Unlike `[Placeholder]`, they're genuine `ValidationAttribute`s: `[MinValue(0)]` both renders the browser-side `min` and rejects an out-of-range value at validation time (null passes — that's `[Required]`'s job), with default messages "The {0} field must be at least {1}."/"The {0} field must be no more than {1}." (override via `ErrorMessage` as usual).
  - DataAnnotations' own `[Range]` is honored as a fallback with no second attribute needed — including `[Range(typeof(DateTime), "2024-01-01", "2024-12-31")]` — and a `[Range]` bound spelling "no bound" — anything unrepresentable as `decimal` (the ubiquitous `[Range(0, double.MaxValue)]` idiom) plus the `int`/`long`/`decimal` extremes (`[Range(int.MinValue, 100)]`) — is treated as unbounded rather than clamped, so those attributes alone render the one real bound and omit the other, agreeing with the one-sided message `ValidationHelper` already rewrites those sentinels into. An explicit `[MinValue]`/`[MaxValue]` is never sentinel-suppressed (one-sided by design, so an extreme written there is intentional). An unparseable/misconfigured bound degrades to no rendered bound and no validation error.
  - Uniform precedence, every wired control: its own `Min`/`Max` parameter → `[MinValue]`/`[MaxValue]` → `[Range]` → none.
  - Wired: `EditNumber<T>` (the rendered `min`/`max`); `EditDate<T>` (forwarded to the inner picker, date-granularity, ignored in `Time` mode); `EditDateNative<T>` (**new** `Min`/`Max` parameters, `DateTime?`, same shape as `EditDate`'s — its first bounds support ever — rendering the native input's own `min`/`max` formatted to match `Type`, also omitted in `Time` mode); and `EditDateRange` (`Min` resolves param → Start's attributes → End's; `Max` resolves param → End's attributes → Start's, so a `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` pairing, or a single `[Range]` on `Start`, supplies both ends).
  - Not wired: `EditString`/`EditTextArea` (length limits are `[StringLength]`/`[MaxLength]`'s job), the selects/radios/checkbox lists, `EditBool*`, `EditFile`, `EditDisplay`, and the UI-kit widgets (not model-bound). See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
- **Every remaining field-semantic markup parameter now has a model-attribute counterpart, completing the `[Placeholder]`/`[MinValue]`/`[MaxValue]` pattern.**
  - Five new `Controls.Helpers` attributes:
    - `[Autocomplete("email")]` → `EditString.Autocomplete` (falls back to the built-in `"one-time-code"`).
    - `[Step(0.01)]`/`[Step(1)]`/`[Step("0.01")]` (same `(int)`/`(double)`/`(string)` three-constructor shape as `[MinValue]`/`[MaxValue]`, string = invariant decimal text) → `EditNumber.Step` (default `1.0`; a non-positive or unconvertible value is ignored, same lenient philosophy as the Min/Max bounds).
    - `[BoolText(TrueText = "Enabled", FalseText = "Disabled", NullText = "Unknown")]` → `EditBool` (`TrueText`/`FalseText`, read-only view) and `EditBoolNullRadio` (all three radio labels + read-only) — defaults stay `"Yes"`/`"No"`/`"Not Set"`, each property independently optional.
    - `[Rows(4)]` or `[Rows(2, AutoSize = true, MinRows = 2, MaxRows = 10)]` → `EditTextArea` `Rows`/`MinRows`/`MaxRows`/`AutoSize` (`0` = unset for the ints, since an attribute can't hold a nullable int; `AutoSize = false` is indistinguishable from unset, which is harmless since `false` is already the default).
    - `[FileConstraints(AllowedExtensions = new[] { ".pdf", ".png" }, MaxFileSizeBytes = 5242880, MaxFiles = 3, MaxTotalBytes = 10485760)]` → `EditFile` (`0`/`null` = unset; defaults stay 10 MB per file, 100 MB total, unlimited count, any extension — also drives the rendered `accept` attribute and the "Supported formats" hint).
  - Four more standard DataAnnotations attributes are now also honored for a rendering effect, not just validation, no new attribute needed:
    - `[StringLength(100)]`/`[MaxLength(100)]` → rendered `maxlength` (and the "n / 100" `ShowCount` text) on `EditString`/`EditTextArea`.
    - `[DataType(DataType.Password)]` → `EditString` renders `type="password"` with the reveal toggle (`IsPassword` fallback).
    - `[DisplayFormat(DataFormatString = "{0:N2}")]` (a composite `"{0:X}"` or bare `"X"` both accepted) → `EditNumber.Format`, `EditDate`'s `Format`/`DateFormat`, `EditDateNative.DateFormat`, `EditDateRange`'s `Format`/`DateFormat` (reads the **Start** field's attributes first, then **End**'s).
    - `[DataType(DataType.Date/DateTime/Time)]` → `EditDate.Type`/`EditDateNative.Type` (`Date`/`DateTimeLocal`/`Time`).
  - Uniform precedence throughout, same shape as `[Placeholder]`/`[MinValue]`/`[MaxValue]`: the control's own markup parameter → the model attribute (custom or DataAnnotations) → the control's built-in default.
  - Deliberately has no model-attribute counterpart: delegates/`RenderFragment`s/`EventCallback`s, runtime state (`IsDisabled`, `Open`, `Indeterminate`), view composition (`Size`, `Width`, CSS classes, `IsHorizontal`), form-level localization strings (picker labels, `*MessageFormat` strings — use `FormDefaults`/markup instead), and runtime data (`Options`, `Presets`).
  - Carries a set of nullable-parameter signature changes — see **Changed** below. See [Model-declared field attributes](#model-declared-field-attributes-autocompletestepbooltextrowsfileconstraints).

- `EditDateRange` gains `ParsingErrorMessage` (`string`, default `"The {0} field must be a date."`) — parity with the `EditDate<T>` parameter above, applied to a two-field control.
  - `{0}` is the **failing endpoint's** own field name, so one format string serves both inputs; the message goes into a dedicated `ValidationMessageStore` scoped to that endpoint's `FieldIdentifier`, and each endpoint's message clears independently the moment a valid value next commits for *that* endpoint.
  - Same trigger contract as `EditDate`'s: only text that fails to parse as a date at all, never a well-formed value merely rejected by `Min`/`Max`/`DisabledDate`/`StartDisabledTime`/`EndDisabledTime`. Previously an unparseable entry in either input was silently reverted with no feedback. Built on two new `DateRangePicker` callbacks — see **New (UI Kit)** below. See [Available Controls](#input-controls).
- `EditDateRange` gains `Size` (`SelectSize`: `Default`/`Small`/`Large`) — the parameter `EditDate<T>`, `DatePicker` and `DateRangePicker` all gained above, now declared on the form control too and forwarded to the inner `DateRangePicker` (`wss-picker-sm`/`wss-picker-lg` on the wrapper; `Default` adds no class). Also closes a footgun: with no `Size` parameter of its own, a consumer's `Size="Small"` fell into `AdditionalAttributes` and splatted the raw string onto the picker's *enum*-typed `Size` parameter instead of sizing the control.
- `EditMultiSelect<TValue>` gains `Variant` (`SelectVariant`, default `Outlined`) — the parameter `EditSelectSearch` already forwarded, now reachable for `Multiple`/`Tags` mode too, so the `Pill` and `Borderless` trigger looks are no longer single-select-only. Purely additive: `Outlined` renders exactly as before. See [Pill filter variant](#pill-filter-variant-select--editselectsearch).

**New** (UI Kit)
- `DatePicker` and `DateRangePicker` both gain `Size` (`SelectSize`) — the same parameter `Select` already has, rendering `wss-picker-sm`/`wss-picker-lg` on the outer wrapper (`Default` byte-identical to before). `EditDate<T>` forwards it (see above).
- `DateRangePicker` gains `OnStartParseError`/`OnEndParseError` (`EventCallback<string>`, raised with the offending text) — per endpoint rather than one callback carrying which side failed, matching every other per-input parameter on this control (`StartPlaceholder`/`EndPlaceholder`, `StartDisabledTime`/`EndDisabledTime`, the `StartAria*`/`EndAria*` pairs), since a host form control needs the two apart to target each field's own `FieldIdentifier`.
  - Raised only on a genuine parse failure at a typed commit (Enter or blur), never for a well-formed value the picker rejects on `Min`/`Max`/`DisabledDate`/`*DisabledTime` grounds — that's a different situation the picker has always handled by reverting. The picker itself has no validation concept; these exist so `EditDateRange` can surface a message it can't (see **New (Edit Controls)** above).
  - Optional and additive: with no handler attached a standalone `DateRangePicker` behaves exactly as before, silently reverting the unparseable text to the formatted bound value.
- `Select` gains an `AriaErrorMessage` parameter (same shape as its existing `AriaRequired`/`AriaInvalid`/`AriaDescribedBy` trio) — forwarded by `EditSelectSearch`/`EditMultiSelect` as `IsInvalid ? _errorMsgId : null`, the same pattern `EditDate` already uses onto `DatePicker`.
  - `EditFile`'s `<InputFile>` gains an `aria-errormessage` too, but keyed off `IsInvalid` (EditContext validation) rather than the `_hasError` flag that drives its `aria-invalid`: a pure upload-time-only rejection (bad extension, duplicate, over a cap) sets `_hasError` without ever populating the `error-msg-{id}` element `FieldValidationDisplay` renders, so pairing `aria-errormessage` with `_hasError` would point assistive tech at that element while it's empty.
- `Select<TValue>` gains `AdditionalAttributes` (`IReadOnlyDictionary<string, object>?`, capturing unmatched values), splatted onto the engine's outer wrapper. Previously an unmatched attribute on `<Select>` threw at render time, and `EditSelectSearch<TValue>` — which forwards everything to this engine — had nowhere to put a consumer's `data-*`/`title`/`aria-*`.
  - `class` and `style` deliberately stay out of that splat: `CssClass` is the wrapper's single class channel, and its inline `style` is JS-owned while the dropdown is open (the open-order z-index). See **Fixes (Edit Controls)** below.
- **`Table<TItem>` gains eight "Override to localize" label parameters, covering the last user-facing strings that had no override:**
  - `SelectRowLabel` (`"Select row"`), `SelectAllRowsLabel` (`"Select all rows"`), `SelectAllRowsOnPageLabel` (`"Select all rows on this page"` — used instead of `SelectAllRowsLabel` whenever the table is paged).
  - `PaginationLabel` (`"Pagination"`), `TopPaginationLabel`/`BottomPaginationLabel` (`"Pagination (top)"`/`"Pagination (bottom)"`, used only when `PagerPosition="Both"` so the two pagers stay distinguishable).
  - `SortLabel` (`"Sort"` — the sort button's accessible name on a column with no `Title`), and `FilterLabel` (`"Filter"` — the exactly-parallel fallback for a title-less column's filter button; a column with a `Title` still uses `FilterButtonLabelFormat`).
  - Previously a fully localized table still announced its own selection checkboxes, pagers, sort and filter buttons in English. Every default renders byte-identically to the literal it replaced, so nothing changes unless you set one.

**Changed** (Edit Controls)
- **`UpdateOn` was deliberately not carried over to the new `EditDate<T>`.** It remains on `EditDateNative<T>` (choosing `oninput` vs. `onchange` for a text input) but has no equivalent on the calendar dropdown: a picker commits on selection, or on parse at blur/Enter, so there is no per-keystroke commit to opt into. If you set `UpdateOn` on the pre-10.7.0 `EditDate` (the native input) and want that behavior back, switch that field to `EditDateNative`. See [Commit timing](#commit-timing-updateon).
- **Default checkbox/radio label spacing now matches AntD's 8px gap — a visible default-rendering change every consumer will see.**
  - `.edit-checkbox-label`/`.edit-radio-label` previously relied on native whitespace-collapse spacing between the `<input>` and its label text unless `UseStyledCheckbox` opted into the flex/gap layout; that 8px flex-row gap (AntD's checkbox/radio spec) is now the default for **every** checkbox and radio label — `EditBool`, `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, `EditRadioString`, `EditRadioEnum<TEnum>`, and `EditRadio`'s consumer-authored `<label>`s that carry `edit-radio-label` — regardless of `UseStyledCheckbox`.
  - Every checkbox/radio list in every consuming app will render with a touch more space between the box and its text after upgrading; nothing to opt into or configure, and no markup changes are required. `.edit-checkbox-label-styled` no longer carries its own layout — it's now an empty marker class kept only as a stable hook for consumers who target the styled variant specifically.
- **Signature change:** `EditSelectSearch.Placeholder` and `EditMultiSelect.Placeholder` are now `string?` (previously `string`, defaulted to `"Please select"`) as part of the new `[Placeholder]` resolution chain above — markup usage is unaffected, but C# code reading `.Placeholder` should account for the type change.
- **Signature change:** the parameters newly resolved via the model-attribute pattern above changed from non-nullable to nullable so "unset" is detectable — markup usage (`Rows="4"`, `Step="0.01m"`, `Type="InputDateType.Time"`, ...) is unaffected, only C# code reading the component instance's property directly needs to account for the type change:
  - `EditString.IsPassword` (`bool?`), `EditString.Autocomplete` (`string?`), `EditTextArea.Rows`/`AutoSize` (`int?`/`bool?`), `EditNumber.Step` (`decimal?`), `EditDate.Type`/`EditDateNative.Type` (`InputDateType?`), `EditDateNative.DateFormat` (`string?`), `EditBool.TrueText`/`FalseText` (`string?`), `EditBoolNullRadio.TrueText`/`FalseText`/`NullText` (`string?`), and `EditFile.MaxFileSizeBytes`/`MaxFiles`/`MaxTotalBytes` (`long?`/`int?`/`long?`).
  - Defaults are unchanged — each is resolved in an `Effective*`/`Resolved*` property, not the parameter itself.
- **A required `EditBool` now shows the required star — a visible change wherever a checkbox is bound to a `[Required]` property or sets `IsRequired`.**
  - Checkbox mode's hand-rolled label had drifted from `FormLabel` by omitting the star while still announcing `aria-required` to assistive tech, violating the library's documented "the star and `aria-required` can never disagree" invariant; the checkbox label now renders through `FormLabel` itself (via two new additive optional `FormLabel` parameters, `NestedInput` and `LabelClass`), so the star, description, tooltip and hidden-label behavior are structurally identical to every other control. The label also carries `id="lbl-{id}"` now, like every sibling.
- **`EditRadioString`'s "Other" free-text box now renders styled and laid out like `EditRadioEnum`'s — a visible change wherever `HasOther` is set.**
  - Its input had drifted onto the empty `.edit-string-input` class (no border, min-width, or disabled affordance) and lacked the flex row wrapper, so it rendered bare and stacked below its radio. Both controls now share one internal `RadioOtherInput`; DOM ids and each control's commit wiring are unchanged, and the long-standing `.edit-radio-other-option-container` consumer hook is retained.
- **`HidingMode.WhenNullOrDefault`/`WhenReadOnlyAndNullOrDefault` now treat "default" uniformly on the native selects, per the mode's documented contract.**
  - `EditSelect<string>` bound to `""` previously stayed visible (every other string-bound control hid), and `EditSelectString<TValue>` with a non-string `TValue` at `0`/`false` previously stayed visible (its check stringified the value). Both now union the base value-type default check with the empty-string case.
  - Only affects consumers using these hiding modes with those exact type/value combinations. `EditSelectSearch<TValue>` is now aligned too — see **Fixes (Select engine)** below.
- **A read-only control whose label is hidden now keeps its accessible name — a change wherever `IsLabelHidden` (or `FormOptions.IsLabelHidden`) is combined with read-only mode.**
  - The read-only value previously dropped `aria-labelledby` in that case, on the premise that a hidden label has no element to point at. It does: `FormLabel` still renders the `lbl-{id}` element, visually hidden, so the reference never dangled — omitting it just left the value with no accessible name at all, which assistive tech reads as an unlabeled blob.
  - Now applied consistently across every read-only view: each `ReadOnlyValue` host (via the new `HasLabelElement` parameter — see **Breaking** above), `EditString`'s masked and link views, and `EditFile`'s read-only file list (whose stand-in `aria-label="Selected files"` is dropped in favor of the real field name; the *edit*-mode list keeps that literal, since it isn't the field's own value display). The four radio fieldsets (`EditRadio`, `EditRadioEnum<TEnum>`, `EditRadioString`, `EditBoolNullRadio`) gain the same unconditional `aria-labelledby` in edit mode, from one shared attribute block.
  - Nothing visual changes; screen-reader output for those combinations does.
- **`EditMultiSelect<TValue>`, `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, and `EditFile` now apply unmatched attributes to their root `.edit-control-wrapper`.**
  - A `style`, `data-*`, `title`, or any other stray attribute written on one of these four was previously captured and silently dropped — they are `ComponentBase`-derived (not `InputBase`), so nothing splatted the captured dictionary anywhere. It now lands on the wrapper, with the component's own inline `style` hand-merged (consumer last, so its declarations win) and the splat emitted first so the explicit `class` still wins.
  - **`class` is unchanged** — it keeps flowing to the field element (the select engine/checkbox fieldset/drop zone) alongside Blazor's field-state classes, same as every other control; `ContainerClass` remains the wrapper's class channel. The only visible difference is that attributes which used to vanish now render.
- **`EditRadioString` now wraps every default-mode option in `<div class="edit-radio-option">` — a DOM change wherever it's used without `OptionType="Button"`.**
  - `EditRadioEnum<TEnum>` already did; `EditRadioString` wrapped only its "Other" row, even though `edit-controls.css` documents `.edit-radio-option` as the universal per-option row. The two controls' default-mode markup is now identical in shape.
  - Nothing visual changes for the built-in stylesheet (the class's own rules already applied to `EditRadioEnum`'s rows); **consumer CSS or test selectors written against `EditRadioString`'s previously-unwrapped options may need updating**, and a consumer stylesheet that targets `.edit-radio-option` now reaches this control's options too.
- **The "Other" radio option behaves identically in `EditRadioString` and `EditRadioEnum<TEnum>` — a behavior change in both.**
  - They used to do opposite things when the user switched away from Other: `EditRadioString` wiped the typed text (an accidental mis-click was unrecoverable), `EditRadioEnum` never cleared it (a stale `OtherValue` rode along on the model attached to a non-Other choice).
  - Both now **preserve the text in the box but take it off the model**, and re-commit it when Other is selected again — so a mis-click is recoverable and nothing stale is ever submitted. An empty `OtherValue` arriving from the parent *while Other is still selected* is treated as a genuine clear (form reset, record reload) and drops the preserved copy.
  - No new parameter — the behavior is not configurable, on the grounds that only one of the two old behaviors was ever correct.
- **Signature change:** `EditDisplay` now derives from `EditControlParametersBase` instead of re-declaring its own copy of the shared parameter set (which had already drifted from the base once).
  - Markup usage is unaffected, but the inherited parameters are **nullable** where `EditDisplay`'s own copies were not — `Label`, `Id`, `IdPrefix`, `Description`, `Tooltip`, `ContainerClass` are now `string?` and `IsRequired` is `bool?`, so C# code reading the component instance's properties directly needs to account for it.
  - `EditDisplay` also formally implements `IEditControl` for the first time and picks up three inherited parameters its markup never reads (`IsEditMode`, `IsDisabled`, `Hiding`) — inert, since this control has no field or editor to affect. Rendered output is unchanged.
- **`EditString`/`EditTextArea`/`EditNumber<T>`'s legacy (non-affix) right padding moved from an inline `style` to an `edit-input-legacy-padding` class.**
  - All three hand-duplicated `style="padding-inline-end: 2rem"` on their editor element; it's now one rule in `edit-controls.css`, specificity/order-matched against the `.edit-theme` base chrome so it still wins inside an opt-in themed scope.
  - **Computed style is unchanged**, but the rendered markup is not — a test selector or consumer stylesheet keyed off that inline style needs the class instead. Also makes `EditTextArea`'s `style` attribute exclusively JS-owned again (`AutoSize` writes height/`overflow-y` there), which the library's own conventions require.
- **Unthemed default text colors shift slightly.**
  - `--color-text` and `--color-text-secondary` were each consumed with three different literal fallbacks across `edit-controls.css`, violating the file's own single-fallback policy; they're unified to `#262626` and `#8c8c8c` (the majority values). Visible only when a consumer sets neither bridge variable: `EditFile`'s drop-zone instructional text, format hint and file-size text, the button-mode radio group/`EditFile` "Choose Files" button text, and the `LabelTooltip` trigger icon.
  - **`--edit-color-danger` was deliberately *not* unified with `--wss-color-error`** — its `#cf1322` fallback (~5.9:1 on white) is a recorded WCAG 1.4.3 decision, because it backs validation message/summary **text** (needs 4.5:1) while the `wss` token backs chrome/borders/backgrounds (WCAG 1.4.11, needs only 3:1). A consumer's own `--color-danger` still overrides both identically; a cross-reference comment now sits on each so the divergence isn't "fixed" again.
- **`EditFile`'s icons now follow the `--edit-*` tokens.**
  - The upload, upload-error, upload-button and delete glyphs baked their fill color directly into an SVG data URI, so a themed consumer's colors never reached them — the error icon in particular baked the *fallback* of `--edit-color-danger` rather than the token.
  - They now use the mask + `background: var(...)` technique the styled checkbox already used: the error icon consumes `var(--edit-color-danger)` directly, and the three teal ones get a new usage-site `--edit-color-file-icon` knob (defaulting to the existing `#277c6c`; no generic `--color-*` bridge represents that brand teal, so per the theming contract it's declared only at its usage sites, never at `:root`). Pixel-neutral for unthemed consumers.
- **Both show/hide toggles keep a constant accessible name now, with `aria-pressed` carrying the state — a test-selector break for anything that queried them by name.**
  - `EditString`'s read-only masked-row eye stays `aria-label="Show value"` in both states (it gained `aria-pressed`), and `EditInputShell`'s password toggle is now `aria-label="Show password"` in both states (it already had `aria-pressed`), replacing the `"Show value"`/`"Hide value"` pair.
  - A toggle whose name *and* pressed state both flip is the classic ambiguous toggle — "Hide value, pressed" reads as "hiding is already in effect" to some users and "press to hide" to others; the fix is to name the action and let `aria-pressed` carry the state.
  - **Consumer action:** update any Playwright `GetByRole("button", new(){ Name = "Hide value" })`/`GetByLabel(...)` selector.
- **A hidden label keeps its description — a change wherever `IsLabelHidden` (or `FormOptions.IsLabelHidden`) is set on a field that has a `Description` or `[Description]`.**
  - `FormLabel`'s hidden branch dropped the description element entirely, so `aria-describedby` also dropped `desc-{id}`: hiding a label is a layout decision (the field sits under a column header, say) and it was silently deleting the field's format instructions for *every* user.
  - The description now renders as a visually-hidden `<p class="edit-sr-only" id="desc-{id}">` alongside the hidden label, and `desc-{id}` stays in `aria-describedby`. The **tooltip** is still dropped in that mode, deliberately: it's an interactive hover/focus widget with no trigger rendered, so `tooltip-{id}` would dangle. Nothing visual changes.
- **The visible validation-message container is now `aria-hidden="true"`.**
  - `FieldValidationDisplay` renders every message twice — once in the `error-msg-{id}` sr-only live region (field-named, for announcement) and once visibly (concise) — and a screen reader in browse mode walked through both, reading each error twice.
  - The visible copy holds text only (no focusable content), so hiding it from the accessibility tree is safe. Only affects AT output; a `.edit-validation-message:not(.edit-sr-only)` test selector still works.
- **`ShowCount` now has an assistive-tech half — `EditString` and `EditTextArea` with `ShowCount` gain a `count-{id}` token in `aria-describedby` and two visually-hidden spans.**
  - The visible count span is `aria-hidden="true"` (`"12 / 100"` is a visual shorthand a screen reader renders as "twelve slash one hundred", and it was never part of the field's description — orphan noise in browse mode, absent on focus).
  - In its place: `<span class="edit-sr-only" id="count-{id}">12 of 100 characters</span>`, referenced by the field, plus a `role="status"` region that stays silent until the last `min(10, 10% of the max)` characters and then announces `"N characters remaining"`/`"Character limit reached"` (`maxlength` truncates a paste with no other signal).
  - Fields without `ShowCount`, and the read-only views of fields with it, keep a byte-identical `aria-describedby`.
- **`EditString`'s read-only link is named by the field label *and* its own text now, and `_blank` says so.**
  - `aria-labelledby="lbl-{id}"` overwrites an element's own text rather than adding to it, so every URL field announced as just its label ("Email") and never the destination — two same-labeled URL fields in a list were indistinguishable.
  - It is now `aria-labelledby="lbl-{id} {id}"` (a legal self-reference that concatenates the link text). `UrlTarget="_blank"` additionally renders a visually-hidden `(opens in new tab)` inside the `<a>`, which joins the name for free; a *named* target doesn't get it, since it may reuse an already-open context.
- **`EditString`'s read-only masked row is a `role="group"` and carries the field's `aria-describedby`.**
  - A bare `<div>` is role-generic, where ARIA prohibits naming — its `aria-labelledby` was inert. The row genuinely groups a value with the control that reveals it, so `group` is both accurate and nameable.
  - It also gained a visually-hidden `role="status"` child announcing `"Value shown"`/`"Value hidden"` on toggle — the state only; the masked value itself is never routed through a live region.

**Changed** (UI Kit)
- **`Table.OnFilterChanged` gains a fourth trigger — a deliberate behavior change.**
  - When a swapped or refilled `FilterOptions` no longer offers a value that column was actively filtering on, the orphaned value is pruned (see **Fixes (UI Kit)** below) and the prune now **raises `OnFilterChanged` with the surviving selection**.
  - It was initially made silent, on the reasoning that the event reports user intent rather than the consumer's own parameter change; that was reversed because `Table` already raises the event for the exactly-parallel case of a filtering column dropping out of the rendered set, with a comment explaining that staying silent left the consumer's own filter summary showing a value that no longer narrows anything. The prune cannot loop (the new options snapshot is captured first) and stays quiet when it removed nothing.
  - **Consumer action:** an `OnFilterChanged` handler that assumed "this only fires from a user's OK/Reset" may now see an event it didn't cause — the payload is the authoritative surviving selection either way.
- **`DateRangePicker`'s Time/DateTime pick-session OK button now renders `disabled` (and `aria-disabled`) whenever confirming the current pending pair would be rejected.** It previously rendered enabled and then did nothing at all on click — no close, no advance to the other endpoint, no callback, no feedback of any kind. One `SessionOkDisabled` predicate now drives both the attribute and the handler, so the two can't disagree, matching the convention every disabled day button and the Now/Today links already follow.

**Fixes** (Edit Controls)
- **Controls now unregister from `FormOptions` when disposed.**
  - A scalar control (or `EditRadio`) removed from the render tree behind an `@if` — a tab switch, a collapsed section, a closed modal — left its field registered forever: `ValidationView` kept rendering a summary link to an element no longer in the DOM, and the per-form registration list grew with every mount/unmount cycle. The list controls already unregistered; every control now does, through one shared register/unregister pair.
  - Relatedly, `EditBool` — the only `InputBase`-derived control implementing `IAsyncDisposable` — never ran its synchronous dispose at all (Blazor treats the two dispose interfaces as exclusive), which also skipped its validation-event unsubscribe; its `DisposeAsync` now invokes the synchronous chain.
- Guarded the new flex checkbox/radio labels against two common consumer CSS resets exposed by the 8px-gap change above:
  - An app-level `input { flex: ... }` rule no longer stretches the plain checkbox/radio `<input>` across the row (the direct-child input is now pinned `flex: none; margin: 0`), and an `input[type=checkbox] { margin-top: -3px }` baseline nudge written for the old whitespace-based layout no longer shoves the box off-center against the row's `align-items: center` — both guards carry an `[type=...]` specificity bump so load order can't decide it.
  - Demo `EditRadio`'s hand-rolled `<label>`s now carry `edit-radio-label` to match what the control's own markup produces.
- **`Size="Small"`/`"Large"` now actually sizes a legacy-mode (non-affix) input inside `.edit-theme`** — a visible change wherever the two were combined.
  - `.edit-theme`'s `edit-input-sm`/`-lg` rules lost a specificity tie to the base chrome rule (`0,3,0` vs `0,4,0`), so the size class was a silent no-op there for `EditString`/`EditNumber`/`EditTextArea`/`EditDateNative` unless an affix parameter had already switched the control into the wrapper-sized affix layout.
  - The size selectors now carry the same `:not([type="checkbox"])` qualifier as the base rule, tying at `0,4,0` and winning on source order. Affix-mode and un-themed rendering are unchanged; see [Opt-in AntD theme](#opt-in-antd-theme-for-the-classic-edit-inputs-edit-theme).
- **An enum display name declared with `[Display(Name = ..., ResourceType = ...)]` now re-resolves per culture on every render.**
  - `EnumHelpers.GetName` memoizes per (enum type, member name), which froze a resource-backed name at whichever culture happened to render first — process-wide, so on Blazor Server one circuit's language leaked into every other user's.
  - Only the "this member is localized" decision is cached now; the name itself goes back through `DisplayAttribute.GetName()` (and therefore `CultureInfo.CurrentUICulture`) each call. Non-localized `[EnumDisplayName]`/`[Display(Name = "literal")]` names stay fully memoized — no per-render cost added for the common case. Affects every enum-driven control: `EditSelectEnum<TEnum>`, `EditRadioEnum<TEnum>`, `EditCheckedEnumList<TEnum>`, and the read-only displays derived from them.
- **A property decorated with `[Display(Name = "...")]` now gets its validation messages rewritten like every other property.**
  - `ValidationHelper`'s rewrites (`Required`, `StringLength`, `MinLength`, `MaxLength`, "must be a number") are deliberate exact-string matches against the framework's own message text — proof that DataAnnotations produced *this* message for *this* field with *these* bounds, which is what makes replacing it safe.
  - DataAnnotations formats with `ValidationContext.DisplayName`, i.e. the `[Display(Name)]` spelling, so a decorated property's message ("The Given Name field is required.") matched none of the member-name candidates and rendered as the raw framework sentence instead of "Required".
  - `FieldValidationDisplay` now also passes that spelling — resolved via `GetName()`, so a localized `[Display(Name = ..., ResourceType = ...)]` works too — and both spellings are tried. `ValidationHelper.GetValidationMessage` gained an overload taking a `displayName` argument for this; the existing overload is kept and forwards `null` (member name only), so external callers are unaffected. `[DisplayName]` needs nothing here and is unchanged — DataAnnotations doesn't read it, so those messages always carried the member name already.
- **`edit-controls.css`'s `prefers-reduced-motion` block now sits last in the file.**
  - It previously sat mid-file, before an equal-specificity `.edit-theme` input-chrome rule that re-declares the same transition — on a cascade tie source order decides, so themed input chrome kept animating under reduced motion despite the block listing it. Matches `wss-controls.css`'s existing block-goes-last convention.
  - Caught by the new `ReducedMotionE2ETests`; together with `RtlE2ETests` (RTL logical padding/margin flips on the pagination/picker selects and tooltip offsets), these suites pin behaviors that were previously verified only by reading the CSS.
- **A `[Range]` whose bounds are *both* no-bound sentinels (`[Range(int.MinValue, int.MaxValue)]`, used purely to trigger numeric parsing validation) no longer leaks the raw framework message with the numeric extremes spelled out.**
  - The range-message rewrite handled the one-sided and neither-sentinel cases but fell through when both bounds were sentinels — the exact case the suppression exists for. It now falls back to the same "Must be a number" wording used for numeric parse failures.
  - Sentinel detection stays culture-aware (the bounds are compared as the validation-time culture formats them), covering `int`/`long`/`decimal`/`double`/`float` — and is gated to the bound property's *own* type (`Nullable<T>` unwrapped): `[Range(int.MinValue, int.MaxValue)]` still collapses on an `int`, but the same attribute on a wider type (e.g. a `long`) is a genuine "must fit in an int" constraint and renders the full bounded range instead of vanishing into "Must be a number"; a mixed pair like `(int.MinValue, long.MaxValue)` on a `long` renders one-sided.
  - The gating lives in the shared sentinel predicate the DOM `min`/`max` rendering also uses (`EditNumber<T>` supplies its own numeric type), so the message and the rendered bounds always agree; an extreme also counts as a sentinel on a *narrower* type that cannot reach it (`[Range(0, int.MaxValue)]` on a `short` stays one-sided).
- `IdPrefix=""` no longer produces ids like `-FirstName`. The prefix (and the `FormGroupOptions.Name` prefix beside it) is applied only when non-empty; an empty string is now indistinguishable from unset. A leading hyphen makes an id that `document.querySelector("#-FirstName")` rejects outright, which broke the `ValidationView` summary links and `FocusFirstInvalidField` for the whole form.
- **Checkbox and radio option lists now de-duplicate their option ids.**
  - Each option's id segment comes from a sanitizer that strips everything outside `[A-Za-z0-9-_]`, so a list of non-ASCII labels (all-CJK options, say) collapsed to the same empty segment for every entry — and with duplicate ids, every `<label for>` resolved to the **first** input, so clicking any label toggled the first option.
  - `EditCheckedStringList`, `EditCheckedEnumList<TEnum>`, `EditRadioString`, and `EditRadioEnum<TEnum>` now route their whole option list through a new public `EnumHelpers.ToUniqueIds<T>(IReadOnlyList<T> options, string? reserved = null)` helper (`reserved` keeps `EditRadioString`'s built-in `"other"` segment verbatim).
  - **Ids change only for colliding options** — the first option to claim a sanitized segment keeps it, so an ordinary ASCII list produces exactly the ids it always did (bUnit selectors, e2e locators, and visual baselines all pin those); a collision or an empty segment falls back to the option's index.
- An empty enum (no members) combined with `HasOtherOption` no longer fabricates a phantom `"0"` option.
  - The "Other always sorts last" logic pulls the last member aside before sorting and re-adds it afterwards, gated on a null check — but `TEnum` is unconstrained, so for a value-type enum `default` is the zero *member*, not null, and the guard passed even with nothing to re-add. The extraction is tracked with an explicit flag now.
  - `EditRadioEnum<TEnum>`'s read-only "is Other selected" check is guarded for the same empty list (it indexed the last element unconditionally).
- `EditSelectEnum<TEnum>` ports `EditSelectString`'s no-JS `selected=` fallback: under static/no-JS rendering (prerender, bUnit) a nullable `EditSelectEnum` with a null or unmatched value used to visually show the first enum member selected — no `<option>` carried a `selected` attribute, relying entirely on JS to set the DOM value — until JS attached, even though the bound value was null/unmatched.
  - A hidden, disabled placeholder `<option>` now also covers a non-nullable enum whose current value has no defined member (e.g. a removed enum value read back from storage), the same gap `EditSelectString` already guarded against.
- **Security: `EditString`'s read-only link mode now mirrors the browser's full href preprocessing — trimming leading/trailing C0 control-or-space, then stripping ASCII tab/CR/LF — before checking the URL scheme, closing two `javascript:` bypasses of the http/https/mailto allow-list.**
  - `Uri.TryCreate(Url, UriKind.Absolute, ...)` fails to parse a URL carrying a tab/newline inside or right after its scheme (e.g. `java<TAB>script:alert(1)`), *or* a URL with a leading C0 control byte (e.g. a leading `U+0001` before `javascript:alert(1)`) — a C0 control isn't a valid scheme-start character either. The old code treated anything unparseable as a safe relative URL, rendering it into `href` verbatim in both cases.
  - A browser's own URL parser trims leading/trailing C0-control-or-space and strips embedded tab/CR/LF before parsing, then re-forms and runs the `javascript:` URL on click — `SafeUrl` now applies both preprocessing steps, in the browser's order, first, so the allow-list check sees exactly what the browser will see, and renders the fully-preprocessed value (never the raw `Url`) when it passes.
- **Security: `EditString`'s read-only link rejects two further unsafe shapes, and `rel="noopener noreferrer"` now covers *named* `UrlTarget`s, not just `_blank`.** Three fixes to the same branch.
  1. A **protocol-relative** URL (`//evil.example/x`) has no scheme, so `Uri.TryCreate` reported it relative and the "unparseable, therefore a safe relative URL" fall-through rendered it verbatim — while a browser resolves it cross-origin against the page's own scheme. Any two leading slash-or-backslash characters are rejected now, which also covers the `/\`, `\\` and `\/` spellings browsers normalize to it: that fall-through's promise is a *same-origin* relative link, and none of these are one.
  2. A `Url` that **preprocessing empties out** — a lone `U+0001`, say, which clears the `IsNullOrWhiteSpace` guard because a C0 control is not .NET whitespace and then trims away to nothing — rendered an empty `href`, and an empty `href` resolves to the current document, so the "link" silently reloaded the page on click. It returns null now and plain read-only text renders.
  3. `rel` hardening previously applied to `_blank` alone. A **named** target (`UrlTarget="vendor"`) is the case that most needs it: it opens or reuses a separate browsing context whose `window.opener` points back here, so that document can navigate this page out from under the user (reverse tabnabbing) — and unlike `_blank`, which browsers already treat as implicitly `noopener`, nothing covers a named target today. Every target now gets the `rel` except the same-context keywords `_self`/`_parent`/`_top` (matched case-insensitively) and no target at all, where there is no opener to sever and `noreferrer` would needlessly drop the referrer on a navigation inside our own frame tree.
  - See [Read-only views](#read-only-views-editstring). **Consumer action:** a test asserting the *absence* of `rel` on a named-target link needs updating; `_blank` and untargeted links are unaffected.
- **A disabled password field's value could still be revealed, and the reveal state was sticky.**
  - The show/hide toggle rendered with no `disabled` attribute, so a disabled field's secret stayed revealable through a tab stop assistive tech announces as unavailable; and because neither the password-reveal flag nor the masked-value-reveal flag is a parameter, nothing ever reset them — a revealed field that went read-only and back to edit mode, or had `IsPassword` flipped off and on, came back still revealed with no new user gesture, and a control instance reused for a different record without `@key` could show record B's masked value in the clear because the user had revealed record A's.
  - The toggle button now renders `disabled` (and stops responding) whenever the field is, and both flags reset whenever the editor stops being a revealable password box — deliberately not on every value change, which in edit mode is every keystroke.
- **An all-whitespace `MaskText` (e.g. `MaskText="   "`) no longer discloses the raw value in read-only mode.** The masked-view branch tested `IsNullOrWhiteSpace` while the mask value it computed tested `IsNullOrEmpty` — the two guards disagreed, so an all-whitespace mask skipped the masked branch entirely and printed the value in plain text. Both are `IsNullOrEmpty` now, so a whitespace mask masks with spaces, the way the consumer asked.
- **`EditString`'s masked read-only row now carries `CssClass`, like its link and plain-text siblings.** The mask branch was the one read-only view that dropped a consumer's class — and since that channel also carries the `modified`/`invalid` field-state classes and a custom `FieldCssClassProvider`'s output, the one view where those never reached the DOM either.
- **`EditString`/`EditTextArea`'s `Clear()` now writes `string.Empty` instead of `null` — a documented behavior change.**
  - Deleting the text by hand already produced `""`; the clear button wrote a different value for the same gesture, and under `HidingMode.WhenNull` the `null` made the control unmount itself — editor and all — the instant its own clear button was clicked. `Clear()` now matches manual deletion (and AntD) by writing `string.Empty`.
  - Alongside it: `ShowCount`'s live count and `AllowClear`'s button visibility, which read `CurrentValue`, no longer freeze at the pre-typing value under `UpdateOn="Change"` (per-control or cascaded from `FormDefaults`) — a shared `oninput` handler now captures live editor text whenever a live-text feature is active and the bound commit event is `onchange`, dropped again on commit and on a programmatic value change, so the affix-free legacy DOM stays byte-identical when neither applies.
- **`EditNumber<T>` no longer renders `step="1.0"` by default — a documented behavior change.**
  - A fractional value (`12.34m`) arriving with neither an explicit `Step` parameter nor a `[Step]` attribute was natively invalid on arrival under the old hardcoded default, silently blocking a native form submit (`EditForm` emits no `novalidate`) before `OnValidSubmit`/`OnSubmit` ever fired.
  - With neither set, non-integral `T` (`float`/`double`/`decimal` and their nullable forms) now renders `step="any"`, and integral `T` renders no `step` attribute at all (the native default of 1 is already correct) — matching the framework's own `InputNumber<T>`, which never renders `step`. An explicit `Step` parameter or `[Step]` attribute is unaffected and still renders exactly as before.
- `EditTextArea`'s `AutoSize` now re-measures when the bound value changes from outside the control (e.g. a parent loading a record into the model) or when `AutoSize` itself flips on at runtime. Both previously left the textarea clipped at its old height until the next keystroke — measurement only ran on first render and on the user's own typing.
- `EditBool`'s `Indeterminate` now survives a genuine user click. A checkbox click resets the DOM `indeterminate` property to `false` as part of the browser's own pre-click handling, but the control's internal mirror didn't notice, so a checkbox whose `Indeterminate` parameter stayed `true` throughout lost its dash permanently after the first click instead of it being reapplied on the next render.
- `EditFile`'s file input and drop zone no longer carry a hardcoded `aria-label`, letting the associated `<label for>` supply the field's actual accessible name. `aria-label="Choose files"` won by accessible-name precedence over the field's own label, so no `EditFile` ever announced its bound field's name (also a WCAG 2.5.3 Label in Name failure); the role-less drop zone's `aria-label="File upload area"` was also inert (prohibited on an element with no ARIA role) and is removed too.
- `EditFile`'s drop zone no longer wires a managed `dragover` handler — `dragenter`/`dragleave` alone drive the hover highlight, and a handler-less `@ondragover:preventDefault` still lets the drop event fire. `dragover` fires continuously (~60/s) while a file is dragged over the zone; on Blazor Server each one used to ship a serialized `DataTransfer` payload over SignalR for a no-op re-render.
- `edit-controls.js`'s back-compat `window.log`/`window.logError`/`window.logWarn`/`window.logInfo`/`window.focusFirstInvalidField` shims now use `??=` instead of an unconditional assignment, so they no longer clobber a host page's own same-named globals — relevant to a cross-origin MFE that links this file as an ES module import rather than a `<script>` tag.
- `focusFirstInvalidField` now scrolls with `behavior: 'auto'` instead of a hardcoded `'smooth'` when the user has `prefers-reduced-motion: reduce` set, matching the reduced-motion handling both stylesheets already apply elsewhere.
- **A `[Range]` bound at a narrow integer type's extreme is now named in the validation message instead of being suppressed as a "no bound" sentinel — a visible message change.**
  - The rendered `min`/`max` attributes and the range-message rewrite each kept their own sentinel list and disagreed on 8 of the 12 numeric extremes: `[Range(1, 255)]` on an `int Quantity` rendered `max="255"` while the message said only "Must be at least 1" — vacuous for an entry of 300, and silent about the ceiling the user had just violated; `[Range(-32768, 100)]` rendered `min="-32768"` while the message claimed there was no floor.
  - Both layers now resolve through one shared predicate, and the `sbyte`/`byte`/`short`/`ushort`/`uint`/`ulong` extremes are out of it on both sides (at those magnitudes a real bound is far likelier than a vacuous one — see [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue)).
  - What the bound property's type *does* decide is reachability: a recognized `int`/`long`/`decimal`/`float`/`double` extreme is still suppressed on a property that cannot reach it, so `[Range(0, int.MaxValue)]` on a `short?`/`byte?` renders `min="0"` with no `max` and says "Must be at least 0" rather than naming a 2147483647 ceiling the type cannot represent — while the same annotation on a `long?` keeps both bounds, because there it is a real "must fit in an int" constraint.
  - Messages under an `int`/`long`/`decimal`/`double`/`float` extreme on its own type are unchanged; a genuinely vacuous `[Range(0, 255)]` on a `byte` now names both bounds (255 is no recognized extreme).
- **`EditDateRange`'s shared calendar bounds are now the looser of the two fields' own, not a first-non-null pick.**
  - `Min` used to resolve param → `Start`'s attributes → `End`'s, and `Max` param → `End`'s → `Start`'s, so whenever both fields declared the same kind of bound the "natural" field won even when the *other* field's bound was looser — the calendar blocked dates that other field's own validation accepts, with no message explaining why.
  - `Min` now takes the earlier of the two minimums and `Max` the later of the two maximums, each still falling back to whichever single field declares one (so the natural `[MinValue]`-on-`Start` + `[MaxValue]`-on-`End` pairing, and a single `[Range]` on one property, behave exactly as before). The result is the convex hull rather than the union — disjoint per-field windows leave the gap selectable, which the annotations still reject at validation time. See [Model-declared Min/Max](#model-declared-minmax-minvaluemaxvalue).
- The `float` range sentinels are computed under the validation-time culture like every other candidate instead of matching a frozen invariant-format literal. `[Range(-100f, float.MaxValue)]` under a `,`-decimal culture (de-DE) matched no branch, so the raw `3,4028234663852886E+38` reached the user — exactly the scientific notation the one-sided rewrite exists to suppress. The mirror `[Range(float.MinValue, 100f)]` failed the same way.
- **`EditDateRange`'s parse-error messages now survive an `EditContext` swap.**
  - Its `ValidationMessageStore` bound to the first `EditContext` and never rebound, so after a form reset or record reload a new parse error was written into the dead context: no message rendered, `aria-invalid` never set, and the unparseable text was silently reverted with zero feedback.
  - The store's entries are now cleared and the store dropped on a genuine swap, so the next parse error lazily rebinds to the current context. (`EditDate<T>`'s twin block was never affected — `InputBase` throws on a context swap, so it can't reach this state.)
- **A duplicate option value no longer crashes `EditCheckedStringList`/`EditCheckedEnumList<TEnum>` on first click.**
  - The shared checkbox-per-option body keyed its siblings on the option *value*, so alias enum members (`Active = 1, Enabled = 1`) or a repeated string in `Options` produced duplicate `@key`s — the first render succeeded and the first re-render threw `InvalidOperationException` out of the renderer.
  - Options are now keyed on their already-unique generated id. The radio and select option lists carry no `@key` and always survived the same input.
- **`EditDate<T>` bound to `DateTimeOffset` no longer throws on an out-of-range write-back.** A year-1 date typed under an east-of-UTC zone (or year 9999 west of UTC) puts the UTC instant outside `DateTimeOffset`'s own range, and the conversion's `ArgumentOutOfRangeException` reached the circuit. The bounds check the constructor performs internally now runs first, and a failure routes through the same parse-failure/revert path (`ParsingErrorMessage`) as any other unusable entry.
- **`EditDate<T>` and `EditDateNative<T>` no longer disagree by 543 years under a non-Gregorian-default culture.**
  - The pickers force the Gregorian calendar for every format they emit, but two read-only paths didn't: `EditDate`'s `FormatException` fallback (an incompatible `DateFormat`) fell back to a bare `ToString()` (CurrentCulture), and `EditDateNative`'s read-only rendering formatted with the raw `CultureInfo.CurrentCulture`. Under `th-TH` the two documented-as-interchangeable controls showed different years — and `EditDate`'s own primary path and degraded fallback could disagree with each other.
  - Both now force Gregorian, matching `EditDateRange`'s twin.
- **`aria-required` and the required star no longer go stale after a model/`EditContext` swap.** `EditControlListBase<TItem>` and `EditDateRange` both refreshed their ARIA state *before* replacing the `FieldIdentifier`, so on a swap the star and `aria-required` resolved via `FormOptions.RequiredResolver` against the swapped-away model and stayed wrong until some later parameter cycle. The refresh now runs last in both. (`EditDateRange`'s parse-error-store clear still runs first — that one has to see the old context.)
- **Closing one of two controls bound to the same property no longer breaks the `ValidationView` summary link.**
  - `FormOptions.FieldIds` is last-writer-wins, and the common page-section + edit-modal pairing registers the modal last under its own `IdPrefix`; disposing the modal left the modal's now-dead DOM id in `FieldIds` while the shared registration stayed alive, so the summary anchored `href="#modal-Name"` at a removed element.
  - The owner set now carries each owner's registered id and restores the last remaining registrant's.
- **A missing `@bind-Value` now reports the helpful diagnostic instead of an `ArgumentNullException` from disposal.** The binding check throws before `InitState` runs, leaving the `FieldIdentifier` at `default` — and unregistering that hashed a null `FieldName`, so the disposal exception replaced the message telling you what to fix. Both control roots now unregister only once init has completed.
- **`EditRadioEnum<TEnum>`'s read-only view no longer renders a dangling `"Other: "`** when Other is the selected option but `OtherValue` is empty — it gated on the enum value alone, not on whether there was any text to show.
- **A combined `[Flags]` enum value no longer renders with a doubled space.** `EnumHelpers`' camel-case fallback inserted a space before every upper-case letter, so the `"A, B"` separator the framework produces for a combined value became `"A,  B"` — and `GetName` memoized it. The split now skips the insert when the previous character is already whitespace. Surfaces in `EditCheckedEnumList<TEnum>`/`EditRadioEnum<TEnum>` read-only views.
- `EditRadio<TValue>.OnInitialized` now chains `base.OnInitialized()` the way its own `OnParametersSet` already did — `InputRadioGroup` owns the group setup it was skipping. Latent (no known symptom), but it contradicted the library's own documented guard.
- **Toggling `EditString`'s read-only mask no longer throws focus to the page body.**
  - The masked and revealed rows rendered from two sibling `@if`/`@else` call sites of a shared `RenderFragment`, and each call site is its own Blazor *render region* — so a toggle removed one region's elements and inserted the other's, destroying and rebuilding the very `<button>` the user had just activated. Keyboard focus fell to `<body>` mid-gesture (the next Tab restarted from the top of the page) and a screen reader's virtual buffer reset.
  - The row is now one render site with the two states expressed as ternaries, so the diff patches the element in place and the button survives.
- **A long unbroken token (a URL, id, or masked value with no spaces) no longer overflows its container.** `.edit-readonly-value` (backing every control's read-only view), `.edit-string-link`, `.edit-masked-value`, and `LabelTooltip`'s bubble (`.edit-tooltip-content`) now wrap mid-token (`overflow-wrap: anywhere`) instead of running past their box; `.edit-masked-value` also gets `min-width: 0`/`max-width: 100%` and its eye-toggle button `flex-shrink: 0`, so a long masked value can't push the toggle out of a clipped container.
- `.edit-string-link` (the read-only URL view) and `.edit-file-delete-btn` now get the library's shared keyboard-focus ring — both previously had no visible focus indicator once a consumer resets the UA outline.
- **Character count text now meets WCAG 1.4.3's 4.5:1 contrast floor instead of sharing the affix icons' 3:1-only gray.**
  - `.edit-input-count`/`.edit-textarea-count` (3.36:1 unthemed, ~4.1:1 themed) move to a new, darker `--edit-color-text-secondary-strong` token/bridge; the clear/password-toggle icon color is unchanged (icons only need 1.4.11's 3:1).
  - Two more contrast fixes alongside it: the themed placeholder (`--edit-color-placeholder`) was `#bfbfbf` (1.84:1, illegible) and is now `rgba(0, 0, 0, 0.55)` (~6.5:1); the themed affix wrapper's `:focus-within` border resolved through the same `color-mix` as its hover state (~2.63:1, too weak for a WCAG 2.4.7 focus indicator) and now resolves straight to `--edit-color-primary` (the glow is unchanged). See [Styling and Customization](#styling-and-customization).
- The themed disabled affix wrapper's dim no longer stops at its own children: `.edit-input-count`, `.edit-input-password-toggle`, and `.edit-textarea-count` each set their own explicit color, which won over the wrapper's disabled dim — a scoped rule now brings them to `--edit-color-text-disabled` once the wrapper is disabled.
- **`Size="Small"`'s legacy-mode `InvalidIcon` padding reserve no longer produces a lopsided input.**
  - `edit-input-legacy-padding`'s `padding-inline-end: 2rem` won its specificity tie against `edit-input-sm`/`-lg`'s own padding regardless of size, so a small (24px-tall) legacy-mode field kept the same 2rem (32px) trailing reserve as its 7px leading padding.
  - Two higher-specificity overrides now scale the reserve to 1.5rem/2.25rem for `-sm`/`-lg`; the default (no `Size`) case is unchanged.
- WCAG 2.5.8: `EditString`'s clear, password-toggle, and mask show/hide buttons were ~14px pointer targets — they now extend to 24px via the same invisible `::before` hit-area technique the tooltip trigger and `EditFile`'s delete button already used.
- `edit-controls.css` gains one `@media print` block hiding purely-interactive chrome with no meaning on paper (the clear/password-toggle/mask-eye buttons, `EditFile`'s delete button).
- **An empty `EditDisplay` no longer collapses to zero height.** It hand-builds its read-only div rather than using `ReadOnlyValue`, and had missed that component's hidden "No Value" line-height placeholder — so an `EditDisplay` with no text sat flat beside sibling read-only fields that each reserve a line, pulling the row out of alignment. It now renders the same `aria-hidden`/`visibility: hidden` placeholder.
- **`EditRadioEnum`'s preserved "Other" text no longer leaks onto a swapped bound record.**
  - The copy kept so a mis-click stays recoverable (see the "Other" contract above) had only two reset branches — a non-empty `OtherValue`, and an empty one while Other is *still* selected — and a record swap fires neither, so record B's disabled Other box displayed record A's free text.
  - An external change to the bound value/`OtherValue` pair now drops it; the control's own writes are excluded by tracking what it wrote, so the parent echoing back a switch-away still preserves normally.
  - One residual in both radio controls, not detectable from inside them: a swap whose new value *equals* the old one is indistinguishable from "nothing happened", so the preserved text survives that particular swap.
- **`EditFile`'s upload-rejection messages no longer outlive the upload they describe.**
  - Nothing reset them on a parameter change, so a rejected file's error — and the `aria-invalid`, red drop zone and `role="alert"` block it drives — came back with the editor after an `IsEditMode` round trip, and followed the control onto a *swapped bound record* that had never had a failed upload at all.
  - They are now cleared when the editor isn't shown and when the bound list changes from outside the control. A batch that both accepts some files and rejects others still keeps its messages: the control's own commit isn't an external change.
  - Going `IsDisabled` mid-drag also clears the drop-zone hover highlight, which used to render "hover disabled" — lit up as accepting a drop it now refuses.
- **Unmatched attributes written on a scalar control are now actually rendered.**
  - `InputBase` captures them into `AdditionalAttributes` for free, so nothing ever threw — but capturing is not rendering, and only `class` (which travels `CssClass`) ever reached the DOM. An `inputmode`, `readonly`, `spellcheck`, `data-*`, `title` or extra `aria-*` written on `EditString`, `EditTextArea`, `EditNumber<T>`, `EditDateNative<T>`, `EditBool`, `EditBoolNullRadio`, `EditSelect<TValue>`, `EditSelectString<TValue>`, `EditSelectEnum<TEnum>`, `EditRadio<TValue>`, `EditRadioEnum<TEnum>`, `EditRadioString` or `EditSelectSearch<TValue>` was silently swallowed.
  - Where each one lands is now uniform across the library: **`class`** keeps its single existing channel (the field element, merged with the EditContext state classes); **`style`** merges onto the root `.edit-control-wrapper`, the same element the list-bound controls already used for it, which also keeps a consumer's declarations off the two elements whose inline style is JS-owned (`EditTextArea`'s AutoSize height, the `Select` engine's open-order z-index); and **everything else** splats onto the element it describes — the editor `<input>`/`<textarea>`/`<select>`/checkbox, the `role="radiogroup"` fieldset for the four radio-group controls, and the engine wrapper for `EditSelectSearch` (via the new `Select.AdditionalAttributes` above).
  - The splat always goes first, so every attribute the control writes itself still wins on a collision, and an empty splat emits nothing at all — a control with no extra attributes renders byte-identical markup.
  - `EditMultiSelect<TValue>` keeps its existing root-wrapper splat (it is a list control, and that is where its whole family puts them), and a read-only control that renders no editor drops the editor-targeted attributes with it, exactly as it already drops the editor's own. `EditDate<T>`/`EditDateRange` are unchanged — they already forwarded the whole splat, `class`/`style` included, to the inner picker's wrapper.
- **A runtime `Id`/`IdPrefix` change now re-targets the whole control.**
  - The resolved element id was computed once during init and never again, so a control handed a new `Id`/`IdPrefix` — one re-used for a different record, or inside a `FormGroupOptions` that renames itself — kept rendering under the old id while its `FormLabel`'s `for`, its `aria-describedby`/`aria-errormessage` targets and its `FormOptions.FieldIds` entry were all rebuilt from the stale value: the label stopped activating the field, the ARIA references dangled, and the `ValidationView` summary linked to an id nothing rendered.
  - Both control roots (plus `EditRadio<TValue>` and `EditDateRange`, which keep their own copies of the plumbing) now re-resolve on every parameter change and move the registration onto the new id. `EditDateRange` moves both — its End id derives from the Start one.
  - Worse in the list-bound controls and `EditDateRange`, which additionally re-registered a freshly-derived `FieldIdentifier` under the stale id whenever the bound model/`EditContext` was swapped. `EditDisplay`, the one control that already re-resolved every parameter cycle, is unchanged.

**Fixes** (Select engine — `Select` / `EditSelectSearch` / `EditMultiSelect`)
- **A string-bound `Select`/`EditSelectSearch` holding `""` now shows the placeholder and no clear button.**
  - `default(string)` is null, not `""`, so an empty string counted as a real selection: the trigger rendered an empty label where the placeholder belonged, alongside a live clear button that appeared to do nothing.
  - The "is something selected" test now excludes the empty string — **unless an option literally carries `""`**, in which case the option lookup wins and it stays a genuine selection, which is what makes an explicit "None"/"Any" option work.
  - The same verdict the native selects reach in `IsValueDefault` and the one `HidingMode` documents, so all three now agree; `EditSelectSearch` also picks up the `HidingMode.WhenNullOrDefault` empty-string treatment here, making it the last string-capable control to align (see **Changed** above).
- Clicks on the dropdown panel's own chrome no longer close a `ShowSearch="false"` select.
  - The parts of the panel carrying no click handler of their own — group headers, the empty/"No data" row, the panel's padding and scrollbar gutter — bubbled into the wrapper's click handler, which reads any wrapper click as a toggle when there's no search input to focus instead.
  - The panel now stops propagation itself; option rows and `DropdownFooter` already did, so their own handlers still run and still commit.
- Fixed label drift on a duplicate-`Value` option list: `Select`, `EditSelectSearch`, and `EditMultiSelect` each built their own value→option lookup with a different tie-break on a duplicate `Value` (last-wins dictionary vs. first-wins `FirstOrDefault` vs. first-wins `TryAdd`) — the same bound value could render a different label in the interactive dropdown than in a read-only view.
  - A new shared `SelectOptionLookup.Build` helper (last-wins, matching the engine's own tie-break) is now the single source of truth for all three; also removes `EditSelectSearch`'s O(n) per-render label scan.
- Tags mode: deselecting a tag no longer erases the label of a same-valued option still supplied by `Options`.
  - A user-created tag that stops being selected also leaves the option list (matching AntD, so a removed typo-tag doesn't stay selectable forever) — but the removal deleted that value's lookup entry outright, including a live entry `Options` provided, after which the value's label fell back to `ToString()`.
  - The lookup is derived from `Options` + the tag list, so it is now rebuilt from what's left rather than having one key removed. Same treatment on the clear-all path. Matters most for the server-echo pattern: commit a tag, the server returns it as a real, better-labelled option, then the user deselects and reselects it.
- **The keyboard highlight always settles on a row the user can actually act on.**
  - Rebuilds that didn't re-derive the active index themselves — `Options` reassigned while the dropdown is open, and the multiple-mode select/clear/tag-commit paths that clear the search text — clamped it to the list *bounds*, which could leave it on a group header or a disabled option: the highlight and `aria-activedescendant` vanished, Enter went dead, and in `Tags` mode it committed a spurious new tag.
  - Every rebuild now clamps to the nearest selectable row, and a list with *no* selectable row (all options disabled) drops the highlight entirely rather than pinning it to a disabled one — a screen reader was previously announcing an `aria-disabled` row as current while Enter did nothing and the arrows couldn't move off it.
- **`DefaultOpen` now derives its initial highlight the same way a user-driven open does** — from the bound `Value`, skipping group headers and disabled options — instead of sitting at raw index 0.
- **Removing a tag with the × button, or clearing the selection, no longer drops keyboard focus to `<body>`.**
  - Both delete the focused button from the DOM, and element removal fires no `focusout`, so focus fell off the control entirely: Tab restarted at the top of the page and an open dropdown stayed open with focus outside it (the focus-out dismiss never ran).
  - Both paths now restore focus to the search input, like the multiple-mode select and tag-commit paths already did. The Backspace tag-remove path deliberately does **not** — the search input never lost focus there, and on Blazor Server each restore is a circuit round-trip, so holding Backspace to clear twenty tags used to issue twenty interop calls for a gesture that needs none.
- **A selection or clear made while a search debounce is in flight no longer fires a spurious `OnSearch("")` afterwards** — an extra server query, an options reset and a highlight jump, seconds after the user had moved on. Only `CloseAsync` used to cancel the pending debounce; all four search-reset paths now do.
- **A non-searchable select's type-ahead prefix is now cleared on close.** It only self-cleared after a ~1s pause between keystrokes, so closing and reopening within that window resumed the previous session's accumulated prefix — the next letter typed jumped somewhere that letter alone doesn't explain, or matched nothing at all. Cleared alongside the search text on every reset path (close, a multiple-mode selection, clear, a committed tag), the way a native `<select>` starts fresh.
- **`SelectOptionList`'s native `<option>` ids are de-duplicated**, the same `ToUniqueIds` pass the checkbox and radio option lists already got.
  - A literal `"none"` option collided with the component's own synthetic `{Id}-option-none`, and options that sanitize alike (all-CJK labels all sanitize to an empty segment) collided with each other. Both synthetic segments are reserved unconditionally — `ShowPlaceholder` is derived from the current value, so a conditional reservation would change a literal option's id as the user picks values.
  - **Ids change only for colliding options**; an ordinary ASCII list produces exactly the ids it always did.

**Fixes** (UI Kit)
- **The loading-toast spinner rendered visibly cropped** — `MessageListView`'s local copy of the spinner glyph had drifted from the canonical one in both path data and `viewBox`.
  - All inline SVG glyphs now come from the single icon registries, which also fixed the `Table` expand chevron's silently corrupted path coordinates (a hand-retyped copy) and removed a duplicated XSS-invariant helper.
- **Week-mode pickers no longer throw on `default(DateTime)`/`DateTime.MinValue`.**
  - `DatePicker`/`DateRangePicker` at `Mode="Week"` build their grid from six week starts, and the week-start subtraction underflowed `DateTime.MinValue` for year 1 — so a `Mode="Week"` picker (or an `EditDate`/`EditDateRange` over one) bound to an unset non-nullable date crashed at *first render*, which on Blazor Server takes the circuit down. The lead is now clamped to the days actually available, yielding that partial first week's own start.
  - The mirror case is fixed too: a typed commit landing in year 9999's last week, whose 7-day span runs past `DateTime.MaxValue`, no longer overflows while the commit guard compares the week end against `Min`.
- **`Tabs` now raises `ActiveKeyChanged` when the tab a bound `ActiveKey` names is removed or disabled.**
  - The strip has always silently fallen back to the first enabled tab in that case, but only a user click raised the event — so a bound key kept pointing at a tab that was no longer rendered or no longer usable, and the consumer's own pane/filter state disagreed with the highlighted tab until the next click.
  - The fallback is now reported with the key actually active, once per distinct fallback (a consumer that ignores it isn't told again, which would loop). Never raised for a null `ActiveKey`: null is the documented "activate the first enabled tab", so that's not a desync. Notified from `OnAfterRender` rather than mid-render, so a `Key`/`Disabled` change on an already-registered tab can't report a fallback that isn't real.
- **`Table` column filter: closing a filter now returns focus to its own funnel button** — unless another column's filter just opened, in which case focus stays in the newly-opened panel.
  - Previously the closing filter unconditionally pulled focus back to its trigger, so clicking straight from column A's funnel to column B's left B's panel open but unfocused, and B never saw Escape or any other key. A plain close (Escape, outside click, OK, Reset) leaves no filter open and still returns focus to the funnel, as before.
- **`Table`: the select-all checkbox's mixed (indeterminate) state now survives a `SelectionMode` `Single` → `Multiple` round trip.**
  - `indeterminate` is a DOM property with no HTML attribute, so it is mirrored from C# via JS only when the value changes. `Single` mode renders no select-all `<input>` at all, but the mirror still "applied" the state to a default `ElementReference` (a silent no-op) *and* recorded it — so switching back to `Multiple`, which brings in a freshly-created checkbox with `indeterminate == false`, short-circuited against that stale record and announced a partial selection as "not checked" instead of "mixed".
  - The mirror is now forgotten whenever the element isn't there to mirror onto (the same treatment `Selectable` and a runtime `UseStyledCheckbox` change already had).
- **`Table` column filter: a close→reopen racing the in-flight positioning call no longer leaks window listeners for the circuit's lifetime.**
  - Under `ScrollY` the escaping dropdown is positioned by a JS call that also wires scroll/resize listeners and returns a handle to release them. The call site re-checked "still open" after the await but held no sequence token, so a close-and-reopen across that round trip orphaned the first handle — its listeners were never released, and they accumulated with every such race.
  - The handle now lives in a new internal `JsHandle` holder owning the token, the two-step release, and the no-JS degrade — the same race guard `Modal`/`Drawer` already had, now shared rather than reimplemented, so a call site can't express the bug.
- **Two overlapping lazy JS-module imports no longer import twice and strand an `IJSObjectReference`.**
  - The internal `JsModule` holder cached the resolved reference, not the in-flight import task — so two callers that both started before the first import resolved each got their own module, and the loser's reference was never disposed, held for the rest of the circuit.
  - It caches the task now (a failed import is still uncached, so the next render retries).
- **A transient JS import failure no longer permanently strands a picker or select without its focus-out dismiss wiring.**
  - `DatePicker`/`DateRangePicker`'s `initPicker` and `Select`'s `initInput` are one-time wirings, and the "already wired" flag was latched *before* the awaited import — so a single import failure (a dropped/slow circuit at exactly the wrong moment) marked the control wired forever, leaving it without Enter form-submit suppression and, worse, without the tab-away dismiss that keeps an abandoned dropdown's invisible backdrop from swallowing the next click anywhere on the page.
  - The flag is now set only on success, matching `JsModule`'s own contract that a failed import retries on the next render.
- Day cells in both pickers now carry `aria-current="date"` on today, matching the month/quarter/year cells that already did — "today" was conveyed to sighted users by a CSS ring with nothing in the accessibility tree behind it.
- `DateRangePicker`'s `Week`-mode grids now show the whole-row hover band `DatePicker`'s own `Week` mode already had (a week row, not a day, is the selection unit there, so per-day hover was the wrong affordance).
  - Relatedly, hovering no longer overrides a row's committed-range or hover-preview tint: the week row's own background *is* that tint, so hover now yields to it explicitly rather than painting over it — plain rows in either picker still get the band.
- Week **numbers** are now always Gregorian, even under a culture whose default calendar isn't. The pickers are Gregorian-calendar controls and force Gregorian for every format they emit by swapping `DateTimeFormat.Calendar`, but the week-number lookup read the culture's own default `Calendar` instead — so `ar-SA`, `fa-IR`, and `th-TH` numbered the week in Umm al-Qura/Solar Hijri/Buddhist terms next to an otherwise entirely Gregorian panel. Identical output for every culture that already defaults to Gregorian, `en-US` included.
- `Popconfirm` no longer leaks a JS object reference when disposed while its module import is in flight — its focus path re-imported `wss-overlay.js` without the disposal re-check its own base class documents. All lazy module imports now flow through one internal guarded holder (`JsModule`), so the guard can't be omitted again; post-dispose calls also no longer import a throwaway module.
- `.wss-table-filter-trigger`'s keyboard focus ring gains the same 2px corner radius its sort-trigger sibling already had (the two header buttons were inconsistently styled).
- `edit-controls.css`'s styled-checkbox check glyph now honors the `--color-on-primary` bridge instead of hardcoding `#fff` — dark/high-contrast themes previously got a correct check mark on the Table's styled checkbox but a hardcoded-white one on `EditBool`'s.
- `SearchInput`'s clear (`AllowClear`) and search buttons now pin an explicit `height`/`min-height: var(--wss-control-height)` — previously unset, so a consumer reset like `button { max-height: fit-content }` could collapse them below the input's height and break the seamless pill layout (the same defense already applied to `Pagination`'s prev/next buttons).
- Consolidated the JS overlay-positioning helpers to fix inconsistent flip behavior: `wss-select.js`'s dropdown placement used to flip to the opposite side whenever it merely had *more* room than the preferred side (which could still overflow), while `Popover`/`Popconfirm`'s `place()` and the `ScrollY`-fixed dropdown positioning only flipped when the opposite side actually fit — two different policies answering the same question.
  - A new shared `fits()` helper in `wss-overlay.js` (imported by `wss-select.js`) now makes every flip site agree on the safer "does the opposite side actually fit" policy; new `stackWithBackdrop()`/`wireDismissOnFocusOut()` helpers also dedupe the backdrop z-index stacking and tab-away dismiss wiring that `wss-select.js` and `wss-picker.js` each reimplemented separately.
- **`wss-select.js`'s horizontal dropdown clamp is now two-sided, replacing 10.6.7's "anchor from the wrapper's right edge" behavior.**
  - Right-anchoring keeps overflowing whenever the wrapper's own right edge is already at or past the viewport's, and for a dropdown wider than the room remaining (the `Pill` variant on a narrow viewport) it pushed the dropdown's *left* edge off-screen — unreachable option text, strictly worse than the right-side clipping it was avoiding.
  - It now shifts left only as far as the viewport's left margin, via the shared clamp helper the other overlay sites use. Still no movement whenever there's room. See [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).
- **`wss-tooltip.js` no longer picks `<body>` as its bounds frame, and intersects the chosen frame with the viewport.**
  - `body { overflow-x: hidden }` is near-ubiquitous boilerplate, and it made `<body>` qualify as a clipping ancestor — whose rect is the whole *document*, as tall as the page and with its top well above the viewport once scrolled, so the "am I in the lower half?" test that decides the flip answered against the document instead of the screen and a trigger near the viewport bottom opened downward, off-screen.
  - `<body>` is now skipped explicitly (a page that genuinely wants a body-sized frame gets the viewport, the same box for an unscrolled page), and any frame that *is* accepted — a modal body, a scroll panel, a recognized `wss-modal`/`wss-drawer`/`wss-popover` panel — is intersected with the viewport, since only its visible part can hold the bubble. Affects both `data-tooltip` and the form controls' `LabelTooltip`, which share the module. See [Hover tooltips](#hover-tooltips-data-tooltip).
- Theming and RTL cleanups across both stylesheets, all with unchanged un-themed rendering:
  - `EditFile`'s drop-zone background/border and its file rows' name/size text now route through the `--color-page-background`/`--border-color`/`--color-text`/`--color-text-secondary` bridges instead of bare literals (previously no override hook at all, so a dark theme got a light-grey card).
  - `.edit-radio-other-input` and the indeterminate styled checkbox now consume the `--edit-color-border` token rather than an ad-hoc bridge of their own — the "Other" input's *un-themed* fallback shifts `#ccc` → `#d9d9d9`, the AntD border value the rest of the file already uses.
  - `Alert`'s icon/actions/close offsets and `Pagination`'s size-changer arrow are now logical properties, so they mirror correctly under `dir="rtl"` (identical computed values under LTR).
  - `prefers-reduced-motion` now actually stops the `Table` expand chevron's rotation (the rule listed the button, but the transition is on the `svg` inside it).
  - A small (`Size="Small"`) searchable select no longer double-insets its typed text by 7px — the search overlay already spans the selector's padding box, so the size rule's own inset stacked on top of it.
- **`ToastQueue.Clear()`/`Dispose()` no longer race a toast's own expiry into an `ObjectDisposedException`.**
  - They walked a snapshot of the timer collection and cancelled + disposed every `CancellationTokenSource` without claiming it, so a toast expiring on the threadpool and a concurrent `Clear()` could both reach the same source — the loser cancelled an already-disposed one and threw out of the caller's `Clear()` (a circuit error on Blazor Server) or faulted a fire-and-forget task.
  - Each entry is now claimed with the same exclusive `TryRemove` handshake the single-timer path already used. Affects `MessageService`/`NotificationService` and both `Wasm*` statics.
- **`Table`: a runtime change to an already-registered column's parameters now re-renders the table.**
  - Columns only queued a re-render when they were *new* to the rendered set, and the header is built from the column instances before the diff reaches `Column.SetParametersAsync` — so `<Column Title="@($"Results ({count})")">` showed the previous title until some unrelated event happened to re-render the table, and the same held for `Ellipsis`, sortability, `FilterOptions` and `PropertyColumn.Format`.
  - Columns now snapshot every scalar the table renders from them and notify on a real change (the same guarded pattern `Tabs` uses), with `FilterOptions` compared **by value** — inline-built option lists are a fresh instance every pass, so a reference compare would loop, and a consumer refilling one `List<TableFilterOption>` field in place would never be noticed.
  - The row-affecting **delegates** (`Property`, `OnFilter`, `SortBy`) are tracked too, compared by *method identity* rather than instance, and a change re-runs filter → sort → page rather than only re-rendering: a swapped `Property` selector used to render the old property forever, and a swapped `OnFilter`/`SortBy` left the cached filtered/sorted list in place while a filter or sort was active.
- **`Table`: a column conditionally inserted ahead of siblings whose parameters Blazor treats as unchanged (`Title`-only columns) now renders in its declared position** instead of being pushed to the end permanently — and column order no longer drifts on unrelated interactions (a sort click, paging, a filter apply).
  - Declaration order is recovered from the renderer's own document-order pass, with stragglers placed relative to re-registering anchor columns rather than re-spliced at stale indices.
  - In the one provably-ambiguous case (a structural change on a pass where *every* existing column is `Title`-only — i.e. none can hold sort/filter state), the `<Table>` child content is rebuilt once to re-collect in document order; columns that can hold state are never rebuilt.
  - The narrow residual is pinned by a named test: when a newcomer and a parameter-skipped column share the same gap between anchors, the skipped column keeps its earlier position and the newcomer lands after it — right for a column declared after it, wrong for one declared before it (which renders after it until a later pass re-registers both).
- **`Table`: applied filter values that a swapped `FilterOptions` no longer offers are pruned instead of filtering invisibly.**
  - With data-derived options — user filters on X, then the data and its options swap and X disappears — the raw applied value kept excluding *every* row while the dropdown showed nothing ticked (OK was a no-op; only Reset recovered) and the consumer's own filter summary reported no filter at all. The prune re-derives filtered/sorted/paged so the freed rows come back, and now also raises `OnFilterChanged` (see **Changed (UI Kit)** above).
  - A column that stops offering a filter entirely also has its dropdown-open flag cleared: an open, untouched dropdown used to leave that flag stuck true forever, which pinned the table's "is any filter open" state true for its lifetime — every other column's filter then skipped its focus restore on close and dropped focus to `<body>` — and made the dropdown reappear already open when options came back, its invisible full-screen backdrop swallowing the next click.
- **`ActionColumn<TItem>`'s click guard moved from its inner flex row to the `<td>` itself.**
  - `.wss-table-actions` is `inline-flex`, so the `stopPropagation` covered only the buttons while the cell's own 16px padding bubbled into the row handler — a click a hair off an action button toggled `ExpandRowByClick` or raised `OnRowClick`, the opposite of what `Table`'s docs promise.
  - Now matches the selection and expand cells, which always guarded the `<td>`. The render tree carries the wrapper only when the flag is set, so every other column's cell keeps its exact shape.
- **`Tabs`: a conditionally-rendered tab takes its declared position on screen *and* in the keyboard order.**
  - Each `Tab` now renders its own nav button, so the rendered strip is always in declaration order with no registration bookkeeping to go stale.
  - The tab *list* behind the arrow keys and the null-`ActiveKey` fallback is still built from child registrations, and Blazor skips `SetParametersAsync` for a child whose parameters are all unchanged immutables — so on a pass where the strip cannot tell where a newcomer was declared, it re-creates its `Tab` children once under a generation `@key` to re-collect in document order. That repair happens inside the same render batch (a guessed order is never painted), and ordinary re-renders, removals, `Title`/`Count`/`Disabled` changes, and insertions the pass could place from what it reported re-create nothing.
  - Arrow-key navigation whose `ActiveKeyChanged` handler inserts a tab keeps DOM focus (the strip re-focuses the replacement button) — including an **async** handler, where the insertion lands a render after the await and the re-collection therefore happens later than the keypress's own render cycle.
  - **Only `<Tab>` may be declared directly inside `<Tabs>`:** the children render inside the `role="tablist"` element, so any other markup there is an `aria-required-children` violation, and a contract-violating stray component also loses its instance state on such an insertion. A component in a tab's *pane* is unaffected by all of this (the pane renders once from the strip's own tree, only while its tab is active).
  - Public API, ARIA wiring, keyboard behavior, and rendered DOM are unchanged. One shape stays unseen: a pure `@key`ed reorder among otherwise-unchanged tabs moves the buttons but not the keyboard order (no tab's parameters change, so nothing reports).
- **`Mode="Week"` pickers bound to a value carrying a time-of-day now work at all.**
  - The week-start normalization was the one mode that didn't truncate to midnight, so a `DateRangePicker Mode="Week"` bound to `2026-03-04T09:00` produced a week start that equalled no rendered (always-midnight) week start: the selected week never painted, no day cell was a focus stop, and **both grids were keyboard-unreachable** — precisely the failure the normalization exists to prevent.
  - `DatePicker Mode="Week"` had the milder half: a typed or `DateTime.Now`-preset commit landed the week start at 09:00, so a `Max` on the week-start day rejected a week that a click accepted.
- **Both pickers' `DisabledTime` is now honored on every commit route.**
  - In `DatePicker`'s `DateTime` mode a day click composed `day + Value.TimeOfDay` and committed with no time check, binding an hour the typed path and the time selects both reject; and the hour/minute/second **selects** checked only the *time* half of the commit guard, making them the one route that committed a **date** nothing had validated — with `Min` a month out, the day buttons and the Now link all rendered disabled while changing the hour fired `ValueChanged` with today.
  - The day-click guard also now judges the **normalized** value actually committed (a stale second that `ShowSeconds="false"` zeroes was rejecting clicks whose committed value it never applied to).
  - `DateRangePicker`'s Time/DateTime pick session had the same two holes, doubled, plus two more: its pre-advance guard judged the pre-swap value against the endpoint it was about to *leave* (rejecting pairs the final normalize → swap → guard accepts), and its pending-state write sat ahead of the guard's own `return`, so a rejected OK still mutated — turning "fall back to the committed Start/End" into an explicit pending value that then shadowed a `Start`/`End` the consumer changed while the panel was open.
- The picker's internal sub-components (`PickerMonthHeader`, `PickerWeekdayHeader`, `PickerTimeRow`, `PickerTimeRowSlot`) degrade to rendering nothing instead of throwing when constructed standalone.
  - Razor compiles every component to a `public` class, so these ship on the package surface even though none is a supported control; `PickerTimeRowSlot` threw a `NullReferenceException` on its first parameter read with no owning picker (`[EditorRequired]` is a tooling hint, not a runtime check).
- **RTL and reduced-motion cleanups across both stylesheets, all LTR pixel-neutral:**
  - `Pagination`'s size `<select>` and the pickers' month/year `<select>`s paired physical padding with an already-logical arrow, so under `dir="rtl"` the arrow flipped to the left over the option text while 28px of dead space stayed on the right (both now use `padding-inline`).
  - `.edit-tooltip-container`'s `margin-left` — the last non-centering physical offset in `edit-controls.css` — became `margin-inline-start`.
  - The `prefers-reduced-motion` blocks gained the `[data-tooltip]` hover tooltip's arrow/body transitions, `.wss-table-expand-btn`'s color transition, `.edit-theme`'s input-chrome border/shadow transition, and the button-mode radio/`EditFile` button color transitions, all of which kept animating while their byte-equivalent counterparts in the other file were already suppressed (WCAG 2.3.3).
- **A long unbroken token no longer overflows its container** in the hover tooltip (`[data-tooltip]::after`), the `Popover`/`Popconfirm` panel (`.wss-popover-content`, `.wss-popconfirm-title` — which also gets `min-width: 0`, since it's a flex item beside the confirm icon), message/notification toasts (`.wss-msg-content`, `.wss-notification-message`/`-description`, both now also `min-width: 0` where they're flex items), `Alert`'s message/description, and default (non-`Ellipsis`) `Table` cells (`overflow-wrap: anywhere` — harmless for ordinary spaced text, the opt-in ellipsis class stays the stronger tier).
- **`Table`'s plain (non-`UseStyledCheckbox`) row-selection/column-filter checkbox and radio inputs, and the filter dropdown's Reset/OK buttons, now get a keyboard-focus ring** (`2px solid var(--wss-color-primary)`, matching the rest of the kit) — previously only the styled-checkbox variant had one.
- `SearchInput`'s leading addon chip now dims with the rest of a disabled control (`.wss-search-disabled .wss-search-addon`) instead of staying full-strength beside a dimmed input/clear button.
- **`Popover`/`Popconfirm`'s arrow drop-shadow had two independently-drifted `rgba` literals** (0.07 on the default/top-side direction, 0.06 on the bottom-placement override) baked into each usage site — unified behind one new `--wss-arrow-shadow` token (each site keeps its own directional offset/blur, which genuinely differs by placement) so a consumer gets one override point and both directions read as the same shadow weight.
- RTL: the `Table` header's column-divider and the `DatePicker`/`DateRangePicker` dropdown's anchor edge are now logical (`inset-inline-end`/`inset-inline-start`) instead of physical — the picker dropdown is `width: max-content`, unlike the full-width `Select` dropdown, so which edge it hangs from genuinely differs under `dir="rtl"`. The file's RTL exception comment also now names `Popover`/`Popconfirm`/the hover tooltip's `-left`/`-right` placement modifiers as deliberately physical, alongside `DrawerPlacement`.
- `wss-controls.css` gains one `@media print` block hiding purely-interactive chrome with no meaning on paper: `Modal`/`Drawer`/`Alert`/`Notification` close buttons, `Select`'s clear/tag-remove, `SearchInput`'s clear/search buttons, the picker's clear button, the table filter trigger, `Pagination`, and the `Tabs` nav strip.
- **Setting `Disabled` on a `DatePicker`/`DateRangePicker` whose dropdown is open now closes the panel.**
  - Only the clear button, the field click and the input focus were ever guarded, so an already-open calendar stayed fully interactive on a disabled control: every day/month/year/quarter cell, preset, footer link, header select and the range session's OK button still committed through to `ValueChanged`/`StartChanged`/`EndChanged`.
  - Both pickers now enforce the same `Disabled ⇒ closed` invariant `Select` has, routed through the normal close so the JS/focus teardown (and the range picker's in-progress pick/session discard) runs as usual — plus a `Disabled` guard on the single `SetValueAsync`/`SetRangeAsync` commit funnel every route ends at, so a disabled picker cannot write through even along a path that somehow reaches it.
- The pickers' weekday header row now truncates culture day names on **grapheme** boundaries rather than after two `char`s. No shipping ICU culture has an abbreviated day name whose second character is astral, but the old slice would have cut such a surrogate pair in half and emitted a lone high surrogate (a replacement glyph). Costs nothing for the ASCII and single-glyph (ja, zh) names that are all the realistic ones — they never reach the truncation.
- **A picker's half-typed input text no longer survives an external value change.**
  - `DatePicker._edit`/`DateRangePicker._startEdit`/`_endEdit` persisted across `Value`/`Start`/`End` changes, so swapping the bound record mid-type left record A's keystrokes on screen over record B's field — and the next Enter/blur committed them onto record B.
  - Each buffer is now dropped when its own bound value changes from outside (per endpoint for the range picker, so typing in one input survives an external change to the other); a re-render that leaves the value alone still keeps in-progress typing intact.

**Internal**
- ~14 commits of pure internal refactoring:
  - Extracting shared bases/components used by multiple controls (`EditInputShell`'s size/CssClass/count-text assembly, `CheckboxOptionList`, `RadioOptionItem`, `SelectOptionList`, `EnumOptionCache`, `EditControlParametersBase`, `OverlayActivationBase` for `Modal`/`Drawer`, `PopupOverlayBase` for `Popover`/`Popconfirm`, `ToastQueue` for `MessageService`/`NotificationService`, `UiKitIcons`).
  - Deduping CSS (`Popover`/`Popconfirm` floating-panel rules) and e2e test scaffolding (hoisted per-control demo-page/baseline tests).
  - Dependency and test-infra bumps (ASP.NET Core Components 10.0.0 → 10.0.10, bUnit migrated to 2.8.6, Microsoft.Playwright 1.49.0 → 1.61.0 with baselines regenerated for the newer bundled Chromium's font rendering, Test SDK/xunit-runner/coverlet bumps).
  - No consumer-facing API or behavior change in any of these — one purely cosmetic `<option>` attribute-order difference in `EditSelectString`'s rendered markup aside.
- A repo-wide DRY audit pass (~16 commits):
  - New intermediate bases (`RadioGroupControlBase<TValue>` for the radio pair, `CheckedListControlBase<TItem>` for the checkbox-list pair, `EditTextControlBase<TValue>`/`EditTextInputBase` for the text-shaped scalars) hoisting each family's hand-synced members with parameter names/types/defaults unchanged.
  - The two pickers' verbatim-duplicated 55-line time row extracted into one `PickerTimeRow` (with its DisabledTime/step-filter/never-jump invariants documented once) and their mode→format/first-day-of-week/read-only-display logic consolidated into `PickerMath`.
  - `EditDateRange`'s ~70-line mirrored copy of the list base's validation-subscription/field-registration plumbing hoisted into `EditControlParametersBase`; every lazy JS module import unified on an internal `JsModule` holder; every inline SVG glyph single-sourced from the icon registries.
  - The 17-copy `OnInitialized`/`InitState` boilerplate moved into the two control roots (`InitState` de-genericized — a protected-surface source break only for out-of-repo subclasses of the abstract bases, none known); `SelectLabelCache` for the searchable wrappers' read-only labels.
  - CSS token/grouping consolidation (`--edit-shadow`, `--edit-ease-standard`/`--wss-ease-standard`, `--edit-color-border`, shared backdrop/button-chrome/clear-button groups, the checkbox check-glyph SVG as a per-file token); a shared JS `applyVerticalFlip`.
  - The bUnit `WithForm` builder deduplicated from 41 files into one helper, and a new scaffold conformance suite asserting the shared wrapper/label/star/aria invariants across all 19 form controls (the net that would have caught the `EditBool` star drift).
  - DOM output byte-identical throughout except where the **Changed**/**Fixes** entries above say otherwise.
- A second, wider DRY pass closing the nine-area audit's structural findings (~25 commits):
  - `EditControlInit` absorbed the two roots' hand-typed init/diagnostic/ARIA sequences (`InitAndRegister`, `RequireBinding`, an `IEditControl` overload of `ResolveAriaState`, plus the shared date-default and picker-splat builders `EditDate`/`EditDateNative`/`EditDateRange` each had a copy of).
  - The validation-state subscribe/re-point/detach mechanics moved to a standalone `ValidationStateSubscription` that `ValidationView` and the parameter base both drive; a new `EditSelectBase<TValue>` makes `EditSelect`/`EditSelectString`'s three byte-identical overrides compiler-enforced rather than comment-enforced.
  - `SelectDefaults` gives the select family's literal defaults and placeholder chain one home; `RangeSentinels` replaces the two hand-synced "this bound means no bound" lists; `BoundValueDisplay` collapses the 19-copy debug block; `EditNumber`'s two 11-case format switches became one `FormatNumber`; `AttributesHelper` gained the `NonZero`/`Positive` attribute-sentinel pair.
  - The two pickers' eight duplicated disabled-predicates/first-enabled scanners moved to `PickerMath`, their six day-button copies to three `RenderFragment<T>`s, their month/weekday headers to shared components and the 20-argument time-row invocation to a `PickerTimeRowSlot`.
  - `Table`/`SearchInput` share fragments for the sort trigger, column filter, selection checkbox, pagers and spinner; `wss-select.js`'s `placeDropdown` and `wss-overlay.js`'s `placePanel` merged onto one `placeAnchoredPanel` (verified equivalent over 33,600 geometry combinations); and both stylesheets' duplicated rule bodies were grouped (close buttons ×4, focus rings ×7 → 2, pagination select/jumper, picker range bands).
  - Rendered output byte-identical throughout except for whitespace-only text nodes and the two markup changes called out under **Changed** above.
- `EditDateRange` and `EditFile` evaluate their "is this field invalid?" checks once per render instead of five and four times respectively — each read was a fresh `EditContext.GetValidationMessages(...).Any()` walk for an answer that cannot change within a render pass. The answers are threaded through as arguments rather than cached in a field, since validation state moves outside the parameter lifecycle. `EditDateRange.FieldCssClass` becomes a `protected` method taking the End-field answer (a source break only for an out-of-repo subclass; the control isn't designed for one) and `EditFile`'s private `_hasError` becomes `HasError(bool)`. No rendered output changes.

**Demo** (`WssBlazorControls.Demo`)
- `DemoEditDate.razor` now demos the calendar-dropdown picker and `DemoEditDateNative.razor` the native input, following the rename — swapped content, not new pages.
- `DemoEditRadio`'s eager validation actually runs again (its copy of the shared page boilerplate had been edited to gate on a cascading parameter nothing cascades, so its Required section never showed the invalid state on load); the two checked-list pages drop their `Task.Delay(10)` validation timing hack; and the `EditForm? _form` + eager-validate boilerplate all 22 demo pages repeated now lives once on a `DemoFormPage` base, so a page's copy can't silently drift again.

### 10.6.7

**New** (Edit Controls)
- `EditBool.Indeterminate` (`bool`, default `false`) — AntD's visual-only "mixed" checkbox state (does not change the bound value).
  - Applied via JS after render (there's no HTML attribute for the `indeterminate` DOM property) through a new shared `wss-checkbox.js` module; the UI-kit `Table`'s header "select all" checkbox now imports the same helper instead of its own copy (`wss-table.js` re-exports it, so its module path is unchanged).
  - Works with or without `UseStyledCheckbox`; degrades to a plain checked/unchecked box with no JS runtime. See [Indeterminate ("mixed") state](#indeterminate-mixed-state).
- `IsOptionDisabled` (per-option disabling) on `EditCheckedStringList` (`Func<string, bool>?`), `EditCheckedEnumList<TEnum>` (`Func<TEnum, bool>?`), `EditRadioString` (`Func<string, bool>?`), and `EditRadioEnum<TEnum>` (`Func<TEnum, bool>?`) — disables the matching option in addition to (not instead of) the whole-group `IsDisabled`.
  - Null (default) disables nothing. See [Per-option disabling](#per-option-disabling).
- `EditRadioString`/`EditRadioEnum<TEnum>` gain `OptionType="RadioOptionType.Button"` — Ant Design's segmented "button" radio look (joined bordered buttons instead of plain radios), with `ButtonStyle` (`RadioButtonStyle.Outline`/`Solid`) and `Size` (the existing `SelectSize`) applying only in button mode.
  - Same `InputRadio`/`InputRadioGroup` keyboard semantics; button mode is inherently horizontal (`IsHorizontal` is ignored) and composes with `HasOther`/`HasOtherOption` and `IsOptionDisabled`.
  - New CSS-only mode (`.edit-radio-button-*` in `edit-controls.css`), not gated behind `.edit-theme`; default mode's markup is unchanged. See [Button-style radio group](#button-style-radio-group-optiontypebutton).
- `EditFile` AntD 4.x parity batch (Upload, minus the transport, which stays deliberately out of scope):
  - `AllowedExtensions` now also accepts full MIME types (`"application/pdf"`) and MIME wildcards (`"image/*"`) alongside bare/dotted extensions, honored by both the `accept` attribute and validation (previously every token was dot-prefixed regardless of shape, turning a MIME token into a meaningless, silently-rejecting one).
  - `BeforeAdd` (`Func<IBrowserFile, Task<bool>>?`) is a new async per-file gate run after the built-in format/size/count/duplicate checks and before buffering, with a localizable `BeforeAddRejectedMessageFormat`.
  - Every selected file's row (edit-mode and read-only) now shows its formatted size; and `Variant="EditFileVariant.Button"` swaps the dashed dropzone for a compact plain button (`ButtonText`), built on the same invisible-`<InputFile>`-overlay technique so keyboard/focus/click behavior match.
  - All additive — default `Variant="Dropzone"` markup is unchanged, and the empty-list state renders no new markup. See [File upload parity features](#file-upload-parity-features-editfile).

**New** (UI Kit)
- Hover tooltips (`data-tooltip`) — ported from the RPG Assistant app's `data-tooltip` convention.
  - Not a component: a `data-tooltip="..."` attribute on any element gets a styled CSS-only hover/focus tooltip (arrow, slide-in, `:focus-visible` support, hidden under `hover: none`) via new rules in `wss-controls.css`, themed through `--wss-*` tokens plus the new `--wss-tooltip-gap`/`--wss-tooltip-z-index` knobs.
  - The optional new `wss-tooltip.js` (a plain `<script>` tag, no interop) auto-places the bubble — above/below and left/right — based on the trigger's position within its nearest clipping ancestor or panel boundary (`wss-modal`/`wss-drawer`/`wss-popover`), so it stays inside a Modal/Drawer instead of running past the edge. See [Hover tooltips](#hover-tooltips-data-tooltip).
- `Select`/`EditSelectSearch`/`EditMultiSelect` AntD 4.x parity batch:
  - `Loading` (spinner in the arrow's slot + `aria-busy`) and `ShowArrow` (default true, unlike Ant Design's hide-for-searchable-multi default — kept on to preserve byte-identical DOM).
  - `SelectOption.Group` renders an AntD-`OptGroup`-style header before each contiguous run of a shared group name in the flattened, virtualized dropdown (keyboard nav skips header rows; a header shows only while one of its options survives the filter).
  - `FilterOption` replaces the default `Label.Contains` match (including for an empty search — `(_, _) => true` disables client filtering for a pure server-driven `OnSearch` flow); `EmptyContent` (richer alternative to `EmptyText`) and `DropdownFooter` (Ant Design's `dropdownRender`, pinned after the list, its own clicks never select/close).
  - A two-way-bindable `Open`/`OpenChanged` (`@bind-Open`) that routes an externally-driven open/close through the same JS placement/focus path as user interaction, guarded against re-triggering on its own echoed value.
  - `SelectVariant.Borderless` (single-select only — `EditMultiSelect` doesn't forward `Variant`); and `wss-select.js`'s `placeDropdown` now clamps horizontally (mirroring its existing above/below flip) when a wide dropdown would run off the right edge of the viewport.
  - All additive — see [Select parity features](#select-parity-features-select--editselectsearch--editmultiselect).
- `Pagination`/`Table` AntD 4.x parity batch, all additive/dependency-free (no new JS module):
  - `Pagination` gains `ShowTotal` (the "1-10 of 200 items" leading text), a `PageSizeOptions` native `<select>` size-changer (`PageSize`/`PageSizeChanged` now two-way bindable; changing size re-clamps `Current` to keep roughly the same data window in view), `ShowQuickJumper` (a "Go to" input, Enter commits and clears), and `Small` (AntD's compact size, CSS-only).
  - `Table` forwards `ShowTotal`/`PageSizeOptions` to its embedded in-memory pager (selection stays keyed by row identity across a page-size change) and adds: `Loading` (a translucent mask + spinner over the body, rows still rendered beneath it, `aria-busy` on the wrapper); `IsRowSelectable` (per-row selection predicate — a rejected row's control renders `disabled` and is excluded from header select-all/indeterminate math); `SelectionMode.Single` (radio-semantics selection, one shared native radio group per `Table`, an empty header cell in place of select-all).
  - Controlled expansion via `ExpandedRowKeys`/`ExpandedRowKeysChanged` layered over the existing uncontrolled expansion set, plus `OnExpand` (raises on every toggle regardless of control mode); `ExpandRowByClick` (whole-row click toggles `RowDetail`) and `OnRowClick` (always fires; composes with `ExpandRowByClick`) — both stop propagation from the selection checkbox/radio, the expand chevron, and `ActionColumn` cells, so existing action buttons need no changes.
  - `Column.Ellipsis` (CSS truncation — the table switches to `table-layout: fixed` only once ≥1 column requests it; `PropertyColumn` also adds a hover `title` with the full text); `EmptyContent` (richer alternative to `EmptyText`); and `FooterContent` (a consumer-supplied summary row in a `<tfoot>`, unaffected by paging/sorting).
  - See [Pagination parity features](#pagination-parity-features-pagination)/[Table parity features](#table-parity-features-tabletitem).
- `Table` column filtering + `ScrollY`, additive/opt-in (unset, DOM is unchanged):
  - `Column.FilterOptions` (a new `TableFilterOption` `Text`/`Value` list) + `OnFilter` (`Func<TItem, string, bool>`) render a funnel-icon header button (after the sort control on a sortable column) that opens a checkbox (or, with `FilterMultiple="false"`, single-select radio) dropdown — OK applies and closes, Reset clears immediately, an outside click discards pending checkbox changes without applying them (the same JS-free backdrop pattern as `Popover`/`Select`).
  - A row passes a column's filter when `OnFilter` matches any selected value (OR within a column); every filterable column must pass (AND across columns). Filtering runs before sorting/paging, and a selected row filtered out of view stays in `SelectedItems` (same key-based preservation as paging).
  - Uncontrolled filter state only — `Table.OnFilterChanged` (`EventCallback<(Column<TItem> Column, IReadOnlyList<string> SelectedValues)>`) observes every apply/reset; there is no fully-controlled `filteredValue` equivalent.
  - `Table.ScrollY` (`string?` CSS length) bounds the body to a fixed height with its own scrollbar and a sticky header (AntD's `scroll.y`; viewport-level sticky is out of scope) — a filter dropdown that would otherwise be clipped by that wrapper's overflow escapes via `position: fixed` (a new `wss-overlay.js` export, `placeFixedBelow`), falling back to the CSS-anchored position with no JS. See [Table parity features](#table-parity-features-tabletitem).
- `Modal`/`Drawer` AntD 4.x parity batch, all additive (unset, DOM/behavior unchanged):
  - `Modal.Centered` (`bool`) vertically centers the dialog instead of the default fixed 100px-from-top offset (CSS-only, `wss-modal-wrap-centered`).
  - Both gain a `Keyboard` (`bool`, default true) parameter that now solely governs Escape-to-close, decoupled from `Closable` (which now only shows/hides the header X) — matches AntD, where `keyboard`/`closable`/`maskClosable` are three independent knobs; previously Escape was gated by `Closable`, so a consumer who set `Closable="false"` to hide the X also silently lost Escape-to-close (now set `Keyboard="false"` explicitly for that).
  - `Drawer.Extra` (`RenderFragment?`) renders a header-right slot beside the close button (AntD's `extra`) — grouped with the close button in a new `wss-drawer-header-actions` wrapper only when `Extra` is set (unset markup is byte-identical to before); `Extra` alone (with no `Title`/`Closable`) now forces the header to render.
- `Popconfirm` AntD 4.x parity batch, all additive:
  - `OnConfirm` handlers that return a genuinely-pending `Task` (checked via `Task.IsCompleted` immediately after invoking — a still-synchronous/already-completed handler keeps today's immediate-close feel) now keep the popup open with both buttons disabled and a small spinner in the OK button until the task resolves, closing only on completion; an exception closes the popup and rethrows (never swallowed).
  - `OkDanger` (`bool`) applies red/danger primary styling to the OK button (new `wss-dialog-btn-danger` class, alongside the existing `wss-dialog-btn-primary`).
  - Both `Popconfirm` and `Popover` gain a controlled `Visible`/`VisibleChanged` (two-way, `@bind-Visible`) mirroring `Select`'s controlled `Open`/`OpenChanged` design: an external `Visible` change while `VisibleChanged` has a delegate routes through the same open/close path as user interaction (JS placement/focus still runs), every open/close raises `VisibleChanged` back, and a `_lastVisibleParam` guard prevents a `@bind-Visible` echo from re-triggering.
  - `Popconfirm.Disabled` (existing param) now also closes an already-open popup the moment it becomes true (any control mode) and ignores an externally-forced `Visible="true"` while disabled — the same "Disabled ⇒ closed" invariant as `Select`'s controlled `Open`. `Popover` has no `Disabled` parameter, so its controlled `Visible` has no such guard.
- `Alert` gains `Banner` (`bool`) — AntD's banner mode: full-width, no border/radius, and (when `Type` is left at its default `Info`) a `Warning`-style icon/tint to match AntD's banner default; an explicitly-set `Type` is left alone. `Action` (`RenderFragment?`) renders a trailing slot before the close button (new `wss-alert-actions` wrapper only when `Action` is set — unset markup is unchanged).
- `SearchInput` gains `AllowClear` (`bool`) — a clear × button (reusing `EditInputShell`'s `EditIcons.ClearCircle`) rendered as a new flex sibling between the input and the search button whenever there's a non-empty `Value` (never when `Disabled`); and `EnterButtonText` (`string?`) — when set, the search button renders this text (primary-styled, `wss-search-btn-enter`) instead of the search icon (AntD's `enterButton="Search"`).
- `NotificationContainer`/`WasmNotificationContainer`/`NotificationListView` gain `Placement` (`NotificationPlacement`: `TopRight` default / `TopLeft` / `BottomRight` / `BottomLeft`) — a render-tree-scoped parameter on the container components (MFE-safe; not a service-level setting) that repositions the fixed toast stack and its slide-in direction. Bottom placements stack newest-nearest-the-edge automatically (no list reordering needed — the container just anchors from the opposite side, and column layout does the rest).
- `Tabs` gains `TabBarExtraContent` (`RenderFragment?`, a right-aligned strip slot — wrapped in a new `wss-tabs-nav-wrapper` only when set), `Centered` (`bool`, centers the tab buttons via `wss-tabs-nav-centered`), and `Type` (`TabsType`: `Line` default / `Card` — AntD's boxed "card" tabs, CSS-only via `wss-tabs-card`; keyboard/ARIA are identical to `Line`).
- `Skeleton` gains `Avatar` (`bool`) + `AvatarShape` (`SkeletonAvatarShape`: `Circle` default / `Square`) — an avatar placeholder block beside the title/paragraph (wrapped in new `wss-skeleton-header`/`wss-skeleton-content` elements only when `Avatar` is set). A new minimal `SkeletonElement` component (`Kind`: `SkeletonElementKind.Button`/`Input`, plus `Active`) covers AntD's standalone `Skeleton.Button`/`Skeleton.Input` shapes without adding N separate components.

**Changed** (Edit Controls)
- `LabelTooltip` (the form-label help-icon popover) is restyled to AntDesign's dark tooltip look — opaque dark chip, 6px radius, arrow, AntD's layered shadow, fade/slide-in — and now auto-places like `data-tooltip` instead of always opening above.
  - The bubble opens below the trigger by default and aims toward the center of the nearest clipping ancestor/panel (flipping above, aligning left/right near an edge) via `wss-tooltip.js`, which `LabelTooltip` lazily imports itself — consumers add nothing, and without JS the CSS default (below, centered) still renders. Hover shows after the same 0.35s hover-intent delay as `data-tooltip`; keyboard focus stays instant.
  - Theming: `--color-tooltip-bg`/`--color-tooltip-text`/`--edit-tooltip-z-index` still honored (the arrow follows the bubble background automatically); new `--edit-tooltip-gap` (default `24px`, below) and `--edit-tooltip-gap-tight` (`3px`, above) knobs; the bubble no longer draws a `--border-color` border. Anything that relied on the old always-above placement will see the new placement.
- `LabelTooltip`'s reveal is now pure CSS `:hover`/`:focus` instead of a C# round-trip per hover; `aria-hidden` on the bubble now carries only the Escape-dismissed state (starts `"false"`, flips `"true"` on Escape until pointer/focus leaves).
  - Rationale: re-rendering mid-hover mutated the DOM under the pointer, and the browser's rebuilt hover chain fired spurious `mouseleave`s that dismissed the bubble while the pointer traveled onto it.
  - Accessibility of the new look/placement, verified end-to-end: the bubble is **hoverable** (WCAG 1.4.13 — pointer-interactive with an invisible gap bridge that exists only while open, so the pointer can travel from icon to bubble and rest on it; text is selectable), Escape-dismiss kept (1.4.13), `prefers-reduced-motion` drops the fade/slide (2.3.3, mirroring the UI kit), and a transparent border keeps a visible bubble boundary under forced-colors/Windows High Contrast. Hover reveal now also works pre-hydration (it previously waited for interactivity).

**Fixes** (Edit Controls)
- `EditRadioEnum<TEnum>`'s `HasOtherOption` free-text input now honors `IsOptionDisabled`: previously the input's `disabled` expression checked only `IsDisabled`, so a predicate disabling the Other enum value locked the radio button but left its paired text input editable.
  - Both render modes (`Default` and `OptionType="Button"`) were affected. (`EditRadioString`'s `HasOther` input is unaffected — by design, `IsOptionDisabled` never applies to the built-in Other option there, since it has no corresponding `Options` entry.)
- **`EditFile`: a second `<InputFile>` change event firing while `LoadFiles` was still suspended** inside `BeforeAdd` (or while buffering a file's bytes) used to run concurrently against the same bound list — bypassing `MaxFiles`/`MaxTotalBytes` (both checked against a `Value` snapshot taken at the top of the method) and risking an `ArgumentException` from two overlapping `EditContext.NotifyFieldChanged` calls for the same field.
  - `LoadFiles` now guards itself with a synchronous re-entrancy flag: a re-entrant call while a batch is in flight returns immediately without touching `Value`/`EditContext` (reject, not queue), and the `<InputFile>` is disabled for the duration of the batch so a real user can't trigger it through the UI — only a synthetic double-fire could.
- `EditFile`'s `AllowedExtensions`: a bare `"*"` or full `"*/*"` accept token now means "accept everything" (both previously normalized into a shape that matched no file, silently rejecting every upload) — `"*"` renders as `"*/*"` in the `accept` attribute; both OR normally with any other tokens in the same list.
  - Leading/trailing whitespace on any token (extension or MIME shape) is now trimmed instead of causing every file to be rejected.

**Fixes** (Select engine — `Select` / `EditSelectSearch` / `EditMultiSelect`)
- **Disabled ⇒ dropdown closed.** An externally forced `Open="true"` (the controlled `@bind-Open` case) previously bypassed `Disabled` entirely — `OnParametersSetAsync` routed it straight into `OpenAsync` with no `Disabled` check, unlike `OnWrapperClickAsync`'s existing gate — so a parent could force open a disabled select and a click on a rendered option would still fire `ValueChanged` (`.wss-select-disabled` has no `pointer-events: none`, so this was real-browser exploitable, not just a controlled-mode curiosity).
  - `OnParametersSetAsync` now ignores an external `Open="true"` while `Disabled` (an external `Open="false"` is still honored) and closes an already-open dropdown through the normal `CloseAsync` path the moment `Disabled` becomes true, controlled or not — `OpenChanged` still fires and JS/focus cleanup still runs.
  - Hardened in depth: `SelectAsync`, `ClearAsync`, `CommitTagAsync`, and the keyboard/search-input handlers now also no-op while `Disabled`, matching `OnWrapperClickAsync`'s whole-method gate, so a mutation can't reach the bound value through those paths either even if the dropdown were somehow still rendered open.
- `FilterOption` is now tracked by reference in the same change-guarded block as `Options`/`Values`: swapping the delegate (with `Options` unchanged) refreshes an already-open dropdown's filtered list immediately, instead of leaving it stale until the next keystroke or reopen.

**Fixes** (UI Kit — `Table`)
- **`SelectionMode.Single` now actually enforces "at most one selected" from every entry point, not just a user picking a row.**
  - Previously only `SelectSingleAsync` (the radio's own `@onchange`) cleared any prior selection first — a runtime `Multiple` → `Single` mode switch with several rows already checked, or a controlled `SelectedItems` handing in 2+ items while already in `Single` mode, both left multiple checked radios in one native `name` group.
  - `OnParametersSet` now clamps `_selected` down to its first (insertion-order) item whenever `SelectionMode` is `Single` and more than one item is present, and raises `SelectedItemsChanged` with the pruned list when the clamp actually dropped something, so a bound `SelectedItems` reflects reality.
- A runtime `IsRowSelectable` or `SelectionMode` change with nothing else different (same page, same sort, same data) used to leave the header "select all" checkbox's disabled/indeterminate state stale.
  - `RebuildPageItems`'s memo guard compared only the sorted view, page, and page size, so it skipped `RecomputeSelectionFlags` entirely. The guard now also tracks the `IsRowSelectable` delegate reference and `SelectionMode`, so either one changing forces a fresh recompute (and the JS indeterminate re-sync that reads it).
- **`Loading`'s translucent mask now covers the whole component** — both pagers plus the body, matching AntD's `Spin`-wrapped-table look — instead of just the table body.
  - The mask previously lived inside `.wss-table-wrapper` (`position: relative` there, `inset: 0` against it), so the top/bottom pager blocks — wrapper siblings — stayed visually uncovered and clickable while loading.
  - The positioning context moved to the root `wss-table-root` element and the mask now renders there directly, so it visually and structurally sits above the pagers too; `aria-busy="true"` moved from the wrapper to the root to match (only changes the DOM when `Loading="true"`).
- A `ScrollY` sticky header cell always establishes its own CSS stacking context (`position: sticky` does this regardless of z-index), which trapped an open filter dropdown's z-index below `Loading`'s mask.
  - The mask visually and functionally covered the dropdown's OK/Reset buttons whenever both were active on the same table, no matter how high the dropdown's own z-index was raised (a nested-stacking-context limit, not fixable from the dropdown's side alone).
  - The column whose filter is currently open now gets its own header cell promoted above the mask via a plain CSS class (`Column.FilterOpen`-driven, no JS), scoped to that one column so every other sticky header cell is unaffected.
- A `ScrollY` filter dropdown escaping the wrapper's overflow clip (`position: fixed`, computed once at open) detached from its trigger the moment the page scrolled or the viewport resized — there were no listeners keeping it in sync.
  - `wss-overlay.js` now wires window-level, capture-phase scroll/resize listeners for as long as the dropdown stays open (`activateFixedDropdown`, returning a dispose handle mirroring `activateModal`'s), so it tracks the trigger continuously and cleans up deterministically on close and on component dispose — no leaked listeners.
- Clicking a filter's OK with nothing (re-)ticked, or Reset on a column with no applied filter, no longer resets the current page — only an applied selection that actually changes does.
  - `Table.OnFilterChanged` now also skips firing on that same no-op, and gains a third trigger: a column that was actively filtering rows raises it (with an empty payload) when it drops out of the rendered set (e.g. an `@if` hiding it) — previously its filter state was silently cleared with no notification, leaving a consumer's own filter-summary display stale.
- A sortable + filterable column's header could push its filter button out of (or past) a narrow `table-layout: fixed` column — the sort trigger, a flex item with no `min-width`, refused to shrink below its label's full nowrap width. The label now truncates with an ellipsis instead.

**Fixes** (UI Kit — `Popconfirm` / `Popover` / `Alert`)
- `Popconfirm`: re-enabling after a `Disabled`-forced close (with the consumer still holding `Visible="true"` the whole time) now reopens the popup. `OnParametersSetAsync` previously recorded the suppressed request's value before returning, so once `Disabled` cleared, the still-true `Visible` compared equal against that stale recording and never took effect.
- `Popconfirm`: a genuinely pending `OnConfirm` now locks the popup closed against Escape, a backdrop click, the Cancel button, and an external controlled `Visible="false"` — all of these used to close the popup and (for Escape/backdrop/Cancel) fire `OnCancel` while `OnConfirm` kept running unobserved.
  - All four now wait/no-op until the pending task settles; the popup then closes itself through the normal path, raising `VisibleChanged` exactly once.
- `Popconfirm`/`Popover`: an `OnAfterRenderAsync` positioning attempt (`place()`) that overlapped a close/reopen could leave stale `_positioned`/`_pendingFocus` state, occasionally skipping the next open's own re-measure/focus. Guarded with a sequence token, mirroring `Modal`'s existing `_activationSeq` fix (see the 2026-07-07 entry below).
- `Popconfirm`: the OK button now reliably gains focus when opened via a controlled `Visible="true"` from a separate trigger elsewhere on the page (e.g. `@bind-Visible`) — a same-tick `FocusAsync()` call in that path could lose a race against Blazor's own render-batch focus-restore and silently never stick.
  - Routed through a new `wss-overlay.js` `focusDeferred` helper that retries via `requestAnimationFrame` until the focus is verified to have landed.
- `Alert`: `Type` no longer goes stale across a re-render that stops passing it. Standard Blazor parameter semantics leave an omitted parameter's backing value at whatever a prior render set it to; a non-`Banner` alert that stopped passing `Type` kept rendering the last severity it was ever given instead of the documented `Info` default.

### 10.6.6

**Fixes / polish** (MFE-compatibility follow-up)
- `.edit-sr-only` now uses the clip-based visually-hidden pattern (`clip-path: inset(50%)` + 1px box + `-1px` margin) instead of `left: -10000px` — the offscreen-position pattern could be un-hidden by a consumer MFE shell's CSS resetting `position`/`left`. Matches `.wss-sr-only`'s existing approach; no visible change for anyone not already relying on the bug.
- `EditString`'s masked read-only wrapper (the `<span>` + eye-toggle `<button>` shown when `MaskText` is set and `IsEditMode=false`) now carries a `edit-masked-value` class, styled `display: inline-flex; align-items: center; gap: 4px`. Consumers previously had to target this wrapper with a `:has()` hack; style `.edit-masked-value` directly instead.
- `.edit-tooltip-content`'s `z-index` is now the overridable `var(--edit-tooltip-z-index, 10000)` (was a hardcoded `100`) — tall consumer stacking contexts (drawers, modals) no longer bury the tooltip popover beneath them. Override `--edit-tooltip-z-index` at any scope if 10000 still isn't high enough.

**Demo** (`WssBlazorControls.Demo`)
- New `DemoEditDatePicker`/`DemoEditDateRange` pages (sidebar views `DatePicker`/`DateRange`): basic binding, Label/read-only/`Min`-`Max` variants, fixed-date presets, and `[Required]` validation sections. Both controls also joined the All Controls kitchen-sink view.

**Picker fixes** (post-10.6.5 audit of the new calendar pickers)
- `EditDatePicker`/`EditDateRange` accessible names now honor the `Label` parameter: the input `aria-label` resolves `InputLabel` → `Label` → the field's auto-derived label (previously `Label` was skipped, so a control with `Label` set spoke a different name than it displayed — WCAG 2.5.3).
  - `EditDateRange` composes unique per-input names — `StartInputLabel`/`EndInputLabel` when set, else `Label` + " start"/" end", else each field's own auto-name; `EndInputLabel`'s default is no longer the literal "End date" (it now derives from the End field like Start always did) and the parameter is now nullable.
- The day grid's roving Tab stop skips disabled days: with `Min` in the future (or the bound value outside `Min`/`Max`), the default focus day used to be a `disabled` button, making the whole grid unreachable by keyboard. The stop now falls back to the first enabled day in view.
- Prev/next month buttons actually disable at the `Min`/`Max` view bounds, as documented — previously they only stopped at the representable-date edges and would page into fully-disabled months.
- Panel-originated closes (picking a day, Enter, Escape, preset click) return focus to the picker's text input instead of stranding keyboard focus on `<body>` when the dropdown unmounts; outside-click closes leave focus where the user clicked.
- `DateRangePicker`: arrowing forward past the end of the right panel's month now shifts the view one month (focused month becomes the right panel) instead of leapfrogging two.
  - Keyboard focus after a forward month-boundary move lands on the in-month day cell, not the left grid's dimmed adjacent-month duplicate (`wss-picker.js` now prefers the roving-tabindex match).
- Both pickers are now explicitly Gregorian-calendar controls under every culture: cultures whose default calendar isn't Gregorian (th-TH Buddhist, ar-SA Hijri) previously got self-contradictory chrome (Hijri month names over a Gregorian grid; a Buddhist-year input beside a Gregorian year select).
  - All picker-internal formatting and typed-input parsing — including `EditDatePicker`/`EditDateRange`'s read-only display, which must agree with edit mode — now use the culture's language with the calendar forced to Gregorian. Behavior under Gregorian-default cultures (en-US etc.) is unchanged.

**Picker parity fixes** (second post-10.6.5 audit)
- Invalid pickers now show the error-red border every other control gets: new `.wss-picker.invalid` rules mirror `.wss-select.invalid` (red border at rest, re-asserted while open/focused; the single-date variant's focus ring also flips to the error shadow).
  - Previously `EditDatePicker`/`EditDateRange` forwarded the `invalid` state class onto the wrapper but no stylesheet rule consumed it, so an invalid picker was pixel-identical to a valid one.
- `EditDateRange` in read-only mode now forwards the consumer's `class` plus the Start field's EditContext state classes (`modified`/`invalid`/custom `FieldCssClassProvider` output) to the read-only value, matching edit mode and every other control's read-only view — previously both were silently dropped.
- Both pickers now honor the documented `HidingMode.*NullOrDefault` contract for `default(DateTime)` (0001-01-01): `EditDatePicker` overrides `IsValueDefault` like `EditDate`, and `EditDateRange` treats a null-or-default pair as empty. Previously a `default(DateTime)` value kept the control visible where `EditDate` would hide it.

**Fixes** (multi-angle audit of the 10.6.3 UI-kit surface + range picker)
- `Tabs` — the strip rendered one render behind for parameter changes on an existing `Tab`: Blazor builds the strip markup before the child `Tab`s' parameters update, and only a *new* tab triggered the corrective re-render, so a `Count` chip updated after a data load (or a runtime `Title` change, or a `Disabled` flip) kept showing the old value until some later unrelated render — a just-disabled tab also kept its enabled-looking, non-`disabled` button.
  - A `Tab` now detects display-relevant parameter changes and requests the follow-up render (change-guarded, so fragment-bearing tabs don't loop).
- `Tabs` — Home/End are no longer handled by the key switch: Blazor has no per-key `preventDefault`, so the browser also scrolled the document to top/bottom before focus yanked it back. Arrows (with wrapping) remain the ARIA tabs navigation; matches the library's established no-JS keyboard policy.
- `SearchInput` — the input had no accessible name when the addon was supplied as an `AddonContent` template (the `aria-label` fallback only considered `AddonLabel`); with `AddonContent` + `Id` and no labels the input's `aria-labelledby` now points at the addon chip, whose `{Id}-addon` id previously dangled unreferenced.
- Pill `Select` variant (`Variant="Pill"`) — the pill's hover/focus/open rules out-ranked the validation `invalid` rules (same-specificity, later in file), so a focused, hovered, or open invalid pill select showed pill-colored chrome instead of the error red; dedicated `.wss-select-pill.invalid` overrides now keep the error border and ring.
  - The pill focus ring also no longer consults `--wss-primary-shadow` — it derives purely from the pill color, as documented (computed default unchanged).
- `EditDateRange` — the shared wrapper's state classes derived from the Start field only, so an End-only validation error (required End left empty, an "End ≥ Start" rule) rendered a normal border while the error message and the End input's `aria-invalid` were live.
  - The wrapper now folds End invalidity into its class (edit and read-only modes both), completing the 10.6.x "invalid pickers get the error border" fix for the range control's most common failure case.

### 10.6.5

**New features**
- `EditDatePicker`/`EditDateRange` — form-bound versions of the UI-kit calendar pickers.
  - `EditDatePicker` binds a `DateTime?` via `@bind-Value` (an `InputBase`-derived scalar control, same contract as every other `Edit*`); `EditDateRange` binds two model properties via `@bind-Start`/`@bind-End`, registers both fields with the form, and validates each independently with its own message.
  - Both render the standard label/required-star/validation scaffolding around the calendar dropdown, support read-only mode via `DateFormat`, and forward the pickers' parameter surface (`Min`/`Max`, `Format`, `Presets`, placeholders, accessible-name params).
  - To support them, `DatePicker`/`DateRangePicker` gained validation-state ARIA passthrough onto their actual inputs (`AriaRequired`/`AriaInvalid`/`AriaDescribedBy`/`AriaErrorMessage`, doubled as `StartAria*`/`EndAria*` on the range picker — the same forwarding shape as `Select`'s trio) and the range picker gained `EndId` (an id for its end input).
  - Use `EditDate` when the browser-native date input is fine; use these when the form wants the AntD-style calendar UX.
- `DatePicker`/`DateRangePicker` calendar round-out:
  - A weekday header row above each day grid (culture-abbreviated names, ordered by `FirstDayOfWeek`); prev/next month buttons flanking the month/year selects (`PrevMonthLabel`/`NextMonthLabel` localize their accessible names; they disable at the `Min`/`Max` view bounds, and `DateRangePicker` places prev on the left panel and next on the right).
  - Roving-tabindex keyboard navigation over the day grid — Arrow keys move by day/week, Home/End jump to the focused week's ends, PageUp/PageDown step a month, and the view follows focus across month edges (page-scroll suppression comes from a new lazily-imported `wss-picker.js`, gracefully absent without JS).
  - `DateRangePicker` tints the prospective span on hover while the second day is being picked (override via `--wss-picker-preview-bg`).
  - A stale `Field="..."` attribute on either picker now fails the build (the same inert `[Obsolete]` guard as the form controls).

**Bug fixes**
- `Table` — a sortable column with `TitleContent` rendered the template inside the sort `<button>`: with interactive template content (the README-advertised `LabelTooltip` composition) that nested a button inside a button (invalid HTML) and clicking the info icon toggled the sort, and an icon-only template left the sort button with no accessible name.
  - The template now renders in its own clickable area beside a caret-only sort button (header clicks still sort; the button is named from `Title`, falling back to "Sort"), and `LabelTooltip`'s trigger stops click propagation so it never triggers a clickable ancestor.
  - Also, changing `UseStyledCheckbox` at runtime no longer loses the header checkbox's indeterminate ("mixed") state across the styled/unstyled DOM swap.
- `DateRangePicker` — a typed commit made mid-pick (click one day, then type a date and press Enter) left the field displaying pending-pick state that contradicted the bound values, and a later day click resurrected the discarded pick; typed commits now finalize the field.
  - Presets were clamped only on one side of `Min`/`Max`, so a preset lying entirely past `Max` (or before `Min`) could commit days the calendar itself disables — both endpoints now clamp into the window.
  - Year selects (both pickers) could offer years beyond `DateTime`'s 1–9999 range and threw an unhandled exception when picked; the offered range and the selection handler now clamp.
- `edit-controls.js`'s `focusFirstInvalidField` DOM query substring-matched `[class*=" invalid"]`, which over-matched an unrelated consumer class like `class="foo invalid-hint"` — it now matches the exact `.invalid` class token only (the same false-positive shape `InvalidIcon.razor` and `EditControlBase.IsInvalid` already fixed for `CssClass`).
- `JsInteropEc` — `edit-controls.js` was the one JS asset `FormDefaults.AssetBase` didn't yet cover: in a cross-origin MFE whose host page doesn't serve/link `_content/WssBlazorControls/edit-controls.js`, `window.WssEditControls` is undefined, and `FocusFirstInvalidField` (unlike `FocusById`) threw instead of degrading gracefully.
  - All three methods (`FocusFirstInvalidField`, `FocusById`, `Log`) are now best-effort and never throw; when the global is missing they lazily `import()` the module (honoring an optional trailing `formDefaults` parameter, resolved through the same `JsModuleUrl` mechanism as the `wss-*.js` imports) and retry once, degrading quietly if that also fails.
- `wss-overlay.js`'s Modal/Drawer body-scroll lock and focus-trap stack were module-scoped, which was fine until `FormDefaults.AssetBase` (10.6.4) made it routine for two MFEs to import this module from different origin URLs — the browser instantiates a module once per distinct URL, so two "instances" could each believe they alone owned the document.
  - An interleaved open/close across instances could leave the page permanently scroll-locked (or unlock it while a dialog from the other instance was still open), and both instances' document-level Tab/Escape/focus listeners could fight over focus.
  - The scroll-lock counter and the trap stack are now shared via `window.__wssOverlayScrollLock`/`window.__wssOverlayTraps` (same pattern as the existing `window.__wssOverlayZ` z-index counter) — ref-counting and topmost-trap ownership now work correctly across instances. No API change; nothing for consumers to configure.

### 10.6.4

**New feature**
- `FormDefaults.AssetBase` — an absolute URL prefixed onto the RCL's lazy `wss-*.js` module imports (`Select`, `Modal`, `Drawer`, `Popover`, `Popconfirm`, `DatePicker`, `DateRangePicker`, `Table`).
  - Fixes a 404 for micro-frontends embedded into a host page that doesn't serve/proxy `_content/WssBlazorControls/*` — the `"./"`-relative import specifier otherwise resolves against the *host document's* origin instead of the MFE's own. Unset (the default) preserves today's relative import path.
  - Cascade it from the MFE's own root the same render-tree-scoped way as `FormDefaults`'s other settings — not a shared JS global — so multiple MFEs composed into one page don't stomp on each other's asset base. See [FormDefaults](#formdefaults).

### 10.6.3

**New features**
- `Table` expandable rows + templated headers (per the Clark Connect Vendor PO Management Figma spec):
  - `RowDetail` (a `RenderFragment<TItem>`) adds a leading chevron column that toggles the template as a full-width row beneath its row — the nested-child-table master/detail pattern; expansion state is keyed by `RowKey` identity (survives paging/sorting, forgotten when a row leaves the data).
  - `Column.TitleContent` renders templated header content in place of the plain `Title` (works in sortable headers too), enabling headers like "ESD ⓘ" composed with `LabelTooltip` — whose `Attributes` parameter is now optional, so it works standalone outside the Edit* form controls.
- `Tabs`/`Tab` — underline tab strip with an optional bordered per-tab count chip (`Count`, the "12 Overdue" pattern). Controlled via `@bind-ActiveKey` (`string?`); a `Tab` with `ChildContent` shows the active pane below the strip (proper `tablist`/`tab`/`tabpanel` wiring), while content-less tabs act as a bare filter strip.
  - ARIA tabs keyboard pattern with automatic activation: Arrow keys select the neighboring enabled tab (skipping disabled, wrapping) and move focus with a roving tabindex; Home/End jump to the ends.
  - Conditionally rendered tabs keep their declared position (the Table-column collect/promote mechanism). Active chip border derives from the primary color (`--wss-tabs-count-active-border` override knob).
- `SearchInput` — the labeled search field from the same spec: optional leading addon chip (`AddonLabel`/`AddonContent`), a per-keystroke `@bind-Value` input, and an icon-only search button; `OnSearch` fires with the current text on Enter and on the button.
  - Pill-rounded ends by default via `--wss-search-radius` (override to square). Not a form control — for validated form text use `EditString`.
- `DatePicker` — the single-date sibling of `DateRangePicker` (per the Clark Connect Vendor PO Management Figma spec): a text field with a calendar suffix opening a one-month calendar whose header is month/year quick-select dropdowns.
  - Bind with `@bind-Value` (`DateTime?`, date-only); picking a day (or typing a date and pressing Enter) commits and closes; Escape and outside clicks close; `Min`/`Max` disable out-of-range days; `Format` drives display/parsing (default `MM/dd/yyyy`); `Placeholder` defaults to "Select date".
  - Shares the `wss-picker-*` calendar internals and `wss-overlay.js` lifecycle (viewport flip/clamp, Enter-submit suppression, focus-out close — all degrade gracefully without JS). Its card carries a hairline border + the new `--wss-picker-radius-lg` (8px) radius, and the focused field shows the spec's primary focus ring. See [UI Kit](#ui-kit-non-form-controls).
- `Select` pill variant + `Prefix` slot — `Variant="SelectVariant.Pill"` restyles the trigger as a fully-rounded outlined filter button that hugs its content ("All shipments ⌄"), and the new `Prefix` `RenderFragment` renders leading content (typically a decorative icon) inside the trigger in any mode/variant.
  - The pill dropdown gains softer corners, content-driven width, roomier rows, and conveys selection by the bold/tinted row alone (checkmark suppressed); the trigger label/border/chevron/focus ring all derive from one override knob, `--wss-select-pill-color` (plus `--wss-select-pill-border`/`--wss-select-pill-bg`).
  - `EditSelectSearch` forwards `Variant` + `Prefix`; `EditMultiSelect` forwards `Prefix`. Internal DOM note: the selector's value/search stack is now wrapped in a `wss-select-selection-wrap` span (so a prefix can sit beside it) — geometry and behavior are unchanged, but CSS/tests targeting direct-child structure inside `.wss-select-selector` may need the extra level. See [Pill filter variant](#pill-filter-variant-select--editselectsearch).

### 10.6.2

**New feature**
- `DateRangePicker` — an AntDesign-style date-range picker: a composite start → end field that opens a dropdown with an optional preset sidebar and a dual-month calendar whose headers are native month/year quick-select dropdowns.
  - Bind with `@bind-Start`/`@bind-End` (`DateTime?`, date-only); picking the second day of a range (or a preset) commits and closes, a backwards pair swaps, and typed input parses by `Format` then culture, committing on Enter/blur.
  - `Presets` resolve their range at click time so relative shortcuts (e.g. "This Week") never go stale in a long-lived page. `Min`/`Max` disable out-of-range days and clamp presets; `FirstDayOfWeek` defaults to the current culture.
  - Not a form control — no `InputBase`/validation wiring. JS interop (viewport flip/clamp placement, Enter-submit suppression, focus-out close) degrades gracefully: without JS the dropdown opens below the field at the CSS default placement and stays fully clickable. New `--wss-picker-*` tokens carry its radii and split-border color. See [UI Kit](#ui-kit-non-form-controls).
- `UseStyledCheckbox` app/MFE-wide switch (shipped in this release but missed in the original changelog) — `FormOptions.UseStyledCheckbox` (`bool?`) and the render-tree-scoped `FormDefaults.UseStyledCheckbox` (`bool?`) resolve the same way as `IsRequiredStarHidden`/`ShowFieldNameInValidation`: instance → nearest enclosing `FormDefaults` → the process-wide `FormOptions.DefaultUseStyledCheckbox` static (default `false`).
  - `EditBool.UseStyledCheckbox` (shipped 10.6.0) changed from `bool` to `bool?` so it participates in this chain instead of being per-control only — existing `UseStyledCheckbox="true"`/`"false"` markup is unaffected, only an unset control now inherits the app-wide default instead of always rendering the native checkbox.
  - Two more controls gained the same opt-in: `EditCheckedStringList.UseStyledCheckbox`/`EditCheckedEnumList.UseStyledCheckbox` (`bool?`) apply the custom-drawn box to every option's checkbox, and the UI-kit `Table.UseStyledCheckbox` (`bool?`) applies it to the header/row selection checkboxes, including the indeterminate "mixed" glyph — `Table` has no `FormOptions` of its own, so it resolves through a cascaded `FormDefaults` then the static only. See [`FormDefaults`](#formdefaults) and [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).
- Styled checkbox visual restyle (also shipped in this release): the checked glyph is now the exact AntD check vector via a themeable CSS mask (was a generic rotated-border "L"), the unchecked border fallback moved from `#ccc` to `#d9d9d9` (AntD `colorBorder`), the `Table` variant's box corner radius moved from 2px to 4px to match `EditBool`'s, and the indeterminate "mixed" state is now an unfilled box with a centered primary-colored square (was a filled box with a white dash).
  - Also fixing a CSS comment bug (`/* ... edit-*/ ...`) that had been closing the `Table` box-wrapper rule early and letting the box escape its cell. The label row for `EditBool` and each `EditChecked*` option is now a flex row (`align-items: center`, 8px gap) instead of relying on inline whitespace.
  - These restyles apply automatically to every consumer already using `UseStyledCheckbox="true"` since 10.6.0 — there is no separate opt-in for the new look.

### 10.6.0

**New feature**
- `EditBool.UseStyledCheckbox` (default `false`) — opt-in custom-drawn checkbox.
  - No current browser (Chromium or Safari/WebKit) honors `border-radius` on a native `<input type="checkbox">` once `accent-color` is set, so there was previously no way to get a shaped checkbox out of `EditBool`.
  - When enabled, the real `<input>` stays in the DOM (focusable, keyboard-operable, full native semantics) but is visually hidden; a sibling element draws the box, checked fill, checkmark, and focus ring via the plain adjacent-sibling (`+`) CSS selector (not `:has()`, so it still works on older Safari). Existing checkboxes are pixel-identical — nothing changes unless you opt in. See [Custom-Styled Checkbox](#custom-styled-checkbox-border-radius).

**Bug fixes**
- `width: 100%` (or any percentage width) on the editor element of `EditString`/`EditNumber`/`EditDate`/`EditTextArea` now works.
  - Previously the `.edit-input-with-icon` wrapper shrink-wrapped to the editor's intrinsic size, which made a percentage width on the editor circular per the CSS sizing spec — it silently resolved to `auto` and the input stayed at its default size.
  - The wrapper is now a flex row that stretches to the control column (so percentages resolve against it), and the red-X invalid icon overlays the editor's trailing edge via a negative flex-item margin instead of absolute positioning — still `dir="rtl"`-correct and still immune to being wrapped onto its own line under a width squeeze.
- `EditFile`: bare `AllowedExtensions` entries without a leading dot (`"pdf"`) are now normalized instead of silently rejecting every file (and emitting an invalid `accept` attribute); the label's `for` no longer dangles at a missing input once the `MaxFiles` cap unmounts the drop zone; the upload icon now turns red for `EditContext` validation failures (not just client-side rejections); the read-only file list is programmatically associated with the field label.
  - Re-selecting a file that's already added (same name, size, and last-modified) is now skipped and reported — via the new `DuplicateFileMessageFormat` parameter — instead of occupying a second `MaxFiles`/`MaxTotalBytes` slot for the same logical file.
- List-bound controls (`EditMultiSelect`, `EditFile`, `EditCheckedStringList`, `EditCheckedEnumList`): a `class` attribute is now captured and merged into the rendered field instead of throwing at render time as an unmatched parameter — onto the select engine (`EditMultiSelect`, matching `EditSelectSearch`), the drop zone and read-only file list (`EditFile`), and every checkbox (`EditChecked*`).
  - These controls also now emit the same `EditContext` field-state classes as the scalar controls (`modified`/`valid`/`invalid` by default, honoring a custom `FieldCssClassProvider`) instead of only `invalid`. `EditRadio` now applies the consumer's `class` to its group fieldset in edit mode (previously it appeared only in the read-only view).
- `EditSelectSearch` / `EditMultiSelect` / `Select`: a disabled multi-select no longer renders focusable tag-remove buttons that silently no-op; Space now opens a closed non-searchable select (ARIA combobox pattern) — searchable inputs keep Space for typing.
- `EditDisplay`: the cascaded `FormOptions` was declared but ignored — form-wide `IsLabelHidden` now applies, and the new `IsLabelHidden` / `IdPrefix` parameters plus `FormGroupOptions.Name` id composition bring it in line with the bound controls (two `EditDisplay`s with the same label in different form groups no longer collide on id).
- Styled checkbox (`UseStyledCheckbox`): the box background is now `var(--color-bg, #fff)` instead of hardcoded white, so dark-theme consumers have an override hook. Default rendering unchanged.
- With `--color-primary` unset, the checked styled-checkbox fill and the `EditFile` drop-zone hover border fell back to a stray teal (`#277c6c`) while the focus rings fell back to blue (`#0066cc`) — two different colors for one interactive role.
  - All three now share a single `--edit-color-primary` token (blue fallback). Note the token is resolved at `:root`, like every other bridging token in both stylesheets — set `--color-primary` at `:root` for it to be picked up (a value scoped to a nested container is not seen, which previously happened to work for these two rules only).
- `Table`: the header checkbox's mixed (indeterminate) state is re-applied after `Selectable` is toggled off and back on while a partial selection exists — the recreated checkbox used to come back plain-unchecked.
- `Modal` / `Drawer`: Escape-to-close no longer goes dead when focus is silently dropped to `<body>` — e.g. the focused default OK button becoming disabled via `ConfirmLoading`, or a conditionally-rendered focused element unmounting. The focus trap now pulls focus back into the panel and re-targets the Escape at it.
- `JsInteropEc.FocusById` now honors its documented best-effort contract (a no-op when JS is unavailable) instead of throwing from a prerender `IJSRuntime`.
- **Theming: scoped token overrides now cascade into derived states.**
  - `--wss-color-primary-hover`, `--wss-primary-shadow`, `--wss-error-shadow`, and `--edit-focus-ring` used to be derived from their base token at `:root`, so overriding `--wss-color-primary`/`--edit-color-primary`/`--wss-color-error` on a nested container (a theme class, an MFE root) changed the base color but left hover borders, focus shadows, and focus rings at the default blue/red.
  - These are now derived at each usage site — a scoped base-token override re-themes the derived states too. All four remain overridable as before (a directly-set value wins over the derivation), the generic `--color-primary-hover` bridge is preserved (and now also works scoped, since it too is consulted at the element); computed defaults are unchanged. `--wss-color-primary-active` (never consumed by any rule) was removed.
- **UI-kit components accept `class`/`style`/arbitrary attributes.**
  - `Alert`, `Skeleton`, `Pagination`, `Modal`, `Drawer`, `Popover`, `Popconfirm`, `Table`, and `EditDisplay` previously threw `InvalidOperationException` on any unmatched attribute. They now capture unmatched attributes onto their root element (`Modal`/`Drawer`: the dialog panel; `Popover`/`Popconfirm`: the trigger wrapper): `class` and `style` merge with the component's own, everything else (`data-*`, `id`, ...) is splatted verbatim.
  - Caveat: parameter matching is case-insensitive, so an attribute sharing a parameter's name binds to the parameter instead — e.g. `title="..."` on `Modal`/`Drawer`/`Popover`/`Popconfirm` sets their `Title`, on `Skeleton` it's a build error (`Skeleton.Title` is a `bool`), and `class` on `EditDisplay` sets its `Class` (same knob).

### 10.5.1

**Bug fixes**
- `EditControlListBase<TItem>.ValueExpression` is now `[EditorRequired]` — a missing/incomplete `@bind-Value` (e.g. one-way `Value="..."` with no binding) is now a build-time `RZ2012` diagnostic instead of only the runtime `InvalidOperationException` each list-bound control's `OnInitialized` already threw.
- Fixed `.edit-icon-invalid` (the validation-error icon overlaid on `EditString`/`EditNumber`/`EditDate`/`EditTextArea`) wrapping onto its own line under a width squeeze. It's now absolutely positioned (`inset-inline-end`, so it still overlays the correct edge under `dir="rtl"`) instead of relying on a negative margin to pull it over the input.

**Demo**
- Added a "Comparison" view to the demo app that renders the same field via WssBlazorControls, hand-rolled Blazor, and React + Ant Design (with and without full accessibility parity) side by side, with reasoned notes on the accessibility and AI-authoring trade-offs of each.

### 10.5.0

**`Field` is gone — `@bind-Value` alone is now enough on every control**

- Every `Edit*` control previously required both `@bind-Value="model.Property"` **and** `Field="@(() => model.Property)"` — the second was pure duplication.
  - Razor's `@bind-Value` directive already populates a `ValueExpression` (the same mechanism Microsoft's own `InputText`/`InputNumber` rely on for validation and labeling without a second parameter); the library just wasn't using it. All 17 controls now resolve their accessor from `ValueExpression` instead.
- This covers the scalar controls (`EditString`, `EditNumber`, `EditDate`, `EditBool`, `EditBoolNullRadio`, `EditSelectEnum`, `EditSelectString`, `EditSelect`, `EditSelectSearch`, `EditRadio`, `EditRadioEnum`, `EditRadioString`, `EditTextArea`) and the list-bound controls (`EditCheckedStringList`, `EditCheckedEnumList`, `EditFile`, `EditMultiSelect`).
  - The list-bound controls aren't `InputBase`-derived, so `EditControlListBase<TItem>` gained its own `ValueExpression` parameter — the compiler synthesizes it from `@bind-Value` for any component with the `Value`/`ValueChanged`/`ValueExpression` parameter shape, not just `InputBase` subclasses.
- **Migration:** delete every `Field="@(() => model.Property)"` attribute — `@bind-Value="model.Property"` alone is sufficient.
  - `Field` still exists on every control as an inert, `[Obsolete(error: true)]`-decorated parameter purely so a leftover `Field=` attribute is a **build error** (`CS0619: 'EditXxx.Field' is obsolete: ...`) instead of a silent runtime failure — Blazor otherwise validates unmatched component parameters at `SetParametersAsync` time, not compile time, so a stale attribute would build cleanly and only throw the first time that component renders.
  - The error message tells you exactly what to remove; this stub carries no other behavior and is planned for physical removal in a future major version.

**Drops net8.0/net9.0 — the package now targets net10.0 only**

- `WssBlazorControls` and `WssBlazorControls.Demo` are single-targeted at `net10.0`; both previously multi-targeted `net8.0;net9.0;net10.0`. **If your app targets net8.0 or net9.0, this version will not install** — stay on `10.4.x` until you upgrade the app to net10.0.
- CI now installs and runs against a single .NET SDK instead of three; the bUnit suite runs once instead of once per TFM.
- No API or behavioral changes for net10.0 consumers — this is purely a supported-platform reduction.

### 10.4.0

A library-wide hardening release: six adversarial review rounds (documented across this release's commit history) spanning correctness, accessibility, performance (measured), globalization/RTL, plus trimming/AOT support, touch support, and validation-stack (FluentValidation) support.

**Correctness**
- `EditRadio.IsDisabled` actually disables its `InputRadio` children now (a nested `fieldset[disabled]` — `InputRadioGroup` renders no element, so the old attribute vanished).
  - All three radio controls forward `ValueExpression` to their inner group so it notifies/styles the real model field. `EditRadio.Field` is now `required` like every sibling.
- A null bound `List<T>` no longer crashes `EditFile`'s render or the first checkbox toggle in the checked lists — null is treated as empty and the list is created on first add.
- `EditFile` now buffers each selected file's bytes into memory at pick time instead of holding the framework's `IBrowserFile`.
  - Previously, choosing files in more than one batch (or hitting `MaxFiles`, which unmounts the `<InputFile>`) left every earlier file throwing on `OpenReadStream()` — Blazor wipes the browser file map on each change event. Buffered files stay readable for the life of the list, so multi-batch accumulation and the per-file remove buttons behave as the UI implies.
  - A bare `file.OpenReadStream()` (no size argument) now works regardless of file size — the bytes are already in memory, bounded by `MaxFileSizeBytes`.
  - Trade-off: selected files occupy memory until cleared, and on Blazor Server the bytes cross the circuit at selection; the aggregate is bounded by the new `MaxTotalBytes` (default **100 MB** across all selected files, `0` = unlimited), with `MaxFileSizeBytes`/`MaxFiles` bounding per-file size and count.
- `EditNumber` binds on `change` instead of `oninput` (browsers report `type=number` as `""` mid-typing, flashing "must be a number" on partial input like `-` or `3.`), and formats the unsigned/byte types invariantly to match the parse side.
- `EditRadioString`: an options list legitimately containing `"Other"` no longer collides with the built-in other-option sentinel (which silently replaced the model value with the empty other-text). The internal sentinel is now also uniquified against the options list, so no option string whatsoever can collide with it.
- `EditRadioString`'s "Other" free-text box now honors `IsDisabled` — with the Other option selected it used to stay editable (writing to the model per keystroke) while every radio was disabled. Matches `EditRadioEnum`.
- The list-bound controls re-derive their `FieldIdentifier` when the model/`EditContext` is swapped, so validation targets the new model instead of dead state; they also work outside an `EditForm` (no more `FieldValidationDisplay` NRE).
- The scalar controls and `EditRadio` also render standalone (no surrounding `EditForm`) again — `IsInvalid` now guards a null `EditContext` instead of dereferencing it, matching the list base.
- `EditSelectString` renders a leading empty option (`NullOptionText`) — a null value used to display the first option as selected while the model stayed null.
  - Selecting that blank now clears the model to `null`/`default` (a `string?` could previously never return from `""` to null; a non-string `TValue` like `EditSelectString<int?>` reported "not valid" instead of clearing).
  - The blank is now opt-out — set `NullOptionText="@null"` to drop it (e.g. a required field) — and is auto-suppressed for a non-nullable value type (`EditSelectString<int>`), where a blank would only map to a spurious `default`.
- Select parsing **and formatting** (`EditSelect`/`EditSelectString`) are invariant-culture, matching `EditNumber` — `"1.5"` no longer parses as `15` under de-DE, and a bound `double` `1.5` now renders as `value="1.5"` (was `"1,5"`, which matched no `<option>` and left the select visually unselected).
- `Table`: fully-equal duplicate rows no longer crash the render (de-duplicated row keys); new `RowKey` parameter (e.g. `x => x.Id`) gives rows identity, and selection is key-based.
  - Descending sort survives `int.MinValue` from subtraction comparators; a column whose parameters never change (title-only spacer) no longer silently vanishes.
- Toast auto-dismiss durations are capped below `Task.Delay`'s ~24.8-day limit instead of throwing into a fire-and-forget task.
- Performance/leak hardening: `FieldValidationDisplay` memoizes its per-field value-type reflection (a large form re-reflected every field on every keystroke).
  - The list-bound controls unregister their old field on a model/`EditContext` swap (and on dispose) so the validation summary's field list can't accumulate dead entries; the `EnumHelpers` id cache stops calling the lock-acquiring `Count` once saturated instead of on every subsequent call.

**Overlays**
- One Escape no longer closes the whole overlay stack: panels stop keydown propagation, and the Select input does so only while its dropdown is open (so Escape still reaches an enclosing Modal once the dropdown is closed).
- Overlays stack in **open order** via a JS z-index counter (Modal-vs-Drawer DOM-order ties and Popover-above-a-later-Modal are gone); an open Select sits above its own backdrop (clicking your own search input/tags/clear no longer closes the dropdown); toasts are the always-on-top layer.
- Modal/Drawer: neither a close→reopen race **nor disposal while the open animation is in flight** can leak the body-scroll lock/document listeners now (a `_disposed` guard releases the late focus-trap handle instead of orphaning it).
  - The Modal only dismisses when a mask click both **starts and ends** on the mask, so a drag crossing the mask/panel boundary in *either* direction keeps it open, and a press released outside the window can't leave a stale flag that closes a later gesture.
  - The focus trap is document-level and survives focus escaping the panel (nested overlays hand it to the innermost dialog); title-less non-closable dialogs render no empty header and fall back to `aria-label="Dialog"`/`"Drawer"`.
- `Alert`'s close button hides the alert itself (`OnClose` is a notification, not a requirement).

**Select engine**
- Enter picks the highlighted option **without** triggering the enclosing form's implicit submission; arrow keys no longer jump the caret; Enter on a closed combobox opens it.
  - Opening highlights the current selection (scrolled into view) and skips disabled options; Tab-away closes the dropdown (its invisible backdrop used to swallow the next click).
- `Options`/`Values` are now explicitly immutable parameters (reference-guarded rebuilds — a parent re-render used to re-copy/re-filter the whole option set per keystroke). Reassign a new instance to refresh.
- Tags mode prunes a user-created tag from the options once deselected; `EditMultiSelect` throws a clear exception on `Mode="Single"` (selections silently reverted — use `EditSelectSearch`).

**Accessibility**
- Hidden labels (`IsLabelHidden`) render a visually-hidden label/legend so controls keep an accessible name — including `EditBool`, whose edit branch renders its own label and had been shipping an unnamed checkbox in the hidden-label case.
  - Checked-list fieldsets expose `role=group` + `aria-required`/`-invalid`; each validation message renders in its own element (no more run-together text); dynamic `Label` changes propagate to `EditBool` and validation messages.
  - `label[for]` no longer references the non-labelable read-only div; `LabelTooltip` dismisses on Escape (WCAG 1.4.13) and now stops that Escape from also closing an enclosing Modal/overlay (one Escape, one layer).
  - Pagers get distinct landmark names when a Table renders two; the select-all checkbox announces its per-page scope.
- `IsInvalid` is read from `EditContext` messages instead of substring-matching `"invalid"` in `CssClass` (a consumer class like `invalid-style-fix` rendered a permanent red X). **`InvalidIcon` now takes `IsInvalid` (bool) instead of `CssClass`.**
- `[Display(Name = …)]` is honored for labels (after `[DisplayName]`/`[EnumDisplayName]`), keeping labels consistent with DataAnnotations' own messages — and now resolves through `GetName()`, so a localized `[Display(Name = …, ResourceType = …)]` yields the localized text instead of the raw resource key (both for control labels and enum display names).
- `EditFile`: removing a file with the keyboard keeps focus on the control (the file that shifted into the slot, else the new last file, else the drop zone) instead of dropping focus to `<body>`; a disabled drop zone no longer shows the drag-hover highlight for a drop it will refuse.
- `Popover`/`Popconfirm`: the consumer's own trigger element (typically a `<button>` in `ChildContent`) is the trigger now — the wrapper span no longer renders `role="button"`/`tabindex="0"` around it, which nested a button inside a button (two tab stops, invalid ARIA).
  - JS mirrors `aria-haspopup`/`aria-expanded` onto the child and restores focus to it on close; content with nothing focusable (plain text/icon) gets the wrapper promoted to the button role as before.
  - Without JS, a button child still opens/closes via its bubbled click — only the popup ARIA and the plain-content keyboard path need the runtime.

**Validation stacks (FluentValidation support)**
- New `FormOptions.RequiredResolver` (`Func<FieldIdentifier, bool>?`): a form-level source of required-ness for validation stacks that don't use `[Required]` (e.g. FluentValidation). Fields the resolver marks required get the star and `aria-required` exactly as if attributed. See the new **Validation stacks** section for the FluentValidation bridge snippet.
- `IsRequired` is now three-state (`bool?`) on all controls and `FormLabel`: unset defers to the attribute/resolver; `true` forces required (unchanged); **`false` now forces optional**.
  - Previously it was a no-op, so a `RequiredAttribute`-derived conditional (RequiredIf) whose condition was off showed a permanent star with no way to remove it. Existing markup (`IsRequired="true"` or a bound `bool`) compiles unchanged.
- The star and `aria-required` are now computed by one shared resolver (`EditControlInit.IsRequired`), so the two signals can never disagree; `FieldValidationDisplay` dropped an unused required-ness field, and `EditControlInit.Init` no longer returns a redundant `IsRequired` tuple member (its value was always recomputed and overwritten).

**API changes to note when upgrading**
- `InvalidIcon.CssClass` → `InvalidIcon.IsInvalid` (bool); `LabelTooltip.TooltipChanged` removed (never invoked); `ValidationView.Model` removed (never read); `EditRadio.Field` is now required.
  - `EditSelectString` gains a leading empty option (opt out with `NullOptionText="@null"`; its type is now `string?`) and selecting the blank now writes `null`/`default` instead of `""`.
  - `EditNumber` commits on change (not per keystroke); `Alert` self-dismisses on close; `Select`/`Table` collection parameters are immutable-by-reference.
- New: `Table.RowKey`, `Pagination.AriaLabel`, `EditSelect.ReadOnlyText`, `EditSelectString.NullOptionText`, `FormLabel.IsForLabelable`.
- `Popover`/`Popconfirm` trigger contract: pass a focusable element (typically a `<button>`) as the trigger content — it is the single tab stop and carries the popup ARIA. Plain-text trigger content still works but is keyboard-accessible only when JS is available.
  - The trigger child is re-resolved on every sync, so conditionally-swapped trigger content (`@if (busy) { spinner } else { button }`) keeps its ARIA and close-focus; focusable non-button children (a `[tabindex]` span, an anchor) get Enter/Space activation, while `input`/`select`/`textarea` children keep their editing semantics (a `<button>` remains the recommended trigger).
- New `FormDefaults` component: render-tree-scoped defaults for `IsRequiredStarHidden`/`ShowFieldNameInValidation` — wrap an app or MFE root to configure its forms without touching the process-wide `FormOptions` statics (which are shared across circuits on Blazor Server).
  - Resolution: `FormOptions` instance value → cascaded `FormDefaults` → static default. Nested instances chain per property (an unset inner setting falls through to the enclosing `FormDefaults` before the static), so host-page defaults and MFE-root overrides compose. Non-breaking; the statics remain as the final fallback.

**Packaging & repo**
- The packages now ship XML docs (IntelliSense), SourceLink + `.snupkg` symbols, deterministic CI builds, package validation, and an SPDX `MIT` license expression; warnings are errors.
  - GitHub Actions CI builds the solution, runs the bUnit suite across net8/net9/net10, packs both packages, and runs the Playwright E2E suite. The E2E project is now part of `FormTesting.sln`. The Quick Start documents the required `edit-controls.js` script tag.

**Trimming / WASM AOT**
- The package is now trim- and AOT-compatible (`IsTrimmable`/`IsAotCompatible` + warning-clean trim/AOT/single-file analyzers, enforced as errors). A default Blazor WASM publish trims the library. See the new **Trimming and AOT** section above for what survives and the consumer caveats.
- Reflection sites were made trim-safe rather than suppressed wholesale: enum option builders use `Enum.GetValuesAsUnderlyingType` (AOT-safe, no `RequiresDynamicCode`); `PropertyColumn`'s comparability probe drops `MakeGenericType` (the one lost corner — `Nullable<T>` whose `T` implements *only* `IComparable<T>` — degrades to non-sortable, `SortBy` unaffected).
  - The generic value-bearing controls annotate `T` with `[DynamicallyAccessedMembers(All)]` exactly like the framework's `InputNumber`/`InputSelect`; the two by-name lookups (validation value-type, enum field) carry justified suppressions with graceful fallbacks.
- Verified end-to-end: the full Playwright e2e suite passes against a `TrimMode=full` publish of the demo host (labels, required stars, length/range message rewrites, `[EnumDisplayName]` options, tooltips, visual baselines).

**Round-3 review fixes** *(post-hardening evaluation)*
- `Popover`/`Popconfirm` re-resolve their trigger child on every ARIA sync, so conditionally-swapped trigger content (`@if (busy) { spinner } else { button }`) no longer strands `aria-haspopup`/`aria-expanded` on a detached element or drops close-focus to `<body>`, and a wrapper promoted around plain content is demoted again when a real button appears (no more button-in-button after a swap).
  - Focusable non-button trigger children (`[tabindex]` spans, anchors) gained Enter/Space activation; a `Disabled` Popconfirm marks an interactive child `aria-disabled`.
  - The per-render JS interop call is now skipped unless `(open, disabled)` changed — a Popconfirm-per-row Table no longer pays one SignalR round trip per row per re-render on Blazor Server (a `focusin` listener repairs ARIA for children swapped while idle).
- `EditFile`: new `MaxTotalBytes` parameter (default **100 MB**, `0` = unlimited) bounds the aggregate buffered footprint across all selected files — buffering at pick time otherwise let a single large multi-file drop allocate unbounded server memory under the default `MaxFiles = 0`.
- Date-typed selects round-trip: `EditSelect<DateOnly>`/`<DateTime>`/`<DateTimeOffset>`/`<TimeOnly>` now format to the ISO forms option values are authored in, so picking an option no longer immediately loses the visual selection while the model holds the value.
  - Author your option values in the matching canonical form — `DateOnly`: `2026-06-15` · `DateTime`: `2026-06-15T14:30:45` · `DateTimeOffset`: `2026-06-15T14:30:45-05:00` (UTC is `+00:00`, not `Z`) · `TimeOnly`: `14:30:45`. Shorter authored forms (`2026-06-15` for a `DateTime`, `14:30` for a `TimeOnly`) still *parse* on pick, but the formatted value won't visually re-match them.
- `EditSelectString` with a suppressed blank option (non-nullable value types, or `NullOptionText="@null"`) renders a hidden placeholder when the current value matches no option — an untouched default (e.g. `0`) displays blank instead of silently showing the first option while the model holds something else.
- The open `Select`'s stacking z-index is mirrored into its C#-owned `style`, so a re-render that changes `Width` mid-open no longer clobbers it and drops the selector below its own backdrop (which made clicks on the select's own input close the dropdown).
- Two controls bound to the same property now share their validation-summary registration safely: disposing one (e.g. closing an edit modal that duplicates a page field) keeps the surviving control's messages — registrations are owner-tracked and dropped only by the last registrant.
- Nested `FormDefaults` chain per property instead of the inner instance shadowing the outer entirely (see the `FormDefaults` note above).
- `Select`, `Modal`, `Drawer`, `Popover`, and `Popconfirm` no longer strand a JS module reference when disposed while their module import is in flight (the same race `Table` was already guarded against).

**Round-4 review fixes** *(post-round-3 evaluation)*
- `EditSelect<DateTimeOffset>` now formats whole-second values without the `.0000000` fraction (`2026-06-15T14:30:45-05:00`), so authored option values actually match and the visual selection survives a pick; sub-second values keep the full round-trip form. The canonical authored forms per date type are documented in the round-3 entry above.
- `Popover`/`Popconfirm` trigger ARIA: a consumer-owned `aria-disabled` on the trigger child is no longer removed when the component's `Disabled` round-trips.
  - When the resolved trigger child changes identity while the old element stays in the DOM, the popup ARIA is stripped off the old element instead of two elements announcing the popup.
- `EditCheckedStringList`/`EditCheckedEnumList` fieldsets no longer emit `aria-required`/`aria-invalid`/`aria-errormessage` — ARIA 1.2 doesn't support them on `role="group"` (assistive tech ignored them; checkers flag them).
  - Required state remains on the legend star and the validation message, invalid state on each checkbox's `aria-invalid`. The radio fieldsets (`role="radiogroup"`, where these attributes are valid) are unchanged.

**Round-5 fixes** *(trim verification, globalization/RTL sweep, measured performance pass)*
- **RTL support:** the direction-sensitive Select geometry (arrow/clear anchoring, search inset, tag/placeholder spacing) and the form controls' trailing invalid-icon/required-star spacing now use CSS logical properties.
  - Under `dir="rtl"` tags no longer render beneath the opaque clear button (where a tap cleared the entire selection) and typed search text no longer starts under the arrow. Rendering under LTR is byte-identical.
  - Notification position, `DrawerPlacement` left/right, and Table alignment deliberately keep physical semantics.
- **Localization:** new label parameters with unchanged English defaults — `Pagination` `PreviousPageLabel`/`NextPageLabel`/`PageLabelFormat`; `Select`/`EditSelectSearch`/`EditMultiSelect` `RemoveItemLabelFormat`/`ClearSelectionLabel`/`ClearSelectionsLabel`/`ListboxLabel` — so localized apps can localize what screen readers hear.
  - `EditFile`'s five upload-error messages are likewise localizable via `*MessageFormat` parameters (`UnsupportedFormat`, `FileTooLarge`, `FileReadFailed`, `MaxFiles`, `TotalSize`); the pluralizing formats receive a pre-pluralized English unit argument that localized formats can ignore.
- **Culture correctness:** the `[Range]` one-sided message rewrite ("Cannot exceed 100") now works after a runtime culture switch and in mixed-culture Blazor Server processes — the type-min/max sentinels are resolved per current culture instead of being frozen at first touch.
- **Performance:** `Table` no longer rebuilds its row keys and rescans selection state on every parent re-render (the cost was O(rows) with boxing, per keystroke in any sibling input for unpaged tables).
  - `FormLabel`/`FieldValidationDisplay` skip label/attribute re-derivation — and stop re-invoking `FormOptions.RequiredResolver` — unless their inputs actually changed, honoring the resolver's documented "not on every keystroke" contract; `EditMultiSelect`'s read-only label join is O(selected) via a value→label lookup.
  - Measured reality check: for *very* large unpaged tables the remaining cost is Blazor re-rendering the row fragment itself — prefer `PageSize` or the server-side paging composition at that scale.
- Verified this round: the full Playwright suite passes against a `TrimMode=full` publish; Select's dropdown virtualization confirmed (20 DOM rows at 1,000 options).

**Round-6 fixes** *(pre-release regression hunt on the round-4/5 fixes)*
- The required star and `aria-required` now share one computation site: each control resolves its required-ness once (`IsRequired` parameter → `[Required]` → `FormOptions.RequiredResolver`) and passes the resolved value to its label.
  - So a conditional resolver that reads model state moves both signals together on re-render (the round-5 label caching had let `aria-required` update while the star stayed frozen).
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

Entries for releases before 10.3.0 have been removed to keep this file within NuGet's readme size; see the repository's git history for the older changelog.
