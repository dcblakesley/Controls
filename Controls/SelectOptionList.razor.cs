namespace Controls;

/// <summary>
/// Internal-use option-rendering body shared by <see cref="EditSelectEnum{TEnum}"/> and
/// <see cref="EditSelectString{TValue}"/>: the leading null/empty option, the unmatched-value hidden
/// placeholder, and the real <c>&lt;option&gt;</c> loop. Renders inside the host's own
/// <c>&lt;select&gt;</c> (this component owns no wrapping element), so it slots directly into the
/// host's markup in place of what used to be three duplicated blocks. The two hosts differ only in
/// how an option is displayed and turned into a <c>value</c>/match string -- captured here as
/// <see cref="DisplayText"/> and <see cref="ValueString"/> -- plus when the null option and unmatched
/// placeholder should render, which the host computes and passes in directly (<see cref="ShowNullOption"/>,
/// <see cref="ShowPlaceholder"/>) since the gating rules genuinely differ (nullable enum vs.
/// ShowNullOption-opted-out string select) and don't belong in a shared component.
/// </summary>
public partial class SelectOptionList<TItem>
{
    [Parameter, EditorRequired] public IEnumerable<TItem> Options { get; set; } = [];
    [Parameter, EditorRequired] public string Id { get; set; } = "";
    [Parameter] public string? CssClass { get; set; }
    [Parameter] public string? Tooltip { get; set; }
    [Parameter] public string? CurrentValueAsString { get; set; }
    [Parameter] public bool ShowNullOption { get; set; }
    [Parameter] public string? NullOptionText { get; set; }
    [Parameter] public bool ShowPlaceholder { get; set; }
    [Parameter] public string? PlaceholderText { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string?> DisplayText { get; set; } = null!;
    [Parameter, EditorRequired] public Func<TItem, string?> ValueString { get; set; } = null!;

    /// <summary>
    /// <see cref="Options"/> materialized once per parameter cycle so the markup can index it in step
    /// with <see cref="OptionIds"/>. A <see cref="IReadOnlyList{T}"/> source (both hosts pass a
    /// <c>List&lt;T&gt;</c>) is used as-is; anything else is copied.
    /// </summary>
    IReadOnlyList<TItem> OptionList { get; set; } = [];

    /// <summary>
    /// One distinct id segment per option, positionally aligned with <see cref="OptionList"/> — the
    /// <c>{Id}-option-{segment}</c> trailing part of each <c>&lt;option&gt;</c>'s <c>id</c>/<c>data-test-id</c>.
    /// The same de-duplication <c>CheckboxOptionList</c> and the radio hosts already apply: <c>ToId</c>
    /// strips everything outside <c>[A-Za-z0-9-_]</c>, so an all-CJK option list sanitized every entry to
    /// the same empty segment and a duplicate string option repeated its own — several elements sharing
    /// one DOM id either way.
    /// </summary>
    /// <remarks>
    /// The two synthetic segments this component emits itself (<c>none</c> for the leading blank option,
    /// <c>placeholder</c> for the unmatched-value option) are reserved unconditionally, not only while
    /// their option renders: <c>ShowPlaceholder</c> is derived from the CURRENT value, so a conditional
    /// reservation would change a literal option's id as the user picks values. An ordinary option list
    /// contains neither segment and renders exactly the ids it always did.
    /// </remarks>
    string[] OptionIds { get; set; } = [];

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        OptionList = Options as IReadOnlyList<TItem> ?? Options.ToList();
        OptionIds = EnumHelpers.ToUniqueIds(OptionList, "none", "placeholder");
    }
}
