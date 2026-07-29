using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditString's model-attribute fallbacks for MaxLength ([StringLength]/[MaxLength] via
/// <see cref="Controls.Helpers.AttributesHelper.MaxTextLength"/>), IsPassword ([DataType(DataType.Password)]
/// via <see cref="Controls.Helpers.AttributesHelper.IsPasswordField"/>), and Autocomplete ([Autocomplete]
/// via <see cref="Controls.Helpers.AttributesHelper.Autocomplete"/>). Each parameter's own value always
/// wins; the model attribute is the fallback; the control's original hard-coded default is the last
/// resort -- proven here to stay byte-identical to the pre-fallback behavior.
/// </summary>
public class EditStringModelAttributeTests : BunitContext
{
    public EditStringModelAttributeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate Clear()'s FocusAsync

    class ModelAttributeModel
    {
        [StringLength(20)]
        public string? WithStringLength { get; set; }

        [MaxLength(15)]
        public string? WithMaxLengthAttr { get; set; }

        public string? WithNoLengthAttr { get; set; }

        [DataType(DataType.Password)]
        public string? PasswordField { get; set; }

        public string? PlainField { get; set; }

        [Autocomplete("email")]
        public string? EmailField { get; set; }

        public string? NoAutocompleteField { get; set; }
    }

    // MaxLength

    [Fact]
    public void StringLength_attribute_drives_maxlength_and_the_count_text_when_no_parameter_is_set()
    {
        var model = new ModelAttributeModel { WithStringLength = "Alice" };
        Expression<Func<string?>> field = () => model.WithStringLength;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.WithStringLength);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("20", input.GetAttribute("maxlength"));
        Assert.Equal("5 / 20", cut.Find(".edit-input-count").TextContent);
    }

    [Fact]
    public void MaxLength_attribute_drives_maxlength_when_no_parameter_is_set()
    {
        var model = new ModelAttributeModel { WithMaxLengthAttr = "hi" };
        Expression<Func<string?>> field = () => model.WithMaxLengthAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.WithMaxLengthAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("15", cut.Find("input.edit-string-input").GetAttribute("maxlength"));
    }

    [Fact]
    public void Explicit_MaxLength_parameter_overrides_the_model_StringLength_attribute()
    {
        var model = new ModelAttributeModel { WithStringLength = "Alice" };
        Expression<Func<string?>> field = () => model.WithStringLength;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.WithStringLength);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaxLength", 5);
            b.CloseComponent();
        }));

        Assert.Equal("5", cut.Find("input.edit-string-input").GetAttribute("maxlength"));
    }

    [Fact]
    public void Renders_no_maxlength_attribute_and_a_bare_count_when_neither_parameter_nor_attribute_is_set()
    {
        var model = new ModelAttributeModel { WithNoLengthAttr = "Alice" };
        Expression<Func<string?>> field = () => model.WithNoLengthAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.WithNoLengthAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("maxlength"));
        Assert.Equal("5", cut.Find(".edit-input-count").TextContent);
    }

    // IsPassword

    [Fact]
    public void DataType_Password_attribute_renders_type_password_when_no_parameter_is_set()
    {
        var model = new ModelAttributeModel { PasswordField = "secret" };
        Expression<Func<string?>> field = () => model.PasswordField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.PasswordField);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
        Assert.NotEmpty(cut.FindAll(".edit-input-password-toggle"));
    }

    [Fact]
    public void Explicit_IsPassword_false_parameter_overrides_the_DataType_Password_attribute()
    {
        var model = new ModelAttributeModel { PasswordField = "secret" };
        Expression<Func<string?>> field = () => model.PasswordField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.PasswordField);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", false);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("type"));
        Assert.Empty(cut.FindAll(".edit-input-password-toggle"));
    }

    [Fact]
    public void Explicit_IsPassword_true_parameter_wins_when_no_attribute_is_present()
    {
        var model = new ModelAttributeModel { PlainField = "hello" };
        Expression<Func<string?>> field = () => model.PlainField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.PlainField);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.CloseComponent();
        }));

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
    }

    [Fact]
    public void Renders_no_type_attribute_when_neither_parameter_nor_attribute_is_set()
    {
        var model = new ModelAttributeModel { PlainField = "hello" };
        Expression<Func<string?>> field = () => model.PlainField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.PlainField);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("type"));
    }

    // Autocomplete

    [Fact]
    public void Autocomplete_attribute_supplies_the_token_when_no_parameter_is_set()
    {
        var model = new ModelAttributeModel { EmailField = "a@b.com" };
        Expression<Func<string?>> field = () => model.EmailField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.EmailField);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("email", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    [Fact]
    public void Explicit_Autocomplete_parameter_overrides_the_model_attribute()
    {
        var model = new ModelAttributeModel { EmailField = "a@b.com" };
        Expression<Func<string?>> field = () => model.EmailField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.EmailField);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Autocomplete", "tel");
            b.CloseComponent();
        }));

        Assert.Equal("tel", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    [Fact]
    public void Default_autocomplete_is_one_time_code_when_neither_parameter_nor_attribute_is_set()
    {
        var model = new ModelAttributeModel { NoAutocompleteField = "hello" };
        Expression<Func<string?>> field = () => model.NoAutocompleteField;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.NoAutocompleteField);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("one-time-code", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }
}
