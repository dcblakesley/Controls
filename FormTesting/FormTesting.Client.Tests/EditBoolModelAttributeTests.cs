using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the model-declared <c>[BoolText]</c> resolution EditBool wires through
/// <see cref="Controls.Helpers.AttributesHelper.BoolText"/>: the control's own <c>TrueText</c>/
/// <c>FalseText</c> parameters win, else the bound property's <c>[BoolText]</c> supplies the
/// read-only display text, else the control's built-in "Yes"/"No" defaults apply. Read-only mode
/// (<see cref="ReadOnlyValue"/>) is the easiest observable surface for both texts at once, so every
/// test here renders with <c>IsEditMode=false</c>.
/// </summary>
public class EditBoolModelAttributeTests : BunitContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    class BoolTextModel
    {
        [BoolText(TrueText = "Enabled", FalseText = "Disabled")]
        public bool WithBothAttrs { get; set; }

        // Only TrueText set -- FalseText must still fall through to the control's own "No" default.
        [BoolText(TrueText = "Enabled")]
        public bool WithTrueTextOnly { get; set; }

        public bool WithNoAttrs { get; set; }
    }

    [Fact]
    public void Model_declared_BoolText_attribute_drives_the_read_only_text_when_no_parameter_is_set()
    {
        var model = new BoolTextModel { WithBothAttrs = true };
        Expression<Func<bool>> field = () => model.WithBothAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithBothAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("Enabled", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Model_declared_BoolText_attribute_drives_the_read_only_text_for_the_false_branch_too()
    {
        var model = new BoolTextModel { WithBothAttrs = false };
        Expression<Func<bool>> field = () => model.WithBothAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithBothAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("Disabled", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Partial_BoolText_attribute_leaves_FalseText_at_its_built_in_default()
    {
        var model = new BoolTextModel { WithTrueTextOnly = false };
        Expression<Func<bool>> field = () => model.WithTrueTextOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithTrueTextOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        // [BoolText(TrueText = "Enabled")] sets no FalseText -- must fall through to "No", not null/empty.
        Assert.Equal("No", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Explicit_TrueText_and_FalseText_parameters_override_the_model_attribute()
    {
        var model = new BoolTextModel { WithBothAttrs = true };
        Expression<Func<bool>> field = () => model.WithBothAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithBothAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "TrueText", "Explicit True");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("Explicit True", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Defaults_are_unchanged_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new BoolTextModel { WithNoAttrs = true };
        Expression<Func<bool>> field = () => model.WithNoAttrs;
        var cutTrue = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));
        Assert.Equal("Yes", cutTrue.Find(".edit-readonly-value").TextContent);

        model.WithNoAttrs = false;
        var cutFalse = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));
        Assert.Equal("No", cutFalse.Find(".edit-readonly-value").TextContent);
    }
}
