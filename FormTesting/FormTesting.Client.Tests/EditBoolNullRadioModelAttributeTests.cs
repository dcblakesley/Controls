using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the model-declared <c>[BoolText]</c> resolution EditBoolNullRadio wires through
/// <see cref="Controls.Helpers.AttributesHelper.BoolText"/>: the control's own <c>TrueText</c>/
/// <c>FalseText</c>/<c>NullText</c> parameters win, else the bound property's <c>[BoolText]</c>
/// supplies the three radio-option labels (and the read-only display text), else the control's
/// built-in "Yes"/"No"/"Not Set" defaults apply.
/// </summary>
public class EditBoolNullRadioModelAttributeTests : BunitContext
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
        [BoolText(TrueText = "Absolutely", FalseText = "No way", NullText = "Undecided")]
        public bool? WithAllThreeAttrs { get; set; }

        public bool? WithNoAttrs { get; set; }
    }

    [Fact]
    public void Model_declared_BoolText_attribute_renders_all_three_texts_when_no_parameter_is_set()
    {
        var model = new BoolTextModel { WithAllThreeAttrs = null };
        Expression<Func<bool?>> field = () => model.WithAllThreeAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.WithAllThreeAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var labelTexts = cut.FindAll(".edit-radio-label").Select(l => l.TextContent.Trim());
        // Render order is False, True, NullOption -- matches EditBoolNullRadio.razor's markup order.
        Assert.Equal(["No way", "Absolutely", "Undecided"], labelTexts);
    }

    [Fact]
    public void NullText_parameter_overrides_the_model_attribute()
    {
        var model = new BoolTextModel { WithAllThreeAttrs = null };
        Expression<Func<bool?>> field = () => model.WithAllThreeAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.WithAllThreeAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "NullText", "Explicit Not Set");
            b.CloseComponent();
        }));

        var labelTexts = cut.FindAll(".edit-radio-label").Select(l => l.TextContent.Trim());
        Assert.Equal(["No way", "Absolutely", "Explicit Not Set"], labelTexts);
    }

    [Fact]
    public void Defaults_are_unchanged_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new BoolTextModel { WithNoAttrs = null };
        Expression<Func<bool?>> field = () => model.WithNoAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.WithNoAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var labelTexts = cut.FindAll(".edit-radio-label").Select(l => l.TextContent.Trim());
        Assert.Equal(["No", "Yes", "Not Set"], labelTexts);
    }

    [Fact]
    public void Model_declared_BoolText_attribute_also_drives_the_read_only_display_text()
    {
        var model = new BoolTextModel { WithAllThreeAttrs = true };
        Expression<Func<bool?>> field = () => model.WithAllThreeAttrs;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.WithAllThreeAttrs);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("Absolutely", cut.Find(".edit-readonly-value").TextContent);
    }
}
