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
    public void Table_renders_fully_equal_duplicate_rows_without_throwing()
    {
        // Two Equals-equal records used to produce duplicate sibling @keys — Blazor rejects those
        // with an InvalidOperationException that killed the whole table render.
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        // Titled sortable column: the visible header names the button, so no redundant aria-label.
        Assert.False(headers[1].QuerySelector("button.wss-table-sort-trigger")!.HasAttribute("aria-label"));
    }

    [Fact]
    public void Table_title_less_sortable_header_button_has_an_accessible_name()
    {
        // A sortable column with no Title would otherwise render a sort <button> with no accessible
        // name (empty label span + aria-hidden carets) — it falls back to aria-label="Sort".
        var cut = RenderComponent<Table<Person>>(p => p
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
    public void Table_sortable_on_a_non_comparable_property_renders_no_sort_control()
    {
        // Person doesn't implement IComparable, so Comparer<Person>.Default would throw on a header
        // click; CanSort degrades the column to non-sortable instead of crashing the circuit.
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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

        var cut = RenderComponent<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample())
            .Add(t => t.ChildContent, Columns()));

        string[] Headers() => cut.FindAll("thead th").Select(th => th.TextContent.Trim()).ToArray();
        Assert.Equal(["Name", "Age", "City"], Headers());

        // Hide the middle column: it drops out, no zombie left behind.
        showMiddle = false;
        cut.SetParametersAndRender(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "City"], Headers());

        // Re-show it: no duplicate, and it returns to its declared (middle) position.
        showMiddle = true;
        cut.SetParametersAndRender(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Equal(["Name", "Age", "City"], Headers());
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

        var cut = RenderComponent<Table<Person>>(p => p
            .Add(t => t.DataSource, Sample()) // Alice(30), Bob(25)
            .Add(t => t.ChildContent, Columns()));

        string[] Names() => cut.FindAll("tbody .wss-table-row td.wss-table-cell:first-child")
            .Select(td => td.TextContent.Trim()).ToArray();

        // Sort ascending by Age -> Bob(25), Alice(30).
        cut.Find("button.wss-table-sort-trigger").Click();
        Assert.Equal(["Bob", "Alice"], Names());

        // Hide the sorted column: the sort clears and rows return to DataSource order.
        showAge = false;
        cut.SetParametersAndRender(p => p.Add(t => t.ChildContent, Columns()));
        Assert.Empty(cut.FindAll("button.wss-table-sort-trigger"));
        Assert.Equal(["Alice", "Bob"], Names());
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

    [Fact]
    public void Toggling_Selectable_off_and_on_reapplies_the_indeterminate_state()
    {
        var data = Sample(); // Alice, Bob
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.Selectable, false));
        cut.SetParametersAndRender(p => p.Add(t => t.Selectable, true));

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
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, people));
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, people));
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, people));

        var extraRenders = cut.RenderCount - rendersAfterFirstRender;
        Assert.True(extraRenders >= 3);
        Assert.Equal(extraRenders * people.Count * 2, keyCalls - callsAfterFirstRender);
    }

    [Fact]
    public void DataSource_swap_rebuilds_keys_and_selection_flags()
    {
        var first = new List<Person> { new("Alice", 30), new("Bob", 25) };
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, second));

        var names = cut.FindAll("tbody .wss-table-row td.wss-table-cell:last-child")
            .Select(td => td.TextContent.Trim()).ToArray();
        Assert.Equal(["Carol", "Dave", "Eve"], names);
        Assert.False(cut.Find("thead input.wss-table-checkbox").HasAttribute("checked"));
    }

    [Fact]
    public void Select_all_and_toggle_row_keep_header_checkbox_state_correct()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, new List<Person> { new("Bob", 25) }));
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
        cut.SetParametersAndRender(p => p.Add(t => t.DataSource, Sample()));
        Assert.Empty(cut.FindAll(".wss-table-expanded-row"));
    }

    [Fact]
    public void TitleContent_replaces_the_plain_header_text()
    {
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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
        var cut = RenderComponent<Table<Person>>(p => p
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

    [Fact]
    public void Toggling_UseStyledCheckbox_reapplies_the_indeterminate_state()
    {
        var data = Sample(); // Alice, Bob
        var cut = RenderComponent<Table<Person>>(p => p
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
        cut.SetParametersAndRender(p => p.Add(t => t.UseStyledCheckbox, true));
        Assert.Equal(2, IndeterminateCalls());

        cut.SetParametersAndRender(p => p.Add(t => t.UseStyledCheckbox, false));
        Assert.Equal(3, IndeterminateCalls());
    }
}
