namespace Controls;

/// <summary>
/// Provides checkboxes for each enum value, binds to a List of selected enum values.
/// Combines enum handling from EditSelectEnum/EditRadioEnum with checkbox functionality from EditCheckedStringList.
/// </summary>
public partial class EditCheckedEnumList<TEnum> : EditControlListBase<TEnum>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the list of enum values property in the model.</summary>
    [Parameter] public required Expression<Func<List<TEnum>>> Field { get; set; }

    /// <summary> If true, the enum values will be sorted by their display names.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary> Labels for the checkboxes.</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    Type _type = null!;
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum>? _cachedOptions;
    bool _lastSort;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);

        // Handle nullable enum types
        _type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(_type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(_type)! : _type;
        _cachedOptions = BuildOptions();
        _lastSort = Sort;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // The option list is cached, but a runtime Sort change must rebuild it — it was frozen at init.
        if (_cachedOptions is not null && Sort != _lastSort)
        {
            _lastSort = Sort;
            _cachedOptions = BuildOptions();
        }
    }

    List<TEnum> GetOptions() => _cachedOptions!;

    List<TEnum> BuildOptions()
    {
        var enumValues = EnumHelpers.GetValues<TEnum>(_underlyingType);

        // Sort by the same display name the UI shows so sort order matches what the user sees.
        // EnumHelpers.GetName caches its lookup, so this stays cheap on subsequent renders.
        if (Sort)
            enumValues = enumValues.OrderBy(x => x!.GetName()).ToList();

        return enumValues;
    }
}
