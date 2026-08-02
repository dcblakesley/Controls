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
        // already-registered tab (no tab added or removed). The count chip is rendered by the tab
        // itself, so the parameter change that carries it also re-renders it; the strip is asked for
        // one corrective pass as well, because a tab reporting new parameter values is its only cue
        // that the pane delegate it embedded came from the fragment's previous execution.
        var rendersBefore = cut.RenderCount;
        count = 34;
        cut.Render(p => p.Add(t => t.ChildContent, Children()));

        Assert.Equal("34", cut.Find(".wss-tabs-count").TextContent);
        // Bounded, not a runaway loop -- an unguarded notification would re-trigger on every
        // subsequent pass, since ChildContent is a new delegate each time. (RenderCount also counts
        // the tabs' own renders below this component, hence bounds larger than the pass count.)
        Assert.True(cut.RenderCount - rendersBefore <= 6, $"render delta {cut.RenderCount - rendersBefore}");

        // ...and it has settled: an identical re-render afterwards costs nothing but the pass-through.
        rendersBefore = cut.RenderCount;
        cut.Render(p => p.Add(t => t.ChildContent, Children()));
        Assert.True(cut.RenderCount - rendersBefore <= 2, $"idle render delta {cut.RenderCount - rendersBefore}");
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

    [Fact]
    public void A_title_and_pane_change_on_the_same_pass_shows_the_new_pane_not_the_previous_one()
    {
        // The strip embeds the active tab's pane while building its own render tree, which is one
        // pass before the diff hands that tab the delegate the consumer's fragment just produced --
        // so a pass that changes both the title and the pane would otherwise paint the new title
        // above the OLD pane. It bites exactly when the pane fragment closes over a local (a foreach
        // variable, a method argument) instead of a field, because then the previous delegate is
        // still holding the previous value rather than reading the current one.
        static RenderFragment Body(string title, string pane) => builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "a");
            builder.AddAttribute(2, "Title", title);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, pane)));
            builder.CloseComponent();

            builder.OpenComponent<Tab>(4);
            builder.AddAttribute(5, "Key", "b");
            builder.AddAttribute(6, "Title", "B");
            builder.AddAttribute(7, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane B")));
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, Body("T1", "P1")));
        Assert.Equal("P1", cut.Find("[role=tabpanel]").TextContent.Trim());

        cut.Render(p => p.Add(t => t.ChildContent, Body("T2", "P2")));

        Assert.Equal("T2", cut.FindAll(".wss-tabs-label")[0].TextContent.Trim());
        Assert.Equal("P2", cut.Find("[role=tabpanel]").TextContent.Trim());
    }

    [Fact]
    public void A_static_strip_settles_instead_of_correcting_itself_forever()
    {
        // The corrective render above is requested from a snapshot COMPARISON, and that is the only
        // thing keeping it finite. A gate that compared the pane delegate's reference identity
        // instead would never settle: re-executing the consumer's fragment mints a fresh
        // RenderFragment every pass, so "the delegate changed" is true forever and each corrective
        // render re-arms the next one -- an invisible treadmill (no DOM change at all) that costs a
        // SignalR round trip per pass on Blazor Server. Identical re-renders must cost a constant,
        // bounded number of passes.
        static RenderFragment Body() => builder =>
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "a");
            builder.AddAttribute(2, "Title", "A");
            builder.AddAttribute(3, "Count", 3);
            builder.AddAttribute(4, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane A")));
            builder.CloseComponent();

            builder.OpenComponent<Tab>(5);
            builder.AddAttribute(6, "Key", "b");
            builder.AddAttribute(7, "Title", "B");
            builder.AddAttribute(8, "ChildContent", (RenderFragment)(b => b.AddContent(0, "Pane B")));
            builder.CloseComponent();
        };

        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, Body()));

        var deltas = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            var before = cut.RenderCount;
            cut.Render(p => p.Add(t => t.ChildContent, Body()));
            deltas.Add(cut.RenderCount - before);
        }

        var trace = string.Join(",", deltas);
        Assert.All(deltas, d => Assert.True(d <= 4, $"per-pass render delta {d} (deltas: {trace})"));
        // Constant, not creeping: every identical pass costs the same as the one before it.
        Assert.True(deltas.Distinct().Count() == 1, $"render cost is not constant at rest (deltas: {trace})");
        Assert.Equal("Pane A", cut.Find("[role=tabpanel]").TextContent.Trim());
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

    // The four tests below pin the EXACT keyboard order the strip ends up with when a tab is
    // inserted among siblings that all skipped their parameters. Three of them pin behavior that is
    // wrong -- deliberately, and named so. They replace a guard that asserted the order was "at
    // worst a rotation of the declared order", which cyclic arrow navigation cannot observe. That
    // invariant is false and unachievable: with no anchor the newcomer's position is simply not in
    // the data, and no placement rule can make every case a rotation. Declared [a, b, mid, c] with
    // mid the newcomer has rotations [a,b,mid,c], [b,mid,c,a], [mid,c,a,b] and [c,a,b,mid]; append
    // yields [a,b,c,mid] and prepend yields [mid,a,b,c], and neither is in that set.
    //
    // Fixing them needs an exact re-collection in document order, which Blazor does not offer for
    // a parameter-skipped child (see Blazor_offers_no_document_ordered_re_registration_of_
    // parameter_skipped_children below). Until one of the mechanisms that CAN be exact is adopted,
    // these record what actually happens so a change to it is deliberate rather than accidental.

    static string[] StripOrder(IRenderedComponent<Tabs> cut) =>
        RenderedTabs(cut.Instance).Select(t => t.Key).ToArray();

    static int ActiveIndex(IRenderedComponent<Tabs> cut) => cut.FindAll(".wss-tabs-tab").ToList()
        .FindIndex(e => e.ClassList.Contains("wss-tabs-tab-active"));

    // Walks the strip with one arrow key, always from the button that currently holds the Tab stop
    // (the only one the keyboard can reach), and reports the rendered index visited at each step.
    static int[] ArrowWalk(IRenderedComponent<Tabs> cut, string key, int steps)
    {
        var visited = new List<int> { ActiveIndex(cut) };
        for (var i = 0; i < steps; i++)
        {
            cut.FindAll(".wss-tabs-tab")[ActiveIndex(cut)].KeyDown(new KeyboardEventArgs { Key = key });
            visited.Add(ActiveIndex(cut));
        }
        return [.. visited];
    }

    [Fact]
    public void Arrow_navigation_after_a_LEADING_insertion_into_an_all_skipped_strip_still_walks_rendered_order()
    {
        // The one shape that survives the ambiguity. Declared [new, a, b]; the newcomer is appended,
        // giving [a, b, new] -- which happens to be a rotation of the declared order, and arrow
        // navigation is cyclic, so it visits the same neighbours in the same direction anyway.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(false, false)));
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingTabs(true, false)));

        Assert.Equal(["New", "A", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["a", "b", "new"], StripOrder(cut));

        // The walk starts on rendered index 1, not 0: the highlighted tab after an unanchored
        // leading insertion is the one that used to be first (see the unbound-strip test below).
        // From wherever it starts, though, each arrow steps to the adjacent RENDERED button.
        Assert.Equal([1, 2, 0, 1], ArrowWalk(cut, "ArrowRight", 3));
        Assert.Equal([1, 0, 2, 1], ArrowWalk(cut, "ArrowLeft", 3));
    }

    // A content-less strip whose conditional tab is declared in the MIDDLE. Every parameter is a
    // string, so no sibling re-registers to anchor it.
    static RenderFragment ConditionalMiddleTab(bool showMid) => builder =>
    {
        builder.OpenComponent<Tab>(0);
        builder.AddAttribute(1, "Key", "a");
        builder.AddAttribute(2, "Title", "A");
        builder.CloseComponent();

        builder.OpenComponent<Tab>(3);
        builder.AddAttribute(4, "Key", "b");
        builder.AddAttribute(5, "Title", "B");
        builder.CloseComponent();

        if (showMid)
        {
            builder.OpenComponent<Tab>(6);
            builder.AddAttribute(7, "Key", "mid");
            builder.AddAttribute(8, "Title", "Mid");
            builder.CloseComponent();
        }

        builder.OpenComponent<Tab>(9);
        builder.AddAttribute(10, "Key", "c");
        builder.AddAttribute(11, "Title", "C");
        builder.CloseComponent();
    };

    [Fact]
    public void Arrow_navigation_after_a_MIDDLE_insertion_into_an_all_skipped_strip_skips_the_newcomer()
    {
        // A middle insertion is not a rotation, so this one is observable and it is an ARIA defect:
        // the ARIA tabs pattern says an arrow moves to the ADJACENT tab. Declared [a, b, mid, c]
        // renders correctly, but the newcomer is appended to the keyboard order, so ArrowRight from
        // the first button visits rendered indices 0, 1, 3, 2 -- skipping Mid and then moving
        // backwards onto it. SHOULD be [0, 1, 2, 3, 0] / [0, 3, 2, 1, 0].
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, ConditionalMiddleTab(false)));
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalMiddleTab(true)));

        // The RENDERED strip is right -- each tab emits its own button, so the diff places it.
        Assert.Equal(["A", "B", "Mid", "C"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["a", "b", "c", "mid"], StripOrder(cut));

        Assert.Equal([0, 1, 3, 2, 0], ArrowWalk(cut, "ArrowRight", 4));
        Assert.Equal([0, 2, 3, 1, 0], ArrowWalk(cut, "ArrowLeft", 4));
    }

    // Two conditional tabs revealed on the same pass, interleaved with skipped siblings.
    static RenderFragment TwoConditionalTabs(bool show) => builder =>
    {
        if (show)
        {
            builder.OpenComponent<Tab>(0);
            builder.AddAttribute(1, "Key", "p1");
            builder.AddAttribute(2, "Title", "P1");
            builder.CloseComponent();
        }

        builder.OpenComponent<Tab>(3);
        builder.AddAttribute(4, "Key", "a");
        builder.AddAttribute(5, "Title", "A");
        builder.CloseComponent();

        if (show)
        {
            builder.OpenComponent<Tab>(6);
            builder.AddAttribute(7, "Key", "p2");
            builder.AddAttribute(8, "Title", "P2");
            builder.CloseComponent();
        }

        builder.OpenComponent<Tab>(9);
        builder.AddAttribute(10, "Key", "b");
        builder.AddAttribute(11, "Title", "B");
        builder.CloseComponent();
    };

    [Fact]
    public void Two_newcomers_on_one_pass_into_an_all_skipped_strip_both_land_at_the_end()
    {
        // Declared [p1, a, p2, b]. Both newcomers are unanchored, so both are appended and even
        // their relation to each other's neighbours is lost. SHOULD be [p1, a, p2, b].
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, TwoConditionalTabs(false)));
        cut.Render(p => p.Add(t => t.ChildContent, TwoConditionalTabs(true)));

        Assert.Equal(["P1", "A", "P2", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["a", "b", "p1", "p2"], StripOrder(cut));
    }

    // The same tabs every pass, only reordered -- a @keyed loop over a list the consumer sorted.
    static RenderFragment KeyedTabs(string[] keys) => builder =>
    {
        var seq = 0;
        foreach (var k in keys)
        {
            builder.OpenComponent<Tab>(seq++);
            builder.SetKey(k);
            builder.AddAttribute(seq++, "Key", k);
            builder.AddAttribute(seq++, "Title", k.ToUpperInvariant());
            builder.CloseComponent();
        }
    };

    [Fact]
    public void A_keyed_reorder_moves_the_buttons_but_not_the_keyboard_order()
    {
        // A reorder changes no tab's parameters at all, so not even the newcomer-registers-late
        // signal exists: nothing reports and the strip's list keeps the original order outright.
        // The buttons DO move, because @key moves the component instances and each one carries its
        // own button. SHOULD be ["c", "a", "b"] both times.
        var cut = Render<Tabs>(p => p.Add(t => t.ChildContent, KeyedTabs(["a", "b", "c"])));
        Assert.Equal(["A", "B", "C"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["a", "b", "c"], StripOrder(cut));

        cut.Render(p => p.Add(t => t.ChildContent, KeyedTabs(["c", "a", "b"])));

        Assert.Equal(["C", "A", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal(["a", "b", "c"], StripOrder(cut));
    }

    [Fact]
    public void An_unbound_strip_after_a_leading_insertion_highlights_the_second_rendered_button()
    {
        // ActiveKey's documented contract is that null "activates the first enabled tab" -- the
        // first one the consumer declared, which is the first one rendered. With no bound key and no
        // prior click, resolution falls through to _tabs[0], and after an unanchored leading
        // insertion that is the tab that USED to be first. So the strip renders [New, A, B] and
        // highlights A. SHOULD highlight New.
        var cut = Render<Tabs>(p => p
            .Add(t => t.ChildContent, ConditionalLeadingTabs(false, false))
            .Add(t => t.Id, "s"));
        Assert.Equal("s-tab-a", cut.Find(".wss-tabs-tab-active").GetAttribute("id"));

        cut.Render(p => p
            .Add(t => t.ChildContent, ConditionalLeadingTabs(true, false))
            .Add(t => t.Id, "s"));

        Assert.Equal(["New", "A", "B"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.Equal("s-tab-a", cut.Find(".wss-tabs-tab-active").GetAttribute("id"));
        // Exactly one Tab stop and one selected tab, at least -- the ARIA invariant holds either way.
        var buttons = cut.FindAll("[role=tab]");
        Assert.Equal(1, buttons.Count(e => e.GetAttribute("tabindex") == "0"));
        Assert.Equal(1, buttons.Count(e => e.GetAttribute("aria-selected") == "true"));
    }

    // A stand-in for Tabs/Tab, used only to pin the framework behavior the whole ordering problem
    // rests on. It is deliberately not the real components: the point is that this is Blazor's
    // behavior, not the strip's, so no amount of bookkeeping inside Tabs can work around it.
    sealed class OrderProbeParent : ComponentBase
    {
        [Parameter] public RenderFragment? ChildContent { get; set; }
        [Parameter] public bool Fixed { get; set; }

        internal readonly List<string> Registered = new();

        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
            Registered.Clear();
            builder.OpenComponent<CascadingValue<OrderProbeParent>>(0);
            builder.AddAttribute(1, "Value", this);
            builder.AddAttribute(2, "IsFixed", Fixed);
            builder.AddAttribute(3, "ChildContent", (RenderFragment)(b => b.AddContent(0, ChildContent)));
            builder.CloseComponent();
        }
    }

    sealed class OrderProbeChild : ComponentBase
    {
        [CascadingParameter] public OrderProbeParent? Parent { get; set; }
        [Parameter] public string Name { get; set; } = "";

        protected override void OnParametersSet() => Parent?.Registered.Add(Name);
    }

    // Razor-compiler-shaped sequence numbers: the conditional child owns its own range, so revealing
    // it does not renumber its siblings (which would defeat the diff's skip optimization and make
    // the probe meaningless).
    static RenderFragment OrderProbeChildren(bool showLead, bool showMid) => builder =>
    {
        if (showLead)
        {
            builder.OpenComponent<OrderProbeChild>(0);
            builder.AddAttribute(1, "Name", "lead");
            builder.CloseComponent();
        }

        builder.OpenComponent<OrderProbeChild>(2);
        builder.AddAttribute(3, "Name", "a");
        builder.CloseComponent();

        if (showMid)
        {
            builder.OpenComponent<OrderProbeChild>(4);
            builder.AddAttribute(5, "Name", "mid");
            builder.CloseComponent();
        }

        builder.OpenComponent<OrderProbeChild>(6);
        builder.AddAttribute(7, "Name", "b");
        builder.CloseComponent();
    };

    [Fact]
    public void Blazor_offers_no_document_ordered_re_registration_of_parameter_skipped_children()
    {
        // Why Tabs keeps an ordering heuristic at all, and why the obvious cure does not work.
        //
        // A component whose own parameters are all unchanged immutable values is skipped by the
        // render-tree diff -- SetParametersAsync is never called -- so a content-less Tab cannot
        // report where it was declared. Dropping IsFixed from the CascadingValue looks like the fix,
        // and it does make every live child report in every pass. It does NOT report them in
        // document order: CascadingValue notifies its subscribers from SetParametersAsync, BEFORE it
        // re-renders its ChildContent, and it walks a HashSet built as children first subscribed. So
        // the survivors come back in construction order and any newcomer, which is created later by
        // the diff, always lands last no matter where it was declared -- and it never self-corrects.
        //
        // That is the same wrong answer the current append heuristic gives, at the cost of one extra
        // render per child per pass. An exact answer needs a mechanism that re-creates the children
        // (a generation @key rebuild, which tears the subtree down) or that reads the rendered DOM.
        var loose = Render<OrderProbeParent>(p => p
            .Add(x => x.Fixed, false)
            .Add(x => x.ChildContent, OrderProbeChildren(false, false)));
        Assert.Equal(["a", "b"], loose.Instance.Registered);

        loose.Render(p => p.Add(x => x.Fixed, false).Add(x => x.ChildContent, OrderProbeChildren(true, false)));
        // The SET is complete...
        Assert.Equal(["a", "b", "lead"], loose.Instance.Registered.Order());
        // ...but the ORDER is subscription order; document order is [lead, a, b].
        Assert.Equal(["a", "b", "lead"], loose.Instance.Registered);

        // A middle insertion on top, and a later pass that changes nothing: still never corrected.
        loose.Render(p => p.Add(x => x.Fixed, false).Add(x => x.ChildContent, OrderProbeChildren(true, true)));
        Assert.Equal(["a", "b", "lead", "mid"], loose.Instance.Registered);
        loose.Render(p => p.Add(x => x.Fixed, false).Add(x => x.ChildContent, OrderProbeChildren(true, true)));
        Assert.Equal(["a", "b", "lead", "mid"], loose.Instance.Registered);

        // The control: with IsFixed (what Tabs uses), only the newcomer reports at all.
        var pinned = Render<OrderProbeParent>(p => p
            .Add(x => x.Fixed, true)
            .Add(x => x.ChildContent, OrderProbeChildren(false, false)));
        Assert.Equal(["a", "b"], pinned.Instance.Registered);

        pinned.Render(p => p.Add(x => x.Fixed, true).Add(x => x.ChildContent, OrderProbeChildren(true, false)));
        Assert.Equal(["lead"], pinned.Instance.Registered);
    }

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

    // A consumer component in a tab's pane, counting its own construction/disposal and holding
    // instance state that only survives if the instance itself does.
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

    // [new?, a (content-less, so parameter-skipped), pane (carries the consumer's component)]. The
    // content-less tab is what makes the insertion the ambiguous shape; the pane tab is where the
    // consumer's component lives, because Tabs.ChildContent renders inside role="tablist" and only
    // <Tab> may be declared there.
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

        builder.OpenComponent<Tab>(6);
        builder.AddAttribute(7, "Key", "pane");
        builder.AddAttribute(8, "Title", "Pane");
        builder.AddAttribute(9, "ChildContent", (RenderFragment)(b =>
        {
            b.OpenComponent<StatefulSibling>(0);
            b.CloseComponent();
        }));
        builder.CloseComponent();
    };

    [Fact]
    public void A_structural_insertion_reconstructs_nothing_that_was_already_live()
    {
        // The previous design recovered the declared order by bumping a generation @key on the
        // ChildContent CascadingValue, which tore down and reconstructed that whole subtree on every
        // structural insertion -- every Tab instance with it. Nothing rebuilds anything now, so this
        // pins that the retired limitation stays retired: the live Tab instances are the SAME
        // objects across an insertion and a removal, and a consumer component in a pane is never
        // reconstructed either.
        //
        // (This used to declare the consumer's component as a direct child of <Tabs>, which is where
        // the old rebuild really hurt. That is no longer a legal place to put one: ChildContent now
        // renders inside the role="tablist" element, so a non-Tab child there is an
        // aria-required-children violation. Moved into a pane, which is what the parameter's own
        // docs direct consumers to do.)
        StatefulSibling.Constructed = 0;
        StatefulSibling.Disposed = 0;

        // ActiveKey names the pane tab throughout: only the active tab's pane is rendered, and the
        // point here is what happens to a live instance.
        var cut = Render<Tabs>(p => p
            .Add(t => t.ChildContent, TabsWithStatefulSibling(false))
            .Add(t => t.ActiveKey, "pane"));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);
        var before = RenderedTabs(cut.Instance).ToDictionary(t => t.Key);

        // Ordinary re-render with the same structure.
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(false)));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);

        // Insertion before a parameter-skipped sibling -- the case that used to force the rebuild.
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(true)));
        Assert.Equal(["New", "A", "Pane"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        // Same Tab objects, not fresh ones standing in for them.
        Assert.All(before, kvp => Assert.Same(kvp.Value, RenderedTabs(cut.Instance).Single(t => t.Key == kvp.Key)));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);

        // Removal: likewise untouched.
        cut.Render(p => p.Add(t => t.ChildContent, TabsWithStatefulSibling(false)));
        Assert.Equal(["A", "Pane"], cut.FindAll(".wss-tabs-label").Select(e => e.TextContent.Trim()));
        Assert.All(before, kvp => Assert.Same(kvp.Value, RenderedTabs(cut.Instance).Single(t => t.Key == kvp.Key)));
        Assert.Equal(1, StatefulSibling.Constructed);
        Assert.Equal(0, StatefulSibling.Disposed);
        Assert.Equal("1", cut.Find(".stateful-sibling").TextContent);
    }

    [Fact]
    public void The_tablist_owns_nothing_but_tab_buttons()
    {
        // aria-required-children: a role="tablist" must own role="tab" elements and nothing else.
        // Everything the strip renders of its own accord has to stay on the right side of that --
        // the extra-content slot beside the strip, and the pane below it. (Consumer markup declared
        // directly inside <Tabs> does land in the tablist and would violate this; that is a
        // documented constraint on the ChildContent parameter, not something the strip can police.)
        var cut = Render<Tabs>(p =>
        {
            p.Add(t => t.TabBarExtraContent, b => b.AddContent(0, "Extra action"));
            p.AddChildContent<Tab>(tp => tp
                .Add(c => c.Key, "a").Add(c => c.Title, "A").Add(c => c.Count, 3)
                .Add(c => c.ChildContent, b => b.AddContent(0, "Pane A")));
            p.AddChildContent<Tab>(tp => tp
                .Add(c => c.Key, "b").Add(c => c.Title, "B").Add(c => c.Disabled, true));
        });

        var tablist = cut.Find("[role=tablist]");
        Assert.NotEmpty(tablist.Children);
        Assert.All(tablist.Children, child => Assert.Equal("tab", child.GetAttribute("role")));
        // ...and the two things that must NOT be in there really are outside it.
        Assert.Empty(cut.FindAll("[role=tablist] .wss-tabs-nav-extra"));
        Assert.Empty(cut.FindAll("[role=tablist] [role=tabpanel]"));
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
