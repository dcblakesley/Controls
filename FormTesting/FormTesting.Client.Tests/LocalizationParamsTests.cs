namespace FormTesting.Client.Tests;

/// <summary>
/// Tests for the localizable accessibility-string parameters on <see cref="Pagination"/> and the
/// <see cref="Select{TValue}"/> engine. The defaults must render exactly the historical hardcoded
/// English (so existing consumers and baselines are unaffected); overrides must flow into the
/// rendered aria-labels, with the page-number format applied via the current culture.
/// </summary>
public class LocalizationParamsTests : TestContext
{
    public LocalizationParamsTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the Select JS module imports

    // ----- Pagination -------------------------------------------------------

    IRenderedComponent<Pagination> RenderPager(Action<ComponentParameterCollectionBuilder<Pagination>>? extra = null) =>
        RenderComponent<Pagination>(p =>
        {
            p.Add(pg => pg.Total, 45)      // 45 / 10 => 5 pages, all rendered (no ellipsis)
             .Add(pg => pg.PageSize, 10)
             .Add(pg => pg.Current, 2);
            extra?.Invoke(p);
        });

    [Fact]
    public void Pagination_defaults_render_the_original_english_aria_labels()
    {
        var cut = RenderPager();

        Assert.Equal("Previous page", cut.Find(".wss-pagination-prev").GetAttribute("aria-label"));
        Assert.Equal("Next page", cut.Find(".wss-pagination-next").GetAttribute("aria-label"));

        var items = cut.FindAll(".wss-pagination-item");
        Assert.Equal(5, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            Assert.Equal($"Page {i + 1}", items[i].GetAttribute("aria-label"));
        }
    }

    [Fact]
    public void Pagination_overridden_prev_next_labels_render_the_overrides()
    {
        var cut = RenderPager(p => p
            .Add(pg => pg.PreviousPageLabel, "Vorherige Seite")
            .Add(pg => pg.NextPageLabel, "Nächste Seite"));

        Assert.Equal("Vorherige Seite", cut.Find(".wss-pagination-prev").GetAttribute("aria-label"));
        Assert.Equal("Nächste Seite", cut.Find(".wss-pagination-next").GetAttribute("aria-label"));
    }

    [Fact]
    public void Pagination_PageLabelFormat_applies_to_every_page_button_including_the_current()
    {
        var cut = RenderPager(p => p.Add(pg => pg.PageLabelFormat, "Seite {0}"));

        var items = cut.FindAll(".wss-pagination-item");
        Assert.Equal(5, items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            Assert.Equal($"Seite {i + 1}", items[i].GetAttribute("aria-label"));
        }

        // The current page keeps its aria-current marker alongside the localized label.
        Assert.Equal("Seite 2", cut.Find("[aria-current=page]").GetAttribute("aria-label"));
    }

    // ----- Select engine ----------------------------------------------------

    static List<SelectOption<string>> Opts(params string[] values) =>
        values.Select(v => new SelectOption<string>(v, v)).ToList();

    [Fact]
    public void Select_defaults_render_the_original_english_aria_labels()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Values, new List<string> { "Apple" })
            .Add(s => s.DefaultOpen, true));

        Assert.Equal("Remove Apple", cut.Find(".wss-select-selection-item-remove").GetAttribute("aria-label"));
        Assert.Equal("Clear all selections", cut.Find(".wss-select-clear").GetAttribute("aria-label"));
        Assert.Equal("Options", cut.Find(".wss-select-dropdown").GetAttribute("aria-label"));
    }

    [Fact]
    public void Select_single_mode_clear_button_uses_the_singular_default()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Value, "Apple"));

        Assert.Equal("Clear selection", cut.Find(".wss-select-clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void Select_overridden_labels_render_the_overrides()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Values, new List<string> { "Apple" })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.RemoveItemLabelFormat, "{0} entfernen")
            .Add(s => s.ClearSelectionsLabel, "Auswahl löschen")
            .Add(s => s.ListboxLabel, "Optionen"));

        Assert.Equal("Apple entfernen", cut.Find(".wss-select-selection-item-remove").GetAttribute("aria-label"));
        Assert.Equal("Auswahl löschen", cut.Find(".wss-select-clear").GetAttribute("aria-label"));
        Assert.Equal("Optionen", cut.Find(".wss-select-dropdown").GetAttribute("aria-label"));
    }

    [Fact]
    public void Select_single_mode_override_renders_on_the_clear_button()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("Apple", "Banana"))
            .Add(s => s.Value, "Apple")
            .Add(s => s.ClearSelectionLabel, "Auswahl aufheben"));

        Assert.Equal("Auswahl aufheben", cut.Find(".wss-select-clear").GetAttribute("aria-label"));
    }
}
