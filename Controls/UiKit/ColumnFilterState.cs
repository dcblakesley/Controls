namespace Controls;

/// <summary>
/// The filter state of one <see cref="Table{TItem}"/> column: what is APPLIED (the selection that
/// actually narrows the rows) and what is PENDING (an open dropdown's staged, not-yet-applied
/// working copy). One instance per column, created and owned by the <see cref="Column{TItem}"/>
/// (see <see cref="Column{TItem}.Filter"/>) and kept for as long as the column's declared
/// <see cref="Kind"/> is unchanged, so the applied selection survives parameter passes and
/// re-renders the way the three raw fields it replaced did.
/// </summary>
/// <remarks>
/// The two-set discipline (from the original raw fields, unchanged): Pending is copied from Applied
/// every time the dropdown opens (<see cref="Discard"/>) and thrown away -- never copied back -- on
/// an outside click or Escape, so only OK (<see cref="Commit"/>) narrows rows; Reset
/// (<see cref="Clear"/>) empties both immediately. Every mutation that can change the applied
/// selection reports whether it actually did, so the Table's "real change only" rule (page reset,
/// <see cref="Table{TItem}.OnFilterChanged"/>) needs no second comparison of its own.
/// </remarks>
internal abstract class ColumnFilterState<TItem>
{
    /// <summary>Which editor this state drives and how <see cref="AppliedValues"/> is shaped.</summary>
    public abstract TableFilterKind Kind { get; }

    /// <summary>Whether this column's dropdown panel is open. <see cref="Table{TItem}.OpenColumnFilter"/>
    /// guarantees at most one column's is at a time. Lives here rather than on the column so a
    /// column that stops offering a filter (and drops this object) cannot leave the flag stuck
    /// true -- see <see cref="Column{TItem}.OnParametersSet"/>.</summary>
    public bool IsOpen;

    /// <summary>Whether the APPLIED state narrows rows. Only when true is
    /// <see cref="PassesFilter"/> consulted at all.</summary>
    public abstract bool IsActive { get; }

    /// <summary>Whether the pending working copy differs from the applied selection -- what
    /// <see cref="Commit"/> would report.</summary>
    public abstract bool HasPendingChange { get; }

    /// <summary>Whether <paramref name="item"/> passes the APPLIED filter. Callers check
    /// <see cref="IsActive"/> first; an inactive filter excludes nothing.</summary>
    public abstract bool PassesFilter(TItem item);

    /// <summary>Pending -&gt; applied (OK). Returns whether the applied selection actually changed.</summary>
    public abstract bool Commit();

    /// <summary>Pending &lt;- applied: stages the dropdown from the last APPLIED selection, discarding
    /// whatever a previous open-then-cancel left staged.</summary>
    public abstract void Discard();

    /// <summary>Empties both sets (Reset, or the column leaving the table). Returns whether the applied
    /// selection actually changed.</summary>
    public abstract bool Clear();

    /// <summary>The APPLIED selection in its serialized form -- the
    /// <see cref="Table{TItem}.OnFilterChanged"/> payload. One contract per <see cref="Kind"/>
    /// (Options: the selected keys in option order), shared with <see cref="TryRestore"/>.</summary>
    public abstract IReadOnlyList<string> AppliedValues { get; }

    /// <summary>Sets the PENDING state from a serialized form (the shape <see cref="AppliedValues"/>
    /// produces). Returns false when the values cannot be interpreted for this kind; a caller then
    /// commits or discards as it would after any other edit.</summary>
    public abstract bool TryRestore(IReadOnlyList<string> values);

    /// <summary>Short human-readable summary of the APPLIED state ("3 selected"), or null when
    /// inactive. <paramref name="table"/> supplies any label text a kind needs.</summary>
    public abstract string? Describe(Table<TItem> table);
}

/// <summary>
/// Shared shape of the string-keyed kinds (<see cref="TableFilterKind.Options"/> and, later,
/// <see cref="TableFilterKind.Custom"/>): a set of selected keys, narrowed by a per-key predicate
/// with AntD's OR-within-a-column semantics -- a row passes when the predicate accepts it for ANY
/// applied key (the Table ANDs each column's verdict together).
/// </summary>
internal abstract class KeyedFilterState<TItem> : ColumnFilterState<TItem>
{
    protected readonly HashSet<string> Applied = new(StringComparer.Ordinal);
    protected readonly HashSet<string> Pending = new(StringComparer.Ordinal);

    /// <summary>The per-key row predicate (<see cref="Column{TItem}.OnFilter"/>). Refreshed by the
    /// column on every parameter pass so a swapped delegate is what the next recompute runs.</summary>
    protected Func<TItem, string, bool> OnFilter { get; private set; }

    protected KeyedFilterState(Func<TItem, string, bool> onFilter) => OnFilter = onFilter;

    protected void SetOnFilter(Func<TItem, string, bool> onFilter) => OnFilter = onFilter;

    public override bool IsActive => Applied.Count > 0;

    public override bool HasPendingChange => !Applied.SetEquals(Pending);

    public override bool PassesFilter(TItem item) => Applied.Any(v => OnFilter(item, v));

