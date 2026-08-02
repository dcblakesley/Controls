using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Direct tests for the <see cref="Select{TValue}"/> engine — the most complex control, previously
/// exercised only indirectly through its form wrappers. Drives keyboard navigation, type-ahead, tags,
/// MaxTagCount, clear and the disabled no-op. Assertions go through the value callbacks rather than the
/// rendered option rows, so they're robust against the virtualized dropdown only rendering rows in view.
/// </summary>
public class SelectEngineTests : BunitContext
{
    public SelectEngineTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the scroll/placement JS imports

    static List<SelectOption<string>> Opts(params (string Value, bool Disabled)[] items) =>
        items.Select(i => new SelectOption<string>(i.Value, i.Value, i.Disabled)).ToList();

    [Fact]
    public void ArrowDown_then_Enter_selects_the_next_enabled_option_skipping_disabled()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Low", false), ("Medium", true), ("High", false)))
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.Value, "Low")
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("High", selected); // Medium was disabled and skipped
    }

    [Fact]
    public void ArrowDown_wraps_from_the_last_option_back_to_the_first()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false), ("B", false)))
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // 0 -> 1
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // 1 -> wraps to 0
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("A", selected);
    }

    [Fact]
    public void TypeAhead_jumps_to_an_option_by_typed_letter_when_not_searchable()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false), ("Banana", false), ("Cherry", false)))
            .Add(s => s.ShowSearch, false)
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "c" }); // -> Cherry
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Cherry", selected);
    }

    [Fact]
    public void Tags_mode_commits_typed_text_as_a_new_value_on_Enter()
    {
        IEnumerable<string>? values = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, Opts())
            .Add(s => s.Values, new List<string>())
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => values = v));

        cut.Find("input.wss-select-selection-search-input").Input("custom");
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotNull(values);
        Assert.Contains("custom", values!);
    }

    [Fact]
    public void Tags_mode_ignores_typed_text_for_a_non_string_value_without_a_factory()
    {
        var fired = false;
        var cut = Render<Select<int>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, new List<SelectOption<int>>())
            .Add(s => s.Values, new List<int>())
            .Add(s => s.ValuesChanged, (IEnumerable<int> _) => fired = true));

        cut.Find("input.wss-select-selection-search-input").Input("abc");
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.False(fired); // can't turn "abc" into an int without a TagValueFactory — silent no-op, not a throw
    }

    [Fact]
    public void Backspace_with_empty_search_removes_the_last_tag()
    {
        IEnumerable<string>? values = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false), ("B", false)))
            .Add(s => s.Values, new List<string> { "A", "B" })
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => values = v));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Backspace" });

        Assert.NotNull(values);
        Assert.Equal(new[] { "A" }, values!);
    }

    [Fact]
    public void MaxTagCount_collapses_the_overflow_into_a_rest_indicator()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false), ("B", false), ("C", false)))
            .Add(s => s.Values, new List<string> { "A", "B", "C" })
            .Add(s => s.MaxTagCount, 2));

        // Two real (removable) tags plus a non-removable "+ 1 ..." rest indicator.
        Assert.Equal(2, cut.FindAll("button.wss-select-selection-item-remove").Count);
        Assert.Contains("1", cut.Find(".wss-select-selection-item-rest").TextContent);
    }

    [Fact]
    public void Disabled_multi_select_renders_tags_without_remove_buttons()
    {
        // RemoveAsync no-ops when disabled, so rendering the buttons left focusable,
        // active-looking controls that did nothing.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false), ("B", false)))
            .Add(s => s.Values, new List<string> { "A", "B" })
            .Add(s => s.Disabled, true));

        Assert.Equal(2, cut.FindAll(".wss-select-selection-item").Count); // tags still shown
        Assert.Empty(cut.FindAll("button.wss-select-selection-item-remove"));
    }

    [Fact]
    public void Space_opens_a_closed_non_searchable_select()
    {
        // ARIA combobox pattern: the non-searchable select's input is readonly, so Space has no
        // text-entry meaning and must open the dropdown (searchable mode keeps Space for typing).
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.ShowSearch, false));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Single(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Disabled_select_does_not_open_on_click()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Disabled, true));

        cut.Find(".wss-select").Click();

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Disabled_select_ignores_ArrowDown_and_does_not_open()
    {
        // OnKeyDownAsync has no native-disabled backstop of its own to rely on in this test (bUnit
        // dispatches straight to the handler regardless of the input's disabled attribute), so this
        // exercises the handler's own Disabled guard directly.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Disabled, true));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Disabled_select_ignores_typed_search_input_and_does_not_open()
    {
        var searched = false;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Disabled, true)
            .Add(s => s.OnSearch, (string _) => searched = true));

        cut.Find("input.wss-select-selection-search-input").Input("a");

        Assert.Empty(cut.FindAll("[role=listbox]"));
        Assert.False(searched);
    }

    [Fact]
    public void Disabled_blocks_SelectAsync_even_if_an_option_row_is_still_in_the_DOM()
    {
        // Defense in depth for the HIGH defect: OnParametersSetAsync's Disabled-closes invariant means
        // a real render can never leave the dropdown open while Disabled, so to prove SelectAsync's own
        // guard actually holds (not just the invariant that is supposed to make it moot) this bypasses
        // the normal parameter lifecycle -- opens while enabled, then flips the Disabled property
        // directly (skipping Render/OnParametersSetAsync entirely) so the option row
        // from the prior render is still sitting in the DOM, and clicks it.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(".wss-select").Click(); // opens while enabled
        Assert.NotEmpty(cut.FindAll(".wss-select-item-option"));

#pragma warning disable BL0005 // deliberately bypassing the parameter lifecycle -- see comment above
        cut.Instance.Disabled = true; // bypasses OnParametersSetAsync's Disabled-closes invariant
#pragma warning restore BL0005

        cut.Find(".wss-select-item-option").Click();

        Assert.Null(selected); // SelectAsync's own Disabled guard blocked the mutation
    }

    [Fact]
    public void Disabled_blocks_ClearAsync_even_if_the_clear_button_is_still_in_the_DOM()
    {
        // Same rationale as the SelectAsync test above -- ShowClear already hides this button when
        // Disabled, so this proves ClearAsync's own guard rather than that suppression.
        string? selected = "A";
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Value, "A")
            .Add(s => s.AllowClear, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        Assert.NotEmpty(cut.FindAll("button.wss-select-clear"));

#pragma warning disable BL0005 // deliberately bypassing the parameter lifecycle -- see comment above
        cut.Instance.Disabled = true; // bypasses ShowClear's suppression, proving ClearAsync's own guard
#pragma warning restore BL0005

        cut.Find("button.wss-select-clear").Click();

        Assert.Equal("A", selected); // ClearAsync's own Disabled guard blocked the mutation
    }

    [Fact]
    public void Clear_resets_a_single_select_to_default()
    {
        string? selected = "A";
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Value, "A")
            .Add(s => s.AllowClear, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find("button.wss-select-clear").Click();

        Assert.Null(selected);
    }

    [Fact]
    public void Opening_by_click_highlights_the_current_selection_not_the_first_option()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false), ("B", false), ("C", false)))
            .Add(s => s.Value, "C")
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(".wss-select").Click(); // OpenAsync -> SetInitialActive lands on the selected "C"
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("C", selected);
    }

    [Fact]
    public void Opening_with_a_disabled_first_option_highlights_the_first_enabled_one()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", true), ("B", false)))
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("B", selected); // previously the disabled "A" was highlighted and Enter did nothing
    }

    [Fact]
    public void Enter_on_a_closed_select_opens_the_dropdown()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Tags_mode_commits_typed_text_even_when_the_only_match_is_disabled()
    {
        IEnumerable<string>? values = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, Opts(("custom-disabled", true)))
            .Add(s => s.Values, new List<string>())
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => values = v));

        cut.Find("input.wss-select-selection-search-input").Input("custom");
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotNull(values);
        Assert.Contains("custom", values!); // Enter fell through the disabled match to the tag commit
    }

    [Fact]
    public void Tags_mode_removes_an_unselected_free_tag_from_the_options()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Values, new List<string>()));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.Input("typo-tag");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });     // commit the free tag
        input.KeyDown(new KeyboardEventArgs { Key = "Backspace" }); // remove it again

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // open
        Assert.DoesNotContain("typo-tag", cut.Markup); // no zombie option lingering in the dropdown
    }

    [Fact]
    public void Rerender_with_unchanged_Values_reference_keeps_the_selection_mirror()
    {
        // Uncontrolled multi usage (no Values rebinding): a parent re-render used to re-mirror the
        // stale Values parameter and wipe in-flight selections.
        var values = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false), ("B", false)))
            .Add(s => s.Values, values));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" }); // select "A"
        Assert.Single(cut.FindAll("button.wss-select-selection-item-remove"));

        cut.Render(p => p.Add(s => s.Values, values)); // same reference — no reset
        Assert.Single(cut.FindAll("button.wss-select-selection-item-remove"));
    }

    [Fact]
    public void EditMultiSelect_throws_on_single_mode()
    {
        var model = new { Items = new List<string>() };
        Assert.Throws<InvalidOperationException>(() =>
            Render<EditMultiSelect<string>>(p => p
                .Add(c => c.Value, model.Items)
                .Add(c => c.ValueExpression, () => model.Items)
                .Add(c => c.Mode, SelectMode.Single)));
    }

    [Fact]
    public void Select_shows_label_and_clear_when_single_value_is_the_type_default()
    {
        var cut = Render<Select<int>>(p => p
            .Add(s => s.Options, new List<SelectOption<int>> { new(0, "Zero"), new(1, "One") })
            .Add(s => s.Value, 0)            // default(int) — but a real option
            .Add(s => s.AllowClear, true));

        // The default value resolves to a real option, so its label shows (not the placeholder) and
        // the clear button is offered — previously HasSingleValue == "!= default" hid both.
        Assert.Contains("Zero", cut.Find(".wss-select-selection-item").TextContent);
        Assert.NotEmpty(cut.FindAll("button.wss-select-clear"));
    }

    [Fact]
    public void Empty_string_value_shows_the_placeholder_and_offers_no_clear_button()
    {
        // default(string) is null, not "", so the old "resolves to an option OR != default" test counted
        // "" as a real selection: an empty selection-item label instead of the placeholder, plus a live
        // clear button that appeared to do nothing.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Value, "")
            .Add(s => s.AllowClear, true));

        Assert.NotEmpty(cut.FindAll(".wss-select-selection-placeholder"));
        Assert.Empty(cut.FindAll(".wss-select-selection-item"));
        Assert.Empty(cut.FindAll("button.wss-select-clear"));
    }

    [Fact]
    public void Empty_string_still_counts_as_selected_when_a_real_option_carries_it()
    {
        // The FindOption arm wins over the empty-string exclusion, so an explicit "None" option keeps
        // working: its label shows and the clear button is offered, exactly like any other selection.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { new("", "None"), new("A", "A") })
            .Add(s => s.Value, "")
            .Add(s => s.AllowClear, true));

        Assert.Equal("None", cut.Find(".wss-select-selection-item").TextContent);
        Assert.Empty(cut.FindAll(".wss-select-selection-placeholder"));
        Assert.NotEmpty(cut.FindAll("button.wss-select-clear"));
    }

    [Fact]
    public void Pruning_a_tag_keeps_the_label_of_a_same_valued_option_still_supplied_by_Options()
    {
        // The value -> option lookup is derived from Options + the user's tags. Pruning a deselected tag
        // used to Remove() its key outright, deleting the entry Options still supplied for the same
        // value -- after which the re-selected option's tag fell back to value.ToString().
        var values = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, new List<SelectOption<string>>())
            .Add(s => s.Values, values)
            // Mirrors a @bind-Values consumer while keeping the same list instance, so the
            // reference-guarded re-mirror below sees the committed tag rather than resetting it.
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => { values.Clear(); values.AddRange(v); }));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.Input("X");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" }); // commit the free tag "X"

        // The server echoes the same value back as a real, better-labelled option.
        cut.Render(p => p.Add(s => s.Options, new List<SelectOption<string>> { new("X", "X-label") }));

        cut.Find("button.wss-select-selection-item-remove").Click(); // deselect -> prunes the tag option
        Assert.Empty(cut.FindAll("button.wss-select-selection-item-remove"));

        cut.Find(".wss-select-item-option").Click(); // re-select it from the list
        Assert.Equal("X-label", cut.Find(".wss-select-selection-item-content").TextContent);
    }

    [Fact]
    public void Clearing_tags_keeps_the_label_of_a_same_valued_option_still_supplied_by_Options()
    {
        // ClearAsync's tag teardown had the same targeted-removal bug as PruneTagOption.
        var values = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, new List<SelectOption<string>>())
            .Add(s => s.Values, values)
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => { values.Clear(); values.AddRange(v); }));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.Input("X");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        cut.Render(p => p.Add(s => s.Options, new List<SelectOption<string>> { new("X", "X-label") }));

        cut.Find("button.wss-select-clear").Click();
        Assert.Empty(cut.FindAll(".wss-select-selection-item-content"));

        cut.Find(".wss-select-item-option").Click();
        Assert.Equal("X-label", cut.Find(".wss-select-selection-item-content").TextContent);
    }

    [Fact]
    public void Clicking_the_dropdown_panel_chrome_does_not_toggle_a_non_searchable_select_closed()
    {
        // The panel stops click propagation, so the parts of it carrying no handler of their own (a group
        // header here, but equally the empty row or the panel's padding) can no longer bubble into the
        // wrapper's OnWrapperClickAsync -- which, with ShowSearch=false, reads as a second click on the
        // control and closes it. This is the hazard the DropdownFooter's own stopPropagation documents.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                new("A", "A") { Group = "Letters" },
                new("B", "B") { Group = "Letters" },
            })
            .Add(s => s.ShowSearch, false)
            .Add(s => s.DefaultOpen, true));

        try
        {
            cut.Find(".wss-select-item-group-label").Click();
        }
        catch (Bunit.MissingEventHandlerException)
        {
            // bUnit models stopPropagation by cutting the bubble path: the header has no onclick of its
            // own, so with the panel stopping propagation the click reaches no handler at all and bUnit
            // reports the miss rather than dispatching. Either way the wrapper's toggle must not run --
            // that's what the open-state assertion below pins.
        }

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Clicking_an_option_in_a_non_searchable_select_still_selects_and_closes()
    {
        // The other half of the panel-stopPropagation change: option rows carry their own handler (and
        // their own stopPropagation), so the panel's directive never gets in their way.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { new("A", "A") { Group = "Letters" } })
            .Add(s => s.ShowSearch, false)
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(".wss-select-item-option").Click();

        Assert.Equal("A", selected);
        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Pill_variant_adds_the_pill_class_and_default_stays_outlined()
    {
        var outlined = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));
        Assert.DoesNotContain("wss-select-pill", outlined.Find(".wss-select").ClassList);

        var pill = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Variant, SelectVariant.Pill));
        Assert.Contains("wss-select-pill", pill.Find(".wss-select").ClassList);
    }

    [Fact]
    public void Prefix_renders_before_the_selection_wrap_and_is_absent_by_default()
    {
        var plain = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));
        Assert.Empty(plain.FindAll(".wss-select-prefix"));

        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Prefix, (RenderFragment)(b => b.AddContent(0, "ICON"))));

        // The prefix is the selector's first child so it leads the flex row; the value/search
        // stack sits beside it inside the selection-wrap.
        var selectorChildren = cut.Find(".wss-select-selector").Children;
        Assert.Equal("wss-select-prefix", selectorChildren[0].ClassName);
        Assert.Equal("ICON", selectorChildren[0].TextContent);
        Assert.Contains("wss-select-selection-wrap", selectorChildren[1].ClassList);
    }

    [Fact]
    public void Prefix_renders_in_multiple_mode_too()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Values, new List<string> { "A" })
            .Add(s => s.Prefix, (RenderFragment)(b => b.AddContent(0, "ICON"))));

        Assert.Equal("ICON", cut.Find(".wss-select-prefix").TextContent);
        // Tags render inside the wrap, so they never slide under the prefix column.
        Assert.NotNull(cut.Find(".wss-select-selection-wrap .wss-select-selection-item"));
    }

    [Fact]
    public void AriaErrorMessage_renders_on_the_search_input_when_set_and_is_omitted_by_default()
    {
        // Same shape as AriaRequired/AriaInvalid/AriaDescribedBy: a direct pass-through onto the
        // search input, with null (the default) omitting the attribute entirely rather than
        // rendering an empty one. EditSelectSearch/EditMultiSelect forward it as
        // "@(IsInvalid ? _errorMsgId : null)"; this pins the engine's own half of that contract.
        var plain = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));
        Assert.False(plain.Find("input.wss-select-selection-search-input").HasAttribute("aria-errormessage"));

        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.AriaErrorMessage, "error-msg-Field"));

        Assert.Equal("error-msg-Field", cut.Find("input.wss-select-selection-search-input").GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void TypeAhead_starts_from_an_empty_prefix_after_a_close_and_reopen()
    {
        // The prefix buffer only self-cleared after a 1s pause between keystrokes, so a close followed
        // by a quick reopen resumed the previous session's accumulated prefix: the first letter typed
        // then matched (or, as here, failed to match) against something the user never typed.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Alpha", false), ("Beta", false), ("AB", false)))
            .Add(s => s.ShowSearch, false)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.KeyDown(new KeyboardEventArgs { Key = "a" });      // prefix "a"  -> Alpha
        input.KeyDown(new KeyboardEventArgs { Key = "b" });      // prefix "ab" -> AB
        input.KeyDown(new KeyboardEventArgs { Key = "Escape" }); // close

        cut.Find(".wss-select").Click();                         // reopen, well inside the 1s window
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "b" });
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // A stale buffer would have made this "abb", matched nothing, and left Enter on whatever the
        // reopen highlighted (Alpha).
        Assert.Equal("Beta", selected);
    }
}
