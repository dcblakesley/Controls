using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the ported Table + PropertyColumn (selection uses raw checkboxes;
/// the Checkbox control is intentionally not part of this library).
/// </summary>
public class UiKitTableTests : TestContext
{
    // Table imports wss-table.js (to set the indeterminate select-all checkbox); tolerate the import.
    public UiKitTableTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    record Person(string Name, int Age);

    static List<Person> Sample() => [new("Alice", 30), new("Bob", 25)];

    [Fact]
    public void Table_renders_headers_and_rows_from_property_columns()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
    public void Table_empty_data_renders_placeholder()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
    public void Table_prunes_selection_when_the_data_source_is_swapped_uncontrolled()
    {
        List<Person>? selected = null;
        var first = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, second));

        // Selecting a row in the new data must not drag the now-absent Alice along.
        cut.FindAll("tbody input.wss-table-checkbox")[0].Change(true); // select Carol
        Assert.NotNull(selected);
        Assert.Single(selected!);
        Assert.Equal("Carol", selected![0].Name);
    }

    [Fact]
    public void Table_sortable_property_column_renders_a_trigger_and_aria_sort_none()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
    }

    [Fact]
    public void Table_clicking_a_sortable_header_cycles_ascending_descending_then_clears()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
    public void Table_custom_column_with_SortBy_is_sortable()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
    public void Table_select_all_checkbox_is_not_checked_when_only_some_rows_are_selected()
    {
        var data = Sample(); // Alice, Bob
        var cut = RenderComponent<Table<Person>>(p => p
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
}
