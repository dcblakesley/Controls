namespace Controls;

/// <summary>
/// Render-tree-scoped defaults for the Edit* controls. Wrap an app root (or each micro-frontend's
/// root) in this component to set defaults for every form underneath it, instead of using the
/// process-wide statics on <see cref="FormOptions"/> — on Blazor Server every circuit shares those
/// statics, and in MFE hosts the composition root may not be yours to configure. Intended as
/// set-once root configuration (the cascade is fixed); resolution per setting:
/// <see cref="FormOptions"/> instance value → this component → the <see cref="FormOptions"/> static.
/// Nesting chains per property: a setting an inner instance leaves null falls through to the
/// enclosing <see cref="FormDefaults"/> (host page defaults + MFE-root overrides compose) before
/// reaching the static.
/// </summary>
public partial class FormDefaults
{
    // The enclosing FormDefaults when nested. An inner instance must not shadow the outer one
    // whole-hog — each unset property falls through to it (see the Effective* accessors).
    [CascadingParameter] FormDefaults? Outer { get; set; }

    /// <summary> Default for <see cref="FormOptions.IsRequiredStarHidden"/> when the form doesn't set it.
    /// Null falls through to any enclosing <see cref="FormDefaults"/>, then to
    /// <see cref="FormOptions.DefaultIsRequiredStarHidden"/>. </summary>
    [Parameter] public bool? IsRequiredStarHidden { get; set; }

    /// <summary> Default for <see cref="FormOptions.ShowFieldNameInValidation"/> when the form doesn't set it.
    /// Null falls through to any enclosing <see cref="FormDefaults"/>, then to
    /// <see cref="FormOptions.DefaultShowFieldNameInValidation"/>. </summary>
    [Parameter] public bool? ShowFieldNameInValidation { get; set; }

    /// <summary> <see cref="IsRequiredStarHidden"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveIsRequiredStarHidden => IsRequiredStarHidden ?? Outer?.EffectiveIsRequiredStarHidden;

    /// <summary> <see cref="ShowFieldNameInValidation"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveShowFieldNameInValidation => ShowFieldNameInValidation ?? Outer?.EffectiveShowFieldNameInValidation;

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
