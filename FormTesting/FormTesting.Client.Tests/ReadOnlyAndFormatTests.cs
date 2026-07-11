using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Regression tests for fixes made during the library audit:
/// <list type="bullet">
///   <item>ReadOnlyValue must HTML-encode the bound text — it previously rendered it as a raw
///   <c>MarkupString</c>, which let bound user data inject markup (an XSS hole).</item>
///   <item>EditDate's read-only display must format the bound value with <c>DateFormat</c> by the
///   value's own type, and degrade (not throw) when the format is incompatible with that type.</item>
/// </list>
/// </summary>
public class ReadOnlyAndFormatTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    class HtmlModel { public string Name { get; set; } = ""; }

    [Fact]
    public void ReadOnlyValue_html_encodes_bound_text_rather_than_rendering_markup()
    {
        // A value containing HTML must show as literal text, never inject DOM elements.
        var model = new HtmlModel { Name = "<b>x</b><img src=z onerror=alert(1)>" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ro = cut.Find(".edit-readonly-value");
        Assert.Empty(ro.QuerySelectorAll("b, img"));   // no elements injected
        Assert.Contains("<b>x</b>", ro.TextContent);    // raw text preserved verbatim
    }

    class DateModel { public DateTime? When { get; set; } }

    [Fact]
    public void EditDate_read_only_formats_value_with_DateFormat()
    {
        var model = new DateModel { When = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.When;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.When);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "DateFormat", "yyyy-MM-dd");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2020-03-05", cut.Find(".edit-readonly-value").TextContent);
    }

    class TimeModel { public TimeOnly? At { get; set; } }

    [Fact]
    public void EditDate_read_only_with_incompatible_format_degrades_without_throwing()
    {
        // A date-style DateFormat applied to a TimeOnly throws FormatException inside ToString; the
        // control must catch it and fall back to the value's own ToString rather than crash the render.
        var model = new TimeModel { At = new TimeOnly(13, 45) };
        Expression<Func<TimeOnly?>> field = () => model.At;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<TimeOnly?>>(0);
            b.AddAttribute(1, "Value", model.At);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Type", InputDateType.Time);
            b.AddAttribute(5, "DateFormat", "yyyy-MM-dd");   // incompatible with TimeOnly
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ro = cut.Find(".edit-readonly-value");
        Assert.False(string.IsNullOrWhiteSpace(ro.TextContent));   // rendered the fallback, no crash
    }
}
