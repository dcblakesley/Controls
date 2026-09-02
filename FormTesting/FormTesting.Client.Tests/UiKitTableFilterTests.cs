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
        // And the panel's own class attribute is the exact pre-Text-kind string -- the Text panel's
        // wider floor is a modifier that must not leak a trailing space into the Options panel.
        Assert.Equal("wss-table-filter-dropdown", panel.ClassName);
    }

    // =====================================================================================
    // Text kind (Column.FilterText / TextFilterMatch)
    // =====================================================================================

    static Func<Person, string?> ByName => x => x.Name;
    static Func<Person, string?> ByAge => x => x.Age.ToString();

    static RenderFragment TextColumn(
        Func<Person, string?>? accessor,
        TextFilterMatch match = TextFilterMatch.Contains,
        IReadOnlyList<TableFilterOption>? options = null,
        Func<Person, string, bool>? onFilter = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
        builder.AddAttribute(3, "FilterText", accessor);
        builder.AddAttribute(4, "TextFilterMatch", match);
        builder.AddAttribute(5, "FilterOptions", options);
        builder.AddAttribute(6, "OnFilter", onFilter);
        builder.CloseComponent();
    };

    IRenderedComponent<Table<Person>> RenderTextFilterable(
        TextFilterMatch match = TextFilterMatch.Contains,
        Action<(Column<Person> Column, IReadOnlyList<string> Values)>? onFilterChanged = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, People());
            p.Add(t => t.ChildContent, TextColumn(ByName, match));
            if (onFilterChanged is not null)
                p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, onFilterChanged));
        });

    // Opens the dropdown and types into the search box (staged only -- OK/Enter applies).
    static void OpenAndType(IRenderedComponent<Table<Person>> cut, string text)
    {
        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-input").Input(text);
    }

    [Fact]
    public void FilterText_declares_the_Text_kind_and_renders_a_search_box_in_place_of_the_list()
    {
        var cut = RenderTextFilterable();

        var state = FilterOf(cut);
        Assert.NotNull(state);
        Assert.Equal(TableFilterKind.Text, Get<TableFilterKind>(state, "Kind"));
        Assert.True(cut.FindComponent<PropertyColumn<Person, string>>().Instance.CanFilter);

        cut.Find(".wss-table-filter-trigger").Click();

        var panel = cut.Find(".wss-table-filter-dropdown");
        Assert.Equal("wss-table-filter-dropdown wss-table-filter-dropdown-text", panel.ClassName);
        var input = cut.Find("input.wss-table-filter-input");
        Assert.Equal("search", input.GetAttribute("type"));
        // Named after the column's filter, exactly as the trigger and the dialog are.
        Assert.Equal("Filter Name", input.GetAttribute("aria-label"));
        Assert.Equal("Filter Name", panel.GetAttribute("aria-label"));
        Assert.Empty(cut.FindAll(".wss-table-filter-list"));
        Assert.Empty(cut.FindAll(".wss-table-filter-input-clear")); // nothing typed yet
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-ok"));
        Assert.NotEmpty(cut.FindAll(".wss-table-filter-reset"));
    }

    [Fact]
    public void Text_Contains_matches_case_insensitively_and_applies_on_OK()
    {
        var cut = RenderTextFilterable();

        OpenAndType(cut, "AL"); // "Alice" only -- "Carol" has no "al"
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut)); // staged, not applied
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        var trigger = cut.Find(".wss-table-filter-trigger");
        Assert.Contains("wss-table-filter-active", trigger.ClassList);
        Assert.Equal("Filter Name (filter applied)", trigger.GetAttribute("aria-label"));

        // Reopening shows the applied text, and the search box is named with the applied suffix too.
        trigger.Click();
        var input = cut.Find(".wss-table-filter-input");
        Assert.Equal("AL", input.GetAttribute("value"));
        Assert.Equal("Filter Name (filter applied)", input.GetAttribute("aria-label"));
    }

    [Fact]
    public void Text_StartsWith_and_Equals_modes_narrow_accordingly()
    {
        var startsWith = RenderTextFilterable(TextFilterMatch.StartsWith);
        OpenAndType(startsWith, "c"); // Carol, not Alice (which merely contains a c)
        startsWith.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Carol"], RenderedNames(startsWith));

        var equals = RenderTextFilterable(TextFilterMatch.Equals);
        OpenAndType(equals, "bob");
        equals.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Bob"], RenderedNames(equals));

        OpenAndType(equals, "bo"); // a prefix is not equal
        equals.Find(".wss-table-filter-ok").Click();
        AssertNoDataRows(equals);
    }

    // Zero matching rows renders the Table's "No data" placeholder row (itself a .wss-table-row with
    // a .wss-table-cell), so "no rows" is asserted as "only the placeholder", not an empty list.
    static void AssertNoDataRows(IRenderedComponent<Table<Person>> cut)
    {
        Assert.Single(cut.FindAll("tbody .wss-table-placeholder"));
        Assert.Empty(cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)"));
    }

    [Fact]
    public void Whitespace_only_text_is_inactive_and_OK_with_it_is_a_no_op()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderTextFilterable(onFilterChanged: v => raised = v);

        OpenAndType(cut, "   ");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.Null(raised);
        Assert.False(Get<bool>(FilterOf(cut)!, "IsActive"));
    }

    [Fact]
    public void Enter_in_the_search_box_applies_exactly_like_OK()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderTextFilterable(onFilterChanged: v => raised = v);

        OpenAndType(cut, "bob");
        cut.Find(".wss-table-filter-input").KeyDown("Enter");

        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.NotNull(raised);
        Assert.Equal(["bob"], raised.Value.Values);
    }

    [Fact]
    public void The_clear_button_clears_the_staged_text_only()
    {
        var cut = RenderTextFilterable();
        OpenAndType(cut, "bob");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Bob"], RenderedNames(cut));

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("bob", cut.Find(".wss-table-filter-input").GetAttribute("value"));
        var clear = cut.Find(".wss-table-filter-input-clear");
        Assert.Equal("Clear", clear.GetAttribute("aria-label"));
        Assert.NotNull(clear.QuerySelector("svg"));

        clear.Click();

        // Staged text gone (button with it); the APPLIED filter is untouched until OK.
        Assert.False(cut.Find(".wss-table-filter-input").HasAttribute("value"));
        Assert.Empty(cut.FindAll(".wss-table-filter-input-clear"));
        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);

        // Dismissing discards the cleared staging; the next open re-stages from the applied text.
        cut.Find(".wss-table-filter-backdrop").Click();
        Assert.Equal(["Bob"], RenderedNames(cut));
        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("bob", cut.Find(".wss-table-filter-input").GetAttribute("value"));

        // And clearing then OK does apply the empty text: everything comes back.
        cut.Find(".wss-table-filter-input-clear").Click();
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
    }

    [Fact]
    public void FilterClearLabel_names_the_clear_button()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.FilterClearLabel, "Borrar")
            .Add(t => t.ChildContent, TextColumn(ByName)));
        OpenAndType(cut, "x");

        Assert.Equal("Borrar", cut.Find(".wss-table-filter-input-clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void OnFilterChanged_payload_is_the_single_trimmed_text_and_empty_on_Reset()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderTextFilterable(onFilterChanged: v => raised = v);

        OpenAndType(cut, "  Bob ");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.NotNull(raised);
        Assert.Equal(["Bob"], raised.Value.Values);
        Assert.Equal(["Bob"], Get<IReadOnlyList<string>>(FilterOf(cut)!, "AppliedValues"));

        // The same text again is not a change.
        raised = null;
        OpenAndType(cut, "Bob");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Null(raised);

        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-reset").Click();
        Assert.NotNull(raised);
        Assert.Empty(raised.Value.Values);
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
    }

    [Fact]
    public void Text_Describe_names_the_match_mode_and_the_applied_text()
    {
        var cut = RenderTextFilterable(TextFilterMatch.StartsWith);
        var state = FilterOf(cut)!;
        Assert.Null(Call<string?>(state, "Describe", cut.Instance));

        OpenAndType(cut, " ca ");
        cut.Find(".wss-table-filter-ok").Click();

        Assert.Equal("starts with \"ca\"", Call<string?>(state, "Describe", cut.Instance));
    }

    [Fact]
    public void Swapping_the_FilterText_delegate_re_derives_rows_while_a_text_filter_is_applied()
    {
        // "0" matches no name but two ages; the swap is detected by method identity (a different
        // lambda), exactly as a swapped OnFilter is, and the applied text survives it -- same kind,
        // same state instance.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, TextColumn(ByName)));
        OpenAndType(cut, "0");
        cut.Find(".wss-table-filter-ok").Click();
        AssertNoDataRows(cut);
        var state = FilterOf(cut);

        cut.Render(p => p.Add(t => t.ChildContent, TextColumn(ByAge)));

        Assert.Equal(["Alice", "Carol"], RenderedNames(cut)); // 30 and 40 contain "0"
        Assert.Same(state, FilterOf(cut));
        Assert.True(Get<bool>(state!, "IsActive"));
    }

    [Fact]
    public void Changing_TextFilterMatch_re_derives_rows_while_a_text_filter_is_applied()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, TextColumn(ByName, TextFilterMatch.Contains)));
        OpenAndType(cut, "c");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Carol"], RenderedNames(cut));

        cut.Render(p => p.Add(t => t.ChildContent, TextColumn(ByName, TextFilterMatch.StartsWith)));

        Assert.Equal(["Carol"], RenderedNames(cut));
        Assert.Equal("starts with \"c\"", Call<string?>(FilterOf(cut)!, "Describe", cut.Instance));
    }

    [Fact]
    public void Opening_a_Text_dropdown_focuses_the_search_box_not_the_panel()
    {
        var cut = RenderTextFilterable();

        cut.Find(".wss-table-filter-trigger").Click();

        var focus = JSInterop.Invocations.Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)).ToList();
        var focused = Assert.Single(focus); // one focus call on open, not input AND panel
        var inputRefId = cut.Find(".wss-table-filter-input").GetAttribute("blazor:elementreference");
        Assert.False(string.IsNullOrEmpty(inputRefId));
        Assert.Equal(inputRefId, ((ElementReference)focused.Arguments[0]!).Id);
    }

    [Fact]
    public void Options_beats_Text_and_dropping_the_options_falls_through_to_Text()
    {
        // Precedence: FilterOptions+OnFilter declares Options even with FilterText set; take the
        // options away and the same column becomes a Text filter -- a kind change, so a fresh state.
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, TextColumn(ByName, options: NameOptions(), onFilter: NameEquals)));
        var options = FilterOf(cut)!;
        Assert.Equal(TableFilterKind.Options, Get<TableFilterKind>(options, "Kind"));

        cut.Render(p => p.Add(t => t.ChildContent, TextColumn(ByName, options: null, onFilter: NameEquals)));

        var text = FilterOf(cut)!;
        Assert.Equal(TableFilterKind.Text, Get<TableFilterKind>(text, "Kind"));
        Assert.NotSame(options, text);
        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Single(cut.FindAll(".wss-table-filter-input"));
        Assert.Empty(cut.FindAll(".wss-table-filter-list"));
    }

    // =====================================================================================
    // Custom kind (Column.FilterDropdown + TableFilterContext)
    // =====================================================================================

    // A consumer template that just reports what it was given: the test drives the context directly
    // (on the renderer's dispatcher) and reads the panel back to see that the template re-rendered.
    static RenderFragment CustomColumn(
        Func<Person, string, bool>? onFilter,
        Action<TableFilterContext<Person>> capture,
        IReadOnlyList<TableFilterOption>? options = null) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
        builder.AddAttribute(3, "OnFilter", onFilter);
        builder.AddAttribute(4, "FilterOptions", options);
        builder.AddAttribute(5, "FilterDropdown", (RenderFragment<TableFilterContext<Person>>)(ctx => b =>
        {
            capture(ctx);
            b.OpenElement(0, "div");
            b.AddAttribute(1, "class", "custom-panel");
            b.AddContent(2, string.Join(",", ctx.SelectedValues));
            b.CloseElement();
        }));
        builder.CloseComponent();
    };

    (IRenderedComponent<Table<Person>> Cut, Func<TableFilterContext<Person>> Context) RenderCustomFilterable(
        Func<Person, string, bool>? onFilter = null,
        IReadOnlyList<TableFilterOption>? options = null,
        Action<(Column<Person> Column, IReadOnlyList<string> Values)>? onFilterChanged = null)
    {
        TableFilterContext<Person>? latest = null;
        var cut = Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, People());
            p.Add(t => t.ChildContent, CustomColumn(onFilter ?? NameEquals, ctx => latest = ctx, options));
            if (onFilterChanged is not null)
                p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, onFilterChanged));
        });
        return (cut, () => latest ?? throw new InvalidOperationException("the template has not rendered"));
    }

    [Fact]
    public void FilterDropdown_declares_Custom_and_the_template_owns_the_whole_panel()
    {
        var (cut, context) = RenderCustomFilterable(options: NameOptions());

        var state = FilterOf(cut)!;
        Assert.Equal(TableFilterKind.Custom, Get<TableFilterKind>(state, "Kind")); // beats Options
        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));

        cut.Find(".wss-table-filter-trigger").Click();

        var panel = cut.Find(".wss-table-filter-dropdown");
        var child = Assert.Single(panel.Children);
        Assert.Equal("custom-panel", child.ClassName); // no built-in list, no footer
        Assert.Empty(cut.FindAll(".wss-table-filter-footer"));
        Assert.Empty(cut.FindComponents<TableFilterEditor<Person>>());

        var ctx = context();
        Assert.Same(cut.FindComponent<PropertyColumn<Person, string>>().Instance, ctx.Column);
        Assert.True(ctx.IsOpen);
        Assert.Empty(ctx.SelectedValues);
        Assert.Equal(["Alice", "Bob", "Carol"], ctx.Options!.Select(o => o.Value));
    }

    [Fact]
    public async Task ConfirmAsync_applies_the_staged_keys_narrows_rows_and_raises_OnFilterChanged()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var (cut, context) = RenderCustomFilterable(onFilterChanged: v => raised = v);
        cut.Find(".wss-table-filter-trigger").Click();

        await cut.InvokeAsync(() => context().SetSelectedValues(["Bob"]));
        Assert.Equal("Bob", cut.Find(".custom-panel").TextContent); // the template re-rendered with the staged key
        Assert.Equal(["Bob"], context().SelectedValues);
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut)); // staged only
        Assert.Null(raised);

        await cut.InvokeAsync(() => context().ConfirmAsync());

        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.NotNull(raised);
        Assert.Equal(["Bob"], raised.Value.Values);
    }

    [Fact]
    public async Task ConfirmAsync_false_applies_but_keeps_the_dropdown_open()
    {
        var (cut, context) = RenderCustomFilterable();
        cut.Find(".wss-table-filter-trigger").Click();

        await cut.InvokeAsync(() => context().SetSelectedValues(["Carol"]));
        await cut.InvokeAsync(() => context().ConfirmAsync(closeDropdown: false));

        Assert.Equal(["Carol"], RenderedNames(cut));
        Assert.Single(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal("true", cut.Find(".wss-table-filter-trigger").GetAttribute("aria-expanded"));
        Assert.True(context().IsOpen);
        Assert.Equal("Carol", cut.Find(".custom-panel").TextContent);
    }

    [Fact]
    public async Task ResetAsync_clears_applied_and_staged_and_closes()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var (cut, context) = RenderCustomFilterable(onFilterChanged: v => raised = v);
        cut.Find(".wss-table-filter-trigger").Click();
        await cut.InvokeAsync(() => context().SetSelectedValues(["Bob"]));
        await cut.InvokeAsync(() => context().ConfirmAsync());
        Assert.Equal(["Bob"], RenderedNames(cut));
        raised = null;

        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("Bob", cut.Find(".custom-panel").TextContent); // re-staged from applied
        await cut.InvokeAsync(() => context().ResetAsync());

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.NotNull(raised);
        Assert.Empty(raised.Value.Values);
        Assert.DoesNotContain("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
    }

    [Fact]
    public async Task Close_discards_the_staged_keys()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var (cut, context) = RenderCustomFilterable(onFilterChanged: v => raised = v);
        cut.Find(".wss-table-filter-trigger").Click();

        await cut.InvokeAsync(() => context().SetSelectedValues(["Bob"]));
        await cut.InvokeAsync(() => context().Close());

        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Null(raised);
        cut.Find(".wss-table-filter-trigger").Click();
        Assert.Equal("", cut.Find(".custom-panel").TextContent); // nothing survived the discard
    }

    [Fact]
    public async Task Custom_without_OnFilter_renders_the_funnel_tracks_state_and_excludes_nothing()
    {
        // The shape for server-side filtering: the consumer reads OnFilterChanged and refetches.
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        TableFilterContext<Person>? latest = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, CustomColumn(onFilter: null, ctx => latest = ctx)));

        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));
        cut.Find(".wss-table-filter-trigger").Click();
        await cut.InvokeAsync(() => latest!.SetSelectedValues(["Bob"]));
        await cut.InvokeAsync(() => latest!.ConfirmAsync());

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Contains("wss-table-filter-active", cut.Find(".wss-table-filter-trigger").ClassList);
        Assert.NotNull(raised);
        Assert.Equal(["Bob"], raised.Value.Values);
    }

    [Fact]
    public void Custom_AppliedValues_follow_option_order_then_insertion_order_and_accept_any_key()
    {
        var (cut, _) = RenderCustomFilterable(options: NameOptions());
        var state = FilterOf(cut)!;

        Assert.True(Call<bool>(state, "TryRestore", (IReadOnlyList<string>)["Zed", "Carol", "Alice"]));
        Assert.True(Call<bool>(state, "Commit"));

        // Unlike Options, a key with no option is kept (the template's key space is its own).
        Assert.Equal(["Alice", "Carol", "Zed"], Get<IReadOnlyList<string>>(state, "AppliedValues"));
        Assert.Equal("3 selected", Call<string?>(state, "Describe", cut.Instance));
    }

    // =====================================================================================
    // FilterIcon
    // =====================================================================================

    [Fact]
    public void FilterIcon_replaces_the_glyph_and_leaves_the_trigger_button_unchanged()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, NameEquals)
                .Add(c => c.FilterIcon, (bool applied) => b =>
                {
                    b.OpenElement(0, "span");
                    b.AddAttribute(1, "class", applied ? "my-icon my-icon-on" : "my-icon");
                    b.CloseElement();
                })));

        var trigger = cut.Find(".wss-table-filter-trigger");
        Assert.Null(trigger.QuerySelector("svg"));
        var icon = Assert.Single(trigger.Children);
        Assert.Equal("my-icon", icon.ClassName);
        Assert.Equal("Filter Name", trigger.GetAttribute("aria-label"));
        Assert.Equal("dialog", trigger.GetAttribute("aria-haspopup"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));

        ApplyFilter(cut, "Alice");

        trigger = cut.Find(".wss-table-filter-trigger");
        Assert.Equal("my-icon my-icon-on", trigger.Children[0].ClassName); // context = applied
        Assert.Contains("wss-table-filter-active", trigger.ClassList);
        Assert.Equal("Filter Name (filter applied)", trigger.GetAttribute("aria-label"));
    }

    // =====================================================================================
    // OnFilterDropdownOpenChange
    // =====================================================================================

    static RenderFragment TwoLoggingColumns(List<bool> nameLog, List<bool> ageLog, object receiver, bool ageFilterable = true) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, string>>(0);
        builder.AddAttribute(1, "Title", "Name");
        builder.AddAttribute(2, "Property", (Func<Person, string>)(x => x.Name));
        builder.AddAttribute(3, "FilterOptions", (IReadOnlyList<TableFilterOption>)NameOptions());
        builder.AddAttribute(4, "OnFilter", NameEquals);
        builder.AddAttribute(5, "OnFilterDropdownOpenChange", EventCallback.Factory.Create<bool>(receiver, v => nameLog.Add(v)));
        builder.CloseComponent();

        builder.OpenComponent<PropertyColumn<Person, int>>(6);
        builder.AddAttribute(7, "Title", "Age");
        builder.AddAttribute(8, "Property", (Func<Person, int>)(x => x.Age));
        builder.AddAttribute(9, "FilterOptions", ageFilterable ? (IReadOnlyList<TableFilterOption>)[new("Old", "old")] : null);
        builder.AddAttribute(10, "OnFilter", (Func<Person, string, bool>)((x, _) => x.Age > 25));
        builder.AddAttribute(11, "OnFilterDropdownOpenChange", EventCallback.Factory.Create<bool>(receiver, v => ageLog.Add(v)));
        builder.CloseComponent();
    };

    [Fact]
    public void OnFilterDropdownOpenChange_fires_once_per_transition_for_every_open_and_close_path()
    {
        var nameLog = new List<bool>();
        var ageLog = new List<bool>();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, TwoLoggingColumns(nameLog, ageLog, this)));
        var triggers = () => cut.FindAll(".wss-table-filter-trigger");

        triggers()[0].Click(); // funnel opens Name
        Assert.Equal([true], nameLog);
        Assert.Empty(ageLog);

        triggers()[1].Click(); // Age opening closes Name
        Assert.Equal([true, false], nameLog);
        Assert.Equal([true], ageLog);

        cut.Find(".wss-table-filter-ok").Click(); // OK closes Age
        Assert.Equal([true, false], ageLog);

        triggers()[1].Click();
        cut.Find(".wss-table-filter-reset").Click(); // Reset closes
        Assert.Equal([true, false, true, false], ageLog);

        triggers()[1].Click();
        cut.Find(".wss-table-filter-backdrop").Click(); // outside click closes
        Assert.Equal([true, false, true, false, true, false], ageLog);

        triggers()[1].Click();
        cut.Find(".wss-table-filter-dropdown").KeyDown("Escape"); // Escape closes
        Assert.Equal([true, false, true, false, true, false, true, false], ageLog);

        triggers()[1].Click();
        triggers()[1].Click(); // the funnel toggles closed
        Assert.Equal(10, ageLog.Count);
        Assert.False(ageLog[^1]);

        Assert.Equal([true, false], nameLog); // nothing above touched Name again
    }

    [Fact]
    public void OnFilterDropdownOpenChange_fires_false_when_an_open_column_stops_offering_a_filter()
    {
        var nameLog = new List<bool>();
        var ageLog = new List<bool>();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, TwoLoggingColumns(nameLog, ageLog, this)));
        cut.FindAll(".wss-table-filter-trigger")[1].Click();
        Assert.Equal([true], ageLog);

        cut.Render(p => p.Add(t => t.ChildContent, TwoLoggingColumns(nameLog, ageLog, this, ageFilterable: false)));

        Assert.Equal([true, false], ageLog);
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Single(cut.FindAll(".wss-table-filter-trigger")); // only Name's funnel is left

        // And a later re-render with the filter still gone raises nothing more.
        cut.Render(p => p.Add(t => t.ChildContent, TwoLoggingColumns(nameLog, ageLog, this, ageFilterable: false)));
        Assert.Equal([true, false], ageLog);
    }

    // =====================================================================================
    // FilterOnClose
    // =====================================================================================

    IRenderedComponent<Table<Person>> RenderOptionsFilterable(
        bool filterOnClose,
        Action<(Column<Person> Column, IReadOnlyList<string> Values)>? onFilterChanged = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, People());
            if (onFilterChanged is not null)
                p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, onFilterChanged));
            p.AddChildContent<PropertyColumn<Person, string>>(cp => cp
                .Add(c => c.Title, "Name")
                .Add(c => c.Property, x => x.Name)
                .Add(c => c.FilterOptions, NameOptions())
                .Add(c => c.OnFilter, NameEquals)
                .Add(c => c.FilterOnClose, filterOnClose));
        });

    [Fact]
    public void FilterOnClose_commits_the_staged_selection_on_an_outside_click()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderOptionsFilterable(filterOnClose: true, v => raised = v);

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-backdrop").Click();

        Assert.Equal(["Alice"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.NotNull(raised);
        Assert.Equal(["Alice"], raised.Value.Values);
    }

    [Fact]
    public void FilterOnClose_commits_the_staged_selection_on_Escape()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderOptionsFilterable(filterOnClose: true, v => raised = v);

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Bob");
        cut.Find(".wss-table-filter-dropdown").KeyDown("Escape");

        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal(["Bob"], raised!.Value.Values);

        // A dismissal that changes nothing is still a no-op notification-wise.
        raised = null;
        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-dropdown").KeyDown("Escape");
        Assert.Null(raised);
    }

    [Fact]
    public void Without_FilterOnClose_Escape_and_the_backdrop_still_discard()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        var cut = RenderOptionsFilterable(filterOnClose: false, v => raised = v);

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-dropdown").KeyDown("Escape");
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));

        cut.Find(".wss-table-filter-trigger").Click();
        CheckOption(cut, "Alice");
        cut.Find(".wss-table-filter-backdrop").Click();
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Null(raised);
    }
}
