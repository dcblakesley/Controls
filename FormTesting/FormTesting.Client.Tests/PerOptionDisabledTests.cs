using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// <c>IsOptionDisabled</c> on the four group controls (<see cref="EditCheckedStringList"/>,
/// <see cref="EditCheckedEnumList{TEnum}"/>, <see cref="EditRadioString"/>,
/// <see cref="EditRadioEnum{TEnum}"/>): the predicate disables individual options, composed with
/// the whole-group <c>IsDisabled</c> (either one disables an option).
/// </summary>
public class PerOptionDisabledTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ----- EditCheckedStringList ---------------------------------------------------------------

    [Fact]
    public void EditCheckedStringList_IsOptionDisabled_unused_leaves_every_checkbox_enabled()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        Assert.All(cut.FindAll("input[type=checkbox]"), c =>
        {
            Assert.False(c.HasAttribute("disabled"));
            Assert.False(c.HasAttribute("aria-disabled"));
        });
    }

    [Fact]
    public void EditCheckedStringList_IsOptionDisabled_disables_only_the_matching_option()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsOptionDisabled", (Func<string, bool>)(o => o == "b"));
            b.CloseComponent();
        }));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.False(checkboxes.First(c => c.GetAttribute("value") == "a").HasAttribute("disabled"));
        var bBox = checkboxes.First(c => c.GetAttribute("value") == "b");
        Assert.True(bBox.HasAttribute("disabled"));
        Assert.Equal("true", bBox.GetAttribute("aria-disabled"));
        Assert.False(checkboxes.First(c => c.GetAttribute("value") == "c").HasAttribute("disabled"));
    }

    [Fact]
    public void EditCheckedStringList_group_IsDisabled_wins_even_when_predicate_returns_false()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsOptionDisabled", (Func<string, bool>)(_ => false));
            b.AddAttribute(5, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.All(cut.FindAll("input[type=checkbox]"), c => Assert.True(c.HasAttribute("disabled")));
    }

    // ----- EditCheckedEnumList ------------------------------------------------------------------

    [Fact]
    public void EditCheckedEnumList_IsOptionDisabled_disables_only_the_matching_enum_value()
    {
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsOptionDisabled", (Func<Color, bool>)(c => c == Color.Blue));
            b.CloseComponent();
        }));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.False(checkboxes.First(c => c.GetAttribute("value") == "Red").HasAttribute("disabled"));
        Assert.True(checkboxes.First(c => c.GetAttribute("value") == "Blue").HasAttribute("disabled"));
    }

    // ----- EditRadioString -----------------------------------------------------------------------

    [Fact]
    public void EditRadioString_IsOptionDisabled_disables_only_the_matching_option()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsOptionDisabled", (Func<string, bool>)(o => o == "b"));
            b.CloseComponent();
        }));

        Assert.False(cut.Find("#rb-Name-a").HasAttribute("disabled"));
        Assert.True(cut.Find("#rb-Name-b").HasAttribute("disabled"));
        Assert.False(cut.Find("#rb-Name-c").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadioString_IsOptionDisabled_does_not_affect_the_Other_radio()
    {
        var model = new PersonModel { Name = "" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "HasOther", true);
            b.AddAttribute(5, "IsOptionDisabled", (Func<string, bool>)(_ => true)); // disables every real option
            b.CloseComponent();
        }));

        Assert.True(cut.Find("#rb-Name-a").HasAttribute("disabled"));
        Assert.True(cut.Find("#rb-Name-b").HasAttribute("disabled"));
        Assert.False(cut.Find("#rb-Name-other").HasAttribute("disabled")); // predicate has no options entry for it
    }

    // ----- EditRadioEnum -------------------------------------------------------------------------

    [Fact]
    public void EditRadioEnum_IsOptionDisabled_disables_only_the_matching_enum_value()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsOptionDisabled", (Func<Priority?, bool>)(p => p == Priority.High));
            b.CloseComponent();
        }));

        var radios = cut.FindAll("input[type=radio]");
        Assert.False(radios.First(r => r.GetAttribute("value") == "Low").HasAttribute("disabled"));
        Assert.True(radios.First(r => r.GetAttribute("value") == "High").HasAttribute("disabled"));
        Assert.False(radios.First(r => r.GetAttribute("value") == "Critical").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadioEnum_IsOptionDisabled_applies_to_the_Other_option_too()
    {
        // HasOtherOption re-purposes the last enum value as "Other" -- the predicate still sees it
        // as a real TEnum value (Critical here), so it composes normally.
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.AddAttribute(4, "IsOptionDisabled", (Func<Priority?, bool>)(p => p == Priority.Critical));
            b.CloseComponent();
        }));

        var radios = cut.FindAll("input[type=radio]");
        Assert.True(radios.First(r => r.GetAttribute("value") == "Critical").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadioEnum_group_IsDisabled_wins_even_when_predicate_returns_false()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsOptionDisabled", (Func<Priority?, bool>)(_ => false));
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.All(cut.FindAll("input[type=radio]"), r => Assert.True(r.HasAttribute("disabled")));
    }
}
