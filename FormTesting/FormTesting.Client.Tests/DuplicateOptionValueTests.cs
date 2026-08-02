using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// Option lists whose values are not distinct — alias enum members (<c>Active = 1, Enabled = 1</c>)
/// and a consumer <c>Options</c> list with a repeated string. <c>CheckboxOptionList</c> keyed its
/// siblings on the option VALUE, so both inputs produced duplicate <c>@key</c>s: the first render
/// succeeded and the first click threw <see cref="InvalidOperationException"/> out of the renderer.
/// The radio/select option lists (no <c>@key</c>) always survived the same input; these pin the
/// checkbox list to that same resilience, so each case must be clicked, not just rendered.
/// </summary>
public class DuplicateOptionValueTests : BunitContext
{
    // Active and Enabled share the underlying value 1 — legal C#, and Enum.GetValues yields one
    // entry per FIELD, so the option list contains the same value twice.
    enum AliasStatus
    {
        None = 0,
        Active = 1,
        Enabled = 1
    }

    class AliasModel
    {
        public List<AliasStatus> Statuses { get; set; } = [];
    }

    [Fact]
    public void EditCheckedEnumList_with_alias_members_renders_and_survives_a_click()
    {
        var model = new AliasModel();
        List<AliasStatus>? captured = null;
        Expression<Func<List<AliasStatus>>> field = () => model.Statuses;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<AliasStatus>>(0);
            b.AddAttribute(1, "Value", model.Statuses);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<AliasStatus>>(this, v =>
            {
                model.Statuses = v;
                captured = v;
            }));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        // One checkbox per enum FIELD (Enum.GetValues yields the aliased value twice), so two of the
        // three siblings carry the same value — the duplicate @key.
        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, boxes.Count);
        var ids = boxes.Select(x => x.GetAttribute("id")).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count()); // ToUniqueIds keeps the ids (and now the keys) distinct

        boxes[2].Change(true); // the aliased twin — the click that used to throw on the re-render

        Assert.NotNull(captured);
        Assert.Contains(AliasStatus.Active, captured);
    }

    [Fact]
    public void EditCheckedStringList_with_a_duplicate_option_renders_and_survives_a_click()
    {
        var model = new PersonModel { Tags = [] };
        List<string>? captured = null;
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v =>
            {
                model.Tags = v;
                captured = v;
            }));
            b.AddAttribute(3, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "a", "b" });
            b.CloseComponent();
        }));

        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, boxes.Count);
        // The ids stay distinct (ToUniqueIds), which is exactly what the @key now rides on.
        var ids = boxes.Select(x => x.GetAttribute("id")).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        boxes[1].Change(true); // second "a" — the duplicate-keyed sibling

        Assert.NotNull(captured);
        Assert.Contains("a", captured);
    }
}
