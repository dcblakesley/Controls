using System.Reflection;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the ported Table + PropertyColumn (selection uses raw checkboxes;
/// the Checkbox control is intentionally not part of this library).
/// </summary>
public class UiKitTableTests : BunitContext
{
    // Table imports wss-table.js (to set the indeterminate select-all checkbox); tolerate the import.
    public UiKitTableTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    record Person(string Name, int Age);

    static List<Person> Sample() => [new("Alice", 30), new("Bob", 25)];

    [Fact]
    public void Table_renders_headers_and_rows_from_property_columns()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)));

        Assert.Equal(2, cut.FindAll("thead .wss-table-cell").Count);
        Assert.Equal(2, cut.FindAll("tbody .wss-table-row").Count);
        Assert.Contains("Alice", cut.Markup);
        Assert.Contains("30", cut.Markup);
    }

    [Fact]
    public void Table_renders_fully_equal_duplicate_rows_without_throwing()
    {
        // Two Equals-equal records used to produce duplicate sibling @keys — Blazor rejects those
        // with an InvalidOperationException that killed the whole table render.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, new List<Person> { new("Alice", 30), new("Alice", 30) })
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Equal(2, cut.FindAll("tbody .wss-table-row").Count);
    }

    [Fact]
    public void Table_RowKey_gives_rows_their_identity()
    {
        List<Person>? selected = null;
        var people = new List<Person> { new("Alice", 30), new("Alice", 31) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.RowKey, x => x.Age)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(true);
        Assert.NotNull(selected);
        Assert.Equal([people[0]], selected);
    }

    [Fact]
    public void Table_descending_sort_survives_a_subtraction_comparator_overflow()
    {
        // The classic (a, b) => a.X - b.X comparator returns int.MinValue for large gaps; negating
        // that overflows back to int.MinValue, silently mis-sorting descending.
        var people = new List<Person> { new("Small", int.MinValue), new("Zero", 0) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.SortBy, (a, b) => a.Age - b.Age)
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Name))));

        var sortButton = cut.Find(".wss-table-sort-trigger");
        sortButton.Click(); // ascending
        sortButton.Click(); // descending

        var firstCell = cut.FindAll("tbody .wss-table-row")[0].TextContent;
        Assert.Contains("Zero", firstCell); // descending: 0 before int.MinValue
    }

    [Fact]
    public void Table_keeps_a_column_whose_parameters_never_change()
    {
        // A title-only column (no template/Property delegates) is skipped by Blazor's diff, so it
        // never re-registers — after two table self-re-renders it used to vanish silently.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp.Add(c => c.Title, "Spacer"))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        var sortButton = cut.Find(".wss-table-sort-trigger");
        sortButton.Click(); // table self-re-render #1
        sortButton.Click(); // table self-re-render #2

        var headers = cut.FindAll("thead .wss-table-cell").Select(h => h.TextContent.Trim()).ToList();
        Assert.Contains("Spacer", headers);
        Assert.Equal("Spacer", headers[0]); // and still in its declared (first) position
    }

    [Fact]
    public void Table_empty_data_renders_placeholder()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, new List<Person>())
            .Add(t => t.EmptyText, "Nothing here")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Contains("Nothing here", cut.Find(".wss-table-placeholder").TextContent);
    }

    [Fact]
    public void Table_selectable_renders_checkboxes_and_raises_change()
    {
        List<Person>? selected = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var checkboxes = cut.FindAll("tbody input.wss-table-checkbox");
        Assert.Equal(2, checkboxes.Count);

        checkboxes[0].Change(true);
        Assert.NotNull(selected);
        Assert.Single(selected!);
    }

    [Fact]
    public void Table_headers_have_scope_and_selection_checkboxes_are_labelled()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.Caption, "People")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.All(cut.FindAll("thead th"), th => Assert.Equal("col", th.GetAttribute("scope")));
        Assert.Equal("People", cut.Find("caption").TextContent);
        Assert.Equal("Select all rows", cut.Find("thead input.wss-table-checkbox").GetAttribute("aria-label"));
        Assert.All(cut.FindAll("tbody input.wss-table-checkbox"),
            cb => Assert.Equal("Select row", cb.GetAttribute("aria-label")));
    }

    [Fact]
    public void Paged_select_all_label_says_it_only_covers_the_page_and_both_are_overridable()
    {
        // Every other user-facing string on Table/Pagination has an override; the selection labels
        // were the last hardcoded English left, so a localized table announced its own checkboxes in
        // the wrong language.
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        var paged = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.PageSize, 2)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));
        Assert.Equal("Select all rows on this page",
            paged.Find("thead input.wss-table-checkbox").GetAttribute("aria-label"));

        var localized = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectRowLabel, "Zeile auswählen")
            .Add(t => t.SelectAllRowsLabel, "Alle Zeilen auswählen")
            .Add(t => t.SelectAllRowsOnPageLabel, "Alle Zeilen dieser Seite auswählen")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Equal("Alle Zeilen auswählen",
            localized.Find("thead input.wss-table-checkbox").GetAttribute("aria-label"));
        Assert.All(localized.FindAll("tbody input.wss-table-checkbox"),
            cb => Assert.Equal("Zeile auswählen", cb.GetAttribute("aria-label")));
    }

    [Fact]
    public void Selection_label_overrides_reach_the_styled_checkbox_and_the_single_mode_radio()
    {
        var data = Sample();
        var styled = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.UseStyledCheckbox, true)
            .Add(t => t.SelectRowLabel, "Zeile auswählen")
            .Add(t => t.SelectAllRowsLabel, "Alle Zeilen auswählen")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Equal("Alle Zeilen auswählen",
            styled.Find("thead input.wss-table-checkbox").GetAttribute("aria-label"));
        Assert.All(styled.FindAll("tbody input.wss-table-checkbox"),
            cb => Assert.Equal("Zeile auswählen", cb.GetAttribute("aria-label")));

        var single = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.SelectRowLabel, "Zeile auswählen")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.All(single.FindAll("tbody input.wss-table-radio"),
            r => Assert.Equal("Zeile auswählen", r.GetAttribute("aria-label")));
    }

    [Fact]
    public void Table_prunes_selection_when_the_data_source_is_swapped_uncontrolled()
    {
        List<Person>? selected = null;
        var first = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, first)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(true); // select Alice
        Assert.Single(selected!);

        // Swap to a new data source that shares no rows with the old one.
        var second = new List<Person> { new("Carol", 40), new("Dave", 22) };
        cut.Render(p => p.Add(t => t.DataSource, second));

        // Selecting a row in the new data must not drag the now-absent Alice along.
        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(true); // select Carol
        Assert.NotNull(selected);
        Assert.Single(selected!);
        Assert.Equal("Carol", selected![0].Name);
    }

    [Fact]
    public void Table_sortable_property_column_renders_a_trigger_and_aria_sort_none()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)) // not sortable
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        var headers = cut.FindAll("thead th");
        // Non-sortable column: plain header, no trigger, no aria-sort.
        Assert.False(headers[0].HasAttribute("aria-sort"));
        Assert.Empty(headers[0].QuerySelectorAll("button.wss-table-sort-trigger"));
        // Sortable column: a sort trigger and aria-sort="none" before any click.
        Assert.Equal("none", headers[1].GetAttribute("aria-sort"));
        Assert.Single(headers[1].QuerySelectorAll("button.wss-table-sort-trigger"));
        // Titled sortable column: the visible header names the button, so no redundant aria-label.
        Assert.False(headers[1].QuerySelector("button.wss-table-sort-trigger")!.HasAttribute("aria-label"));
    }

    [Fact]
    public void Table_title_less_sortable_header_button_has_an_accessible_name()
    {
        // A sortable column with no Title would otherwise render a sort <button> with no accessible
        // name (empty label span + aria-hidden carets) — it falls back to aria-label="Sort".
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        var button = cut.Find("button.wss-table-sort-trigger");
        Assert.Equal("Sort", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Table_clicking_a_sortable_header_cycles_ascending_descending_then_clears()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice(30), Bob(25)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        string[] Names() => cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        void ClickAge() => cut.FindAll("thead th")[1].QuerySelector("button.wss-table-sort-trigger")!.Click();
        string AgeAriaSort() => cut.FindAll("thead th")[1].GetAttribute("aria-sort")!;

        Assert.Equal(["Alice", "Bob"], Names()); // original order

        ClickAge(); // ascending by Age -> Bob(25), Alice(30)
        Assert.Equal(["Bob", "Alice"], Names());
        Assert.Equal("ascending", AgeAriaSort());

        ClickAge(); // descending -> Alice(30), Bob(25)
        Assert.Equal(["Alice", "Bob"], Names());
        Assert.Equal("descending", AgeAriaSort());

        ClickAge(); // cleared -> original order restored
        Assert.Equal(["Alice", "Bob"], Names());
        Assert.Equal("none", AgeAriaSort());
    }

    [Fact]
    public void Table_pager_alignment_renders_the_modifier_class()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.PageSize, 1) // force the pager to render
            .Add(t => t.PagerAlign, PagerAlign.Left)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Contains("wss-table-pagination-left", cut.Find(".wss-table-pagination").ClassName);
    }

    [Fact]
    public void Table_pager_position_top_renders_a_single_top_pager()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.PageSize, 1)
            .Add(t => t.PagerPosition, PagerPosition.Top)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var pagers = cut.FindAll(".wss-table-pagination");
        Assert.Single(pagers);
        Assert.Contains("wss-table-pagination-top", pagers[0].ClassName);
    }

    [Fact]
    public void Table_pager_position_both_renders_a_top_and_a_bottom_pager()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.PageSize, 1)
            .Add(t => t.PagerPosition, PagerPosition.Both)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var pagers = cut.FindAll(".wss-table-pagination");
        Assert.Equal(2, pagers.Count);
        Assert.Contains("wss-table-pagination-top", pagers[0].ClassName);
        Assert.Contains("wss-table-pagination-bottom", pagers[1].ClassName);
    }

    [Fact]
    public void Pager_and_sort_accessible_names_default_unchanged_and_are_overridable()
    {
        // The last hardcoded English in the Table: the embedded Pagination's aria-label (three
        // literals, with no Table-level way to reach Pagination.AriaLabel) and the sort button's
        // title-less fallback. Same naming/doc style as SelectRowLabel/SelectAllRowsLabel.
        IRenderedComponent<Table<Person>> RenderPagers(PagerPosition position, bool localized) =>
            Render<Table<Person>>(p =>
            {
                p.Add(t => t.DataSource, Sample())
                 .Add(t => t.PageSize, 1)
                 .Add(t => t.PagerPosition, position);
                if (localized)
                {
                    p.Add(t => t.PaginationLabel, "Paginación")
                     .Add(t => t.TopPaginationLabel, "Paginación (arriba)")
                     .Add(t => t.BottomPaginationLabel, "Paginación (abajo)")
                     .Add(t => t.SortLabel, "Ordenar");
                }
                p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                    .Add(c => c.Property, x => x.Name) // no Title: the sort button needs the fallback
                    .Add(c => c.Sortable, true));
            });

        // Defaults render byte-identically to before the parameters existed.
        Assert.Equal("Pagination", RenderPagers(PagerPosition.Bottom, false).Find("nav.wss-pagination").GetAttribute("aria-label"));
        var bothDefault = RenderPagers(PagerPosition.Both, false).FindAll("nav.wss-pagination");
        Assert.Equal("Pagination (top)", bothDefault[0].GetAttribute("aria-label"));
        Assert.Equal("Pagination (bottom)", bothDefault[1].GetAttribute("aria-label"));
        Assert.Equal("Sort", RenderPagers(PagerPosition.Bottom, false).Find("button.wss-table-sort-trigger").GetAttribute("aria-label"));

        // ...and every one of them is now reachable.
        Assert.Equal("Paginación", RenderPagers(PagerPosition.Bottom, true).Find("nav.wss-pagination").GetAttribute("aria-label"));
        var bothLocalized = RenderPagers(PagerPosition.Both, true).FindAll("nav.wss-pagination");
        Assert.Equal("Paginación (arriba)", bothLocalized[0].GetAttribute("aria-label"));
        Assert.Equal("Paginación (abajo)", bothLocalized[1].GetAttribute("aria-label"));
        Assert.Equal("Ordenar", RenderPagers(PagerPosition.Bottom, true).Find("button.wss-table-sort-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void SortLabel_also_names_a_title_less_TitleContent_sort_button()
    {
        // The other branch of the fallback: a TitleContent template renders OUTSIDE the sort button,
        // so the button has no visible content of its own and always needs the aria-label.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.SortLabel, "Ordenar")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.Sortable, true)
                .Add(c => c.TitleContent, (RenderFragment)(b => b.AddContent(0, "hi")))));

        Assert.Equal("Ordenar", cut.Find("button.wss-table-sort-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void Table_custom_column_with_SortBy_is_sortable()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice(30), Bob(25)
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.SortBy, (a, b) => a.Age - b.Age)
                .Add(c => c.ChildContent, (RenderFragment<Person>)(person => b => b.AddContent(0, person.Name)))));

        Assert.Single(cut.FindAll("button.wss-table-sort-trigger"));

        cut.Find("button.wss-table-sort-trigger").Click(); // ascending by Age -> Bob, Alice
        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Bob", "Alice"], names);
    }

    [Fact]
    public void Table_sortable_on_a_non_comparable_property_renders_no_sort_control()
    {
        // Person doesn't implement IComparable, so Comparer<Person>.Default would throw on a header
        // click; CanSort degrades the column to non-sortable instead of crashing the circuit.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, Person>>(cp => cp
                .Add(c => c.Title, "Self")
                .Add(c => c.Property, x => x)
                .Add(c => c.Sortable, true)));

        Assert.Empty(cut.FindAll("button.wss-table-sort-trigger"));
        Assert.False(cut.Find("thead th").HasAttribute("aria-sort"));
    }

    [Fact]
    public void Table_non_comparable_property_is_still_sortable_with_an_explicit_SortBy()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice(30), Bob(25)
            .AddChildContent<PropertyColumn<Person, Person>>(cp => cp
                .Add(c => c.Title, "Self")
                .Add(c => c.Property, x => x)
                .Add(c => c.Sortable, true)
                .Add(c => c.SortBy, (a, b) => a.Age - b.Age)));

        Assert.Single(cut.FindAll("button.wss-table-sort-trigger"));
        cut.Find("button.wss-table-sort-trigger").Click(); // ascending by Age -> Bob first
        Assert.Contains("Bob", cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")[0].TextContent);
    }

    [Fact]
    public void Table_conditionally_hidden_column_drops_and_reappears_in_order()
    {
        var showMiddle = true;
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();

            if (showMiddle)
            {
                builder.OpenComponent<PropertyColumn<Person, int>>(3);
                builder.AddAttribute(4, "Title", "Age");
                builder.AddAttribute(5, "Property", (Func<Person, int>)(x => x.Age));
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, string>>(6);
            builder.AddAttribute(7, "Title", "City");
            builder.AddAttribute(8, "Property", (Func<Person, string>)(_ => "NYC"));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));

        string[] Headers() => cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();
        Assert.Equal(["Name", "Age", "City"], Headers());

        // Hide the middle column: it drops out, no zombie left behind.
        showMiddle = false;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "City"], Headers());

        // Re-show it: no duplicate, and it returns to its declared (middle) position.
        showMiddle = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "Age", "City"], Headers());
    }

    // ----- Declaration order across parameter-skipped siblings (AUDIT-2026-07-30 finding 6, Table analog) -----
    //
    // Modeled on TabsAndSearchInputTests.A_tab_shown_before_parameter_skipped_siblings_lands_in_its_declared_position
    // (the Tabs repro for the same defect class, fixed in c5cab30 before Tabs was later redesigned so each Tab
    // renders its own nav button). Columns here carry only a Title string, so once a column has rendered once
    // with the same Title, Blazor's diff skips SetParametersAsync on it entirely on a later render where nothing
    // about it changed -- it never re-registers into Table's per-pass collection buffer, and
    // StartCollectingColumns' straggler merge re-inserts it at its OLD _columns index instead of leaving recovery
    // to a fresh document-order pass.
    static RenderFragment ConditionalLeadingColumns(bool showNew, bool showMid) => builder =>
    {
        if (showNew)
        {
            builder.OpenComponent<Column<Person>>(0);
            builder.AddAttribute(1, "Title", "New");
            builder.CloseComponent();
        }

        if (showMid)
        {
            builder.OpenComponent<Column<Person>>(2);
            builder.AddAttribute(3, "Title", "Mid");
            builder.CloseComponent();
        }

        builder.OpenComponent<Column<Person>>(4);
        builder.AddAttribute(5, "Title", "A");
        builder.CloseComponent();

        builder.OpenComponent<Column<Person>>(6);
        builder.AddAttribute(7, "Title", "B");
        builder.CloseComponent();

        builder.OpenComponent<Column<Person>>(8);
        builder.AddAttribute(9, "Title", "C");
        builder.CloseComponent();
    };

    static string[] Headers(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();

    // A filterable/sortable header wraps its title next to trigger buttons (and, while open, a whole
    // filter dropdown), so read the title element rather than the cell's whole text.
    static string[] HeaderTitles(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("thead th")
            .Select(th =>
            {
                var title = th.QuerySelector(".wss-table-header-text")
                            ?? th.QuerySelector(".wss-table-sort-trigger")
                            ?? th;
                return string.Join(" ", title.TextContent.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            })
            .ToArray();

    [Fact]
    public void Table_column_shown_before_parameter_skipped_siblings_lands_in_its_declared_position()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, ConditionalLeadingColumns(false, false)));

        Assert.Equal(["A", "B", "C"], Headers(cut));

        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingColumns(true, false)));

        // Declared [New, A, B, C]. If StartCollectingColumns has no recovery beyond the straggler
        // merge, A/B/C (unchanged) never re-register and get re-inserted at their old _columns
        // index ahead of New, landing New LAST instead of first.
        Assert.Equal(["New", "A", "B", "C"], Headers(cut));

        // A second insertion, between the newcomer and the still-skipped siblings.
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingColumns(true, true)));
        Assert.Equal(["New", "Mid", "A", "B", "C"], Headers(cut));
    }

    [Fact]
    public void Table_column_removed_then_reshown_returns_to_its_declared_position()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, ConditionalLeadingColumns(true, false)));

        Assert.Equal(["New", "A", "B", "C"], Headers(cut));

        // Remove it -- the merge always handled removal correctly (a gone column just drops out).
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingColumns(false, false)));
        Assert.Equal(["A", "B", "C"], Headers(cut));

        // Re-show it: does it come back to its declared (first) position, or does the previous
        // wrong _columns order (if any survived) resurface?
        cut.Render(p => p.Add(t => t.ChildContent, ConditionalLeadingColumns(true, false)));
        Assert.Equal(["New", "A", "B", "C"], Headers(cut));
    }

    [Fact]
    public void Table_column_shown_after_skipped_siblings_lands_in_its_declared_position()
    {
        // Mirror of the leading-insertion case: a new column declared AFTER the unchanged siblings.
        // A trailing insertion never needs to move earlier stragglers out of the way, so this is
        // the "should always have worked" control case.
        RenderFragment TrailingColumn(bool showTrailing) => builder =>
        {
            builder.OpenComponent<Column<Person>>(0);
            builder.AddAttribute(1, "Title", "A");
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(2);
            builder.AddAttribute(3, "Title", "B");
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(4);
            builder.AddAttribute(5, "Title", "C");
            builder.CloseComponent();

            if (showTrailing)
            {
                builder.OpenComponent<Column<Person>>(6);
                builder.AddAttribute(7, "Title", "New");
                builder.CloseComponent();
            }
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, TrailingColumn(false)));

        Assert.Equal(["A", "B", "C"], Headers(cut));

        cut.Render(p => p.Add(t => t.ChildContent, TrailingColumn(true)));
        Assert.Equal(["A", "B", "C", "New"], Headers(cut));
    }

    [Fact]
    public void Table_column_order_survives_an_unrelated_sort_click()
    {
        // Same repro as the leading-insertion test, but this time one sibling is Sortable, so a
        // completely unrelated user action (clicking its sort trigger) forces further Table
        // re-renders after the insertion has landed. The old straggler merge re-spliced the
        // parameter-skipped column at a stale index on EVERY pass, so an interaction that touched
        // nothing about the column declarations could reorder the table on its own.
        //
        // Note which columns are which here: Age carries a Property delegate, so Blazor can never
        // prove it unchanged and it re-registers in document order every pass -- it is the "anchor"
        // that pins straggler B's position. B is Title-only and never re-registers again.
        RenderFragment Columns(bool showNew) => builder =>
        {
            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(0);
                builder.AddAttribute(1, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, int>>(2);
            builder.AddAttribute(3, "Title", "Age");
            builder.AddAttribute(4, "Property", (Func<Person, int>)(x => x.Age));
            builder.AddAttribute(5, "Sortable", true);
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(6);
            builder.AddAttribute(7, "Title", "B");
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns(false)));

        Assert.Equal(["Age", "B"], Headers(cut));

        cut.Render(p => p.Add(t => t.ChildContent, Columns(true)));
        Assert.Equal(["New", "Age", "B"], Headers(cut));

        // Click the sort trigger on Age -- an unrelated re-render with the exact same ChildContent.
        cut.Find("button.wss-table-sort-trigger").Click();
        Assert.Equal(["New", "Age", "B"], Headers(cut));

        // ... and again, in the other direction. Order must not drift on repeated passes either.
        cut.Find("button.wss-table-sort-trigger").Click();
        Assert.Equal(["New", "Age", "B"], Headers(cut));
    }

    [Fact]
    public void Table_column_order_is_stable_across_filter_and_page_interactions()
    {
        // The straggler placement runs on every render, so anything that re-renders the table is a
        // chance to reorder it. Exercise the interactive paths a user actually drives -- open a
        // filter dropdown, apply it, page forward, reset -- around a Title-only straggler.
        var people = Enumerable.Range(0, 12).Select(i => new Person($"P{i}", 20 + i)).ToList();

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)
                [new TableFilterOption("P1", "P1"), new TableFilterOption("P2", "P2")]);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(5);
            builder.AddAttribute(6, "Title", "Spacer");
            builder.CloseComponent();

            builder.OpenComponent<PropertyColumn<Person, int>>(7);
            builder.AddAttribute(8, "Title", "Age");
            builder.AddAttribute(9, "Property", (Func<Person, int>)(x => x.Age));
            builder.AddAttribute(10, "Sortable", true);
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.PageSize, 5)
            .Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));

        CheckOption(cut, "P1");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();
        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));

        cut.FindAll(".wss-pagination-item").First(li => li.TextContent.Trim() == "2").Click();
        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));

        cut.Find("button.wss-table-sort-trigger").Click();
        Assert.Equal(["Name", "Spacer", "Age"], HeaderTitles(cut));
    }

    [Fact]
    public void Table_inserting_a_column_keeps_a_sibling_columns_applied_filter_and_sort()
    {
        // Blast-radius bound on the document-order rebuild (Table.MergeCollectedColumns): the
        // rebuild only fires when NO already-rendered column re-registered, and a column holding
        // any state necessarily re-registers (FilterOptions/OnFilter/SortBy/Property are all
        // parameters Blazor can never prove unchanged). So a table with real columns must keep its
        // applied filter, its active sort and its filterable column's identity across an insertion.
        var showNew = false;
        RenderFragment Columns() => builder =>
        {
            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(0);
                builder.AddAttribute(1, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, string>>(2);
            builder.AddAttribute(3, "Title", "Name");
            builder.AddAttribute(4, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(5, "FilterOptions", (IReadOnlyList<TableFilterOption>)
                [new TableFilterOption("Alice", "Alice"), new TableFilterOption("Bob", "Bob")]);
            builder.AddAttribute(6, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();

            builder.OpenComponent<PropertyColumn<Person, int>>(7);
            builder.AddAttribute(8, "Title", "Age");
            builder.AddAttribute(9, "Property", (Func<Person, int>)(x => x.Age));
            builder.AddAttribute(10, "Sortable", true);
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(11);
            builder.AddAttribute(12, "Title", "Spacer");
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));

        // Apply a filter and a sort, so there is state that a teardown would destroy.
        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        cut.Find("button.wss-table-sort-trigger").Click();

        Assert.Single(cut.FindAll("tbody .wss-table-row"));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Equal("ascending", cut.FindAll("thead th")[1].GetAttribute("aria-sort"));

        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["New", "Name", "Age", "Spacer"], HeaderTitles(cut));
        // Nothing was rebuilt: the filter is still applied and the sort still active.
        Assert.Single(cut.FindAll("tbody .wss-table-row"));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Equal("ascending", cut.FindAll("thead th")[2].GetAttribute("aria-sort"));
    }

    /// <summary>Non-column content declared inside &lt;Table&gt;; counts its own instantiations so a
    /// test can tell whether the ChildContent subtree was rebuilt.</summary>
    public class RebuildProbe : ComponentBase
    {
        public static int Instantiations;
        public RebuildProbe() => Instantiations++;
        protected override void BuildRenderTree(RenderTreeBuilder builder) { }
    }

    [Fact]
    public void Table_does_not_rebuild_ChildContent_when_a_column_can_anchor_the_order()
    {
        // Upper bound on the one teardown in Table (the _columnGeneration @key). Any column carrying
        // a template/Property/SortBy/OnFilter/FilterOptions re-registers on every pass in document
        // order, which is enough to place the parameter-skipped ones -- so no rebuild, and nothing
        // in the ChildContent subtree is torn down. That is also what makes the rebuild safe when it
        // DOES fire: a column that can hold state is exactly a column that anchors, so it is never
        // the one being rebuilt.
        var showNew = false;
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<RebuildProbe>(0);
            builder.CloseComponent();

            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(1);
                builder.AddAttribute(2, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, string>>(3);
            builder.AddAttribute(4, "Title", "Name");
            builder.AddAttribute(5, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(6);
            builder.AddAttribute(7, "Title", "Spacer");
            builder.CloseComponent();
        };

        RebuildProbe.Instantiations = 0;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "Spacer"], Headers(cut));
        Assert.Equal(1, RebuildProbe.Instantiations);

        // An ordinary re-render never rebuilds and never reorders.
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "Spacer"], Headers(cut));
        Assert.Equal(1, RebuildProbe.Instantiations);

        // An insertion ahead of the anchor is placed exactly, with no rebuild.
        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["New", "Name", "Spacer"], Headers(cut));
        Assert.Equal(1, RebuildProbe.Instantiations);

        // As is the removal, and the re-insertion after it.
        showNew = false;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "Spacer"], Headers(cut));
        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["New", "Name", "Spacer"], Headers(cut));
        Assert.Equal(1, RebuildProbe.Instantiations);
    }

    [Fact]
    public void Table_rebuilds_ChildContent_once_when_declaration_order_is_unknowable()
    {
        // The lower bound: a table of Title-only columns has no anchor, so an insertion is
        // indistinguishable from an append and the order can only be recovered by re-collecting
        // from scratch. Pins that the rebuild fires, that it fires ONCE (no render loop), that a
        // removal does not trigger it, and that the documented casualty -- non-column content
        // declared inside <Table> -- is what pays for it.
        var showNew = false;
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<RebuildProbe>(0);
            builder.CloseComponent();

            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(1);
                builder.AddAttribute(2, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<Column<Person>>(3);
            builder.AddAttribute(4, "Title", "A");
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(5);
            builder.AddAttribute(6, "Title", "B");
            builder.CloseComponent();
        };

        RebuildProbe.Instantiations = 0;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal(["A", "B"], Headers(cut));
        Assert.Equal(1, RebuildProbe.Instantiations);

        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(1, RebuildProbe.Instantiations);

        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["New", "A", "B"], Headers(cut));
        Assert.Equal(2, RebuildProbe.Instantiations);

        // Settles: further renders neither rebuild again nor drift.
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["New", "A", "B"], Headers(cut));
        Assert.Equal(2, RebuildProbe.Instantiations);

        // A removal is never ambiguous -- the survivors keep their relative order.
        showNew = false;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["A", "B"], Headers(cut));
        Assert.Equal(2, RebuildProbe.Instantiations);
    }

    [Fact]
    public void Table_column_collection_costs_no_extra_render_passes()
    {
        // The ordering work is pure bookkeeping over the registrations Blazor already delivers: the
        // cascade stays IsFixed, so no column is re-parameterized or re-rendered on its account, and
        // an ordinary re-render queues nothing extra. (Making the cascade non-fixed -- the shape that
        // looks like it should fix the ordering, see RendererChildOrderingContractTests -- would add
        // one SetParametersAsync + render per column per pass and still get the order wrong.)
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(3);
            builder.AddAttribute(4, "Title", "Spacer");
            builder.CloseComponent();

            builder.OpenComponent<PropertyColumn<Person, int>>(5);
            builder.AddAttribute(6, "Title", "Age");
            builder.AddAttribute(7, "Property", (Func<Person, int>)(x => x.Age));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));

        var before = cut.RenderCount;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        // Three parent renders in, three renders out: the straggler never costs a corrective pass.
        Assert.Equal(3, cut.RenderCount - before);
        Assert.Equal(["Name", "Spacer", "Age"], Headers(cut));
    }

    [Fact]
    public void Table_column_inserted_after_a_skipped_column_lands_in_declaration_order()
    {
        // The half of the ambiguous gap the merge gets RIGHT. A newcomer declared AFTER a
        // parameter-skipped column, with an anchor following: the pass reports nothing about where
        // the newcomer sits relative to the skipped column, and the merge's choice -- skipped columns
        // keep their place, the newcomer goes after them -- happens to be the declared order here.
        // Pinned as correct behavior, next to the mirror shape below where the same choice is wrong.
        var showNew = false;
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<Column<Person>>(0);
            builder.AddAttribute(1, "Title", "Spacer");
            builder.CloseComponent();

            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(2);
                builder.AddAttribute(3, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, string>>(4);
            builder.AddAttribute(5, "Title", "Name");
            builder.AddAttribute(6, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Spacer", "Name"], Headers(cut));

        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Spacer", "New", "Name"], Headers(cut));
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Spacer", "New", "Name"], Headers(cut));
    }

    [Fact]
    public void Table_column_declared_before_a_skipped_column_still_renders_after_it_known_limitation()
    {
        // The one residual, pinned by the shape that actually exhibits it (the mirror of the test
        // above): declared [New, Spacer, Name], rendered [Spacer, New, Name]. A newcomer and a
        // parameter-skipped column share the gap before the anchor, and the registrations Blazor
        // delivers are IDENTICAL for "the newcomer is before the skipped column" and "after it" --
        // only Name re-registers, and it reports nothing about either. The merge commits to keeping
        // skipped columns where they were and appending the newcomer after them, which is right for
        // the far more common trailing insertion and wrong here.
        //
        // Not fixed, deliberately. The only mechanism that could tell the two apart is the generation
        // @key rebuild, and the gate that fires it (anchors == 0) is what proves the teardown is
        // state-free: every previously-rendered column was one Blazor skipped, so none of them can be
        // sorted, filtered, or hold a template. Firing it per AMBIGUOUS GAP instead would tear down
        // live anchors -- here, Name -- discarding real sort/filter state and re-creating any
        // non-column content in ChildContent, to move a column that by construction has none. A
        // misplaced parameter-less column is the cheaper wrong answer, and it self-corrects the
        // moment any pass changes one of the affected columns' parameters. (Tabs CAN do the
        // finer-grained thing precisely because a Tab has no such state; see
        // TabsAndSearchInputTests.) Documented on Table.ChildContent.
        var showNew = false;
        RenderFragment Columns() => builder =>
        {
            if (showNew)
            {
                builder.OpenComponent<Column<Person>>(0);
                builder.AddAttribute(1, "Title", "New");
                builder.CloseComponent();
            }

            builder.OpenComponent<Column<Person>>(2);
            builder.AddAttribute(3, "Title", "Spacer");
            builder.CloseComponent();

            builder.OpenComponent<PropertyColumn<Person, string>>(4);
            builder.AddAttribute(5, "Title", "Name");
            builder.AddAttribute(6, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Spacer", "Name"], Headers(cut));

        showNew = true;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        // Declared ["New", "Spacer", "Name"]. This is what it currently does -- the honest assertion,
        // so that a future fix breaks this test loudly instead of drifting past it.
        Assert.Equal(["Spacer", "New", "Name"], Headers(cut));

        // At least it is stable: further re-renders neither drift nor rebuild.
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Spacer", "New", "Name"], Headers(cut));

        // ...and it recovers as soon as a pass actually REPORTS the order: change both columns'
        // parameters and they re-register in document order, which the merge takes verbatim. (One of
        // the two is not enough -- the other is still a straggler and still keeps its wrong place.)
        RenderFragment Touched() => builder =>
        {
            builder.OpenComponent<Column<Person>>(0);
            builder.AddAttribute(1, "Title", "New!");
            builder.CloseComponent();

            builder.OpenComponent<Column<Person>>(2);
            builder.AddAttribute(3, "Title", "Spacer!");
            builder.CloseComponent();

            builder.OpenComponent<PropertyColumn<Person, string>>(4);
            builder.AddAttribute(5, "Title", "Name");
            builder.AddAttribute(6, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();
        };

        cut.Render(p => p.Add(t => t.ChildContent, Touched()));
        Assert.Equal(["New!", "Spacer!", "Name"], Headers(cut));
    }

    [Fact]
    public void Table_hiding_the_sorted_column_clears_the_sort()
    {
        var showAge = true;
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();

            if (showAge)
            {
                builder.OpenComponent<PropertyColumn<Person, int>>(3);
                builder.AddAttribute(4, "Title", "Age");
                builder.AddAttribute(5, "Property", (Func<Person, int>)(x => x.Age));
                builder.AddAttribute(6, "Sortable", true);
                builder.CloseComponent();
            }
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice(30), Bob(25)
            .Add(t => t.ChildContent, Columns()));

        string[] Names() => cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")
            .Select(td => td.TextContent.Trim()).ToArray();

        // Sort ascending by Age -> Bob(25), Alice(30).
        cut.Find("button.wss-table-sort-trigger").Click();
        Assert.Equal(["Bob", "Alice"], Names());

        // Hide the sorted column: the sort clears and rows return to DataSource order.
        showAge = false;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Empty(cut.FindAll("button.wss-table-sort-trigger"));
        Assert.Equal(["Alice", "Bob"], Names());
    }

    [Fact]
    public void Table_select_all_checkbox_is_not_checked_when_only_some_rows_are_selected()
    {
        var data = Sample(); // Alice, Bob
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItems, new List<Person> { data[0] }) // one of two selected
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        // Partial selection: the header checkbox is unchecked (the mixed/indeterminate state is then
        // applied via JS, which bUnit can't observe) — it must not falsely render as fully checked.
        Assert.False(cut.Find("thead input.wss-table-checkbox").HasAttribute("checked"));
    }

    [Fact]
    public void Toggling_Selectable_off_and_on_reapplies_the_indeterminate_state()
    {
        var data = Sample(); // Alice, Bob
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItems, new List<Person> { data[0] }) // partial selection -> mixed state
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        int IndeterminateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "setIndeterminate");
        Assert.Equal(1, IndeterminateCalls());

        // The select-all <input> lives inside @if (Selectable): toggling it off destroys the element
        // and a fresh one comes back with indeterminate == false. The stale _lastIndeterminate mirror
        // used to short-circuit the JS call, leaving the recreated checkbox plain-unchecked while
        // some rows were selected.
        cut.Render(p => p.Add(t => t.Selectable, false));
        cut.Render(p => p.Add(t => t.Selectable, true));

        Assert.Equal(2, IndeterminateCalls());
    }

    [Fact]
    public void Parent_rerender_with_unchanged_data_does_not_rewalk_rows()
    {
        // Pins the perf contract algorithmically (bUnit can't see wall-clock): with an unchanged
        // DataSource / page / page size, a parent re-render must not re-run the page-key rebuild or
        // any selection scan. The only remaining KeyFor calls during a pure re-render are the row
        // markup's two IsSelected probes per row (the tr's selected-class check and the row
        // checkbox's checked attribute) — exactly 2 * rows per render pass. Before the fix each
        // re-render also walked all rows for the key rebuild plus up to three header-checkbox
        // scans, i.e. O(rows) growth per re-render beyond the markup probes.
        var keyCalls = 0;
        var people = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.RowKey, x => { keyCalls++; return x.Name; })
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var callsAfterFirstRender = keyCalls;
        var rendersAfterFirstRender = cut.RenderCount;

        // Simulate the parent re-rendering with identical values (ChildContent defeats Blazor's
        // parameter-change skip, so each of these runs the Table's OnParametersSet + render).
        cut.Render(p => p.Add(t => t.DataSource, people));
        cut.Render(p => p.Add(t => t.DataSource, people));
        cut.Render(p => p.Add(t => t.DataSource, people));

        var extraRenders = cut.RenderCount - rendersAfterFirstRender;
        Assert.True(extraRenders >= 3);
        Assert.Equal(extraRenders * people.Count * 2, keyCalls - callsAfterFirstRender);
    }

    [Fact]
    public void DataSource_swap_rebuilds_keys_and_selection_flags()
    {
        var first = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, first)
            .Add(t => t.Selectable, true)
            .Add(t => t.RowKey, x => x.Name)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.Find("thead input.wss-table-checkbox").Change(true); // select all: Alice, Bob
        Assert.True(cut.Find("thead input.wss-table-checkbox").HasAttribute("checked"));

        // Swap to disjoint data: the rows re-render, the stale (uncontrolled) selection is pruned,
        // and the cached header-checkbox state recomputes to unchecked.
        var second = new List<Person> { new("Carol", 40), new("Dave", 22), new("Eve", 35) };
        cut.Render(p => p.Add(t => t.DataSource, second));

        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell:last-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Carol", "Dave", "Eve"], names);
        Assert.False(cut.Find("thead input.wss-table-checkbox").HasAttribute("checked"));
    }

    [Fact]
    public void Select_all_and_toggle_row_keep_header_checkbox_state_correct()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice, Bob — uncontrolled selection
            .Add(t => t.Selectable, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        bool HeaderChecked() => cut.Find("thead input.wss-table-checkbox").HasAttribute("checked");

        Assert.False(HeaderChecked()); // nothing selected yet

        cut.Find("thead input.wss-table-checkbox").Change(true); // select all
        Assert.True(HeaderChecked());
        Assert.All(cut.FindAll("tbody input.wss-table-checkbox"),
            cb => Assert.True(cb.HasAttribute("checked")));

        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(false); // untick one row
        // Partial selection: not fully checked (the mixed state is applied via JS, unobservable here).
        Assert.False(HeaderChecked());
    }

    [Fact]
    public void UseStyledCheckbox_renders_the_custom_drawn_box_for_header_and_row_checkboxes()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.UseStyledCheckbox, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Single(cut.FindAll("thead .wss-table-checkbox-wrap"));
        Assert.Equal(2, cut.FindAll("tbody .wss-table-checkbox-wrap").Count);
        Assert.Equal(3, cut.FindAll("input.wss-table-checkbox-input-styled").Count);
    }

    [Fact]
    public void UseStyledCheckbox_unset_renders_bare_native_checkboxes()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Empty(cut.FindAll(".wss-table-checkbox-wrap"));
        Assert.Equal(3, cut.FindAll("input.wss-table-checkbox").Count);
    }

    [Fact]
    public void Table_merges_a_consumer_class_and_splats_other_attributes_onto_the_root()
    {
        // Unmatched attributes used to throw InvalidOperationException; per the library owner's
        // decision, class merges with the component's own and the rest splat onto the root element.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddUnmatched("class", "consumer-table")
            .AddUnmatched("data-testid", "orders")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var root = cut.Find(".wss-table-root");
        Assert.Contains("consumer-table", root.ClassList);
        Assert.Contains("wss-table-root", root.ClassList); // merged, not replaced
        Assert.Equal("orders", root.GetAttribute("data-testid"));
    }

    // ----- Expandable rows (RowDetail) + header templates (TitleContent) -----

    IRenderedComponent<Table<Person>> RenderExpandable(List<Person>? data = null) =>
        Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data ?? Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

    [Fact]
    public void RowDetail_adds_an_expand_column_and_toggles_the_detail_row()
    {
        var cut = RenderExpandable();

        // A leading expand header cell plus one chevron button per row; nothing expanded yet.
        Assert.Equal(2, cut.FindAll("thead .wss-table-cell").Count); // expand + Name
        Assert.Equal(2, cut.FindAll("tbody .wss-table-expand-btn").Count);
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));

        var btn = cut.FindAll("tbody .wss-table-expand-btn")[0];
        Assert.Equal("false", btn.GetAttribute("aria-expanded"));
        btn.Click();

        var detail = cut.Find(".wss-table-expanded-row .wss-table-expanded-cell");
        Assert.Contains("Detail for Alice", detail.TextContent);
        Assert.Equal("2", detail.GetAttribute("colspan")); // expand column + Name
        Assert.Equal("true", cut.FindAll("tbody .wss-table-expand-btn")[0].GetAttribute("aria-expanded"));

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click();
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void Expansion_follows_row_identity_through_a_sort()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice, Bob
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click(); // expand Alice (row 0)
        cut.Find(".wss-table-sort-trigger").Click();           // ascending by age -> Bob first

        // Alice (30) is now row 1, and her detail row moved with her. (Only the Age column
        // renders, so rows are identified by age.)
        var rows = cut.FindAll("tbody tr");
        Assert.Contains("25", rows[0].TextContent);
        Assert.Contains("30", rows[1].TextContent);
        Assert.Contains("Detail for Alice", rows[2].TextContent);
        Assert.Single(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void Expansion_state_is_forgotten_for_rows_that_leave_the_data()
    {
        var cut = RenderExpandable();
        cut.FindAll("tbody .wss-table-expand-btn")[0].Click(); // expand Alice
        Assert.Single(cut.FindAll(".wss-table-expanded-row"));

        // Swap Alice out, then back in — she must come back collapsed, not zombie-expanded.
        cut.Render(p => p.Add(t => t.DataSource, new List<Person> { new("Bob", 25) }));
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
        cut.Render(p => p.Add(t => t.DataSource, Sample()));
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void TitleContent_replaces_the_plain_header_text()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.TitleContent, b => b.AddMarkupContent(0, "Name <em class=\"hdr-extra\">(info)</em>"))
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Name))));

        var header = cut.Find("thead .wss-table-cell");
        Assert.NotNull(header.QuerySelector("em.hdr-extra"));
        Assert.Contains("Name", header.TextContent);
    }

    [Fact]
    public void TitleContent_on_a_sortable_column_renders_outside_the_button_which_falls_back_to_Sort()
    {
        // The template renders in its own clickable content area, not inside the sort button
        // (nesting the template's own interactive content — e.g. a LabelTooltip's <button> — inside
        // the sort trigger would be invalid HTML and let its clicks bubble into ToggleSort).
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.TitleContent, b => b.AddContent(0, "Age"))
                .Add(c => c.SortBy, (a, b) => a.Age.CompareTo(b.Age))
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Age.ToString()))));

        var trigger = cut.Find(".wss-table-sort-trigger");
        Assert.DoesNotContain("Age", trigger.TextContent);
        Assert.Contains("Age", cut.Find(".wss-table-sort-content").TextContent);
        // With no Title set, the now icon-only button has no visible content of its own -> "Sort".
        Assert.Equal("Sort", trigger.GetAttribute("aria-label"));

        trigger.Click(); // sorting still works through the button
        Assert.Contains("wss-table-sorter-active", cut.Find(".wss-table-sorter-up").ClassList);
    }

    [Fact]
    public void Sortable_TitleContent_without_Title_falls_back_to_Sort_button_label()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.TitleContent, b => b.AddMarkupContent(0, "<svg aria-hidden=\"true\"></svg>")) // icon-only, no visible text
                .Add(c => c.SortBy, (a, b) => a.Age.CompareTo(b.Age))
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Age.ToString()))));

        var button = cut.Find("button.wss-table-sort-trigger");
        Assert.Equal("Sort", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Sortable_TitleContent_with_Title_names_the_button_from_Title()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "ESD")
                .Add(c => c.TitleContent, b => b.AddMarkupContent(0, "ESD <em>(info)</em>"))
                .Add(c => c.SortBy, (a, b) => a.Age.CompareTo(b.Age))
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Age.ToString()))));

        var button = cut.Find("button.wss-table-sort-trigger");
        Assert.Equal("ESD", button.GetAttribute("aria-label"));
    }

    [Fact]
    public void Sortable_TitleContent_nested_button_click_does_not_toggle_sort_but_content_click_does()
    {
        // Mirrors the real-world composition: a LabelTooltip's own <button> nested in the header
        // template. Its trigger stops propagation, so clicking it must not bubble into the header's
        // click-to-sort handler; clicking the (non-interactive) content area around it still sorts.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.TitleContent, (RenderFragment)(b =>
                {
                    b.AddContent(0, "Age ");
                    b.OpenComponent<LabelTooltip>(1);
                    b.AddAttribute(2, "Id", "age-info");
                    b.AddAttribute(3, "Tooltip", "Age in years");
                    b.CloseComponent();
                }))
                .Add(c => c.SortBy, (a, b) => a.Age.CompareTo(b.Age))
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Age.ToString()))));

        string AriaSort() => cut.Find("thead th").GetAttribute("aria-sort")!;
        Assert.Equal("none", AriaSort());

        // The tooltip trigger stops propagation, which bUnit models by cutting the bubble path:
        // the click reaches no onclick handler at all (the trigger itself has none — only the
        // stopPropagation/preventDefault directives), so bUnit throws instead of sorting. That
        // exception IS the assertion that the ancestor's click-to-sort handler can't be reached.
        Assert.Throws<Bunit.MissingEventHandlerException>(
            () => cut.Find(".edit-tooltip-container").Click());
        Assert.Equal("none", AriaSort()); // must not have toggled the sort

        cut.Find(".wss-table-sort-content").Click();
        Assert.Equal("ascending", AriaSort());
    }

    [Fact]
    public void Sortable_column_without_TitleContent_keeps_the_original_single_button_structure()
    {
        // Committed E2E visual baselines and other bUnit assertions depend on this exact DOM shape
        // — a templated header must not perturb the overwhelmingly common (no TitleContent) case.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        var th = cut.Find("thead th");
        Assert.Single(th.Children);
        var button = th.Children[0];
        Assert.Equal("button", button.TagName, ignoreCase: true);
        Assert.Contains("wss-table-sort-trigger", button.ClassList);
        Assert.DoesNotContain("wss-table-sort-trigger-icon", button.ClassList);

        Assert.Equal(2, button.Children.Length);
        Assert.Contains("wss-table-sort-label", button.Children[0].ClassList);
        Assert.Contains("wss-table-sorter", button.Children[1].ClassList);
    }

    // ----- IsRowSelectable -----

    [Fact]
    public void IsRowSelectable_disables_the_matching_row_checkbox_and_excludes_it_from_select_all()
    {
        var people = Sample(); // Alice(30), Bob(25)
        List<Person>? selected = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.IsRowSelectable, (Person x) => x.Name != "Bob")
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var rowCheckboxes = cut.FindAll("tbody input.wss-table-checkbox");
        Assert.False(rowCheckboxes[0].HasAttribute("disabled")); // Alice: selectable
        Assert.True(rowCheckboxes[1].HasAttribute("disabled"));  // Bob: not selectable

        // Select-all only selects the selectable row (Alice); Bob is left out entirely.
        cut.Find("thead input.wss-table-checkbox").Change(true);
        Assert.Single(selected!);
        Assert.Equal("Alice", selected![0].Name);

        // The header still reports fully-checked (not mixed) because Bob doesn't count.
        Assert.True(cut.Find("thead input.wss-table-checkbox").HasAttribute("checked"));
    }

    [Fact]
    public void IsRowSelectable_rejecting_every_row_on_the_page_disables_the_header_checkbox()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.IsRowSelectable, (Person _) => false)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.True(cut.Find("thead input.wss-table-checkbox").HasAttribute("disabled"));
    }

    [Fact]
    public void IsRowSelectable_null_leaves_no_disabled_attribute_anywhere()
    {
        // Guards the byte-identical-DOM contract: untouched (default null), no row or header
        // checkbox gains a disabled attribute regardless of page contents.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.All(cut.FindAll("input.wss-table-checkbox"), cb => Assert.False(cb.HasAttribute("disabled")));
    }

    [Fact]
    public void Runtime_IsRowSelectable_change_with_nothing_else_changed_still_disables_the_header_checkbox()
    {
        // RebuildPageItems's memo guard used to compare only _sorted/_page/_pageSize, so a runtime
        // IsRowSelectable (or SelectionMode) change alone -- nothing else different -- let it skip
        // the rebuild and, with it, RecomputeSelectionFlags: the header checkbox's disabled/
        // indeterminate state went stale.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.False(cut.Find("thead input.wss-table-checkbox").HasAttribute("disabled"));

        cut.Render(p => p.Add(t => t.IsRowSelectable, (Person _) => false));

        Assert.True(cut.Find("thead input.wss-table-checkbox").HasAttribute("disabled"));
    }

    // ----- SelectionMode.Single -----

    [Fact]
    public void SelectionMode_Single_renders_radios_and_an_empty_header_cell()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Empty(cut.FindAll("thead input")); // no select-all control
        var headerCell = cut.Find("thead .wss-table-selection-cell");
        Assert.Equal(string.Empty, headerCell.TextContent.Trim());

        var radios = cut.FindAll("tbody input[type=radio].wss-table-radio");
        Assert.Equal(2, radios.Count);
        Assert.Empty(cut.FindAll("tbody input.wss-table-checkbox"));
    }

    [Fact]
    public void SelectionMode_Single_picking_a_row_replaces_any_previous_selection()
    {
        List<Person>? selected = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice, Bob
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var radios = cut.FindAll("tbody input[type=radio].wss-table-radio");
        radios[0].Change(true); // pick Alice
        Assert.Single(selected!);
        Assert.Equal("Alice", selected![0].Name);

        radios = cut.FindAll("tbody input[type=radio].wss-table-radio");
        radios[1].Change(true); // pick Bob -- replaces Alice, never both
        Assert.Single(selected!);
        Assert.Equal("Bob", selected![0].Name);
    }

    [Fact]
    public void Runtime_switch_from_Multiple_to_Single_clamps_to_the_first_selected_row()
    {
        // Entry point (a): a runtime Multiple -> Single mode switch with more than one row already
        // checked used to leave every checked box's radio-semantics counterpart checked too --
        // multiple checked radios in one native name group, which is not a valid radio state.
        List<Person>? selected = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice, Bob
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Multiple)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        // Select both rows while still in Multiple mode.
        cut.Find("thead input.wss-table-checkbox").Change(true);
        Assert.Equal(2, selected!.Count);

        // Switch to Single: exactly one radio must end up checked, and the parent must be told the
        // selection was pruned (SelectedItemsChanged fires with the clamped, single-item list).
        cut.Render(p => p.Add(t => t.SelectionMode, SelectionMode.Single));

        var radios = cut.FindAll("tbody input[type=radio].wss-table-radio");
        Assert.Equal(2, radios.Count);
        Assert.Single(radios, r => r.HasAttribute("checked"));
        Assert.NotNull(selected);
        Assert.Single(selected!);
        Assert.Equal("Alice", selected![0].Name); // first (insertion-order) row kept
    }

    [Fact]
    public void Controlled_SelectedItems_with_two_items_under_Single_clamps_to_the_first()
    {
        // Entry point (b): a controlled SelectedItems handing in 2+ items while SelectionMode is
        // already Single used to render every matching row's radio checked -- only the user-driven
        // SelectSingleAsync (picking a row by hand) enforced exclusivity before this fix.
        List<Person>? selected = null;
        var people = Sample(); // Alice, Bob
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.SelectedItems, new List<Person> { people[0], people[1] }) // both -- invalid for Single
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var radios = cut.FindAll("tbody input[type=radio].wss-table-radio");
        Assert.Single(radios, r => r.HasAttribute("checked"));
        Assert.NotNull(selected);
        Assert.Single(selected!);
        Assert.Equal("Alice", selected![0].Name); // first item of SelectedItems kept
    }

    [Fact]
    public void Switching_Single_back_to_Multiple_applies_the_indeterminate_state()
    {
        // Single mode renders no select-all <input> at all, so OnAfterRenderAsync had nothing to mirror
        // onto -- but its early return only checked !Selectable, so it "applied" the mixed state to a
        // default ElementReference (a silent no-op) and still recorded _lastIndeterminate. The switch
        // back to Multiple then short-circuited against that stale mirror and left the real, freshly
        // created header checkbox plain-unchecked, announced as "not checked" instead of "mixed".
        var data = Sample(); // Alice, Bob
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.SelectedItems, new List<Person> { data[0] }) // one of two -> partial selection
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        int IndeterminateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "setIndeterminate");
        Assert.Empty(cut.FindAll("thead input.wss-table-checkbox")); // no element to mirror onto...
        Assert.Equal(0, IndeterminateCalls());                       // ...so no JS call is even attempted

        cut.Render(p => p.Add(t => t.SelectionMode, SelectionMode.Multiple));

        Assert.NotNull(cut.Find("thead input.wss-table-checkbox"));
        Assert.Equal(1, IndeterminateCalls());
    }

    // ----- Controlled expansion / OnExpand -----

    [Fact]
    public void OnExpand_fires_with_the_item_and_new_state_uncontrolled()
    {
        (Person Item, bool Expanded)? raised = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.OnExpand, EventCallback.Factory.Create<(Person, bool)>(this, v => raised = v))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click();
        Assert.NotNull(raised);
        Assert.Equal("Alice", raised!.Value.Item.Name);
        Assert.True(raised.Value.Expanded);

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click();
        Assert.False(raised.Value.Expanded);
    }

    [Fact]
    public void ExpandedRowKeys_controls_which_rows_render_expanded()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice, Bob
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandedRowKeys, new List<object> { "Bob" })
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var detail = cut.Find(".wss-table-expanded-row .wss-table-expanded-cell");
        Assert.Contains("Detail for Bob", detail.TextContent);

        cut.Render(p => p.Add(t => t.ExpandedRowKeys, new List<object> { "Alice" }));
        detail = cut.Find(".wss-table-expanded-row .wss-table-expanded-cell");
        Assert.Contains("Detail for Alice", detail.TextContent);
    }

    [Fact]
    public void ExpandedRowKeysChanged_raises_the_full_expanded_set_after_a_toggle()
    {
        IEnumerable<object>? changed = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandedRowKeysChanged,
                EventCallback.Factory.Create<IEnumerable<object>>(this, v => changed = v)));

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click();
        Assert.Equal(new object[] { "Alice" }, changed);
    }

    // ----- OnRowClick / ExpandRowByClick -----

    [Fact]
    public void OnRowClick_fires_with_the_clicked_item()
    {
        Person? clicked = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody .wss-table-row")[1].Click();
        Assert.Equal("Bob", clicked!.Name);
        Assert.Contains("wss-table-row-clickable", cut.FindAll("tbody .wss-table-row")[1].ClassList);
    }

    [Fact]
    public void ExpandRowByClick_toggles_expansion_from_a_row_click_and_OnRowClick_still_fires()
    {
        Person? clicked = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandRowByClick, true)
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody .wss-table-row")[0].Click();

        Assert.Equal("Alice", clicked!.Name); // OnRowClick still fires
        Assert.Single(cut.FindAll(".wss-table-expanded-row")); // and expansion toggled
    }

    [Fact]
    public void Clicking_the_expand_chevron_does_not_also_toggle_via_ExpandRowByClick()
    {
        // The chevron's own click stops propagation, so a row click driven by ExpandRowByClick
        // cannot ALSO fire for the same physical click and immediately re-collapse the row.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandRowByClick, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody .wss-table-expand-btn")[0].Click();
        Assert.Single(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void Clicking_the_selection_checkbox_does_not_raise_OnRowClick()
    {
        Person? clicked = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        // bUnit models stopPropagation by cutting the bubble path entirely: the click reaches no
        // onclick handler at all (the cell itself has none, only the stopPropagation directive), so
        // bUnit throws instead of bubbling into the row. That exception IS the assertion that the
        // row's OnRowClick can't be reached from here (same pattern as the existing sortable-header
        // TitleContent test).
        Assert.Throws<Bunit.MissingEventHandlerException>(() => cut.Find("tbody .wss-table-selection-cell").Click());
        Assert.Null(clicked);
    }

    [Fact]
    public void Clicking_inside_an_ActionColumn_cell_does_not_raise_OnRowClick()
    {
        Person? clicked = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name))
            .AddChildContent<ActionColumn<Person>>(cp => cp
                .Add(c => c.ChildContent, (RenderFragment<Person>)(_ => b => b.AddMarkupContent(0, "<button type=\"button\">Edit</button>")))));

        Assert.Throws<Bunit.MissingEventHandlerException>(() => cut.Find("tbody .wss-table-actions").Click());
        Assert.Null(clicked);
    }

    [Fact]
    public void Clicking_an_ActionColumn_cells_padding_does_not_toggle_ExpandRowByClick()
    {
        // The guard used to sit on the inner .wss-table-actions div, which is inline-flex -- it only
        // covers the buttons. .wss-table-cell has 16px of padding around them, and a click there
        // bubbled straight into the row handler, expanding the row the consumer was trying to act on.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandRowByClick, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name))
            .AddChildContent<ActionColumn<Person>>(cp => cp
                .Add(c => c.ChildContent, (RenderFragment<Person>)(_ => b => b.AddMarkupContent(0, "<button type=\"button\">Edit</button>")))));

        // Cells of the first row: [0] the expand chevron's own cell, [1] Name, [2] the actions.
        IElement Cell(int index) =>
            cut.FindAll("tbody .wss-table-row")[0].QuerySelectorAll("td.wss-table-cell")[index];

        // A click on an ordinary cell does toggle the row (the behavior being protected from here).
        Cell(1).Click();
        Assert.Single(cut.FindAll(".wss-table-expanded-row"));
        Cell(1).Click();
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));

        // The action column's <td> -- the padding around the buttons -- is severed instead: bUnit
        // models stopPropagation by cutting the bubble path, so the click reaches no handler at all
        // and throws. That exception IS the assertion that the row's toggle can't be reached.
        var actionCell = Cell(2);
        Assert.Throws<Bunit.MissingEventHandlerException>(() => actionCell.Click());
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void Rows_are_not_clickable_looking_when_neither_OnRowClick_nor_ExpandRowByClick_is_set()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.All(cut.FindAll("tbody .wss-table-row"),
            row => Assert.DoesNotContain("wss-table-row-clickable", row.ClassList));
    }

    // ----- Column.Ellipsis -----

    [Fact]
    public void Ellipsis_false_leaves_the_cell_as_bare_text_with_no_title_and_no_fixed_layout()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.DoesNotContain("wss-table-fixed", cut.Find("table.wss-table").ClassList);
        var cell = cut.FindAll("tbody .wss-table-cell").First(c => c.TextContent.Contains("Alice"));
        Assert.Null(cell.QuerySelector("span"));
    }

    [Fact]
    public void Ellipsis_true_adds_the_truncation_class_fixed_layout_and_a_title_span_for_PropertyColumn()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.Ellipsis, true)));

        Assert.Contains("wss-table-fixed", cut.Find("table.wss-table").ClassList);
        var cell = cut.FindAll("tbody .wss-table-cell").First(c => c.TextContent.Contains("Alice"));
        Assert.Contains("wss-table-cell-ellipsis", cell.ClassList);
        var span = cell.QuerySelector("span");
        Assert.NotNull(span);
        Assert.Equal("Alice", span!.GetAttribute("title"));
    }

    [Fact]
    public void Ellipsis_on_a_custom_Column_gets_the_truncation_class_but_no_title()
    {
        // Custom ChildContent is arbitrary markup, not a string the base class computed -- no title.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Ellipsis, true)
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Name))));

        var cell = cut.FindAll("tbody .wss-table-cell").First(c => c.TextContent.Contains("Alice"));
        Assert.Contains("wss-table-cell-ellipsis", cell.ClassList);
        Assert.False(cell.HasAttribute("title"));
        Assert.Null(cell.QuerySelector("span"));
    }

    // ----- Loading overlay -----

    [Fact]
    public void Loading_false_renders_no_overlay_and_no_aria_busy()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Empty(cut.FindAll(".wss-table-loading-mask"));
        // aria-busy lives on the root (it now spans the pagers too, not just the wrapper) -- see the
        // pager-masking tests below.
        Assert.False(cut.Find(".wss-table-root").HasAttribute("aria-busy"));
    }

    [Fact]
    public void Loading_true_renders_the_overlay_over_still_rendered_rows_with_aria_busy()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Loading, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Single(cut.FindAll(".wss-table-loading-mask"));
        Assert.Equal("true", cut.Find(".wss-table-root").GetAttribute("aria-busy"));
        // Rows are still rendered beneath the mask, not replaced by it.
        Assert.Equal(2, cut.FindAll("tbody .wss-table-row").Count);
    }

    [Fact]
    public void Loading_true_masks_the_pager_too_not_just_the_table_body()
    {
        // The mask used to live inside .wss-table-wrapper (absolute/inset:0 against it), so it never
        // covered the pager blocks, which are wrapper siblings -- the pager stayed visually
        // uncovered and clickable while Loading. It's now anchored to .wss-table-root (the common
        // ancestor of both pagers and the wrapper), so it renders as a root-level element sitting
        // structurally over the whole component, pager included.
        var people = Enumerable.Range(1, 3).Select(i => new Person($"P{i}", i)).ToList();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.PageSize, 1) // force a pager to render
            .Add(t => t.Loading, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var root = cut.Find(".wss-table-root");
        // The mask is a direct child of the root -- a sibling of the pager and the wrapper, not
        // nested inside the wrapper -- so its inset:0/z-index covers the pager area too.
        Assert.Contains(root.Children, c => c.ClassList.Contains("wss-table-loading-mask"));
        Assert.DoesNotContain(cut.Find(".wss-table-wrapper").Children, c => c.ClassList.Contains("wss-table-loading-mask"));
        Assert.NotEmpty(cut.FindAll(".wss-table-pagination"));

        // Not asserted here: "clicking next-page does nothing while loading". bUnit invokes the
        // clicked element's own Blazor event handler directly -- it doesn't hit-test overlapping
        // absolutely-positioned elements the way a real browser does, so a pager button click would
        // "work" in bUnit regardless of the mask sitting visually on top of it in a real DOM. The
        // real click-blocking behavior is a browser-level effect of this structural placement, not
        // something bUnit can observe; it's implicit in the DOM shape asserted above.
    }

    // ----- EmptyContent -----

    [Fact]
    public void EmptyContent_wins_over_EmptyText_when_set()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, new List<Person>())
            .Add(t => t.EmptyText, "Plain text")
            .Add(t => t.EmptyContent, (RenderFragment)(b => b.AddMarkupContent(0, "<strong class=\"custom-empty\">Nothing to show</strong>")))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var placeholder = cut.Find(".wss-table-placeholder");
        Assert.NotNull(placeholder.QuerySelector("strong.custom-empty"));
        Assert.DoesNotContain("Plain text", placeholder.TextContent);
    }

    // ----- FooterContent (summary row) -----

    [Fact]
    public void FooterContent_renders_in_a_tfoot_after_the_body_and_is_unaffected_by_paging()
    {
        var people = Enumerable.Range(1, 3).Select(i => new Person($"P{i}", i * 10)).ToList();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.PageSize, 2)
            .Add(t => t.FooterContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "tr");
                b.OpenElement(1, "td");
                b.AddAttribute(2, "class", "wss-table-cell");
                b.AddContent(3, "Total: 60");
                b.CloseElement();
                b.CloseElement();
            }))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var tfoot = cut.Find("tfoot.wss-table-tfoot");
        Assert.Contains("Total: 60", tfoot.TextContent);

        // The footer sits after the tbody in document order.
        var table = cut.Find("table.wss-table");
        var tagNames = table.Children.Select(c => c.TagName.ToLowerInvariant()).ToArray();
        Assert.Equal("tfoot", tagNames.Last());
    }

    [Fact]
    public void FooterContent_null_renders_no_tfoot()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Empty(cut.FindAll("tfoot"));
    }

    // ----- Pager wire-through (ShowTotal / PageSizeOptions) -----

    [Fact]
    public void ShowTotal_forwards_to_the_embedded_pager()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.PageSize, 1)
            .Add(t => t.ShowTotal, (Func<(int Start, int End, int Total), string>)(w => $"{w.Start}-{w.End} of {w.Total}"))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Equal("1-1 of 2", cut.Find(".wss-pagination-total").TextContent);
    }

    [Fact]
    public void PageSizeOptions_forwards_a_size_changer_that_reslices_the_table()
    {
        var people = Enumerable.Range(1, 20).Select(i => new Person($"P{i}", i)).ToList();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.PageSize, 5)
            .Add(t => t.PageSizeOptions, new[] { 5, 10 })
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Equal(5, cut.FindAll("tbody .wss-table-row").Count);

        cut.Find(".wss-pagination-size-select").Change("10");

        Assert.Equal(10, cut.FindAll("tbody .wss-table-row").Count);
    }

    [Fact]
    public void PageSizeOptions_change_preserves_selection_across_the_reslice()
    {
        var people = Enumerable.Range(1, 20).Select(i => new Person($"P{i}", i)).ToList();
        List<Person>? selected = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.PageSize, 5)
            .Add(t => t.PageSizeOptions, new[] { 5, 10 })
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(true); // select P1
        Assert.Single(selected!);

        cut.Find(".wss-pagination-size-select").Change("10");

        // The selection (keyed by row identity) survives the page-size change untouched.
        Assert.Single(selected!);
        Assert.Equal("P1", selected![0].Name);
        Assert.True(cut.FindAll("tbody input.wss-table-checkbox")[0].HasAttribute("checked"));
    }

    [Fact]
    public void Toggling_UseStyledCheckbox_reapplies_the_indeterminate_state()
    {
        var data = Sample(); // Alice, Bob
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItems, new List<Person> { data[0] }) // partial selection -> mixed state
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        int IndeterminateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "setIndeterminate");
        Assert.Equal(1, IndeterminateCalls());

        // The styled/unstyled branches have different DOM shapes (see EffectiveUseStyledCheckbox):
        // swapping UseStyledCheckbox recreates the <input>, so the stale _lastIndeterminate mirror
        // must not short-circuit the re-apply.
        cut.Render(p => p.Add(t => t.UseStyledCheckbox, true));
        Assert.Equal(2, IndeterminateCalls());

        cut.Render(p => p.Add(t => t.UseStyledCheckbox, false));
        Assert.Equal(3, IndeterminateCalls());
    }

    // ----- Column filtering (FilterOptions/OnFilter) -----

    static List<TableFilterOption> NameOptions() =>
        [new("Alice", "Alice"), new("Bob", "Bob"), new("Carol", "Carol")];

    IRenderedComponent<Table<Person>> RenderNameFilterable(
        List<Person>? data = null,
        bool filterMultiple = true,
        EventCallback<(Column<Person> Column, IReadOnlyList<string> SelectedValues)>? onFilterChanged = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, data ?? new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) });
            if (onFilterChanged is not null) p.Add(t => t.OnFilterChanged, onFilterChanged.Value);
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))
                .Add(c => c.FilterMultiple, filterMultiple));
        });

    string[] RenderedNames(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("tbody .wss-table-row td.wss-table-cell").Select(td => td.TextContent.Trim()).ToArray();

    void CheckOption(IRenderedComponent<Table<Person>> cut, string text) =>
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains(text))
            .QuerySelector("input")!.Change(true);

    [Fact]
    public void Non_filterable_column_renders_no_filter_button_or_wrapper_DOM_unchanged()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Empty(cut.FindAll(".wss-table-header-inner"));
        // Exactly the pre-existing shape: one bare text-only header cell.
        Assert.Single(cut.Find("thead th").ChildNodes);
    }

    [Fact]
    public void FilterOptions_without_OnFilter_renders_no_filter_button()
    {
        // CanFilter requires BOTH -- a column that forgot OnFilter must not render dead UI.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())));

        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
    }

    [Fact]
    public void Filterable_column_renders_a_filter_button_with_an_accessible_name()
    {
        var cut = RenderNameFilterable();

        var button = cut.Find(".wss-table-filter-trigger");
        Assert.Equal("Filter Name", button.GetAttribute("aria-label"));
        Assert.Equal("false", button.GetAttribute("aria-expanded"));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
    }

    IRenderedComponent<Table<Person>> RenderHeaderlessFilterable(string? filterLabel = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, Sample());
            if (filterLabel is not null) p.Add(t => t.FilterLabel, filterLabel);
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Property, x => x.Name) // no Title: the filter button needs the fallback
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)));
        });

    [Fact]
    public void FilterLabel_defaults_to_Filter_for_a_headerless_column()
    {
        // Byte-identical to before FilterLabel existed: the fallback was a hardcoded "Filter" literal.
        var cut = RenderHeaderlessFilterable();

        Assert.Equal("Filter", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void FilterLabel_is_overridable_for_a_headerless_column()
    {
        var cut = RenderHeaderlessFilterable("Filtrar");

        Assert.Equal("Filtrar", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_column_with_a_header_uses_FilterButtonLabelFormat_not_FilterLabel()
    {
        // A custom FilterLabel must not leak into the header-present branch, which stays on
        // FilterButtonLabelFormat -- mirrors SortLabel only naming the title-less fallback.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.FilterLabel, "Filtrar")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        Assert.Equal("Filter Name", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void Clicking_the_filter_button_opens_a_dropdown_with_a_checkbox_per_option()
    {
        var cut = RenderNameFilterable();

        cut.Find(".wss-table-filter-trigger").Click();

        Assert.Equal("true", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-expanded"));
        Assert.Equal(3, cut.FindAll(".wss-table-filter-checkbox").Count);
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-ok"));
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-reset"));
    }

    [Fact]
    public void Checking_a_value_and_clicking_OK_narrows_the_rows_to_that_column()
    {
        var cut = RenderNameFilterable();

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice"], RenderedNames(cut));
        // Dropdown closes and the button reports its active (filtered) state.
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    [Fact]
    public void Filter_OR_semantics_selecting_two_values_in_one_column_shows_both()
    {
        var cut = RenderNameFilterable();

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        CheckOption(cut, "Carol");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice", "Carol"], RenderedNames(cut));
    }

    [Fact]
    public void Filter_AND_semantics_across_two_columns()
    {
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)))
            .AddChildContent<Column<Person>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.ChildContent, (Person x) => b => b.AddContent(0, x.Age))
                .Add(c => c.FilterOptions, (IReadOnlyList<TableFilterOption>)[new("Old", "old"), new("Young", "young")])
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => v == "old" ? x.Age > 25 : x.Age <= 25))));

        // Name in {Alice, Bob} (excludes Carol) AND Age = old (>25, excludes Bob) -> Alice only.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        CheckOption(cut, "Alice");
        CheckOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();

        cut.FindAll(".wss-table-filter-trigger")[1].Click();
        CheckOption(cut, "Old");
        cut.Find(".wss-table-filter-ok").Click();

        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Alice"], names);
    }

    [Fact]
    public void Reset_clears_the_column_filter_and_shows_every_row_again()
    {
        var cut = RenderNameFilterable();

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice"], RenderedNames(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    [Fact]
    public void Clicking_outside_the_dropdown_closes_it_without_applying_pending_changes()
    {
        var cut = RenderNameFilterable();

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice"); // staged, not yet applied
        cut.Find(".wss-table-filter-backdrop").Click(); // outside click -- discard, don't apply

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut)); // unfiltered -- nothing was applied
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));

        // Re-opening must not resurrect the discarded pending check -- it re-syncs from Applied
        // (still empty), not from whatever was left in Pending.
        cut.Find(".wss-table-filter-trigger").Click();
        var aliceCheckbox = cut.FindAll(".wss-table-filter-item")
            .First(li => li.TextContent.Contains("Alice")).QuerySelector("input")!;
        Assert.False(aliceCheckbox.HasAttribute("checked"));
    }

    [Fact]
    public void FilterMultiple_false_renders_radios_and_only_the_latest_pick_stays_selected()
    {
        var cut = RenderNameFilterable(filterMultiple: false);

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal(3, cut.FindAll(".wss-table-filter-radio").Count);
        Assert.Empty(cut.FindAll(".wss-table-filter-checkbox"));

        CheckOption(cut, "Alice");
        CheckOption(cut, "Bob"); // replaces Alice, never both
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Bob"], RenderedNames(cut));
    }

    [Fact]
    public void Filtering_applies_before_sorting()
    {
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, (IReadOnlyList<TableFilterOption>)[new("Alice", "Alice"), new("Carol", "Carol")])
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.Sortable, true)));

        // Filter out Bob, leaving Alice(30)/Carol(40).
        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        CheckOption(cut, "Carol");
        cut.Find(".wss-table-filter-ok").Click();

        // Sort descending by Age: if filtering ran AFTER sorting instead, Bob (excluded either way)
        // couldn't reveal the bug here -- but the row COUNT confirms only the filtered two remain,
        // and the order confirms sort still ran over exactly that filtered set.
        // Re-Find (not a cached element reference) between clicks -- ToggleSort re-renders, and a
        // stale reference's event handler ID no longer exists in the new render tree.
        cut.Find("button.wss-table-sort-trigger").Click(); // ascending
        cut.Find("button.wss-table-sort-trigger").Click(); // descending: Carol(40), Alice(30)

        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Carol", "Alice"], names);
    }

    [Fact]
    public void Filter_button_click_on_a_sortable_column_does_not_toggle_the_sort()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.Sortable, true)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        cut.Find(".wss-table-filter-trigger").Click();

        Assert.Equal("none", cut.Find("thead th").GetAttribute("aria-sort")); // unchanged
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-dropdown")); // but the filter did open
    }

    [Fact]
    public void OnFilterChanged_raises_the_column_and_selected_values_on_OK_and_empty_on_Reset()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderNameFilterable(onFilterChanged: EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.NotNull(raised);
        Assert.Equal(["Bob"], raised!.Value.Values);

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();

        Assert.NotNull(raised);
        Assert.Empty(raised!.Value.Values);
    }

    [Fact]
    public void Selected_rows_that_get_filtered_out_stay_in_SelectedItems()
    {
        List<Person>? selected = null;
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectedItemsChanged,
                EventCallback.Factory.Create<IEnumerable<Person>>(this, s => selected = s.ToList()))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        // Select Bob (row 1).
        cut.FindAll("tbody input.wss-table-checkbox")[1].Change(true);
        Assert.Single(selected!);
        Assert.Equal("Bob", selected![0].Name);

        // Filter down to Alice only -- Bob disappears from view but must stay selected (same
        // key-based preservation as paging).
        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        // :last-child, not the shared RenderedNames helper -- Selectable adds a leading selection
        // <td> that also carries the plain wss-table-cell class.
        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell:last-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Alice"], names);
        Assert.Single(selected!);
        Assert.Equal("Bob", selected![0].Name); // unchanged -- still selected, just not rendered
    }

    [Fact]
    public void Applying_a_filter_after_unrelated_parent_rerenders_still_narrows_the_rows()
    {
        // The exact staleness class that has bitten this file twice before (see
        // Runtime_IsRowSelectable_change_with_nothing_else_changed_still_disables_the_header_checkbox):
        // a few no-op parent re-renders (RebuildPageItems's guard correctly skips them) must not
        // leave anything in a state where a REAL filter change afterward fails to take effect.
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        cut.Render(p => p.Add(t => t.DataSource, data));
        cut.Render(p => p.Add(t => t.DataSource, data));
        cut.Render(p => p.Add(t => t.DataSource, data));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice"], RenderedNames(cut));
    }

    [Fact]
    public void Empty_placeholder_renders_once_a_filter_excludes_every_row()
    {
        var cut = RenderNameFilterable(new List<Person> { new("Alice", 30), new("Bob", 25) });

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Carol"); // matches nobody in this DataSource
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Empty(cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)"));
        Assert.NotEmpty(cut.FindAll(".wss-table-placeholder"));
    }

    [Fact]
    public void Filtered_pager_total_reflects_the_narrowed_row_count()
    {
        var data = Enumerable.Range(1, 20).Select(i => new Person($"Item{i}", i)).ToList();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.PageSize, 5)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, (IReadOnlyList<TableFilterOption>)[new("Item1", "Item1"), new("Item2", "Item2")])
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        Assert.Equal(5, cut.FindAll("tbody .wss-table-row").Count); // page 1 of 20, unfiltered

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Item1");
        CheckOption(cut, "Item2");
        cut.Find(".wss-table-filter-ok").Click();

        // Only 2 rows survive the filter -- one page, no stranding on a now-nonexistent page 2+.
        Assert.Equal(2, cut.FindAll("tbody .wss-table-row").Count);
    }

    // ----- No-op OK/Reset must not reset the page (Fix 3) -----

    IRenderedComponent<Table<Person>> RenderPagedEvenFilterable(EventCallback<(Column<Person>, IReadOnlyList<string>)>? onFilterChanged = null)
    {
        var data = Enumerable.Range(1, 30).Select(i => new Person($"Item{i}", i)).ToList(); // Age = i
        return Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, data);
            p.Add(t => t.PageSize, 10); // 3 pages of 10
            if (onFilterChanged is not null) p.Add(t => t.OnFilterChanged, onFilterChanged.Value);
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, (IReadOnlyList<TableFilterOption>)[new("Even", "even")])
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Age % 2 == 0)));
        });
    }

    [Fact]
    public void A_no_op_OK_click_with_nothing_ticked_does_not_reset_the_page()
    {
        var cut = RenderPagedEvenFilterable();

        cut.FindAll(".wss-pagination-item")[2].Click(); // page 3
        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent);

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-ok").Click(); // nothing ticked -- Applied is empty before and after

        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent);
    }

    [Fact]
    public void Reset_on_an_already_empty_filter_does_not_reset_the_page()
    {
        var cut = RenderPagedEvenFilterable();

        cut.FindAll(".wss-pagination-item")[2].Click(); // page 3
        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent);

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click(); // never had anything applied

        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent);
    }

    [Fact]
    public void A_real_filter_change_resets_to_page_1()
    {
        var cut = RenderPagedEvenFilterable();

        cut.FindAll(".wss-pagination-item")[2].Click(); // page 3 of 3
        Assert.Equal("3", cut.Find(".wss-pagination-item-active").TextContent);

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Even");
        cut.Find(".wss-table-filter-ok").Click();

        // 15 even rows / page size 10 = 2 pages -- must land back on page 1, not stay stranded on
        // the now out-of-range page 3.
        Assert.Equal(2, cut.FindAll(".wss-pagination-item").Count);
        Assert.Equal("1", cut.Find(".wss-pagination-item-active").TextContent);
    }

    [Fact]
    public void OnFilterChanged_does_not_fire_on_a_no_op_OK_or_Reset_only_on_a_real_change()
    {
        var fireCount = 0;
        var cut = RenderPagedEvenFilterable(EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, _ => fireCount++));

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-ok").Click(); // no-op OK
        Assert.Equal(0, fireCount);

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click(); // no-op Reset
        Assert.Equal(0, fireCount);

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Even");
        cut.Find(".wss-table-filter-ok").Click(); // real change
        Assert.Equal(1, fireCount);
    }

    // ----- A filtered column removed from the render tree raises OnFilterChanged (Fix 4) -----

    [Fact]
    public void Hiding_a_column_with_an_active_filter_raises_OnFilterChanged_with_an_empty_payload()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var showName = true;
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            if (showName)
            {
                builder.OpenComponent<PropertyColumn<Person, string>>(0);
                builder.AddAttribute(1, "Title", "Name");
                builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
                builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)NameOptions());
                builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
                builder.CloseComponent();
            }

            builder.OpenComponent<PropertyColumn<Person, int>>(5);
            builder.AddAttribute(6, "Title", "Age");
            builder.AddAttribute(7, "Property", (Func<Person, int>)(x => x.Age));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.NotNull(raised);
        Assert.Equal(["Alice"], raised!.Value.Values);
        Assert.Single(cut.FindAll("tbody .wss-table-row"));

        raised = null;
        showName = false; // drop the filtered column entirely
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.NotNull(raised);
        Assert.Empty(raised!.Value.Values);
        // The row set stops being narrowed by the now-gone column's filter too.
        Assert.Equal(3, cut.FindAll("tbody .wss-table-row").Count);
    }

    // ----- Shared local render fragments (one copy per repeated block) -----

    // bUnit renders Blazor's event wiring as blazor:onclick="<handler id>"; the ids are per-render
    // counters, so they differ between two components (and between two blocks of one component)
    // without the markup differing at all.
    static string WithoutHandlerIds(string markup) =>
        System.Text.RegularExpressions.Regex.Replace(markup, "blazor:[a-zA-Z]+=\"[^\"]*\"", "");

    [Fact]
    public void The_sort_trigger_renders_identically_with_and_without_a_column_filter()
    {
        // The filterable and non-filterable sortable headers carried byte-identical copies of the
        // button + caret stack (a third, icon-only copy sits in the TitleContent branch). They share
        // one fragment now; this pins that they can't drift apart again.
        string Trigger(bool filterable)
        {
            var cut = Render<Table<Person>>(p =>
            {
                p.Add(t => t.DataSource, Sample());
                p.AddChildContent<PropertyColumn<Person, string>>(cp =>
                {
                    cp.Add(c => c.Title, "Name").Add(c => c.Property, x => x.Name).Add(c => c.Sortable, true);
                    if (filterable)
                    {
                        cp.Add(c => c.FilterOptions, NameOptions())
                          .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v));
                    }
                });
            });
            return WithoutHandlerIds(cut.Find("button.wss-table-sort-trigger").OuterHtml);
        }

        Assert.Equal(Trigger(false), Trigger(true));
    }

    [Fact]
    public void Top_and_bottom_pagers_render_the_same_block_apart_from_position_and_label()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.PageSize, 2)
            .Add(t => t.PagerPosition, PagerPosition.Both)
            .Add(t => t.PageSizeOptions, new[] { 2, 5 })
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var top = WithoutHandlerIds(cut.Find(".wss-table-pagination-top").OuterHtml)
            .Replace("wss-table-pagination-top", "wss-table-pagination-bottom")
            .Replace("Pagination (top)", "Pagination (bottom)");
        var bottom = WithoutHandlerIds(cut.Find(".wss-table-pagination-bottom").OuterHtml);

        Assert.Equal(top, bottom);
    }

    [Fact]
    public void Header_and_row_styled_checkboxes_share_the_same_box_wrapper()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.Selectable, true)
            .Add(t => t.UseStyledCheckbox, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        // Same wrapper + same drawn box either side; only the input's own label/state differ.
        var header = cut.Find("thead .wss-table-checkbox-wrap");
        var row = cut.Find("tbody .wss-table-checkbox-wrap");
        Assert.Equal(header.QuerySelector(".wss-table-checkbox-box")!.OuterHtml,
                     row.QuerySelector(".wss-table-checkbox-box")!.OuterHtml);
        Assert.Equal("wss-table-checkbox wss-table-checkbox-input-styled",
                     header.QuerySelector("input")!.GetAttribute("class"));
        Assert.Equal("wss-table-checkbox wss-table-checkbox-input-styled",
                     row.QuerySelector("input")!.GetAttribute("class"));
    }

    // ----- Runtime parameter changes on an already-registered column -----

    [Fact]
    public void Changing_a_bound_column_Title_updates_the_header_on_the_same_render_cycle()
    {
        // Register only queued a re-render for columns that were NEW to the rendered set, so a
        // same-set parameter change queued nothing: the Table's header is built from the column
        // instances BEFORE the diff reaches Column.SetParametersAsync, leaving
        // <Column Title="@($"Results ({count})")"> showing the previous value indefinitely -- until
        // some unrelated event happened to re-render the table.
        var title = "Results (2)";
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", title);
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal("Results (2)", cut.Find("thead th").TextContent.Trim());

        var rendersBefore = cut.RenderCount;
        title = "Results (7)";
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal("Results (7)", cut.Find("thead th").TextContent.Trim());
        // Bounded corrective re-render, not a runaway loop -- notifying on every pass would recurse
        // forever, since each Table render hands the column a fresh Property/ChildContent delegate.
        Assert.True(cut.RenderCount - rendersBefore <= 4);
    }

    [Fact]
    public void Repeated_renders_with_an_inline_FilterOptions_list_do_not_loop()
    {
        // FilterOptions built inline in markup is a brand-new list instance every pass; comparing it
        // by reference would report "changed" forever, and with the corrective render above that is
        // an infinite render loop rather than a stale header.
        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)NameOptions());
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));

        var rendersBefore = cut.RenderCount;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.True(cut.RenderCount - rendersBefore <= 4);
        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));
    }

    [Fact]
    public void Swapping_FilterOptions_prunes_applied_values_that_no_longer_exist()
    {
        // Data-derived options swap with the data. PassesFilter reads FilterApplied raw, so a value
        // that left the options kept excluding every row: an empty table, a dropdown with nothing
        // ticked to explain it (OK a no-op, only Reset recovering), and a consumer summary reporting
        // no filter, because AppliedFilterValues already intersects with the current options.
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var options = NameOptions(); // Alice, Bob, Carol
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice"], RenderedNames(cut));
        Assert.NotNull(raised);

        // Alice leaves the options.
        raised = null;
        options = [new("Bob", "Bob"), new("Carol", "Carol")];
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        // The orphaned value is dropped, so the column stops narrowing anything...
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        // ...and the consumer is told, with the surviving selection -- exactly what happens when a
        // filtered column drops out of the table. Pruning silently left a consumer's own filter-summary
        // display showing a value that no longer narrows anything, which is the defect the
        // removed-column path was already fixed for; one component cannot hold both policies.
        Assert.NotNull(raised);
        Assert.Empty(raised.Value.Values);

        // The dropdown re-opens on the new options with nothing ticked.
        cut.Find(".wss-table-filter-trigger").Click();
        var boxes = cut.FindAll(".wss-table-filter-checkbox");
        Assert.Equal(2, boxes.Count);
        Assert.All(boxes, b => Assert.False(b.HasAttribute("checked")));
    }

    [Fact]
    public void Swapping_FilterOptions_keeps_applied_values_that_survive()
    {
        // The other half of the prune: only the orphans go, and a still-offered value keeps filtering
        // (no silent full reset of the user's selection on every options refresh).
        var options = NameOptions();
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        CheckOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Bob"], RenderedNames(cut));

        options = [new("Bob", "Bob"), new("Carol", "Carol")]; // Alice gone, Bob stays
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    // Two columns, the first filterable only while it has options -- the shape a data-refresh takes
    // when a column's options are derived from rows that all left.
    static RenderFragment OptionalFilterColumns(IReadOnlyList<TableFilterOption>? options) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
        builder.AddAttribute(3, "FilterOptions", options);
        builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
        builder.CloseComponent();
        builder.OpenComponent<PropertyColumn<Person, int>>(5);
        builder.AddAttribute(6, "Title", "Age");
        builder.AddAttribute(7, "Property", (Func<Person, int>)(x => x.Age));
        builder.CloseComponent();
    };

    [Fact]
    public void A_column_that_stops_offering_a_filter_closes_its_open_dropdown()
    {
        // FilterOpen used to survive the column losing its filter entirely: the header kept the
        // wss-table-cell-filter-open promotion, and Table.AnyColumnFilterOpen reported true for the
        // rest of the table's life -- which makes every OTHER column's filter skip its focus restore
        // on close and drop focus to <body>.
        IReadOnlyList<TableFilterOption>? options = NameOptions();

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, OptionalFilterColumns(options)));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Single(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Contains("wss-table-cell-filter-open", cut.FindAll("thead th")[0].ClassList);

        options = null;
        cut.Render(p => p.Add(t => t.ChildContent, OptionalFilterColumns(options)));

        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.DoesNotContain("wss-table-cell-filter-open", cut.FindAll("thead th")[0].ClassList);

        var anyOpen = (bool)typeof(Table<Person>)
            .GetProperty("AnyColumnFilterOpen", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!;
        Assert.False(anyOpen);
    }

    [Fact]
    public void Options_coming_back_do_not_reopen_the_dropdown_on_their_own()
    {
        // The other consequence of the stuck flag: the dropdown (and its full-screen invisible
        // backdrop) reappeared already open the moment options returned, with no user interaction --
        // the next click anywhere on the page hit the backdrop instead of what it aimed at.
        IReadOnlyList<TableFilterOption>? options = NameOptions();

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, OptionalFilterColumns(options)));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Single(cut.FindAll(".wss-table-filter-dropdown"));

        options = null;
        cut.Render(p => p.Add(t => t.ChildContent, OptionalFilterColumns(options)));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));

        options = [new("Carol", "Carol")];
        cut.Render(p => p.Add(t => t.ChildContent, OptionalFilterColumns(options)));

        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Empty(cut.FindAll(".wss-table-filter-backdrop"));
    }

    [Fact]
    public void Mutating_the_same_FilterOptions_list_in_place_still_prunes_orphaned_values()
    {
        // The ordinary consumer shape for data-derived options is ONE List<TableFilterOption> field
        // refilled in place (RemoveAll, or Clear() + AddRange). That hands the column back the same
        // object with different contents, and the previous snapshot stored that very reference -- so
        // OptionsEqual's ReferenceEquals fast path compared the list to itself, reported "unchanged",
        // and the prune never ran. Alice kept excluding every other row with nothing ticked to
        // explain it.
        var options = new List<TableFilterOption> { new("Alice", "Alice"), new("Bob", "Bob"), new("Carol", "Carol") };
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice"], RenderedNames(cut));

        options.RemoveAll(o => o.Value == "Alice"); // same instance, new contents
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    [Fact]
    public void A_partial_prune_reports_the_values_that_survived_it()
    {
        // The other half of the notification: the payload is the selection that is still applied, not
        // an unconditional "empty" -- a consumer's filter summary has to keep showing Bob.
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var options = NameOptions();
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        CheckOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();

        raised = null;
        options = [new("Bob", "Bob"), new("Carol", "Carol")]; // Alice orphaned, Bob survives
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.NotNull(raised);
        Assert.Equal(["Bob"], raised.Value.Values);
        Assert.Equal(["Bob"], RenderedNames(cut));
    }

    [Fact]
    public void A_prune_that_removes_nothing_raises_nothing()
    {
        // The guard on the notification: options changing without orphaning an applied value is not a
        // filter change, and a consumer must not get a spurious "it changed" (or a render loop out of
        // hearing its own parameter change back).
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var options = NameOptions();

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", (Func<Person, string, bool>)((x, v) => x.Name == v));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        raised = null;
        var rendersBefore = cut.RenderCount;
        options = [new("Alice", "Alice"), new("Dave", "Dave")]; // Alice still offered
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Null(raised);
        Assert.True(cut.RenderCount - rendersBefore <= 4);
    }

    // ----- Swapped row-affecting delegates (Property / OnFilter / SortBy) -----

    [Fact]
    public void Swapping_the_Property_selector_updates_the_cells()
    {
        // Property flows through the identical CellFor that Format does, but only Format was tracked.
        // The justification for leaving delegates out -- "a lambda closes over the parent's state, so
        // its output is already current" -- does not hold when the parent SELECTS A DIFFERENT
        // delegate: nothing re-renders the table, so the cells showed the old property forever.
        Func<Person, string> selector = x => x.Name;
        var data = new List<Person> { new("Alice", 30) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Value");
            builder.AddAttribute(2, "Property", selector);
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));
        Assert.Equal("Alice", cut.Find("tbody td.wss-table-cell").TextContent.Trim());

        selector = x => x.Age.ToString();
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal("30", cut.Find("tbody td.wss-table-cell").TextContent.Trim());
    }

    [Fact]
    public void Swapping_OnFilter_while_a_filter_is_applied_re_derives_the_rows()
    {
        // _filtered is cached and only re-derived from the explicit mutation points, so a swapped
        // predicate (exact match -> contains) left the previously narrowed row set in place forever.
        Func<Person, string, bool> predicate = (x, v) => x.Name == v;
        var data = new List<Person> { new("Alice", 30), new("Alicia", 25), new("Bob", 40) };
        var options = new List<TableFilterOption> { new("Ali", "Ali"), new("Bob", "Bob") };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)options);
            builder.AddAttribute(4, "OnFilter", predicate);
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Ali");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Single(cut.FindAll("tbody .wss-table-placeholder")); // exact match: nothing is called "Ali"

        predicate = (x, v) => x.Name.Contains(v, StringComparison.Ordinal);
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Alice", "Alicia"], RenderedNames(cut));
    }

    [Fact]
    public void Swapping_SortBy_while_a_sort_is_active_re_orders_the_rows()
    {
        // Same class for _sorted: the comparison is only ever run from ToggleSort and the pipeline
        // entry points, so a swapped Comparison left the old order in place indefinitely.
        Comparison<Person> comparison = (a, b) => string.CompareOrdinal(a.Name, b.Name);
        var data = new List<Person> { new("Alice", 30), new("Bob", 25), new("Carol", 40) };

        RenderFragment Columns() => builder =>
        {
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
            builder.AddAttribute(3, "SortBy", comparison);
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));

        cut.Find(".wss-table-sort-trigger").Click(); // ascending by name
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));

        comparison = (a, b) => a.Age.CompareTo(b.Age);
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.Equal(["Bob", "Alice", "Carol"], RenderedNames(cut)); // 25, 30, 40
    }

    [Fact]
    public void Re_passing_an_equivalent_capturing_lambda_does_not_loop()
    {
        // The reason delegates were excluded in the first place, and the constraint the fix has to
        // respect: markup lambdas that capture a local are a fresh delegate object on every pass, so
        // comparing them by INSTANCE would report a change forever -- and each report queues another
        // Table render, which hands out fresh delegates again. Method identity is stable across those.
        var suffix = "!";
        var data = new List<Person> { new("Alice", 30) };

        RenderFragment Columns() => builder =>
        {
            var local = suffix;
            builder.OpenComponent<PropertyColumn<Person, string>>(0);
            builder.AddAttribute(1, "Title", "Name");
            builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name + local));
            builder.AddAttribute(3, "SortBy", (Comparison<Person>)((a, b) => string.CompareOrdinal(a.Name + local, b.Name + local)));
            builder.CloseComponent();
        };

        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ChildContent, Columns()));

        var rendersBefore = cut.RenderCount;
        cut.Render(p => p.Add(t => t.ChildContent, Columns()));

        Assert.True(cut.RenderCount - rendersBefore <= 4, $"render count ran away: {cut.RenderCount - rendersBefore}");
        Assert.Equal("Alice!", cut.Find("tbody td.wss-table-cell").TextContent.Trim());
    }

    // ----- ScrollY sticky header + Loading mask stacking (Fix 1) -----

    [Fact]
    public void Opening_a_column_filter_under_ScrollY_promotes_only_its_own_th()
    {
        var data = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, data)
            .Add(t => t.ScrollY, "160px")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)))
            .AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)));

        var headers = cut.FindAll("thead th");
        Assert.DoesNotContain("wss-table-cell-filter-open", headers[0].ClassList);
        Assert.DoesNotContain("wss-table-cell-filter-open", headers[1].ClassList);

        cut.Find(".wss-table-filter-trigger").Click();

        headers = cut.FindAll("thead th");
        Assert.Contains("wss-table-cell-filter-open", headers[0].ClassList); // Name's own th
        Assert.DoesNotContain("wss-table-cell-filter-open", headers[1].ClassList); // Age's th untouched

        cut.Find(".wss-table-filter-reset").Click();
        headers = cut.FindAll("thead th");
        Assert.DoesNotContain("wss-table-cell-filter-open", headers[0].ClassList); // closed again
    }

    // ----- Column-filter focus hand-off + the fixed-dropdown activation handle -----

    // Two independently filterable columns: the shape that exercises "opening one filter closes the
    // other" (Table.OpenColumnFilter) and the focus hand-off between them.
    IRenderedComponent<Table<Person>> RenderTwoFilterableColumns(string? scrollY = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, new List<Person> { new("Alice", 30), new("Bob", 25) });
            if (scrollY is not null) p.Add(t => t.ScrollY, scrollY);
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)));
            p.AddChildContent<PropertyColumn<Person, int>>(cp => cp
                .Add(c => c.Title, "Age")
                .Add(c => c.Property, x => x.Age)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v)));
        });

    // ElementReference.FocusAsync goes through the JS runtime, so bUnit records it like any other
    // invocation -- the identifier is a framework internal, hence the substring match.
    int FocusCalls() =>
        JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Opening_another_columns_filter_does_not_pull_focus_back_to_the_first_funnel()
    {
        var cut = RenderTwoFilterableColumns();

        cut.FindAll(".wss-table-filter-trigger")[0].Click(); // Name's dropdown opens and focuses its panel
        var afterFirstOpen = FocusCalls();
        Assert.True(afterFirstOpen > 0, "the panel focus must be observable for the assertion below to mean anything");

        // Opening Age's filter closes Name's (Table.OpenColumnFilter). Name's close path must NOT
        // restore focus to its own funnel button: that restore used to be awaited behind the JS handle
        // release (two round trips under ScrollY), so it landed after Age's panel had already focused
        // itself -- and Age's panel then never saw Escape or any other key. Age's panel focus is the
        // only new focus call.
        cut.FindAll(".wss-table-filter-trigger")[1].Click();

        Assert.Equal(afterFirstOpen + 1, FocusCalls());
        Assert.Equal("true", cut.FindAll(".wss-table-filter-trigger")[1].GetAttribute("aria-expanded"));
        Assert.Equal("false", cut.FindAll(".wss-table-filter-trigger")[0].GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Closing_a_column_filter_on_its_own_still_returns_focus_to_its_funnel_button()
    {
        var cut = RenderTwoFilterableColumns();

        cut.FindAll(".wss-table-filter-trigger")[0].Click(); // opens, focuses the panel
        var afterOpen = FocusCalls();

        // Outside click with no other filter opening: the funnel button still gets focus back (the skip
        // above is only for the another-column-took-over case).
        cut.Find(".wss-table-filter-backdrop").Click();

        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal(afterOpen + 1, FocusCalls());
    }

    [Fact]
    public void Reopening_a_column_filter_under_ScrollY_reactivates_the_fixed_dropdown()
    {
        // The fixed-position escape hatch's handle is released on close (JsHandle.ReleaseAsync, which
        // also invalidates any activation still in flight), so the next open must activate a fresh one.
        // A release that left the old handle in place would silently skip re-activation and the
        // reopened dropdown would never track its trigger across page scroll again.
        var cut = RenderTwoFilterableColumns(scrollY: "160px");
        int ActivateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "activateFixedDropdown");

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(1, ActivateCalls()); // and only once, however many renders happen while it's open

        cut.Find(".wss-table-filter-backdrop").Click();      // close
        cut.FindAll(".wss-table-filter-trigger")[0].Click(); // reopen

        Assert.Equal(2, ActivateCalls());
    }

    // ----- ScrollY -----

    [Fact]
    public void ScrollY_unset_leaves_the_wrapper_without_a_style_attribute_or_scroll_class()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var wrapper = cut.Find(".wss-table-wrapper");
        Assert.False(wrapper.HasAttribute("style"));
        Assert.DoesNotContain("wss-table-wrapper-scroll-y", wrapper.ClassList);
    }

    [Fact]
    public void ScrollY_set_adds_the_scroll_class_and_a_max_height_style()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ScrollY, "240px")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        var wrapper = cut.Find(".wss-table-wrapper");
        Assert.Contains("wss-table-wrapper-scroll-y", wrapper.ClassList);
        Assert.Contains("max-height:240px", wrapper.GetAttribute("style"));
    }

    // ----- Accessibility: keyboard row activation, names, live region, Loading inertness -----

    IRenderedComponent<Table<Person>> RenderNameColumn(Action<ComponentParameterCollectionBuilder<Table<Person>>>? extra = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, Sample());
            extra?.Invoke(p);
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name));
        });

    [Fact]
    public void Enter_activates_a_focusable_row_the_same_way_a_click_does()
    {
        // OnRowClick used to be pointer-only: no tab stop, no key handler (WCAG 2.1.1).
        Person? clicked = null;
        var cut = RenderNameColumn(p => p
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x)));

        var row = cut.FindAll("tbody .wss-table-row")[1];
        Assert.Equal("0", row.GetAttribute("tabindex"));

        row.KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Bob", clicked!.Name);
    }

    [Fact]
    public void Enter_on_a_row_also_toggles_ExpandRowByClick_expansion()
    {
        Person? clicked = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .Add(t => t.ExpandRowByClick, true)
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        cut.FindAll("tbody .wss-table-row")[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Alice", clicked!.Name);
        Assert.Single(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void Space_does_not_activate_a_row()
    {
        // Deliberate: suppressing Space's page scroll needs @onkeydown:preventDefault, which Blazor
        // applies to EVERY keydown on the element -- it would swallow Tab and trap focus in the row.
        Person? clicked = null;
        var cut = RenderNameColumn(p => p
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, x => clicked = x)));

        cut.FindAll("tbody .wss-table-row")[0].KeyDown(new KeyboardEventArgs { Key = " " });

        Assert.Null(clicked);
    }

    [Fact]
    public void Rows_are_a_tab_stop_only_when_OnRowClick_is_wired()
    {
        var plain = RenderNameColumn();
        Assert.All(plain.FindAll("tbody .wss-table-row"), r => Assert.False(r.HasAttribute("tabindex")));

        // ExpandRowByClick on its own is already keyboard-operable from the chevron button, so it
        // must not add a second tab stop per row.
        var expandOnly = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, "detail"))
            .Add(t => t.ExpandRowByClick, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        Assert.All(expandOnly.FindAll("tbody .wss-table-row"), r => Assert.False(r.HasAttribute("tabindex")));
    }

    [Fact]
    public void A_clickable_row_drops_its_tab_stop_while_Loading()
    {
        var cut = RenderNameColumn(p => p
            .Add(t => t.OnRowClick, EventCallback.Factory.Create<Person>(this, _ => { }))
            .Add(t => t.Loading, true));

        Assert.All(cut.FindAll("tbody .wss-table-row"), r => Assert.False(r.HasAttribute("tabindex")));

        cut.Render(p => p.Add(t => t.Loading, false));
        Assert.All(cut.FindAll("tbody .wss-table-row"), r => Assert.Equal("0", r.GetAttribute("tabindex")));
    }

    [Fact]
    public void ScrollY_makes_the_wrapper_a_named_keyboard_reachable_scroll_region()
    {
        var cut = RenderNameColumn(p => p.Add(t => t.ScrollY, "160px"));

        var wrapper = cut.Find(".wss-table-wrapper");
        Assert.Equal("0", wrapper.GetAttribute("tabindex"));
        Assert.Equal("region", wrapper.GetAttribute("role"));
        Assert.Equal("Table content", wrapper.GetAttribute("aria-label"));
    }

    [Fact]
    public void The_scroll_region_name_prefers_the_caption_then_the_table_aria_label()
    {
        var captioned = RenderNameColumn(p => p
            .Add(t => t.ScrollY, "160px").Add(t => t.Caption, "People").Add(t => t.AriaLabel, "Ignored"));
        Assert.Equal("People", captioned.Find(".wss-table-wrapper").GetAttribute("aria-label"));

        var labelled = RenderNameColumn(p => p
            .Add(t => t.ScrollY, "160px").Add(t => t.AriaLabel, "Orders"));
        Assert.Equal("Orders", labelled.Find(".wss-table-wrapper").GetAttribute("aria-label"));

        var localized = RenderNameColumn(p => p
            .Add(t => t.ScrollY, "160px").Add(t => t.ScrollRegionLabel, "Tabelleninhalt"));
        Assert.Equal("Tabelleninhalt", localized.Find(".wss-table-wrapper").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_wrapper_without_ScrollY_gets_no_tab_stop_or_region_role()
    {
        // Plain overflow-x: auto only scrolls when the content happens to be wider than the wrapper,
        // so a tab stop here would come and go with the viewport -- documented known limitation.
        var wrapper = RenderNameColumn().Find(".wss-table-wrapper");

        Assert.False(wrapper.HasAttribute("tabindex"));
        Assert.False(wrapper.HasAttribute("role"));
        Assert.False(wrapper.HasAttribute("aria-label"));
    }

    [Fact]
    public void AriaLabel_names_the_table_only_when_there_is_no_Caption()
    {
        var labelled = RenderNameColumn(p => p.Add(t => t.AriaLabel, "Orders"));
        Assert.Equal("Orders", labelled.Find("table.wss-table").GetAttribute("aria-label"));
        Assert.Empty(labelled.FindAll("caption"));

        // A caption is already the accessible name; a second one would override the visible text.
        var captioned = RenderNameColumn(p => p.Add(t => t.Caption, "People").Add(t => t.AriaLabel, "Orders"));
        Assert.False(captioned.Find("table.wss-table").HasAttribute("aria-label"));
        Assert.Equal("People", captioned.Find("caption").TextContent);

        Assert.False(RenderNameColumn().Find("table.wss-table").HasAttribute("aria-label"));
    }

    [Fact]
    public void The_filter_trigger_declares_the_dialog_it_opens()
    {
        // Matches DatePicker/DateRangePicker's static aria-haspopup="dialog" and what wss-overlay.js
        // writes onto Popover/Popconfirm triggers.
        Assert.Equal("dialog", RenderNameFilterable().Find(".wss-table-filter-trigger").GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void The_filter_button_name_reports_the_applied_state_and_reverts_on_reset()
    {
        // The applied state was signalled only by recoloring the funnel glyph -- invisible to AT.
        var cut = RenderNameFilterable();
        Assert.Equal("Filter Name", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal("Filter Name (filter applied)", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();

        Assert.Equal("Filter Name", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_applied_state_filter_names_are_overridable_and_cover_a_headerless_column()
    {
        var localized = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.FilterAppliedButtonLabelFormat, "{0} filtern (Filter aktiv)")
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        localized.Find(".wss-table-filter-trigger").Click();
        CheckOption(localized, "Alice");
        localized.Find(".wss-table-filter-ok").Click();
        Assert.Equal("Name filtern (Filter aktiv)", localized.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));

        // Headerless columns fall back to FilterAppliedLabel, the way they fall back to FilterLabel.
        var headerless = RenderHeaderlessFilterable();
        headerless.Find(".wss-table-filter-trigger").Click();
        CheckOption(headerless, "Alice");
        headerless.Find(".wss-table-filter-ok").Click();
        Assert.Equal("Filter (filter applied)", headerless.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }

    static string StatusText(IRenderedComponent<Table<Person>> cut) =>
        cut.Find("div.wss-sr-only[role='status']").TextContent.Trim();

    [Fact]
    public void The_status_region_announces_Loading_and_the_empty_state()
    {
        // The region has to exist from the first render: an aria-live region injected together with
        // its text is not reliably announced.
        var cut = RenderNameColumn(p => p
            .Add(t => t.EmptyText, "Nothing here")
            .Add(t => t.LoadingLabel, "Wird geladen"));
        Assert.Equal(string.Empty, StatusText(cut));

        cut.Render(p => p.Add(t => t.Loading, true));
        Assert.Equal("Wird geladen", StatusText(cut));

        // Loading wins while both would apply -- the rows under the mask are stale by definition.
        cut.Render(p => p.Add(t => t.DataSource, new List<Person>()));
        Assert.Equal("Wird geladen", StatusText(cut));

        cut.Render(p => p.Add(t => t.Loading, false));
        Assert.Equal("Nothing here", StatusText(cut));
        Assert.Single(cut.FindAll(".wss-table-placeholder"));
    }

    [Fact]
    public void The_status_region_announces_a_filter_that_narrows_every_row_out()
    {
        var cut = RenderNameFilterable(data: new List<Person> { new("Alice", 30), new("Bob", 25) });
        Assert.Equal(string.Empty, StatusText(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Carol"); // no row matches
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal("No data", StatusText(cut));
    }

    [Fact]
    public void SelectRowLabelFor_names_each_rows_checkbox_and_radio_individually()
    {
        var multiple = RenderNameColumn(p => p
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectRowLabelFor, (Person x) => $"Select {x.Name}"));

        Assert.Equal(["Select Alice", "Select Bob"],
            multiple.FindAll("tbody input.wss-table-checkbox").Select(cb => cb.GetAttribute("aria-label")).ToArray());
        // The header select-all keeps its own (scope-accurate) name.
        Assert.Equal("Select all rows", multiple.Find("thead input.wss-table-checkbox").GetAttribute("aria-label"));

        var single = RenderNameColumn(p => p
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.SelectRowLabelFor, (Person x) => $"Select {x.Name}"));

        Assert.Equal(["Select Alice", "Select Bob"],
            single.FindAll("tbody input.wss-table-radio").Select(r => r.GetAttribute("aria-label")).ToArray());

        // Unset: every row keeps the static label (unchanged from before the labeler existed).
        var unset = RenderNameColumn(p => p.Add(t => t.Selectable, true));
        Assert.All(unset.FindAll("tbody input.wss-table-checkbox"),
            cb => Assert.Equal("Select row", cb.GetAttribute("aria-label")));
    }

    [Fact]
    public void Loading_disables_every_control_the_mask_covers()
    {
        // The mask blocks the pointer only -- keyboard users could still tab into and operate every
        // control underneath it. Native disabled is what makes the two match.
        var people = Enumerable.Range(1, 3).Select(i => new Person($"P{i}", i)).ToList();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, people)
            .Add(t => t.Selectable, true)
            .Add(t => t.PageSize, 2) // forces a pager
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, "detail"))
            .Add(t => t.Loading, true)
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.Sortable, true)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, (Func<Person, string, bool>)((x, v) => x.Name == v))));

        Assert.True(cut.Find(".wss-table-sort-trigger").HasAttribute("disabled"));
        Assert.True(cut.Find(".wss-table-filter-trigger").HasAttribute("disabled"));
        Assert.All(cut.FindAll(".wss-table-expand-btn"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.All(cut.FindAll("input.wss-table-checkbox"), c => Assert.True(c.HasAttribute("disabled")));
        Assert.All(cut.FindAll(".wss-pagination-item"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".wss-pagination-next").HasAttribute("disabled"));

        // ...and nothing stays disabled once loading ends.
        cut.Render(p => p.Add(t => t.Loading, false));
        Assert.False(cut.Find(".wss-table-sort-trigger").HasAttribute("disabled"));
        Assert.False(cut.Find(".wss-table-filter-trigger").HasAttribute("disabled"));
        Assert.All(cut.FindAll(".wss-table-expand-btn"), b => Assert.False(b.HasAttribute("disabled")));
        Assert.All(cut.FindAll("input.wss-table-checkbox"), c => Assert.False(c.HasAttribute("disabled")));
        Assert.False(cut.Find(".wss-pagination-next").HasAttribute("disabled"));
    }

    [Fact]
    public void Loading_disables_the_single_mode_radios_without_forgetting_IsRowSelectable()
    {
        var cut = RenderNameColumn(p => p
            .Add(t => t.Selectable, true)
            .Add(t => t.SelectionMode, SelectionMode.Single)
            .Add(t => t.IsRowSelectable, (Person x) => x.Name != "Bob")
            .Add(t => t.Loading, true));

        Assert.All(cut.FindAll("tbody input.wss-table-radio"), r => Assert.True(r.HasAttribute("disabled")));

        cut.Render(p => p.Add(t => t.Loading, false));
        var radios = cut.FindAll("tbody input.wss-table-radio");
        Assert.False(radios[0].HasAttribute("disabled")); // Alice is selectable again
        Assert.True(radios[1].HasAttribute("disabled"));  // Bob still isn't
    }

    [Fact]
    public void The_expand_button_points_aria_controls_at_the_detail_row_it_opens()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.RowKey, x => x.Name)
            .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, $"Detail for {x.Name}"))
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)));

        // Collapsed: nothing to point at, so no dangling reference.
        Assert.False(cut.FindAll(".wss-table-expand-btn")[0].HasAttribute("aria-controls"));

        cut.FindAll(".wss-table-expand-btn")[0].Click();

        var detailId = cut.Find(".wss-table-expanded-row").GetAttribute("id");
        Assert.False(string.IsNullOrEmpty(detailId));
        Assert.Equal(detailId, cut.FindAll(".wss-table-expand-btn")[0].GetAttribute("aria-controls"));
        Assert.False(cut.FindAll(".wss-table-expand-btn")[1].HasAttribute("aria-controls")); // still collapsed
    }
}
