namespace Controls;

/// <summary>
/// Render-tree-scoped defaults for the Edit* controls. Wrap an app root (or each micro-frontend's
/// root) in this component to set defaults for every form underneath it, instead of using the
/// process-wide statics on <see cref="FormOptions"/> — on Blazor Server every circuit shares those
/// statics, and in MFE hosts the composition root may not be yours to configure. Intended as
/// set-once root configuration (the cascade is fixed); resolution per setting:
/// <see cref="FormOptions"/> instance value → this component → the <see cref="FormOptions"/> static.
/// </summary>
public partial class FormDefaults
{
    /// <summary> Default for <see cref="FormOptions.IsRequiredStarHidden"/> when the form doesn't set it.
    /// Null falls through to <see cref="FormOptions.DefaultIsRequiredStarHidden"/>. </summary>
    [Parameter] public bool? IsRequiredStarHidden { get; set; }

    /// <summary> Default for <see cref="FormOptions.ShowFieldNameInValidation"/> when the form doesn't set it.
    /// Null falls through to <see cref="FormOptions.DefaultShowFieldNameInValidation"/>. </summary>
    [Parameter] public bool? ShowFieldNameInValidation { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
