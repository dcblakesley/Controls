namespace Controls;

/// <summary> Edit control for selecting a value using radio buttons. Create options within the markup using InputRadio components.</summary>
/// <remarks>
/// Inherits <see cref="InputRadioGroup{TValue}"/> directly rather than the shared
/// <c>EditControlBase&lt;T&gt;</c> because <c>EditRadio</c>'s public API takes
/// <see cref="InputRadio{TValue}"/> children as <c>ChildContent</c>, and those children resolve a
/// cascading <c>InputRadioContext</c> that only <see cref="InputRadioGroup{TValue}"/> supplies.
/// Replacing the base would require a parallel <c>&lt;InputRadio&gt;</c> API and break every
/// consumer — see README §10.2.0 for the intentional design. The sibling controls
/// <c>EditRadioEnum</c> and <c>EditRadioString</c> render their own <c>&lt;input type="radio"&gt;</c>
/// markup and never see <c>InputRadio</c> children, so they inherit <c>EditControlBase</c> normally.
/// </remarks>
public partial class EditRadio<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TValue> : InputRadioGroup<TValue>, IEditControl
{
    // Injected only for FocusAsync's group-focus call below -- this control renders no JS-driven
    // behavior of its own.
    [Inject] IJSRuntime JS { get; set; } = default!;

    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    /// <summary>
    /// The enclosing <see cref="Controls.FormDefaults"/>, if any. Read only so
    /// <see cref="JsInteropEc.FocusGroupInput"/> can resolve a lazy <c>edit-controls.js</c> re-import
    /// against the right origin in the cross-origin micro-frontend case; this control derives no
    /// defaults from it.
    /// </summary>
    [CascadingParameter] public FormDefaults? FormDefaults { get; set; }

    // IEditControl interface properties
    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }

    /// <inheritdoc cref="EditControlBase{TValue}.LabelContent"/>
    [Parameter] public RenderFragment? LabelContent { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; } 
    /// <inheritdoc/>
    [Parameter] public bool? IsRequired { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }

    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    /// <inheritdoc cref="EditControlBase{TValue}.AutoFocus"/>
    [Parameter] public bool AutoFocus { get; set; }

    string _id = string.Empty;
    string? _isRequired;
    FieldIdentifier _fieldIdentifier;
    List<Attribute>? _attributes;
    string _errorMsgId = string.Empty;
    string _describedBy = string.Empty;

    // The fully-resolved required-ness (IsRequired param → [Required] → RequiredResolver), passed
    // to FormLabel so the star and aria-required share one computation site and can never disagree.
    bool? IsRequiredResolved => _isRequired is not null;

    /// <summary>
    /// True when this field currently has a validation error. Read from the EditContext's messages
    /// rather than substring-matching CssClass (which also contains the consumer's class attribute).
    /// Guarded on a null <c>EditContext</c> so a standalone <c>EditRadio</c> (no surrounding
    /// <c>EditForm</c>) doesn't NRE — <see cref="InputRadioGroup{TValue}"/> supports it since .NET 8.
    /// </summary>
    bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(FieldIdentifier).Any();

    // The inner InputRadioGroup can't use @bind-Value here because an explicit ValueExpression
    // alongside @bind-Value is a Razor compile error — so the change handler is spelled out.
    void OnGroupValueChanged(TValue? value) => CurrentValue = value;

    protected override void OnInitialized()
    {
        // Chained for the same reason the control bases document it (and the same reason this control's
        // OnParametersSet already chains): InputRadioGroup owns the group-name/context setup, so an
        // unchained override here is exactly the "silently skipped base init" the library warns about.
        base.OnInitialized();
        var accessor = EditControlInit.RequireBinding(ValueExpression, this);
        // Resolve + register in one call, shared with the two control bases: registering here (rather
        // than relying on FieldValidationDisplay) is what makes the field survive HidingMode so links
        // from the validation summary always work. Paired with the Dispose override below — see
        // EditControlInit.RegisterField's remarks.
        (_id, _attributes, _fieldIdentifier) = EditControlInit.InitAndRegister(accessor, this, FormOptions, FormGroupOptions);
        RefreshAriaState();
    }

    // InputRadioGroup uses OnParametersSet to set up the group name/context — call base first, then
    // re-resolve the element id (a runtime Id/IdPrefix change; see EditControlInit.SyncResolvedId)
    // and the cached ARIA state, so a runtime Description/Tooltip/label change is reflected too.
    // _attributes is the "init completed" flag here, as it is for RefreshAriaState below.
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_attributes is not null)
            EditControlInit.SyncResolvedId(ref _id, this, FormOptions, FormGroupOptions, _fieldIdentifier);
        RefreshAriaState();
    }

    // Mirrors EditControlBase.RefreshAriaState: aria-required plus the error-msg id and
    // aria-describedby token list through the one shared helper. No-op until OnInitialized has run —
    // _attributes is null before then.
    void RefreshAriaState()
    {
        if (_attributes is null) return;
        (_isRequired, _errorMsgId, _describedBy) =
            EditControlInit.ResolveAriaState(this, FormOptions, _id, _attributes, _fieldIdentifier);
    }

    /// <summary>
    /// Drops the field registration <see cref="OnInitialized"/> added — the same pairing
    /// <c>EditControlBase&lt;T&gt;</c> gets from its own override, spelled out here because this
    /// control inherits <see cref="InputRadioGroup{TValue}"/> instead. Without it a removed control
    /// (behind a conditional <c>@if</c>) leaves a dead <see cref="FieldIdentifier"/> in the long-lived
    /// <see cref="FormOptions"/> for the validation summary to link to.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            EditControlInit.UnregisterField(FormOptions, _fieldIdentifier, this);
        base.Dispose(disposing);
    }

    // ───────────────────────────── programmatic focus ─────────────────────────────

    /// <inheritdoc cref="EditControlBase{TValue}.FocusAsync"/>
    /// <remarks>
    /// Focuses the CHECKED radio when the group has a selection, else the first enabled one — which is
    /// what a Tab into a real radiogroup does. Routed through
    /// <see cref="JsInteropEc.FocusGroupInput"/> rather than an <see cref="ElementReference"/>, because
    /// this control's radios are <see cref="InputRadio{TValue}"/> children the CONSUMER authors: there
    /// is no element for <c>@ref</c> to bind and no id to compute, so resolving the option inside the
    /// fieldset (which already carries this control's own id — see <c>RadioAria.Fieldset</c>) is the
    /// only channel that reaches them. <c>EditRadioEnum</c>/<c>EditRadioString</c>/
    /// <c>EditBoolNullRadio</c> deliberately share it, so all four radio groups can't disagree about
    /// which radio "focus the group" means. Best-effort like every other <c>FocusAsync</c>: no-op in
    /// read-only mode (no fieldset id renders), when every option is disabled, or with no JS
    /// (prerender / tests).
    /// </remarks>
    public ValueTask FocusAsync() =>
        new(JsInteropEc.FocusGroupInput(JS, _id, "input[type=radio]", preferChecked: true, FormDefaults));

    /// <inheritdoc cref="EditControlBase{TValue}.OnAfterRenderAsync"/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (firstRender && AutoFocus) await FocusAsync();
    }

    bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);
    bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    protected bool ShouldShowComponent()
    {
        var value = Value;
        var isNull = value == null;
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull,
            isNull || EqualityComparer<TValue>.Default.Equals(value, default));
    }
}
