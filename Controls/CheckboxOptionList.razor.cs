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
    [Parameter] public bool IsLabelHidden { get; set; }
    [Parameter, EditorRequired] public EventCallback<TItem> OnToggle { get; set; }
    [Parameter, EditorRequired] public Func<TItem, string?> DisplayText { get; set; } = null!;
}
