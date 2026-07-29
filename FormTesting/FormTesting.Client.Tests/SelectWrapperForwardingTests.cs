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
public class SelectWrapperForwardingTests : BunitContext
{
    public SelectWrapperForwardingTests() => JSInterop.Mode = JSRuntimeMode.Loose;

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
        var cut = Render<EditMultiSelect<Color>>(p => p
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
        var cut = Render<EditMultiSelect<Color>>(p => p
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
        var cut = Render<EditMultiSelect<Color>>(p => p
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
        var cut = Render<EditMultiSelect<Color>>(p => p
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

    [Fact]
    public void EditMultiSelect_forwards_Variant()
    {
        // EditSelectSearch already declared+forwarded Variant; EditMultiSelect omitted it, so Pill and
        // Borderless were unreachable for multiple/tags even though the engine applies them modelessly.
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;

        var outlined = Render<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red")]));
        Assert.DoesNotContain("wss-select-pill", outlined.Find(".wss-select").ClassList);

        var pill = Render<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red")])
            .Add(x => x.Variant, SelectVariant.Pill));
        Assert.Contains("wss-select-pill", pill.Find(".wss-select").ClassList);
    }

    [Fact]
    public void EditMultiSelect_renders_unmatched_attributes_on_the_wrapper_and_still_routes_class_to_the_engine()
    {
        // EditControlListBase captures unmatched values, but EditMultiSelect never splatted them, so
        // style/data-*/title on the control were silently dropped. They land on the outer
        // edit-control-wrapper (not the engine's wrapper, whose inline style is JS-owned); `class` keeps
        // its single existing channel -- FieldCssClass -> the engine's CssClass.
        var model = new PersonModel { FavoriteColors = [] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render<EditMultiSelect<Color>>(p => p
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Red, "Red")])
            .AddUnmatched("style", "margin-top:4px")
            .AddUnmatched("data-test", "multi")
            .AddUnmatched("class", "my-custom-class"));

        var wrapper = cut.Find(".edit-control-wrapper");
        Assert.Equal("margin-top:4px", wrapper.GetAttribute("style"));
        Assert.Equal("multi", wrapper.GetAttribute("data-test"));
        // The consumer's class must not be duplicated onto the wrapper -- it belongs to the engine.
        Assert.DoesNotContain("my-custom-class", wrapper.ClassList);
        Assert.Contains("my-custom-class", cut.Find(".wss-select").ClassList);
    }
}
