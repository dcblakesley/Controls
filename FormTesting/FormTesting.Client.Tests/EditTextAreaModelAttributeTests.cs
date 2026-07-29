using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditTextArea's model-attribute fallbacks: MaxLength ([StringLength]/[MaxLength] via
/// <see cref="Controls.Helpers.AttributesHelper.MaxTextLength"/>, same wiring as EditString) and the
/// Rows/MinRows/MaxRows/AutoSize quartet against the model's <see cref="RowsAttribute"/> (0 meaning
/// "unset" for the numeric properties -- see <see cref="Controls.Helpers.AttributesHelper.Rows"/>).
/// Each parameter's own value always wins; the model attribute is the fallback; the control's original
/// hard-coded defaults (Rows: 2, MinRows/MaxRows: null, AutoSize: false) are the last resort -- proven
/// here to stay byte-identical to the pre-fallback behavior.
/// </summary>
public class EditTextAreaModelAttributeTests : BunitContext
{
    public EditTextAreaModelAttributeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate Clear()/AutoSize's JS calls

    class RowsAttributeModel
    {
        [Rows(5, MinRows = 3, MaxRows = 10, AutoSize = true)]
        public string? WithFullRowsAttr { get; set; }

        public string? WithNoRowsAttr { get; set; }

        // Only Rows is set on the attribute -- MinRows/MaxRows/AutoSize stay at their 0/false "unset" defaults.
        [Rows(4)]
        public string? WithRowsOnlyAttr { get; set; }

        [StringLength(30)]
        public string? WithStringLength { get; set; }
    }

    // Rows / MinRows / MaxRows / AutoSize

    [Fact]
    public void Rows_attribute_drives_Rows_MinRows_MaxRows_and_AutoSize_when_no_parameters_are_set()
    {
        var planned = JSInterop.SetupVoid("WssEditControls.autoSizeTextArea", _ => true);
        planned.SetVoidResult();

        var model = new RowsAttributeModel { WithFullRowsAttr = "hello" };
        Expression<Func<string?>> field = () => model.WithFullRowsAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithFullRowsAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Contains("edit-textarea-autosize", textarea.ClassList);
        Assert.Equal("3", textarea.GetAttribute("rows")); // AutoSize on -> MinRows (3), not Rows (5)

        // MinRows/MaxRows aren't DOM-visible once AutoSize takes over rendering -- confirm they reached
        // the JS resize call (id, minRows, maxRows) with the attribute's values.
        JSRuntimeInvocation invocation = default;
        cut.WaitForAssertion(() => invocation = Assert.Single(planned.Invocations));
        Assert.Equal(3, invocation.Arguments[1]);
        Assert.Equal(10, invocation.Arguments[2]);
    }

    [Fact]
    public void Explicit_parameters_override_the_model_Rows_attribute()
    {
        var model = new RowsAttributeModel { WithFullRowsAttr = "hello" };
        Expression<Func<string?>> field = () => model.WithFullRowsAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithFullRowsAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Rows", 8);
            b.AddAttribute(5, "AutoSize", false); // overrides the attribute's AutoSize = true
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.DoesNotContain("edit-textarea-autosize", textarea.ClassList);
        Assert.Equal("8", textarea.GetAttribute("rows"));
    }

    [Fact]
    public void Rows_only_attribute_still_supplies_the_AutoSize_floor_when_MinRows_is_unset()
    {
        var model = new RowsAttributeModel { WithRowsOnlyAttr = "hello" };
        Expression<Func<string?>> field = () => model.WithRowsOnlyAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithRowsOnlyAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AutoSize", true); // attribute's AutoSize is unset (false) -- parameter turns it on
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Contains("edit-textarea-autosize", textarea.ClassList);
        // MinRows/MaxRows are 0 (unset) on this attribute -- rows falls back to the attribute's own Rows (4).
        Assert.Equal("4", textarea.GetAttribute("rows"));
    }

    [Fact]
    public void Renders_old_default_rows_and_no_autosize_class_when_neither_parameter_nor_attribute_is_set()
    {
        var model = new RowsAttributeModel { WithNoRowsAttr = "hello" };
        Expression<Func<string?>> field = () => model.WithNoRowsAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithNoRowsAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Equal("2", textarea.GetAttribute("rows"));
        Assert.DoesNotContain("edit-textarea-autosize", textarea.ClassList);
    }

    // MaxLength

    [Fact]
    public void StringLength_attribute_drives_maxlength_and_the_count_text_when_no_parameter_is_set()
    {
        var model = new RowsAttributeModel { WithStringLength = "hello" };
        Expression<Func<string?>> field = () => model.WithStringLength;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithStringLength);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Equal("30", textarea.GetAttribute("maxlength"));
        Assert.Equal("5 / 30", cut.Find(".edit-textarea-count").TextContent);
    }

    [Fact]
    public void Explicit_MaxLength_parameter_overrides_the_model_StringLength_attribute()
    {
        var model = new RowsAttributeModel { WithStringLength = "hello" };
        Expression<Func<string?>> field = () => model.WithStringLength;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.WithStringLength);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaxLength", 10);
            b.CloseComponent();
        }));

        Assert.Equal("10", cut.Find("textarea.edit-textarea-input").GetAttribute("maxlength"));
    }
}
