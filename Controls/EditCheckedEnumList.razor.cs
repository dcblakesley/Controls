namespace Controls;

/// <summary>
/// Provides checkboxes for each enum value, binds to a List of selected enum values.
/// Combines enum handling from EditSelectEnum/EditRadioEnum with checkbox functionality from EditCheckedStringList.
/// </summary>
public partial class EditCheckedEnumList<TEnum> : EditControlListBase<TEnum>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<List<TEnum>>>? Field { get; set; }

    /// <summary> If true, the enum values will be sorted by their display names.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary> Labels for the checkboxes.</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each enum value being rendered. An option
    /// is disabled when this returns true OR the whole group's <c>IsDisabled</c> is true. Null
    /// (default) disables nothing beyond <c>IsDisabled</c>.
    /// </summary>
    [Parameter] public Func<TEnum, bool>? IsOptionDisabled { get; set; }

    /// <summary>
    /// When true, each checkbox renders with a custom-drawn box (hidden native input + a sibling
    /// element that draws the visual state) instead of the bare native checkbox — same opt-in as
    /// <see cref="EditBool.UseStyledCheckbox"/>. Null (default) falls through to <see cref="FormOptions"/>,
    /// then any enclosing <see cref="Controls.FormDefaults"/>, then <see cref="FormOptions.DefaultUseStyledCheckbox"/>.
    /// </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the FormOptions/FormDefaults/static chain. </summary>
    bool EffectiveUseStyledCheckbox => EditControlInit.UseStyledCheckbox(UseStyledCheckbox, FormOptions, FormDefaults);

    Type _type = null!;
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum>? _cachedOptions;
    bool _lastSort;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditCheckedEnumList<TEnum>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));

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
