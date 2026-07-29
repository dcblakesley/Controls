namespace Controls;

/// <summary>
/// Internal-use single-option row shared by <see cref="EditRadioEnum{TEnum}"/> and
/// <see cref="EditRadioString"/>: the Button-mode segmented item and the Default-mode list item.
/// Renders inside the host's own <c>InputRadioGroup</c>, so <see cref="InputRadio{TValue}"/> here
/// picks up that ambient group's cascading context same as if the host had rendered it directly.
/// Each host's "Other" option is genuinely different (a reused enum member vs. a synthetic sentinel
/// value) and stays in the host, not here.
/// </summary>
/// <remarks>
/// Takes the host control's id plus the option's already-de-duplicated <see cref="IdSuffix"/> and
/// composes the <c>rb-{id}-{option}</c> element id itself — the same division of labor
/// <c>CheckboxOptionList</c> (<c>cbx-{Id}-{option}</c>) and <c>SelectOptionList</c>
/// (<c>{Id}-option-{option}</c>) already use, so the id recipe has one authoring site instead of one
/// per host call site.
/// <para>
/// Unlike those two, the display label arrives pre-resolved as a plain <see cref="Display"/> string
/// rather than as a <c>Func&lt;TItem, string?&gt;</c> projection: they own the <c>foreach</c> over the
/// options and so must be handed a way to project each one, whereas this component renders exactly
/// one option that the host already has in hand.
/// </para>
/// </remarks>
public partial class RadioOptionItem<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TItem>
{
    [Parameter, EditorRequired] public TItem Value { get; set; } = default!;
    [Parameter] public bool IsButtonMode { get; set; }
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary>
    /// The <b>host control's</b> id (its <c>_id</c>), not the finished element id — see
    /// <see cref="OptionId"/> for what actually gets rendered.
    /// </summary>
    [Parameter, EditorRequired] public string Id { get; set; } = "";

    /// <summary>
    /// The trailing segment of <see cref="OptionId"/>. Both hosts pass it for every real option: the
    /// segment has to be de-duplicated across the whole option list
    /// (<c>EnumHelpers.ToUniqueIds</c>), which only the host — the one holding the list — can do.
    /// <see cref="EditRadioString"/> also passes the literal <c>"other"</c> for its built-in "Other"
    /// radio, whose <see cref="Value"/> is an internal sentinel that must never leak into a DOM id.
    /// Null falls back to deriving the segment from <see cref="Value"/> alone (no de-duplication).
    /// </summary>
    [Parameter] public string? IdSuffix { get; set; }

    [Parameter] public string? CssClass { get; set; }
    [Parameter] public string? LabelClass { get; set; }
    [Parameter] public string? Display { get; set; }

    /// <summary>
    /// The rendered <c>id</c> (also emitted as <c>data-test-id</c>, and referenced by the label's
    /// <c>for</c> in Button mode). Both hosts' test suites and visual baselines pin this
    /// <c>rb-{host id}-{option}</c> shape.
    /// </summary>
    string OptionId => $"rb-{Id}-{IdSuffix ?? Value.ToId()}";
}
