namespace Controls;

/// <summary>
/// Base class for the radio-group form controls — <see cref="EditRadioEnum{TEnum}"/> and
/// <see cref="EditRadioString"/>. Hoists the parameters and computed values the two declared as
/// byte-identical copies: the layout pair (<see cref="IsHorizontal"/>, <see cref="LabelClass"/>),
/// Ant Design's segmented-button trio (<see cref="OptionType"/>, <see cref="ButtonStyle"/>,
/// <see cref="Size"/>) plus the <see cref="ButtonGroupClass"/> it resolves to, and the "Other"
/// free-text box's commit-event choice (<see cref="UpdateOn"/> plus <see cref="UpdateEventName"/>).
/// </summary>
/// <typeparam name="TValue">The bound value type, passed straight through to <see cref="EditControlBase{TValue}"/>.</typeparam>
/// <remarks>
/// <para>
/// Two things deliberately stay on the derived controls. Each control's <c>IsOptionDisabled</c>
/// predicate can't move: <see cref="EditRadioEnum{TEnum}"/> declares
/// <c>Func&lt;TEnum, bool&gt;?</c> while inheriting <c>EditControlBase&lt;TEnum?&gt;</c>, so the
/// predicate's type argument isn't this class's <typeparamref name="TValue"/> and the two
/// declarations aren't the same member. The "Other" halves differ outright — a reused enum member
/// with a separate <c>OtherValue</c>/<c>OtherValueChanged</c> pair, vs. a synthetic sentinel whose
/// typed text <i>is</i> the bound value — so only the event-name plumbing above is shared.
/// </para>
/// <para>
/// <c>EditRadio&lt;TValue&gt;</c> is unrelated to this base: it must inherit
/// <c>InputRadioGroup&lt;TValue&gt;</c> so its consumer-authored child <c>InputRadio</c>s get the
/// cascading context, and is a documented exception to the base-class convention.
/// </para>
/// </remarks>
public abstract class RadioGroupControlBase<TValue> : EditControlBase<TValue>
{
    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary> The labels around each radio button</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary>
    /// Rendering mode, mirroring Ant Design's <c>Radio.Group optionType</c>. Defaults to
    /// <see cref="RadioOptionType.Default"/> (the plain-radio markup). <see cref="RadioOptionType.Button"/>
    /// renders AntD's segmented "button" look — the same <c>InputRadio</c>/keyboard semantics, styled
    /// as joined bordered buttons. Inherently horizontal: <see cref="IsHorizontal"/> is ignored in
    /// button mode. Composes with the control's own "Other" option (the Other button joins the row;
    /// its free-text input still renders as a normal input below) and its <c>IsOptionDisabled</c>
    /// predicate.
    /// </summary>
    [Parameter] public RadioOptionType OptionType { get; set; } = RadioOptionType.Default;

    /// <summary>
    /// Checked-button coloring in <see cref="RadioOptionType.Button"/> mode (no effect otherwise),
    /// mirroring Ant Design's <c>Radio.Group buttonStyle</c>. Defaults to <see cref="RadioButtonStyle.Outline"/>.
    /// </summary>
    [Parameter] public RadioButtonStyle ButtonStyle { get; set; } = RadioButtonStyle.Outline;

    /// <summary>
    /// Button size in <see cref="RadioOptionType.Button"/> mode (no effect otherwise) — reuses the
    /// <see cref="SelectSize"/> the Select/EditString family already shares. Defaults to <see cref="SelectSize.Default"/>.
    /// </summary>
    [Parameter] public SelectSize Size { get; set; } = SelectSize.Default;

    /// <summary>
    /// Which DOM event commits the "Other" free-text box's typed value --
    /// <see cref="UpdateTrigger.Input"/> (<c>oninput</c>) commits on every keystroke,
    /// <see cref="UpdateTrigger.Change"/> (<c>onchange</c>) commits on blur/Enter. Affects ONLY the
    /// "Other" free-text box -- the radio buttons themselves always commit on selection (native radio
    /// <c>onchange</c>) and are unaffected. Resolution order: this parameter, then the cascaded
    /// <see cref="FormDefaults.EffectiveUpdateOn"/>, then this control's own default of
    /// <see cref="UpdateTrigger.Input"/>.
    /// </summary>
    /// <remarks>
    /// Where the committed text lands differs per control: <see cref="EditRadioEnum{TEnum}"/> raises
    /// its separate <c>OtherValueChanged</c> callback, while <see cref="EditRadioString"/> writes
    /// straight to <see cref="InputBase{TValue}.CurrentValue"/> (the typed text IS its bound value).
    /// </remarks>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary> The resolved DOM event name ("oninput" or "onchange") for the "Other" text box, per <see cref="UpdateOn"/>'s resolution order.</summary>
    protected string UpdateEventName => ResolveUpdateEvent(UpdateOn, UpdateTrigger.Input);

    /// <summary>
    /// The <see cref="RadioOptionType.Button"/> wrapper's root class list. Both radio controls render
    /// this one computation, so the base class + solid modifier + size class assembly can't drift
    /// between them.
    /// </summary>
    protected string ButtonGroupClass => RadioButtonGroup.GroupClass(ButtonStyle, Size);
}
