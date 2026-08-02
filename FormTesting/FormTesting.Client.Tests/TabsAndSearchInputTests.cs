using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the <see cref="Tabs"/>/<see cref="Tab"/> strip and the <see cref="SearchInput"/>
/// UI-kit controls: selection binding, count chips, ARIA wiring, keyboard navigation, panes, and
/// the search commit paths.
/// </summary>
public class TabsAndSearchInputTests : BunitContext
{
    public TabsAndSearchInputTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate FocusAsync

    IRenderedComponent<Tabs> RenderTabs(
        string? activeKey = null,
        EventCallback<string?>? changed = null,
        bool withPanes = false,
        bool middleDisabled = false) =>
        Render<Tabs>(p =>
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
        // Each Tab renders its own button, so pin that they still land as direct children of the
        // tablist (and nowhere else): consumers' CSS and the visual baselines depend on that shape.
        Assert.Equal(3, cut.FindAll(".wss-tabs > .wss-tabs-nav > button.wss-tabs-tab").Count);
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
    public void Selecting_a_tab_updates_every_other_button_not_just_the_clicked_one()
    {
        // A tab renders its own button, and "which tab is active" is not one of its parameters --
        // Blazor's diff cannot see that the tab the user just left has to drop its underline,
        // aria-selected and Tab stop, and the click only re-renders the tab that handled it. The
        // strip pushes the change to every live tab instead; without that push this strip would
        // render two active tabs and two Tab stops. Deliberately unbound (no ActiveKeyChanged), the
        // case where nothing else re-renders the strip either.
        var cut = RenderTabs();
        Assert.Equal("0", cut.FindAll("[role=tab]")[0].GetAttribute("tabindex"));

        cut.FindAll("[role=tab]")[2].Click();

        var tabs = cut.FindAll("[role=tab]");
        Assert.DoesNotContain("wss-tabs-tab-active", tabs[0].ClassList);
        Assert.Equal("false", tabs[0].GetAttribute("aria-selected"));
        Assert.Equal("-1", tabs[0].GetAttribute("tabindex"));
        Assert.Contains("wss-tabs-tab-active", tabs[2].ClassList);
        Assert.Equal("true", tabs[2].GetAttribute("aria-selected"));
        Assert.Equal("0", tabs[2].GetAttribute("tabindex"));
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
    public void Home_and_End_keys_no_longer_move_the_active_tab()
    {
        // Home/End were removed from the key switch (they fall through to the null branch and
        // return early) because Blazor has no per-key preventDefault — handling them would still
        // let the browser scroll the page out from under the corrective FocusAsync.
        string? selected = null;
        var cut = RenderTabs(changed: EventCallback.Factory.Create<string?>(this, v => selected = v));

        cut.FindAll("[role=tab]")[0].KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Null(selected);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[0].ClassList);

        cut.FindAll("[role=tab]")[0].KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Null(selected);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[0].ClassList);
    }

    [Fact]
    public void Existing_tab_Count_change_renders_on_the_same_pass_instead_of_one_behind()
    {
        var count = 12;
        RenderFragment Children() => builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "overdue");
            builder.AddAttribute(2, "Title", "Overdue");
            builder.AddAttribute(3, "Count", count);
            builder.CloseComponent();

