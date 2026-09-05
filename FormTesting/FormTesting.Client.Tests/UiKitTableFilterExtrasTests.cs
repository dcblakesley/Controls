using System.Collections;
using System.Globalization;
using System.Reflection;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// The dropdown extras and the table-level filtering switches layered on top of the kinds and
/// placements the other two filter suites cover: <c>Column.FilterSearch</c> (+ its placeholder and
/// no-match text), <c>Column.FilterCheckAll</c>, <c>Column.DefaultFilterValues</c> /
/// <c>FilterResetToDefault</c>, and <c>Table.ClientSideFiltering</c>. The kinds themselves live in
/// <see cref="UiKitTableTypedFilterTests"/>, the state object and the placement/event machinery in
/// <see cref="UiKitTableFilterTests"/>.
/// </summary>
public class UiKitTableFilterExtrasTests : BunitContext
{
    public UiKitTableFilterExtrasTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    enum Team { Red, Green, Blue }

    record Person(string Name, int Age, DateTime Joined, bool Active, Team Team);

    static List<Person> People() =>
    [
        new("Alice", 30, new DateTime(2026, 1, 5), true, Team.Red),
        new("Bob", 25, new DateTime(2026, 2, 10), false, Team.Green),
        new("Carol", 40, new DateTime(2026, 3, 20), true, Team.Red),
        new("Dave", 35, new DateTime(2026, 4, 1), false, Team.Blue),
    ];

    static List<TableFilterOption> NameOptions() =>
        [new("Alice", "Alice"), new("Bob", "Bob"), new("Carol", "Carol"), new("Dave", "Dave")];

    static Func<Person, string, bool> NameEquals => (x, v) => x.Name == v;

    // The round-trip form DateRangeFilterState serializes -- what DefaultFilterValues has to be given.
    static string Iso(int year, int month, int day) =>
        new DateTime(year, month, day).ToString("o", CultureInfo.InvariantCulture);

    // ----- Column / table builders -----

