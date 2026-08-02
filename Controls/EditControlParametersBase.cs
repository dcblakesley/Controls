namespace Controls;

/// <summary>
/// Shared parameter set for the edit controls that aren't <see cref="EditControlBase{TValue}"/> (i.e.
/// not an <c>InputBase&lt;TValue&gt;</c>): <see cref="EditControlListBase{TItem}"/> (list-bound
/// controls) and <see cref="EditDateRange"/> (two independent scalar fields). Both are plain
/// <see cref="ComponentBase"/>s that hand-roll <see cref="FormOptions"/> registration and
/// <see cref="EditContext"/> validation-state subscription instead of inheriting <c>InputBase</c>, and
/// both need the exact same <see cref="IEditControl"/> parameter set plus the three cascading options
/// — hoisted here so neither redeclares it independently.
/// </summary>
public abstract class EditControlParametersBase : ComponentBase, IEditControl
{
    [CascadingParameter] protected EditContext? EditContext { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    /// <inheritdoc/>
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    /// <inheritdoc/>
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

    // ----- Hand-rolled InputBase plumbing -------------------------------------
    // Neither subclass gets these for free: an InputBase re-renders itself on validation-state change
    // and owns one FieldIdentifier, while these bind a collection / two independent fields. The two
    // sequences below were duplicated verbatim -- once in EditControlListBase for its single field,
    // twice in EditDateRange for its Start/End pair -- so they live here, on the one type both derive
    // from. Per-field derived state (ids, attribute lists, the FieldIdentifiers themselves) deliberately
    // stays on each subclass: the markup binds those directly, and the two shapes genuinely differ.

    // Protected (not private) so a subclass that owns extra per-EditContext state of its own --
    // EditDateRange's _parseErrorMessages store -- can read the OLD value in its own OnParametersSet
    // BEFORE calling SyncValidationSubscription below overwrites it, in order to clean that state up
    // against the context it actually belongs to rather than the one that just started cascading.
    protected EditContext? _subscribedEditContext;

    /// <summary>
    /// Points this control's validation-state handler at the CURRENT cascading
    /// <see cref="EditContext"/>, detaching from the previously-subscribed one first, and reports
    /// whether the context actually changed.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the same <see cref="EditContext"/> instance is still cascading (nothing to do)
    /// — which is also the signal that the cached <see cref="FieldIdentifier"/>(s) are still valid, so a
    /// caller's <c>OnParametersSet</c> can return early on it rather than re-running
    /// <see cref="SyncFieldRegistration"/> every parameter cycle.
    /// </returns>
    /// <remarks>
    /// Call from <c>OnParametersSet</c>. The subscription is what makes <c>aria-invalid</c> / the field
    /// state classes update live (e.g. after a form submit) the way an <c>InputBase</c>'s do; the paired
    /// detach is <see cref="DetachValidationSubscription"/>.
    /// </remarks>
    protected bool SyncValidationSubscription()
    {
        if (ReferenceEquals(EditContext, _subscribedEditContext)) return false;
        if (_subscribedEditContext is not null)
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
        if (EditContext is not null)
            EditContext.OnValidationStateChanged += OnValidationStateChanged;
        _subscribedEditContext = EditContext;
        return true;
    }

    /// <summary>
    /// Detaches the handler <see cref="SyncValidationSubscription"/> attached. Call from the control's
    /// <c>Dispose</c>: the <see cref="EditContext"/> outlives any one control, so a control removed
    /// behind a conditional <c>@if</c> would otherwise keep being called back (and keep re-rendering a
    /// detached component) for the life of the form.
    /// </summary>
    protected void DetachValidationSubscription()
    {
        if (_subscribedEditContext is not null)
        {
            _subscribedEditContext.OnValidationStateChanged -= OnValidationStateChanged;
            _subscribedEditContext = null;
        }
    }

    /// <summary>
    /// Moves this control's <see cref="FormOptions"/> registration onto a freshly-derived
    /// <paramref name="field"/>: unregisters the old (dead) one, re-derives from
    /// <paramref name="factory"/> against the current model, then registers the new one under
    /// <paramref name="id"/>. A no-op when <paramref name="factory"/> is null (the control hasn't
    /// initialized yet).
    /// </summary>
    /// <remarks>
    /// Called when the cascading <see cref="EditContext"/> changes, which is how a parent swapping the
    /// bound model instance (form reset, reload) surfaces: the cached
    /// <see cref="FieldIdentifier"/> still points at the OLD model, so <c>NotifyFieldChanged</c> and
    /// validation lookups would silently target dead state forever. Unregistering BEFORE re-registering
    /// is load-bearing — otherwise every swap leaves a dead <see cref="FieldIdentifier"/> behind and
    /// <see cref="ValidationView"/> re-iterates all of them each render, growing with the swap count.
    /// (The scalar <c>InputBase</c> controls throw on a context swap instead; these two support it —
    /// that asymmetry is intentional, not an oversight.)
    /// </remarks>
    protected void SyncFieldRegistration(ref FieldIdentifier field, Func<FieldIdentifier>? factory, string id)
    {
        if (factory is null) return;
        EditControlInit.UnregisterField(FormOptions, field, this);
        field = factory();
        EditControlInit.RegisterField(FormOptions, field, id, this);
    }

    void OnValidationStateChanged(object? sender, ValidationStateChangedEventArgs e) => StateHasChanged();
}
