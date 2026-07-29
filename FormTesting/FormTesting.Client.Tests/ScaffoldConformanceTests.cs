using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Conformance safety net for the shared Edit* scaffold: renders every public form control through
/// one parameterized fixture and asserts the invariants every control's markup is supposed to agree
/// on (root wrapper, label/legend id convention, required-star ⟺ aria-required, aria-describedby
/// resolving to a real element, aria-invalid after a failed validation). The per-control scaffold
/// markup is intentionally repeated across controls rather than shared via a base-class render — this
/// suite exists so a control silently missing a piece of it (e.g. a required control with no star) is
/// a red test instead of a silent accessibility regression.
/// </summary>
/// <remarks>
/// <para>
/// Coverage: EditString, EditTextArea, EditNumber&lt;int?&gt;, EditBool, EditBoolNullRadio,
/// EditDate&lt;DateTime?&gt;, EditDateNative&lt;DateTime?&gt;, EditFile, EditRadio&lt;string&gt;,
/// EditRadioEnum&lt;Priority&gt;, EditRadioString, EditCheckedEnumList&lt;Priority&gt;,
/// EditCheckedStringList, EditSelect&lt;string&gt;, EditSelectEnum&lt;Priority?&gt;,
/// EditSelectString&lt;string&gt;, EditSelectSearch&lt;Priority?&gt;, EditMultiSelect&lt;Priority&gt;
/// go through the generic <see cref="Scalar_and_group_controls_satisfy_the_shared_scaffold_invariants"/>
/// theory. <see cref="EditDateRange"/> gets its own <see cref="EditDateRange_satisfies_the_shared_scaffold_invariants"/>
/// fact — it binds two independent fields (Start/End) behind one shared label, which doesn't fit the
/// generic single-field shape. <see cref="EditDisplay"/> is excluded entirely: it has no bound field
/// (no <c>Attributes</c>/<c>FieldIdentifier</c>), no <c>FieldValidationDisplay</c>, and treats
/// <c>IsRequired</c> as a bare bool it renders through unconditionally rather than resolving through
/// <c>EditControlInit.IsRequired</c>/<c>[Required]</c> — there is no aria-required/validation wiring
/// for these invariants to apply to.
/// </para>
/// <para>
/// Each control is exercised with two model properties: a "Required" one carrying <c>[Required]</c>
/// (so the star/aria-required invariant has something to assert true) plus the custom
/// <see cref="AlwaysInvalidAttribute"/> (so ONE submit deterministically fails validation regardless of
/// how the control's bound type interacts with <c>[Required]</c> itself — an empty-but-non-null list,
/// a false bool, and a set DateTime all satisfy <see cref="RequiredAttribute"/>, so only a
/// genuinely-always-failing rule proves the aria-invalid/error-message wiring end to end); and an
/// "Optional" one with no attributes at all, so the star/aria-required/aria-invalid absence path gets
/// exercised too. <see cref="EditControlInit.IsRequired"/> only checks for the attribute's presence
/// (not whether it currently fails), so <c>[Required]</c> alongside a non-null/non-empty default value
/// drives the star without ever tripping <c>RequiredAttribute.IsValid</c> itself — only
/// <see cref="AlwaysInvalidAttribute"/> fails, giving exactly one validation message per field.
/// </para>
/// </remarks>
public class ScaffoldConformanceTests : BunitContext
{
    // Several controls under test (DatePicker, the Select engine) import a JS module during their own
    // first render (wrapped in try/catch and gracefully degrading -- see Select.razor.cs's
    // GetJsModuleAsync remarks) — Loose tolerates that unconfigured invocation the same way the other
    // mixed-control test files in this project do (DatePickerTests, EditDateRangeModelAttributeTests).
    public ScaffoldConformanceTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // ----- shared validation attribute -----------------------------------------------------------

    /// <summary>
    /// Fails for literally any value on any property type. See the class remarks for why this (rather
    /// than relying on <c>[Required]</c> alone) is what actually drives the aria-invalid/error-message
    /// assertions below.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    sealed class AlwaysInvalidAttribute : ValidationAttribute
    {
        public AlwaysInvalidAttribute() : base("Always invalid.") { }
        public override bool IsValid(object? value) => false;
    }

    // ----- shared model: one Required/Optional property pair per control under test ---------------