    static RenderFragment Col<TProp>(
        string title,
        Func<Person, TProp> property,
        bool filterable = false,
        bool valuesFromData = false,
        bool filterSearch = false,
        bool filterCheckAll = false,
        bool filterMultiple = true,
        IReadOnlyList<TableFilterOption>? options = null,
        Func<Person, string, bool>? onFilter = null,
        Func<Person, string?>? filterText = null,
        IEnumerable<string>? defaults = null,
        bool resetToDefault = false) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, TProp>>(0);
        builder.AddAttribute(1, "Title", title);
        builder.AddAttribute(2, "Property", property);
        builder.AddAttribute(3, "Filterable", filterable);
        builder.AddAttribute(4, "FilterValuesFromData", valuesFromData);
        builder.AddAttribute(5, "FilterSearch", filterSearch);
        builder.AddAttribute(6, "FilterCheckAll", filterCheckAll);
        builder.AddAttribute(7, "FilterMultiple", filterMultiple);
        builder.AddAttribute(8, "FilterOptions", options);
        builder.AddAttribute(9, "OnFilter", onFilter);
        builder.AddAttribute(10, "FilterText", filterText);
        builder.AddAttribute(11, "DefaultFilterValues", defaults);
        builder.AddAttribute(12, "FilterResetToDefault", resetToDefault);
        builder.CloseComponent();
    };

    // Each part in its own region (AddContent) so their sequence numbers cannot collide.
    static RenderFragment Columns(params RenderFragment[] parts) => builder =>
    {
        for (var i = 0; i < parts.Length; i++) builder.AddContent(i, parts[i]);
    };

    // The unfilterable Name column every multi-column test renders first, so RowNames always reads
    // the same cell.
    static RenderFragment NameCol() => Col<string>("Name", x => x.Name);

    // The Options-kind Name column (explicit FilterOptions + OnFilter), built the way markup does --
    // a fresh options list per pass.
    static RenderFragment NameOptionsCol(
        bool filterSearch = false,
        bool filterCheckAll = false,
        bool filterMultiple = true,
        IEnumerable<string>? defaults = null,
        bool resetToDefault = false) =>
        Col<string>("Name", x => x.Name,
            options: NameOptions(), onFilter: NameEquals,
            filterSearch: filterSearch, filterCheckAll: filterCheckAll, filterMultiple: filterMultiple,
            defaults: defaults, resetToDefault: resetToDefault);

    IRenderedComponent<Table<Person>> RenderTable(
        RenderFragment columns,
        Action<ComponentParameterCollectionBuilder<Table<Person>>>? configure = null,
        List<Person>? data = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, data ?? People());
            p.Add(t => t.ChildContent, columns);
            configure?.Invoke(p);
        });

    // The same table with both filter events logged in raise order ("Name:Bob", "all:1"), which is
    // what every "raises exactly this, in this order" assertion below reads.
    IRenderedComponent<Table<Person>> RenderLogged(
        List<string> log,
        RenderFragment columns,
        List<IReadOnlyList<TableColumnFilterSnapshot<Person>>>? snapshots = null,
        Action<ComponentParameterCollectionBuilder<Table<Person>>>? configure = null) =>
        RenderTable(columns, p =>
        {
            p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(
                this, v => log.Add($"{v.Item1.HeaderText}:{string.Join("+", v.Item2)}")));
            p.Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Person>>>(
                this, s => { log.Add($"all:{s.Count}"); snapshots?.Add(s); }));
            configure?.Invoke(p);
        });

    // ----- DOM readers -----

    static string[] RowNames(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)")
            .Select(tr => tr.QuerySelector("td")!.TextContent.Trim()).ToArray();

    static string[] OptionTexts(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()).ToArray();

    static IElement SearchBox(IRenderedComponent<Table<Person>> cut) =>
        cut.Find(".wss-table-filter-search input");

    static IElement CheckAllBox(IRenderedComponent<Table<Person>> cut) =>
        cut.Find("li.wss-table-filter-checkall input");

    static void CheckAll(IRenderedComponent<Table<Person>> cut, bool on) => CheckAllBox(cut).Change(on);

    static void TickOption(IRenderedComponent<Table<Person>> cut, string text, bool on = true) =>
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains(text))
            .QuerySelector("input")!.Change(on);

    static string StatusText(IRenderedComponent<Table<Person>> cut) =>
        cut.Find("div.wss-sr-only[role='status']").TextContent.Trim();

    static string[] ChildTags(IElement element) => element.Children.Select(c => c.TagName).ToArray();

    // Every ElementReference.FocusAsync lands on this identifier in bUnit's loose JS-interop mode.
    IReadOnlyList<object?> FocusedRefs() =>
        [.. JSInterop.Invocations
            .Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Arguments[0])];

    // The check-all's mixed-state mirror: one entry per setIndeterminate round trip, in order.
    bool[] IndeterminateCalls() =>
        [.. JSInterop.Invocations.Where(i => i.Identifier == "setIndeterminate").Select(i => (bool)i.Arguments[1]!)];

    // ----- Reflection into the internal filter state (internal, no InternalsVisibleTo) -----

    // Via the Table's own promoted column list rather than FindComponents, so the index is rendered
    // column order and the closed PropertyColumn<Person, TProp> type doesn't have to be named.
    static Column<Person> ColumnAt(IRenderedComponent<Table<Person>> cut, int index) =>
        (Column<Person>)((IList)typeof(Table<Person>)
            .GetField("_columns", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.Instance)!)[index]!;

    static IReadOnlyList<string> AppliedValues(IRenderedComponent<Table<Person>> cut, int column = 0) =>
        (IReadOnlyList<string>)typeof(Column<Person>)
            .GetProperty("AppliedFilterValues", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(ColumnAt(cut, column))!;

    // =====================================================================================
    // Column.FilterSearch
    // =====================================================================================

    [Fact]
    public void FilterSearch_renders_a_search_box_above_the_list_and_narrows_it_case_insensitively()
    {
        var cut = RenderTable(NameOptionsCol(filterSearch: true));
        cut.Find(".wss-table-filter-trigger").Click();

        var box = SearchBox(cut);
        Assert.Equal("search", box.GetAttribute("type"));
        Assert.Equal("wss-table-filter-input", box.ClassName);
        // Placeholder AND accessible name: a placeholder alone names nothing.
        Assert.Equal("Search in filters", box.GetAttribute("placeholder"));
        Assert.Equal("Search in filters", box.GetAttribute("aria-label"));
        // Box, then list, then the panel's own footer.
        Assert.Equal(["DIV", "UL", "DIV"], ChildTags(cut.Find(".wss-table-filter-dropdown")));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], OptionTexts(cut));

        SearchBox(cut).Input("aR"); // matched against the option TEXT, not its value
        Assert.Equal(["Carol"], OptionTexts(cut));

        SearchBox(cut).Input("A"); // ...ordinal-ignore-case, in both directions
        Assert.Equal(["Alice", "Carol", "Dave"], OptionTexts(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-empty"));
    }

    [Fact]
    public void FilterSearchPlaceholder_and_FilterEmptyText_are_overridable()
    {
        var cut = RenderTable(NameOptionsCol(filterSearch: true), p => p
            .Add(t => t.FilterSearchPlaceholder, "Filter suchen")
            .Add(t => t.FilterEmptyText, "Keine Treffer"));
        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("Filter suchen", SearchBox(cut).GetAttribute("aria-label"));

        SearchBox(cut).Input("zzz");

        Assert.Empty(cut.FindAll(".wss-table-filter-item"));
        var empty = cut.Find(".wss-table-filter-empty");
        Assert.Equal("LI", empty.TagName); // inside the list, so the panel keeps its shape
        Assert.Equal("Keine Treffer", empty.TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-ok")); // the footer is still there to press
    }

    [Fact]
    public void FilterSearch_commits_ticks_the_current_query_hides()
    {
        // The query is a VIEW over the options, not a filter on the staged set: hiding an option
        // leaves it staged, and OK commits the whole set either way.
        var cut = RenderTable(NameOptionsCol(filterSearch: true));
        cut.Find(".wss-table-filter-trigger").Click();
        TickOption(cut, "Bob");

        SearchBox(cut).Input("ali"); // Bob is off screen now
        Assert.Equal(["Alice"], OptionTexts(cut));
        TickOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice", "Bob"], RowNames(cut));
        Assert.Equal(["Alice", "Bob"], AppliedValues(cut));
    }

    [Fact]
    public void FilterSearch_query_starts_empty_on_every_open()
    {
        // Panel-local UI state, not filter state: the panel (and this editor with it) is created
        // fresh on each open, so there is nothing to carry over.
        var cut = RenderTable(NameOptionsCol(filterSearch: true));
        cut.Find(".wss-table-filter-trigger").Click();
        SearchBox(cut).Input("bo");
        Assert.Equal(["Bob"], OptionTexts(cut));

        cut.Find(".wss-table-filter-ok").Click();      // closes
        cut.Find(".wss-table-filter-trigger").Click(); // reopens

        Assert.Equal(string.Empty, SearchBox(cut).GetAttribute("value"));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], OptionTexts(cut));
    }

    [Fact]
    public void FilterSearch_takes_the_open_focus_instead_of_the_panel()
    {
        var cut = RenderTable(NameOptionsCol(filterSearch: true));

        cut.Find(".wss-table-filter-trigger").Click();

        var focused = Assert.Single(FocusedRefs()); // one focus call, not box AND panel
        var boxRefId = SearchBox(cut).GetAttribute("blazor:elementreference");
        Assert.False(string.IsNullOrEmpty(boxRefId));
        Assert.Equal(boxRefId, ((ElementReference)focused!).Id);
    }

    [Fact]
    public void FilterSearch_off_leaves_the_Options_panel_exactly_as_it_was()
    {
        var cut = RenderTable(NameOptionsCol());

        cut.Find(".wss-table-filter-trigger").Click();

        var panel = cut.Find(".wss-table-filter-dropdown");
        Assert.Equal(["UL", "DIV"], ChildTags(panel)); // list + footer, nothing else
        Assert.Empty(cut.FindAll(".wss-table-filter-search"));
        Assert.Empty(cut.FindAll(".wss-table-filter-empty"));
        Assert.Empty(cut.FindAll(".wss-table-filter-checkall"));
        // ...and the dialog still focuses itself, as it did before either extra existed.
        var focused = Assert.Single(FocusedRefs());
        Assert.Equal(panel.GetAttribute("blazor:elementreference"), ((ElementReference)focused!).Id);
    }

    // =====================================================================================
    // Column.FilterCheckAll
    // =====================================================================================

    [Fact]
    public void FilterCheckAll_ticks_and_unticks_every_option()
    {
        var cut = RenderTable(NameOptionsCol(filterCheckAll: true), p => p.Add(t => t.FilterCheckAllLabel, "Alle"));
        cut.Find(".wss-table-filter-trigger").Click();

        var row = cut.Find("li.wss-table-filter-checkall");
        Assert.Equal("Alle", row.TextContent.Trim());
        // The label reuses the option row's class, so the two cannot drift apart visually.
        Assert.Equal("wss-table-filter-option-label", row.QuerySelector("label")!.ClassName);
        // First in the list, ahead of every option.
        Assert.Equal("wss-table-filter-checkall", cut.Find(".wss-table-filter-list").Children[0].ClassName);

        CheckAll(cut, true);
        Assert.All(cut.FindAll(".wss-table-filter-item input"), i => Assert.True(i.HasAttribute("checked")));
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], AppliedValues(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.True(CheckAllBox(cut).HasAttribute("checked"));
        CheckAll(cut, false);
        Assert.All(cut.FindAll(".wss-table-filter-item input"), i => Assert.False(i.HasAttribute("checked")));
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Empty(AppliedValues(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-active"));
    }

    [Fact]
    public void FilterCheckAll_toggles_only_what_an_active_search_query_leaves_visible()
    {
        var cut = RenderTable(NameOptionsCol(filterSearch: true, filterCheckAll: true));
        cut.Find(".wss-table-filter-trigger").Click();
        SearchBox(cut).Input("o"); // Bob, Carol
        Assert.Equal(["Bob", "Carol"], OptionTexts(cut));

        CheckAll(cut, true);

        SearchBox(cut).Input(""); // everything back on screen
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], OptionTexts(cut));
        Assert.Equal([false, true, true, false],
            cut.FindAll(".wss-table-filter-item input").Select(i => i.HasAttribute("checked")).ToArray());
        Assert.False(CheckAllBox(cut).HasAttribute("checked")); // "some", not "all", over the full list

        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Bob", "Carol"], RowNames(cut));
    }

    [Fact]
    public void FilterCheckAll_is_unchecked_for_none_checked_for_all_and_mixed_for_some()
    {
        var cut = RenderTable(NameOptionsCol(filterCheckAll: true));
        cut.Find(".wss-table-filter-trigger").Click();

        Assert.False(CheckAllBox(cut).HasAttribute("checked"));
        Assert.Equal([false], IndeterminateCalls()); // the mirror is seeded on the first render

        TickOption(cut, "Bob");
        Assert.False(CheckAllBox(cut).HasAttribute("checked"));
        Assert.Equal([false, true], IndeterminateCalls());

        TickOption(cut, "Alice"); // still mixed -- no second round trip for an unchanged state
        Assert.Equal([false, true], IndeterminateCalls());

        TickOption(cut, "Carol");
        TickOption(cut, "Dave");
        Assert.True(CheckAllBox(cut).HasAttribute("checked"));
        Assert.Equal([false, true, false], IndeterminateCalls());
    }

    [Fact]
    public void FilterCheckAll_renders_nothing_on_a_single_select_column_or_with_no_visible_option()
    {
        var single = RenderTable(NameOptionsCol(filterCheckAll: true, filterMultiple: false));
        single.Find(".wss-table-filter-trigger").Click();
        Assert.Empty(single.FindAll(".wss-table-filter-checkall"));
        Assert.NotEmpty(single.FindAll(".wss-table-filter-radio")); // radios, and nothing to "select all" of
        Assert.Empty(IndeterminateCalls());

        // A query that hides everything leaves nothing to select: the empty row says so instead.
        var searched = RenderTable(NameOptionsCol(filterSearch: true, filterCheckAll: true));
        searched.Find(".wss-table-filter-trigger").Click();
        SearchBox(searched).Input("zzz");
        Assert.Empty(searched.FindAll(".wss-table-filter-checkall"));
        Assert.Single(searched.FindAll(".wss-table-filter-empty"));
    }

    [Fact]
    public void The_extras_work_for_enum_derived_and_data_derived_options_too()
    {
        // Both extras key off the STATE's options, not the FilterOptions parameter -- which is null on
        // each of these columns, since PropertyColumn derives their option lists itself.
        var cut = RenderTable(Columns(
            NameCol(),
            Col("Team", x => x.Team, filterable: true, filterSearch: true, filterCheckAll: true),
            Col("Age", x => x.Age, valuesFromData: true, filterSearch: true, filterCheckAll: true)));

        // Enum-derived: every declared member, in declaration order.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["Red", "Green", "Blue"], OptionTexts(cut));
        SearchBox(cut).Input("bl");
        Assert.Equal(["Blue"], OptionTexts(cut));
        CheckAll(cut, true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Dave"], RowNames(cut));

        // Data-derived: the distinct values of the CURRENT DataSource (unfiltered), ordered by value.
        cut.FindAll(".wss-table-filter-trigger")[1].Click();
        Assert.Equal(["25", "30", "35", "40"], OptionTexts(cut));
        SearchBox(cut).Input("3");
        Assert.Equal(["30", "35"], OptionTexts(cut));
        CheckAll(cut, true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Dave"], RowNames(cut)); // Team=Blue AND Age in {30,35}
        Assert.Equal(["30", "35"], AppliedValues(cut, 2));
    }

    // =====================================================================================
    // Column.DefaultFilterValues
    // =====================================================================================

    [Fact]
    public void DefaultFilterValues_narrows_the_rows_on_the_very_first_render_for_every_kind()
    {
        // Options: the selected keys.
        Assert.Equal(["Alice", "Carol"], RowNames(RenderTable(NameOptionsCol(defaults: ["Alice", "Carol"]))));

        // Text: the single (trimmed) text.
        Assert.Equal(["Bob", "Carol"], RowNames(RenderTable(Col<string>("Name", x => x.Name, filterable: true, defaults: ["o"]))));

        // NumberRange: [min, max], inclusive.
        Assert.Equal(["Alice", "Carol", "Dave"], RowNames(RenderTable(Columns(
            NameCol(), Col("Age", x => x.Age, filterable: true, defaults: ["30", "40"])))));

        // DateRange: round-trip "o" strings, inclusive at day granularity.
        Assert.Equal(["Bob", "Carol"], RowNames(RenderTable(Columns(
            NameCol(), Col("Joined", x => x.Joined, filterable: true, defaults: [Iso(2026, 2, 1), Iso(2026, 3, 31)])))));

        // Bool: "true"/"false".
        Assert.Equal(["Alice", "Carol"], RowNames(RenderTable(Columns(
            NameCol(), Col("Active", x => x.Active, filterable: true, defaults: ["true"])))));
    }

    [Fact]
    public void DefaultFilterValues_applies_silently()
    {
        var log = new List<string>();
        var cut = RenderLogged(log, NameOptionsCol(defaults: ["Bob"]));

        Assert.Equal(["Bob"], RowNames(cut));
        Assert.Empty(log); // it is the consumer's OWN initial state -- nothing to report back
        Assert.Equal(string.Empty, StatusText(cut)); // and nothing to announce

        // Everything else about an applied filter is true of it, though.
        var trigger = cut.Find(".wss-table-filter-trigger");
        Assert.Contains("wss-table-filter-active", trigger.ClassList);
        Assert.Equal("Filter Name (filter applied)", trigger.GetAttribute("aria-label"));
        Assert.Equal(["Bob"], AppliedValues(cut));

        // ...including that the dropdown opens staged FROM it.
        trigger.Click();
        Assert.Equal([false, true, false, false],
            cut.FindAll(".wss-table-filter-item input").Select(i => i.HasAttribute("checked")).ToArray());
    }

    [Fact]
    public void DefaultFilterValues_drops_what_the_kind_cannot_interpret()
    {
        // Options: a key the option list doesn't offer is outside the kind's key space.
        var options = RenderTable(NameOptionsCol(defaults: ["Bob", "Nobody"]));
        Assert.Equal(["Bob"], RowNames(options));
        Assert.Equal(["Bob"], AppliedValues(options));

        // NumberRange: an unparseable bound is simply not a bound; the other one still applies.
        var range = RenderTable(Columns(NameCol(), Col("Age", x => x.Age, filterable: true, defaults: ["1e", "35"])));
        Assert.Equal(["Alice", "Bob", "Dave"], RowNames(range));
        Assert.Equal(["", "35"], AppliedValues(range, 1));

        // DateRange: a value that is not a date at all fails the restore outright, so the column
        // starts unfiltered rather than half-applied.
        var dates = RenderTable(Columns(NameCol(), Col("Joined", x => x.Joined, filterable: true, defaults: ["not-a-date", ""])));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(dates));
        Assert.Empty(dates.FindAll(".wss-table-filter-active"));
    }

    [Fact]
    public void DefaultFilterValues_keeps_only_the_first_key_on_a_single_select_column()
    {
        var cut = RenderTable(NameOptionsCol(filterMultiple: false, defaults: ["Alice", "Bob"]));

        Assert.Equal(["Alice"], RowNames(cut));
        Assert.Equal(["Alice"], AppliedValues(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        var radios = cut.FindAll(".wss-table-filter-radio");
        Assert.Equal(4, radios.Count);
        Assert.Equal([true, false, false, false], radios.Select(r => r.HasAttribute("checked")).ToArray());
    }

    [Fact]
    public void DefaultFilterValues_works_for_a_data_derived_options_column()
    {
        // The option list is built while the state is being created, which is before the column has
        // registered with the table -- so the default's keys are already in the kind's key space by
        // the time TryRestore validates them. An ordering this column has to keep.
        var cut = RenderTable(Columns(NameCol(), Col("Age", x => x.Age, valuesFromData: true, defaults: ["25", "40"])));

        Assert.Equal(["Bob", "Carol"], RowNames(cut));
        Assert.Equal(["25", "40"], AppliedValues(cut, 1));
    }

    [Theory]
    [InlineData(true)]  // DataSource null on the first render...
    [InlineData(false)] // ...and the empty-list flavour of the same thing
    public void DefaultFilterValues_waits_for_a_data_derived_columns_options_to_exist(bool startNull)
    {
        // The derived option list IS the kind's key space, so a default validated against an empty one
        // would be dropped and its one shot spent. The column offers no filter until the data yields
        // an option; the default lands on that pass instead, still silently.
        var log = new List<string>();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, (IEnumerable<Person>?)(startNull ? null : new List<Person>()))
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(
                this, v => log.Add($"{v.Item1.HeaderText}:{string.Join("+", v.Item2)}")))
            .Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Person>>>(
                this, s => log.Add($"all:{s.Count}")))
            .Add(t => t.ChildContent, Columns(NameCol(), Col("Age", x => x.Age, valuesFromData: true, defaults: ["25", "40"]))));
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));

        cut.Render(p => p.Add(t => t.DataSource, People()));

        Assert.Equal(["Bob", "Carol"], RowNames(cut));
        Assert.Equal(["25", "40"], AppliedValues(cut, 1));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Empty(log);
    }

    [Fact]
    public void DefaultFilterValues_is_applied_once_and_never_re_asserted()
    {
        var cut = RenderTable(NameOptionsCol(defaults: ["Bob"]));
        Assert.Equal(["Bob"], RowNames(cut));

        // Clear it the ordinary way (no FilterResetToDefault here), then hand the column the same
        // default again on a later parameter pass.
        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));

        cut.Render(p => p.Add(t => t.ChildContent, NameOptionsCol(defaults: ["Bob"])));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-active"));

        // Nor does a KIND change bring it back: a default is an initial value, not one the column
        // keeps re-asserting. ("Bob" would match the Text kind too, so this really does discriminate.)
        cut.Render(p => p.Add(t => t.ChildContent,
            Col<string>("Name", x => x.Name, filterable: true, defaults: ["Bob"])));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-active"));
    }

    [Fact]
    public void A_deferred_DefaultFilterValues_pulls_the_reader_back_onto_a_page_that_exists()
    {
        // The default lands one pass after the column appears, against a page number chosen while the
        // unfiltered set was still wider -- unclamped, the body shows the empty placeholder over a row
        // that is right there.
        RenderFragment columns(bool withFilter) => Columns(
            NameCol(),
            withFilter ? Col<int>("Age", x => x.Age, options: [new("25", "25")], onFilter: (x, v) => x.Age.ToString() == v, defaults: ["25"]) : (b => { }));
        var cut = RenderTable(columns(false), p => p.Add(t => t.PageSize, 2));
        cut.FindAll(".wss-pagination-item")[^1].Click(); // page 2 of 2
        Assert.Equal(["Carol", "Dave"], RowNames(cut));

        cut.Render(p => p.Add(t => t.ChildContent, columns(true)));

        Assert.Equal(["Bob"], RowNames(cut));
        Assert.Empty(cut.FindAll("tbody .wss-table-placeholder"));
    }

    // =====================================================================================
    // Column.FilterResetToDefault
    // =====================================================================================

    [Fact]
    public void FilterResetToDefault_makes_Reset_restore_the_default_and_raise_it_as_the_payload()
    {
        var log = new List<string>();
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Person>>>();
        var cut = RenderLogged(log, NameOptionsCol(defaults: ["Bob"], resetToDefault: true), snapshots);
        Assert.Equal(["Bob"], RowNames(cut));
        Assert.Empty(log);

        // Move away from the default...
        cut.Find(".wss-table-filter-trigger").Click();
        TickOption(cut, "Bob", on: false);
        TickOption(cut, "Alice");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice"], RowNames(cut));
        Assert.Equal(["Name:Alice", "all:1"], log);
        log.Clear();

        // ...and Reset goes back TO it, not to empty.
        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();

        Assert.Equal(["Bob"], RowNames(cut));
        Assert.Equal(["Name:Bob", "all:1"], log);
        Assert.Equal(["Bob"], Assert.Single(snapshots[^1]).Values);
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown")); // Reset still closes the panel
    }

    [Fact]
    public void FilterResetToDefault_reset_while_already_at_the_default_changes_nothing_and_raises_nothing()
    {
        var log = new List<string>();
        var cut = RenderLogged(log, NameOptionsCol(defaults: ["Alice", "Bob"], resetToDefault: true),
            configure: p => p.Add(t => t.PageSize, 1));
        Assert.Equal(["Alice"], RowNames(cut));

        cut.FindAll(".wss-pagination-item")[^1].Click(); // page 2 of the 2 the default leaves
        Assert.Equal(["Bob"], RowNames(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();

        Assert.Equal(["Bob"], RowNames(cut)); // still page 2 -- a no-op reset never resets the page
        Assert.Empty(log);
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
    }

    [Fact]
    public async Task ClearFiltersAsync_restores_the_defaulted_columns_and_clears_the_rest()
    {
        var log = new List<string>();
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Person>>>();
        var cut = RenderLogged(log, Columns(
            NameOptionsCol(defaults: ["Bob"], resetToDefault: true),
            Col("Age", x => x.Age, filterable: true)), snapshots);

        // Move Name off its default and apply a range on Age.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        TickOption(cut, "Bob", on: false);
        TickOption(cut, "Carol");
        cut.Find(".wss-table-filter-ok").Click();
        cut.FindAll(".wss-table-filter-trigger")[1].Click();
        cut.Find(".wss-table-filter-range input").Input("20");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Carol"], RowNames(cut));
        log.Clear();

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());

        Assert.Equal(["Bob"], RowNames(cut));             // Name back at its default, Age cleared
        Assert.Equal(["Name:Bob", "Age:", "all:1"], log); // per column in column order, then ONE aggregate
        var still = Assert.Single(snapshots[^1]);         // only the defaulted column is still active
        Assert.Equal("Name", still.Column.HeaderText);
        Assert.Equal(["Bob"], still.Values);

        // A second call has nothing left to change -- the default is not re-applied over itself.
        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public async Task ClearFiltersAsync_restores_a_default_the_user_had_emptied()
    {
        // The column is INACTIVE when the clear runs, which is exactly the case a plain "skip anything
        // with nothing applied" guard would step over.
        var log = new List<string>();
        var cut = RenderLogged(log, NameOptionsCol(defaults: ["Bob"], resetToDefault: true));
        cut.Find(".wss-table-filter-trigger").Click();
        TickOption(cut, "Bob", on: false);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));
        log.Clear();

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());

        Assert.Equal(["Bob"], RowNames(cut));
        Assert.Equal(["Name:Bob", "all:1"], log);
    }

    // =====================================================================================
    // Table.ClientSideFiltering
    // =====================================================================================

    [Fact]
    public void ClientSideFiltering_false_keeps_every_row_while_the_state_and_events_still_work()
    {
        var log = new List<string>();
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Person>>>();
        var cut = RenderLogged(log, NameOptionsCol(), snapshots, p => p.Add(t => t.ClientSideFiltering, false));

        cut.Find(".wss-table-filter-trigger").Click();
        TickOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut)); // nothing narrowed
        var trigger = cut.Find(".wss-table-filter-trigger");
        Assert.Contains("wss-table-filter-active", trigger.ClassList);
        Assert.Equal("Filter Name (filter applied)", trigger.GetAttribute("aria-label"));
        Assert.Equal(["Name:Bob", "all:1"], log);
        var snap = Assert.Single(snapshots[^1]);
        Assert.Equal(["Bob"], snap.Values);
        Assert.Equal("1 selected", snap.Description); // Describe still summarizes the applied state
        Assert.Equal(["Bob"], AppliedValues(cut));
    }

    [Fact]
    public void ClientSideFiltering_false_suppresses_the_matching_row_count_announcement()
    {
        var server = RenderTable(NameOptionsCol(), p => p.Add(t => t.ClientSideFiltering, false));
        server.Find(".wss-table-filter-trigger").Click();
        TickOption(server, "Bob");
        server.Find(".wss-table-filter-ok").Click();
        // The count would be describing rows the server hasn't answered with yet.
        Assert.Equal(string.Empty, StatusText(server));

        // The identical table client-side does announce it -- the count is the only difference.
        var client = RenderTable(NameOptionsCol());
        client.Find(".wss-table-filter-trigger").Click();
        TickOption(client, "Bob");
        client.Find(".wss-table-filter-ok").Click();
        Assert.Equal("1 matching rows", StatusText(client));
        Assert.Equal(["Bob"], RowNames(client));
    }

    [Fact]
    public void ClientSideFiltering_false_still_honours_a_DefaultFilterValues_without_narrowing()
    {
        var log = new List<string>();
        var cut = RenderLogged(log, NameOptionsCol(defaults: ["Bob"]), configure: p => p.Add(t => t.ClientSideFiltering, false));

        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));
        Assert.Equal(["Bob"], AppliedValues(cut));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Empty(log);
    }

    [Fact]
    public void Flipping_ClientSideFiltering_at_runtime_re_derives_the_rows()
    {
        var cut = RenderTable(NameOptionsCol());
        cut.Find(".wss-table-filter-trigger").Click();
        TickOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Bob"], RowNames(cut));

        cut.Render(p => p.Add(t => t.ClientSideFiltering, false));
        Assert.Equal(["Alice", "Bob", "Carol", "Dave"], RowNames(cut));
        Assert.Equal(["Bob"], AppliedValues(cut)); // the applied state is untouched either way

        cut.Render(p => p.Add(t => t.ClientSideFiltering, true));
        Assert.Equal(["Bob"], RowNames(cut));
    }

    [Fact]
    public void Row_placement_renders_no_filter_row_while_no_column_is_filterable()
    {
        RenderFragment columns(bool filterable) => Columns(NameCol(), Col("Age", x => x.Age, filterable: filterable));
        var cut = RenderTable(columns(false), p => p.Add(t => t.FilterPlacement, TableFilterPlacement.Row));

        Assert.Empty(cut.FindAll("tr.wss-table-filter-row"));
        Assert.DoesNotContain("wss-table-has-filter-row", cut.Find("table.wss-table").ClassList);

        cut.Render(p => p.Add(t => t.ChildContent, columns(true)));

        Assert.Single(cut.FindAll("tr.wss-table-filter-row"));
        Assert.Contains("wss-table-has-filter-row", cut.Find("table.wss-table").ClassList);
    }

    // =====================================================================================
    // Cleanup: the dead forwarded labels are gone
    // =====================================================================================

    [Fact]
    public void TableColumnFilter_declares_only_the_labels_it_actually_renders()
    {
        // FilterButtonLabelFormat / FilterAppliedButtonLabelFormat / FilterAppliedLabel / FilterLabel
        // were forwarded parameters this component stopped reading once Table.FilterAccessibleName
        // became the single implementation of the name. Only the two footer buttons rendered HERE
        // still need a forwarded label.
        var parameters = typeof(TableColumnFilter<Person>)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

        Assert.Equal(["Column", "Disabled", "OkLabel", "ResetLabel", "Table"], parameters);
    }

    [Fact]
    public void The_funnel_is_still_named_from_the_Tables_own_label_parameters()
    {
        // The names those removed parameters used to carry now come straight off the Table -- pinned
        // here as well as in UiKitTableTests, since this is the seam the removal touched.
        var cut = RenderTable(NameOptionsCol(), p => p
            .Add(t => t.FilterButtonLabelFormat, "{0} filtern")
            .Add(t => t.FilterAppliedButtonLabelFormat, "{0} filtern (aktiv)")
            .Add(t => t.FilterResetLabel, "Zuruecksetzen")
            .Add(t => t.FilterOkLabel, "Ja"));

        Assert.Equal("Name filtern", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("Name filtern", cut.Find(".wss-table-filter-dropdown").GetAttribute("aria-label"));
        Assert.Equal("Zuruecksetzen", cut.Find(".wss-table-filter-reset").TextContent.Trim());
        Assert.Equal("Ja", cut.Find(".wss-table-filter-ok").TextContent.Trim());

        TickOption(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal("Name filtern (aktiv)", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-label"));
    }
}
