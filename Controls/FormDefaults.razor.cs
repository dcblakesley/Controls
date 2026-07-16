namespace Controls;

/// <summary>
/// Render-tree-scoped defaults for the Edit* controls (plus the UI-kit <c>Table</c>'s
/// <c>UseStyledCheckbox</c> and the RCL's lazy-JS asset base, neither of which has a
/// <see cref="FormOptions"/> counterpart). Wrap an app root (or each micro-frontend's root) in this
/// component to set defaults for everything underneath it, instead of using the process-wide statics
/// on <see cref="FormOptions"/> — on Blazor Server every circuit shares those statics, and in MFE
/// hosts the composition root may not be yours to configure. Intended as set-once root configuration
/// (the cascade is fixed); resolution per setting: <see cref="FormOptions"/> instance value → this
/// component → the <see cref="FormOptions"/> static (or, for settings with no <see cref="FormOptions"/>
/// counterpart, the built-in default). Nesting chains per property: a setting an inner instance leaves
/// null falls through to the enclosing <see cref="FormDefaults"/> (host page defaults + MFE-root
/// overrides compose) before reaching that final fallback.
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

    /// <summary> Default for <see cref="FormOptions.UseStyledCheckbox"/> when the form doesn't set it —
    /// also read directly by the UI-kit <c>Table</c> (which has no <see cref="FormOptions"/> of its own).
    /// Null falls through to any enclosing <see cref="FormDefaults"/>, then to
    /// <see cref="FormOptions.DefaultUseStyledCheckbox"/>. </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="IsRequiredStarHidden"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveIsRequiredStarHidden => IsRequiredStarHidden ?? Outer?.EffectiveIsRequiredStarHidden;

    /// <summary> <see cref="ShowFieldNameInValidation"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveShowFieldNameInValidation => ShowFieldNameInValidation ?? Outer?.EffectiveShowFieldNameInValidation;

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveUseStyledCheckbox => UseStyledCheckbox ?? Outer?.EffectiveUseStyledCheckbox;

    /// <summary> Base URL prefixed onto the RCL's lazy <c>wss-*.js</c> module imports when set, so a
    /// render tree whose host page origin differs from the one serving <c>WssBlazorControls</c>'s
    /// static assets (e.g. a micro-frontend embedded into a host that doesn't serve/proxy them)
    /// resolves the import against the right origin instead of the browser default (which is
    /// <c>document.baseURI</c> — the host page). Must be absolute; a relative value would just
    /// re-resolve against the host document again. Null (default) preserves today's relative import
    /// path; null also falls through to any enclosing <see cref="FormDefaults"/>. </summary>
    [Parameter] public string? AssetBase { get; set; }

    /// <summary> <see cref="AssetBase"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public string? EffectiveAssetBase => AssetBase ?? Outer?.EffectiveAssetBase;

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
