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
public class FormA11yTests : BunitContext
{
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
        var cut = Render(WithValidatedForm(model, false, b => AddEditString(b, model, field, ("IsLabelHidden", true))));

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
        var cut = Render(WithValidatedForm(model, false, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsLabelHidden", true);
            b.CloseComponent();
        }));

        // EditBool's hidden-label path once emitted a bare checkbox with no accessible name; it now
        // renders a visually-hidden label bound to the checkbox by id. The label markup comes from
        // FormLabel (via its NestedInput slot) like every other control.
        var srLabel = cut.Find("label.edit-sr-only");
        Assert.Equal("IsActive", srLabel.GetAttribute("for"));
        Assert.Contains("Is Active", srLabel.TextContent);
        Assert.Equal("IsActive", cut.Find("input[type=checkbox]").Id);
    }

    [Fact]
    public void Hidden_label_keeps_the_description_visually_hidden_rather_than_dropping_it()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, false, b => AddEditString(b, model, field,
            ("IsLabelHidden", true), ("Description", "Format: first last"), ("Tooltip", "Some hint"))));

        // The hidden-label branch used to render the label alone, silently deleting the field's
        // format instructions for EVERY user -- hiding a label is a layout decision (the field sits
        // under a column header, say), not a decision to drop its instructions.
        var description = cut.Find("#desc-Name");
        Assert.Contains("edit-sr-only", description.ClassList);
        Assert.Equal("Format: first last", description.TextContent.Trim());

        var describedBy = (cut.Find("input.edit-string-input").GetAttribute("aria-describedby") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("desc-Name", describedBy);
        // The tooltip is the deliberate exception: it is an interactive hover/focus widget and the
        // hidden-label branch renders no trigger for it, so the reference would dangle.
        Assert.DoesNotContain("tooltip-Name", describedBy);
        Assert.Empty(cut.FindAll("#tooltip-Name"));
        // Nothing in the token list may point at a missing element, in either state.
        foreach (var token in describedBy) Assert.NotNull(cut.Find("#" + token));
    }

    [Fact]
    public void A_visible_label_still_renders_exactly_one_description_element()
    {
        // The hidden branch gained a desc- element; the visible branch must not have grown a second.
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, false,
            b => AddEditString(b, model, field, ("Description", "Format: first last"))));

        Assert.Single(cut.FindAll("#desc-Name"));
        Assert.Contains("edit-label-description", cut.Find("#desc-Name").ClassList);
        Assert.Contains("desc-Name", cut.Find("input.edit-string-input").GetAttribute("aria-describedby")!);
    }

    // Both halves of an EditDateRange carrying their own [Description] -- only the Start-anchored
    // FormLabel renders one, so only Start may reference it.
    class DescribedRangeModel
    {
        [System.ComponentModel.Description("When the window opens")] public DateTime? Start { get; set; }
        [System.ComponentModel.Description("When the window closes")] public DateTime? End { get; set; }
    }

    [Fact]
    public void EditDateRange_end_input_never_references_a_description_only_Start_renders()
    {
        var model = new DescribedRangeModel { Start = new DateTime(2024, 1, 1), End = new DateTime(2024, 1, 5) };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithValidatedForm(model, false, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        // Description/Tooltip belong to the control as a whole and are rendered by the Start-anchored
        // FormLabel, so the End input's describedby is just its own validation message. The End half
        // used to be kept honest by the label-hidden gate it passes; with desc- no longer gated on
        // that, its attribute list has to stay out of the aria-ref resolution entirely.
        // Both halves also carry the picker's own visually-hidden format hint ("{Id}-format"), appended
        // after the wrapper-supplied chain so the error/description ids keep their reading order.
        Assert.Equal("error-msg-Start desc-Start Start-format", cut.Find("input.wss-picker-input-start").GetAttribute("aria-describedby"));
        // The End half's id is derived from Start's ("{id}-end"), so its would-be description element
        // is #desc-Start-end -- which nothing renders.
        Assert.Equal("error-msg-Start-end Start-format", cut.Find("input.wss-picker-input-end").GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll("#desc-Start-end"));
        Assert.NotNull(cut.Find("#desc-Start"));
    }

    [Fact]
    public void The_visible_validation_copy_is_hidden_from_AT_so_errors_are_not_read_twice()
    {
        var model = new PersonModel { Name = "" };  // [Required]
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, true, b => AddEditString(b, model, field)));
        cut.Find("form").Submit();

        // Both copies carry the same message; only the sr-only one is meant to be read. Without
        // aria-hidden on the visible copy, browse mode walked through both and read every error
        // twice. It holds text only -- no focusable content, the one thing aria-hidden must not
        // swallow.
        var srOnly = cut.Find(".edit-validation-message.edit-sr-only");
        var visible = cut.Find(".edit-validation-message:not(.edit-sr-only)");
        Assert.False(srOnly.HasAttribute("aria-hidden"));
        Assert.Equal("true", visible.GetAttribute("aria-hidden"));
        Assert.NotEmpty(visible.QuerySelectorAll("div"));   // ...and it really is a second copy
        Assert.NotEmpty(srOnly.QuerySelectorAll("div"));
    }

    [Fact]
    public void Read_only_label_drops_the_for_attribute()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, false, b => AddEditString(b, model, field, ("IsEditMode", false))));

        // label[for] must reference a labelable element; the read-only value is a div (named via
        // aria-labelledby), so the label renders unassociated.
        Assert.False(cut.Find("label.edit-label").HasAttribute("for"));
        Assert.Equal($"lbl-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Read_only_hidden_label_still_names_the_value_via_the_visually_hidden_label()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, false,
            b => AddEditString(b, model, field, ("IsEditMode", false), ("IsLabelHidden", true))));

        // The hidden label renders lbl-Name (visually hidden) instead of nothing at all, so the
        // read-only div must point at it — omitting aria-labelledby here, on the premise that no
        // label element existed, left read-only hidden-label fields unnamed to assistive tech.
        Assert.Equal("lbl-Name", cut.Find("label.edit-sr-only").Id);
        Assert.Equal("lbl-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Read_only_checked_list_option_rows_carry_no_dangling_aria_labelledby()
    {
        var model = new PersonModel { Tags = ["a", "b"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithValidatedForm(model, false, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        // The per-option rows carry their own derived ids and have no label element of their own
        // (only the group's legend renders an lbl- id), so they must reference nothing at all — the
        // one case that opts out of ReadOnlyValue's always-on aria-labelledby.
        var rows = cut.FindAll(".edit-readonly-value");
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.False(r.HasAttribute("aria-labelledby")));
        Assert.Equal("lbl-Tags", cut.Find("legend").Id);
    }

    [Fact]
    public void Checked_list_fieldset_exposes_group_semantics_without_unsupported_aria()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithValidatedForm(model, false, b =>
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
        var cut = Render(WithValidatedForm(model, false, b =>
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
        cut.FindComponent<EditBool>().Render(p => p.Add(x => x.Label, "Second"));
        Assert.Contains("Second", cut.Find("label.edit-checkbox-label").TextContent);
    }

    [Fact]
    public void Consumer_class_containing_invalid_does_not_trip_the_invalid_state()
    {
        var model = new PersonModel { Name = "valid value" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithValidatedForm(model, true, b => AddEditString(b, model, field, ("class", "invalid-style-fix"))));

        // IsInvalid used to substring-match "invalid" in CssClass, so this rendered aria-invalid
        // plus the red X despite the field being perfectly valid.
        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-invalid"));
        Assert.Empty(cut.FindAll("svg.edit-icon-invalid"));
    }
}