    sealed class ScaffoldModel
    {
        // EditString
        [Required, AlwaysInvalid] public string StringRequired { get; set; } = "value";
        public string StringOptional { get; set; } = "value";

        // EditTextArea
        [Required, AlwaysInvalid] public string TextAreaRequired { get; set; } = "value";
        public string TextAreaOptional { get; set; } = "value";

        // EditNumber<int?>
        [Required, AlwaysInvalid] public int? NumberRequired { get; set; } = 1;
        public int? NumberOptional { get; set; } = 1;

        // EditBool -- [Required]'s presence (not its IsValid result) is what drives the star/aria-required,
        // so it applies just as well to a bool as to any other type; see EditControlInit.IsRequired.
        [Required, AlwaysInvalid] public bool BoolRequired { get; set; } = true;
        public bool BoolOptional { get; set; } = true;

        // EditBoolNullRadio
        [Required, AlwaysInvalid] public bool? BoolRadioRequired { get; set; } = true;
        public bool? BoolRadioOptional { get; set; } = true;

        // EditDate<DateTime?>
        [Required, AlwaysInvalid] public DateTime? DateRequired { get; set; } = new(2024, 1, 1);
        public DateTime? DateOptional { get; set; } = new(2024, 1, 1);

        // EditDateNative<DateTime?>
        [Required, AlwaysInvalid] public DateTime? DateNativeRequired { get; set; } = new(2024, 1, 1);
        public DateTime? DateNativeOptional { get; set; } = new(2024, 1, 1);

        // EditFile
        [Required, AlwaysInvalid] public List<IBrowserFile> FileRequired { get; set; } = [];
        public List<IBrowserFile> FileOptional { get; set; } = [];

        // EditRadio<string>
        [Required, AlwaysInvalid] public string RadioRequired { get; set; } = "a";
        public string RadioOptional { get; set; } = "a";

        // EditRadioEnum<Priority>
        [Required, AlwaysInvalid] public Priority RadioEnumRequired { get; set; } = Priority.Low;
        public Priority RadioEnumOptional { get; set; } = Priority.Low;

        // EditRadioString
        [Required, AlwaysInvalid] public string RadioStringRequired { get; set; } = "a";
        public string RadioStringOptional { get; set; } = "a";

        // EditCheckedEnumList<Priority>
        [Required, AlwaysInvalid] public List<Priority> CheckedEnumRequired { get; set; } = [];
        public List<Priority> CheckedEnumOptional { get; set; } = [];

        // EditCheckedStringList
        [Required, AlwaysInvalid] public List<string> CheckedStringRequired { get; set; } = [];
        public List<string> CheckedStringOptional { get; set; } = [];

        // EditSelect<string>
        [Required, AlwaysInvalid] public string SelectRequired { get; set; } = "a";
        public string SelectOptional { get; set; } = "a";

        // EditSelectEnum<Priority?>
        [Required, AlwaysInvalid] public Priority? SelectEnumRequired { get; set; } = Priority.Low;
        public Priority? SelectEnumOptional { get; set; } = Priority.Low;

        // EditSelectString<string>
        [Required, AlwaysInvalid] public string SelectStringRequired { get; set; } = "a";
        public string SelectStringOptional { get; set; } = "a";

        // EditSelectSearch<Priority?>
        [Required, AlwaysInvalid] public Priority? SelectSearchRequired { get; set; } = Priority.Low;
        public Priority? SelectSearchOptional { get; set; } = Priority.Low;

        // EditMultiSelect<Priority>
        [Required, AlwaysInvalid] public List<Priority> MultiSelectRequired { get; set; } = [];
        public List<Priority> MultiSelectOptional { get; set; } = [];
    }

    // ----- rendering plumbing -----------------------------------------------------------------------

