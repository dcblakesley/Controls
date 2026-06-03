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
}
