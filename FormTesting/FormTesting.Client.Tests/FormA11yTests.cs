using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Accessibility regressions from the a11y hardening pass: hidden labels keep an accessible name,
/// checked-list group semantics, one element per validation message, dynamic Label updates, and
/// label[for] only referencing labelable elements.
/// </summary>
public class FormA11yTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, bool withValidator, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
        {
            if (withValidator)
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
            }
            inner(content);
        }));
        builder.CloseComponent();
    };

    static void AddEditString(RenderTreeBuilder b, PersonModel model, Expression<Func<string>> field, params (string Name, object Value)[] extra)
    {
        b.OpenComponent<EditString>(0);
        b.AddAttribute(1, "Value", model.Name);
        b.AddAttribute(2, "ValueExpression", field);
        var seq = 4;
        foreach (var (name, value) in extra)
            b.AddAttribute(seq++, name, value);
        b.CloseComponent();
    }

    [Fact]
    public void Hidden_label_still_names_the_input_via_a_visually_hidden_label()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, false, b => AddEditString(b, model, field, ("IsLabelHidden", true))));

        // Previously nothing rendered at all — an unnamed field to assistive tech.
        var srLabel = cut.Find("label.edit-sr-only");
        Assert.Equal("Name", srLabel.GetAttribute("for"));
        Assert.Contains("Full Name", srLabel.TextContent);
    }

    [Fact]
    public void EditBool_hidden_label_still_names_the_checkbox_via_a_visually_hidden_label()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, false, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsLabelHidden", true);
            b.CloseComponent();
        }));

        // EditBool's edit branch renders its own <label> (not FormLabel), so its hidden-label path used
        // to emit a bare checkbox with no accessible name. It now renders a visually-hidden label bound
        // to the checkbox by id — matching the FormLabel path every other control uses.
        var srLabel = cut.Find("label.edit-sr-only");
        Assert.Equal("IsActive", srLabel.GetAttribute("for"));
        Assert.Contains("Is Active", srLabel.TextContent);
        Assert.Equal("IsActive", cut.Find("input[type=checkbox]").Id);
    }

    [Fact]
    public void Read_only_label_drops_the_for_attribute()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, false, b => AddEditString(b, model, field, ("IsEditMode", false))));

        // label[for] must reference a labelable element; the read-only value is a div (named via
        // aria-labelledby), so the label renders unassociated.
        Assert.False(cut.Find("label.edit-label").HasAttribute("for"));
        Assert.Equal($"lbl-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Checked_list_fieldset_exposes_group_semantics_without_unsupported_aria()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, false, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsRequired", (bool?)true);
            b.CloseComponent();
        }));

        // ARIA 1.2 supports aria-required/aria-invalid/aria-errormessage on radiogroup but NOT on
        // group, so the checkbox fieldset must carry none of them (AT ignores them; axe flags them).
        // Required-ness is conveyed by the legend star; invalid state by each checkbox's aria-invalid.
        var fieldset = cut.Find("fieldset.edit-checkedList-fieldset");
        Assert.Equal("group", fieldset.GetAttribute("role"));
        Assert.False(fieldset.HasAttribute("aria-required"));
        Assert.False(fieldset.HasAttribute("aria-invalid"));
        Assert.False(fieldset.HasAttribute("aria-errormessage"));
        Assert.NotNull(cut.Find(".edit-label-required-star")); // the star still marks the group required
    }

    class TwoRuleModel
    {
        [System.ComponentModel.DataAnnotations.MinLength(5)]
        [System.ComponentModel.DataAnnotations.RegularExpression("^[0-9]+$", ErrorMessage = "Digits only")]
        public string Code { get; set; } = "ab"; // fails both rules at once ([Required] would short-circuit)
    }

    [Fact]
    public void Each_validation_message_renders_in_its_own_element()
    {
        var model = new TwoRuleModel();
        Expression<Func<string>> field = () => model.Code;
        var cut = Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "Model", model);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<DataAnnotationsValidator>(0);
                b.CloseComponent();
                b.OpenComponent<EditString>(1);
                b.AddAttribute(2, "Value", model.Code);
                b.AddAttribute(3, "ValueExpression", field);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.Find("form").Submit();

        // Two messages used to concatenate into one text run ("RequiredMust be between…").
        var visible = cut.FindAll(".edit-validation-message:not(.edit-sr-only) > div");
        Assert.True(visible.Count >= 2, $"expected each message in its own element, found {visible.Count}");
    }

    [Fact]
    public void Dynamic_Label_change_updates_EditBool_and_validation_labels()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, false, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Label", "First");
            b.CloseComponent();
        }));

        Assert.Contains("First", cut.Find("label.edit-checkbox-label").TextContent);

        // CLAUDE.md documents the Label parameter as the vehicle for dynamic/runtime text — a
        // change must not be frozen at the first value.
        cut.FindComponent<EditBool>().SetParametersAndRender(p => p.Add(x => x.Label, "Second"));
        Assert.Contains("Second", cut.Find("label.edit-checkbox-label").TextContent);
    }

    [Fact]
    public void Consumer_class_containing_invalid_does_not_trip_the_invalid_state()
    {
        var model = new PersonModel { Name = "valid value" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, true, b => AddEditString(b, model, field, ("class", "invalid-style-fix"))));

        // IsInvalid used to substring-match "invalid" in CssClass, so this rendered aria-invalid
        // plus the red X despite the field being perfectly valid.
        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("svg.edit-icon-invalid"));
    }
}
