using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Confirms the AntD 4.x parity batch's new <see cref="Select{TValue}"/> engine parameters
/// (<c>Loading</c>/<c>ShowArrow</c>, <c>FilterOption</c>, <c>EmptyContent</c>, <c>DropdownFooter</c>,
/// <c>Open</c>/<c>OpenChanged</c>) are actually forwarded through <see cref="EditSelectSearch{TValue}"/>
/// and <see cref="EditMultiSelect{TValue}"/> rather than only tested at the engine level directly.
/// Grouping (<see cref="SelectOption{TValue}.Group"/>) needs no wrapper wiring — it rides along on
/// the <c>Options</c> the wrappers already forward, so it isn't re-tested here.
/// </summary>
public class SelectWrapperForwardingTests : TestContext
{
    public SelectWrapperForwardingTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static List<SelectOption<Priority?>> PriorityOptions() =>
    [
        new(Priority.Low, "Low"),
        new(Priority.Medium, "Medium"),
    ];

    // ----- EditSelectSearch ---------------------------------------------------------------------

    [Fact]
    public void EditSelectSearch_forwards_Loading_and_ShowArrow()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "Loading", true);
            b.AddAttribute(5, "ShowArrow", false);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".wss-select-arrow .wss-icon-spin")); // Loading wins the arrow slot
        Assert.Equal("true", cut.Find(".wss-select").GetAttribute("aria-busy"));
    }

    [Fact]
    public void EditSelectSearch_forwards_FilterOption()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "FilterOption", (Func<string, SelectOption<Priority?>, bool>)((_, _) => true));
            b.CloseComponent();
        }));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").Input("zzz-matches-nothing");

        // The default Label.Contains would filter both out; the forwarded FilterOption keeps them.
        Assert.Equal(2, cut.FindAll(".wss-select-item-option").Count);
    }

    [Fact]
    public void EditSelectSearch_forwards_EmptyContent()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "EmptyContent", (RenderFragment)(rb => rb.AddContent(0, "Nothing matched")));
            b.CloseComponent();
        }));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").Input("zzz");

        Assert.Contains("Nothing matched", cut.Find(".wss-select-item-empty").TextContent);
    }

    [Fact]
    public void EditSelectSearch_forwards_DropdownFooter()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "DropdownFooter", (RenderFragment)(rb => rb.AddContent(0, "Footer content")));
            b.CloseComponent();
        }));

        cut.Find(".wss-select").Click();

        Assert.Contains("Footer content", cut.Find(".wss-select-dropdown-footer").TextContent);
    }

    [Fact]
    public void EditSelectSearch_forwards_controlled_Open_and_OpenChanged()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var raised = new List<bool>();
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "Open", false);
            b.AddAttribute(5, "OpenChanged", EventCallback.Factory.Create<bool>(this, v => raised.Add(v)));
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("[role=listbox]"));
        cut.Find(".wss-select").Click();
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Contains(true, raised);
    }

    // ----- EditMultiSelect ------------------------------------------------------------------------

    // List controls tolerate a null EditContext (see PerfGuardTests), so these render standalone.

    [Fact]
    public void EditMultiSelect_forwards_Loading_and_ShowArrow()
    {
        var model = new PersonModel { FavoriteColors = [Color.Red] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = RenderComponent<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red"), new SelectOption<Color>(Color.Blue, "Blue")])
            .Add(x => x.Loading, true)
            .Add(x => x.ShowArrow, false));

        Assert.NotEmpty(cut.FindAll(".wss-select-arrow .wss-icon-spin"));
        Assert.Equal("true", cut.Find(".wss-select").GetAttribute("aria-busy"));
    }

    [Fact]
    public void EditMultiSelect_forwards_FilterOption()
    {
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = RenderComponent<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red"), new SelectOption<Color>(Color.Blue, "Blue")])
            .Add(x => x.FilterOption, (Func<string, SelectOption<Color>, bool>)((_, _) => true)));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").Input("zzz-matches-nothing");

        Assert.Equal(2, cut.FindAll(".wss-select-item-option").Count);
    }

    [Fact]
    public void EditMultiSelect_forwards_EmptyContent_and_DropdownFooter()
    {
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = RenderComponent<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red")])
            .Add(x => x.EmptyContent, (RenderFragment)(rb => rb.AddContent(0, "Nothing matched")))
            .Add(x => x.DropdownFooter, (RenderFragment)(rb => rb.AddContent(0, "Footer content"))));

        cut.Find(".wss-select").Click();
        Assert.Contains("Footer content", cut.Find(".wss-select-dropdown-footer").TextContent);

        cut.Find("input.wss-select-selection-search-input").Input("zzz");
        Assert.Contains("Nothing matched", cut.Find(".wss-select-item-empty").TextContent);
    }

    [Fact]
    public void EditMultiSelect_forwards_controlled_Open_and_OpenChanged()
    {
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var raised = new List<bool>();
        var cut = RenderComponent<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red")])
            .Add(x => x.Open, false)
            .Add(x => x.OpenChanged, EventCallback.Factory.Create<bool>(this, v => raised.Add(v))));

        Assert.Empty(cut.FindAll("[role=listbox]"));
        cut.Find(".wss-select").Click();
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Contains(true, raised);
    }
}
