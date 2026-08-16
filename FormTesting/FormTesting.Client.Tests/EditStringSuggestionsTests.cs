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

        // The generated id (dl-{id}, matching the count-{id}/desc-{id}/lbl-{id} shape) is what list=
        // actually points at -- not just "some id", the SAME one.
        Assert.Equal("dl-Text", datalist.Id);
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
        Assert.Equal("dl-Text", input.GetAttribute("list"));
        var datalist = cut.Find("datalist");
        Assert.Equal("dl-Text", datalist.Id);
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
    public void Two_instances_on_one_page_get_distinct_datalist_ids_correctly_cross_wired()
    {
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

        Assert.Equal("dl-Text", inputs[0].GetAttribute("list"));
        Assert.Equal("dl-Other", inputs[1].GetAttribute("list"));
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
