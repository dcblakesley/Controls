using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Accessibility contracts of the <see cref="Select{TValue}"/> engine and the four select form
/// controls built on or beside it, from the 2026-08-13 audit's SEL findings:
/// <list type="bullet">
/// <item>SEL-1 — the <c>role="combobox"</c> input exposes a VALUE: the selected label in single mode,
/// the joined selection through <c>aria-describedby</c> in multiple/tags mode.</item>
/// <item>SEL-2/SEL-9 — one persistent <c>role="status"</c> region, empty on first render, driving
/// filter/selection/clear/loading announcements from localizable templates.</item>
/// <item>SEL-3 — group runs reach assistive tech through per-run hidden names referenced by every
/// option in the run (a <c>role="group"</c> is impossible in a flat virtualized list).</item>
/// <item>SEL-4 — a standalone <c>&lt;Select&gt;</c> can be named (<c>InputLabel</c>, or a bare
/// <c>aria-label</c> lifted off the roleless wrapper); the form wrappers name it from
/// <c>FormLabel</c>'s <c>lbltext-{id}</c> anchor.</item>
/// <item>SEL-6 — every option reports <c>aria-setsize</c>/<c>aria-posinset</c> against the whole
/// filtered list, not the ~8 rows virtualization keeps in the DOM.</item>
/// <item>SEL-7/SEL-10/SEL-12 — the select-only combobox's keyboard model and the ARIA it advertises.</item>
/// <item>SEL-8 — the outside-click backdrop does not strand keyboard focus on <c>&lt;body&gt;</c>.</item>
/// <item>SEL-11/SEL-13 — no per-option repetition of the field tooltip; the MaxTagCount overflow chip
/// has a localizable name instead of "plus 3 dot dot dot".</item>
/// </list>
/// </summary>
public class A11ySelectTests : BunitContext
{
    public A11ySelectTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the placement/scroll/focus JS

    const string InputSelector = "input.wss-select-selection-search-input";

    static KeyboardEventArgs Key(string key) => new() { Key = key };

    static List<SelectOption<string>> Opts(params string[] values) =>
        values.Select(v => new SelectOption<string>(v, v)).ToList();

    static SelectOption<string> Opt(string value, string? group = null, bool disabled = false) =>
        new(value, value, disabled) { Group = group };

    // A model whose fields carry a field-level tooltip, for SEL-11.
    sealed class TooltipModel
    {
        [ToolTip("Pick the ticket's urgency")]
        public Priority? Priority { get; set; }

        [ToolTip("Pick a colour")]
        public string Colour { get; set; } = "Red";
    }

    // ---------------------------------------------------------------- SEL-1: the combobox's value

    [Fact]
    public void Single_mode_combobox_reports_the_selected_label_as_its_value_while_closed()
    {
        // The defect: value was bound to the search text, which is empty except while typing, so a
        // completed field was announced as "combo box, blank" though it visibly showed its label.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Value, "Apple"));

        Assert.Equal("Apple", cut.Find(InputSelector).GetAttribute("value"));
    }

