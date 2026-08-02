namespace Controls;

/// <summary>
/// Base class for edit controls that bind to a single value. Hoists the <see cref="IEditControl"/>
/// parameters, the <see cref="FormOptions"/> / <see cref="FormGroupOptions"/> cascading parameters,
/// and the standard derived state (<c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>,
/// <c>_fieldIdentifier</c>) so derived controls only declare component-specific parameters and markup.
/// </summary>
/// <remarks>
/// <para>
/// Derived classes must implement <see cref="InputBase{TValue}.TryParseValueFromString"/> — replacing
/// the parsing that Microsoft's <c>Input*</c> classes used to provide. A control that never parses a
/// string (e.g. <c>EditBool</c>, which assigns <see cref="InputBase{TValue}.CurrentValue"/> directly)
/// throws <see cref="NotSupportedException"/> from it instead.
/// </para>
/// <para>
/// Initialization needs no code in the derived control at all: <c>@bind-Value</c> supplies
/// <see cref="InputBase{TValue}.ValueExpression"/>, and this class's <see cref="OnInitialized"/> feeds
/// it to <see cref="InitState"/>. Override <see cref="OnInitialized"/> only for extra init of the
/// control's own (e.g. <c>EditRadioEnum</c>'s option cache) — and then call <c>base.OnInitialized()</c>
/// first, so the extra work sees the derived state already populated. Forgetting the call is the whole
/// reason the wiring lives here: a control that skipped it rendered silently broken (empty <c>_id</c>,
/// no model attributes, no field registration) rather than failing loudly.
/// </para>
/// <para>
/// Derived controls do still each declare an inert <c>Field</c> parameter marked
/// <c>[Obsolete(error: true)]</c>. It is a compile-time guard only — nothing reads it and nothing
/// passes it to <see cref="InitState"/> — so that a leftover <c>Field="..."</c> attribute in consumer
/// markup is a build error rather than an unmatched-parameter throw at first render.
/// </para>
/// </remarks>
public abstract class EditControlBase<TValue> : InputBase<TValue>, IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    [CascadingParameter] public FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
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
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Standard derived state — populated by InitState, which OnInitialized below calls.
    protected string _id = string.Empty;
    protected string? _isRequired;
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;
    // Cached ARIA references — resolved in InitState and re-resolved each OnParametersSet (see BuildDescribedBy).
    protected string _errorMsgId = string.Empty;
    protected string _describedBy = string.Empty;

    // False until InitState has run to completion — see Dispose.
    bool _stateInitialized;

    /// <summary>
    /// The control's fully-resolved required-ness (IsRequired parameter → [Required] attribute →
    /// FormOptions.RequiredResolver), recomputed alongside <c>_isRequired</c> each parameter cycle.
    /// Markup passes THIS to FormLabel's IsRequired (an explicit value wins outright there), so the
    /// star and <c>aria-required</c> share one computation site and can never disagree — FormLabel's
    /// own derivation path remains only for standalone use (e.g. EditDisplay).
    /// </summary>
    protected bool? IsRequiredResolved => _isRequired is not null;

    /// <summary>
    /// True when this field currently has a validation error. Read from the EditContext's messages
    /// rather than substring-matching <see cref="InputBase{TValue}.CssClass"/> — CssClass also
    /// contains the consumer's <c>class</c> attribute, so a class like "invalid-style-fix" was a
    /// permanent false positive (aria-invalid + red X). Guarded on a null <c>EditContext</c> because
    /// <see cref="InputBase{TValue}"/> supports standalone use (no surrounding <c>EditForm</c>) since
    /// .NET 8 — no context means no validation, so no error.
    /// </summary>
    protected bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(FieldIdentifier).Any();

    /// <summary>
    /// This control's name for diagnostics — <c>EditNumber</c>, not the CLR's <c>EditNumber`1</c>.
    /// </summary>
    /// <remarks>
    /// Not <c>virtual</c>: nothing needs a name other than its own type's, and the trimmed
    /// <c>GetType().Name</c> reproduces the per-control <c>nameof(EditNumber&lt;T&gt;)</c> the hoisted
    /// exception message below used to be written with, character for character — so there is nothing
    /// for a derived control to correct. See <see cref="EditControlInit.ControlName"/>.
    /// </remarks>
    protected string ControlName => EditControlInit.ControlName(this);

    /// <summary>
    /// Wires the standard derived state for every scalar control, so a derived control needs no
    /// <c>OnInitialized</c> of its own unless it has extra init to run (in which case it calls
    /// <c>base.OnInitialized()</c> first — see this class's remarks).
    /// </summary>
    protected override void OnInitialized()
    {
        // Chains to InputBase even though it doesn't override this today — every control's own
        // OnInitialized did, and hoisting them here must not quietly drop the call.
        base.OnInitialized();
        InitState(EditControlInit.RequireBinding(ValueExpression, this));
    }

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c> from
    /// the control's bound accessor (<see cref="InputBase{TValue}.ValueExpression"/>, which
    /// <c>@bind-Value</c> supplies), and registers the field with
    /// <see cref="FormOptions.FieldIdentifiers"/> so the validation summary can link to it.
    /// Called by <see cref="OnInitialized"/>; a derived control never calls it itself.
    /// </summary>
    /// <remarks>
    /// Registration used to live in <c>FieldValidationDisplay.OnInitialized</c>, but that
    /// component is rendered conditionally (inside <c>@if (ShouldShowComponent())</c>) — so
    /// hidden fields silently never registered, and the validation summary couldn't link to them.
    /// Registering here happens once per control init and survives any HidingMode setting; the
    /// paired unregister lives in <see cref="Dispose(bool)"/>.
    /// </remarks>
    protected void InitState(Expression<Func<TValue>> field)
    {
        // Resolve + register in one call, shared with the list base and EditRadio — the registration
        // is paired with the Dispose override below (see EditControlInit.RegisterField's remarks).
        (_id, _attributes, _fieldIdentifier) = EditControlInit.InitAndRegister(field, this, FormOptions, FormGroupOptions);
        _stateInitialized = true;
        RefreshAriaState();
    }

    /// <summary>
    /// Re-resolves the cached ARIA state on parameter change (e.g. a runtime Description/Tooltip
    /// or label-hidden toggle) so aria-describedby stays accurate and never dangles.
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        RefreshAriaState();
    }

    // aria-required plus the error-msg id and aria-describedby token list, all through the one shared
    // helper (required-ness resolves as IsRequired param → [Required] attribute →
    // FormOptions.RequiredResolver, so aria-required always matches the FormLabel star). No-op until
    // InitState has run — _attributes is null before then.
    void RefreshAriaState()
    {
        if (_attributes is null) return;
        (_isRequired, _errorMsgId, _describedBy) =
            EditControlInit.ResolveAriaState(this, FormOptions, _id, _attributes, _fieldIdentifier);
    }

    /// <summary>
    /// Drops the field registration <see cref="InitState"/> added, so a control removed from the
    /// render tree (e.g. behind a conditional <c>@if</c>) doesn't leave stale state for the validation
    /// summary to link to — <see cref="FormOptions"/> is per-form and long-lived, so an unpaired
    /// registration also grows <see cref="FormOptions.FieldIdentifiers"/> on every mount/unmount cycle.
    /// Mirrors <see cref="EditControlListBase{TItem}.Dispose"/>; derived overrides must chain to base.
    /// </summary>
    /// <remarks>
    /// Gated on <c>_stateInitialized</c> so a control whose init never completed disposes cleanly:
    /// a missing <c>@bind-Value</c> makes <see cref="OnInitialized"/> throw its helpful diagnostic
    /// BEFORE <see cref="InitState"/> runs, leaving <c>_fieldIdentifier</c> at <c>default</c> — and
    /// unregistering that hashes a null <c>FieldName</c>, so an <see cref="ArgumentNullException"/>
    /// out of disposal replaced the message that says what to fix.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _stateInitialized)
            EditControlInit.UnregisterField(FormOptions, _fieldIdentifier, this);
        base.Dispose(disposing);
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    /// <summary>
    /// Resolves the effective <see cref="UpdateTrigger"/> through three levels — the control's own
    /// <paramref name="updateOn"/> parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>,
    /// then the calling control's own built-in <paramref name="fallback"/> — and maps it to the DOM
    /// event name that drives the commit. Returns that name as a plain string, rather than leaving
    /// callers to switch on the enum, because the call site is <c>@bind-value:event</c>: the Razor
    /// compiler emits the <c>:event</c> modifier's value as a runtime expression (not a compile-time
    /// literal), so a string result plugs straight in.
    /// </summary>
    protected string ResolveUpdateEvent(UpdateTrigger? updateOn, UpdateTrigger fallback) =>
        (updateOn ?? FormDefaults?.EffectiveUpdateOn ?? fallback) == UpdateTrigger.Change ? "onchange" : "oninput";

    /// <summary>
    /// True when <see cref="InputBase{TValue}.CurrentValue"/> is the type's semantic "empty" —
    /// empty string for string controls, numeric zero for number controls, <c>default(DateTime)</c>
    /// for date controls, etc. Override in derived classes where the default semantics aren't
    /// <c>EqualityComparer&lt;T&gt;.Default.Equals(value, default)</c>. <see cref="ShouldShowComponent"/>
    /// already short-circuits the null check, so overrides only need to answer "is this value
    /// semantically empty?" — the null branch is handled for them.
    /// </summary>
    protected virtual bool IsValueDefault() =>
        EqualityComparer<TValue>.Default.Equals(CurrentValue, default!);

    /// <summary>
    /// Decides whether the control's wrapper renders at all, based on <see cref="IsHidden"/> and
    /// the effective <see cref="HidingMode"/> (per-control <see cref="Hiding"/> ?? form-wide
    /// <see cref="FormOptions.Hiding"/> ?? <see cref="HidingMode.None"/>). Centralizes the
    /// hiding logic that every scalar control used to re-implement. Override
    /// <see cref="IsValueDefault"/> rather than this method when only the "what counts as
    /// default?" question changes.
    /// </summary>
    protected virtual bool ShouldShowComponent()
    {
        var isNull = CurrentValue is null;
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isNull || IsValueDefault());
    }
}
