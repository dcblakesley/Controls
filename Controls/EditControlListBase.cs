namespace Controls;

/// <summary>
/// Base class for edit controls that bind to a <see cref="List{TItem}"/> rather than a single
/// value (e.g. <c>EditCheckedStringList</c>, <c>EditCheckedEnumList</c>). Mirrors
/// <see cref="EditControlBase{TValue}"/> but doesn't inherit <c>InputBase</c> — list-bound
/// controls don't fit Microsoft's <c>Value</c>/<c>ValueChanged</c>/<c>ValueExpression</c> shape
/// because they bind a collection rather than a scalar.
/// </summary>
/// <typeparam name="TItem">The type of each item in the bound list.</typeparam>
/// <remarks>
/// Declares its own <see cref="ValueExpression"/> parameter (the Razor compiler's <c>@bind-Value</c>
/// synthesis only needs the Value/ValueChanged/ValueExpression parameter shape — it isn't limited to
/// <c>InputBase</c>), which is what lets <see cref="OnInitialized"/> here wire every derived control's
/// state with no consumer-supplied <c>Field</c> expression and no per-control <c>OnInitialized</c>.
/// Same contract as <see cref="EditControlBase{TValue}"/> — see its remarks.
/// </remarks>
public abstract class EditControlListBase<TItem> : EditControlParametersBase, IDisposable
{
    /// <summary>
    /// Captures unmatched attributes (in practice, a consumer's <c>class="..."</c>) so list controls
    /// can merge it into <see cref="FieldCssClass"/> — the same forwarding scalar controls get for free
    /// from <see cref="InputBase{TValue}.AdditionalAttributes"/>. Without this, an unmatched attribute
    /// on a list control (e.g. <c>class</c> on <c>EditMultiSelect</c>) throws at render time instead of
    /// being applied.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary> The current list of selected items.</summary>
    [Parameter] public required List<TItem> Value { get; set; }

    /// <summary> Event callback that fires when the <see cref="Value"/> list changes.</summary>
    [Parameter] public EventCallback<List<TItem>> ValueChanged { get; set; }

    /// <summary>
    /// Compiler-populated by <c>@bind-Value</c> alongside <see cref="Value"/>/<see cref="ValueChanged"/>
    /// (same convention <c>InputBase</c> uses) — supplies the accessor <see cref="InitState"/> needs.
    /// </summary>
    /// <remarks>
    /// <see cref="EditorRequiredAttribute"/> makes a missing/incomplete bind (e.g. one-way <c>Value="..."</c>
    /// with no <c>@bind-Value</c>) a build-time <c>RZ2012</c> diagnostic instead of only the runtime
    /// <see cref="InvalidOperationException"/> <see cref="OnInitialized"/> throws. Unlike
    /// the scalar controls, this parameter is declared here rather than inherited from Microsoft's
    /// <c>InputBase&lt;TValue&gt;</c>, so attaching the attribute doesn't require hiding an inherited,
    /// non-virtual member — which would silently break <c>InputBase</c>'s own change-notification path.
    /// </remarks>
    [Parameter, EditorRequired] public Expression<Func<List<TItem>>>? ValueExpression { get; set; }

    // Standard derived state — populated by InitState, which OnInitialized below calls.
    protected string _id = string.Empty;
    protected string? _isRequired;
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;
    // Cached ARIA references — resolved in InitState and re-resolved each OnParametersSet (see EditControlInit.BuildDescribedBy).
    protected string _errorMsgId = string.Empty;
    protected string _describedBy = string.Empty;

    /// <summary>
    /// The control's fully-resolved required-ness (IsRequired parameter → [Required] attribute →
    /// FormOptions.RequiredResolver), recomputed alongside <c>_isRequired</c> each parameter cycle.
    /// Markup passes THIS to FormLabel's IsRequired (an explicit value wins outright there), so the
    /// star and <c>aria-required</c> share one computation site and can never disagree.
    /// </summary>
    protected bool? IsRequiredResolved => _isRequired is not null;

    /// <summary>
    /// True when this field currently has a validation error. List controls aren't
    /// <see cref="InputBase{TValue}"/>, so validity is read from the cascading
    /// <see cref="EditContext"/> rather than an InputBase-provided <c>CssClass</c>.
    /// </summary>
    protected bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(_fieldIdentifier).Any();

    /// <summary>
    /// The consumer's <c>class</c> attribute (if any) merged with the <see cref="EditContext"/>'s
    /// field-state classes (<c>modified</c>/<c>valid</c>/<c>invalid</c> by default, or whatever the
    /// form's <c>FieldCssClassProvider</c> emits) — the list-control analogue of the <c>CssClass</c>
    /// the scalar controls inherit from <see cref="InputBase{TValue}"/>, same merge order.
    /// </summary>
    protected string FieldCssClass
    {
        get
        {
            var fieldClass = EditContext is null ? string.Empty : EditContext.FieldCssClass(_fieldIdentifier);
            if (AdditionalAttributes is not null &&
                AdditionalAttributes.TryGetValue("class", out var classObj) &&
                Convert.ToString(classObj, CultureInfo.InvariantCulture) is { Length: > 0 } consumerClass)
            {
                return fieldClass.Length > 0 ? $"{consumerClass} {fieldClass}" : consumerClass;
            }
            return fieldClass;
        }
    }

    // Re-derives the FieldIdentifier when the cascading EditContext is swapped (see OnParametersSet).
    // The field expression evaluates its model access live against the parent's state, so calling
    // FieldIdentifier.Create again picks up the new model instance.
    Func<FieldIdentifier>? _fieldIdentifierFactory;

