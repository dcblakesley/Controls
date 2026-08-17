using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for EditString's <c>Suggestions</c> parameter -- an open-vocabulary HTML
/// <c>&lt;datalist&gt;</c> of autofill hints, wired to the input via <c>list=</c>. Genuinely different
/// from <c>EditSelectSearch</c>/<c>EditMultiSelect</c> (closed vocabulary: the bound value must be one
/// of the supplied options) -- here any typed value is accepted, and the list is just a hint.
/// <para>
/// The load-bearing invariant these tests protect: a consumer could already hand-wire
/// <c>&lt;EditString list="myListId" /&gt;</c> plus their own <c>&lt;datalist id="myListId"&gt;</c>
/// before this feature existed (AttributeSplat.RestWith already splats an unmatched <c>list</c>
/// attribute onto the input). <see cref="EditString.Suggestions"/> being null must leave that path untouched --
/// the library's own <c>list=</c> frame has to be genuinely absent, not merely empty, or it would
/// stomp the consumer's splatted one.
/// </para>
/// </summary>
public class EditStringSuggestionsTests : BunitContext
{
    class SuggestionsModel
    {
        public string? Text { get; set; }
        public string? Other { get; set; }
    }

    // One row of the list case below: several instances of the SAME property name, which is what
    // makes AttributesHelper.GetId resolve one shared element id across all of them.
    class RowModel
    {
        public string? Name { get; set; }
    }

    IRenderedComponent<ContainerFragment> RenderOne(SuggestionsModel model,
        IEnumerable<string>? suggestions, bool isPassword = false) =>
        Render(WithForm(model, b =>
        {
            Expression<Func<string?>> field = () => model.Text;
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            if (suggestions is not null) b.AddAttribute(3, "Suggestions", suggestions);
            if (isPassword) b.AddAttribute(4, "IsPassword", true);
            b.CloseComponent();
        }));

    [Fact]
    public void Datalist_renders_with_the_given_options_and_the_input_list_points_at_it()
    {
        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, ["Apple", "Banana", "Cherry"]);

        var input = cut.Find("input.edit-string-input");
        var datalist = cut.Find("datalist");

        // The generated id is what list= actually points at -- not just "some id", the SAME one. The
        // id itself is a per-instance dl-{guid} (see SuggestionsListId), never derived from the field
        // name, so only its prefix is pinned here; the uniqueness contract has its own test below.
        Assert.StartsWith("dl-", datalist.Id);
        Assert.Equal(datalist.Id, input.GetAttribute("list"));
        Assert.Equal(
            new[] { "Apple", "Banana", "Cherry" },
            datalist.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
    }

