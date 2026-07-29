namespace Controls.Helpers;

/// <summary>
/// Shared cached-enum-options machinery for <c>EditSelectEnum</c>, <c>EditRadioEnum</c> and
/// <c>EditCheckedEnumList</c>: resolves <typeparamref name="TEnum"/>'s nullable-ness/underlying type
/// once, builds the (optionally sorted, optionally Other-reserved-last) option list, and rebuilds it
/// only when a shaping parameter actually changes at runtime -- the list would otherwise stay frozen
/// at its init-time shape forever. Holds per-instance cache state, so each control owns its own
/// instance rather than sharing a static.
/// </summary>
public sealed class EnumOptionCache<TEnum>
{
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum>? _options;
    List<TEnum?>? _nullableOptions;
    bool _lastSort;
    bool _lastHasOtherOption;

    /// <summary>
    /// The enum's underlying type -- itself when non-nullable, or the <c>Nullable&lt;T&gt;</c> type
    /// argument otherwise. Needed alongside <see cref="IsNullable"/> by the controls'
    /// <c>SelectParsing.TryParseEnum</c> call.
    /// </summary>
    public Type UnderlyingType => _underlyingType;

    /// <summary> Whether <typeparamref name="TEnum"/> is a nullable enum (i.e. <c>Nullable.GetUnderlyingType</c> returned non-null).</summary>
    public bool IsNullable => _isNullable;

    /// <summary>
    /// The cached option list as <c>List&lt;TEnum?&gt;</c>, current as of the last
    /// <see cref="Initialize"/>/<see cref="Refresh"/> call -- the shape the controls that bind a
    /// <c>TEnum?</c> value (<c>EditSelectEnum</c>, <c>EditRadioEnum</c>) render against. Every entry
    /// is a real enum member; the nullable annotation is only about the declared element type.
    /// </summary>
    public List<TEnum?> Options => _nullableOptions!;

    /// <summary>
    /// The same options, in the same order, as <c>List&lt;TEnum&gt;</c> -- the shape a control whose
    /// items are non-nullable needs (<c>EditCheckedEnumList</c> binds <c>List&lt;TEnum&gt;</c> and its
    /// <c>IsOptionDisabled</c> predicate takes a bare <c>TEnum</c>, so the nullable-element view above
    /// would only force an unwrap back at the call site).
    /// </summary>
    public List<TEnum> OptionsNonNullable => _options!;

    /// <summary>
    /// Resolves <typeparamref name="TEnum"/>'s nullable-ness/underlying type and builds the initial
    /// option list. Call once from <c>OnInitialized</c>.
    /// </summary>
    public void Initialize(bool sort, bool hasOtherOption)
    {
        var type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(type)! : type;
        _lastSort = sort;
        _lastHasOtherOption = hasOtherOption;
        SetOptions(BuildOptions(sort, hasOtherOption));
    }

    /// <summary>
    /// Rebuilds the cached option list when <paramref name="sort"/> or <paramref name="hasOtherOption"/>
    /// has changed since the last <see cref="Initialize"/>/<see cref="Refresh"/> call -- the list is
    /// cached, but a runtime change to either shaping parameter must not stay frozen at its init-time
    /// value. Call from <c>OnParametersSet</c>; a no-op when neither changed.
    /// </summary>
    public void Refresh(bool sort, bool hasOtherOption)
    {
        if (_options is not null && (sort != _lastSort || hasOtherOption != _lastHasOtherOption))
        {
            _lastSort = sort;
            _lastHasOtherOption = hasOtherOption;
            SetOptions(BuildOptions(sort, hasOtherOption));
        }
    }

    // Both views are cached side by side so neither accessor converts on the render path and each
    // hands back a reference-stable list between rebuilds. The second list is a copy purely to
    // satisfy the accessors' declared element nullability -- for an unconstrained TEnum a bare
    // `TEnum?` carries no Nullable<> wrapper, so the two lists hold the very same values.
    void SetOptions(List<TEnum> options)
    {
        _options = options;
        _nullableOptions = options.Cast<TEnum?>().ToList();
    }

    List<TEnum> BuildOptions(bool sort, bool hasOtherOption)
    {
        var enumValues = EnumHelpers.GetValues<TEnum>(_underlyingType);

        // If hasOtherOption is true, pull the last enum value out so it can be added back after
        // sorting -- Other always stays last regardless of Sort.
        TEnum? otherOption = default;
        if (hasOtherOption && enumValues.Count > 0)
        {
            otherOption = enumValues.Last();
            enumValues.RemoveAt(enumValues.Count - 1);
        }

        // Sort by the same display name the UI shows so sort order matches what the user sees.
        // EnumHelpers.GetName caches its lookup, so this stays cheap on subsequent renders.
        if (sort)
            enumValues = enumValues.OrderBy(x => x!.GetName()).ToList();

        // Add the "other" option back at the end if it exists.
        if (hasOtherOption && otherOption != null)
            enumValues.Add(otherOption);

        return enumValues;
    }
}