    /// <summary>
    /// This control's name for diagnostics — <c>EditMultiSelect</c>, not the CLR's
    /// <c>EditMultiSelect`1</c>. Mirrors <see cref="EditControlBase{TValue}.ControlName"/> (the two
    /// bases share no ancestor, so the one-line forward is declared twice; the trimming itself lives
    /// once, in <see cref="EditControlInit.ControlName"/>).
    /// </summary>
    protected string ControlName => EditControlInit.ControlName(this);

    /// <summary>
    /// Wires the standard derived state for every list-bound control, so a derived control needs no
    /// <c>OnInitialized</c> of its own unless it has extra init to run (in which case it calls
    /// <c>base.OnInitialized()</c> first — see <see cref="EditControlBase{TValue}"/>'s remarks, which
    /// this base follows exactly).
    /// </summary>
    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{ControlName} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c>
    /// from the control's bound accessor (<see cref="ValueExpression"/>, which <c>@bind-Value</c>
    /// supplies), and registers the field with <see cref="FormOptions.FieldIdentifiers"/> so the
    /// validation summary can link to it. Called by <see cref="OnInitialized"/>; a derived control
    /// never calls it itself. See the matching remarks on
    /// <see cref="EditControlBase{TValue}.InitState"/> for why registration lives here rather than in
    /// <c>FieldValidationDisplay</c>.
    /// </summary>
    protected void InitState(Expression<Func<List<TItem>>> field)
    {
        (_id, _attributes, _fieldIdentifier) = EditControlInit.Init(field, Id, FormGroupOptions, IdPrefix);
        _fieldIdentifierFactory = () => FieldIdentifier.Create(field);
        // Paired with Dispose below — see EditControlInit.RegisterField's remarks.
        EditControlInit.RegisterField(FormOptions, _fieldIdentifier, _id, this);
        RefreshAriaState();
    }

    // aria-required plus the error-msg id and aria-describedby token list, all through the one shared
    // helper (same resolution and same call sites as EditControlBase.RefreshAriaState). No-op until
    // InitState has run — _attributes is null before then.
    void RefreshAriaState()
    {
        if (_attributes is null) return;
        (_isRequired, _errorMsgId, _describedBy) = EditControlInit.ResolveAriaState(
            _id, ShouldHideLabel, Description, Tooltip, _attributes, IsRequired, FormOptions, _fieldIdentifier);
    }

    /// <summary>
    /// Writes <paramref name="newValue"/> back to the bound model, fires <see cref="ValueChanged"/>,
    /// then notifies the <see cref="EditContext"/> — in that order, since the validator reads the
    /// property live off the model via reflection during <c>NotifyFieldChanged</c>, and notifying
    /// first would validate the stale (pre-write) value, leaving the error state one interaction
    /// behind (e.g. a <c>[MinLength(2)]</c> error lingering after a second item is added). Shared by
    /// <see cref="ToggleAsync"/> (single-item toggle) and any wrapper that replaces the whole list at
    /// once (e.g. <c>EditMultiSelect.OnValuesChanged</c>, <c>EditFile</c>'s own file-list updates).
    /// </summary>
    protected async Task SetValueAsync(List<TItem> newValue)
    {
        Value = newValue;
        await ValueChanged.InvokeAsync(Value);
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
    }

    /// <summary> Toggles an item in <see cref="Value"/>, notifies the EditContext, and fires <see cref="ValueChanged"/>. </summary>
    protected Task ToggleAsync(TItem item)
    {
        // Build a new list rather than mutating the caller's bound instance — so a parent that
        // compares references detects the change, and any shared/source list isn't mutated as a
        // side effect. A null bound list (model property never initialized) starts fresh.
        List<TItem> updated = Value is null ? [] : [.. Value];
        if (!updated.Remove(item))
            updated.Add(item);
        return SetValueAsync(updated);
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    /// <summary>
    /// Default visibility logic shared by both list controls. Treats an empty list as "default"
    /// for the <c>NullOrDefault</c> hiding modes.
    /// </summary>
    protected virtual bool ShouldShowComponent()
    {
        var isNull = Value is null;
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isNull || Value!.Count == 0);
    }

    /// <summary>
    /// List controls are <c>ComponentBase</c>, not <see cref="InputBase{TValue}"/>, so they don't
    /// re-render automatically when validation state changes. Subscribing to the cascading
    /// <see cref="EditContext"/> (via <see cref="EditControlParametersBase.SyncValidationSubscription"/>,
    /// shared with <see cref="EditDateRange"/>) is what makes <see cref="IsInvalid"/> /
    /// <c>aria-invalid</c> update live (e.g. after a form submit) the way the scalar controls do.
    /// </summary>
    protected override void OnParametersSet()
    {
        // Keep the cached ARIA state current when parameters change (runtime Description/Tooltip or
        // label-hidden toggle).
        RefreshAriaState();

        // A false return means the same EditContext is still cascading, so the cached FieldIdentifier
        // is still live and there's nothing to re-register.
        if (!SyncValidationSubscription()) return;

        // The context changed, which is how a parent swapping the model instance (form reset, reload)
        // surfaces — re-derive the FieldIdentifier against the current model and move the registration
        // onto it. See SyncFieldRegistration for why the unregister has to come first.
        SyncFieldRegistration(ref _fieldIdentifier, _fieldIdentifierFactory, _id);
    }

    /// <summary> Detaches the validation-state listener and drops the field registration so a removed
    /// control (e.g. behind a conditional <c>@if</c>) doesn't leave stale state in the validation summary. </summary>
    public void Dispose()
    {
        DetachValidationSubscription();
        EditControlInit.UnregisterField(FormOptions, _fieldIdentifier, this);
    }
}
