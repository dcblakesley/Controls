namespace Controls;

/// <summary> Uses an enum as the options. Defaults to the enum's numeric order; set <c>Sort</c> to sort alphabetically by display name.</summary>
public partial class EditSelectEnum<TEnum> : EditControlBase<TEnum>
{
    // Component specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TEnum>>? Field { get; set; }

    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary>
    /// Text for the empty/placeholder option rendered only for a <b>nullable</b> enum, so the user can
    /// represent and select "no value". Defaults to empty. Has no effect on a non-nullable enum.
    /// </summary>
    [Parameter] public string NullOptionText { get; set; } = "";

    /// <summary>
    /// Resolved text for the leading null option: the <see cref="NullOptionText"/> parameter when the
    /// consumer set it, else the bound property's <c>[Placeholder]</c>/<c>[Display(Prompt)]</c>
    /// attribute, else empty. Unlike <c>EditSelectString.NullOptionText</c>, an empty
    /// <see cref="NullOptionText"/> here carries no "suppress the option" meaning (that's gated
    /// separately by <c>_isNullable</c>) — so an unset/empty value is free to fall through to the
    /// model attribute without resurrecting anything the consumer deliberately turned off.
    /// </summary>
    string EffectiveNullOptionText => string.IsNullOrEmpty(NullOptionText) ? _attributes.Placeholder() ?? "" : NullOptionText;

    readonly EnumOptionCache<TEnum> _cache = new();

    // Markup reads _isNullable directly (leading empty option / unmatched-value placeholder), so it
    // stays a same-named member delegating to the cache rather than a call site rewrite.
    bool _isNullable => _cache.IsNullable;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditSelectEnum<TEnum>)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));

        // EditSelectEnum has no "Other" option, so hasOtherOption is always false here.
        _cache.Initialize(Sort, hasOtherOption: false);
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // The option list is cached, but a runtime Sort change must rebuild it — it was frozen at init.
        _cache.Refresh(Sort, hasOtherOption: false);
    }

    List<TEnum?> GetOptions() => _cache.Options;

    // Base IsValueDefault uses EqualityComparer<TEnum>.Default — for non-nullable TEnum that
    // matches the zero-valued enum, which is the same "default" as before.
    protected override bool TryParseValueFromString(string? value, out TEnum result, out string validationErrorMessage) =>
        SelectParsing.TryParseEnum(value, _cache.UnderlyingType, _cache.IsNullable, FieldIdentifier.FieldName, out result, out validationErrorMessage);
}
