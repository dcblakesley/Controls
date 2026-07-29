namespace Controls;

/// <summary>
/// Internal-use body shared by <see cref="EditCheckedEnumList{TEnum}"/> and
/// <see cref="EditCheckedStringList"/>: the checkbox-per-option editor (styled/unstyled) plus the
/// read-only fallback. The two hosts differ only in how an option is enumerated, keyed, and
/// displayed — captured here as <see cref="DisplayText"/> — everything else (fieldset contents,
/// ARIA wiring, styled-checkbox markup) is identical between them.
/// </summary>
public partial class CheckboxOptionList<TItem>
{
    [Parameter, EditorRequired] public IReadOnlyList<TItem> Options { get; set; } = [];
    [Parameter] public List<TItem>? Value { get; set; }
    [Parameter, EditorRequired] public string Id { get; set; } = "";
    [Parameter] public bool ShowEditor { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public Func<TItem, bool>? IsOptionDisabled { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }
    [Parameter] public string? LabelClass { get; set; }
    [Parameter] public string? FieldCssClass { get; set; }
    [Parameter] public bool UseStyledCheckbox { get; set; }
    [Parameter] public bool IsInvalid { get; set; }
    [Parameter] public string? DescribedBy { get; set; }
    [Parameter, EditorRequired] public EventCallback<TItem> OnToggle { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string?> DisplayText { get; set; } = null!;

    /// <summary>
    /// One distinct id segment per entry of <see cref="Options"/>, positionally aligned with it — the
    /// <c>cbx-{Id}-{segment}</c> trailing part of each checkbox's <c>id</c>/<c>data-test-id</c> and of
    /// its label's <c>for</c>. Resolved per parameter cycle rather than inline per option because
    /// <c>EnumHelpers.ToUniqueIds</c> has to see the whole list to spot a collision: <c>ToId</c> strips
    /// non-ASCII, so an all-CJK option list sanitized every entry to the same empty segment and — per
    /// HTML's label-for resolution — every label then toggled the FIRST checkbox. An ordinary ASCII list
    /// is unaffected and renders the same ids as ever.
    /// </summary>
    string[] OptionIds { get; set; } = [];

    /// <summary>
    /// The same de-duplication over the selected <see cref="Value"/>s, for the read-only branch's
    /// per-item element ids — two selections that sanitize alike would otherwise render two elements
    /// sharing one id.
    /// </summary>
    string[] SelectedIds { get; set; } = [];

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        OptionIds = EnumHelpers.ToUniqueIds(Options);
        SelectedIds = Value is null || Value.Count == 0 ? [] : EnumHelpers.ToUniqueIds(Value);
    }
}
