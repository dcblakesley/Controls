using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for <see cref="SelectOption{TValue}.Group"/> — the flattened, virtualized dropdown list
/// interleaves a non-interactive header before the first option of each contiguous run of a shared
/// group name. Keyboard navigation (Move*/FindLabelPrefix) must skip header rows entirely.
/// </summary>
public class SelectGroupHeaderTests : TestContext
{
    public SelectGroupHeaderTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    static SelectOption<string> Opt(string value, string? group = null, bool disabled = false) =>
        new(value, value, disabled) { Group = group };

    [Fact]
    public void Options_with_a_shared_Group_render_one_header_before_each_run()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Banana", "Fruit"),
                Opt("Carrot", "Vegetable"),
            })
            .Add(s => s.DefaultOpen, true));

        var headers = cut.FindAll(".wss-select-item-group-label");
        Assert.Equal(2, headers.Count);
        Assert.Equal("Fruit", headers[0].TextContent);
        Assert.Equal("Vegetable", headers[1].TextContent);

        // Order preserved: Fruit header, its 2 options, then the Vegetable header, its option.
        // (.wss-select-item covers both header and option rows, excluding Virtualize's own spacer
        // elements, which carry no class of their own.)
        var rows = cut.FindAll(".wss-select-item");
        Assert.Equal(5, rows.Count);
        Assert.Contains("Fruit", rows[0].TextContent);
        Assert.Contains("Apple", rows[1].TextContent);
        Assert.Contains("Banana", rows[2].TextContent);
        Assert.Contains("Vegetable", rows[3].TextContent);
        Assert.Contains("Carrot", rows[4].TextContent);
    }

    [Fact]
    public void Ungrouped_options_render_with_no_header_in_their_given_order()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple"), Opt("Banana") })
            .Add(s => s.DefaultOpen, true));

        Assert.Empty(cut.FindAll(".wss-select-item-group-label"));
        Assert.Equal(2, cut.FindAll(".wss-select-item-option").Count);
    }

    [Fact]
    public void Header_rows_are_presentation_only_never_role_option()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple", "Fruit") })
            .Add(s => s.DefaultOpen, true));

        var header = cut.Find(".wss-select-item-group-label");
        Assert.Equal("presentation", header.GetAttribute("role"));
        Assert.Equal("true", header.GetAttribute("aria-hidden"));
        // Exactly one role=option element (the real option) -- the header never gets one.
        Assert.Single(cut.FindAll("[role=option]"));
    }

    [Fact]
    public void A_header_disappears_when_none_of_its_options_match_the_filter_but_stays_when_one_does()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Banana", "Fruit"),
                Opt("Carrot", "Vegetable"),
            })
            .Add(s => s.DefaultOpen, true));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.Input("Carrot"); // matches only the Vegetable group -- the Fruit header must disappear

        var headers = cut.FindAll(".wss-select-item-group-label");
        Assert.Single(headers);
        Assert.Equal("Vegetable", headers[0].TextContent);

        input.Input("Apple"); // matches only one Fruit option -- its header still shows
        headers = cut.FindAll(".wss-select-item-group-label");
        Assert.Single(headers);
        Assert.Equal("Fruit", headers[0].TextContent);
    }

    [Fact]
    public void ArrowDown_navigation_skips_a_header_row_and_lands_on_its_first_option()
    {
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple", "Fruit"), Opt("Banana", "Fruit") })
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // open -> SetInitialActive must skip the header
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Apple", selected); // not the "Fruit" header
    }

    [Fact]
    public void End_key_lands_on_the_last_option_not_a_trailing_header()
    {
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple", "Fruit"), Opt("Carrot", "Vegetable") })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.KeyDown(new KeyboardEventArgs { Key = "End" });
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Carrot", selected);
    }

    [Fact]
    public void TypeAhead_does_not_match_a_group_header_as_a_jump_target()
    {
        // ShowSearch=false routes typed letters through type-ahead (FindLabelPrefix) instead of the
        // filter text box -- "F" must never resolve to the "Fruit" header, only to an option label.
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Fig", "Fruit"), Opt("Grape", "Fruit") })
            .Add(s => s.ShowSearch, false)
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "F" });
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Fig", selected);
    }

    [Fact]
    public void Same_group_name_in_two_non_contiguous_runs_gets_a_separate_header_per_run()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit"),
                Opt("Carrot", "Vegetable"),
                Opt("Banana", "Fruit"), // a second, non-contiguous "Fruit" run
            })
            .Add(s => s.DefaultOpen, true));

        var headers = cut.FindAll(".wss-select-item-group-label");
        Assert.Equal(3, headers.Count); // Fruit, Vegetable, Fruit again -- one per contiguous run
        Assert.Equal("Fruit", headers[0].TextContent);
        Assert.Equal("Vegetable", headers[1].TextContent);
        Assert.Equal("Fruit", headers[2].TextContent);
    }

    [Fact]
    public void Disabled_option_within_a_group_is_still_skipped_by_keyboard_navigation()
    {
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>>
            {
                Opt("Apple", "Fruit", disabled: true),
                Opt("Banana", "Fruit"),
            })
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find("input.wss-select-selection-search-input");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowDown" }); // open -> skips the header AND the disabled option
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Banana", selected);
    }
}
