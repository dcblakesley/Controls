using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Pins the seams the column-filter state object (<c>ColumnFilterState&lt;TItem&gt;</c>, held by
/// <c>Column.Filter</c>) introduced: when it exists, that it is the same instance across parameter
/// passes, that it is rebuilt when the declared kind changes, and its commit/restore contract. The
/// behaviour it drives (OK/Reset/prune/removal) is covered by the filter tests in
/// <see cref="UiKitTableTests"/>, which pass unchanged. The state type is internal with no
/// InternalsVisibleTo, so the contract is reached by reflection -- the same route
/// <see cref="UiKitTableTests"/> already takes to <c>Table.AnyColumnFilterOpen</c>.
/// </summary>
public class UiKitTableFilterTests : BunitContext
{
    public UiKitTableFilterTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    record Person(string Name, int Age);

    static List<Person> People() => [new("Alice", 30), new("Bob", 25), new("Carol", 40)];

    static List<TableFilterOption> NameOptions() =>
        [new("Alice", "Alice"), new("Bob", "Bob"), new("Carol", "Carol")];

    // Options built inline per pass (a fresh list every render), exactly like markup does.
    static RenderFragment NameColumn(IReadOnlyList<TableFilterOption>? options, Func<Person, string, bool>? onFilter) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
        builder.AddAttribute(3, "FilterOptions", options);
        builder.AddAttribute(4, "OnFilter", onFilter);
        builder.CloseComponent();
    };

    static Func<Person, string, bool> NameEquals => (x, v) => x.Name == v;

    string[] RenderedNames(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("tbody .wss-table-row td.wss-table-cell").Select(td => td.TextContent.Trim()).ToArray();

    void CheckOption(IRenderedComponent<Table<Person>> cut, string text) =>
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains(text))
            .QuerySelector("input")!.Change(true);

    void ApplyFilter(IRenderedComponent<Table<Person>> cut, params string[] values)
    {
        cut.Find(".wss-table-filter-trigger").Click();
        foreach (var v in values) CheckOption(cut, v);
        cut.Find(".wss-table-filter-ok").Click();
    }

    // ----- Reflection into the internal state object -----

    static object? FilterOf(IRenderedComponent<Table<Person>> cut) =>
        typeof(Column<Person>)
            .GetProperty("Filter", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(cut.FindComponent<PropertyColumn<Person, string>>().Instance);

    static T Call<T>(object state, string method, params object?[] args) =>
        (T)state.GetType().GetMethod(method)!.Invoke(state, args)!;

    static void Call(object state, string method, params object?[] args) =>
        state.GetType().GetMethod(method)!.Invoke(state, args);

    static T Get<T>(object state, string property) =>
        (T)state.GetType().GetProperty(property)!.GetValue(state)!;

    [Fact]
    public void A_column_without_both_FilterOptions_and_OnFilter_has_no_filter_state()
    {
        // CanFilter is now "Filter is not null", so the two must agree -- and the kind derivation must
        // still demand both parameters (no dead UI for a column that forgot OnFilter).
        var neither = Render<Table<Person>>(p => p.Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(null, null)));
        var optionsOnly = Render<Table<Person>>(p => p.Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), null)));
        var predicateOnly = Render<Table<Person>>(p => p.Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(null, NameEquals)));
        var both = Render<Table<Person>>(p => p.Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));

        Assert.Null(FilterOf(neither));
        Assert.Null(FilterOf(optionsOnly));
        Assert.Null(FilterOf(predicateOnly));
        Assert.False(neither.FindComponent<PropertyColumn<Person, string>>().Instance.CanFilter);

        var state = FilterOf(both);
        Assert.NotNull(state);
        Assert.True(both.FindComponent<PropertyColumn<Person, string>>().Instance.CanFilter);
        Assert.Equal(TableFilterKind.Options, Get<TableFilterKind>(state, "Kind"));
    }

    [Fact]
    public void The_state_instance_survives_parameter_passes_while_the_kind_is_unchanged()
    {
        // The applied selection lives IN the state object, so the object has to be the same one
        // across re-renders -- a fresh inline options list per pass (value-equal) must refresh the
        // existing state, not replace it.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        ApplyFilter(cut, "Alice");
        var before = FilterOf(cut);
        Assert.Equal(["Alice"], RenderedNames(cut));

        cut.Render(p => p.Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));

        Assert.Same(before, FilterOf(cut));
        Assert.Equal(["Alice"], RenderedNames(cut));
        Assert.True(Get<bool>(before!, "IsActive"));
    }

    [Fact]
    public void The_state_is_rebuilt_when_the_kind_changes_and_carries_nothing_over()
    {
        // Options gone -> no state (CanFilter false, nothing open); options back -> a NEW, empty state.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        ApplyFilter(cut, "Alice");
        var first = FilterOf(cut);

        cut.Render(p => p.Add(t => t.ChildContent, NameColumn(null, NameEquals)));
        Assert.Null(FilterOf(cut));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));

        cut.Render(p => p.Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        var second = FilterOf(cut);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.False(Get<bool>(second, "IsActive"));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    [Fact]
    public void Losing_OnFilter_while_a_filter_is_applied_drops_the_state_and_reports_it()
    {
        // The column stops offering a filter (kind -> none) while it was narrowing rows: the rows the
        // lost selection excluded come back and the consumer hears about it with an empty payload --
        // the same treatment FilterOptions going null already got. (Before the state object, the
        // applied set silently survived in the column and resumed narrowing if OnFilter came back.)
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        ApplyFilter(cut, "Alice");
        Assert.Equal(["Alice"], RenderedNames(cut));
        raised = null;

        cut.Render(p => p.Add(t => t.ChildContent, NameColumn(NameOptions(), null)));

        Assert.Null(FilterOf(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.NotNull(raised);
        Assert.Empty(raised.Value.Values);

        // And coming back starts clean -- nothing resumes narrowing on its own.
        raised = null;
        cut.Render(p => p.Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Null(raised);
    }

    [Fact]
    public void Commit_reports_a_real_change_only()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        var state = FilterOf(cut)!;

        // Nothing staged, nothing applied: a no-op.
        Assert.False(Call<bool>(state, "Commit"));
        Assert.False(Get<bool>(state, "IsActive"));

        Call(state, "TogglePending", "Bob", true);
        Assert.True(Get<bool>(state, "HasPendingChange"));
        Assert.True(Call<bool>(state, "Commit"));
        Assert.False(Get<bool>(state, "HasPendingChange"));
        Assert.Equal(["Bob"], Get<IReadOnlyList<string>>(state, "AppliedValues"));

        // Re-committing the identical set is a no-op again; so is clearing after Clear.
        Assert.False(Call<bool>(state, "Commit"));
        Assert.True(Call<bool>(state, "Clear"));
        Assert.False(Call<bool>(state, "Clear"));
        Assert.Empty(Get<IReadOnlyList<string>>(state, "AppliedValues"));
    }

    [Fact]
    public void Discard_restages_from_the_applied_selection()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        var state = FilterOf(cut)!;
        Call(state, "TogglePending", "Alice", true);
        Call(state, "Commit");

        Call(state, "TogglePending", "Alice", false);
        Call(state, "TogglePending", "Carol", true);
        Assert.True(Get<bool>(state, "HasPendingChange"));

        Call(state, "Discard");

        Assert.False(Get<bool>(state, "HasPendingChange"));
        Assert.True(Call<bool>(state, "IsPending", "Alice"));
        Assert.False(Call<bool>(state, "IsPending", "Carol"));
    }

    [Fact]
    public void TryRestore_then_Commit_round_trips_AppliedValues_in_option_order()
    {
        // One serialization contract: what AppliedValues emits, TryRestore accepts. Order on the way
        // out is the options' declared order regardless of the order restored; a key with no option
        // is dropped on the way in, so the applied set can never hold an un-tickable value.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        var state = FilterOf(cut)!;

        Assert.True(Call<bool>(state, "TryRestore", (IReadOnlyList<string>)["Carol", "Zed", "Alice"]));
        Assert.True(Call<bool>(state, "Commit"));

        var applied = Get<IReadOnlyList<string>>(state, "AppliedValues");
        Assert.Equal(["Alice", "Carol"], applied);

        // And back again through the same contract onto a fresh pending set.
        Call(state, "Clear");
        Assert.True(Call<bool>(state, "TryRestore", applied));
        Assert.True(Call<bool>(state, "Commit"));
        Assert.Equal(["Alice", "Carol"], Get<IReadOnlyList<string>>(state, "AppliedValues"));
    }

    [Fact]
    public void Describe_reports_the_applied_count_and_null_when_inactive()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        var state = FilterOf(cut)!;

        Assert.Null(Call<string?>(state, "Describe", cut.Instance));

        ApplyFilter(cut, "Alice", "Carol");
        Assert.Equal("2 selected", Call<string?>(state, "Describe", cut.Instance));
    }

    [Fact]
    public void The_dropdown_editor_is_a_child_component_with_no_wrapper_DOM()
    {
        // The option list moved into TableFilterEditor; the panel's DOM must not have changed -- the
        // list is still the dialog's first child, directly followed by the footer.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        cut.Find(".wss-table-filter-trigger").Click();

        var panel = cut.Find(".wss-table-filter-dropdown");
        var children = panel.Children;
        Assert.Equal(2, children.Length);
        Assert.Equal("wss-table-filter-list", children[0].ClassName);
        Assert.Equal("wss-table-filter-footer", children[1].ClassName);
        Assert.Equal(3, children[0].QuerySelectorAll("li.wss-table-filter-item > label.wss-table-filter-option-label > input.wss-table-filter-checkbox").Length);
        Assert.Single(cut.FindComponents<TableFilterEditor<Person>>());
        Assert.Equal(TableFilterPlacement.Dropdown, cut.FindComponent<TableFilterEditor<Person>>().Instance.Placement);
    }
}
