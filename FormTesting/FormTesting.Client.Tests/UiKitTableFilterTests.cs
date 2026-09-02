using System.Reflection;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

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
        IReadOnlyList<TableFilterOption>? options = null,
        bool filterOnClose = false) => builder =>
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
        builder.AddAttribute(6, "FilterOnClose", filterOnClose);
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

    [Fact]
    public async Task FilterOnClose_is_ignored_for_a_Custom_column_so_a_dismissal_still_discards()
    {
        // AntD ignores filterOnClose under filterDropdown: the template owns confirm, and a backdrop
        // click must not commit whatever it had staged behind its back.
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        TableFilterContext<Person>? latest = null;
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v))
            .Add(t => t.ChildContent, CustomColumn(NameEquals, ctx => latest = ctx, filterOnClose: true)));

        cut.Find(".wss-table-filter-trigger").Click();
        await cut.InvokeAsync(() => latest!.SetSelectedValues(["Bob"]));
        cut.Find(".wss-table-filter-backdrop").Click();

        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Null(raised);
    }

    // =====================================================================================
    // Row placement (Table.FilterPlacement = Row)
    // =====================================================================================

    // A column with every filter-relevant knob exposed, so one builder covers the sortable /
    // headerless / Options / Text permutations the row tests need.
    static RenderFragment Col<TProp>(
        string? title,
        Func<Person, TProp> property,
        bool sortable = false,
        IReadOnlyList<TableFilterOption>? options = null,
        Func<Person, string, bool>? onFilter = null,
        Func<Person, string?>? filterText = null,
        bool filterMultiple = true) => builder =>
    {
        builder.OpenComponent<PropertyColumn<Person, TProp>>(0);
        builder.AddAttribute(1, "Title", title);
        builder.AddAttribute(2, "Property", property);
        builder.AddAttribute(3, "Sortable", sortable);
        builder.AddAttribute(4, "FilterOptions", options);
        builder.AddAttribute(5, "OnFilter", onFilter);
        builder.AddAttribute(6, "FilterText", filterText);
        builder.AddAttribute(7, "FilterMultiple", filterMultiple);
        builder.CloseComponent();
    };

    // Each part in its own region (AddContent), so their sequence numbers cannot collide.
    static RenderFragment Columns(params RenderFragment[] parts) => builder =>
    {
        for (var i = 0; i < parts.Length; i++) builder.AddContent(i, parts[i]);
    };

    static IReadOnlyList<TableFilterOption> AgeOptions() => [new("Old", "old"), new("Young", "young")];
    static Func<Person, string, bool> AgeBand => (x, v) => v == "old" ? x.Age > 25 : x.Age <= 25;

    // Name column only (default), or every column's first cell -- RenderedNames assumes one column.
    static string[] FirstCellNames(IRenderedComponent<Table<Person>> cut) =>
        cut.FindAll("tbody .wss-table-row:not(.wss-table-placeholder)")
            .Select(tr => tr.QuerySelector("td")!.TextContent.Trim()).ToArray();

    IRenderedComponent<Table<Person>> RenderRowPlacement(
        RenderFragment columns,
        Action<ComponentParameterCollectionBuilder<Table<Person>>>? configure = null) =>
        Render<Table<Person>>(p =>
        {
            p.Add(t => t.DataSource, People());
            p.Add(t => t.FilterPlacement, TableFilterPlacement.Row);
            p.Add(t => t.ChildContent, columns);
            configure?.Invoke(p);
        });

    static IElement RowTextInput(IRenderedComponent<Table<Person>> cut) =>
        cut.Find(".wss-table-filter-row input.wss-table-filter-input");

    // Find + dispatch on the renderer's dispatcher, so no render can slip in between them: the row
    // editor's @oninput lambda gets a fresh handler id on every render, and a Find on one line with
    // the dispatch on the next raced a pending render under the parallel full-suite run (bUnit's
    // UnknownEventHandlerIdException). The returned task is the handler's own -- it stays pending
    // while a debounce counts down.
    static Task TypeInRowAsync(IRenderedComponent<Table<Person>> cut, string text) =>
        cut.InvokeAsync(() => RowTextInput(cut).InputAsync(new ChangeEventArgs { Value = text }));

    static Task PressEnterInRowAsync(IRenderedComponent<Table<Person>> cut) =>
        cut.InvokeAsync(() => RowTextInput(cut).KeyDownAsync(new KeyboardEventArgs { Key = "Enter" }));

    [Fact]
    public void Row_placement_renders_a_filter_row_with_one_cell_per_column_and_no_funnels()
    {
        // Name: sortable + Options; Age: Text, not sortable; Note: nothing. Every header <th> must take
        // its plain non-filterable shape (the sortable one its exact single-button DOM), the editors
        // move to the second row, and the unfilterable column still gets a (blank) cell.
        var cut = RenderRowPlacement(Columns(
            Col("Name", x => x.Name, sortable: true, options: NameOptions(), onFilter: NameEquals),
            Col("Age", x => x.Age, filterText: ByAge),
            Col("Note", x => "-")));

        Assert.Contains("wss-table-has-filter-row", cut.Find("table.wss-table").ClassList);
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Empty(cut.FindAll(".wss-table-header-inner"));

        var rows = cut.FindAll("thead tr");
        Assert.Equal(2, rows.Count);
        Assert.Equal("wss-table-filter-row", rows[1].ClassName);

        var headers = rows[0].QuerySelectorAll("th");
        Assert.Equal(3, headers.Length);
        var sortButton = Assert.Single(headers[0].Children);
        Assert.Equal("wss-table-sort-trigger", sortButton.ClassName); // no header-inner wrapper, no funnel
        Assert.Single(headers[1].ChildNodes);                          // bare text-only header cell
        Assert.Equal("Age", headers[1].TextContent);

        var cells = rows[1].QuerySelectorAll("td");
        Assert.Equal(3, cells.Length);
        Assert.All(cells, td => Assert.Equal("wss-table-cell wss-table-filter-row-cell", td.ClassName));
        Assert.NotNull(cells[0].QuerySelector(".wss-table-filter-editor > .wss-select"));
        Assert.NotNull(cells[1].QuerySelector(".wss-table-filter-editor > input.wss-table-filter-input"));
        Assert.Empty(cells[2].Children);
        Assert.Empty(cells[1].QuerySelectorAll(".wss-table-filter-text")); // the dropdown's gutter wrapper stays there

        var editors = cut.FindComponents<TableFilterEditor<Person>>();
        Assert.Equal(2, editors.Count);
        Assert.All(editors, e => Assert.Equal(TableFilterPlacement.Row, e.Instance.Placement));

        // Named per column with the row format; the Select through its combobox input.
        Assert.Equal("Filter by Name", cells[0].QuerySelector("input.wss-select-selection-search-input")!.GetAttribute("aria-label"));
        Assert.Equal("Filter by Age", cells[1].QuerySelector("input")!.GetAttribute("aria-label"));
    }

    [Fact]
    public void Row_placement_adds_blank_leading_cells_for_the_expand_and_selection_columns()
    {
        var cut = RenderRowPlacement(
            Columns(Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals), Col("Age", x => x.Age)),
            p => p.Add(t => t.Selectable, true)
                  .Add(t => t.RowDetail, (Person x) => b => b.AddContent(0, x.Name)));

        var headerCount = cut.FindAll("thead tr:first-child th").Count;
        var cells = cut.FindAll("thead tr.wss-table-filter-row td");
        Assert.Equal(4, headerCount);
        Assert.Equal(4, cells.Count);
        Assert.Empty(cells[0].Children); // expand
        Assert.Empty(cells[1].Children); // selection
        Assert.NotNull(cells[2].QuerySelector(".wss-table-filter-editor"));
        Assert.Empty(cells[3].Children);
    }

    [Fact]
    public void Dropdown_placement_renders_no_filter_row_and_the_funnel_header_is_unchanged()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, Col("Name", x => x.Name, sortable: true, options: NameOptions(), onFilter: NameEquals)));

        Assert.Empty(cut.FindAll(".wss-table-filter-row"));
        Assert.Empty(cut.FindAll(".wss-table-filter-row-cell"));
        Assert.DoesNotContain("wss-table-has-filter-row", cut.Find("table.wss-table").ClassList);
        Assert.Single(cut.FindAll("thead tr"));
        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Single(cut.FindAll("thead th > .wss-table-header-inner"));
        Assert.Empty(cut.FindComponents<TableFilterEditor<Person>>()); // only rendered while a dropdown is open
    }

    [Fact]
    public void FilterRowLabelFormat_names_the_row_editors_and_a_headerless_column_falls_back_to_FilterLabel()
    {
        var cut = RenderRowPlacement(
            Columns(Col("Name", x => x.Name, filterText: ByName), Col<string>(null, x => x.Name, filterText: ByName)),
            p => p.Add(t => t.FilterRowLabelFormat, "Filtrar por {0}").Add(t => t.FilterLabel, "Filtrar"));

        var inputs = cut.FindAll(".wss-table-filter-row input.wss-table-filter-input");
        Assert.Equal("Filtrar por Name", inputs[0].GetAttribute("aria-label"));
        Assert.Equal("Filtrar", inputs[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void Options_row_editor_is_a_multiple_Select_that_narrows_on_pick_and_clears_on_AllowClear()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var select = cut.Find(".wss-table-filter-row .wss-select");
        Assert.Contains("wss-select-multiple", select.ClassList);
        Assert.Contains("wss-select-sm", select.ClassList);

        select.Click(); // open
        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains("Bob")).Click();

        Assert.Equal(["Bob"], RenderedNames(cut)); // committed on the spot -- no OK in this placement
        Assert.Equal([["Bob"]], raised);
        Assert.True(Get<bool>(FilterOf(cut)!, "IsActive"));

        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains("Carol")).Click();
        Assert.Equal(["Bob", "Carol"], RenderedNames(cut));
        Assert.Equal(["Bob", "Carol"], raised[^1]);

        cut.Find(".wss-table-filter-row button.wss-select-clear").Click();
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(raised[^1]);
        Assert.Equal(3, raised.Count);
    }

    [Fact]
    public void Options_row_editor_with_FilterMultiple_false_is_a_single_Select_where_a_pick_replaces()
    {
        var cut = RenderRowPlacement(Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals, filterMultiple: false));

        var select = cut.Find(".wss-table-filter-row .wss-select");
        Assert.Contains("wss-select-single", select.ClassList);

        select.Click();
        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains("Bob")).Click();
        Assert.Equal(["Bob"], RenderedNames(cut));

        cut.Find(".wss-table-filter-row .wss-select").Click();
        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains("Carol")).Click();
        Assert.Equal(["Carol"], RenderedNames(cut));
        Assert.Equal(["Carol"], Get<IReadOnlyList<string>>(FilterOf(cut)!, "AppliedValues"));
    }

    [Fact]
    public async Task Text_row_editor_with_a_zero_debounce_narrows_on_every_input()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, filterText: ByName),
            p => p.Add(t => t.FilterDebounceMilliseconds, 0)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        await TypeInRowAsync(cut, "o");
        Assert.Equal(["Bob", "Carol"], RenderedNames(cut));
        await TypeInRowAsync(cut, "ob");
        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Equal([["o"], ["ob"]], raised);
        Assert.Single(cut.FindAll(".wss-table-filter-row .wss-table-filter-input-clear"));
    }

    [Fact]
    public async Task Text_row_editor_Enter_commits_at_once_while_a_debounce_is_still_counting_down()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, filterText: ByName),
            p => p.Add(t => t.FilterDebounceMilliseconds, 5000)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var pending = TypeInRowAsync(cut, "bob");
        Assert.False(pending.IsCompleted);                          // the debounce is in flight
        cut.WaitForAssertion(() => Assert.Equal("bob", RowTextInput(cut).GetAttribute("value"))); // staged
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut)); // pending only
        Assert.Empty(raised);

        await PressEnterInRowAsync(cut);

        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Equal([["bob"]], raised);
        await pending; // cancelled, and it must not commit a second time
        Assert.Single(raised);
    }

    [Fact]
    public async Task Text_row_editor_clear_button_clears_and_commits_at_once()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, filterText: ByName),
            p => p.Add(t => t.FilterDebounceMilliseconds, 5000)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var pending = TypeInRowAsync(cut, "bob");
        await PressEnterInRowAsync(cut);
        await pending;
        Assert.Equal(["Bob"], RenderedNames(cut));

        Assert.Equal("Clear", cut.Find(".wss-table-filter-row .wss-table-filter-input-clear").GetAttribute("aria-label"));
        await cut.InvokeAsync(() => cut.Find(".wss-table-filter-row .wss-table-filter-input-clear").ClickAsync(new MouseEventArgs()));

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-row .wss-table-filter-input-clear"));
        Assert.False(RowTextInput(cut).HasAttribute("value"));
        Assert.Equal([["bob"], []], raised);
    }

    [Fact]
    public async Task Text_row_editor_debounce_coalesces_a_burst_of_keystrokes_into_one_commit()
    {
        // The window is deliberately wide: the three dispatches below are back-to-back synchronous
        // calls, but the suite runs classes in parallel, and a 30ms window once let the first
        // keystroke's countdown expire before the second was dispatched on a loaded machine.
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, filterText: ByName),
            p => p.Add(t => t.FilterDebounceMilliseconds, 500)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var t1 = TypeInRowAsync(cut, "b");
        var t2 = TypeInRowAsync(cut, "bo");
        var t3 = TypeInRowAsync(cut, "bob");
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut)); // nothing committed yet
        Assert.Empty(raised);
        await Task.WhenAll(t1, t2, t3);

        cut.WaitForAssertion(() => Assert.Equal(["Bob"], RenderedNames(cut)), TimeSpan.FromSeconds(5));
        Assert.Equal([["bob"]], raised); // the first two keystrokes never committed
    }

    [Fact]
    public async Task Disposing_the_row_editor_mid_debounce_neither_throws_nor_applies()
    {
        var raised = new List<IReadOnlyList<string>>();
        var cut = RenderRowPlacement(
            Col("Name", x => x.Name, filterText: ByName),
            p => p.Add(t => t.FilterDebounceMilliseconds, 50)
                  .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised.Add(v.Item2))));

        var pending = TypeInRowAsync(cut, "bob");
        Assert.False(pending.IsCompleted);

        // Switching back to Dropdown removes the filter row, disposing the editor with its countdown.
        cut.Render(p => p.Add(t => t.FilterPlacement, TableFilterPlacement.Dropdown));
        Assert.Empty(cut.FindAll(".wss-table-filter-row"));
        Assert.Single(cut.FindAll(".wss-table-filter-trigger"));

        await pending;              // cancelled, not faulted
        await Task.Delay(150);      // well past the original deadline

        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(raised);
        Assert.False(Get<bool>(FilterOf(cut)!, "IsActive"));
    }

    [Fact]
    public async Task Loading_disables_every_row_editor()
    {
        var cut = RenderRowPlacement(
            Columns(
                Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals),
                Col("Age", x => x.Age, filterText: ByAge)),
            p => p.Add(t => t.FilterDebounceMilliseconds, 0));

        await TypeInRowAsync(cut, "3"); // so the clear button is rendered too
        Assert.Equal(["Alice"], FirstCellNames(cut));
        Assert.False(RowTextInput(cut).HasAttribute("disabled"));
        Assert.DoesNotContain("wss-select-disabled", cut.Find(".wss-table-filter-row .wss-select").ClassList);

        cut.Render(p => p.Add(t => t.Loading, true));

        Assert.True(RowTextInput(cut).HasAttribute("disabled"));
        Assert.True(cut.Find(".wss-table-filter-row .wss-table-filter-input-clear").HasAttribute("disabled"));
        var select = cut.Find(".wss-table-filter-row .wss-select");
        Assert.Contains("wss-select-disabled", select.ClassList);
        Assert.True(select.QuerySelector("input")!.HasAttribute("disabled"));
        Assert.Equal(["Alice"], FirstCellNames(cut)); // the applied filter itself is untouched

        cut.Render(p => p.Add(t => t.Loading, false));
        Assert.False(RowTextInput(cut).HasAttribute("disabled"));
    }

    [Fact]
    public async Task Custom_template_renders_in_the_row_cell_and_ConfirmAsync_narrows()
    {
        (Column<Person> Column, IReadOnlyList<string> Values)? raised = null;
        TableFilterContext<Person>? latest = null;
        var cut = RenderRowPlacement(
            CustomColumn(NameEquals, ctx => latest = ctx, NameOptions()),
            p => p.Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => raised = v)));

        var cell = cut.Find(".wss-table-filter-row-cell");
        Assert.NotNull(cell.QuerySelector(".wss-table-filter-editor > .custom-panel"));
        Assert.Empty(cut.FindAll(".wss-table-filter-trigger"));
        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown"));
        Assert.False(latest!.IsOpen);
        Assert.Equal(["Alice", "Bob", "Carol"], latest.Options!.Select(o => o.Value));

        await cut.InvokeAsync(() => latest!.SetSelectedValues(["Bob"]));
        Assert.Equal("Bob", cut.Find(".custom-panel").TextContent); // staged, template re-rendered
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));

        await cut.InvokeAsync(() => latest!.ConfirmAsync());
        Assert.Equal(["Bob"], RenderedNames(cut));
        Assert.Equal(["Bob"], raised!.Value.Values);

        // Close is inert here -- nothing is open, and the template stays put.
        await cut.InvokeAsync(() => latest!.Close());
        Assert.NotNull(cut.Find(".wss-table-filter-row-cell .custom-panel"));
        Assert.Equal(["Bob"], RenderedNames(cut));

        await cut.InvokeAsync(() => latest!.ResetAsync());
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Empty(raised.Value.Values);
    }

    // =====================================================================================
    // ClearFiltersAsync / OnFiltersChanged / result announcement
    // =====================================================================================

    // Name + Age Options columns with both events logged into one list, so relative order is visible:
    // "Name:Alice" / "Age:" for the per-column event, "all:2" for the aggregate.
    (IRenderedComponent<Table<Person>> Cut, List<string> Log, List<IReadOnlyList<TableColumnFilterSnapshot<Person>>> Snapshots) RenderTwoFilterable(TableFilterPlacement placement = TableFilterPlacement.Dropdown)
    {
        var log = new List<string>();
        var snapshots = new List<IReadOnlyList<TableColumnFilterSnapshot<Person>>>();
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.FilterPlacement, placement)
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this,
                v => log.Add($"{v.Item1.HeaderText}:{string.Join("+", v.Item2)}")))
            .Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Person>>>(this,
                s => { log.Add($"all:{s.Count}"); snapshots.Add(s); }))
            .Add(t => t.ChildContent, Columns(
                Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals),
                Col("Age", x => x.Age, options: AgeOptions(), onFilter: AgeBand))));
        return (cut, log, snapshots);
    }

    static void ApplyViaDropdown(IRenderedComponent<Table<Person>> cut, int column, string optionText)
    {
        cut.FindAll(".wss-table-filter-trigger")[column].Click();
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains(optionText)).QuerySelector("input")!.Change(true);
        cut.Find(".wss-table-filter-ok").Click();
    }

    [Fact]
    public void OnFiltersChanged_follows_every_OnFilterChanged_with_the_active_columns_in_column_order()
    {
        var (cut, log, snapshots) = RenderTwoFilterable();

        ApplyViaDropdown(cut, 1, "Old"); // Age first: Alice(30), Carol(40)
        Assert.Equal(["Age:old", "all:1"], log);

        ApplyViaDropdown(cut, 0, "Alice");
        Assert.Equal(["Age:old", "all:1", "Name:Alice", "all:2"], log);
        Assert.Equal(["Alice"], FirstCellNames(cut));

        var latest = snapshots[^1];
        Assert.Equal(2, latest.Count);
        Assert.Equal("Name", latest[0].Column.HeaderText); // column order, not apply order
        Assert.Equal(TableFilterKind.Options, latest[0].Kind);
        Assert.Equal(["Alice"], latest[0].Values);
        Assert.Equal("1 selected", latest[0].Description);
        Assert.Equal("Age", latest[1].Column.HeaderText);
        Assert.Equal(["old"], latest[1].Values);

        // A no-op OK raises neither.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(4, log.Count);

        // Reset drops Name out of the aggregate.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.Find(".wss-table-filter-reset").Click();
        Assert.Equal(["Name:", "all:1"], log.Skip(4));
        Assert.Equal("Age", Assert.Single(snapshots[^1]).Column.HeaderText);
    }

    [Fact]
    public void OnFiltersChanged_also_follows_a_forced_clear_when_a_filtered_column_leaves_the_table()
    {
        var log = new List<string>();
        RenderFragment columns(bool withAge) => Columns(
            Col("Name", x => x.Name, options: NameOptions(), onFilter: NameEquals),
            withAge ? Col("Age", x => x.Age, options: AgeOptions(), onFilter: AgeBand) : (b => { }));
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.OnFilterChanged, EventCallback.Factory.Create<(Column<Person>, IReadOnlyList<string>)>(this, v => log.Add($"{v.Item1.HeaderText}:{string.Join("+", v.Item2)}")))
            .Add(t => t.OnFiltersChanged, EventCallback.Factory.Create<IReadOnlyList<TableColumnFilterSnapshot<Person>>>(this, s => log.Add($"all:{s.Count}")))
            .Add(t => t.ChildContent, columns(true)));
        ApplyViaDropdown(cut, 0, "Alice");
        ApplyViaDropdown(cut, 1, "Old");
        log.Clear();

        cut.Render(p => p.Add(t => t.ChildContent, columns(false)));

        Assert.Equal(["Age:", "all:1"], log); // Name is still active, Age is gone
        Assert.Equal(["Alice"], FirstCellNames(cut));
    }

    [Fact]
    public async Task ClearFiltersAsync_clears_every_active_column_then_raises_per_column_and_once_in_aggregate()
    {
        var (cut, log, snapshots) = RenderTwoFilterable();
        ApplyViaDropdown(cut, 0, "Bob");
        ApplyViaDropdown(cut, 1, "Young");
        Assert.Equal(["Bob"], FirstCellNames(cut));
        log.Clear();

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());

        Assert.Equal(["Name:", "Age:", "all:0"], log); // per column in column order, then ONE aggregate
        Assert.Empty(snapshots[^1]);
        Assert.Equal(["Alice", "Bob", "Carol"], FirstCellNames(cut));
        Assert.Empty(cut.FindAll(".wss-table-filter-active"));

        // Nothing left to clear: a second call raises nothing at all.
        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());
        Assert.Equal(3, log.Count);
    }

    [Fact]
    public async Task ClearFiltersAsync_resets_to_page_1_and_discards_a_merely_staged_selection_without_raising()
    {
        var (cut, log, _) = RenderTwoFilterable();
        cut.Render(p => p.Add(t => t.PageSize, 1));
        cut.FindAll(".wss-pagination-item")[^1].Click(); // page 3 of 3
        ApplyViaDropdown(cut, 1, "Old");                 // Alice, Carol -> page resets to 1 anyway
        cut.FindAll(".wss-pagination-item")[^1].Click(); // page 2 of 2: Carol
        Assert.Equal(["Carol"], FirstCellNames(cut));
        log.Clear();

        // Stage (don't apply) something on Name and leave its dropdown open.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains("Bob")).QuerySelector("input")!.Change(true);
        Assert.Single(cut.FindAll(".wss-table-filter-dropdown"));

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());

        Assert.Empty(cut.FindAll(".wss-table-filter-dropdown")); // the open dropdown closed
        Assert.Equal(["Alice"], FirstCellNames(cut));            // page 1 of the unfiltered set
        Assert.Equal(["Age:", "all:0"], log);                    // Name never applied anything, so it is silent

        // The staged Bob did not survive: reopening shows nothing ticked.
        cut.FindAll(".wss-table-filter-trigger")[0].Click();
        Assert.All(cut.FindAll(".wss-table-filter-checkbox"), cb => Assert.False(cb.HasAttribute("checked")));
    }

    [Fact]
    public void ClearFiltersAsync_is_a_no_op_with_nothing_applied_or_staged()
    {
        var (cut, log, snapshots) = RenderTwoFilterable();

        cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync()).GetAwaiter().GetResult();

        Assert.Empty(log);
        Assert.Empty(snapshots);
        Assert.Equal(["Alice", "Bob", "Carol"], FirstCellNames(cut));
    }

    static string StatusText(IRenderedComponent<Table<Person>> cut) =>
        cut.Find("div.wss-sr-only[role='status']").TextContent.Trim();

    static void UncheckOption(IRenderedComponent<Table<Person>> cut, string text) =>
        cut.FindAll(".wss-table-filter-item").First(li => li.TextContent.Contains(text))
            .QuerySelector("input")!.Change(false);

    [Fact]
    public void The_status_region_announces_the_matching_row_count_after_a_narrowing_filter()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.ChildContent, NameColumn(NameOptions(), NameEquals)));
        Assert.Equal(string.Empty, StatusText(cut)); // nothing announced until a filter actually changes

        ApplyFilter(cut, "Alice", "Carol");
        Assert.Equal("2 matching rows", StatusText(cut));

        // A no-op OK changes nothing, so the text (and therefore the announcement) stays as it was.
        cut.Find(".wss-table-filter-trigger").Click();
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal("2 matching rows", StatusText(cut));

        // Un-ticking everything is a real change too, and the full count is what it leaves.
        cut.Find(".wss-table-filter-trigger").Click();
        UncheckOption(cut, "Alice");
        UncheckOption(cut, "Carol");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal(["Alice", "Bob", "Carol"], RenderedNames(cut));
        Assert.Equal("3 matching rows", StatusText(cut));

        // Loading wins over the count, and a DataSource swap forgets it.
        cut.Render(p => p.Add(t => t.Loading, true));
        Assert.Equal("Loading", StatusText(cut));
        cut.Render(p => p.Add(t => t.Loading, false).Add(t => t.DataSource, People()));
        Assert.Equal(string.Empty, StatusText(cut));
    }

    [Fact]
    public void FilterResultAnnouncementFormat_is_overridable_and_the_empty_state_still_wins()
    {
        var cut = Render<Table<Person>>(p => p
            .Add(t => t.DataSource, People())
            .Add(t => t.FilterResultAnnouncementFormat, "{0} Treffer")
            .Add(t => t.EmptyText, "Nichts")
            .Add(t => t.ChildContent, TextColumn(ByName)));

        OpenAndType(cut, "o");
        cut.Find(".wss-table-filter-ok").Click();
        Assert.Equal("2 Treffer", StatusText(cut));

        OpenAndType(cut, "zzz");
        cut.Find(".wss-table-filter-ok").Click();
        AssertNoDataRows(cut);
        Assert.Equal("Nichts", StatusText(cut));
    }

    [Fact]
    public async Task Row_placement_commits_announce_the_count_and_raise_the_aggregate_too()
    {
        var (cut, log, _) = RenderTwoFilterable(TableFilterPlacement.Row);

        cut.FindAll(".wss-table-filter-row .wss-select")[1].Click();
        cut.FindAll(".wss-select-item-option").First(o => o.TextContent.Contains("Young")).Click();

        Assert.Equal(["Bob"], FirstCellNames(cut));
        Assert.Equal(["Age:young", "all:1"], log);
        Assert.Equal("1 matching rows", cut.FindAll("div.wss-sr-only[role='status']")[0].TextContent.Trim()); // the Table's region, not a Select's

        await cut.InvokeAsync(() => cut.Instance.ClearFiltersAsync());
        Assert.Equal(["Age:young", "all:1", "Age:", "all:0"], log);
        Assert.Equal(["Alice", "Bob", "Carol"], FirstCellNames(cut));
        // The Select mirrors the cleared pending set on the next render.
        Assert.Empty(cut.FindAll(".wss-table-filter-row .wss-select-selection-item"));
    }
}
