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
/// Shared shape of the string-keyed kinds (<see cref="TableFilterKind.Options"/> and
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

    /// <summary>The one ordering both serialized views share: <see cref="AppliedValues"/> (the
    /// <see cref="Table{TItem}.OnFilterChanged"/> payload) and <see cref="PendingValues"/> (what a
    /// custom dropdown template reads back through <see cref="TableFilterContext{TItem}.SelectedValues"/>).
    /// Insertion order here; the kinds with an option list override with option order.</summary>
    protected virtual IReadOnlyList<string> Order(HashSet<string> keys) => keys.ToList();

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

    public override IReadOnlyList<string> AppliedValues => Order(Applied);

    /// <summary>The PENDING keys, in the same order <see cref="AppliedValues"/> uses -- what a custom
    /// dropdown template sees as its current selection while the panel is open.</summary>
    public IReadOnlyList<string> PendingValues => Order(Pending);

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

    /// <summary>The options the editor renders and <see cref="ColumnFilterState{TItem}.AppliedValues"/> orders by. Always the
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

    /// <summary>The keys in <see cref="Options"/>' declared order (every applied key has an option:
    /// <see cref="Accepts"/> and <see cref="Prune"/> guarantee it).</summary>
    protected override IReadOnlyList<string> Order(HashSet<string> keys) =>
        Options.Where(o => keys.Contains(o.Value)).Select(o => o.Value).ToList();

    protected override bool Accepts(string value) => Options.Any(o => o.Value == value);

    /// <summary>
    /// Drops applied (and pending) keys the current <see cref="Options"/> no longer offer; returns
    /// whether an APPLIED key was dropped. Called by the column after an options change (data-derived
    /// options usually swap with the data). Without it an orphaned key kept excluding every row: an
    /// empty table, a dropdown with nothing ticked to explain it (OK a no-op, only Reset recovering),
    /// and a consumer's own summary reporting no filter, since <see cref="ColumnFilterState{TItem}.AppliedValues"/> already
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

/// <summary>
/// <see cref="TableFilterKind.Custom"/>: the consumer's own <see cref="Column{TItem}.FilterDropdown"/>
/// template owns the panel and stages string keys through a <see cref="TableFilterContext{TItem}"/>;
/// rows are narrowed through <see cref="Column{TItem}.OnFilter"/> exactly as for Options. No option
/// list constrains the keys (<see cref="KeyedFilterState{TItem}.Accepts"/> stays "anything" -- the
/// template's key space is its own), so nothing is ever pruned; <see cref="Options"/> is carried
/// only to hand the column's <see cref="Column{TItem}.FilterOptions"/> to the template (AntD passes
/// <c>filters</c> into <c>filterDropdown</c> the same way) and to order the serialized keys by it
/// when it exists.
/// </summary>
internal sealed class CustomFilterState<TItem> : KeyedFilterState<TItem>
{
    // With no OnFilter the template can still stage and confirm keys (state, highlight and
    // OnFilterChanged all work -- a consumer filtering server-side wants exactly that) and the
    // predicate excludes nothing.
    static readonly Func<TItem, string, bool> AcceptAll = (_, _) => true;

    public override TableFilterKind Kind => TableFilterKind.Custom;

    /// <summary>The column's <see cref="Column{TItem}.FilterOptions"/> snapshot, or null -- surfaced
    /// to the template as <see cref="TableFilterContext{TItem}.Options"/>.</summary>
    public IReadOnlyList<TableFilterOption>? Options { get; private set; }

    public CustomFilterState(IReadOnlyList<TableFilterOption>? options, Func<TItem, string, bool>? onFilter)
        : base(onFilter ?? AcceptAll) => Options = options;

    /// <summary>Refresh from the column's current parameters, every parameter pass while the kind is
    /// unchanged.</summary>
    public void Update(IReadOnlyList<TableFilterOption>? options, Func<TItem, string, bool>? onFilter)
    {
        Options = options;
        SetOnFilter(onFilter ?? AcceptAll);
    }

