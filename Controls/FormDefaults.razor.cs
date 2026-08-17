namespace Controls;

/// <summary>
/// Render-tree-scoped defaults for the Edit* controls (plus the UI-kit <c>Table</c>'s
/// <c>UseStyledCheckbox</c>, the RCL's lazy-JS asset base, and each control's <see cref="UpdateTrigger"/>,
/// none of which has a <see cref="FormOptions"/> counterpart). Wrap an app root (or each micro-frontend's root) in this
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

    /// <summary> Default <see cref="UpdateTrigger"/> for controls that support it, when neither the
    /// control's own parameter nor an enclosing <see cref="FormDefaults"/> sets one — another setting
    /// with no <see cref="FormOptions"/> counterpart (like <see cref="AssetBase"/>), so the final
    /// fallback is each control's own built-in default rather than a <see cref="FormOptions"/> static.
    /// Null falls through to any enclosing <see cref="FormDefaults"/>, then to that per-control default. </summary>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary> <see cref="UpdateOn"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public UpdateTrigger? EffectiveUpdateOn => UpdateOn ?? Outer?.EffectiveUpdateOn;

    /// <summary>
    /// When true, the first form field rendered beneath this component takes keyboard focus once,
    /// after this component's first render — the form-level counterpart to a single control's
    /// <c>FocusOnFirstRender</c>, and the reason it lives here rather than on a control: "first" then
    /// follows the markup instead of being pinned to whichever control happens to be written first,
    /// so reordering fields can't silently move the focus. Default (null/false) is OFF — nothing is
    /// ever focused unless a scope asks for it. Null falls through to any enclosing
    /// <see cref="FormDefaults"/>; there is no <see cref="FormOptions"/> counterpart and no static,
    /// so an unset chain simply means off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Resolution of "first" is document order, decided in the browser</b> from the DOM between
    /// the two <c>&lt;template&gt;</c> markers this component renders while the feature is on (see
    /// <see cref="JsInteropEc.FocusFirstField"/>). Fields that can't or shouldn't take focus are
    /// skipped: disabled, <c>readonly</c>, <c>tabindex="-1"</c>, anything inside an <c>inert</c> or
    /// <c>aria-hidden</c> subtree, and anything not actually rendered/visible (a
    /// <see cref="HidingMode"/>-hidden control, a collapsed panel). Buttons and links are not fields
    /// and are never targets.
    /// </para>
    /// <para>
    /// <b>An explicit <c>FocusOnFirstRender="true"</c> on a specific control wins.</b> The JS side
    /// declines to move focus when a field already holds it, so whichever of the two runs first, the
    /// named control ends up focused — the same deference <c>wss-overlay.js</c> already shows an
    /// in-dialog focus request. That guard also stops a second armed scope on the page from stealing
    /// focus from the first, and stops any scope from stealing it from the user.
    /// </para>
    /// <para>
    /// <b>Once per instance, on first render</b> — not on re-render, and not again when a value or
    /// the validation state changes. Each <see cref="FormDefaults"/> whose resolved value is true
    /// arms its own scope, which is what makes the dialog case work: a <see cref="FormDefaults"/>
    /// inside a <c>Modal</c>/<c>Drawer</c> first renders when the dialog opens, so that is when it
    /// focuses. An enclosing scope that already fired at page load is not re-triggered by the open.
    /// </para>
    /// <para>
    /// <b>Accessibility.</b> Moving focus on open is helpful in a focused dialog or a search-first
    /// page and harmful on a long page where the form isn't the main content: it can drop a screen
    /// reader user past the heading and context they were about to hear, and on a touch device it
    /// pops the soft keyboard over the content. That is exactly why this is opt-in per scope rather
    /// than a library-wide default — put it on the dialog or the form, not reflexively on the app root.
    /// </para>
    /// </remarks>
    [Parameter] public bool? FocusFirstField { get; set; }

    /// <summary> <see cref="FocusFirstField"/> resolved through the chain of enclosing
    /// <see cref="FormDefaults"/> instances. Null only when no instance in the chain sets it. </summary>
    public bool? EffectiveFocusFirstField => FocusFirstField ?? Outer?.EffectiveFocusFirstField;

    /// <summary> Suffix appended to <see cref="_focusScopeId"/> for the scope's closing marker; the JS
    /// side rebuilds the same name, so only one id crosses the interop boundary. </summary>
    internal const string FocusScopeEndSuffix = "-end";

    // Non-null only while this instance's scope is armed, and it is ALSO the armed flag: the markers
    // render on it and OnAfterRenderAsync fires on it, so the DOM anchor and the interop call can
    // never disagree about whether the feature is on. Decided once in OnInitialized rather than per
    // render because FormDefaults is set-once root configuration (its own cascade is IsFixed) and
    // because the markers must not appear/disappear under the diff on an unrelated parameter change.
    string? _focusScopeId;

    // A Guid, not a counter: several FormDefaults can be armed on one page (an MFE root plus a dialog's
    // own), and the marker ids have to be unique document-wide with no shared mutable state to
    // coordinate through -- a static counter is exactly the process-wide state this component exists
    // to avoid (one Blazor Server process serves every circuit).
    protected override void OnInitialized()
    {
        if (EffectiveFocusFirstField == true)
            _focusScopeId = $"wss-focus-scope-{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Fires <see cref="FocusFirstField"/> exactly once, after this component's first render — by
    /// which point the whole render batch (this component's children included) has been applied to
    /// the DOM, so the query the JS side runs sees the finished form.
    /// </summary>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _focusScopeId is not null)
            await JsInteropEc.FocusFirstField(JsRuntime, _focusScopeId, this);
    }

    // Only the FocusFirstField path uses this. Resolved unconditionally because [Inject] is not
    // conditional, which is harmless: IJSRuntime is registered by every Blazor host (including the
    // static-SSR one, whose implementation throws on use -- and every call here is best-effort).
    [Inject] IJSRuntime JsRuntime { get; set; } = default!;

    [Parameter] public RenderFragment? ChildContent { get; set; }
}
