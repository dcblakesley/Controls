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
/// Each derived control declares its own <c>Field</c> parameter (typed
/// <c>Expression&lt;Func&lt;List&lt;TItem&gt;&gt;&gt;</c> or similar) and calls
/// <see cref="EditControlBase{TValue}.InitState{T}"/>'s sibling <see cref="InitState{T}"/>
/// in its <c>OnInitialized</c>.
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
    [Parameter] public bool IsRequired { get; set; }
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

    /// <summary> The current list of selected items.</summary>
    [Parameter] public required List<TItem> Value { get; set; }

    /// <summary> Event callback that fires when the <see cref="Value"/> list changes.</summary>
    [Parameter] public EventCallback<List<TItem>> ValueChanged { get; set; }

    // Standard derived state — populated by InitState in derived class's OnInitialized.
    protected string _id = string.Empty;
    protected string? _isRequired;
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;
    // Cached ARIA references, resolved once in InitState (see EditControlInit.BuildDescribedBy).
    protected string _errorMsgId = string.Empty;
    protected string _describedBy = string.Empty;
    EditContext? _subscribedEditContext;

    /// <summary>
    /// True when this field currently has a validation error. List controls aren't
    /// <see cref="InputBase{TValue}"/>, so validity is read from the cascading
    /// <see cref="EditContext"/> rather than an InputBase-provided <c>CssClass</c>.
    /// </summary>
    protected bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(_fieldIdentifier).Any();

    /// <summary>
    /// <c>"invalid"</c> when the field has a validation error, else empty — the list-control analogue
    /// of the <c>CssClass</c> the scalar controls inherit from <see cref="InputBase{TValue}"/>.
    /// </summary>
    protected string FieldCssClass => IsInvalid ? "invalid" : string.Empty;

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c>
    /// from the derived control's <c>Field</c> expression, and registers the field with
    /// <see cref="FormOptions.FieldIdentifiers"/> so the validation summary can link to it.
    /// See the matching remarks on <see cref="EditControlBase{TValue}.InitState{T}"/> for why
    /// registration lives here rather than in <c>FieldValidationDisplay</c>.
    /// </summary>
    protected void InitState<T>(Expression<Func<T>> field)
    {
        (_id, _isRequired, _attributes, _fieldIdentifier) = EditControlInit.Init(field, Id, FormGroupOptions, IdPrefix);
        FormOptions?.RegisterField(_fieldIdentifier, _id);

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
        // side effect.
        var updated = new List<TItem>(Value);
        if (!updated.Remove(item))
            updated.Add(item);
        Value = updated;

        EditContext?.NotifyFieldChanged(_fieldIdentifier);
        await ValueChanged.InvokeAsync(Value);
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
            (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);

        if (ReferenceEquals(EditContext, _subscribedEditContext)) return;
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        if (EditContext is not null)
            EditContext.OnValidationStateChanged += OnValidationStateChanged;
        _subscribedEditContext = EditContext;
    }

    void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();

    /// <summary> Detaches the validation-state listener. </summary>
    public void Dispose()
    {
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
    }
}