    // EditForm + DataAnnotationsValidator (so a submit actually runs validation) + a cascaded
    // FormOptions (the task's suggested shape) around whatever control the case builds.
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<CascadingValue<FormOptions>>(0);
        builder.AddAttribute(1, "Value", new FormOptions());
        builder.AddAttribute(2, "ChildContent", (RenderFragment)(cascaded =>
        {
            cascaded.OpenComponent<EditForm>(0);
            cascaded.AddAttribute(1, "Model", model);
            cascaded.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                inner(content);
            }));
            cascaded.CloseComponent();
        }));
        builder.CloseComponent();
    };

    /// <summary>
    /// Everything one control needs to run through the shared assertions. Built fresh per test (via
    /// <see cref="BuildCase"/>) rather than handed to xUnit as theory data directly -- the record holds
    /// delegates (<see cref="Build"/>, the selector functions), and xUnit needs to be able to identify
    /// theory data across its discovery/execution boundary, which a Func-laden object isn't guaranteed
    /// to support cleanly. Dispatching on a plain string keeps the theory data trivially safe while
    /// still giving each case a readable name in the test explorer.
    /// </summary>
    sealed record ScaffoldCase
    {
        public required string Name { get; init; }

        /// <summary>Builds one control instance bound to the Required (true) or Optional (false)
        /// model property, returning the model it must be rendered inside an EditForm for.</summary>
        public required Func<bool, (object Model, RenderFragment Content)> Build { get; init; }

        public required Func<IRenderedComponent<ContainerFragment>, IElement> Wrapper { get; init; }

        /// <summary>The label/legend element expected to carry <c>id="lbl-{id}"</c> (when
        /// <see cref="HasLabelId"/>) and the <c>.edit-label-required-star</c> when required.</summary>
        public required Func<IRenderedComponent<ContainerFragment>, IElement> LabelOrLegend { get; init; }

        /// <summary>Escape hatch for a control whose label element legitimately carries no
        /// <c>lbl-{id}</c>. Currently unused -- every covered control routes through FormLabel --
        /// and kept so a future structural exception opts out explicitly instead of weakening the
        /// shared assertion.</summary>
        public bool HasLabelId { get; init; } = true;

        /// <summary>The element(s) that should carry aria-required/aria-invalid/aria-describedby: the
        /// input/select/textarea for scalar controls, the fieldset for radio-groups, and every checkbox
        /// for the checked-list controls (whose fieldset uses role="group", which ARIA 1.2 doesn't allow
        /// aria-required/aria-invalid on).</summary>
        public required Func<IRenderedComponent<ContainerFragment>, IReadOnlyList<IElement>> AriaCarriers { get; init; }

        /// <summary>False only for EditCheckedEnumList/EditCheckedStringList -- see AriaCarriers'
        /// remarks and CheckboxOptionList.razor's own comment on ARIA 1.2 role="group".</summary>
        public bool SupportsAriaRequired { get; init; } = true;

        /// <summary>False only for EditFile: its InputFile ties aria-invalid to an upload-time
        /// rejection (<c>_hasError</c>), not to EditContext validation -- documented directly in
        /// EditFile.razor's inline comment and proven by EditFileTests'
        /// Aria_errormessage_is_not_set_from_an_upload_rejection_alone test. aria-describedby and the
        /// error-msg-{id} message count are still asserted for this control; only the aria-invalid
        /// attribute itself is excluded.</summary>
        public bool SupportsAriaInvalid { get; init; } = true;
    }

    // ----- one factory per control ------------------------------------------------------------------

    static ScaffoldCase StringCase() => new()
    {
        Name = "EditString",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string>> field = required ? () => model.StringRequired : () => model.StringOptional;
            var value = required ? model.StringRequired : model.StringOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditString>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input.edit-string-input")]
    };

    static ScaffoldCase TextAreaCase() => new()
    {
        Name = "EditTextArea",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string>> field = required ? () => model.TextAreaRequired : () => model.TextAreaOptional;
            var value = required ? model.TextAreaRequired : model.TextAreaOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditTextArea>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("textarea")]
    };

    static ScaffoldCase NumberCase() => new()
    {
        Name = "EditNumber<int?>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<int?>> field = required ? () => model.NumberRequired : () => model.NumberOptional;
            var value = required ? model.NumberRequired : model.NumberOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditNumber<int?>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input[type=number]")]
    };

    static ScaffoldCase BoolCase() => new()
    {
        Name = "EditBool",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<bool>> field = required ? () => model.BoolRequired : () => model.BoolOptional;
            var value = required ? model.BoolRequired : model.BoolOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditBool>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        // EditBool's checkbox-mode label renders through FormLabel (via its NestedInput slot) since
        // the same change that restored the required star, so it carries lbl-{id} and the star like
        // every other control -- the full invariant applies.
        LabelOrLegend = cut => cut.Find("label.edit-checkbox-label"),
        AriaCarriers = cut => [cut.Find("input[type=checkbox]")]
    };

    static ScaffoldCase BoolNullRadioCase() => new()
    {
        Name = "EditBoolNullRadio",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<bool?>> field = required ? () => model.BoolRadioRequired : () => model.BoolRadioOptional;
            var value = required ? model.BoolRadioRequired : model.BoolRadioOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditBoolNullRadio>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => [cut.Find("fieldset.edit-radio-fieldset")]
    };

    static ScaffoldCase DateCase() => new()
    {
        Name = "EditDate<DateTime?>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<DateTime?>> field = required ? () => model.DateRequired : () => model.DateOptional;
            var value = required ? model.DateRequired : model.DateOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditDate<DateTime?>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input.wss-picker-input-date")]
    };

    static ScaffoldCase DateNativeCase() => new()
    {
        Name = "EditDateNative<DateTime?>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<DateTime?>> field = required ? () => model.DateNativeRequired : () => model.DateNativeOptional;
            var value = required ? model.DateNativeRequired : model.DateNativeOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditDateNative<DateTime?>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input[type=date]")]
    };

    static ScaffoldCase FileCase() => new()
    {
        Name = "EditFile",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<List<IBrowserFile>>> field = required ? () => model.FileRequired : () => model.FileOptional;
            var value = required ? model.FileRequired : model.FileOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditFile>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input[type=file]")],
        SupportsAriaInvalid = false
    };

    static ScaffoldCase RadioCase() => new()
    {
        Name = "EditRadio<string>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string>> field = required ? () => model.RadioRequired : () => model.RadioOptional;
            var value = required ? model.RadioRequired : model.RadioOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditRadio<string>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenComponent<InputRadio<string>>(0);
                    cb.AddAttribute(1, "Value", "a");
                    cb.CloseComponent();
                    cb.OpenComponent<InputRadio<string>>(2);
                    cb.AddAttribute(3, "Value", "b");
                    cb.CloseComponent();
                }));
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => [cut.Find("fieldset.edit-radio-fieldset")]
    };

    static ScaffoldCase RadioEnumCase() => new()
    {
        Name = "EditRadioEnum<Priority>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<Priority>> field = required ? () => model.RadioEnumRequired : () => model.RadioEnumOptional;
            var value = required ? model.RadioEnumRequired : model.RadioEnumOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditRadioEnum<Priority>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => [cut.Find("fieldset.edit-radio-fieldset")]
    };

    static ScaffoldCase RadioStringCase() => new()
    {
        Name = "EditRadioString",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string?>> field = required ? () => model.RadioStringRequired : () => model.RadioStringOptional;
            var value = required ? model.RadioStringRequired : model.RadioStringOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditRadioString>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => [cut.Find("fieldset.edit-radio-fieldset")]
    };

    static ScaffoldCase CheckedEnumCase() => new()
    {
        Name = "EditCheckedEnumList<Priority>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<List<Priority>>> field = required ? () => model.CheckedEnumRequired : () => model.CheckedEnumOptional;
            var value = required ? model.CheckedEnumRequired : model.CheckedEnumOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditCheckedEnumList<Priority>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => cut.FindAll("input[type=checkbox]"),
        // ARIA 1.2 doesn't allow aria-required on role="group" (only role="radiogroup") -- this
        // control's fieldset uses role="group" and its checkboxes carry no aria-required either
        // (CheckboxOptionList.razor's own comment). Required-ness is conveyed by the legend star alone.
        // Matches the existing FormA11yTests.Checked_list_fieldset_exposes_group_semantics test.
        SupportsAriaRequired = false
    };

    static ScaffoldCase CheckedStringCase() => new()
    {
        Name = "EditCheckedStringList",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<List<string>>> field = required ? () => model.CheckedStringRequired : () => model.CheckedStringOptional;
            var value = required ? model.CheckedStringRequired : model.CheckedStringOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditCheckedStringList>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("legend.edit-label-legend"),
        AriaCarriers = cut => cut.FindAll("input[type=checkbox]"),
        SupportsAriaRequired = false // see CheckedEnumCase's remarks -- identical ARIA 1.2 exception
    };

    static ScaffoldCase SelectCase() => new()
    {
        Name = "EditSelect<string>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string>> field = required ? () => model.SelectRequired : () => model.SelectOptional;
            var value = required ? model.SelectRequired : model.SelectOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditSelect<string>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
                    cb.AddMarkupContent(0, "<option value=\"a\">A</option><option value=\"b\">B</option>")));
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("select.edit-select-select")]
    };

    static ScaffoldCase SelectEnumCase() => new()
    {
        Name = "EditSelectEnum<Priority?>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<Priority?>> field = required ? () => model.SelectEnumRequired : () => model.SelectEnumOptional;
            var value = required ? model.SelectEnumRequired : model.SelectEnumOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditSelectEnum<Priority?>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("select.edit-select-select")]
    };

    static ScaffoldCase SelectStringCase() => new()
    {
        Name = "EditSelectString<string>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<string>> field = required ? () => model.SelectStringRequired : () => model.SelectStringOptional;
            var value = required ? model.SelectStringRequired : model.SelectStringOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditSelectString<string>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("select.edit-select-select")]
    };

    static ScaffoldCase SelectSearchCase() => new()
    {
        Name = "EditSelectSearch<Priority?>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<Priority?>> field = required ? () => model.SelectSearchRequired : () => model.SelectSearchOptional;
            var value = required ? model.SelectSearchRequired : model.SelectSearchOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditSelectSearch<Priority?>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", new List<SelectOption<Priority?>>
                {
                    new(Priority.Low, "Low"),
                    new(Priority.Medium, "Medium")
                });
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input.wss-select-selection-search-input")]
    };

    static ScaffoldCase MultiSelectCase() => new()
    {
        Name = "EditMultiSelect<Priority>",
        Build = required =>
        {
            var model = new ScaffoldModel();
            Expression<Func<List<Priority>>> field = required ? () => model.MultiSelectRequired : () => model.MultiSelectOptional;
            var value = required ? model.MultiSelectRequired : model.MultiSelectOptional;
            RenderFragment content = b =>
            {
                b.OpenComponent<EditMultiSelect<Priority>>(0);
                b.AddAttribute(1, "Value", value);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", new List<SelectOption<Priority>>
                {
                    new(Priority.Low, "Low"),
                    new(Priority.Medium, "Medium")
                });
                b.CloseComponent();
            };
            return (model, content);
        },
        Wrapper = cut => cut.Find(".edit-control-wrapper"),
        LabelOrLegend = cut => cut.Find("label.edit-label"),
        AriaCarriers = cut => [cut.Find("input.wss-select-selection-search-input")]
    };

    static ScaffoldCase BuildCase(string name) => name switch
    {
        "EditString" => StringCase(),
        "EditTextArea" => TextAreaCase(),
        "EditNumber<int?>" => NumberCase(),
        "EditBool" => BoolCase(),
        "EditBoolNullRadio" => BoolNullRadioCase(),
        "EditDate<DateTime?>" => DateCase(),
        "EditDateNative<DateTime?>" => DateNativeCase(),
        "EditFile" => FileCase(),
        "EditRadio<string>" => RadioCase(),
        "EditRadioEnum<Priority>" => RadioEnumCase(),
        "EditRadioString" => RadioStringCase(),
        "EditCheckedEnumList<Priority>" => CheckedEnumCase(),
        "EditCheckedStringList" => CheckedStringCase(),
        "EditSelect<string>" => SelectCase(),
        "EditSelectEnum<Priority?>" => SelectEnumCase(),
        "EditSelectString<string>" => SelectStringCase(),
        "EditSelectSearch<Priority?>" => SelectSearchCase(),
        "EditMultiSelect<Priority>" => MultiSelectCase(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown scaffold conformance case.")
    };

    public static TheoryData<string> ControlNames() => new()
    {
        "EditString", "EditTextArea", "EditNumber<int?>", "EditBool", "EditBoolNullRadio",
        "EditDate<DateTime?>", "EditDateNative<DateTime?>", "EditFile",
        "EditRadio<string>", "EditRadioEnum<Priority>", "EditRadioString",
        "EditCheckedEnumList<Priority>", "EditCheckedStringList",
        "EditSelect<string>", "EditSelectEnum<Priority?>", "EditSelectString<string>",
        "EditSelectSearch<Priority?>", "EditMultiSelect<Priority>"
    };

    // ----- shared assertions -------------------------------------------------------------------

    static void AssertLabelAndStar(IRenderedComponent<ContainerFragment> cut, ScaffoldCase c, bool expectRequired)
    {
        var label = c.LabelOrLegend(cut);
        if (c.HasLabelId)
            Assert.StartsWith("lbl-", label.Id ?? string.Empty);

        var stars = label.QuerySelectorAll(".edit-label-required-star");
        if (expectRequired)
            Assert.Single(stars); // "{c.Name}: expected exactly one required-star when the field is required"
        else
            Assert.Empty(stars); // "{c.Name}: expected no required-star when the field is optional"
    }

    static void AssertAriaRequired(IRenderedComponent<ContainerFragment> cut, ScaffoldCase c, bool expectRequired)
    {
        if (!c.SupportsAriaRequired) return;
        foreach (var carrier in c.AriaCarriers(cut))
        {
            if (expectRequired)
                Assert.Equal("true", carrier.GetAttribute("aria-required"));
            else
                Assert.False(carrier.HasAttribute("aria-required"),
                    $"{c.Name}: aria-required must be omitted (not \"false\") when the field is optional.");
        }
    }

    // Derives the resolved error-msg-{id} from the carrier's aria-describedby (no Description/Tooltip
    // is set on any case, so the token list is exactly "error-msg-{id}" -- see
    // EditControlInit.BuildDescribedBy), confirms every carrier agrees, and confirms the id actually
    // resolves to FieldValidationDisplay's rendered container.
    static string AssertDescribedByAndGetErrorMsgId(IRenderedComponent<ContainerFragment> cut, ScaffoldCase c)
    {
        var carriers = c.AriaCarriers(cut);
        Assert.NotEmpty(carriers);
        string? errorMsgId = null;
        foreach (var carrier in carriers)
        {
            var tokens = (carrier.GetAttribute("aria-describedby") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var token = Assert.Single(tokens, t => t.StartsWith("error-msg-", StringComparison.Ordinal));
            errorMsgId ??= token;
            Assert.Equal(errorMsgId, token); // every carrier must reference the same validation message
        }

        Assert.NotNull(cut.Find("#" + errorMsgId));
        return errorMsgId!;
    }

    static void AssertInvalidAfterSubmit(IRenderedComponent<ContainerFragment> cut, ScaffoldCase c, string errorMsgId, bool expectInvalid)
    {
        cut.Find("form").Submit();

        if (c.SupportsAriaInvalid)
        {
            foreach (var carrier in c.AriaCarriers(cut))
                Assert.Equal(expectInvalid, carrier.GetAttribute("aria-invalid") == "true");
        }

        var messages = cut.Find("#" + errorMsgId).QuerySelectorAll("div");
        if (expectInvalid)
            Assert.NotEmpty(messages); // "{c.Name}: expected a validation message after submit"
        else
            Assert.Empty(messages);
    }

    // ----- the theory ----------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(ControlNames))]
    public void Scalar_and_group_controls_satisfy_the_shared_scaffold_invariants(string controlName)
    {
        var c = BuildCase(controlName);

        // ---- Required scenario: star present, aria-required="true" (where supported), and a submit
        // trips AlwaysInvalid -> aria-invalid (where supported) + a rendered validation message. ----
        var (reqModel, reqContent) = c.Build(true);
        var reqCut = Render(WithForm(reqModel, reqContent));

        Assert.NotNull(c.Wrapper(reqCut)); // invariant 1: root .edit-control-wrapper renders
        AssertLabelAndStar(reqCut, c, expectRequired: true); // invariant 2/3 (star half)
        AssertAriaRequired(reqCut, c, expectRequired: true); // invariant 3 (aria-required half)
        var reqErrorMsgId = AssertDescribedByAndGetErrorMsgId(reqCut, c); // invariant 4
        AssertInvalidAfterSubmit(reqCut, c, reqErrorMsgId, expectInvalid: true); // invariant 5

        // ---- Optional scenario: no star, no aria-required, and a submit leaves the field valid. ----
        var (optModel, optContent) = c.Build(false);
        var optCut = Render(WithForm(optModel, optContent));

        Assert.NotNull(c.Wrapper(optCut));
        AssertLabelAndStar(optCut, c, expectRequired: false);
        AssertAriaRequired(optCut, c, expectRequired: false);
        var optErrorMsgId = AssertDescribedByAndGetErrorMsgId(optCut, c);
        AssertInvalidAfterSubmit(optCut, c, optErrorMsgId, expectInvalid: false);
    }

    // ----- EditDateRange: bespoke coverage for its two-field shape --------------------------------

    // [Required]/AlwaysInvalid live on Start only in this model -- EditDateRange derives its single
    // shared star/aria-required from Start alone (see EditDateRange.razor.cs's IsRequiredResolved
    // remarks), and leaving End with no validation attributes at all lets the test also prove Start's
    // failure never bleeds into End's independent validation state.
    class RequiredStartDateRangeModel
    {
        [Required, AlwaysInvalidAttribute] public DateTime? Start { get; set; } = new(2024, 1, 1);
        public DateTime? End { get; set; } = new(2024, 1, 5);
    }

    class OptionalDateRangeModel
    {
        public DateTime? Start { get; set; } = new(2024, 1, 1);
        public DateTime? End { get; set; } = new(2024, 1, 5);
    }

    static string ExtractErrorMsgId(IElement carrier)
    {
        var tokens = (carrier.GetAttribute("aria-describedby") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return Assert.Single(tokens, t => t.StartsWith("error-msg-", StringComparison.Ordinal));
    }

    [Fact]
    public void EditDateRange_satisfies_the_shared_scaffold_invariants()
    {
        // ---- Required (Start only) ----
        var reqModel = new RequiredStartDateRangeModel();
        Expression<Func<DateTime?>> reqStartField = () => reqModel.Start;
        Expression<Func<DateTime?>> reqEndField = () => reqModel.End;
        var reqCut = Render(WithForm(reqModel, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", reqModel.Start);
            b.AddAttribute(2, "StartExpression", reqStartField);
            b.AddAttribute(3, "End", reqModel.End);
            b.AddAttribute(4, "EndExpression", reqEndField);
            b.CloseComponent();
        }));

        Assert.NotNull(reqCut.Find(".edit-control-wrapper")); // invariant 1
        var reqLabel = reqCut.Find("label.edit-label"); // invariant 2 (one shared label, anchored on Start)
        Assert.StartsWith("lbl-", reqLabel.Id ?? string.Empty);
        Assert.Single(reqLabel.QuerySelectorAll(".edit-label-required-star")); // invariant 3

        var reqStartInput = reqCut.Find("input.wss-picker-input-start");
        var reqEndInput = reqCut.Find("input.wss-picker-input-end");
        Assert.Equal("true", reqStartInput.GetAttribute("aria-required"));
        // The shared star/aria-required comes from Start alone -- End never raises it on its own.
        Assert.False(reqEndInput.HasAttribute("aria-required"));

        var reqStartErrorMsgId = ExtractErrorMsgId(reqStartInput); // invariant 4, per field
        var reqEndErrorMsgId = ExtractErrorMsgId(reqEndInput);
        Assert.NotEqual(reqStartErrorMsgId, reqEndErrorMsgId); // Start/End each get their own message container
        Assert.NotNull(reqCut.Find("#" + reqStartErrorMsgId));
        Assert.NotNull(reqCut.Find("#" + reqEndErrorMsgId));

        reqCut.Find("form").Submit();

        Assert.Equal("true", reqCut.Find("input.wss-picker-input-start").GetAttribute("aria-invalid")); // invariant 5
        Assert.NotEmpty(reqCut.Find("#" + reqStartErrorMsgId).QuerySelectorAll("div"));
        // End carries no validation attributes of its own -- Start's failure must not leak onto it.
        Assert.False(reqCut.Find("input.wss-picker-input-end").HasAttribute("aria-invalid"));
        Assert.Empty(reqCut.Find("#" + reqEndErrorMsgId).QuerySelectorAll("div"));

        // ---- Optional (neither field required) ----
        var optModel = new OptionalDateRangeModel();
        Expression<Func<DateTime?>> optStartField = () => optModel.Start;
        Expression<Func<DateTime?>> optEndField = () => optModel.End;
        var optCut = Render(WithForm(optModel, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", optModel.Start);
            b.AddAttribute(2, "StartExpression", optStartField);
            b.AddAttribute(3, "End", optModel.End);
            b.AddAttribute(4, "EndExpression", optEndField);
            b.CloseComponent();
        }));

        Assert.Empty(optCut.Find("label.edit-label").QuerySelectorAll(".edit-label-required-star"));
        Assert.False(optCut.Find("input.wss-picker-input-start").HasAttribute("aria-required"));

        optCut.Find("form").Submit();

        Assert.False(optCut.Find("input.wss-picker-input-start").HasAttribute("aria-invalid"));
        Assert.False(optCut.Find("input.wss-picker-input-end").HasAttribute("aria-invalid"));
    }
}
