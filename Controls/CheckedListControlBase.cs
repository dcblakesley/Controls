namespace Controls;

/// <summary>
/// Base class for the checkbox-list form controls — <see cref="EditCheckedEnumList{TEnum}"/> and
/// <see cref="EditCheckedStringList"/>. Hoists the members the two declared identically: the layout
/// pair (<see cref="LabelClass"/>, <see cref="IsHorizontal"/>), the per-option
/// <see cref="IsOptionDisabled"/> predicate, and the <see cref="UseStyledCheckbox"/> opt-in plus the
/// <see cref="EffectiveUseStyledCheckbox"/> resolution both markup files pass to
/// <c>CheckboxOptionList</c>.
/// </summary>
/// <typeparam name="TItem">The type of each option / each item in the bound list.</typeparam>
/// <remarks>
/// Only the parameter surface is shared. Each control keeps its own option source (an enum's members
/// — optionally sorted, hence <c>EditCheckedEnumList.Sort</c> — vs. a consumer-supplied
/// <c>Options</c> list) and its own markup: the two <c>.razor</c> bodies differ in that one
/// projection plus the bound-value display line, which <c>CheckboxOptionList</c> already
/// parameterizes, so folding them into a further shared shell would buy nothing.
/// </remarks>
public abstract class CheckedListControlBase<TItem> : EditControlListBase<TItem>
{
    /// <summary> Labels for the checkboxes.</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <summary>
    /// Optional per-option disable predicate, called with each option being rendered. An option is
    /// disabled when this returns true OR the whole group's <c>IsDisabled</c> is true. Null
    /// (default) disables nothing beyond <c>IsDisabled</c>.
    /// </summary>
    [Parameter] public Func<TItem, bool>? IsOptionDisabled { get; set; }

    /// <summary>
    /// When true, each checkbox renders with a custom-drawn box (hidden native input + a sibling
    /// element that draws the visual state) instead of the bare native checkbox — same opt-in as
    /// <see cref="EditBool.UseStyledCheckbox"/>. Null (default) falls through to <see cref="FormOptions"/>,
    /// then any enclosing <see cref="Controls.FormDefaults"/>, then <see cref="FormOptions.DefaultUseStyledCheckbox"/>.
    /// </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the FormOptions/FormDefaults/static chain. </summary>
    protected bool EffectiveUseStyledCheckbox => EditControlInit.UseStyledCheckbox(UseStyledCheckbox, FormOptions, FormDefaults);
}
