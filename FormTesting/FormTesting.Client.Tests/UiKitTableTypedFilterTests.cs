using System.Globalization;
using System.Reflection;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// The typed column filters a <c>PropertyColumn</c> derives from its own property rather than being
/// handed an options list: <c>Filterable</c> (string / number / date / bool / enum) and
/// <c>FilterValuesFromData</c> (options built from the rows on screen). Covers the derivation itself,
/// each new editor in both placements, the range/bool matching rules, and the data-derived options'
/// re-derivation and prune across a DataSource swap. The keyed kinds and the placement/debounce
/// machinery they share are covered by <see cref="UiKitTableFilterTests"/>.
/// </summary>
public class UiKitTableTypedFilterTests : BunitContext
{
    public UiKitTableTypedFilterTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    enum Status
    {
        [EnumDisplayName("In stock")] InStock,
        OutOfStock,
        Discontinued
    }

    // Two names for value 0: Enum.GetValues yields one entry per declared field, so the option list
    // has to de-duplicate by value.
    enum Priority
    {
        Normal = 0,
        Default = Normal,
        Urgent = 1
    }

    // A property type with no derived editor: not comparable, not formattable, nothing to build a
    // range or an option list from.
    sealed class Tag
    {
        public string Name { get; init; } = "";
        public override string ToString() => Name;
    }

    record Product(
        string Name,
        string? Category,
        int Qty,
        decimal? Price,
        DateTime Added,
        DateOnly Due,
        bool? Active,
        Status Status,
        Tag Label);

    static List<Product> Products() =>
    [
        new("Widget", "b", 10, 9.99m, new DateTime(2026, 1, 5), new DateOnly(2026, 3, 1), true, Status.InStock, new Tag { Name = "a" }),
        new("Gadget", null, 25, null, new DateTime(2026, 2, 10, 14, 30, 0), new DateOnly(2026, 3, 15), false, Status.OutOfStock, new Tag { Name = "b" }),
        new("Doodad", "a", 40, 19.5m, new DateTime(2026, 3, 20), new DateOnly(2026, 4, 1), null, Status.InStock, new Tag { Name = "a" }),
        new("Sprocket", "a", 25, 5m, new DateTime(2026, 1, 5), new DateOnly(2026, 3, 1), true, Status.Discontinued, new Tag { Name = "b" }),
    ];

    // ----- Column / table builders -----

    static RenderFragment Col<TProp>(
        string title,
        Func<Product, TProp> property,
        bool filterable = false,
        bool valuesFromData = false,
        string? format = null,
        bool filterMultiple = true,
        IReadOnlyList<TableFilterOption>? options = null,
        Func<Product, string, bool>? onFilter = null,
        Func<Product, string?>? filterText = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Product, TProp>>(0);
        builder.AddAttribute(1, "Title", title);
        builder.AddAttribute(2, "Property", property);
        builder.AddAttribute(3, "Filterable", filterable);
        builder.AddAttribute(4, "FilterValuesFromData", valuesFromData);
        builder.AddAttribute(5, "Format", format);
        builder.AddAttribute(6, "FilterMultiple", filterMultiple);
        builder.AddAttribute(7, "FilterOptions", options);
        builder.AddAttribute(8, "OnFilter", onFilter);
        builder.AddAttribute(9, "FilterText", filterText);
        builder.CloseComponent();
    };

    // Each part in its own region (AddContent) so their sequence numbers cannot collide.
    static RenderFragment Columns(params RenderFragment[] parts) => builder =>
    {
        for (var i = 0; i < parts.Length; i++) builder.AddContent(i, parts[i]);
    };

    // The Name column every test renders first, so RowNames below always reads the same cell.
    static RenderFragment NameCol() => Col<string>("Name", x => x.Name);

    IRenderedComponent<Table<Product>> RenderTable(
        RenderFragment columns,
        Action<ComponentParameterCollectionBuilder<Table<Product>>>? configure = null,
        List<Product>? data = null) =>
        Render<Table<Product>>(p =>
        {
            p.Add(t => t.DataSource, data ?? Products());
            p.Add(t => t.ChildContent, columns);
            configure?.Invoke(p);
        });

    IRenderedComponent<Table<Product>> RenderRow(
        RenderFragment columns,
        Action<ComponentParameterCollectionBuilder<Table<Product>>>? configure = null,
        List<Product>? data = null) =>
        RenderTable(columns, p =>
        {
            p.Add(t => t.FilterPlacement, TableFilterPlacement.Row);
            configure?.Invoke(p);
        }, data);