    /// <summary>Option order for the keys that have an option, then any other key in insertion order
    /// (the template is free to stage keys outside the list); plain insertion order with no options.</summary>
    protected override IReadOnlyList<string> Order(HashSet<string> keys)
    {
        if (Options is null || Options.Count == 0) return base.Order(keys);
        var ordered = Options.Where(o => keys.Contains(o.Value)).Select(o => o.Value).ToList();
        if (ordered.Count < keys.Count)
        {
            var listed = new HashSet<string>(ordered, StringComparer.Ordinal);
            ordered.AddRange(keys.Where(k => !listed.Contains(k)));
        }
        return ordered;
    }
}

/// <summary>
/// <see cref="TableFilterKind.Text"/>: one typed string (<see cref="Column{TItem}.FilterText"/> supplies
/// the row accessor, <see cref="Column{TItem}.TextFilterMatch"/> the comparison), matched
/// case-insensitively (<see cref="StringComparison.OrdinalIgnoreCase"/>). The APPLIED text is always
/// normalized -- trimmed, and null when nothing but whitespace was typed, which is the inactive state.
/// The PENDING text is kept exactly as typed: the editor binds to it on every keystroke, and
/// trimming it live would eat the space a user just typed before the next word.
/// </summary>
internal sealed class TextFilterState<TItem> : ColumnFilterState<TItem>
{
    string? _applied;

    public override TableFilterKind Kind => TableFilterKind.Text;

    /// <summary>The row accessor (<see cref="Column{TItem}.FilterText"/>); a null result never matches.</summary>
    public Func<TItem, string?> Accessor { get; private set; }

    /// <summary>How the applied text is compared against a row's (<see cref="Column{TItem}.TextFilterMatch"/>).</summary>
    public TextFilterMatch Match { get; private set; }

    /// <summary>The staged text, as typed (untrimmed, may be null). What the editor binds to.</summary>
    public string? PendingText { get; set; }

    public TextFilterState(Func<TItem, string?> accessor, TextFilterMatch match)
    {
        Accessor = accessor;
        Match = match;
    }

    /// <summary>Refresh from the column's current parameters, every parameter pass while the kind is
    /// unchanged. The applied text survives; the column re-derives rows when the match mode changed.</summary>
    public void Update(Func<TItem, string?> accessor, TextFilterMatch match)
    {
        Accessor = accessor;
        Match = match;
    }

    static string? Normalize(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    public override bool IsActive => _applied is not null;

    // Ordinal, not ignore-case: "abc" and "ABC" narrow identically, but they are different payloads
    // for OnFilterChanged, and the payload is what "the applied selection changed" is judged by.
    public override bool HasPendingChange => !string.Equals(Normalize(PendingText), _applied, StringComparison.Ordinal);

    public override bool PassesFilter(TItem item)
    {
        var text = Accessor(item);
        if (text is null) return false;
        return Match switch
        {
            TextFilterMatch.StartsWith => text.StartsWith(_applied!, StringComparison.OrdinalIgnoreCase),
            TextFilterMatch.Equals => string.Equals(text, _applied, StringComparison.OrdinalIgnoreCase),
            _ => text.Contains(_applied!, StringComparison.OrdinalIgnoreCase)
        };
    }

    public override bool Commit()
    {
        var next = Normalize(PendingText);
        var changed = !string.Equals(next, _applied, StringComparison.Ordinal);
        _applied = next;
        return changed;
    }

    public override void Discard() => PendingText = _applied;

    public override bool Clear()
    {
        var changed = _applied is not null;
        _applied = null;
        PendingText = null;
        return changed;
    }

    public override IReadOnlyList<string> AppliedValues => _applied is null ? [] : [_applied];

    /// <summary>The first value (if any) becomes the pending text; every string is interpretable, so
    /// this always succeeds.</summary>
    public override bool TryRestore(IReadOnlyList<string> values)
    {
        PendingText = values.Count > 0 ? values[0] : null;
        return true;
    }

    public override string? Describe(Table<TItem> table) =>
        _applied is null
            ? null
            : Match switch
            {
                TextFilterMatch.StartsWith => $"starts with \"{_applied}\"",
                TextFilterMatch.Equals => $"equals \"{_applied}\"",
                _ => $"contains \"{_applied}\""
            };
}
