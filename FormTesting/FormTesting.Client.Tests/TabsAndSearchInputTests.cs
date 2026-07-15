using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the <see cref="Tabs"/>/<see cref="Tab"/> strip and the <see cref="SearchInput"/>
/// UI-kit controls: selection binding, count chips, ARIA wiring, keyboard navigation, panes, and
/// the search commit paths.
/// </summary>
public class TabsAndSearchInputTests : TestContext
{
    public TabsAndSearchInputTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate FocusAsync

    IRenderedComponent<Tabs> RenderTabs(
        string? activeKey = null,
        EventCallback<string?>? changed = null,
        bool withPanes = false,
        bool middleDisabled = false) =>
        RenderComponent<Tabs>(p =>
        {
            if (activeKey is not null) p.Add(t => t.ActiveKey, activeKey);
            if (changed is not null) p.Add(t => t.ActiveKeyChanged, changed.Value);
            p.AddChildContent<Tab>(tp =>
            {
                tp.Add(c => c.Key, "overdue").Add(c => c.Title, "Overdue").Add(c => c.Count, 12);
                if (withPanes) tp.Add(c => c.ChildContent, b => b.AddContent(0, "Overdue pane"));
            });
            p.AddChildContent<Tab>(tp =>
            {
                tp.Add(c => c.Key, "missing").Add(c => c.Title, "Missing Estimations").Add(c => c.Count, 12)
                  .Add(c => c.Disabled, middleDisabled);
                if (withPanes) tp.Add(c => c.ChildContent, b => b.AddContent(0, "Missing pane"));
            });
            p.AddChildContent<Tab>(tp =>
            {
                tp.Add(c => c.Key, "other").Add(c => c.Title, "Other Active").Add(c => c.Count, 5);
                if (withPanes) tp.Add(c => c.ChildContent, b => b.AddContent(0, "Other pane"));
            });
        });

    [Fact]
    public void Renders_tabs_with_count_chips_and_activates_the_first_by_default()
    {
        var cut = RenderTabs();

        var tabs = cut.FindAll("[role=tab]");
        Assert.Equal(3, tabs.Count);
        Assert.Equal("true", tabs[0].GetAttribute("aria-selected"));
        Assert.Contains("wss-tabs-tab-active", tabs[0].ClassList);
        Assert.Equal("12", cut.FindAll(".wss-tabs-count")[0].TextContent);
        // Roving tabindex: exactly the active tab is the strip's Tab stop.
        Assert.Equal("0", tabs[0].GetAttribute("tabindex"));
        Assert.Equal("-1", tabs[1].GetAttribute("tabindex"));
        // A bare filter strip renders no panel.
        Assert.Empty(cut.FindAll("[role=tabpanel]"));
    }

    [Fact]
    public void Click_selects_and_raises_the_bound_key()
    {
        string? selected = null;
        var cut = RenderTabs(changed: EventCallback.Factory.Create<string?>(this, v => selected = v));

        cut.FindAll("[role=tab]")[2].Click();

        Assert.Equal("other", selected);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[2].ClassList);
    }

    [Fact]
    public void Bound_ActiveKey_wins_and_a_disabled_tab_cannot_activate()
    {
        var cut = RenderTabs(activeKey: "missing");
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[1].ClassList);

        var disabled = RenderTabs(middleDisabled: true);
        var middle = disabled.FindAll("[role=tab]")[1];
        Assert.True(middle.HasAttribute("disabled"));
    }

    [Fact]
    public void Arrow_keys_select_the_neighboring_enabled_tab_and_wrap()
    {
        string? selected = null;
        var cut = RenderTabs(
            changed: EventCallback.Factory.Create<string?>(this, v => selected = v),
            middleDisabled: true);

        // From the first tab, ArrowRight skips the disabled middle to "other".
        cut.FindAll("[role=tab]")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("other", selected);

        // ...and ArrowRight from the last enabled tab wraps back to the first.
        cut.FindAll("[role=tab]")[2].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("overdue", selected);
    }

    [Fact]
    public void Active_pane_renders_with_the_tabpanel_wiring()
    {
        var cut = RenderTabs(activeKey: "missing", withPanes: true);

        var panel = cut.Find("[role=tabpanel]");
        Assert.Contains("Missing pane", panel.TextContent);
        var activeTab = cut.FindAll("[role=tab]")[1];
        Assert.Equal(panel.GetAttribute("aria-labelledby"), activeTab.GetAttribute("id"));
        Assert.Equal(panel.GetAttribute("id"), activeTab.GetAttribute("aria-controls"));
    }

    // ----- SearchInput -------------------------------------------------------

    [Fact]
    public void SearchInput_renders_the_addon_and_binds_per_keystroke()
    {
        string? value = null;
        var cut = RenderComponent<SearchInput>(p => p
            .Add(s => s.AddonLabel, "POs")
            .Add(s => s.ValueChanged, (string? v) => value = v));

        Assert.Equal("POs", cut.Find(".wss-search-addon").TextContent.Trim());
        Assert.Equal("POs", cut.Find(".wss-search-input").GetAttribute("aria-label"));

        cut.Find(".wss-search-input").Input("89990");
        Assert.Equal("89990", value);
    }

    [Fact]
    public void SearchInput_raises_OnSearch_on_enter_and_on_the_button()
    {
        var searches = new List<string?>();
        var cut = RenderComponent<SearchInput>(p => p
            .Add(s => s.Value, "abc")
            .Add(s => s.OnSearch, (string? v) => searches.Add(v)));

        cut.Find(".wss-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.Find(".wss-search-btn").Click();

        Assert.Equal(["abc", "abc"], searches);
    }

    [Fact]
    public void SearchInput_without_addon_renders_no_chip_and_disabled_blocks_search()
    {
        var fired = false;
        var cut = RenderComponent<SearchInput>(p => p
            .Add(s => s.Disabled, true)
            .Add(s => s.OnSearch, (string? _) => fired = true));

        Assert.Empty(cut.FindAll(".wss-search-addon"));
        Assert.True(cut.Find(".wss-search-input").HasAttribute("disabled"));
        cut.Find(".wss-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.False(fired);
    }
}
