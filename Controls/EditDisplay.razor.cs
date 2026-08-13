namespace Controls;

/// <summary>
/// Read-only display component for displaying text with styling and format being consistent with all other "Edit" controls
/// Useful in situations such as displaying combined values such "15.3 Ounces per can" double "volume" + enum "measurement type"
/// </summary>
/// <remarks>
/// Derives from <see cref="EditControlParametersBase"/> like <see cref="EditControlListBase{TItem}"/>/
/// <see cref="EditDateRange"/> rather than re-declaring its own copy of the shared parameter set --
/// the redeclaration used to drift from the base (see <see cref="ShouldHideLabel"/>'s remarks for the
/// bug that caused). <see cref="IEditControl.IsEditMode"/>/<see cref="IEditControl.IsDisabled"/>/
/// <see cref="IEditControl.Hiding"/> come along inert: this control has no field/editor of its own, so
/// none of the three has anything to affect.
/// </remarks>
public partial class EditDisplay : EditControlParametersBase
{
    /// <summary>Extra CSS class(es) appended to the displayed value element (alongside <c>edit-readonly-value</c>); use <see cref="EditControlParametersBase.ContainerClass"/> to style the wrapper instead.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>
    /// Unmatched attributes (e.g. a consumer's <c>style</c> or <c>data-*</c>), applied to the
    /// displayed value element. <c>style</c> merges with the component's own; the rest are splatted
    /// verbatim. A <c>class</c> attribute never lands here — parameter matching is case-insensitive,
    /// so it binds to <see cref="Class"/> (the two are the same knob).
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }
    /// <summary>The read-only text to display, styled like the other Edit controls' read-only values.</summary>
    [Parameter] public string Text { get; set; } = "";

    /// <summary>
    /// The fallback text rendered in place of <see cref="Text"/> when it's empty (LST-2) -- mirrors
    /// <see cref="ReadOnlyValue.EmptyText"/> (this control hand-builds its own read-only div rather
    /// than reusing that component -- see the markup's remarks). A parameter so a consumer can
    /// localize it. Defaults to "Not Set".
    /// </summary>
    [Parameter] public string EmptyText { get; set; } = "Not Set";

    // Same resolution as every other control (EditControlInit.ShouldHideLabel): the per-control
    // parameter or the cascaded form-wide setting. Not on EditControlParametersBase itself -- every
    // deriving control (EditControlListBase, EditDateRange) redeclares this one-liner rather than
    // sharing it, since the base has no notion of "the label" on its own. Previously the cascaded
    // FormOptions was declared but ignored here -- fixed before this control derived from the base at
    // all, and now trivially correct since FormOptions is the inherited cascading parameter.
    internal bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    // Resolved id used by the markup: explicit Id wins, then a Label-derived id, else a unique
    // fallback so label-less displays don't collide on an empty id (and the markup can omit
    // aria-labelledby rather than point it at an empty label). The composition itself is
    // AttributesHelper.GetId's (group name / IdPrefix prefixes, spaces stripped) over that base name
    // instead of a FieldIdentifier, so two "Status" displays in different groups don't collide.
    string _id = string.Empty;
    readonly string _fallbackId = $"ed-{Guid.NewGuid():N}";

    protected override void OnParametersSet() =>
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix,
            !string.IsNullOrEmpty(Label) ? Label.ToId() : _fallbackId);
}
