using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// EditNumber gets the same <c>Suggestions</c> datalist wiring as EditString (see
/// <see cref="EditStringSuggestionsTests"/> for the full contract this mirrors) -- <c>list</c> is valid
/// HTML on <c>type="number"</c>, and it drops in cleanly alongside the stepper's button-group markup
/// since the datalist renders as the shell's sibling either way. This file only pins that the wiring
/// exists on EditNumber too; the null/empty/encoding/uniqueness semantics are EditString's tests to
/// avoid duplicating coverage of shared, byte-identical logic.
/// </summary>
public class EditNumberSuggestionsTests : BunitContext
{
    [Fact]
    public void Datalist_renders_with_the_given_options_and_the_input_list_points_at_it()
    {
        var model = new PersonModel { Price = 30m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Suggestions", new List<string> { "9.99", "19.99", "29.99" });
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        var datalist = cut.Find("datalist");
        Assert.Equal("dl-Price", datalist.Id);
        Assert.Equal(datalist.Id, input.GetAttribute("list"));
        Assert.Equal(
            new[] { "9.99", "19.99", "29.99" },
            datalist.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
    }

    [Fact]
    public void No_datalist_or_list_attribute_when_Suggestions_is_unset()
    {
        var model = new PersonModel { Price = 30m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.False(input.HasAttribute("list"));
        Assert.Empty(cut.FindAll("datalist"));
    }

    [Fact]
    public void Suggestions_drop_in_cleanly_alongside_the_stepper_markup()
    {
        // ShowStepper wraps EditorFragment in a button group -- the datalist still has to land as the
        // shell's sibling, not get lost inside the stepper's div.
        var model = new PersonModel { Price = 30m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Suggestions", new List<string> { "9.99" });
            b.AddAttribute(4, "ShowStepper", true);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        var datalist = cut.Find("datalist");
        Assert.Equal(datalist.Id, input.GetAttribute("list"));
        Assert.NotEmpty(cut.FindAll(".edit-number-step"));
    }
}