            builder.OpenComponent<Tab>(4);
            builder.AddAttribute(5, "Key", "other");
            builder.AddAttribute(6, "Title", "Other");
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, Children()));
        Assert.Equal("12", cut.Find(".wss-tabs-count").TextContent);

        // The auditor's exact repro shape: the parent re-renders with a changed Count on an
        // already-registered tab (no tab added or removed). The count chip is now rendered by the
        // tab itself, so the parameter change that carries it also re-renders it -- and, unlike a
        // key/disabled change, it needs no corrective render of the strip at all.
        var rendersBefore = cut.RenderCount;
        count = 34;
        cut.Render(p => p.Add(t => t.ChildContent, Children()));

        Assert.Equal("34", cut.Find(".wss-tabs-count").TextContent);
        // Bounded, not a runaway loop -- an unguarded notification would re-trigger on every
        // subsequent pass, since ChildContent is a new delegate each time. (RenderCount also counts
        // the tabs' own renders below this component, hence bounds larger than the pass count.)
        Assert.True(cut.RenderCount - rendersBefore <= 4, $"render delta {cut.RenderCount - rendersBefore}");
    }

    [Fact]
    public void Existing_tab_Disabled_flip_renders_on_the_same_pass_instead_of_one_behind()
    {
        var disabled = false;
        RenderFragment Children() => builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "overdue");
            builder.AddAttribute(2, "Title", "Overdue");
            builder.AddAttribute(3, "Disabled", disabled);
            builder.CloseComponent();

            builder.OpenComponent<Tab>(4);
            builder.AddAttribute(5, "Key", "other");
            builder.AddAttribute(6, "Title", "Other");
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, Children()));
        Assert.False(cut.FindAll("[role=tab]")[0].HasAttribute("disabled"));

        var rendersBefore = cut.RenderCount;
        disabled = true;
        cut.Render(p => p.Add(t => t.ChildContent, Children()));

        Assert.True(cut.FindAll("[role=tab]")[0].HasAttribute("disabled"));
        // Disabling the active tab moves the selection, which the OTHER tab's button has to show:
        // the strip resolves the new active tab before this tab's own OnParametersSet has run, so
        // it settles over two passes (NotifyTabChanged, then the push to the buttons). Bounded, and
        // settled -- an identical re-render afterwards costs nothing but the pass-through.
        Assert.True(cut.RenderCount - rendersBefore <= 8, $"render delta {cut.RenderCount - rendersBefore}");
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[1].ClassList);

        rendersBefore = cut.RenderCount;
        cut.Render(p => p.Add(t => t.ChildContent, Children()));
        Assert.True(cut.RenderCount - rendersBefore <= 2, $"idle render delta {cut.RenderCount - rendersBefore}");
    }

    [Fact]
    public void Existing_tab_Key_change_renders_on_the_same_pass_instead_of_one_behind()
    {
        // Key feeds the button id, aria-controls, and the ActiveKey match that drives the active
        // underline/aria-selected/tab stop — a re-keyed tab whose display text is unchanged must
        // still trigger the corrective render, or the new ActiveKey matches nothing and the
        // active state silently falls back to the first enabled tab.
        var key = "draft";
        RenderFragment Children() => builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "all");
            builder.AddAttribute(2, "Title", "All");
            builder.CloseComponent();

            builder.OpenComponent<Tab>(4);
            builder.AddAttribute(5, "Key", key);
            builder.AddAttribute(6, "Title", "Filtered");
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p => p
            .Add(t => t.ChildContent, Children())
            .Add(t => t.ActiveKey, "draft"));
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[1].ClassList);

        var rendersBefore = cut.RenderCount;
        key = "saved";
        cut.Render(p => p
            .Add(t => t.ChildContent, Children())
            .Add(t => t.ActiveKey, "saved"));

        var rekeyed = cut.FindAll("[role=tab]")[1];
        Assert.Contains("wss-tabs-tab-active", rekeyed.ClassList);
        Assert.EndsWith("-tab-saved", rekeyed.GetAttribute("id"));
        // Two corrective passes at worst (the strip resolves "saved" against the old keys first,
        // then re-resolves once the re-keyed tab has reported), and then it settles.
        Assert.True(cut.RenderCount - rendersBefore <= 10, $"render delta {cut.RenderCount - rendersBefore}");

        rendersBefore = cut.RenderCount;
        cut.Render(p => p
            .Add(t => t.ChildContent, Children())
            .Add(t => t.ActiveKey, "saved"));
        Assert.True(cut.RenderCount - rendersBefore <= 2, $"idle render delta {cut.RenderCount - rendersBefore}");
    }

    // ----- ActiveKey fallback notification -------------------------------------

    // Two tabs where the second can be removed or disabled from the outside, built through the raw
    // builder so the consumer's markup really drops it (the parameter-based helper above can't).
    static RenderFragment TwoTabs(bool showSecond = true, bool secondDisabled = false) => builder =>
    {
        builder.OpenComponent<Tab>(0);
        builder.AddAttribute(1, "Key", "all");
        builder.AddAttribute(2, "Title", "All");
        builder.CloseComponent();

        if (!showSecond) return;
        builder.OpenComponent<Tab>(3);
        builder.AddAttribute(4, "Key", "filtered");
        builder.AddAttribute(5, "Title", "Filtered");
        builder.AddAttribute(6, "Disabled", secondDisabled);
        builder.CloseComponent();
    };

    [Fact]
    public void Removing_the_active_tab_reports_the_fallback_key()
    {
        // ActiveTab silently falls back to the first enabled tab, and only SelectAsync used to raise
        // ActiveKeyChanged -- so a bound ActiveKey kept naming a tab that is no longer in the strip and
        // the consumer's own pane/filter state disagreed with the highlighted tab until the next click.
        var keys = new List<string?>();
        var cut = Render<Tabs>(p => p
            .Add(t => t.ChildContent, TwoTabs())
            .Add(t => t.ActiveKey, "filtered")
            .Add(t => t.ActiveKeyChanged, EventCallback.Factory.Create<string?>(this, k => keys.Add(k))));
        Assert.Empty(keys); // nothing changed: "filtered" is present, enabled, and active

        cut.Render(p => p.Add(t => t.ChildContent, TwoTabs(showSecond: false)));

        Assert.Equal(["all"], keys);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[0].ClassList);
    }

    [Fact]
    public void Disabling_the_active_tab_reports_the_fallback_key_exactly_once()
    {
        var keys = new List<string?>();
        var cut = Render<Tabs>(p => p
            .Add(t => t.ChildContent, TwoTabs())
            .Add(t => t.ActiveKey, "filtered")
            .Add(t => t.ActiveKeyChanged, EventCallback.Factory.Create<string?>(this, k => keys.Add(k))));

        cut.Render(p => p.Add(t => t.ChildContent, TwoTabs(secondDisabled: true)));

        // Once, not once per render: this test's consumer ignores the new key (ActiveKey stays
        // "filtered"), and re-notifying an unchanged fallback would loop forever, since
        // EventCallback.InvokeAsync re-renders the parent that would notify again.
        Assert.Equal(["all"], keys);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[0].ClassList);
        Assert.True(cut.FindAll("[role=tab]")[1].HasAttribute("disabled"));
    }

    [Fact]
    public void An_ActiveKey_naming_a_disabled_tab_reports_the_fallback_on_the_first_render()
    {
        var keys = new List<string?>();
        var cut = RenderTabs(
            activeKey: "missing",
            changed: EventCallback.Factory.Create<string?>(this, k => keys.Add(k)),
            middleDisabled: true);

        Assert.Equal(["overdue"], keys);
        Assert.Contains("wss-tabs-tab-active", cut.FindAll("[role=tab]")[0].ClassList);
    }

    [Fact]
    public void A_resolvable_or_unset_ActiveKey_never_reports_a_fallback()
    {
        var keys = new List<string?>();
        var changed = EventCallback.Factory.Create<string?>(this, k => keys.Add(k));

        // Resolvable: the requested tab is present and enabled, through re-renders as well.
        var bound = RenderTabs(activeKey: "missing", changed: changed);
        bound.Render(p => p.Add(t => t.TablistLabel, "Filters"));
        Assert.Empty(keys);

        // Unset: null ActiveKey is the documented "activate the first enabled tab", not a desync --
        // reporting it would populate a consumer's deliberately-null bound field on first render.
        RenderTabs(changed: changed);
        Assert.Empty(keys);
    }

    // ----- Declaration order across parameter-skipped siblings ------------------

    // Three content-less tabs (the bare filter-strip shape), the first two conditional. Every
    // parameter is a string, so Blazor's diff skips SetParametersAsync on any tab whose own
    // parameters didn't change -- those tabs never re-register, and the strip has to reconstruct
    // their position without help from them.
    static RenderFragment ConditionalLeadingTabs(bool showFirst, bool showSecond) => builder =>
    {
        if (showFirst)
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "new");
            builder.AddAttribute(2, "Title", "New");
            builder.CloseComponent();
        }

        if (showSecond)
        {
            builder.OpenComponent<Tab>(3);
            builder.AddAttribute(4, "Key", "mid");
            builder.AddAttribute(5, "Title", "Mid");
            builder.CloseComponent();
        }

        builder.OpenComponent<Tab>(6);
        builder.AddAttribute(7, "Key", "a");
        builder.AddAttribute(8, "Title", "A");
        builder.CloseComponent();

        builder.OpenComponent<Tab>(9);
        builder.AddAttribute(10, "Key", "b");
        builder.AddAttribute(11, "Title", "B");
        builder.CloseComponent();
    };

    [Fact]
    public void A_tab_shown_before_parameter_skipped_siblings_lands_in_its_declared_position()
    {
        // The original defect: the strip rendered the nav buttons itself, from a list it rebuilt
        // from child registrations, and re-inserted each skipped tab at its OLD index -- which
        // pushes a newly declared FIRST tab past every one of them, permanently, because a skipped
        // tab never re-registers. Declared [new, a, b] rendered [a, b, new]. Each tab now renders
        // its own button, so the position is the render-tree diff's business and no list is
        // consulted to place it.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));

        string[] Titles() => cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()).ToArray();
        Assert.Equal(["A", "B"], Titles());

        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, false)));
        Assert.Equal(["New", "A", "B"], Titles());

        // A second insertion, this time between the newcomer and the still-skipped siblings.
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, true)));
        Assert.Equal(["New", "Mid", "A", "B"], Titles());

        // ...and hiding them again returns the strip to the remaining declared order.
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));
        Assert.Equal(["A", "B"], Titles());
    }

    [Fact]
    public void The_keyboard_order_follows_a_tab_inserted_before_parameter_skipped_siblings()
    {
        // The rendered order is the diff's, but arrow navigation walks a list the strip keeps, and
        // only tabs that re-registered this pass report their position. Here NOTHING re-registers
        // except the newcomer (every sibling's parameters are unchanged strings), so its position in
        // that list is genuinely not in the data and it is appended -- see Tabs.ResolveOrder. What
        // must still hold is that the list is that rotation and nothing worse: the surviving tabs
        // keep their relative order, so arrow navigation, which is cyclic, still walks the strip in
        // rendered order in both directions.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, false)));

        Assert.Equal(["New", "A", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        var strip = RenderedTabs(cut.Instance).Select(t => t.Key).ToArray();
        Assert.True(IsRotationOf(strip, ["new", "a", "b"]), $"keyboard order was [{string.Join(",", strip)}]");

        // Walking with the arrows -- always from the tab that currently holds the Tab stop, which is
        // the only button the keyboard can reach -- steps to the next RENDERED button each time, all
        // the way round, and back the other way.
        int ActiveIndex() => cut.FindAll(".wss-tabs-tab").ToList()
            .FindIndex(e => e.ClassList.Contains("wss-tabs-tab-active"));

        for (var step = 0; step < 3; step++)
        {
            var from = ActiveIndex();
            cut.FindAll(".wss-tabs-tab")[from].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
            Assert.Equal((from + 1) % 3, ActiveIndex());
        }

        for (var step = 0; step < 3; step++)
        {
            var from = ActiveIndex();
            cut.FindAll(".wss-tabs-tab")[from].KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
            Assert.Equal((from + 2) % 3, ActiveIndex());
        }
    }

    static bool IsRotationOf(IReadOnlyList<string> actual, IReadOnlyList<string> expected) =>
        actual.Count == expected.Count &&
        Enumerable.Range(0, expected.Count).Any(offset =>
            Enumerable.Range(0, expected.Count).All(i => actual[i] == expected[(i + offset) % expected.Count]));

    [Fact]
    public void A_tab_inserted_beside_a_sibling_that_re_registers_lands_exactly_in_the_keyboard_order()
    {
        // The ambiguity above only exists while every sibling is skipped. As soon as ONE sibling
        // re-registers in the same pass (here because it carries pane content, so its RenderFragment
        // parameter is a fresh delegate every render), it anchors the newcomer and the strip's list
        // is the declared order exactly -- no rotation.
        static RenderFragment Tabs(bool showFirst) => builder =>
        {
            if (showFirst)
            {
                builder.OpenComponent<Tab>(0);
                builder.AddAttribute(1, "Key", "new");
                builder.AddAttribute(2, "Title", "New");
                builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane new")));
                builder.CloseComponent();
            }

            builder.OpenComponent<Tab>(4);
            builder.AddAttribute(5, "Key", "a");
            builder.AddAttribute(6, "Title", "A");
            builder.AddAttribute(7, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane a")));
            builder.CloseComponent();

            builder.OpenComponent<Tab>(8);
            builder.AddAttribute(9, "Key", "b");
            builder.AddAttribute(10, "Title", "B");
            builder.AddAttribute(11, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane b")));
            builder.CloseComponent();
        };

        var cut = Render<Controls.Tabs>(p => p.Add(t => t.ChildContent, Tabs(false)));
        cut.Render(p => p.Add(t => t.ChildContent, Tabs(true)));

        Assert.Equal(["New", "A", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["new", "a", "b"], RenderedTabs(cut.Instance).Select(t => t.Key));
    }

    // Tabs.ButtonRef is internal (no InternalsVisibleTo), and the rendered tab set is private -- both
    // are read here because the defect below is invisible in the DOM: the markup is completely correct
    // and only the captured element references are wrong.
    static List<Tab> RenderedTabs(Tabs tabs) =>
        (List<Tab>)typeof(Tabs).GetField("_tabs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tabs)!;

    static ElementReference ButtonRefOf(Tab tab) =>
        (ElementReference)typeof(Tab).GetField("ButtonRef", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(tab)!;

    [Fact]
    public void Every_tab_still_has_a_captured_button_reference_after_an_insertion()
    {
        // Blazor re-runs an element-reference capture only for an element it CREATES, never for one
        // it retains -- which is how the previous design lost every ButtonRef: it recovered the
        // declared order by rebuilding the Tab children while the nav buttons, living in the strip's
        // own render tree, were retained. Each button is now declared in the same render tree as the
        // capture that points at it, so the two are created and destroyed together and cannot come
        // apart. The defect is invisible in the DOM (the markup was completely correct), which is
        // why this reads the references directly.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));
        Assert.All(RenderedTabs(cut.Instance), t => Assert.NotNull(ButtonRefOf(t).Id));

        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, false)));

        var unset = RenderedTabs(cut.Instance).Where(t => ButtonRefOf(t).Id is null).Select(t => t.Key).ToArray();
        Assert.True(unset.Length == 0, $"tabs with no captured ButtonRef after the insertion: {string.Join(",", unset)}");
    }

    [Fact]
    public void Arrow_navigation_after_an_insertion_still_moves_DOM_focus()
    {
        // The observable half of the same defect: FocusAsync on an uncaptured ElementReference throws
        // before it ever reaches the renderer, so no focus call was issued at all -- the roving
        // tabindex then pointed at a button the browser had not focused, the next arrow key re-fired
        // from the old button, and Tab left the strip entirely.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, false)));

        cut.FindAll(".wss-tabs-tab")[0].KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("A", cut.Find(".wss-tabs-tab-active .wss-tabs-label").TextContent.Trim());
        Assert.NotEmpty(JSInterop.Invocations["Blazor._internal.domWrapper.focus"]);
    }

    // A consumer component declared alongside the tabs, counting its own construction/disposal and
    // holding instance state that only survives if the instance itself does.
    sealed class StatefulSibling : ComponentBase, IDisposable
    {
        internal static int Constructed;
        internal static int Disposed;

        readonly int _instanceNumber;

        public StatefulSibling() => _instanceNumber = ++Constructed;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "stateful-sibling");
            builder.AddContent(2, _instanceNumber);
            builder.CloseElement();
        }

        public void Dispose() => Disposed++;
    }

    static RenderFragment TabsWithStatefulSibling(bool showFirst) => builder =>
    {
        if (showFirst)
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "new");
            builder.AddAttribute(2, "Title", "New");
            builder.CloseComponent();
        }

        builder.OpenComponent<Tab>(3);
        builder.AddAttribute(4, "Key", "a");
        builder.AddAttribute(5, "Title", "A");
        builder.CloseComponent();

        builder.OpenComponent<StatefulSibling>(6);
        builder.CloseComponent();
    };

    [Fact]
    public void A_component_declared_inside_Tabs_keeps_its_state_across_a_structural_insertion()
    {
        // The previous design recovered the declared order by bumping a generation @key on the
        // ChildContent CascadingValue, which tore down and reconstructed the WHOLE fragment -- so a
        // non-Tab component a consumer had declared inside <Tabs> lost its instance state (cached
        // lookups, element references, subscriptions, timers) on every structural insertion. That
        // was a documented limitation of Tabs.ChildContent; nothing rebuilds the fragment any more,
        // so this pins that the limitation is gone -- and that the bounds it used to carry (ordinary
        // re-renders and removals never disturb the subtree) still hold.
        StatefulSibling.Constructed = 0;
        StatefulSibling.Disposed = 0;

        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(false)));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);

        // Ordinary re-render with the same structure.
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(false)));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);

        // Insertion before a parameter-skipped sibling -- the case that used to force the rebuild.
        // The newcomer still lands in its declared position, and the consumer's component is the
        // same live instance it was before (same instance number, never disposed).
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(true)));
        Assert.Equal(["New", "A"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);

        // Removal: likewise untouched.
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(false)));
        Assert.Equal(["A"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);
    }

    // A pane whose content counts how many times it is instantiated: the strip renders ChildContent
    // once (into the tablist), so a Tab's pane must be executed only by the panel below it.
    sealed class CountingPane : ComponentBase
    {
        internal static int Constructed;

        public CountingPane() => Constructed++;

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "counting-pane");
            builder.CloseElement();
        }
    }

    [Fact]
    public void The_active_pane_is_instantiated_once_and_only_the_active_one_renders()
    {
        // A tab renders its own button but never its own pane: the pane belongs to the single
        // tabpanel below the strip. A design that walked ChildContent twice (once for buttons, once
        // for panes) would construct everything declared inside <Tabs> twice over -- doubling the
        // consumer's side effects and rendering their markup in both places.
        CountingPane.Constructed = 0;
        static RenderFragment Pane() => builder =>
        {
            builder.OpenComponent<CountingPane>(0);
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p =>
        {
            p.AddChildContent<Tab>(tp => tp.Add(c => c.Key, "a").Add(c => c.Title, "A").Add(c => c.ChildContent, Pane()));
            p.AddChildContent<Tab>(tp => tp.Add(c => c.Key, "b").Add(c => c.Title, "B").Add(c => c.ChildContent, Pane()));
        });

        Assert.Equal(1, CountingPane.Constructed);                        // the active tab's pane only
        Assert.Single(cut.FindAll(".counting-pane"));
        Assert.Single(cut.FindAll("[role=tabpanel] .counting-pane"));     // in the panel, not the tablist
        Assert.Empty(cut.FindAll(".wss-tabs-nav .counting-pane"));
    }

    [Fact]
    public void TabBarExtraContent_renders_beside_the_strip_only_when_set()
    {
        var plain = RenderTabs();
        Assert.Empty(plain.FindAll(".wss-tabs-nav-wrapper"));
        Assert.NotNull(plain.Find(".wss-tabs-nav"));

        var withExtra = Render<Tabs>(p =>
        {
            p.Add(t => t.TabBarExtraContent, b => b.AddContent(0, "Extra action"));
            p.AddChildContent<Tab>(tp => tp.Add(c => c.Key, "a").Add(c => c.Title, "A"));
        });
        Assert.Contains("Extra action", withExtra.Find(".wss-tabs-nav-extra").TextContent);
        Assert.NotNull(withExtra.Find(".wss-tabs-nav-wrapper .wss-tabs-nav"));
    }

    [Fact]
    public void Centered_adds_the_nav_modifier_class_only_when_set()
    {
        var plain = RenderTabs();
        Assert.DoesNotContain("wss-tabs-nav-centered", plain.Find(".wss-tabs-nav").ClassList);

        var centered = Render<Tabs>(p =>
        {
            p.Add(t => t.Centered, true);
            p.AddChildContent<Tab>(tp => tp.Add(c => c.Key, "a").Add(c => c.Title, "A"));
        });
        Assert.Contains("wss-tabs-nav-centered", centered.Find(".wss-tabs-nav").ClassList);
    }

    [Fact]
    public void Type_card_adds_the_root_modifier_class_only_when_set()
    {
        var plain = RenderTabs();
        Assert.DoesNotContain("wss-tabs-card", plain.Find(".wss-tabs").ClassList);

        var card = Render<Tabs>(p =>
        {
            p.Add(t => t.Type, TabsType.Card);
            p.AddChildContent<Tab>(tp => tp.Add(c => c.Key, "a").Add(c => c.Title, "A"));
        });
        Assert.Contains("wss-tabs-card", card.Find(".wss-tabs").ClassList);
        // Keyboard/ARIA are identical to Line -- still a plain tab, just CSS-different.
        Assert.Equal("tab", card.Find("[role=tab]").GetAttribute("role"));
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
        var cut = Render<SearchInput>(p => p
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
        var cut = Render<SearchInput>(p => p
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
        var cut = Render<SearchInput>(p => p
            .Add(s => s.Disabled, true)
            .Add(s => s.OnSearch, (string? _) => fired = true));

        Assert.Empty(cut.FindAll(".wss-search-addon"));
        Assert.True(cut.Find(".wss-search-input").HasAttribute("disabled"));
        cut.Find(".wss-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.False(fired);
    }

    [Fact]
    public void SearchInput_addon_template_without_labels_wires_aria_labelledby()
    {
        var cut = Render<SearchInput>(p => p
            .Add(s => s.Id, "po-search")
            .Add(s => s.AddonContent, b => b.AddContent(0, "POs")));

        var input = cut.Find(".wss-search-input");
        Assert.Equal("po-search-addon", input.GetAttribute("aria-labelledby"));
        Assert.Null(input.GetAttribute("aria-label"));
    }

    [Fact]
    public void SearchInput_InputLabel_wins_over_addon_content_and_suppresses_aria_labelledby()
    {
        var cut = Render<SearchInput>(p => p
            .Add(s => s.Id, "po-search")
            .Add(s => s.InputLabel, "Search purchase orders")
            .Add(s => s.AddonContent, b => b.AddContent(0, "POs")));

        var input = cut.Find(".wss-search-input");
        Assert.Equal("Search purchase orders", input.GetAttribute("aria-label"));
        Assert.Null(input.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void SearchInput_AddonLabel_path_is_unaffected_by_the_labelledby_wiring()
    {
        var cut = Render<SearchInput>(p => p
            .Add(s => s.Id, "po-search")
            .Add(s => s.AddonLabel, "POs"));

        var input = cut.Find(".wss-search-input");
        Assert.Equal("POs", input.GetAttribute("aria-label"));
        Assert.Null(input.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void SearchInput_allow_clear_renders_only_with_a_non_empty_value_and_clears_it()
    {
        string? value = "abc";
        var cut = Render<SearchInput>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.Value, value)
            .Add(s => s.ValueChanged, (string? v) => value = v));

        Assert.NotNull(cut.Find(".wss-search-clear"));
        cut.Find(".wss-search-clear").Click();
        Assert.Null(value);

        // Re-render with the now-empty value: the clear button disappears.
        cut.Render(p => p.Add(s => s.Value, (string?)null));
        Assert.Empty(cut.FindAll(".wss-search-clear"));
    }

    [Fact]
    public void SearchInput_without_allow_clear_never_renders_the_clear_button()
    {
        var cut = Render<SearchInput>(p => p.Add(s => s.Value, "abc"));
        Assert.Empty(cut.FindAll(".wss-search-clear"));
    }

    [Fact]
    public void SearchInput_allow_clear_is_suppressed_while_disabled()
    {
        var cut = Render<SearchInput>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.Disabled, true)
            .Add(s => s.Value, "abc"));

        Assert.Empty(cut.FindAll(".wss-search-clear"));
    }

    [Fact]
    public void SearchInput_enter_button_text_renders_text_instead_of_the_icon()
    {
        var cut = Render<SearchInput>(p => p.Add(s => s.EnterButtonText, "Search"));

        var btn = cut.Find(".wss-search-btn");
        Assert.Contains("wss-search-btn-enter", btn.ClassList);
        Assert.Contains("Search", btn.TextContent);
        Assert.Null(btn.GetAttribute("aria-label")); // visible text is the accessible name instead
    }

    [Fact]
    public void SearchInput_without_enter_button_text_keeps_the_icon_only_button()
    {
        var cut = Render<SearchInput>();
        var btn = cut.Find(".wss-search-btn");
        Assert.DoesNotContain("wss-search-btn-enter", btn.ClassList);
        Assert.Equal("Search", btn.GetAttribute("aria-label"));
    }

    [Fact]
    public void SearchInput_empty_AddonLabel_with_AddonContent_still_uses_aria_labelledby()
    {
        // AddonLabel = "" (not null) is the edge case: InputLabel ?? AddonLabel alone would
        // render aria-label="" while aria-labelledby also pointed at the addon span — both
        // computed from the code-behind properties so exactly one renders.
        var cut = Render<SearchInput>(p => p
            .Add(s => s.Id, "po-search")
            .Add(s => s.AddonLabel, "")
            .Add(s => s.AddonContent, b => b.AddContent(0, "POs")));

        var input = cut.Find(".wss-search-input");
        Assert.Equal("po-search-addon", input.GetAttribute("aria-labelledby"));
        Assert.Null(input.GetAttribute("aria-label"));
    }
}
