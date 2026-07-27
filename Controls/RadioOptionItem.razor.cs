namespace Controls;

/// <summary>
/// Internal-use single-option row shared by <see cref="EditRadioEnum{TEnum}"/> and
/// <see cref="EditRadioString"/>: the Button-mode segmented item and the Default-mode list item.
/// Renders inside the host's own <c>InputRadioGroup</c>, so <see cref="InputRadio{TValue}"/> here
/// picks up that ambient group's cascading context same as if the host had rendered it directly.
/// Each host's "Other" option is genuinely different (a reused enum member vs. a synthetic sentinel
/// value) and stays in the host, not here.
/// </summary>
public partial class RadioOptionItem<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>
{
    [Parameter, EditorRequired] public TItem Value { get; set; } = default!;
    [Parameter] public bool IsButtonMode { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter, EditorRequired] public string Id { get; set; } = "";
    [Parameter] public string? CssClass { get; set; }
    [Parameter] public string? LabelClass { get; set; }
    [Parameter] public string? Display { get; set; }
}
