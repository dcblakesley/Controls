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
    /// no filter UI. Must be paired with <see cref="OnFilter"/> to actually filter rows — see
    /// <see cref="CanFilter"/>.
    /// </summary>
    [Parameter] public IReadOnlyList<TableFilterOption>? FilterOptions { get; set; }

    /// <summary>
    /// Row-inclusion predicate given one currently-selected filter value. A row passes this
    /// column's filter when this returns true for ANY selected value (AntD's OR-within-a-column
    /// semantics; the Table ANDs each filterable column's result together). Required (with
    /// <see cref="FilterOptions"/>) for the column to render filter UI — see <see cref="CanFilter"/>.
    /// </summary>
    [Parameter] public Func<TItem, string, bool>? OnFilter { get; set; }

    /// <summary>
    /// true (default) renders the filter dropdown as a checkbox list (any number of values may be
    /// selected — AntD's default). false renders a single-select radio list instead (AntD's
    /// <c>filterMultiple={false}</c>) — picking one option replaces any other.
    /// </summary>
    [Parameter] public bool FilterMultiple { get; set; } = true;

    /// <summary>Whether the Table should render a filter control on this column's header — both
    /// <see cref="FilterOptions"/> and <see cref="OnFilter"/> must be supplied.</summary>
    public bool CanFilter => FilterOptions is { Count: > 0 } && OnFilter is not null;

    // Uncontrolled filter state, scoped to this column instance (there is no fully-controlled
    // `filteredValue` equivalent — see Table.OnFilterChanged for how a consumer observes it
    // instead). FilterApplied is what actually narrows the rows (read by Table.ApplyFilters/
    // PassesFilter below); FilterPending is the open dropdown's staged, not-yet-applied working
    // set — copied from FilterApplied every time the dropdown opens and discarded (never copied
    // back) on an outside click or Escape, so only OK commits it (Reset clears both immediately).
    internal readonly HashSet<string> FilterApplied = new();
    internal readonly HashSet<string> FilterPending = new();
    internal bool FilterOpen;

    /// <summary>The currently-applied filter selection, in <see cref="FilterOptions"/>' declared
    /// order — used for the <see cref="Table{TItem}.OnFilterChanged"/> payload.</summary>
    internal IReadOnlyList<string> AppliedFilterValues =>
        FilterOptions is null
            ? Array.Empty<string>()
            : FilterOptions.Where(o => FilterApplied.Contains(o.Value)).Select(o => o.Value).ToList();

    /// <summary>Whether <paramref name="item"/> passes this column's currently-applied filter (OR
    /// across the selected values); true whenever the column isn't filterable or nothing is
    /// selected, so an untouched column never excludes a row.</summary>
    internal bool PassesFilter(TItem item) =>
        !CanFilter || FilterApplied.Count == 0 || FilterApplied.Any(v => OnFilter!(item, v));

    // Re-register on every render so the Table re-collects its columns in document order each pass.
    // This makes conditionally-rendered columns (@if) drop and re-appear in their declared position
    // instead of leaving a stale registration or appending a duplicate. The Table only adds during
    // an active collection pass (see Table.StartCollectingColumns / FinishCollectingColumns).
    protected override void OnParametersSet() => Table?.Register(this);

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

    // Columns are declarative metadata only — they emit nothing themselves.
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    // When a column is conditionally removed (@if), Blazor disposes it; tell the Table so it
    // re-renders and the now-shorter column buffer is promoted (no zombie left behind).
    public void Dispose() => Table?.Unregister(this);
}
