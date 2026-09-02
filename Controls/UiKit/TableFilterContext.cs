namespace Controls;

/// <summary>
/// What a <see cref="Column{TItem}.FilterDropdown"/> template receives (AntD's <c>filterDropdown</c>
/// props): the column, its staged selection, and the operations the built-in panel's OK/Reset footer
/// would otherwise perform -- the template owns the whole panel, buttons included. Selections are
/// string keys narrowed through <see cref="Column{TItem}.OnFilter"/>, exactly like the Options kind.
/// A fresh instance is handed to the template on every render of the panel; every member reads the
/// column's live filter state, so <see cref="SelectedValues"/> is always current.
/// </summary>
public sealed class TableFilterContext<TItem>
{
    readonly Table<TItem> _table;
    readonly CustomFilterState<TItem> _state;

    internal TableFilterContext(Table<TItem> table, Column<TItem> column, CustomFilterState<TItem> state)
    {
        _table = table;
        Column = column;
        _state = state;
    }

    /// <summary>The column this dropdown filters.</summary>
    public Column<TItem> Column { get; }

    /// <summary>The STAGED keys -- what <see cref="ConfirmAsync"/> would apply. Seeded from the applied
    /// selection when the dropdown opens (AntD's <c>selectedKeys</c>). Ordered by
    /// <see cref="Options"/> when the column has options, else in the order they were staged.</summary>
    public IReadOnlyList<string> SelectedValues => _state.PendingValues;

    /// <summary>Replaces the staged keys (AntD's <c>setSelectedKeys</c>). Nothing is applied until
    /// <see cref="ConfirmAsync"/>; the panel re-renders so the template can reflect the new
    /// selection.</summary>
    public void SetSelectedValues(IEnumerable<string> values)
    {
        _state.TryRestore(values as IReadOnlyList<string> ?? values.ToList());
        _table.NotifyColumnChanged();
    }

    /// <summary>Applies the staged keys (AntD's <c>confirm</c>): rows are re-derived and
    /// <see cref="Table{TItem}.OnFilterChanged"/> is raised when the applied selection actually changed.
    /// <paramref name="closeDropdown"/> false keeps the panel open (AntD's
    /// <c>confirm({ closeDropdown: false })</c>) -- for a template that applies as the user edits.</summary>
    public Task ConfirmAsync(bool closeDropdown = true) => _table.ApplyColumnFilterAsync(Column, closeDropdown);

    /// <summary>Clears both the staged and the applied selection and closes the panel (AntD's
    /// <c>clearFilters</c>): the same path as the built-in Reset button.</summary>
    public Task ResetAsync() => _table.ResetColumnFilterAsync(Column);

    /// <summary>Closes the panel without applying (AntD's <c>close</c>); the staged keys are thrown
    /// away the next time it opens.</summary>
    public void Close() => _table.CloseColumnFilter(Column);

    /// <summary>Whether the panel is currently open (AntD's <c>visible</c>).</summary>
    public bool IsOpen => _state.IsOpen;

    /// <summary>The column's <see cref="Column{TItem}.FilterOptions"/>, if any (AntD's <c>filters</c>)
    /// -- for a template that renders its own list of the declared options.</summary>
    public IReadOnlyList<TableFilterOption>? Options => _state.Options;
}
