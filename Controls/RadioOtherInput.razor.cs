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
    /// <remarks>
    /// RAD-8: an external change to the bound value (a parent record swap, a form reset, a sibling
    /// field's side effect) can flip this true while the user is mid-type here, with the caret still
    /// in the box. Applying native <c>disabled</c> to a focused element forces an immediate,
    /// unconditional browser blur — with nothing else in this row to receive it, focus silently drops
    /// to <c>&lt;body&gt;</c>. This component defends against that itself: see
    /// <see cref="NativelyDisabled"/>.
    /// </remarks>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary> Optional placeholder text; <c>null</c> renders no attribute at all. </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// Default accessible name (RAD-4) for when neither host overrides it via its own
    /// <c>OtherAriaLabel</c> parameter (<see cref="EditRadioEnum{TEnum}.OtherAriaLabel"/> /
    /// <see cref="EditRadioString.OtherAriaLabel"/>). Exposed so both hosts share one literal instead
    /// of duplicating it at each null-coalesce call site.
    /// </summary>
    public const string DefaultAriaLabel = "Custom text value input";

    /// <summary>
    /// The free-text box's accessible name. RAD-4: this used to be the hard-coded literal
    /// <see cref="DefaultAriaLabel"/> with no parameter, no localization, and no tie back to the
    /// field or "Other" option it belongs to — both hosts now forward their own optional
    /// <c>OtherAriaLabel</c> override here, falling back to <see cref="DefaultAriaLabel"/>.
    /// </summary>
    [Parameter] public string AriaLabel { get; set; } = DefaultAriaLabel;

    /// <summary>
    /// Single-entry map of the host's resolved DOM event name to its commit handler — see the
    /// remarks on this class for why the wiring arrives as a splat instead of a binder.
    /// </summary>
    [Parameter, EditorRequired] public IReadOnlyDictionary<string, object> CommitAttributes { get; set; } = new Dictionary<string, object>(0);

    // ----- RAD-8: focus preservation --------------------------------------------------------------

    // Tracked purely from this element's own onfocus/onblur -- no JS interop needed. Starts (and stays)
    // false in every existing bUnit test, none of which simulate real browser focus, so this changes
    // nothing about the box's native `disabled` rendering for any scenario already covered.
    bool _isFocused;

    /// <summary>
    /// The focus-preservation half of RAD-8: while this box has focus, <see cref="IsDisabled"/>
    /// flipping true no longer applies the native <c>disabled</c> attribute (which would force an
    /// unconditional browser blur to <c>&lt;body&gt;</c>) — it stays focusable, marked
    /// <c>readonly</c> + <c>aria-disabled="true"</c> instead, so it reads and behaves as inert without
    /// the forced blur. The moment the user tabs away on their own, <c>_isFocused</c> flips false on
    /// the next render and native <c>disabled</c> applies with nothing left to blur.
    /// </summary>
    bool NativelyDisabled => IsDisabled && !_isFocused;

    /// <summary>
    /// The other half of the same state: logically disabled but currently focused, so it renders
    /// <c>readonly</c> + <c>aria-disabled="true"</c> instead of <see cref="NativelyDisabled"/>'s
    /// native attribute. Equivalent to <c>IsDisabled &amp;&amp; _isFocused</c> -- named so the markup
    /// states the intent once instead of re-deriving it at each of the two attribute sites.
    /// </summary>
    bool InertButFocused => IsDisabled && !NativelyDisabled;

    void OnFocus() => _isFocused = true;
    void OnBlur() => _isFocused = false;
}
