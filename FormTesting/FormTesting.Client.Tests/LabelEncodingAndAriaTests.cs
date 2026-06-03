using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Regression tests for audit fixes:
/// <list type="bullet">
///   <item>Form labels, descriptions and radio option text must render HTML-encoded. They were
///   previously cast to <c>MarkupString</c>, so a consumer binding a label/description/option from
///   model data could inject markup (XSS). Sibling list/select controls already rendered as text.</item>
///   <item>EditBool's inline description and tooltip ids must match the checkbox's
///   <c>aria-describedby</c>. They used the raw (usually null) <c>Id</c> parameter while the
///   checkbox referenced the resolved <c>_id</c>, so neither was announced.</item>
/// </list>
/// </summary>
public class LabelEncodingAndAriaTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void FormLabel_html_encodes_label_and_description()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Label", "<img src=x onerror=alert(1)>");
            b.AddAttribute(5, "Description", "<b>boom</b>");
            b.CloseComponent();
        }));

        var label = cut.Find("label.edit-label");
        var description = cut.Find(".edit-label-description");

        // No elements injected from the bound strings...
        Assert.Empty(label.QuerySelectorAll("img"));
        Assert.Empty(description.QuerySelectorAll("b"));
        // ...the markup is shown verbatim as text instead.
        Assert.Contains("<img src=x onerror=alert(1)>", label.TextContent);
        Assert.Contains("<b>boom</b>", description.TextContent);
    }

    class StringModel { public string? Choice { get; set; } }

    [Fact]
    public void EditRadioString_html_encodes_option_text()
    {
        var model = new StringModel();
        Expression<Func<string?>> field = () => model.Choice;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Choice);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "<img src=x onerror=alert(1)>", "safe" });
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset");
        Assert.Empty(fieldset.QuerySelectorAll("img"));
        Assert.Contains("<img src=x onerror=alert(1)>", fieldset.TextContent);
    }

    [Fact]
    public void EditBool_description_and_tooltip_ids_match_aria_describedby()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Description", "Whether the account is active");
            b.AddAttribute(5, "Tooltip", "Toggles the active flag");
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        var describedBy = (checkbox.GetAttribute("aria-describedby") ?? "").Split(' ');
        var description = cut.Find("p.edit-label-description");
        var tooltip = cut.Find("[role=tooltip]");

        // The ids must be built from the resolved control id, not the (null) Id parameter.
        Assert.Equal($"desc-{checkbox.Id}", description.Id);
        Assert.Equal($"tooltip-{checkbox.Id}", tooltip.Id);
        // ...and the checkbox must actually point at them.
        Assert.Contains(description.Id, describedBy);
        Assert.Contains(tooltip.Id, describedBy);
    }
}
