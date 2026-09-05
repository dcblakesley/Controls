namespace Controls;

/// <summary>
/// A table column bound to a property of the row item. Set <see cref="Property"/>
/// to a selector and (optionally) <see cref="Format"/> for formatting.
/// </summary>
/// <remarks>
/// <see cref="Property"/> is what every derived behaviour on this column is built from, not just the
/// cell text: <see cref="Sortable"/> orders by it through <see cref="Comparer{T}.Default"/>, and
/// <see cref="Filterable"/> / <see cref="FilterValuesFromData"/> derive a filter editor from
/// <typeparamref name="TProp"/> and narrow rows through it. Each of those is only ever a default —
/// <see cref="Column{TItem}.SortBy"/> overrides the derived comparison, and any explicitly declared
/// filter (<see cref="Column{TItem}.FilterOptions"/>+<see cref="Column{TItem}.OnFilter"/>,
/// <see cref="Column{TItem}.FilterText"/>, <see cref="Column{TItem}.FilterDropdown"/>) overrides the
/// derived editor.
/// </remarks>
// TProp is annotated 'All' because Filterable's derived NumberRange editor feeds it (through
// NumberRangeFilterState<TItem, TProp>) to BindConverter.TryConvertTo<T>, which declares that
// requirement for its TypeConverter fallback -- the same annotation, for the same reason, that
// EditNumber<T>/EditDateNative<T> already carry. It only asks the trimmer to keep TProp's members,
// which for the property types a table column binds (primitives, enums, dates, strings) is nothing
// it wasn't keeping anyway.
public class PropertyColumn<TItem, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProp> : Column<TItem>
{
    // Whether Comparer<TProp>.Default can actually order TProp values. Computed once per closed
    // generic type. Covers Nullable<T> (the default comparer orders a comparable underlying type)
    // and any type implementing IComparable / IComparable<T>; for everything else (e.g. a plain
    // class) Comparer<T>.Default.Compare throws, so we treat the column as non-sortable instead.
    static readonly bool TPropIsComparable = ComputeComparable();

    // The editor Filterable derives from TProp, or null for a type with no built-in editor. Computed
    // once per closed generic type, exactly like TPropIsComparable and for the same reason: it is a
    // property of the type argument, not of any one column instance.
    static readonly TableFilterKind? TPropFilterKind = ComputeFilterKind();

    // An enum TProp's fixed option list ([EnumDisplayName]/[Display] labels, member names as values),
    // built once per closed generic type; null for every other TProp. The list is immutable and
    // shared by every column of this closed type, which is exactly what OptionsFilterState wants
    // (see its Options remarks -- the reference is its change signal).
    static readonly IReadOnlyList<TableFilterOption>? TPropEnumOptions = ComputeEnumOptions();

    static bool ComputeComparable()
    {
        // Statically-known interface checks only — the previous MakeGenericType probe was flagged
        // RequiresDynamicCode (IL3050) under AOT. The one shape this no longer detects is a
        // Nullable<T> whose T implements IComparable<T> but not non-generic IComparable (every BCL
        // comparable implements both); such a column degrades to non-sortable, and SortBy still works.
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        return typeof(IComparable).IsAssignableFrom(type)
            || typeof(IComparable<TProp>).IsAssignableFrom(typeof(TProp));
    }

