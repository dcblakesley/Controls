namespace Controls;

/// <summary>
/// Provides checkboxes for each enum value, binds to a List of selected enum values.
/// Combines enum handling from EditSelectEnum/EditRadioEnum with checkbox functionality from EditCheckedStringList.
/// </summary>
public partial class EditCheckedEnumList<TEnum> : CheckedListControlBase<TEnum>
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

    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    readonly EnumOptionCache<TEnum> _cache = new();

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditCheckedEnumList<TEnum>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));

        // EditCheckedEnumList has no "Other" option, so hasOtherOption is always false here.
        _cache.Initialize(Sort, hasOtherOption: false);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // The option list is cached, but a runtime Sort change must rebuild it — it was frozen at init.
        _cache.Refresh(Sort, hasOtherOption: false);
    }

    // The non-nullable view: the bound list is List<TEnum> and IsOptionDisabled takes a bare TEnum,
    // so this control needs the options in the same shape rather than the TEnum? one EditSelectEnum
    // and EditRadioEnum render against.
    List<TEnum> GetOptions() => _cache.OptionsNonNullable;
}
