using Microsoft.AspNetCore.Components.Rendering;

namespace Controls;

/// <summary>
/// A table column with a custom cell template. Declared as a child of
/// <see cref="Table{TItem}"/>; it registers itself and renders no markup of its own.
/// </summary>
public class Column<TItem> : ComponentBase, IDisposable
{
    [CascadingParameter] public Table<TItem>? Table { get; set; }

    [Parameter] public string? Title { get; set; }

    /// <summary>Optional header template rendered instead of <see cref="Title"/> — e.g. a title
    /// with a trailing info-tooltip icon. On a sortable column the template renders in its own
    /// clickable area next to the sort button, never inside it (nesting the template's own
    /// interactive content — e.g. a LabelTooltip's <c>&lt;button&gt;</c> — inside the sort trigger
    /// would be invalid HTML and let its clicks bubble into a sort toggle). That leaves the button
    /// with no visible content of its own, so keep <see cref="Title"/> set too: screen readers name
    /// the sort button from it (falling back to "Sort" when Title is also unset).</summary>
    [Parameter] public RenderFragment? TitleContent { get; set; }

    /// <summary>Cell template; receives the row item as context.</summary>
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }

    /// <summary>
    /// Optional comparison that makes this column sortable. Supply one for a custom
    /// (template) column; <see cref="PropertyColumn{TItem,TProp}"/> derives one from its
    /// property when <c>Sortable</c> is set, so this is only needed to override it.
    /// </summary>
    [Parameter] public Comparison<TItem>? SortBy { get; set; }

    /// <summary>
    /// Truncates overflowing cell text with an ellipsis instead of wrapping/overflowing (CSS-only —
    /// the Table adds <c>wss-table-cell-ellipsis</c> to the cell and switches the whole table to
    /// <c>table-layout: fixed</c> when any column requests it, since truncation needs a bounded
    /// column width). <see cref="PropertyColumn{TItem,TProp}"/> additionally sets a <c>title</c>
    /// attribute with the full computed text so it stays discoverable on hover; a plain
    /// <see cref="Column{TItem}"/>/<see cref="ActionColumn{TItem}"/>'s <see cref="ChildContent"/> is
    /// arbitrary markup, not a string this base class computed, so it gets the truncation styling
    /// only, no <c>title</c>.
    /// </summary>
    [Parameter] public bool Ellipsis { get; set; }

    /// <summary>
    /// Filter dropdown options for this column (AntD 4.x's <c>filters</c>); null (default) renders
    /// no filter UI. Paired with <see cref="OnFilter"/> it declares the Options kind — a checkbox or
    /// radio list — see <see cref="CanFilter"/>. Under a <see cref="FilterDropdown"/> template it is
    /// instead handed to the template as <see cref="TableFilterContext{TItem}.Options"/>.
    /// <para>This list is also the Options kind's key space: a <see cref="DefaultFilterValues"/>
    /// entry with no matching option is dropped, and an applied value an updated list no longer
    /// offers is pruned (raising <see cref="Table{TItem}.OnFilterChanged"/> with the survivors).</para>
    /// </summary>
    [Parameter] public IReadOnlyList<TableFilterOption>? FilterOptions { get; set; }

    /// <summary>
    /// Row-inclusion predicate given one currently-selected filter value (key). A row passes this
    /// column's filter when this returns true for ANY selected value (AntD's OR-within-a-column
    /// semantics; the Table ANDs each filterable column's result together). Required (with
    /// <see cref="FilterOptions"/>) for the Options kind — see <see cref="CanFilter"/>. The
    /// <see cref="FilterDropdown"/> (Custom) kind narrows rows through it too, and without it merely
    /// stops excluding rows. Not consulted by <see cref="FilterText"/>.
    /// </summary>
    [Parameter] public Func<TItem, string, bool>? OnFilter { get; set; }

    /// <summary>
    /// true (default) renders the filter dropdown as a checkbox list (any number of values may be
    /// selected — AntD's default). false renders a single-select radio list instead (AntD's
    /// <c>filterMultiple={false}</c>) — picking one option replaces any other.
    /// </summary>
    [Parameter] public bool FilterMultiple { get; set; } = true;

    /// <summary>
    /// Enables a free-text filter over this accessor (kind <see cref="TableFilterKind.Text"/>): the
    /// dropdown holds a search box, and a row passes when the accessor's text matches the applied
    /// text per <see cref="TextFilterMatch"/> — case-insensitive; a null accessor result never
    /// matches; whitespace-only input filters nothing. Ignored while <see cref="FilterDropdown"/> or
    /// <see cref="FilterOptions"/>+<see cref="OnFilter"/> is set (those kinds win — see
    /// <see cref="CanFilter"/>). The <see cref="Table{TItem}.OnFilterChanged"/> payload is the single
    /// trimmed text, or empty when cleared.
    /// </summary>
    [Parameter] public Func<TItem, string?>? FilterText { get; set; }

    /// <summary>How a <see cref="FilterText"/> filter compares the typed text with a row's:
    /// <see cref="Controls.TextFilterMatch.Contains"/> (default), <c>StartsWith</c> or <c>Equals</c>,
    /// all ordinal-ignore-case. Changing it while a text filter is applied re-derives the rows.</summary>
    [Parameter] public TextFilterMatch TextFilterMatch { get; set; } = TextFilterMatch.Contains;

    /// <summary>
    /// AntD's <c>filterDropdown</c>: a template that renders the WHOLE dropdown panel in place of the
    /// built-in option list and OK/Reset footer (kind <see cref="TableFilterKind.Custom"/>; wins over
    /// every other filter parameter). The template receives a <see cref="TableFilterContext{TItem}"/>
    /// to stage string keys, commit, clear and close. Rows are narrowed through <see cref="OnFilter"/>
    /// with the same OR-within-a-column semantics as Options; with no <see cref="OnFilter"/> the
    /// funnel, the applied state and <see cref="Table{TItem}.OnFilterChanged"/> all still work and no
    /// row is excluded — the shape for filtering server-side. <see cref="FilterOptions"/>, if set, is
    /// handed to the template as <see cref="TableFilterContext{TItem}.Options"/>.
    /// </summary>
    [Parameter] public RenderFragment<TableFilterContext<TItem>>? FilterDropdown { get; set; }

    /// <summary>AntD's <c>filterIcon</c>: replaces the funnel glyph inside the filter trigger button;
    /// the context is whether a filter is currently applied. The button itself — its classes,
    /// accessible name and behaviour — is unchanged. Null (default) keeps the funnel.</summary>
    [Parameter] public RenderFragment<bool>? FilterIcon { get; set; }

    /// <summary>AntD's <c>onFilterDropdownOpenChange</c>: raised with true when this column's filter
    /// dropdown opens and false when it closes — once per actual transition, whatever caused it (the
    /// funnel, OK, Reset, Escape, an outside click, another column's dropdown opening, or the column
    /// ceasing to offer a filter).</summary>
    [Parameter] public EventCallback<bool> OnFilterDropdownOpenChange { get; set; }

    /// <summary>AntD's <c>filterOnClose</c>: when true, dismissing the dropdown (an outside click,
    /// Escape, or clicking the funnel again) COMMITS the staged edits exactly as OK would, instead of
    /// discarding them. Default false — only OK applies. Ignored for a <see cref="FilterDropdown"/>
    /// column (as AntD ignores it under <c>filterDropdown</c>): the template owns confirm, so a
    /// dismissal there always discards. Not consulted under
    /// <see cref="TableFilterPlacement.Row"/>, where nothing opens or closes.</summary>
    [Parameter] public bool FilterOnClose { get; set; }

    /// <summary>
    /// AntD's <c>filterSearch</c>: puts a search box above this column's filter dropdown option list,
    /// narrowing the rendered options to the ones whose <see cref="TableFilterOption.Text"/> contains
    /// what is typed (ordinal-ignore-case). <see cref="TableFilterKind.Options"/> columns in
    /// <see cref="TableFilterPlacement.Dropdown"/> placement only — a
    /// <see cref="TableFilterPlacement.Row"/> editor is a <see cref="Select{TValue}"/>, which brings
    /// its own search. Default false.
    /// <para>The query is panel-local UI state, not filter state: it starts empty on every open, and
    /// hiding an option leaves whatever it had staged alone, so OK still commits the whole staged set
    /// (including ticks the current query hides). The placeholder/accessible name comes from
    /// <see cref="Table{TItem}.FilterSearchPlaceholder"/> and the no-match row from
    /// <see cref="Table{TItem}.FilterEmptyText"/>.</para>
    /// </summary>
    [Parameter] public bool FilterSearch { get; set; }

    /// <summary>
    /// Adds a "select all" checkbox row at the top of this column's filter dropdown option list,
    /// labelled <see cref="Table{TItem}.FilterCheckAllLabel"/>. Multi-select
    /// <see cref="TableFilterKind.Options"/> columns (<see cref="FilterMultiple"/>) in
    /// <see cref="TableFilterPlacement.Dropdown"/> placement only. Default false.
    /// <para>It toggles exactly the options currently VISIBLE, so an active <see cref="FilterSearch"/>
    /// query scopes it to what the user can see; it renders checked when all of them are staged,
    /// unchecked when none, and in the native "mixed" state when some (which needs one JS round trip,
    /// like the table's own select-all — with no JS runtime it simply reads checked/unchecked). It is
    /// not rendered while a search query hides every option: there is nothing to select.</para>
    /// </summary>
    [Parameter] public bool FilterCheckAll { get; set; }

    /// <summary>
    /// AntD's <c>defaultFilteredValue</c>: the filter this column starts out with, in the same
    /// serialized shape <see cref="Table{TItem}.OnFilterChanged"/> publishes (Options/Custom: the
    /// selected keys; Text: the single text; NumberRange/DateRange: <c>[min, max]</c> with <c>""</c>
    /// for an unset bound; Bool: <c>["true"]</c>/<c>["false"]</c>). Works for every kind.
    /// <para>Applied ONCE, on the first parameter pass that actually produces a filter state, and
    /// never again — not on a later parameter pass, not on a kind change, and not after the user
    /// clears it. It raises no <see cref="Table{TItem}.OnFilterChanged"/> or
    /// <see cref="Table{TItem}.OnFiltersChanged"/>: it is the consumer's own initial state, not a
    /// change to report back to them. Values this kind cannot interpret are dropped (an Options key
    /// with no matching <see cref="FilterOptions"/> entry, an unparseable bound), leaving whatever
    /// survives applied.</para>
    /// <para>Uncontrolled, like every other filter parameter: changing it afterwards does nothing.
    /// Set <see cref="FilterResetToDefault"/> to make Reset return here instead of clearing.</para>
    /// </summary>
    [Parameter] public IEnumerable<string>? DefaultFilterValues { get; set; }

    /// <summary>
    /// AntD's <c>filterResetToDefaultFilteredValue</c>: with <see cref="DefaultFilterValues"/> set,
    /// the dropdown's Reset button and <see cref="Table{TItem}.ClearFiltersAsync"/> restore that
    /// default instead of clearing this column. Default false (Reset clears). Ignored with no
    /// <see cref="DefaultFilterValues"/>.
    /// <para>The "real change only" rule is unchanged: resetting a column that is already at its
    /// default applies nothing, so it neither resets the page nor raises an event.</para>
    /// </summary>
    [Parameter] public bool FilterResetToDefault { get; set; }

    /// <summary>Whether the Table should render a filter control on this column's header — true once
    /// the column's parameters declare a filter kind (see <see cref="ExplicitFilterKind"/>, first
    /// match wins: <see cref="FilterDropdown"/>; <see cref="FilterOptions"/> with
    /// <see cref="OnFilter"/>; <see cref="FilterText"/>), or a derived column type infers one from
    /// its own (<see cref="DerivedFilterKind"/> — see
    /// <see cref="PropertyColumn{TItem,TProp}.Filterable"/>).</summary>
    public bool CanFilter => Filter is not null;

    /// <summary>
    /// This column's filter state — the applied selection (what actually narrows the rows), the open
    /// dropdown's staged edits, and the open flag — or null while the column offers no filter. Built
    /// by <see cref="OnParametersSet"/> from the declared parameters and kept (the SAME instance)
    /// for as long as the derived <see cref="TableFilterKind"/> is unchanged, so the applied
    /// selection survives parameter passes and re-renders. Uncontrolled: there is no
    /// fully-controlled <c>filteredValue</c> equivalent — see
    /// <see cref="Table{TItem}.OnFilterChanged"/> for how a consumer observes it instead.
    /// </summary>
    internal ColumnFilterState<TItem>? Filter { get; private set; }

    /// <summary>Whether this column's filter dropdown is open (never while it has no filter).</summary>
    internal bool IsFilterOpen => Filter is { IsOpen: true };

    /// <summary>The currently-applied selection in its serialized form (Options kind: the selected
    /// keys in <see cref="FilterOptions"/>' declared order) — the
    /// <see cref="Table{TItem}.OnFilterChanged"/> payload. Empty when the column has no filter.</summary>
    internal IReadOnlyList<string> AppliedFilterValues => Filter?.AppliedValues ?? Array.Empty<string>();

    /// <summary>Whether <paramref name="item"/> passes this column's currently-applied filter; true
    /// whenever the column has no filter or nothing is applied, so an untouched column never
    /// excludes a row.</summary>
    internal bool PassesFilter(TItem item) => Filter is null || !Filter.IsActive || Filter.PassesFilter(item);

    /// <summary>Whether Reset (and <see cref="Table{TItem}.ClearFiltersAsync"/>) should put this
    /// column back to <see cref="DefaultFilterValues"/> rather than clear it — both parameters have to
    /// be set for there to be a default to return to.</summary>
    internal bool HasFilterDefault => FilterResetToDefault && DefaultFilterValues is not null;

    /// <summary>
    /// Stages <see cref="DefaultFilterValues"/> and applies it, reporting whether the APPLIED state
    /// actually changed — the same measure <c>Commit</c>/<c>Clear</c> report, so returning to a
    /// default already in force stays the no-op the Table's "real change only" rule expects. One
    /// implementation for the default's two users: the once-only initial application
    /// (<see cref="ApplyInitialFilterDefault"/>) and <see cref="FilterResetToDefault"/>'s Reset.
    /// A list this kind cannot interpret at all falls back to clearing, so Reset always ends
    /// somewhere definite instead of leaving the previous selection applied.
    /// </summary>
    internal bool RestoreFilterDefault()
    {
        if (Filter is not { } filter) return false;
        var values = DefaultFilterValues as IReadOnlyList<string> ?? DefaultFilterValues?.ToArray() ?? [];
        return filter.TryRestore(values) ? filter.Commit() : filter.Clear();
    }

    /// <summary>
    /// The one place the dropdown's open flag flips. Returns the consumer's
    /// <see cref="OnFilterDropdownOpenChange"/> invocation (already completed when nothing changed or
    /// nobody listens), so every path that opens or closes the dropdown — the funnel, OK, Reset,
    /// Escape, an outside click, another column's dropdown opening, the column ceasing to offer a
    /// filter — raises exactly one notification per actual transition, and a caller with an await
    /// point can observe it. Synchronous callers discard the task (the same fire-and-forget the Table
    /// already applies to <see cref="Table{TItem}.OnFilterChanged"/> where no await point exists).
    /// </summary>
    internal Task SetFilterOpen(bool open) => SetFilterOpen(Filter, open);

    Task SetFilterOpen(ColumnFilterState<TItem>? filter, bool open)
    {
        if (filter is null || filter.IsOpen == open) return Task.CompletedTask;
        filter.IsOpen = open;
        return OnFilterDropdownOpenChange.HasDelegate ? OnFilterDropdownOpenChange.InvokeAsync(open) : Task.CompletedTask;
    }

    // Snapshot of everything the Table renders from this column, so a parameter change on an
    // ALREADY-registered column can be told apart from the same parameters arriving again (a
    // RenderFragment/Func parameter is a new instance every pass, so this method can't be skipped and
    // "it ran" means nothing on its own). See OnParametersSet for what the distinction buys.
    bool _initialized;
    string? _lastHeaderText;
    bool _lastHasTitleContent;
    bool _lastEllipsis;
    bool _lastCanSort;
    bool _lastCanFilter;
    // The kind, not just CanFilter: Options -> Custom (a FilterDropdown template arriving) or
    // Options -> Text (FilterOptions leaving while FilterText stays) keeps CanFilter true and swaps the
    // whole panel, which only a Table re-render can show.
    TableFilterKind? _lastFilterKind;
    bool _lastFilterMultiple;
    bool _lastHasFilterIcon;
    // The two dropdown extras: both add (or remove) markup inside a panel the TABLE renders, so a
    // runtime flip needs the same corrective render every other display parameter gets.
    bool _lastFilterSearch;
    bool _lastFilterCheckAll;
    // A COPY of the options as they were last seen, never the consumer's own list instance. Holding
    // the reference made OptionsEqual's ReferenceEquals fast path compare the list to itself, so a
    // consumer keeping one List<TableFilterOption> field and refilling it in place (Clear()+AddRange,
    // RemoveAll -- the ordinary pattern for data-derived options) always compared "equal" and the
    // prune (see SyncFilterState) never ran, which is the exact failure the by-value comparison was
    // written for. The same copy is what the filter state renders and orders by (OptionsFilterState
    // .Options), so there is one snapshot, not two.
    IReadOnlyList<TableFilterOption>? _lastFilterOptions;

    // The delegates the Table's own derived state (_filtered / _sorted) is computed FROM, tracked by
    // method identity -- see DelegateChanged. TextFilterMatch rides along: it is not a delegate, but
    // it changes which rows an applied text filter admits just as a swapped FilterText does.
    Delegate? _lastOnFilter;
    Delegate? _lastSortBy;
    Delegate? _lastFilterText;
    TextFilterMatch _lastTextFilterMatch;

    // Re-register on every render so the Table re-collects its columns in document order each pass.
    // This makes conditionally-rendered columns (@if) drop and re-appear in their declared position
    // instead of leaving a stale registration or appending a duplicate. The Table only adds during
    // an active collection pass (see Table.StartCollectingColumns / FinishCollectingColumns).
    //
    // Registration alone is not enough for a column that was ALREADY rendered, though: the Table
    // builds its header and body from these instances BEFORE the diff reaches this method, so a
    // runtime parameter change (<Column Title="@($"Results ({count})")">) renders one pass stale, and
    // membership was the only thing that used to queue a corrective render -- a same-set change
    // queued nothing at all and stayed stale until some unrelated event happened to re-render the
    // table. NotifyColumnChanged fixes that, guarded by the snapshot above: notifying on every pass
    // would recurse forever, since the Table's own re-render hands every column a fresh
    // ChildContent/Property delegate and lands right back here.
    protected override void OnParametersSet()
    {
        // One comparison per pass, shared by the display snapshot, the filter-state sync and the
        // prune below. The snapshot copy is taken only when the options actually changed, so the
        // steady state costs one walk of a short list and no allocation -- the same order of work the
        // old reference-then-value compare did for the inline-list case, which is the common one.
        var optionsChanged = !OptionsEqual(_lastFilterOptions, FilterOptions);
        if (optionsChanged) _lastFilterOptions = FilterOptions?.ToArray();

        // Build, drop or refresh the filter state BEFORE the display comparison: CanFilter reads it,
        // and a column gaining or losing its filter is exactly the transition that comparison has to
        // see. Applied values the new FilterOptions no longer offers are pruned in here too.
        var filterPruned = SyncFilterState(optionsChanged);
        var displayChanged = _initialized && (optionsChanged || DisplayStateChanged());
        // A swapped row-affecting delegate needs more than a re-render: the Table caches _filtered
        // and _sorted, neither of which is re-derived by a bare StateHasChanged -- see RowStateChanged.
        var rowStateChanged = _initialized && RowStateChanged();

        CaptureDisplayState();
        CaptureRowState();
        _initialized = true;

        Table?.Register(this);
        // DefaultFilterValues just turned this column's filter on. The Table's ApplyFilters for this
        // pass ran before this column existed to it, so only it can ask for the re-derive -- and it
        // has to ask AFTER Register, since the Table defers the work to the pass that promotes this
        // column into the set ApplyFilters walks. Silent by contract (see DefaultFilterValues).
        if (_filterDefaultNeedsRecompute)
        {
            _filterDefaultNeedsRecompute = false;
            Table?.NotifyColumnFilterDefaultApplied();
        }
        // The prune path already re-derives everything a row-state change needs, so it subsumes it.
        if (filterPruned) Table?.NotifyColumnFilterPruned(this);
        else if (rowStateChanged) Table?.RecomputeColumnDerivedState();
        else if (displayChanged) Table?.NotifyColumnChanged();
    }

    // The kinds THIS class's own parameters declare outright, or null for none. First match wins (a
    // FilterDropdown template owns the panel outright, so it beats the built-in editors; the keyed
    // Options kind beats Text because its OnFilter is the more specific declaration). Further
    // explicitly-declared kinds slot in here as more arms plus a case each in SyncFilterState below.
    //
    // Kept separate from the derived kinds because SyncFilterState has to know which route built the
    // state: only an explicit declaration carries the FilterOptions/OnFilter/FilterText inputs its
    // switches refresh from.
    private protected TableFilterKind? ExplicitFilterKind =>
        FilterDropdown is not null ? TableFilterKind.Custom
        : FilterOptions is { Count: > 0 } && OnFilter is not null ? TableFilterKind.Options
        : FilterText is not null ? TableFilterKind.Text
        : null;

    /// <summary>
    /// The filter kind a derived column type infers from its OWN parameters, consulted only when
    /// none of the explicit ones is declared — so an explicit <see cref="FilterOptions"/>+
    /// <see cref="OnFilter"/>, <see cref="FilterText"/> or <see cref="FilterDropdown"/> always wins,
    /// exactly as <see cref="SortBy"/> wins over <c>PropertyColumn.Sortable</c>. Null on the base;
    /// see <see cref="PropertyColumn{TItem,TProp}.Filterable"/>.
    /// </summary>
    private protected virtual TableFilterKind? DerivedFilterKind => null;

    /// <summary>
    /// Builds the state for a <see cref="DerivedFilterKind"/>. A hook rather than another arm of
    /// SyncFilterState's switch because the typed kinds need a row accessor only the derived column
    /// can produce — <c>PropertyColumn</c>'s <c>TProp</c> is invisible from here. Null for a kind
    /// this column type does not build.
    /// </summary>
    private protected virtual ColumnFilterState<TItem>? CreateDerivedFilterState(TableFilterKind kind) => null;

    /// <summary>
    /// Refreshes a state built by <see cref="CreateDerivedFilterState"/> from the current parameters,
    /// every pass while the kind is unchanged (the counterpart of the <c>Update</c> calls in
    /// SyncFilterState's switch). Returns whether an APPLIED value was pruned, the same contract
    /// SyncFilterState itself reports.
    /// </summary>
    private protected virtual bool UpdateDerivedFilterState(ColumnFilterState<TItem> state) => false;

    /// <summary>
    /// The table's rows changed — a <see cref="Table{TItem}.DataSource"/> swap, or this column
    /// registering with a table whose data already arrived. A no-op here; a column whose filter
    /// options are derived FROM the data overrides it to re-derive them (see
    /// <see cref="PropertyColumn{TItem,TProp}.FilterValuesFromData"/>).
    /// </summary>
    internal virtual void OnTableDataChanged(IReadOnlyList<TItem> items) { }

    // Keeps Filter in step with the declared kind, every parameter pass. Same kind as last pass: the
    // SAME instance is kept -- the applied selection lives in it -- and only its inputs (options,
    // multiple, predicate, accessor, match mode) are refreshed, pruning applied values the new options
    // no longer offer. Different kind, including to or from none: the old state goes and a fresh one
    // is built (or none). A different kind has a different value shape, and no kind has nothing
    // selectable, so nothing carries over.
    //
    // Dropping the state is also what closes a dropdown whose column stopped offering a filter: the
    // funnel button AND the panel leave the header with it, so an open flag must not survive. Two
    // things read that flag and both broke when it did: Table.AnyColumnFilterOpen stayed permanently
    // true, which made every OTHER column's filter skip its focus restore on close and drop focus to
    // <body> for the table's lifetime; and if options later came back, the dropdown reappeared
    // already open -- full-screen invisible backdrop and all -- with no user interaction, swallowing
    // the next click. Keyed off the kind rather than the prune, which only runs when the OPTIONS
    // changed and does nothing when nothing was selected (opening a dropdown and ticking nothing is
    // exactly the case that left it stuck).
    //
    // Returns whether an APPLIED value was lost -- to a prune, or to the state being dropped while it
    // was narrowing rows. Either is a prune the Table has to hear about (NotifyColumnFilterPruned):
    // rows the lost values were excluding have to come back, and the consumer's own summary has to
    // stop showing them. A fresh state has nothing to lose, so the first pass always returns false.
    bool SyncFilterState(bool optionsChanged)
    {
        var explicitKind = ExplicitFilterKind;
        var kind = explicitKind ?? DerivedFilterKind;
        if (kind == Filter?.Kind)
        {
            // A kind that now comes from the DERIVED declaration is refreshed by the column that
            // declared it. Options is the one kind both routes produce, so a column handing its
            // explicit FilterOptions over to Filterable/FilterValuesFromData (or back) keeps the same
            // state object, and with it the applied selection.
            if (explicitKind is null) return UpdateDerivedFilterState(Filter!);
            switch (Filter)
            {
                case OptionsFilterState<TItem> options:
                    // Two independent losses of an applied key, and both have to run: a FilterMultiple
                    // flip trims with the options unchanged, an options change prunes with no flip.
                    var trimmed = options.Update(_lastFilterOptions!, FilterMultiple, OnFilter!);
                    return (optionsChanged && options.Prune()) || trimmed;
                case CustomFilterState<TItem> custom:
                    custom.Update(_lastFilterOptions, OnFilter);
                    return false;
                case TextFilterState<TItem> text:
                    text.Update(FilterText!, TextFilterMatch);
                    return false;
            }
            return false;
        }

        var wasActive = Filter is { IsActive: true };
        // A dropdown open on the state being dropped closes with it, and the column's listener hears
        // that close like any other (a no-op on a closed or absent state).
        _ = SetFilterOpen(Filter, false);
        Filter = explicitKind switch
        {
            TableFilterKind.Options => new OptionsFilterState<TItem>(_lastFilterOptions!, FilterMultiple, OnFilter!),
            TableFilterKind.Custom => new CustomFilterState<TItem>(_lastFilterOptions, OnFilter),
            TableFilterKind.Text => new TextFilterState<TItem>(FilterText!, TextFilterMatch),
            _ => kind is { } derived ? CreateDerivedFilterState(derived) : null
        };
        ApplyInitialFilterDefault();
        return wasActive;
    }

    // Whether DefaultFilterValues has had its one chance, and whether that chance actually applied
    // something the Table has yet to narrow rows by. The flag is set the first time a state EXISTS to
    // hold the default -- a column that only becomes filterable on a later pass still gets it once --
    // and never cleared, so a kind change afterwards starts genuinely empty (AntD's
    // defaultFilteredValue is an initial value, not a value the component keeps re-asserting).
    bool _filterDefaultApplied;
    bool _filterDefaultNeedsRecompute;

    void ApplyInitialFilterDefault()
    {
        if (Filter is null || _filterDefaultApplied || DefaultFilterValues is null) return;
        _filterDefaultApplied = true;
        _filterDefaultNeedsRecompute = RestoreFilterDefault();
    }

    // Whether a delegate parameter now points at DIFFERENT CODE than the one previously captured.
    //
    // Compared by method, never by instance: a lambda written in markup is a brand-new delegate object
    // on most renders (one that closes over a local allocates a display class every pass), so an
    // instance comparison would report a change on every single render -- and with the corrective
    // renders below, that is an infinite render loop. The method identity is what changes when the
    // parent SELECTS A DIFFERENT delegate ("_byAge ? x => x.Age : x => x.Name", or a swapped Func
    // field), which is the case the Table cannot recover from on its own.
    //
    // A delegate whose method is unchanged but whose captured state moved on still produces current
    // output, because CellFor/OnFilter/Compare are all invoked live at render time -- that is the case
    // the original "delegates close over parent state" reasoning covers correctly.
    private protected static bool DelegateChanged(Delegate? last, Delegate? current) =>
        last is null ? current is not null : current is null || last.Method != current.Method;

    // The delegates the Table's cached row pipeline is derived from, as opposed to the ones it merely
    // invokes while rendering. A change here has to re-run ApplyFilters/ApplySort, not just re-render:
    // swapping OnFilter (exact -> contains) while a filter is applied leaves the previously computed
    // _filtered list in place, and swapping SortBy while a sort is active leaves _sorted in place, both
    // indefinitely.
    private protected virtual bool RowStateChanged() =>
        DelegateChanged(_lastOnFilter, OnFilter)
        || DelegateChanged(_lastSortBy, SortBy)
        || DelegateChanged(_lastFilterText, FilterText)
        || _lastTextFilterMatch != TextFilterMatch;

    private protected virtual void CaptureRowState()
    {
        _lastOnFilter = OnFilter;
        _lastSortBy = SortBy;
        _lastFilterText = FilterText;
        _lastTextFilterMatch = TextFilterMatch;
    }

    // Everything the Table's markup reads off a column, by value. ChildContent is deliberately not
    // compared: it is a template the Table invokes live at render time, and the Table re-renders
    // whenever the parent that owns it does, so its output is never stale. The delegates the row
    // pipeline is DERIVED from are handled separately -- see RowStateChanged.
    private protected virtual bool DisplayStateChanged() =>
        _lastHeaderText != HeaderText
        || _lastHasTitleContent != (TitleContent is not null)
        || _lastEllipsis != Ellipsis
        || _lastCanSort != CanSort
        || _lastCanFilter != CanFilter
        || _lastFilterKind != Filter?.Kind
        || _lastFilterMultiple != FilterMultiple
        || _lastHasFilterIcon != (FilterIcon is not null)
        || _lastFilterSearch != FilterSearch
        || _lastFilterCheckAll != FilterCheckAll;
    // FilterOptions is deliberately NOT compared here: OnParametersSet already runs that comparison
    // once (it needs the result for the prune too) and ORs it in. FilterDropdown/FilterIcon are
    // templates invoked live at render time (like ChildContent), so only their presence is tracked.

    private protected virtual void CaptureDisplayState()
    {
        _lastHeaderText = HeaderText;
        _lastHasTitleContent = TitleContent is not null;
        _lastEllipsis = Ellipsis;
        _lastCanSort = CanSort;
        _lastCanFilter = CanFilter;
        _lastFilterKind = Filter?.Kind;
        _lastFilterMultiple = FilterMultiple;
        _lastHasFilterIcon = FilterIcon is not null;
        _lastFilterSearch = FilterSearch;
        _lastFilterCheckAll = FilterCheckAll;
        // _lastFilterOptions is captured in OnParametersSet instead, as a defensive copy and only
        // when it changed -- see the field.
    }

    // By value, not by reference: FilterOptions is routinely built inline in markup (a fresh list per
    // render of an otherwise unchanged column), and a reference comparison would report a change every
    // single pass -- which, with the corrective render above, is an infinite render loop. `a` is always
    // this column's own private snapshot, so the ReferenceEquals below now only short-circuits the
    // both-null case; it can never make a mutated-in-place list compare equal to itself.
    private protected static bool OptionsEqual(IReadOnlyList<TableFilterOption>? a, IReadOnlyList<TableFilterOption>? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Value != b[i].Value || a[i].Text != b[i].Text) return false;
        }
        return true;
    }

    public virtual string? HeaderText => Title;

    /// <summary>The header content the table renders: <see cref="TitleContent"/> when supplied,
    /// otherwise the plain <see cref="HeaderText"/>.</summary>
    public RenderFragment HeaderFor() => TitleContent ?? (b => b.AddContent(0, HeaderText));

    /// <summary>Whether the table should render a sort control on this column's header.</summary>
    public virtual bool CanSort => SortBy is not null;

    /// <summary>Ascending comparison of two rows by this column. Only called when <see cref="CanSort"/>.</summary>
    public virtual int Compare(TItem a, TItem b) => SortBy!(a, b);

    public virtual RenderFragment CellFor(TItem item) =>
        ChildContent != null ? ChildContent(item) : _ => { };

    // Whether the Table should stop click propagation on this column's whole <td> -- see
    // ActionColumn<TItem>, the only column that does.
    internal virtual bool StopsRowClickPropagation => false;

    // Columns are declarative metadata only — they emit nothing themselves.
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    // When a column is conditionally removed (@if), Blazor disposes it; tell the Table so it
    // re-renders and the now-shorter column buffer is promoted (no zombie left behind).
    public void Dispose() => Table?.Unregister(this);
}