    // Nullable unwrapped first, so int? filters exactly as int does (the state objects handle the
    // null rows). Enums are checked before the numeric type codes -- an enum reports its underlying
    // integral code -- and a type with no arm falls through to null, which renders no filter UI at
    // all rather than throwing: the same silent degrade a non-comparable Sortable gets.
    static TableFilterKind? ComputeFilterKind()
    {
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        if (type == typeof(string)) return TableFilterKind.Text;
        if (type.IsEnum) return TableFilterKind.Options;
        if (type == typeof(bool)) return TableFilterKind.Bool;
        if (type == typeof(DateTime) || type == typeof(DateOnly) || type == typeof(DateTimeOffset))
            return TableFilterKind.DateRange;
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16
                or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64
                or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => TableFilterKind.NumberRange,
            _ => null
        };
    }

    static IReadOnlyList<TableFilterOption>? ComputeEnumOptions()
    {
        var type = Nullable.GetUnderlyingType(typeof(TProp)) ?? typeof(TProp);
        if (!type.IsEnum) return null;
        // EnumHelpers.GetValues<T>(underlying) rather than Enum.GetValues(Type): the latter is
        // RequiresDynamicCode (IL3050). The values are re-boxed as the enum type, so they unbox to
        // both TProp and TProp? -- which is why one call covers MyEnum and MyEnum? alike.
        // DistinctBy: GetValues yields one entry per declared FIELD, so an alias (enum E { None = 0,
        // Default = None }) would offer two options with the same key.
        return EnumHelpers.GetValues<TProp>(type)
            .Select(v => new TableFilterOption(EnumHelpers.GetName(v), v!.ToString()!))
            .DistinctBy(o => o.Value)
            .ToList();
    }

    [Parameter] public Func<TItem, TProp>? Property { get; set; }

    /// <summary>Optional format string for <see cref="IFormattable"/> values.</summary>
    [Parameter] public string? Format { get; set; }

    /// <summary>
    /// Makes the column header sortable. The comparison is derived from <see cref="Property"/>
    /// via <see cref="Comparer{T}.Default"/>, so <typeparamref name="TProp"/> must be comparable —
    /// if it isn't, the header silently stays non-sortable rather than throwing on click. Set
    /// <c>SortBy</c> to supply a custom comparison, which works for any type.
    /// </summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>
    /// Gives the column a filter editor derived from <typeparamref name="TProp"/> (nullable
    /// unwrapped), the way <see cref="Sortable"/> derives its comparison — no options list and no
    /// predicate to write:
    /// <list type="bullet">
    /// <item><description><c>string</c> → a text box (<see cref="TableFilterKind.Text"/>, honouring
    /// <see cref="Column{TItem}.TextFilterMatch"/>).</description></item>
    /// <item><description>any numeric primitive (<c>byte</c>…<c>ulong</c>, <c>float</c>,
    /// <c>double</c>, <c>decimal</c>) → an inclusive min/max pair
    /// (<see cref="TableFilterKind.NumberRange"/>).</description></item>
    /// <item><description><c>DateTime</c>, <c>DateOnly</c>, <c>DateTimeOffset</c> → an inclusive
    /// date range (<see cref="TableFilterKind.DateRange"/>), compared at day
    /// granularity.</description></item>
    /// <item><description><c>bool</c> → a yes/no/any pick
    /// (<see cref="TableFilterKind.Bool"/>).</description></item>
    /// <item><description>an <c>enum</c> → an option list
    /// (<see cref="TableFilterKind.Options"/>) of every declared member, labelled by
    /// <c>[EnumDisplayName]</c>/<c>[Display]</c> and matched on the member name;
    /// <see cref="Column{TItem}.FilterMultiple"/> applies as usual.</description></item>
    /// </list>
    /// Any other type — and a column with no <see cref="Property"/> — renders no filter UI at all,
    /// silently, exactly as a non-comparable <see cref="Sortable"/> column stays unsortable.
    /// Overridden by <see cref="FilterValuesFromData"/> and by any explicitly declared filter.
    /// </summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>
    /// Gives the column an option-list filter (<see cref="TableFilterKind.Options"/>) whose options
    /// are the distinct values actually present in the table's current
    /// <see cref="Table{TItem}.DataSource"/> — each formatted exactly as its cell is (so
    /// <see cref="Format"/> applies and the option text always matches what the user can see), null
    /// values skipped, ordered by the underlying values when <typeparamref name="TProp"/> is
    /// comparable and by their text otherwise.
    /// </summary>
    /// <remarks>
    /// The options are re-derived on every <see cref="Table{TItem}.DataSource"/> swap. An applied
    /// value the new data no longer offers is pruned and <see cref="Table{TItem}.OnFilterChanged"/>
    /// raised with the survivors, exactly as a consumer swapping <see cref="Column{TItem}.FilterOptions"/>
    /// out from under an applied selection already behaves — an orphaned value would otherwise keep
    /// excluding every row with no way to un-tick it. Wins over <see cref="Filterable"/>; loses to
    /// any explicitly declared filter.
    /// </remarks>
    [Parameter] public bool FilterValuesFromData { get; set; }

    public override bool CanSort =>
        SortBy is not null || (Sortable && Property is not null && TPropIsComparable);

    // Format is the one extra scalar the Table renders from this column (through CellFor), so it
    // joins the base snapshot that decides whether a same-set parameter change needs a corrective
    // Table render -- see Column<TItem>.OnParametersSet. Sortable needs no entry of its own: the base
    // already tracks CanSort, which it feeds; Filterable/FilterValuesFromData feed CanFilter and the
    // filter kind, which it tracks too.
    string? _lastFormat;

    // Property does NOT reduce to CanSort, though, and it flows through the identical CellFor that
    // Format does: swapping the selector itself ("selector = x => x.Age.ToString()" replacing
    // "x => x.Name") left the cells rendering the old property indefinitely, because nothing else
    // re-rendered the table. It joins the row-state snapshot rather than the display one because the
    // derived sort comparison reads it too (see Compare), and so does every derived filter's row
    // accessor, so an active sort or filter has to be re-run.
    Delegate? _lastProperty;

    private protected override bool DisplayStateChanged() => base.DisplayStateChanged() || _lastFormat != Format;

    private protected override void CaptureDisplayState()
    {
        base.CaptureDisplayState();
        _lastFormat = Format;
    }

    // Format joins the ROW state as well, but only under FilterValuesFromData: there it decides both
    // the option texts and what the per-option predicate compares against, so a changed format has to
    // re-derive the rows and not merely re-render the cells. (DerivedOptions notices the same change
    // and rebuilds the list; this is what makes the Table act on it.)
    private protected override bool RowStateChanged() =>
        base.RowStateChanged()
        || DelegateChanged(_lastProperty, Property)
        || (FilterValuesFromData && _lastFormat != Format);

    private protected override void CaptureRowState()
    {
        base.CaptureRowState();
        _lastProperty = Property;
    }

    public override int Compare(TItem a, TItem b) =>
        SortBy is not null
            ? SortBy(a, b)
            : Comparer<TProp>.Default.Compare(Property!(a), Property!(b));

    /// <summary>
    /// The text this column shows for one property value: <see cref="Format"/> applied when the value
    /// is <see cref="IFormattable"/>, plain <c>ToString()</c> otherwise, null for a null value.
    /// </summary>
    /// <remarks>
    /// One implementation shared by the cell (<see cref="CellFor"/>) and by
    /// <see cref="FilterValuesFromData"/>'s option text, so a filter option can never read
    /// differently from the cells it matches — they were two copies of this expression before, free
    /// to drift the moment either grew a special case.
    /// </remarks>
    internal string? FormatValue(TProp value)
    {
        if (value is null) return null;
        return value is IFormattable formattable && !string.IsNullOrEmpty(Format)
            ? formattable.ToString(Format, null)
            : value.ToString();
    }

    public override RenderFragment CellFor(TItem item) => builder =>
    {
        var text = Property is null ? null : FormatValue(Property(item));

        // Ellipsis wraps the text in a title-bearing <span> so the truncated value stays
        // discoverable on hover; left as bare text (unchanged DOM) when Ellipsis is unset.
        if (Ellipsis)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "title", text);
            builder.AddContent(2, text);
            builder.CloseElement();
        }
        else
        {
            builder.AddContent(0, text);
        }
    };

    // ----- Derived filtering (Filterable / FilterValuesFromData) -----

    // The data-derived option snapshot and the Format it was built with. Held here, never in
    // Column._lastFilterOptions: that field tracks the FilterOptions PARAMETER (still null on a
    // FilterValuesFromData column), and letting the two share a slot would make the base's
    // options-changed comparison see a change on every pass -- with the corrective render the base
    // queues, an infinite render loop.
    IReadOnlyList<TableFilterOption>? _dataOptions;
    string? _dataOptionsFormat;
    // FormatValue is CurrentCulture-dependent, so the culture is part of the cache key: without it a
    // runtime culture switch left the option keys in the old culture while the live predicate
    // formatted rows in the new one, and nothing matched. The keys themselves stay culture-formatted
    // (they are the text the cells show, and the shipped OnFilterChanged payload).
    string? _dataOptionsCulture;

    // FilterValuesFromData declares no kind until the data actually yields an option: with none
    // (DataSource null/empty, or every value null) the funnel would open an empty panel, and a
    // DefaultFilterValues would spend its one shot against an empty key space and be lost. Staying
    // null defers both to the pass where options exist -- Property is a delegate parameter, so the
    // column re-runs OnParametersSet on every Table render and cannot miss it.
    private protected override TableFilterKind? DerivedFilterKind =>
        Property is null ? null
        : FilterValuesFromData ? (DerivedOptions.Count > 0 ? TableFilterKind.Options : null)
        : Filterable ? TPropFilterKind
        : null;

    private protected override ColumnFilterState<TItem>? CreateDerivedFilterState(TableFilterKind kind) => kind switch
    {
        TableFilterKind.Options => new OptionsFilterState<TItem>(DerivedOptions, FilterMultiple, DerivedOptionPredicate),
        TableFilterKind.Text => new TextFilterState<TItem>(StringAccessor, TextFilterMatch),
        TableFilterKind.NumberRange => new NumberRangeFilterState<TItem, TProp>(Property!),
        TableFilterKind.DateRange => new DateRangeFilterState<TItem>(DateAccessor),
        TableFilterKind.Bool => new BoolFilterState<TItem>(BoolAccessor),
        _ => null
    };

    private protected override bool UpdateDerivedFilterState(ColumnFilterState<TItem> state)
    {
        switch (state)
        {
            case OptionsFilterState<TItem> options:
                // Reference, not value: DerivedOptions hands back the SAME list until it genuinely
                // rebuilds (a DataSource swap, a Format change, or a switch between the enum list and
                // the data-derived one), so this is the whole "did the options change?" test -- and it
                // is false on the pass right after OnTableDataChanged already pushed and pruned.
                var current = DerivedOptions;
                var changed = !ReferenceEquals(options.Options, current);
                // The single-select trim rides alongside, not on, that comparison -- a FilterMultiple
                // flip leaves the options identical.
                var trimmed = options.Update(current, FilterMultiple, DerivedOptionPredicate);
                return (changed && options.Prune()) || trimmed;
            case TextFilterState<TItem> text:
                text.Update(StringAccessor, TextFilterMatch);
                return false;
            case NumberRangeFilterState<TItem, TProp> range:
                range.Update(Property!);
                return false;
            case DateRangeFilterState<TItem> dates:
                dates.Update(DateAccessor);
                return false;
            case BoolFilterState<TItem> flag:
                flag.Update(BoolAccessor);
                return false;
        }
        return false;
    }

    internal override void OnTableDataChanged(IReadOnlyList<TItem> items)
    {
        if (!FilterValuesFromData || Property is null) return;
        if (!RebuildDataOptions(items)) return;
        // An explicitly declared filter owns the live state; pushing the data-derived options into it
        // would replace the consumer's own list and predicate and prune their applied selection on
        // every DataSource swap. The snapshot above is still refreshed, so dropping the explicit
        // declaration later finds current options.
        if (ExplicitFilterKind is not null) return;
        // The new list has to reach the live state (the editor renders from it, and AppliedValues
        // orders by it) before anything is pruned against it.
        if (Filter is not OptionsFilterState<TItem> options) return;
        var trimmed = options.Update(_dataOptions!, FilterMultiple, DerivedOptionPredicate);
        // Same prune contract as a consumer-swapped FilterOptions: the Table re-derives the rows and
        // raises OnFilterChanged with whatever survived.
        if (options.Prune() || trimmed) Table?.NotifyColumnFilterPruned(this);
    }

    // The option list a derived Options state renders from: an enum's fixed per-type list, or the
    // distinct formatted values of the table's current rows. Rebuilt lazily so a column that only
    // just turned FilterValuesFromData on (or changed Format) does not have to wait for the next
    // DataSource swap to see options.
    IReadOnlyList<TableFilterOption> DerivedOptions
    {
        get
        {
            if (!FilterValuesFromData) return TPropEnumOptions ?? [];
            if (_dataOptions is null || _dataOptionsFormat != Format || _dataOptionsCulture != CultureInfo.CurrentCulture.Name)
                RebuildDataOptions(Table?.Items ?? []);
            return _dataOptions!;
        }
    }

    // Rebuilds _dataOptions from the given rows; returns whether the LIST changed. Keeps the previous
    // instance when the values are value-equal so the reference stays a usable change signal (see
    // UpdateDerivedFilterState) and the editor's per-options-snapshot caches survive a no-op swap.
    bool RebuildDataOptions(IReadOnlyList<TItem> items)
    {
        var next = BuildDataOptions(items);
        _dataOptionsFormat = Format;
        _dataOptionsCulture = CultureInfo.CurrentCulture.Name;
        if (_dataOptions is not null && OptionsEqual(_dataOptions, next)) return false;
        _dataOptions = next;
        return true;
    }

    IReadOnlyList<TableFilterOption> BuildDataOptions(IReadOnlyList<TItem> items)
    {
        if (Property is null) return [];

        // First-seen order while de-duplicating, so the ordering below is the only thing that decides
        // the final order (OrderBy is stable, so values that compare equal keep first-seen order
        // rather than whatever a hash bucket produced).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var entries = new List<(string Text, TProp Value)>();
        foreach (var item in items)
        {
            var value = Property(item);
            if (value is null) continue;
            var text = FormatValue(value);
            if (text is null || !seen.Add(text)) continue;
            entries.Add((text, value));
        }

        var ordered = TPropIsComparable
            ? entries.OrderBy(e => e.Value, Comparer<TProp>.Default)
            : entries.OrderBy(e => e.Text, StringComparer.Ordinal);
        return ordered.Select(e => new TableFilterOption(e.Text, e.Text)).ToList();
    }

    // One predicate for both derived Options flavours, because the option VALUE differs: the enum
    // list keys on the member name (ToString), the data-derived list on the formatted text that IS
    // the option. Read live rather than captured so a runtime flip between the two needs no new
    // delegate.
    bool DerivedOptionPredicate(TItem item, string value)
    {
        if (Property is null) return false;
        var current = Property(item);
        if (current is null) return false;
        var text = FilterValuesFromData ? FormatValue(current) : current.ToString();
        return string.Equals(text, value, StringComparison.Ordinal);
    }

    // The typed row accessors the derived states need. Method groups, not lambdas: they read Property
    // live, so no state has to be handed a fresh delegate when the selector is swapped, and their
    // method identity is stable (which is what Column.DelegateChanged compares).
    string? StringAccessor(TItem item) => Property is null ? null : (object?)Property(item) as string;

    DateTime? DateAccessor(TItem item)
    {
        if (Property is null) return null;
        return Property(item) switch
        {
            DateTime value => value,
            DateOnly value => value.ToDateTime(TimeOnly.MinValue),
            DateTimeOffset value => value.DateTime,
            _ => null
        };
    }

    bool? BoolAccessor(TItem item)
    {
        if (Property is null) return null;
        return Property(item) is bool value ? value : null;
    }
}
