using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the AntD 4.x parity batch on the <see cref="Select{TValue}"/> engine that isn't
/// option-grouping (<see cref="SelectGroupHeaderTests"/>) or controlled Open
/// (<see cref="SelectControlledOpenTests"/>): <c>Loading</c>/<c>ShowArrow</c> precedence,
/// <c>FilterOption</c>, <c>EmptyContent</c>, <c>DropdownFooter</c>, the <c>Borderless</c> variant,
/// and a DOM-stability regression guard for default (unused-new-params) rendering.
/// </summary>
public class SelectAntdParityTests : TestContext
{
    public SelectAntdParityTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    static List<SelectOption<string>> Opts(params (string Value, bool Disabled)[] items) =>
        items.Select(i => new SelectOption<string>(i.Value, i.Value, i.Disabled)).ToList();

    // ----- DOM stability -----------------------------------------------------------------------

    [Fact]
    public void Default_params_render_the_legacy_markup_with_none_of_the_new_features_present()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Id, "sel1")
            .Add(s => s.Options, Opts(("A", false))));

        // ShowArrow defaults true, Loading defaults false -> the plain chevron, no spinner.
        Assert.Single(cut.FindAll(".wss-select-arrow"));
        Assert.Empty(cut.FindAll(".wss-icon-spin"));
        Assert.Null(cut.Find(".wss-select").GetAttribute("aria-busy"));

        // No group headers, footer, or borderless class when those params are unused.
        Assert.Empty(cut.FindAll(".wss-select-item-group-label"));
        Assert.Empty(cut.FindAll(".wss-select-dropdown-footer"));
        Assert.DoesNotContain("wss-select-borderless", cut.Find(".wss-select").ClassList);

        cut.Find(".wss-select").Click(); // open to render the dropdown/options
        Assert.Single(cut.FindAll(".wss-select-item-option"));
        Assert.Empty(cut.FindAll("[role=presentation]"));
        Assert.Empty(cut.FindAll(".wss-select-item-empty")); // the one option matches -- no empty state
    }

    // ----- Loading / ShowArrow ------------------------------------------------------------------

    [Fact]
    public void Loading_shows_a_spinner_in_the_arrow_slot_and_sets_aria_busy()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Loading, true));

        Assert.NotEmpty(cut.FindAll(".wss-select-arrow .wss-icon-spin"));
        Assert.Equal("true", cut.Find(".wss-select").GetAttribute("aria-busy"));
    }

    [Fact]
    public void Loading_shows_the_spinner_even_when_ShowArrow_is_false()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Loading, true)
            .Add(s => s.ShowArrow, false));

        Assert.NotEmpty(cut.FindAll(".wss-select-arrow .wss-icon-spin"));
    }

    [Fact]
    public void ShowArrow_false_hides_the_arrow_when_not_loading()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.ShowArrow, false));

        Assert.Empty(cut.FindAll(".wss-select-arrow"));
    }

    // ----- FilterOption ---------------------------------------------------------------------------

    [Fact]
    public void FilterOption_replaces_the_default_label_match()
    {
        // Default Label.Contains("an", OrdinalIgnoreCase) would match only "Banana" -- this predicate
        // ignores the search text entirely and excludes it instead, proving it's authoritative.
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false), ("Banana", false)))
            .Add(s => s.FilterOption, (Func<string, SelectOption<string>, bool>)((_, o) => o.Value != "Banana"))
            .Add(s => s.DefaultOpen, true));

        cut.Find("input.wss-select-selection-search-input").Input("an");

        var labels = cut.FindAll(".wss-select-item-option-content").Select(e => e.TextContent).ToList();
        Assert.Contains("Apple", labels);
        Assert.DoesNotContain("Banana", labels);
    }

    [Fact]
    public void FilterOption_always_true_disables_client_filtering_for_server_driven_search()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false), ("Banana", false)))
            .Add(s => s.FilterOption, (Func<string, SelectOption<string>, bool>)((_, _) => true))
            .Add(s => s.DefaultOpen, true));

        cut.Find("input.wss-select-selection-search-input").Input("zzz-matches-nothing");

        // The default Label.Contains would have filtered both out; FilterOption keeps them all.
        Assert.Equal(2, cut.FindAll(".wss-select-item-option").Count);
    }

    [Fact]
    public void Swapping_FilterOption_with_Options_unchanged_refreshes_an_open_list_immediately()
    {
        // OnParametersSet previously only rebuilt _filtered on an Options/Values reference change; a
        // swapped FilterOption delegate left an open list stale until the next keystroke/reopen.
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false), ("Banana", false)))
            .Add(s => s.FilterOption, (Func<string, SelectOption<string>, bool>)((_, o) => o.Value != "Banana"))
            .Add(s => s.DefaultOpen, true));

        var labels = cut.FindAll(".wss-select-item-option-content").Select(e => e.TextContent).ToList();
        Assert.Contains("Apple", labels);
        Assert.DoesNotContain("Banana", labels);

        // New delegate reference, Options unchanged, no keystroke and no reopen in between.
        cut.SetParametersAndRender(p => p
            .Add(s => s.FilterOption, (Func<string, SelectOption<string>, bool>)((_, o) => o.Value != "Apple")));

        labels = cut.FindAll(".wss-select-item-option-content").Select(e => e.TextContent).ToList();
        Assert.DoesNotContain("Apple", labels);
        Assert.Contains("Banana", labels);
    }

    // ----- EmptyContent -----------------------------------------------------------------------------

    [Fact]
    public void EmptyText_renders_when_EmptyContent_is_unset()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false)))
            .Add(s => s.DefaultOpen, true));

        cut.Find("input.wss-select-selection-search-input").Input("zzz");

        Assert.Equal("No data", cut.Find(".wss-select-item-empty").TextContent);
    }

    [Fact]
    public void EmptyContent_wins_over_EmptyText_when_set()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false)))
            .Add(s => s.EmptyText, "No data")
            .Add(s => s.EmptyContent, (RenderFragment)(b => b.AddContent(0, "Nothing here, try again")))
            .Add(s => s.DefaultOpen, true));

        cut.Find("input.wss-select-selection-search-input").Input("zzz");

        var emptyDiv = cut.Find(".wss-select-item-empty");
        Assert.Contains("Nothing here, try again", emptyDiv.TextContent);
        Assert.DoesNotContain("No data", emptyDiv.TextContent);
    }

    // ----- DropdownFooter ---------------------------------------------------------------------------

    [Fact]
    public void DropdownFooter_renders_after_the_option_list_and_its_clicks_do_not_select_or_close()
    {
        var footerClicked = false;
        string? selected = null;
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("Apple", false)))
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v)
            .Add(s => s.DropdownFooter, (RenderFragment)(b =>
            {
                b.OpenElement(0, "button");
                b.AddAttribute(1, "type", "button");
                b.AddAttribute(2, "onclick", EventCallback.Factory.Create(this, () => footerClicked = true));
                b.AddContent(3, "Add item");
                b.CloseElement();
            })));

        var footer = cut.Find(".wss-select-dropdown-footer");
        Assert.Equal("presentation", footer.GetAttribute("role"));

        cut.Find(".wss-select-dropdown-footer button").Click();

        Assert.True(footerClicked);
        Assert.Null(selected); // clicking the footer never selects an option
        Assert.NotEmpty(cut.FindAll("[role=listbox]")); // ...or closes the dropdown
    }

    // ----- Borderless variant -----------------------------------------------------------------------

    [Fact]
    public void Borderless_variant_adds_the_borderless_class_and_default_stays_outlined()
    {
        var outlined = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false))));
        Assert.DoesNotContain("wss-select-borderless", outlined.Find(".wss-select").ClassList);

        var borderless = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts(("A", false)))
            .Add(s => s.Variant, SelectVariant.Borderless));
        Assert.Contains("wss-select-borderless", borderless.Find(".wss-select").ClassList);
    }
}