    public override bool Commit()
    {
        // Compare BEFORE overwriting: a no-op OK (nothing ticked, or re-ticked back to the exact set
        // already applied) must report false so the Table neither resets the page nor notifies.
        var changed = !Applied.SetEquals(Pending);
        Applied.Clear();
        Applied.UnionWith(Pending);
        return changed;
    }

    public override void Discard()
    {
        Pending.Clear();
        Pending.UnionWith(Applied);
    }

    public override bool Clear()
    {
        var changed = Applied.Count > 0;
        Applied.Clear();
        Pending.Clear();
        return changed;
    }

    /// <summary>Insertion order; <see cref="OptionsFilterState{TItem}"/> overrides with option order.</summary>
    public override IReadOnlyList<string> AppliedValues => Applied.ToList();

    public override bool TryRestore(IReadOnlyList<string> values)
    {
        Pending.Clear();
        foreach (var v in values)
        {
            if (Accepts(v)) Pending.Add(v);
        }
        return true;
    }

    /// <summary>Whether <paramref name="value"/> is a key this filter can hold. The base accepts any
    /// string (a custom dropdown owns its own key space); Options restricts to its option list so
    /// the applied set can never hold a key with no option to un-tick it.</summary>
    protected virtual bool Accepts(string value) => true;

    public override string? Describe(Table<TItem> table) =>
        Applied.Count == 0 ? null : $"{Applied.Count} selected";

    // ----- Pending edits, for the editor component (the dropdown edits PENDING only). -----

    /// <summary>Whether <paramref name="value"/> is currently staged.</summary>
    public bool IsPending(string value) => Pending.Contains(value);

    /// <summary>Stage or un-stage one key (a checkbox in the multiple editor).</summary>
    public void TogglePending(string value, bool selected)
    {
        if (selected) Pending.Add(value);
        else Pending.Remove(value);
    }

    /// <summary>Stage exactly one key, replacing any other (a radio in the single editor).</summary>
    public void SelectPending(string value)
    {
        Pending.Clear();
        Pending.Add(value);
    }
}

/// <summary>
/// <see cref="TableFilterKind.Options"/>: a fixed <see cref="TableFilterOption"/> list rendered as a
/// checkbox list (<see cref="Multiple"/>) or radios, narrowed through
/// <see cref="Column{TItem}.OnFilter"/>. Owns the prune-on-options-change rule (see
/// <see cref="Prune"/>).
/// </summary>
internal sealed class OptionsFilterState<TItem> : KeyedFilterState<TItem>
{
    public override TableFilterKind Kind => TableFilterKind.Options;

    /// <summary>The options the editor renders and <see cref="AppliedValues"/> orders by. Always the
    /// column's own defensive snapshot, never the consumer's list instance -- see
    /// <c>Column._lastFilterOptions</c> for why.</summary>
    public IReadOnlyList<TableFilterOption> Options { get; private set; }

    /// <summary>Checkbox list (any number of keys) when true; radios (at most one) when false.</summary>
    public bool Multiple { get; private set; }

    public OptionsFilterState(IReadOnlyList<TableFilterOption> options, bool multiple, Func<TItem, string, bool> onFilter)
        : base(onFilter)
    {
        Options = options;
        Multiple = multiple;
    }

    /// <summary>Refresh from the column's current parameters -- called on every parameter pass while
    /// the kind is unchanged, so the applied selection survives while the inputs stay current.</summary>
    public void Update(IReadOnlyList<TableFilterOption> options, bool multiple, Func<TItem, string, bool> onFilter)
    {
        Options = options;
        Multiple = multiple;
        SetOnFilter(onFilter);
    }

    /// <summary>The applied keys in <see cref="Options"/>' declared order.</summary>
    public override IReadOnlyList<string> AppliedValues =>
        Options.Where(o => Applied.Contains(o.Value)).Select(o => o.Value).ToList();

    protected override bool Accepts(string value) => Options.Any(o => o.Value == value);

    /// <summary>
    /// Drops applied (and pending) keys the current <see cref="Options"/> no longer offer; returns
    /// whether an APPLIED key was dropped. Called by the column after an options change (data-derived
    /// options usually swap with the data). Without it an orphaned key kept excluding every row: an
    /// empty table, a dropdown with nothing ticked to explain it (OK a no-op, only Reset recovering),
    /// and a consumer's own summary reporting no filter, since <see cref="AppliedValues"/> already
    /// intersects with the options. Pending is pruned too: it is what an open dropdown would commit,
    /// and a key with no option cannot be un-ticked. Silent here -- the column decides whether the
    /// Table hears about it (<see cref="Table{TItem}.NotifyColumnFilterPruned"/>).
    /// </summary>
    public bool Prune()
    {
        if (Applied.Count == 0 && Pending.Count == 0) return false;

        var available = new HashSet<string>(Options.Select(o => o.Value), StringComparer.Ordinal);
        var appliedPruned = Applied.RemoveWhere(v => !available.Contains(v)) > 0;
        Pending.RemoveWhere(v => !available.Contains(v));
        return appliedPruned;
    }
}
