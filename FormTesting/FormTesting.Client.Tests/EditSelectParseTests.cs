using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// End-to-end coverage of the select parse path: a native &lt;select&gt; change sets
/// CurrentValueAsString, which routes through the control's TryParseValueFromString into the shared
/// SelectParsing helper. SelectParsingTests covers the helper in isolation; these confirm the
/// controls actually delegate to it and write the parsed value back through ValueChanged.
/// </summary>
public class EditSelectParseTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void EditSelectEnum_rebuilds_options_when_Sort_changes_at_runtime()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Sort", false);
            b.CloseComponent();
        }));

        var unsortedFirst = cut.FindAll("select option")[1].TextContent.Trim(); // [0] is the null option
        Assert.Equal("Low", unsortedFirst);

        // Previously the cached option list was frozen at init and a Sort change was ignored.
        cut.FindComponent<EditSelectEnum<Priority?>>().SetParametersAndRender(p => p.Add(x => x.Sort, true));
        var sortedFirst = cut.FindAll("select option")[1].TextContent.Trim();
        Assert.Equal("Critical", sortedFirst);
    }

    enum EmptyEnum { }

    class EmptyEnumModel
    {
        public EmptyEnum? Choice { get; set; }
    }

    [Fact]
    public void EditRadioEnum_with_empty_enum_and_other_option_renders_read_only_without_throwing()
    {
        var model = new EmptyEnumModel();
        Expression<Func<EmptyEnum?>> field = () => model.Choice;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<EmptyEnum?>>(0);
            b.AddAttribute(1, "Value", model.Choice);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "HasOtherOption", true);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        // GetOptions().Last() used to throw on an empty enum in the read-only branch.
        Assert.NotNull(cut.Find(".edit-readonly-value"));
    }

    [Fact]
    public void EditSelectString_null_value_selects_the_leading_empty_option()
    {
        var model = new PersonModel { Name = null! };
        Expression<Func<string?>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", (string?)null);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        // A null value used to render with "a" visually selected while the model stayed null.
        Assert.True(cut.Find("option[value='']").HasAttribute("selected"));
        Assert.False(cut.Find("option[value='a']").HasAttribute("selected"));
    }

    [Fact]
    public void EditSelectEnum_change_parses_and_updates_bound_value()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("High");
        Assert.Equal(Priority.High, model.Priority);
    }

    [Fact]
    public void EditSelectEnum_empty_change_clears_nullable_value()
    {
        var model = new PersonModel { Priority = Priority.High };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("");   // empty is valid (null) for a nullable enum
        Assert.Null(model.Priority);
    }

    [Fact]
    public void EditSelectEnum_invalid_change_leaves_value_unchanged()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("NotAValidMember");   // failed parse must not corrupt the model
        Assert.Equal(Priority.Medium, model.Priority);
    }

    [Fact]
    public void EditSelectString_change_parses_and_updates_bound_value()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, v => model.Name = v));
            b.AddAttribute(4, "Field", field);
            b.AddAttribute(5, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        cut.Find("select").Change("b");
        Assert.Equal("b", model.Name);
    }

    class RatioModel { public double Ratio { get; set; } }

    [Fact]
    public void EditSelect_double_selects_the_matching_option_under_a_foreign_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            var model = new RatioModel { Ratio = 1.5 };
            Expression<Func<double>> field = () => model.Ratio;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditSelect<double>>(0);
                b.AddAttribute(1, "Value", model.Ratio);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Field", field);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenElement(0, "option");
                    cb.AddAttribute(1, "value", "1.5");
                    cb.AddContent(2, "One and a half");
                    cb.CloseElement();
                    cb.OpenElement(3, "option");
                    cb.AddAttribute(4, "value", "2.5");
                    cb.AddContent(5, "Two and a half");
                    cb.CloseElement();
                }));
                b.CloseComponent();
            }));

            // Before the fix, the value formatted as "1,5" under de-DE, matched no <option value>, and
            // the select showed unselected. FormatValueAsString is now invariant, so "1.5" matches.
            Assert.True(cut.Find("option[value='1.5']").HasAttribute("selected"));
            Assert.False(cut.Find("option[value='2.5']").HasAttribute("selected"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
