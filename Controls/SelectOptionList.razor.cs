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
    [Parameter, EditorRequired] public Func<TItem, string?> DisplayText { get; set; } = null!;
    [Parameter, EditorRequired] public Func<TItem, string?> ValueString { get; set; } = null!;
}
