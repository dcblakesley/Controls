using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Direct tests for the <see cref="Select{TValue}"/> engine — the most complex control, previously
/// exercised only indirectly through its form wrappers. Drives keyboard navigation, type-ahead, tags,
/// MaxTagCount, clear and the disabled no-op. Assertions go through the value callbacks rather than the
/// rendered option rows, so they're robust against the virtualized dropdown only rendering rows in view.
/// </summary>
public class SelectEngineTests : TestContext
{
    public SelectEngineTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the scroll/placement JS imports

    static List<SelectOption<string>> Opts(params (string Value, bool Disabled)[] items) =>
        items.Select(i => new SelectOption<string>(i.Value, i.Value, i.Disabled)).ToList();

    [Fact]
    public void ArrowDown_then_Enter_selects_the_next_enabled_option_skipping_disabled()
    {
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<int>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts(("A", false), ("B", false), ("C", false)))
            .Add(s => s.Values, new List<string> { "A", "B", "C" })
            .Add(s => s.MaxTagCount, 2));

        // Two real (removable) tags plus a non-removable "+ 1 ..." rest indicator.
        Assert.Equal(2, cut.FindAll("button.wss-select-selection-item-remove").Count);
        Assert.Contains("1", cut.Find(".wss-select-selection-item-rest").TextContent);
    }

    [Fact]
    public void Disabled_select_does_not_open_on_click()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Disabled, true));

        cut.Find(".wss-select").Click();

        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Clear_resets_a_single_select_to_default()
    {
        string? selected = "A";
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
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
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", true), ("B", false)))
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(".wss-select").Click();
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("B", selected); // previously the disabled "A" was highlighted and Enter did nothing
    }

    [Fact]
    public void Enter_on_a_closed_select_opens_the_dropdown()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));

        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
    }

    [Fact]
    public void Tags_mode_commits_typed_text_even_when_the_only_match_is_disabled()
    {
        IEnumerable<string>? values = null;
        var cut = RenderComponent<Select<string>>(p => p
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
    public void Select_shows_label_and_clear_when_single_value_is_the_type_default()
    {
        var cut = RenderComponent<Select<int>>(p => p
            .Add(s => s.Options, new List<SelectOption<int>> { new(0, "Zero"), new(1, "One") })
            .Add(s => s.Value, 0)            // default(int) — but a real option
            .Add(s => s.AllowClear, true));

        // The default value resolves to a real option, so its label shows (not the placeholder) and
        // the clear button is offered — previously HasSingleValue == "!= default" hid both.
        Assert.Contains("Zero", cut.Find(".wss-select-selection-item").TextContent);
        Assert.NotEmpty(cut.FindAll("button.wss-select-clear"));
    }
}
