namespace Controls;

/// <summary> Uses an enum as the options. Defaults to sorted by Id, can be sorted by name using the SortByName parameter</summary>
public partial class EditSelectEnum<TEnum> : EditControlBase<TEnum>
{
    // Component specific parameters
    /// <summary> Expression that binds to the enum property in the model.</summary>
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }

    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary>
    /// Text for the empty/placeholder option rendered only for a <b>nullable</b> enum, so the user can
    /// represent and select "no value". Defaults to empty. Has no effect on a non-nullable enum.
    /// </summary>
    [Parameter] public string NullOptionText { get; set; } = "";

    Type _type = null!;
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum?>? _cachedOptions;
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

    List<TEnum?> GetOptions() => _cachedOptions!;

    List<TEnum?> BuildOptions()
    {
        var enumValues = Enum.GetValues(_underlyingType).Cast<TEnum>().ToList();

        // Sort by the same display name the UI shows so sort order matches what the user sees.
        // EnumHelpers.GetName caches its lookup, so this stays cheap on subsequent renders.
        if (Sort)
            enumValues = enumValues.OrderBy(x => x!.GetName()).ToList();

        return enumValues.Cast<TEnum?>().ToList();
    }

    // Base IsValueDefault uses EqualityComparer<TEnum>.Default — for non-nullable TEnum that
    // matches the zero-valued enum, which is the same "default" as before.
    protected override bool TryParseValueFromString(string? value, out TEnum result, out string validationErrorMessage) =>
        SelectParsing.TryParseEnum(value, _underlyingType, _isNullable, FieldIdentifier.FieldName, out result, out validationErrorMessage);
}
