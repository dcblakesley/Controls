using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers two previously-untested areas: the <c>IsDisabled</c> parameter (declared on every control but
/// never set in a test) and the hand-maintained <see cref="IEditControl"/> surface on <c>EditRadio</c>
/// (which can't inherit the shared base and so re-declares the interface by hand).
/// </summary>
public class DisabledAndConformanceTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void EditString_IsDisabled_renders_a_disabled_input()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.True(cut.Find("input.edit-string-input").HasAttribute("disabled"));
    }

    [Fact]
    public void EditSelectEnum_IsDisabled_renders_a_disabled_select()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.True(cut.Find("select.edit-select-select").HasAttribute("disabled"));
    }

    [Fact]
    public void EditCheckedStringList_IsDisabled_disables_every_checkbox()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "Field", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.All(cut.FindAll("input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
    }

    [Fact]
    public void EditRadio_declares_a_parameter_for_every_IEditControl_member()
    {
        // EditRadio can't inherit EditControlBase (it must inherit InputRadioGroup to supply the
        // InputRadioContext its <InputRadio> children consume), so it re-declares the IEditControl
        // surface by hand. Guard against drift: every IEditControl property must exist as a [Parameter].
        var radioParameters = typeof(EditRadio<string>)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet();

        var missing = typeof(IEditControl).GetProperties()
            .Select(p => p.Name)
            .Where(name => !radioParameters.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            $"EditRadio<T> is missing [Parameter] declarations for IEditControl members: {string.Join(", ", missing)}");
    }
}
