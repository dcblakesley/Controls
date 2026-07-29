namespace Controls.Helpers;

/// <summary>
/// The read-only-view label resolution shared by <see cref="EditSelectSearch{TValue}"/> and
/// <see cref="EditMultiSelect{TValue}"/>: "the matching option's label, else the value's own
/// <c>ToString</c>", resolved through one <see cref="SelectOptionLookup"/> dictionary and cached at
/// both levels — the dictionary (rebuilt only when the options *reference* changes) and the resolved
/// text (recomputed only when the value changes). Holds per-instance state, so each control owns its
/// own instance rather than sharing a static (same shape as <see cref="EnumOptionCache{TEnum}"/>).
/// </summary>
/// <remarks>
/// <para>
/// Only the read-only path uses this. While the editor renders, the label on screen comes from
/// <see cref="Select{TValue}"/>, which keeps its own lookup — interleaved with its tag/selection state,
/// which is why the engine isn't a consumer here. Both build through <see cref="SelectOptionLookup"/>,
/// so a duplicate-valued option list resolves the same last-wins way in the read-only view as in the
/// dropdown.
/// </para>
/// <para>
/// Caching matters because a parent re-render re-parameterizes the control on every keystroke elsewhere
/// in the form: without the reference guard, the read-only path would rebuild the lookup per parameter
/// set, and the joined form would re-scan the options once per selected value on every render.
/// </para>
/// </remarks>
internal sealed class SelectLabelCache<TValue>
{
    // value -> option. Null option values are filtered before insertion (by SelectOptionLookup) and
    // guarded on lookup (Resolve below), so the dictionary never holds a null key -- suppressed here,
    // once for both wrappers, so TValue stays unconstrained (e.g. nullable-enum options).
#pragma warning disable CS8714
    Dictionary<TValue, SelectOption<TValue>> _lookup = SelectOptionLookup.Build<TValue>(null);
#pragma warning restore CS8714

    // The options the dictionary was built from. _built is separate because null is a legitimate
    // options value: without it, a first Refresh(null) would look identical to "never refreshed".
    IEnumerable<SelectOption<TValue>>? _options;
    bool _built;

    // Cached resolved text. The _has* flags can't be inferred from the cached key -- default(TValue)
    // and a null list are real inputs with real answers ("0", ""), not "nothing cached yet".
    TValue? _value;
    string _label = string.Empty;
    bool _hasLabel;

    IEnumerable<TValue>? _values;
    string _joined = string.Empty;
    bool _hasJoined;

    /// <summary>
    /// Points the cache at the control's current option list, rebuilding the value → option dictionary
    /// only when <paramref name="options"/> is a different reference than last time (so <c>Options</c> is
    /// an immutable-by-reference parameter here exactly as it is for <see cref="Select{TValue}"/> —
    /// reassign a new collection to refresh, don't mutate in place). Call from <c>OnParametersSet</c>.
    /// </summary>
    public void Refresh(IEnumerable<SelectOption<TValue>>? options)
    {
        if (_built && ReferenceEquals(options, _options)) return;
        _built = true;
        _options = options;
        _lookup = SelectOptionLookup.Build(options);
        // A rebuilt dictionary can resolve the same value to a different label, so the cached text has
        // to go with it -- the value guards below would otherwise hand back the pre-rebuild string.
        _hasLabel = false;
        _hasJoined = false;
    }

    /// <summary>
    /// The label for a single bound value (<see cref="EditSelectSearch{TValue}"/>'s read-only text):
    /// the matching option's <see cref="SelectOption{TValue}.Label"/>, else the value's own
    /// <c>ToString</c>, else the empty string. Safe to call from a render getter — recomputes only when
    /// the value differs from the last one asked for (by <see cref="EqualityComparer{T}.Default"/>) or
    /// <see cref="Refresh"/> rebuilt the dictionary.
    /// </summary>
    public string Label(TValue? value)
    {
        if (_hasLabel && EqualityComparer<TValue>.Default.Equals(value, _value)) return _label;
        _hasLabel = true;
        _value = value;
        _label = Resolve(value);
        return _label;
    }

    /// <summary>
    /// The same resolution applied to every item of a bound collection and comma-joined
    /// (<see cref="EditMultiSelect{TValue}"/>'s read-only text); a null collection joins to the empty
    /// string. Safe to call from a render getter — recomputes only when the collection is a different
    /// reference than the last one asked for (every selection change hands the control a new list) or
    /// <see cref="Refresh"/> rebuilt the dictionary.
    /// </summary>
    public string JoinedLabels(IEnumerable<TValue>? values)
    {
        if (_hasJoined && ReferenceEquals(values, _values)) return _joined;
        _hasJoined = true;
        _values = values;
        _joined = string.Join(", ", (values ?? []).Select(v => Resolve(v)));
        return _joined;
    }

    string Resolve(TValue? value) =>
        (value is not null && _lookup.TryGetValue(value, out var option) ? option.Label : null)
        ?? value?.ToString()
        ?? string.Empty;
}