    [Fact]
    public void No_datalist_or_list_attribute_when_Suggestions_is_unset()
    {
        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, suggestions: null);

        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("list"));
        Assert.Empty(cut.FindAll("datalist"));
    }

    [Fact]
    public void A_consumer_splatted_list_attribute_still_reaches_the_input_when_Suggestions_is_null()
    {
        // The pre-existing hand-wire path: a consumer's own list="..." is an unmatched attribute that
        // AttributeSplat.RestWith already forwards to the input. The library must not emit a competing
        // (null) list frame that could interfere with it.
        var model = new SuggestionsModel { Text = "hi" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "list", "myOwnListId");
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("myOwnListId", input.GetAttribute("list"));
        Assert.Empty(cut.FindAll("datalist")); // the library renders none of its own
    }

    [Fact]
    public void Empty_but_non_null_Suggestions_still_renders_the_list_attribute_and_an_empty_datalist()
    {
        // Deliberate choice: "Suggestions is set" (not "Suggestions has entries") is the on/off switch,
        // so a consumer binding a filtered list that transiently empties (e.g. mid-fetch) doesn't see
        // the list attribute flicker on and off.
        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, suggestions: []);

        var input = cut.Find("input.edit-string-input");
        var datalist = cut.Find("datalist");
        Assert.StartsWith("dl-", datalist.Id);
        Assert.Equal(datalist.Id, input.GetAttribute("list"));
        Assert.Empty(datalist.QuerySelectorAll("option"));
    }

    [Fact]
    public void Suggestions_are_suppressed_on_a_password_field()
    {
        // Browsers ignore `list` on type="password" outright, so wiring it there would be dead markup.
        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, ["hunter2"], isPassword: true);

        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("list"));
        Assert.Empty(cut.FindAll("datalist"));
    }

    [Fact]
    public void Two_instances_bound_to_DIFFERENT_properties_get_distinct_datalist_ids_correctly_cross_wired()
    {
        // Note the name: distinctness here would hold even under a field-name-derived id, because the
        // two properties differ. The load-bearing uniqueness case -- several instances of the SAME
        // property -- is the list test further down; this one only covers cross-wiring of the option
        // sets.
        var model = new SuggestionsModel { Text = "hi", Other = "there" };
        Expression<Func<string?>> textField = () => model.Text;
        Expression<Func<string?>> otherField = () => model.Other;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", textField);
            b.AddAttribute(3, "Suggestions", new List<string> { "Alice", "Bob" });
            b.CloseComponent();

            b.OpenComponent<EditString>(4);
            b.AddAttribute(5, "Value", model.Other);
            b.AddAttribute(6, "ValueExpression", otherField);
            b.AddAttribute(7, "Suggestions", new List<string> { "Carol", "Dave" });
            b.CloseComponent();
        }));

        var inputs = cut.FindAll("input.edit-string-input");
        var datalists = cut.FindAll("datalist");
        Assert.Equal(2, inputs.Count);
        Assert.Equal(2, datalists.Count);

        var ids = datalists.Select(d => d.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());

        Assert.Equal(inputs[0].GetAttribute("list"), datalists[0].Id);
        Assert.Equal(inputs[1].GetAttribute("list"), datalists[1].Id);

        // Cross-wiring: each instance's datalist carries only its OWN suggestions.
        Assert.Equal(
            new[] { "Alice", "Bob" },
            datalists[0].QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
        Assert.Equal(
            new[] { "Carol", "Dave" },
            datalists[1].QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
    }

    [Fact]
    public void Instances_bound_to_the_SAME_property_get_distinct_datalists_each_resolving_to_its_own()
    {
        // The list case, and the one the old two-instance test could not see: three rows all binding a
        // property named `Name`, so AttributesHelper.GetId resolves the SAME element id for every one
        // of them (the pre-existing id collision this test does NOT fix -- IdPrefix is the documented
        // escape hatch for that). A datalist id DERIVED from that element id inherits the collision,
        // and a browser resolves `list=` by getElementById -- first match in document order -- so rows
        // 1..N would all display ROW 0's suggestions while their own correct datalists sat unreachable
        // in the DOM. Silently wrong data, not a visible failure, which is why the id has to be unique
        // per component INSTANCE rather than per bound property.
        var rows = new[] { new RowModel(), new RowModel(), new RowModel() };
        var cut = Render(WithForm(rows, b =>
        {
            var seq = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                Expression<Func<string?>> field = () => row.Name;
                b.OpenComponent<EditString>(seq++);
                b.AddAttribute(seq++, "Value", row.Name);
                b.AddAttribute(seq++, "ValueExpression", field);
                b.AddAttribute(seq++, "Suggestions", new List<string> { $"row{i}-A", $"row{i}-B" });
                b.CloseComponent();
            }
        }));

        var inputs = cut.FindAll("input.edit-string-input");
        Assert.Equal(3, inputs.Count);
        Assert.Equal(3, cut.FindAll("datalist").Count);

        // Precondition of the bug: every input really does share one element id.
        Assert.Single(inputs.Select(i => i.Id).Distinct());

        var listIds = inputs.Select(i => i.GetAttribute("list")!).ToList();
        Assert.Equal(3, listIds.Distinct().Count());

        for (var i = 0; i < rows.Length; i++)
        {
            // Single() is the getElementById simulation: exactly one element carries this id, so
            // "the browser's first match" and "this row's own datalist" are provably the same node.
            var target = Assert.Single(cut.FindAll($"datalist#{listIds[i]}"));
            Assert.Equal(
                new[] { $"row{i}-A", $"row{i}-B" },
                target.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray());
        }
    }

    [Fact]
    public void The_datalist_id_is_stable_across_re_renders_of_the_same_instance()
    {
        // Uniqueness alone isn't enough: the id has to be generated ONCE per instance, not per render.
        // A per-render value would still pair correctly (both halves are emitted in one render) but
        // would rewrite two DOM attributes on every keystroke, and anything holding the id would drift.
        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, ["Apple", "Banana"]);
        var first = cut.Find("datalist").Id;

        cut.Find("input.edit-string-input").Input("hi there");

        Assert.Equal(first, cut.Find("datalist").Id);
        Assert.Equal(first, cut.Find("input.edit-string-input").GetAttribute("list"));
    }

    [Fact]
    public void The_Suggestions_sequence_is_re_enumerated_on_every_render()
    {
        // Not a defect -- rendering a sequence means walking it -- but it is the reason the parameter's
        // documented contract requires a STABLE, REPEATABLE sequence. A generator-backed or
        // side-effecting one silently renders something different on each pass, which this pins by
        // demonstration: a materialized List/array (what consumers should pass) is immune.
        var passes = 0;

        IEnumerable<string> Generated()
        {
            passes++;
            yield return $"pass{passes}";
        }

        var model = new SuggestionsModel { Text = "hi" };
        var cut = RenderOne(model, Generated());

        Assert.Equal(new[] { "pass1" }, Options(cut));

        cut.Find("input.edit-string-input").Input("hi there");

        Assert.Equal(new[] { "pass2" }, Options(cut));
    }

    static string?[] Options(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find("datalist").QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ToArray();

    [Fact]
    public void Suggestions_containing_html_special_characters_are_encoded_correctly()
    {
        var model = new SuggestionsModel { Text = "hi" };
        const string raw = "<b>Bold</b> & \"quoted\" 'ticked'";
        var cut = RenderOne(model, [raw]);

        var option = cut.Find("datalist option");
        // AngleSharp decodes the attribute back to the original text -- proves the value round-trips
        // rather than being interpreted as markup.
        Assert.Equal(raw, option.GetAttribute("value"));
        // No actual <b> element was created -- the tag characters never parsed as markup.
        Assert.Empty(cut.FindAll("datalist b"));
        Assert.Single(cut.FindAll("datalist option"));
    }
}
