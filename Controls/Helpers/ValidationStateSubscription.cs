namespace Controls.Helpers;

/// <summary>
/// One component's subscription to an <see cref="EditContext"/>'s validation-state notifications,
/// re-pointable at a different context and detachable on dispose. Owns the single load-bearing detail
/// the hand-rolled copies kept re-implementing: the SAME handler instance has to be used for
/// <c>+=</c> and <c>-=</c>, or the detach silently doesn't detach.
/// </summary>
/// <remarks>
/// Used by the components that aren't <c>InputBase&lt;TValue&gt;</c> and therefore don't get the
/// re-render-on-validation-state-change behavior for free: <see cref="EditControlParametersBase"/>
/// (and through it <see cref="EditControlListBase{TItem}"/> and <see cref="EditDateRange"/>) plus
/// <see cref="ValidationView"/>. A plain class rather than a base member because
/// <see cref="ValidationView"/> shares none of the edit controls' parameter surface and has no
/// business inheriting it.
/// </remarks>
/// <param name="onValidationStateChanged">
/// Invoked when the subscribed context raises <c>OnValidationStateChanged</c> — in practice each
/// caller's <c>StateHasChanged</c>.
/// </param>
public sealed class ValidationStateSubscription(Action onValidationStateChanged)
{
    readonly EventHandler<ValidationStateChangedEventArgs> _handler = (_, _) => onValidationStateChanged();

    /// <summary>
    /// The currently-subscribed <see cref="EditContext"/>, or null when nothing is subscribed. Read it
    /// BEFORE <see cref="SyncTo"/> to reach the context being swapped away from — a caller with extra
    /// per-context state of its own (e.g. <see cref="EditDateRange"/>'s parse-error store) has to clean
    /// that up against the context it actually belongs to.
    /// </summary>
    public EditContext? Context { get; private set; }

    /// <summary>
    /// Points the subscription at <paramref name="context"/>, detaching from the previous one first,
    /// and reports whether the context actually changed (<c>false</c> = same instance, nothing to do).
    /// </summary>
    public bool SyncTo(EditContext? context)
    {
        if (ReferenceEquals(context, Context)) return false;
        if (Context is not null)
            Context.OnValidationStateChanged -= _handler;
        if (context is not null)
            context.OnValidationStateChanged += _handler;
        Context = context;
        return true;
    }

    /// <summary>
    /// Drops the subscription. Call from the component's dispose: an <see cref="EditContext"/> outlives
    /// any one component, so a component removed behind a conditional <c>@if</c> would otherwise keep
    /// being called back (and keep re-rendering a detached component) for the life of the form.
    /// </summary>
    public void Detach()
    {
        if (Context is not null)
        {
            Context.OnValidationStateChanged -= _handler;
            Context = null;
        }
    }
}