    [Fact]
    public void An_empty_single_select_reports_an_empty_value_and_keeps_its_placeholder()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Placeholder, "Please select"));

        Assert.Equal(string.Empty, cut.Find(InputSelector).GetAttribute("value"));
        Assert.Equal("Please select", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    [Fact]
    public void Typing_replaces_the_value_while_open_and_the_label_comes_back_on_close()
    {
        // Filter-as-you-type must still own the input while the popup is open — the APG editable
        // combobox contract, and the behaviour every search test in the suite depends on.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Value, "Apple"));

        cut.Find(InputSelector).Input("ban");
        Assert.Equal("ban", cut.Find(InputSelector).GetAttribute("value"));
        Assert.Single(cut.FindAll("[role=option]")); // still filtering

        cut.Find(InputSelector).KeyDown(Key("Escape"));
        Assert.Equal("Apple", cut.Find(InputSelector).GetAttribute("value"));
    }

    [Fact]
    public void Multiple_mode_describes_the_combobox_with_the_joined_selection()
    {
        // The input is the tag-entry box there, so the value can't carry the selection; a hidden
        // element referenced from aria-describedby does.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.Values, new List<string> { "Apple", "Cherry" }));

        var describedBy = cut.Find(InputSelector).GetAttribute("aria-describedby");
        Assert.Contains("sel-selection", describedBy!);
        Assert.Equal("Apple, Cherry", cut.Find("#sel-selection").TextContent);
    }

    [Fact]
    public void The_joined_selection_includes_the_tags_MaxTagCount_collapsed()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.Values, new List<string> { "Apple", "Banana", "Cherry" })
            .Add(s => s.MaxTagCount, 1));

        Assert.Equal("Apple, Banana, Cherry", cut.Find("#sel-selection").TextContent);
    }

    [Fact]
    public void The_multiple_mode_description_is_appended_to_the_wrappers_own_describedby()
    {
        // The form wrapper's description/validation ids must survive, not be replaced.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.Values, new List<string> { "Apple" })
            .Add(s => s.AriaDescribedBy, "desc-sel error-msg-sel"));

        Assert.Equal("desc-sel error-msg-sel sel-selection",
            cut.Find(InputSelector).GetAttribute("aria-describedby"));
    }

    [Fact]
    public void Single_mode_leaves_aria_describedby_exactly_as_the_wrapper_supplied_it()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.AriaDescribedBy, "desc-sel"));

        Assert.Equal("desc-sel", cut.Find(InputSelector).GetAttribute("aria-describedby"));
    }

    // ------------------------------------------------------------------- SEL-4: naming the trigger

    [Fact]
    public void InputLabel_names_the_combobox_not_the_roleless_wrapper()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Red", "Green"))
            .Add(s => s.InputLabel, "Colour"));

        Assert.Equal("Colour", cut.Find(InputSelector).GetAttribute("aria-label"));
        Assert.False(cut.Find(".wss-select").HasAttribute("aria-label"));
    }

    [Fact]
    public void A_bare_aria_label_is_lifted_off_the_wrapper_onto_the_combobox()
    {
        // The trap the audit found: <Select aria-label="Colour"> splatted onto a roleless <div>,
        // where it is ignored — markup that looks labelled and renders a nameless combobox.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Red", "Green"))
            .AddUnmatched("aria-label", "Colour"));

        Assert.Equal("Colour", cut.Find(InputSelector).GetAttribute("aria-label"));
        Assert.False(cut.Find(".wss-select").HasAttribute("aria-label"));
    }

    [Fact]
    public void A_case_variant_aria_Label_is_also_lifted_off_the_wrapper()
    {
        // AttributeSplat.RestExcept's presence check (rest.ContainsKey) uses the same OrdinalIgnoreCase
        // comparer as Blazor's own CaptureUnmatchedValues dictionary, so it found a case-variant key
        // like "aria-Label" -- but the removal loop compared with plain Ordinal, so the key was
        // detected as present and then never actually stripped: a duplicate "aria-Label" leaked onto
        // the roleless wrapper alongside the copy correctly re-homed onto the combobox input.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Red", "Green"))
            .AddUnmatched("aria-Label", "Colour"));

        Assert.Equal("Colour", cut.Find(InputSelector).GetAttribute("aria-label"));
        Assert.False(cut.Find(".wss-select").HasAttribute("aria-label"));
    }

    [Fact]
    public void Lifting_aria_label_leaves_the_other_splatted_attributes_on_the_wrapper()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Red"))
            .AddUnmatched("aria-label", "Colour")
            .AddUnmatched("data-foo", "bar")
            .AddUnmatched("title", "pick one"));

        var wrapper = cut.Find(".wss-select");
        Assert.Equal("bar", wrapper.GetAttribute("data-foo"));
        Assert.Equal("pick one", wrapper.GetAttribute("title"));
        Assert.False(wrapper.HasAttribute("aria-label"));
    }

    [Fact]
    public void InputLabel_wins_over_a_splatted_aria_label()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Red"))
            .Add(s => s.InputLabel, "Explicit")
            .AddUnmatched("aria-label", "Splatted"));

        Assert.Equal("Explicit", cut.Find(InputSelector).GetAttribute("aria-label"));
    }

    [Fact]
    public void An_unnamed_Select_emits_no_empty_aria_label()
    {
        var cut = Render<Select<string>>(p => p.Add(s => s.Options, Opts("Red")));

        Assert.False(cut.Find(InputSelector).HasAttribute("aria-label"));
        Assert.False(cut.Find(InputSelector).HasAttribute("aria-labelledby"));
    }

    [Fact]
    public void EditSelectSearch_names_its_combobox_from_the_label_text_anchor()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<SelectOption<Priority?>> { new(Priority.Medium, "Medium") });
            b.CloseComponent();
        }));

        var input = cut.Find(InputSelector);
        Assert.Equal("lbltext-Priority", input.GetAttribute("aria-labelledby"));
        // A dangling reference leaves the field unnamed, which is worse than the tooltip-polluted
        // name the anchor replaced.
        Assert.Equal("Priority", cut.Find("#lbltext-Priority").TextContent.Trim());
    }

    [Fact]
    public void EditMultiSelect_names_its_combobox_from_the_label_text_anchor()
    {
        var model = new PersonModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<SelectOption<Color>> { new(Color.Red, "Red") });
            b.CloseComponent();
        }));

        var input = cut.Find(InputSelector);
        Assert.Equal("lbltext-FavoriteColors", input.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#lbltext-FavoriteColors"));
    }

    [Fact]
    public void The_native_select_is_named_from_the_label_text_anchor_too()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var select = cut.Find("select.edit-select-select");
        Assert.Equal("lbltext-Priority", select.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#lbltext-Priority"));
        // aria-labelledby supersedes <label for> for NAMING; `for` still drives click-to-focus.
        Assert.Equal("Priority", cut.Find("label.edit-label").GetAttribute("for"));
    }

    // ---------------------------------------------------- SEL-2 / SEL-9: the status live region

    [Fact]
    public void The_status_region_renders_from_the_first_pass_with_no_content()
    {
        // A live region injected together with its text is not reliably announced; it has to be in
        // the accessibility tree before its content changes.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Options, Opts("Apple", "Banana")));

        var status = cut.Find("#sel-status");
        Assert.Equal("status", status.GetAttribute("role"));
        Assert.Contains("wss-sr-only", status.ClassList);
        Assert.Equal(string.Empty, status.TextContent);
    }

    [Fact]
    public void Filtering_announces_the_result_count()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry")));

        cut.Find(InputSelector).Input("a"); // Apple + Banana

        Assert.Equal("2 results", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Filtering_to_nothing_announces_the_empty_text()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.EmptyText, "Nothing found"));

        cut.Find(InputSelector).Input("zzz");

        Assert.Equal("Nothing found", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Opening_announces_the_result_count()
    {
        var cut = Render<Select<string>>(p => p.Add(s => s.Options, Opts("Apple", "Banana")));

        cut.Find(InputSelector).KeyDown(Key("ArrowDown"));

        Assert.Equal("2 results", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Options_arriving_from_a_server_driven_search_announce_the_new_count()
    {
        // OnSearch -> the consumer reassigns Options: the list changes under the user with no
        // keystroke of their own left to trigger the filter announcement.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.DefaultOpen, true));

        cut.Render(p => p.Add(s => s.Options, Opts("Apple", "Banana", "Cherry")));

        Assert.Equal("3 results", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Selecting_and_deselecting_are_announced()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Values, new List<string>())
            .Add(s => s.DefaultOpen, true));

        cut.FindAll("[role=option]")[0].Click();
        Assert.Equal("Apple selected", cut.Find("[role=status]").TextContent);

        cut.FindAll("[role=option]")[0].Click();
        Assert.Equal("Apple deselected", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Backspace_removing_a_tag_is_announced()
    {
        // Five presses used to remove five tags in total silence.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Values, new List<string> { "Apple", "Banana" }));

        cut.Find(InputSelector).KeyDown(Key("Backspace"));

        Assert.Equal("Banana deselected", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Clearing_is_announced()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Value, "Apple"));

        cut.Find(".wss-select-clear").Click();

        Assert.Equal("Selection cleared", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Loading_is_announced_through_the_same_region_and_yields_to_it_afterwards()
    {
        // aria-busy on the roleless wrapper announced nothing, and the spinner is aria-hidden.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Loading, true)
            .Add(s => s.DefaultOpen, true));

        Assert.Equal("Loading", cut.Find("[role=status]").TextContent);

        cut.Render(p => p.Add(s => s.Loading, false).Add(s => s.Options, Opts("Apple", "Banana")));
        Assert.Equal("2 results", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void The_announcement_strings_are_localizable()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Values, new List<string>())
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ResultCountAnnouncementFormat, "{0} Treffer")
            .Add(s => s.SelectedAnnouncementFormat, "{0} ausgewählt")
            .Add(s => s.DeselectedAnnouncementFormat, "{0} abgewählt")
            .Add(s => s.LoadingAnnouncement, "Wird geladen"));

        cut.FindAll("[role=option]")[1].Click();
        Assert.Equal("Banana ausgewählt", cut.Find("[role=status]").TextContent);

        cut.FindAll("[role=option]")[1].Click();
        Assert.Equal("Banana abgewählt", cut.Find("[role=status]").TextContent);

        cut.Find(InputSelector).Input("a");
        Assert.Equal("2 Treffer", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void The_status_region_is_not_referenced_from_aria_describedby()
    {
        // It is a live region: referencing it would also read the last announcement back on focus.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.Values, new List<string> { "Apple" }));

        Assert.DoesNotContain("sel-status", cut.Find(InputSelector).GetAttribute("aria-describedby")!);
    }

    // ----------------------------------------------------------------------- SEL-3: group runs

    [Fact]
    public void Every_option_is_described_by_its_group_and_the_reference_resolves()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Banana", "Fruit"),
                Opt("Carrot", "Vegetable"),
            })
            .Add(s => s.DefaultOpen, true));

        var options = cut.FindAll("[role=option]");
        Assert.Equal("sel-grp-0", options[0].GetAttribute("aria-describedby"));
        Assert.Equal("sel-grp-0", options[1].GetAttribute("aria-describedby"));
        Assert.Equal("sel-grp-1", options[2].GetAttribute("aria-describedby"));

        Assert.Equal("Fruit", cut.Find("#sel-grp-0").TextContent);
        Assert.Equal("Vegetable", cut.Find("#sel-grp-1").TextContent);
    }

    [Fact]
    public void The_group_names_live_outside_the_listbox_so_virtualization_cannot_unmount_them()
    {
        // The visible header row is a virtualized row like any other: on a group longer than the
        // dropdown it is gone while its own options are still on screen, and an aria-describedby
        // pointing at an unmounted id silently resolves to nothing.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple", "Fruit") })
            .Add(s => s.DefaultOpen, true));

        var name = cut.Find("#sel-grp-0");
        Assert.Contains("wss-sr-only", name.ClassList);
        // A listbox's children should be options; the names sit beside the trigger instead (an id
        // reference resolves document-wide), which is also what keeps them mounted.
        Assert.Empty(cut.FindAll("[role=listbox] #sel-grp-0"));
        // ...and the visible header keeps its presentation-only treatment (it must not become a
        // second element with the same id, nor a role=option).
        var header = cut.Find(".wss-select-item-group-label");
        Assert.Equal("presentation", header.GetAttribute("role"));
        Assert.Equal("true", header.GetAttribute("aria-hidden"));
        Assert.False(header.HasAttribute("id"));
    }

    [Fact]
    public void An_ungrouped_option_carries_no_group_description()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.DefaultOpen, true));

        Assert.All(cut.FindAll("[role=option]"), o => Assert.False(o.HasAttribute("aria-describedby")));
        Assert.Empty(cut.FindAll(".wss-select .wss-sr-only[id*='-grp-']"));
    }

    [Fact]
    public void Group_names_track_the_filter()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Id, "sel")
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Carrot", "Vegetable"),
            })
            .Add(s => s.DefaultOpen, true));

        cut.Find(InputSelector).Input("Carrot"); // only the Vegetable run survives

        var options = cut.FindAll("[role=option]");
        Assert.Single(options);
        Assert.Equal("sel-grp-0", options[0].GetAttribute("aria-describedby"));
        Assert.Equal("Vegetable", cut.Find("#sel-grp-0").TextContent);
        Assert.Empty(cut.FindAll("#sel-grp-1"));
    }

    // ------------------------------------------------------------- SEL-6: set size / position

    [Fact]
    public void Options_report_their_position_in_the_whole_filtered_list()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.DefaultOpen, true));

        var options = cut.FindAll("[role=option]");
        Assert.Equal(3, options.Count);
        for (var i = 0; i < options.Count; i++)
        {
            Assert.Equal("3", options[i].GetAttribute("aria-setsize"));
            Assert.Equal($"{i + 1}", options[i].GetAttribute("aria-posinset"));
        }
    }

    [Fact]
    public void Group_headers_do_not_consume_a_position_and_the_count_follows_the_filter()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Banana", "Fruit"),
                Opt("Carrot", "Vegetable"),
            })
            .Add(s => s.DefaultOpen, true));

        var options = cut.FindAll("[role=option]");
        Assert.Equal(3, options.Count); // two header ROWS render as well, and must not be counted
        for (var i = 0; i < options.Count; i++)
        {
            Assert.Equal("3", options[i].GetAttribute("aria-setsize"));
            Assert.Equal($"{i + 1}", options[i].GetAttribute("aria-posinset"));
        }

        cut.Find(InputSelector).Input("Carrot");

        var filtered = cut.FindAll("[role=option]");
        Assert.Single(filtered);
        Assert.Equal("1", filtered[0].GetAttribute("aria-setsize"));
        Assert.Equal("1", filtered[0].GetAttribute("aria-posinset"));
    }

    // ------------------------------------------- SEL-10: what the select-only combobox advertises

    [Fact]
    public void A_searchable_combobox_advertises_list_autocomplete_and_is_not_readonly()
    {
        var cut = Render<Select<string>>(p => p.Add(s => s.Options, Opts("Apple")));

        var input = cut.Find(InputSelector);
        Assert.Equal("list", input.GetAttribute("aria-autocomplete"));
        Assert.False(input.HasAttribute("readonly"));
        Assert.False(input.HasAttribute("aria-readonly"));
    }

    [Fact]
    public void A_select_only_combobox_drops_aria_autocomplete_and_declares_itself_not_read_only()
    {
        // readonly is a real requirement (nothing else stops the browser putting characters in the
        // box), but announcing "read only" about a control the arrows/Enter/Space/type-ahead all
        // change is a lie. aria-readonly="false" is the explicit override.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.ShowSearch, false));

        var input = cut.Find(InputSelector);
        Assert.False(input.HasAttribute("aria-autocomplete"));
        Assert.True(input.HasAttribute("readonly"));
        Assert.Equal("false", input.GetAttribute("aria-readonly"));
    }

    // ---------------------------------------------------------- SEL-7: Space on an open select

    [Fact]
    public void Space_selects_the_active_option_on_an_open_select_only_combobox()
    {
        // APG's select-only combobox: Space commits the active option. It used to be dead once open.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.ShowSearch, false)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find(InputSelector);
        input.KeyDown(Key(" "));                 // opens, highlight on Apple
        Assert.Single(cut.FindAll("[role=listbox]"));

        cut.Find(InputSelector).KeyDown(Key("ArrowDown")); // -> Banana
        cut.Find(InputSelector).KeyDown(Key(" "));

        Assert.Equal("Banana", selected);
        Assert.Empty(cut.FindAll("[role=listbox]")); // committing a single select closes it
    }

    [Fact]
    public void Space_still_belongs_to_the_search_text_on_a_searchable_combobox()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(Key(" "));

        Assert.Null(selected);
        Assert.Single(cut.FindAll("[role=listbox]"));
    }

    // ------------------------------------------------------------------- SEL-8: backdrop focus

    [Fact]
    public void Dismissing_by_outside_click_puts_focus_back_on_the_combobox()
    {
        // The backdrop is click-focusable (tabindex=-1, needed for WebKit's tap-to-click synthesis)
        // and closing deletes it, so focus fell to <body>: Tab restarted at the top of the page.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple"))
            .Add(s => s.DefaultOpen, true)); // DefaultOpen skips OpenAsync, so no focus call yet

        Assert.Equal(0, JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)));

        cut.Find(".wss-select-backdrop").Click();

        Assert.Empty(cut.FindAll("[role=listbox]"));
        Assert.Equal(1, JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)));
    }

    // ------------------------------------------------- SEL-12: modifiers and the APG Alt+Arrows

    [Fact]
    public void Ctrl_F_reaches_the_browser_instead_of_the_type_ahead()
    {
        // e.Key is "f" for Ctrl+F, so the unguarded type-ahead opened the dropdown and moved the
        // highlight while the user was reaching for Find.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Fig", "Grape"))
            .Add(s => s.ShowSearch, false));

        cut.Find(InputSelector).KeyDown(new KeyboardEventArgs { Key = "f", CtrlKey = true });

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void A_shifted_letter_still_types_ahead()
    {
        // Shift is deliberately not excluded — it is how a capital letter is typed.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Fig", "Grape"))
            .Add(s => s.ShowSearch, false)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(new KeyboardEventArgs { Key = "G", ShiftKey = true });
        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal("Grape", selected);
    }

    [Fact]
    public void Alt_ArrowDown_opens_without_moving_the_highlight()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(Key("ArrowDown")); // -> Banana
        cut.Find(InputSelector).KeyDown(new KeyboardEventArgs { Key = "ArrowDown", AltKey = true });
        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal("Banana", selected); // a plain ArrowDown would have moved on to Cherry
    }

    [Fact]
    public void Alt_ArrowDown_opens_a_closed_combobox()
    {
        var cut = Render<Select<string>>(p => p.Add(s => s.Options, Opts("Apple", "Banana")));

        cut.Find(InputSelector).KeyDown(new KeyboardEventArgs { Key = "ArrowDown", AltKey = true });

        Assert.Single(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Alt_ArrowUp_closes_the_popup()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.DefaultOpen, true));

        cut.Find(InputSelector).KeyDown(new KeyboardEventArgs { Key = "ArrowUp", AltKey = true });

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    // ------------------------------------------------------------- SEL-13: the overflow chip

    [Fact]
    public void The_MaxTagCount_chip_reads_a_sentence_instead_of_plus_n_dot_dot_dot()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.Values, new List<string> { "Apple", "Banana", "Cherry" })
            .Add(s => s.MaxTagCount, 1));

        var chip = cut.Find(".wss-select-selection-item-rest");
        // The visible glyph text is unchanged, and hidden from assistive tech.
        var visible = chip.QuerySelector(".wss-select-selection-item-content")!;
        Assert.Equal("true", visible.GetAttribute("aria-hidden"));
        Assert.Contains("+ 2", visible.TextContent);
        Assert.Equal("2 more selected", chip.QuerySelector(".wss-sr-only")!.TextContent);
    }

    [Fact]
    public void The_MaxTagCount_chip_label_is_localizable()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana", "Cherry"))
            .Add(s => s.Values, new List<string> { "Apple", "Banana", "Cherry" })
            .Add(s => s.MaxTagCount, 1)
            .Add(s => s.MaxTagCountLabelFormat, "{0} weitere ausgewählt"));

        Assert.Equal("2 weitere ausgewählt",
            cut.Find(".wss-select-selection-item-rest .wss-sr-only").TextContent);
    }

    // ------------------------------------------- SEL-11: no per-option repetition of the tooltip

    [Fact]
    public void EditSelectEnum_options_do_not_repeat_the_field_tooltip()
    {
        var model = new TooltipModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var options = cut.FindAll("option");
        Assert.NotEmpty(options);
        Assert.All(options, o => Assert.False(o.HasAttribute("title")));
        // The tooltip itself is untouched — it still reaches everyone through the label's trigger.
        Assert.Contains("Pick the ticket's urgency", cut.Markup);
    }

    [Fact]
    public void EditSelectString_options_do_not_repeat_the_field_tooltip()
    {
        var model = new TooltipModel();
        Expression<Func<string>> field = () => model.Colour;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string>>(0);
            b.AddAttribute(1, "Value", model.Colour);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "Red", "Green" });
            b.CloseComponent();
        }));

        var options = cut.FindAll("option");
        Assert.NotEmpty(options);
        Assert.All(options, o => Assert.False(o.HasAttribute("title")));
        Assert.Contains("Pick a colour", cut.Markup);
    }
}
