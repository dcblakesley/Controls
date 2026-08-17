using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// EditNumber gets the same <c>Suggestions</c> datalist wiring as EditString (see
/// <see cref="EditStringSuggestionsTests"/> for the full contract this mirrors) -- <c>list</c> is valid
/// HTML on <c>type="number"</c>, and it drops in cleanly alongside the stepper's button-group markup
/// since the datalist renders as the shell's sibling either way. This file pins that the wiring exists
/// on EditNumber too, plus the two things EditNumber implements in its OWN code rather than sharing --
/// the per-instance datalist id and the placement of the datalist relative to the stepper group. The
/// null/empty/encoding semantics stay EditString's tests, to avoid duplicating coverage of logic that
/// really is byte-identical.
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
        // Per-instance dl-{guid}, never derived from the field name -- see EditString.SuggestionsListId.
        Assert.StartsWith("dl-", datalist.Id);
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
    public void Instances_bound_to_the_SAME_property_get_distinct_datalists_each_resolving_to_its_own()
    {
        // EditNumber carries its own copy of the id mechanism, so it needs its own copy of the test --
        // see EditStringSuggestionsTests for the full rationale (a row list binding one property name
        // otherwise emitted one shared datalist id and showed every row the FIRST row's suggestions).
        var rows = new[] { new PersonModel { Price = 1m }, new PersonModel { Price = 2m } };
        var cut = Render(WithForm(rows, b =>
        {
            var seq = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                Expression<Func<decimal?>> field = () => row.Price;
                b.OpenComponent<EditNumber<decimal?>>(seq++);
                b.AddAttribute(seq++, "Value", row.Price);
                b.AddAttribute(seq++, "ValueExpression", field);
                b.AddAttribute(seq++, "Suggestions", new List<string> { $"row{i}" });
                b.CloseComponent();
            }
        }));

        var inputs = cut.FindAll("input.edit-number-input");
        Assert.Equal(2, inputs.Count);
        Assert.Single(inputs.Select(i => i.Id).Distinct()); // the element ids really do collide

        for (var i = 0; i < rows.Length; i++)
        {
            var target = Assert.Single(cut.FindAll($"datalist#{inputs[i].GetAttribute("list")}"));
            Assert.Equal(
                new[] { $"row{i}" },
                target.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
        }
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