    static string[] RowNames(IRenderedComponent<Table<Product>> cut) =>
        cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)")
            .Select(tr => tr.QuerySelector("td")!.TextContent.Trim()).ToArray();

    static void AssertNoDataRows(IRenderedComponent<Table<Product>> cut)
    {
        Assert.Single(cut.FindAll("tbody .wss-table-placeholder"));
        Assert.Empty(cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)"));
    }

    // ----- Reflection into the internal filter state (internal, no InternalsVisibleTo) -----

    static object? FilterOf<TProp>(IRenderedComponent<Table<Product>> cut, int index = 0) =>
        typeof(Column<Product>)
            .GetProperty("Filter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.FindComponents<PropertyColumn<Product, TProp>>()[index].Instance);

    static T Get<T>(object state, string property) =>
        (T)state.GetType().GetProperty(property)!.GetValue(state)!;

    static T Call<T>(object state, string method, params object?[] args) =>
        (T)state.GetType().GetMethod(method)!.Invoke(state, args)!;

    // =====================================================================================
    // Kind derivation
    // =====================================================================================

    [Fact]
    public void Filterable_derives_one_editor_per_property_type_and_nothing_for_an_unsupported_one()
    {
        // Row placement renders every column's editor at once, so one render pins the whole table of
        // TProp -> editor. Order: string, int, decimal?, DateTime, DateOnly, bool?, enum, class.
        var cut = RenderRow(Columns(
            Col<string>("Name", x => x.Name, filterable: true),
            Col("Qty", x => x.Qty, filterable: true),
            Col("Price", x => x.Price, filterable: true),
            Col("Added", x => x.Added, filterable: true),
            Col("Due", x => x.Due, filterable: true),
            Col("Active", x => x.Active, filterable: true),
            Col("Status", x => x.Status, filterable: true),
            Col("Label", x => x.Label, filterable: true)));

        var cells = cut.FindAll("thead tr.wss-table-filter-row td");
        Assert.Equal(8, cells.Count);

        Assert.NotNull(cells[0].QuerySelector("input.wss-table-filter-input[type=search]"));
        Assert.Equal(2, cells[1].QuerySelectorAll(".wss-table-filter-range input[type=number]").Length);
        Assert.Equal(2, cells[2].QuerySelectorAll(".wss-table-filter-range input[type=number]").Length);
        Assert.NotNull(cells[3].QuerySelector(".wss-picker-input-start"));
        Assert.NotNull(cells[4].QuerySelector(".wss-picker-input-end"));
        Assert.NotNull(cells[5].QuerySelector(".wss-select"));
        Assert.NotNull(cells[6].QuerySelector(".wss-select"));
        Assert.Empty(cells[7].Children); // a plain class: no filter UI at all, silently

        Assert.Equal(TableFilterKind.Text, Get<TableFilterKind>(FilterOf<string>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.NumberRange, Get<TableFilterKind>(FilterOf<int>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.NumberRange, Get<TableFilterKind>(FilterOf<decimal?>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.DateRange, Get<TableFilterKind>(FilterOf<DateTime>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.DateRange, Get<TableFilterKind>(FilterOf<DateOnly>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.Bool, Get<TableFilterKind>(FilterOf<bool?>(cut)!, "Kind"));
        Assert.Equal(TableFilterKind.Options, Get<TableFilterKind>(FilterOf<Status>(cut)!, "Kind"));
        Assert.Null(FilterOf<Tag>(cut));
        Assert.False(cut.FindComponent<PropertyColumn<Product, Tag>>().Instance.CanFilter);
    }

    [Fact]
    public void Filterable_without_a_Property_offers_no_filter()
    {
        var cut = RenderRow(Col<int>("Qty", null!, filterable: true));

        Assert.Null(FilterOf<int>(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-editor"));
    }

    [Fact]
    public void An_explicitly_declared_filter_wins_over_Filterable()
    {
        // FilterOptions+OnFilter and FilterText each beat the derived NumberRange an int would get,
        // the same way SortBy beats the derived comparison.
        var options = RenderRow(Col("Qty", x => x.Qty, filterable: true,
            options: [new("Small", "small")], onFilter: (x, v) => v == "small" && x.Qty < 30));
        var text = RenderRow(Col("Qty", x => x.Qty, filterable: true,
            filterText: x => x.Qty.ToString()));

        Assert.Equal(TableFilterKind.Options, Get<TableFilterKind>(FilterOf<int>(options)!, "Kind"));
        Assert.Empty(options.FindAll(".wss-table-filter-range"));
        Assert.Equal(TableFilterKind.Text, Get<TableFilterKind>(FilterOf<int>(text)!, "Kind"));
        Assert.Empty(text.FindAll(".wss-table-filter-range"));
        Assert.NotNull(text.Find("input.wss-table-filter-input[type=search]"));
    }

    [Fact]
    public void FilterValuesFromData_wins_over_Filterable()
    {
        // Qty would derive a NumberRange; the data-derived flag turns it into an option list instead.
        var cut = RenderRow(Col("Qty", x => x.Qty, filterable: true, valuesFromData: true));

        var state = FilterOf<int>(cut)!;
        Assert.Equal(TableFilterKind.Options, Get<TableFilterKind>(state, "Kind"));
        Assert.Empty(cut.FindAll(".wss-table-filter-range"));
    }

    // =====================================================================================
    // NumberRange
    // =====================================================================================

    static IReadOnlyList<IElement> NumberInputs(IRenderedComponent<Table<Product>> cut, string scope) =>
        cut.FindAll($"{scope} .wss-table-filter-range input[type=number]");

    // Open the funnel and type both bounds into the dropdown's boxes (staged only -- OK applies).
    static void StageRange(IRenderedComponent<Table<Product>> cut, string? min, string? max, int column = 0)
    {
        cut.FindAll(".wss-table-filter-trigger")[column].Click();
        var inputs = NumberInputs(cut, ".wss-table-filter-dropdown");
        if (min is not null) inputs[0].Input(min);
        if (max is not null) NumberInputs(cut, ".wss-table-filter-dropdown")[1].Input(max);
    }

    static void ApplyRange(IRenderedComponent<Table<Product>> cut, string? min, string? max, int column = 0)
    {
        StageRange(cut, min, max, column);
        cut.Find(".wss-table-filter-ok").Click();
    }

    [Fact]
    public void NumberRange_narrows_inclusively_with_a_min_only_a_max_only_and_both()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)));

        ApplyRange(cut, "25", null);                                        // inclusive lower bound
        Assert.Equal(["Gadget", "Doodad", "Sprocket"], RowNames(cut));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.Find(".wss-table-filter-reset").Click();
        ApplyRange(cut, null, "25");                                        // inclusive upper bound
        Assert.Equal(["Widget", "Gadget", "Sprocket"], RowNames(cut));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        NumberInputs(cut, ".wss-table-filter-dropdown")[0].Input("20");      // 20..25
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Gadget", "Sprocket"], RowNames(cut));
        Assert.Equal(["20", "25"], Get<IReadOnlyList<string>>(FilterOf<int>(cut)!, "AppliedValues"));
    }

    [Fact]
    public void NumberRange_AppliedValues_pads_the_unset_bound_and_is_empty_with_neither()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)));
        var state = FilterOf<int>(cut)!;
        Assert.Empty(Get<IReadOnlyList<string>>(state, "AppliedValues"));
        Assert.False(Get<bool>(state, "IsActive"));

        ApplyRange(cut, "25", null);
        Assert.Equal(["25", ""], Get<IReadOnlyList<string>>(state, "AppliedValues"));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        NumberInputs(cut, ".wss-table-filter-dropdown")[0].Input("");
        NumberInputs(cut, ".wss-table-filter-dropdown")[1].Input("30");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["", "30"], Get<IReadOnlyList<string>>(state, "AppliedValues"));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.Find(".wss-table-filter-reset").Click();
        Assert.Empty(Get<IReadOnlyList<string>>(state, "AppliedValues"));
        Assert.Equal(4, RowNames(cut).Length);
    }

    [Fact]
    public void NumberRange_ignores_a_bound_that_is_not_a_number_and_excludes_null_values()
    {
        // Price is decimal? and Gadget's is null: it survives with no bound set, and drops out the
        // moment either bound is -- there is nothing to compare it against.
        var cut = RenderTable(Columns(NameCol(), Col("Price", x => x.Price, filterable: true)));

        ApplyRange(cut, "not a number", null);
        Assert.Equal(["Widget", "Gadget", "Doodad", "Sprocket"], RowNames(cut));
        Assert.Empty(Get<IReadOnlyList<string>>(FilterOf<decimal?>(cut)!, "AppliedValues"));
        Assert.DoesNotContain("wss-table-filter-active", cut.FindAll(".wss-table-filter-trigger")[0].ClassList);

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        NumberInputs(cut, ".wss-table-filter-dropdown")[0].Input("");
        NumberInputs(cut, ".wss-table-filter-dropdown")[1].Input("10");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Widget", "Sprocket"], RowNames(cut)); // 9.99 and 5; Gadget's null is out
    }

    [Fact]
    public void NumberRange_Enter_in_the_dropdown_applies_and_closes()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        StageRange(cut, "30", null);
        Assert.Equal(4, RowNames(cut).Length); // staged only
        NumberInputs(cut, ".wss-table-filter-dropdown")[0].KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Doodad"], RowNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal([["30", ""]], raised);
    }

    [Fact]
    public void NumberRange_dropdown_panel_gets_the_wide_modifier_and_the_padded_slot()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        var panel = cut.Find(".wss-table-filter-dropdown");
        Assert.Equal("wss-table-filter-dropdown wss-table-filter-dropdown-wide", panel.ClassName);
        Assert.NotNull(panel.QuerySelector(".wss-table-filter-pane > .wss-table-filter-range"));
        Assert.NotNull(panel.QuerySelector(".wss-table-filter-footer")); // the built-in footer still commits
    }

    [Fact]
    public void NumberRange_inputs_are_named_by_the_column_and_the_bound_together()
    {
        var cut = RenderRow(
            Columns(Col("Qty", x => x.Qty, filterable: true), Col("Price", x => x.Price, filterable: true)),
            p => p.Add(t => t.FilterMinLabel, "Von").Add(t => t.FilterMaxLabel, "Bis"));

        var inputs = NumberInputs(cut, ".wss-table-filter-row");
        Assert.Equal("Filter by Qty: Von", inputs[0].GetAttribute("aria-label"));
        Assert.Equal("Filter by Qty: Bis", inputs[1].GetAttribute("aria-label"));
        Assert.Equal("Filter by Price: Von", inputs[2].GetAttribute("aria-label"));
        Assert.Equal("Filter by Price: Bis", inputs[3].GetAttribute("aria-label"));
    }

    // Find + dispatch on the renderer's dispatcher (the row editor's @oninput lambda gets a fresh
    // handler id each render), the same shape UiKitTableFilterTests uses for the text box.
    static Task TypeBoundAsync(IRenderedComponent<Table<Product>> cut, int index, string text) =>
        cut.InvokeAsync(() => NumberInputs(cut, ".wss-table-filter-row")[index]
            .InputAsync(new ChangeEventArgs { Value = text }));

    [Fact]
    public async Task NumberRange_row_editor_with_a_zero_debounce_narrows_on_every_input()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRow(
            Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)),
            p => p.Add(t => t.FilterDebounceMilliseconds, 0)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        await TypeBoundAsync(cut, 0, "25");
        Assert.Equal(["Gadget", "Doodad", "Sprocket"], RowNames(cut));

        await TypeBoundAsync(cut, 1, "25");
        Assert.Equal(["Gadget", "Sprocket"], RowNames(cut));
        Assert.Equal([["25", ""], ["25", "25"]], raised);
    }

    [Fact]
    public async Task NumberRange_row_debounce_coalesces_both_bounds_into_one_commit()
    {
        // One CTS per editor, not per box: typing a max supersedes a min still counting down, and the
        // pair lands together.
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRow(
            Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)),
            p => p.Add(t => t.FilterDebounceMilliseconds, 500)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var min = TypeBoundAsync(cut, 0, "20");
        var max = TypeBoundAsync(cut, 1, "30");
        Assert.Equal(4, RowNames(cut).Length); // nothing committed yet
        Assert.Empty(raised);
        await Task.WhenAll(min, max);

        cut.WaitForAssertion(() => Assert.Equal(["Gadget", "Sprocket"], RowNames(cut)), TimeSpan.FromSeconds(5));
        Assert.Equal([["20", "30"]], raised); // the min alone never committed
    }

    [Fact]
    public async Task NumberRange_row_editor_Enter_commits_at_once_while_a_debounce_is_counting_down()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRow(
            Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)),
            p => p.Add(t => t.FilterDebounceMilliseconds, 5000)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var pending = TypeBoundAsync(cut, 0, "30");
        Assert.False(pending.IsCompleted);
        Assert.Empty(raised);

        await cut.InvokeAsync(() => NumberInputs(cut, ".wss-table-filter-row")[0]
            .KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

        Assert.Equal(["Doodad"], RowNames(cut));
        Assert.Equal([["30", ""]], raised);
        await pending;                 // cancelled, and it must not commit a second time
        Assert.Single(raised);
    }

    // =====================================================================================
    // DateRange
    // =====================================================================================

    // The picker's own typing/parse/commit protocol is DateRangePickerTests' subject; what matters
    // here is the editor's wiring, so an endpoint is set by raising the callback the editor bound --
    // exactly what the picker does once a date is committed. One test below drives the real inputs
    // end to end to prove the two meet.
    static Task SetEndpointAsync(IRenderedComponent<Table<Product>> cut, bool start, DateTime? value)
    {
        var picker = cut.FindComponent<DateRangePicker>().Instance;
        return cut.InvokeAsync(() => (start ? picker.StartChanged : picker.EndChanged).InvokeAsync(value));
    }

    [Fact]
    public async Task DateRange_is_inclusive_at_both_ends_with_the_end_covering_the_whole_day()
    {
        // Gadget is stamped 14:30 on 2026-02-10; a naive "value <= end" would drop it from a range
        // that ends on its own day.
        var cut = RenderRow(Columns(NameCol(), Col("Added", x => x.Added, filterable: true)));

        await SetEndpointAsync(cut, true, new DateTime(2026, 2, 10));
        await SetEndpointAsync(cut, false, new DateTime(2026, 2, 10));

        Assert.Equal(["Gadget"], RowNames(cut));
        Assert.True(Get<bool>(FilterOf<DateTime>(cut)!, "IsActive"));
    }

    [Fact]
    public async Task DateRange_applies_a_single_endpoint_and_excludes_rows_with_no_date()
    {
        var cut = RenderRow(Columns(
            NameCol(),
            Col("Due", x => x.Name == "Doodad" ? (DateOnly?)null : x.Due, filterable: true)));

        await SetEndpointAsync(cut, true, new DateTime(2026, 3, 15));
        Assert.Equal(["Gadget"], RowNames(cut)); // Doodad's date is null, so it can't be in range

        await SetEndpointAsync(cut, true, null);
        await SetEndpointAsync(cut, false, new DateTime(2026, 3, 1));
        Assert.Equal(["Widget", "Sprocket"], RowNames(cut));

        await SetEndpointAsync(cut, false, null);
        Assert.Equal(4, RowNames(cut).Length); // nothing set: the null row is back
    }

    [Fact]
    public async Task DateRange_row_editor_commits_each_endpoint_immediately_and_AppliedValues_round_trips()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRow(
            Columns(NameCol(), Col("Added", x => x.Added, filterable: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        await SetEndpointAsync(cut, true, new DateTime(2026, 2, 1)); // no debounce: a pick is discrete
        Assert.Equal(["Gadget", "Doodad"], RowNames(cut));
        Assert.Single(raised);

        var state = FilterOf<DateTime>(cut)!;
        var values = Get<IReadOnlyList<string>>(state, "AppliedValues");
        Assert.Equal(2, values.Count);
        Assert.Equal(new DateTime(2026, 2, 1).ToString("o", CultureInfo.InvariantCulture), values[0]);
        Assert.Equal("", values[1]);

        // Round trip: the serialized form restores the same pending endpoints.
        Assert.True(Call<bool>(state, "TryRestore", values));
        Assert.Equal(new DateTime(2026, 2, 1), Get<DateTime?>(state, "Start"));
        Assert.Null(state.GetType().GetProperty("End")!.GetValue(state));
        Assert.False(Get<bool>(state, "HasPendingChange"));
    }

    [Fact]
    public void DateRange_row_editor_commits_a_date_typed_into_the_picker_itself()
    {
        // End to end through the real picker: open the field, type, Enter -- which is the sequence
        // that raises StartChanged, and therefore the filter row's own commit.
        var cut = RenderRow(Columns(NameCol(), Col("Added", x => x.Added, filterable: true)));

        cut.Find(".wss-table-filter-row .wss-picker-input").Click();
        cut.Find(".wss-picker-input-start").Input("03/01/2026");
        cut.Find(".wss-table-filter-row .wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(["Doodad"], RowNames(cut));
    }

    [Fact]
    public async Task DateRange_dropdown_stages_until_OK()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Added", x => x.Added, filterable: true)));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        Assert.Equal("wss-table-filter-dropdown wss-table-filter-dropdown-wide", cut.Find(".wss-table-filter-dropdown").ClassName);
        Assert.NotNull(cut.Find(".wss-table-filter-dropdown .wss-table-filter-pane > .wss-picker"));
        await SetEndpointAsync(cut, true, new DateTime(2026, 3, 1));
        Assert.Equal(4, RowNames(cut).Length); // staged only

        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Doodad"], RowNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
    }

    // =====================================================================================
    // Bool
    // =====================================================================================

    static void PickInSelect(IRenderedComponent<Table<Product>> cut, string scope, string optionText)
    {
        cut.Find($"{scope} .wss-select").Click();
        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains(optionText)).Click();
    }

    [Fact]
    public void Bool_row_editor_picks_true_false_and_clears_back_to_every_row()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRow(
            Columns(NameCol(), Col("Active", x => x.Active, filterable: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var select = cut.Find(".wss-table-filter-row .wss-select");
        Assert.Contains("wss-select-single", select.ClassList);
        Assert.Contains("wss-select-sm", select.ClassList);
        Assert.Equal("All", cut.Find(".wss-table-filter-row .wss-select-selection-placeholder").TextContent);

        PickInSelect(cut, ".wss-table-filter-row", "Yes");
        Assert.Equal(["Widget", "Sprocket"], RowNames(cut));
        Assert.Equal([["true"]], raised);

        PickInSelect(cut, ".wss-table-filter-row", "No");
        Assert.Equal(["Gadget"], RowNames(cut));
        Assert.Equal(["false"], raised[^1]);

        cut.Find(".wss-table-filter-row button.wss-select-clear").Click();
        Assert.Equal(["Widget", "Gadget", "Doodad", "Sprocket"], RowNames(cut)); // the null row is back
        Assert.Empty(raised[^1]);
        Assert.False(Get<bool>(FilterOf<bool?>(cut)!, "IsActive"));
    }

    [Fact]
    public void Bool_option_and_placeholder_wording_comes_from_the_Table()
    {
        var cut = RenderRow(
            Columns(NameCol(), Col("Active", x => x.Active, filterable: true)),
            p => p.Add(t => t.FilterBoolAnyText, "Alle")
                  .Add(t => t.FilterBoolTrueText, "Ja")
                  .Add(t => t.FilterBoolFalseText, "Nein"));

        Assert.Equal("Alle", cut.Find(".wss-table-filter-row .wss-select-selection-placeholder").TextContent);
        cut.Find(".wss-table-filter-row .wss-select").Click();
        Assert.Equal(["Ja", "Nein"], cut.FindAll(".wss-select-item-option").Select(o => o.TextContent.Trim()));
        Assert.Equal("Filter by Active", cut.Find(".wss-table-filter-row input.wss-select-selection-search-input").GetAttribute("aria-label"));
    }

    [Fact]
    public void Bool_dropdown_stages_until_OK_and_Describe_uses_the_Table_wording()
    {
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Product>>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Active", x => x.Active, filterable: true)),
            p => p.Add(t => t.FilterBoolTrueText, "Ja")
                  .Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Product>>>(this, s => snapshots.Add(s))));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        PickInSelect(cut, ".wss-table-filter-dropdown", "Ja");
        Assert.Equal(4, RowNames(cut).Length); // staged only

        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Widget", "Sprocket"], RowNames(cut));
        var snapshot = Assert.Single(snapshots[^1]);
        Assert.Equal(TableFilterKind.Bool, snapshot.Kind);
        Assert.Equal(["true"], snapshot.Values);
        Assert.Equal("Ja", snapshot.Description);
    }

    // =====================================================================================
    // Enum options
    // =====================================================================================

    [Fact]
    public void An_enum_column_offers_every_member_by_display_name_and_matches_on_the_member_name()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Status", x => x.Status, filterable: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(
            ["In stock", "Out Of Stock", "Discontinued"], // [EnumDisplayName], then the camel-case split
            cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));

        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains("In stock"))
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Widget", "Doodad"], RowNames(cut));
        Assert.Equal([["InStock"]], raised); // the member name, not the label
    }

    [Fact]
    public void An_enum_column_offers_one_option_per_distinct_value_not_per_alias()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Priority", x => x.Qty > 20 ? Priority.Urgent : Priority.Normal, filterable: true)));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["Normal", "Urgent"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));

        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "Normal")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Widget"], RowNames(cut));
    }

    [Fact]
    public void A_nullable_enum_column_derives_the_same_option_list_and_excludes_its_nulls()
    {
        var data = Products();
        var cut = RenderTable(
            Columns(NameCol(), Col("Status", x => x.Name == "Gadget" ? (Status?)null : x.Status, filterable: true)),
            data: data);

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(3, cut.FindAll(".wss-table-filter-item").Count);

        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains("Out Of Stock"))
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        AssertNoDataRows(cut); // Gadget was the only OutOfStock row, and its value is now null
    }

    // =====================================================================================
    // FilterValuesFromData
    // =====================================================================================

    [Fact]
    public void FilterValuesFromData_builds_distinct_options_from_the_current_rows_skipping_nulls()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true)));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        // "b", null, "a", "a" -> distinct, non-null, ordered by the underlying values.
        Assert.Equal(["a", "b"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));

        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "a")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Doodad", "Sprocket"], RowNames(cut));
    }

    [Fact]
    public void FilterValuesFromData_options_are_formatted_exactly_as_the_cells_are_and_ordered_by_value()
    {
        // Format "D3" makes the text sort differently from the numbers only if the ordering used the
        // text; it doesn't here, but the option text must still be what the cell shows.
        var cut = RenderTable(Columns(NameCol(), Col("Qty", x => x.Qty, valuesFromData: true, format: "D3")));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        Assert.Equal(["010", "025", "040"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
        Assert.Equal(["010", "025", "040", "025"], cut.FindAll("tbody .wss-table-row td:nth-child(2)").Select(td => td.TextContent.Trim()));

        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "025")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Gadget", "Sprocket"], RowNames(cut));
    }

    [Fact]
    public void FilterValuesFromData_re_derives_on_a_DataSource_swap_and_prunes_an_orphaned_value()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "b")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Widget"], RowNames(cut));
        Assert.Equal([["b"]], raised);

        // New data with no "b" at all: the option goes, and so does the applied value that depended
        // on it -- otherwise it would keep excluding every row with nothing left to un-tick.
        cut.Render(p => p.Add(t => t.DataSource, new List<Product>
        {
            Products()[2], // Doodad, "a"
            Products()[1], // Gadget, null
        }));

        Assert.Equal(["Doodad", "Gadget"], RowNames(cut));
        Assert.Equal([["b"], []], raised);
        Assert.False(Get<bool>(FilterOf<string>(cut, 1)!, "IsActive"));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["a"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
    }

    [Fact]
    public void FilterValuesFromData_keeps_an_applied_value_the_new_data_still_offers()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "a")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        raised.Clear();

        cut.Render(p => p.Add(t => t.DataSource, new List<Product> { Products()[2], Products()[0] }));

        Assert.Equal(["Doodad"], RowNames(cut)); // "a" still narrows the new rows
        Assert.Empty(raised);                    // nothing was pruned, so nothing is announced
    }

    [Fact]
    public void FilterValuesFromData_leaves_an_explicitly_declared_filter_alone_on_a_DataSource_swap()
    {
        // Both declared at once: the explicit FilterOptions+OnFilter wins the kind, so the data-derived
        // options must not replace the consumer's list, swap in the derived predicate, or prune the
        // applied selection behind their back.
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderTable(
            Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true,
                options: [new("A", "a"), new("B", "b")], onFilter: (x, v) => x.Category == v)),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Product>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["A", "B"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "B")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Widget"], RowNames(cut));
        raised.Clear();

        // New rows with no "b" in them at all -- what used to prune the declared key.
        cut.Render(p => p.Add(t => t.DataSource, new List<Product> { Products()[2], Products()[3] }));

        Assert.Empty(raised);
        Assert.Equal(["b"], Get<IReadOnlyList<string>>(FilterOf<string>(cut, 1)!, "AppliedValues"));
        AssertNoDataRows(cut); // the declared filter still narrows by "b", which the new page has none of
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["A", "B"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
    }

    [Fact]
    public void FilterValuesFromData_offers_no_filter_until_the_data_yields_an_option()
    {
        // A funnel that opens an empty panel is worse than no funnel: there is nothing to select, and
        // nothing a DefaultFilterValues could validate against either.
        var cut = RenderTable(Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true)), data: []);
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.False(cut.FindComponents<PropertyColumn<Product, string>>()[1].Instance.CanFilter);

        cut.Render(p => p.Add(t => t.DataSource, Products()));

        Assert.True(cut.FindComponents<PropertyColumn<Product, string>>()[1].Instance.CanFilter);
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["a", "b"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
    }

    [Fact]
    public void A_FilterValuesFromData_column_declared_after_the_data_still_gets_its_options()
    {
        // The DataSource-swap branch only reaches columns already registered; a column brought in by
        // an @if long after the data has to be handed the rows when it registers.
        RenderFragment columns(bool withCategory) => Columns(
            NameCol(),
            withCategory ? Col("Category", x => x.Category, valuesFromData: true) : (b => { }));
        var cut = RenderTable(columns(false));
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));

        cut.Render(p => p.Add(t => t.ChildContent, columns(true)));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["a", "b"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
    }

    [Fact]
    public void FilterValuesFromData_rebuilds_when_Format_changes_and_re_derives_the_rows()
    {
        RenderFragment columns(string format) => Columns(NameCol(), Col("Qty", x => x.Qty, valuesFromData: true, format: format));
        var cut = RenderTable(columns("D3"));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["010", "025", "040"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
        cut.Find(".wss-table-filter-reset").Click();

        cut.Render(p => p.Add(t => t.ChildContent, columns("D5")));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["00010", "00025", "00040"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "00040")
            .QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Doodad"], RowNames(cut)); // the predicate compares the NEW format too
    }

    [Fact]
    public void FilterValuesFromData_re_derives_its_options_after_a_culture_change()
    {
        // The option keys ARE formatted cell text, so they follow CurrentCulture -- and so does the
        // live predicate. A cache keyed on Format alone left the two in different cultures, and
        // nothing matched.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var cut = RenderTable(Columns(NameCol(), Col("Price", x => x.Price, valuesFromData: true)));
            cut.FindAll(".wss-table-filter-trigger")[0].Click();
            Assert.Equal(["5", "9.99", "19.5"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
            cut.Find(".wss-table-filter-ok").Click();

            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            cut.Render(p => p.Add(t => t.Bordered, true));

            cut.FindAll(".wss-table-filter-trigger")[0].Click();
            Assert.Equal(["5", "9,99", "19,5"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
            cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Trim() == "9,99")
                .QuerySelector("input")!.Change(true);
            cut.Find(".wss-table-filter-ok").Click();
            Assert.Equal(["Widget"], RowNames(cut));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FilterValuesFromData_re_renders_stably_across_an_unrelated_parameter_pass()
    {
        // The data-derived options live outside the FilterOptions parameter the base compares, so a
        // plain re-render must not look like an options change (which would loop through the
        // corrective render the base queues).
        var cut = RenderTable(Columns(NameCol(), Col("Category", x => x.Category, valuesFromData: true)));
        var state = FilterOf<string>(cut, 1);

        cut.Render(p => p.Add(t => t.Bordered, true));
        cut.Render(p => p.Add(t => t.Bordered, false));

        Assert.Same(state, FilterOf<string>(cut, 1));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.Equal(["a", "b"], cut.FindAll(".wss-table-filter-item").Select(li => li.TextContent.Trim()));
    }

    // =====================================================================================
    // Snapshots and Loading
    // =====================================================================================

    [Fact]
    public async Task OnFiltersChanged_carries_the_right_Kind_and_Values_for_every_typed_column()
    {
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Product>>>();
        var cut = RenderRow(
            Columns(
                NameCol(),
                Col("Qty", x => x.Qty, filterable: true),
                Col("Added", x => x.Added, filterable: true),
                Col("Active", x => x.Active, filterable: true),
                Col("Status", x => x.Status, filterable: true)),
            p => p.Add(t => t.FilterDebounceMilliseconds, 0)
                  .Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Product>>>(this, s => snapshots.Add(s))));

        await TypeBoundAsync(cut, 0, "10");
        await SetEndpointAsync(cut, true, new DateTime(2026, 1, 1));
        PickInSelect(cut, "thead tr.wss-table-filter-row td:nth-child(4)", "Yes");
        PickInSelect(cut, "thead tr.wss-table-filter-row td:nth-child(5)", "In stock");

        var latest = snapshots[^1];
        Assert.Equal(4, latest.Count);
        Assert.Equal(TableFilterKind.NumberRange, latest[0].Kind);
        Assert.Equal(["10", ""], latest[0].Values);
        Assert.Equal("≥ 10", latest[0].Description);
        Assert.Equal(TableFilterKind.DateRange, latest[1].Kind);
        Assert.Equal(new DateTime(2026, 1, 1).ToString("o", CultureInfo.InvariantCulture), latest[1].Values[0]);
        Assert.Equal(TableFilterKind.Bool, latest[2].Kind);
        Assert.Equal(["true"], latest[2].Values);
        Assert.Equal(TableFilterKind.Options, latest[3].Kind);
        Assert.Equal(["InStock"], latest[3].Values);
        Assert.Equal(["Widget"], RowNames(cut));
    }

    [Fact]
    public void The_row_select_and_date_editor_labels_come_from_the_Table()
    {
        var cut = RenderRow(
            Columns(
                NameCol(),
                Col("Status", x => x.Status, filterable: true),
                Col("Added", x => x.Added, filterable: true)),
            p => p
                .Add(t => t.FilterSelectPlaceholder, "Alle")
                .Add(t => t.FilterEmptyText, "Keine Treffer")
                .Add(t => t.FilterDateStartLabel, "Von")
                .Add(t => t.FilterDateEndLabel, "Bis"));

        var cells = cut.FindAll("thead tr.wss-table-filter-row td");
        Assert.Equal("Alle", cells[1].QuerySelector(".wss-select-selection-placeholder")!.TextContent.Trim());
        Assert.Equal("Von", cells[2].QuerySelector(".wss-picker-input-start")!.GetAttribute("aria-label"));
        Assert.Equal("Bis", cells[2].QuerySelector(".wss-picker-input-end")!.GetAttribute("aria-label"));

        cut.Find("thead tr.wss-table-filter-row td:nth-child(2) .wss-select").Click();
        cut.Find("thead tr.wss-table-filter-row td:nth-child(2) .wss-select input").Input("zzz");
        Assert.Equal("Keine Treffer", cut.Find(".wss-select-item-empty").TextContent.Trim());
    }

    [Fact]
    public void The_dropdown_date_editor_takes_the_same_endpoint_labels()
    {
        var cut = RenderTable(Columns(NameCol(), Col("Added", x => x.Added, filterable: true)),
            p => p.Add(t => t.FilterDateStartLabel, "Von").Add(t => t.FilterDateEndLabel, "Bis"));

        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        Assert.Equal("Von", cut.Find(".wss-table-filter-dropdown .wss-picker-input-start").GetAttribute("aria-label"));
        Assert.Equal("Bis", cut.Find(".wss-table-filter-dropdown .wss-picker-input-end").GetAttribute("aria-label"));
    }

    [Fact]
    public void Loading_disables_every_typed_row_editor()
    {
        var cut = RenderRow(Columns(
            NameCol(),
            Col("Qty", x => x.Qty, filterable: true),
            Col("Added", x => x.Added, filterable: true),
            Col("Active", x => x.Active, filterable: true)));

        Assert.All(NumberInputs(cut, ".wss-table-filter-row"), i => Assert.False(i.HasAttribute("disabled")));
        Assert.DoesNotContain("wss-picker-disabled", cut.Find(".wss-table-filter-row .wss-picker").ClassList);
        Assert.DoesNotContain("wss-select-disabled", cut.Find(".wss-table-filter-row .wss-select").ClassList);

        cut.Render(p => p.Add(t => t.Loading, true));

        Assert.All(NumberInputs(cut, ".wss-table-filter-row"), i => Assert.True(i.HasAttribute("disabled")));
        Assert.Contains("wss-picker-disabled", cut.Find(".wss-table-filter-row .wss-picker").ClassList);
        Assert.Contains("wss-select-disabled", cut.Find(".wss-table-filter-row .wss-select").ClassList);

        cut.Render(p => p.Add(t => t.Loading, false));
        Assert.All(NumberInputs(cut, ".wss-table-filter-row"), i => Assert.False(i.HasAttribute("disabled")));
    }

    [Fact]
    public void An_already_open_typed_dropdown_stays_usable_while_Loading()
    {
        // Same contract the funnel has: Loading disables the trigger and the row editors, never a
        // panel that was already open (the mask never covered it).
        var cut = RenderTable(Columns(NameCol(), Col("Qty", x => x.Qty, filterable: true)));
        cut.FindAll(".wss-table-filter-trigger")[0].Click();

        cut.Render(p => p.Add(t => t.Loading, true));

        Assert.All(NumberInputs(cut, ".wss-table-filter-dropdown"), i => Assert.False(i.HasAttribute("disabled")));
        Assert.True(cut.FindAll(".wss-table-filter-trigger")[0].HasAttribute("disabled"));
    }
}
