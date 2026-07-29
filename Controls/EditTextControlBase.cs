namespace Controls;

/// <summary>
/// Base class for the form controls that render a single native text-ish editor —
/// <see cref="EditString"/>, <see cref="EditTextArea"/>, <see cref="EditNumber{T}"/> and
/// <see cref="EditDateNative{T}"/>. Hoists the members all four declared as identical copies: the
/// <see cref="Size"/> hook, and the commit-event pair (<see cref="UpdateOn"/> plus the
/// <see cref="UpdateEventName"/> every one of their markup files binds to <c>@bind-value:event</c>)
/// whose per-control built-in fallback arrives through <see cref="DefaultUpdateTrigger"/>.
/// </summary>
/// <typeparam name="TValue">The bound value type, passed straight through to <see cref="EditControlBase{TValue}"/>.</typeparam>
/// <remarks>
/// <para>
/// The two string-bound members of the family (<see cref="EditString"/>, <see cref="EditTextArea"/>)
/// share considerably more than this — their whole clear/count/max-length surface — which lives one
/// level further down in <see cref="EditTextInputBase"/>.
/// </para>
/// <para>
/// Three things deliberately stay on the derived controls. <c>Placeholder</c>/
/// <c>EffectivePlaceholder</c> can't come up here: <see cref="EditDateNative{T}"/> intentionally
/// renders no placeholder (a native date input shows its own format hint instead), and hoisting the
/// pair would hand it a public parameter it ignores — so it lives in <see cref="EditTextInputBase"/>
/// for the two string controls, and <see cref="EditNumber{T}"/> keeps its own copy.
/// <c>ParsingErrorMessage</c> stays on <see cref="EditNumber{T}"/>/<see cref="EditDateNative{T}"/>
/// because their defaults differ ("must be a number" vs. "must be a date"); the parse body those two
/// shared moved to <see cref="EditControlInit.TryConvert{T}"/> instead, which is also where the
/// <see cref="DynamicallyAccessedMemberTypes.All"/> annotation their <c>T</c> needs for
/// <see cref="BindConverter"/> stops — this class's <typeparamref name="TValue"/> stays unannotated.
/// And each control's <c>UseAffixLayout</c>/<c>InputClass</c> one-liners stay put: every one passes
/// different arguments (a different base class token, a different subset of affix features), the
/// shared logic already sits in <see cref="EditInputShell"/>'s statics, so hoisting the call sites
/// would move one-liners without removing duplication.
/// </para>
/// </remarks>
public abstract class EditTextControlBase<TValue> : EditControlBase<TValue>
{
    /// <summary>
    /// Visual size, shared with the <c>Select</c> family's <see cref="SelectSize"/> (Default/Small/
    /// Large). Adds <c>edit-input-sm</c>/<c>edit-input-lg</c> to the editor's class in both legacy and
    /// affix mode, and to the shell's affix wrapper in affix mode (via <see cref="EditInputShell.WrapperClass"/>).
    /// Unthemed these are inert hooks -- the opt-in <c>.edit-theme</c> section is what actually sizes
    /// them. <see cref="SelectSize.Default"/> adds no class (byte-identical legacy DOM).
    /// </summary>
    /// <remarks>
    /// Two per-control notes survive the consolidation. For <see cref="EditTextArea"/> only
    /// padding/font change -- a textarea's height is never locked here
    /// (<see cref="EditTextArea.Rows"/>/<see cref="EditTextArea.AutoSize"/> still govern it). And
    /// <see cref="EditDateNative{T}"/> never enters the shell's affix mode (it declares no
    /// Prefix/Suffix/clear/count/password parameters), so it passes the wrapper class through for
    /// consistency but that class never actually renders.
    /// </remarks>
    [Parameter] public SelectSize Size { get; set; }

    /// <summary>
    /// Which DOM event commits keystrokes to <see cref="InputBase{TValue}.CurrentValue"/> --
    /// <see cref="UpdateTrigger.Input"/> (<c>oninput</c>) commits on every keystroke,
    /// <see cref="UpdateTrigger.Change"/> (<c>onchange</c>) commits on blur/Enter. Resolution order:
    /// this parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>, then the
    /// control's own <see cref="DefaultUpdateTrigger"/>.
    /// </summary>
    /// <remarks>
    /// That last fallback is where the family splits, and each control's
    /// <see cref="DefaultUpdateTrigger"/> override documents its own reasoning: the string editors
    /// default to <see cref="UpdateTrigger.Input"/>, while <see cref="EditNumber{T}"/> and
    /// <see cref="EditDateNative{T}"/> default to <see cref="UpdateTrigger.Change"/> because their
    /// native input types report an empty value mid-typing. See <see cref="EditTextArea.AutoSize"/>
    /// for how the resolved trigger interacts with auto-resizing.
    /// </remarks>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary>
    /// The control's own built-in commit trigger — the last fallback in <see cref="UpdateOn"/>'s
    /// resolution order, used when neither the parameter nor a cascaded
    /// <see cref="FormDefaults.EffectiveUpdateOn"/> supplies one. Abstract rather than defaulted
    /// because the answer genuinely differs per control and getting it wrong is a behavior change:
    /// each derived control states its choice (and why) explicitly.
    /// </summary>
    protected abstract UpdateTrigger DefaultUpdateTrigger { get; }

    /// <summary> The resolved DOM event name ("oninput" or "onchange") driving <c>@bind-value:event</c>, per <see cref="UpdateOn"/>'s resolution order.</summary>
    protected string UpdateEventName => ResolveUpdateEvent(UpdateOn, DefaultUpdateTrigger);
}
