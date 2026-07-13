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
/// <c>InputBase</c>) so derived controls can call <see cref="InitState{T}"/> with it directly instead
/// of requiring a separate <c>Field</c> expression from the consumer.
/// </remarks>
public abstract class EditControlListBase<TItem> : ComponentBase, IEditControl, IDisposable
{
    [CascadingParameter] protected EditContext? EditContext { get; set; }
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

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
    /// (same convention <c>InputBase</c> uses) — supplies the accessor <see cref="InitState{T}"/> needs.
    /// </summary>
    /// <remarks>
    /// <see cref="EditorRequiredAttribute"/> makes a missing/incomplete bind (e.g. one-way <c>Value="..."</c>
    /// with no <c>@bind-Value</c>) a build-time <c>RZ2012</c> diagnostic instead of only the runtime
    /// <see cref="InvalidOperationException"/> each derived control's <c>OnInitialized</c> throws. Unlike
    /// the scalar controls, this parameter is declared here rather than inherited from Microsoft's
    /// <c>InputBase&lt;TValue&gt;</c>, so attaching the attribute doesn't require hiding an inherited,
    /// non-virtual member — which would silently break <c>InputBase</c>'s own change-notification path.
    /// </remarks>
    [Parameter, EditorRequired] public Expression<Func<List<TItem>>>? ValueExpression { get; set; }

    // Standard derived state — populated by InitState in derived class's OnInitialized.
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
    EditContext? _subscribedEditContext;

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

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c>
    /// from the derived control's accessor expression (<see cref="ValueExpression"/>), and registers the field with
    /// <see cref="FormOptions.FieldIdentifiers"/> so the validation summary can link to it.
    /// See the matching remarks on <see cref="EditControlBase{TValue}.InitState{T}"/> for why
    /// registration lives here rather than in <c>FieldValidationDisplay</c>.
    /// </summary>
    // Re-derives the FieldIdentifier when the cascading EditContext is swapped (see OnParametersSet).
    // The field expression evaluates its model access live against the parent's state, so calling
    // FieldIdentifier.Create again picks up the new model instance.
    Func<FieldIdentifier>? _fieldIdentifierFactory;

    protected void InitState<T>(Expression<Func<T>> field)
    {
        (_id, _attributes, _fieldIdentifier) = EditControlInit.Init(field, Id, FormGroupOptions, IdPrefix);
        _fieldIdentifierFactory = () => FieldIdentifier.Create(field);
        // Required-ness resolves through the shared helper (IsRequired param → [Required] attribute
        // → FormOptions.RequiredResolver) so aria-required always matches the FormLabel star.
        _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _fieldIdentifier);
        FormOptions?.RegisterField(_fieldIdentifier, _id, this);

        // Resolve the ARIA references (error-msg id + aria-describedby token list). Recomputed in
        // OnParametersSet too, so a runtime Description/Tooltip/label-hidden change is reflected and
        // aria-describedby never dangles.
        (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
    }

    /// <summary> Toggles an item in <see cref="Value"/>, notifies the EditContext, and fires <see cref="ValueChanged"/>. </summary>
    protected async Task ToggleAsync(TItem item)
    {
        // Build a new list rather than mutating the caller's bound instance — so a parent that
        // compares references detects the change, and any shared/source list isn't mutated as a
        // side effect. A null bound list (model property never initialized) starts fresh.
        List<TItem> updated = Value is null ? [] : [.. Value];
        if (!updated.Remove(item))
            updated.Add(item);
        Value = updated;

        // Write the new value back to the bound model BEFORE notifying the EditContext. The validator
        // reads the property live off the model via reflection during NotifyFieldChanged, so notifying
        // first would validate the stale (pre-toggle) value — leaving the error state one click behind
        // (e.g. a [MinLength(2)] error lingering after the second box is checked).
        await ValueChanged.InvokeAsync(Value);
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
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
    /// re-render automatically when validation state changes. Subscribe to the cascading
    /// <see cref="EditContext"/> so <see cref="IsInvalid"/> / <c>aria-invalid</c> update live (e.g.
    /// after a form submit) the way the scalar controls do.
    /// </summary>
    protected override void OnParametersSet()
    {
        // Keep the cached ARIA references current when parameters change (runtime Description/Tooltip
        // or label-hidden toggle). No-op until InitState has run (_attributes is null before then).
        if (_attributes is not null)
        {
            _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _fieldIdentifier);
            (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
        }

        if (ReferenceEquals(EditContext, _subscribedEditContext)) return;
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        if (EditContext is not null)
            EditContext.OnValidationStateChanged += OnValidationStateChanged;
        _subscribedEditContext = EditContext;

        // The EditContext changes when the parent swaps the model instance (form reset, reload).
        // The cached FieldIdentifier still pointed at the old model, so NotifyFieldChanged and
        // validation lookups silently targeted dead state forever — re-derive it against the
        // current model and re-register so the validation summary links keep working. (The scalar
        // InputBase controls throw on a context swap; list controls support it instead.)
        if (_fieldIdentifierFactory is not null)
        {
            // Drop the previous (old-model) registration before adding the new one — otherwise every
            // swap leaves a dead FieldIdentifier behind and ValidationView iterates all of them each
            // render, growing with the swap count.
            FormOptions?.UnregisterField(_fieldIdentifier, this);
            _fieldIdentifier = _fieldIdentifierFactory();
            FormOptions?.RegisterField(_fieldIdentifier, _id, this);
        }
    }

    void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();

    /// <summary> Detaches the validation-state listener and drops the field registration so a removed
    /// control (e.g. behind a conditional <c>@if</c>) doesn't leave stale state in the validation summary. </summary>
    public void Dispose()
    {
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        FormOptions?.UnregisterField(_fieldIdentifier, this);
    }
}
