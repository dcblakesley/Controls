using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the model-declared placeholder resolution shared by EditString, EditTextArea and EditNumber:
/// each control's own <c>Placeholder</c> parameter wins, else the bound property's
/// <c>[Placeholder]</c>/<c>[Display(Prompt = "…")]</c> supplies the rendered <c>placeholder</c>
/// attribute (see <see cref="Controls.Helpers.AttributesHelper.Placeholder"/>), else the attribute is
/// omitted entirely -- covered at the extension-method level in <see cref="AttributesHelperTests"/>,
/// this file proves the three controls actually wire it through to the DOM.
/// </summary>
public class ModelPlaceholderInputTests : BunitContext
{
    public ModelPlaceholderInputTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate EditString/EditTextArea Clear()'s FocusAsync

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    class PlaceholderModel
    {
        [Placeholder("Attribute placeholder")]
        public string? StringWithAttr { get; set; }

        public string? StringWithNoAttr { get; set; }

        [Placeholder("Attribute placeholder")]
        public string? TextAreaWithAttr { get; set; }

        public string? TextAreaWithNoAttr { get; set; }

        [Placeholder("Attribute placeholder")]
        public int? NumberWithAttr { get; set; }

        public int? NumberWithNoAttr { get; set; }

        [Display(Prompt = "Prompt placeholder")]
        public string? StringWithDisplayPromptOnly { get; set; }
    }

    // EditString

    [Fact]
    public void EditString_renders_the_model_declared_Placeholder_attribute_when_no_parameter_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.StringWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.StringWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Attribute placeholder", cut.Find("input.edit-string-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditString_falls_back_to_Display_Prompt_when_no_PlaceholderAttribute_is_present()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.StringWithDisplayPromptOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.StringWithDisplayPromptOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Prompt placeholder", cut.Find("input.edit-string-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditString_explicit_Placeholder_parameter_overrides_the_model_attribute()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.StringWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.StringWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Placeholder", "Explicit placeholder");
            b.CloseComponent();
        }));

        Assert.Equal("Explicit placeholder", cut.Find("input.edit-string-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditString_renders_no_placeholder_attribute_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.StringWithNoAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.StringWithNoAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("placeholder"));
    }

    // EditTextArea

    [Fact]
    public void EditTextArea_renders_the_model_declared_Placeholder_attribute_when_no_parameter_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.TextAreaWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.TextAreaWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Attribute placeholder", cut.Find("textarea.edit-textarea-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditTextArea_explicit_Placeholder_parameter_overrides_the_model_attribute()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.TextAreaWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.TextAreaWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Placeholder", "Explicit placeholder");
            b.CloseComponent();
        }));

        Assert.Equal("Explicit placeholder", cut.Find("textarea.edit-textarea-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditTextArea_renders_no_placeholder_attribute_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<string?>> field = () => model.TextAreaWithNoAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.TextAreaWithNoAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("textarea.edit-textarea-input").HasAttribute("placeholder"));
    }

    // EditNumber

    [Fact]
    public void EditNumber_renders_the_model_declared_Placeholder_attribute_when_no_parameter_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<int?>> field = () => model.NumberWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.NumberWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Attribute placeholder", cut.Find("input.edit-number-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditNumber_explicit_Placeholder_parameter_overrides_the_model_attribute()
    {
        var model = new PlaceholderModel();
        Expression<Func<int?>> field = () => model.NumberWithAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.NumberWithAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Placeholder", "Explicit placeholder");
            b.CloseComponent();
        }));

        Assert.Equal("Explicit placeholder", cut.Find("input.edit-number-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void EditNumber_renders_no_placeholder_attribute_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new PlaceholderModel();
        Expression<Func<int?>> field = () => model.NumberWithNoAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.NumberWithNoAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("input.edit-number-input").HasAttribute("placeholder"));
    }
}
