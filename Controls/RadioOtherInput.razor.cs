namespace Controls;

/// <summary>
/// Internal-use "Other" free-text box shared by <see cref="EditRadioEnum{TEnum}"/> and
/// <see cref="EditRadioString"/> — the sibling of <see cref="RadioOptionItem{TItem}"/> for the one
/// element both hosts render at the end of their option list. It owns exactly what the two hosts
/// must keep identical: the input's classes (<c>edit-input edit-radio-other-input</c> — the second
/// is what carries the padding/border/radius/<c>min-width</c> and the <c>:disabled</c> affordance),
/// its accessible name, and its <c>disabled</c> wiring. That used to be duplicated markup in both
/// controls and had drifted: EditRadioString's copy still said <c>edit-string-input</c> (an empty
/// rule), so its Other box rendered with no border and no disabled affordance while EditRadioEnum's
/// looked correct.
/// <para>
/// Everything genuinely per-host stays a parameter, because each host's public contract pins it:
/// the DOM id (<c>other-{id}</c> vs. <c>txt-{id}-custom-value</c>), the placeholder (EditRadioEnum
/// exposes <c>OtherPlaceholder</c>; EditRadioString deliberately has no placeholder parameter), and
/// the commit wiring. The per-option wrapper also stays in the hosts — EditRadioEnum's Default-mode
/// wrapper holds the last radio *and* this input, so it can't move in here.
/// </para>
/// </summary>
/// <remarks>
/// <see cref="CommitAttributes"/> is a pre-built splattable dictionary rather than a
/// value/<c>EventCallback</c> pair because both hosts must bind their commit handler to an event name
/// that is only known at runtime (<c>UpdateOn</c> resolves to "oninput" or "onchange"). A child
/// component's markup can't express that with <c>@bind:event</c> using data handed to it, whereas an
/// <c>@attributes</c> splat can — which is the mechanism EditRadioEnum already used. Passing the
/// dictionary therefore keeps EditRadioEnum's commit path byte-identical and lets EditRadioString
/// reuse the same element.
/// </remarks>
public partial class RadioOtherInput
{
    /// <summary>
    /// DOM id, also emitted as <c>data-test-id</c>. Each host keeps its own long-standing id scheme
    /// (both are load-bearing in the test suites), so this is never derived here.
    /// </summary>
    [Parameter, EditorRequired] public string Id { get; set; } = "";

    /// <summary> The current free-text value, rendered into the <c>value</c> attribute. </summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>
    /// True when the box must be inert — the "Other" option isn't the selected one, or that option
    /// (or the whole group) is disabled. Each host computes this itself; the hosts' notions of
    /// "the Other option" differ (a reused enum member vs. a synthetic sentinel).
    /// </summary>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary> Optional placeholder text; <c>null</c> renders no attribute at all. </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// Single-entry map of the host's resolved DOM event name to its commit handler — see the
    /// remarks on this class for why the wiring arrives as a splat instead of a binder.
    /// </summary>
    [Parameter, EditorRequired] public IReadOnlyDictionary<string, object> CommitAttributes { get; set; } = new Dictionary<string, object>(0);
}
