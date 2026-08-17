namespace Controls.Helpers;

/// <summary>
/// One component's subscription to an <see cref="EditContext"/>'s per-field change notifications,
/// re-pointable at a different context and detachable on dispose. The
/// <see cref="EditContext.OnFieldChanged"/> twin of <see cref="ValidationStateSubscription"/>, and it
/// exists for the same reason: the SAME handler instance has to be used for <c>+=</c> and <c>-=</c>,
/// or the detach silently doesn't detach and the (long-lived) <see cref="EditContext"/> goes on
/// calling back into a torn-down component for the life of the form.
/// </summary>
/// <remarks>
/// Used by <see cref="FormAutoSave"/>, which is the whole point of the pairing: one subscription per
/// form replaces a per-field <c>@bind-Value:after</c>, so the one subscription has to be exactly right.
/// A separate class rather than a mode flag on <see cref="ValidationStateSubscription"/> because the
/// two events carry different payloads — this one hands the caller the
/// <see cref="FieldIdentifier"/> that changed, which is the entire reason a caller subscribes to it.
/// </remarks>
/// <param name="onFieldChanged">
/// Invoked with the changed field each time the subscribed context raises <c>OnFieldChanged</c>.
/// </param>
public sealed class FieldChangedSubscription(Action<FieldIdentifier> onFieldChanged)
{
    readonly EventHandler<FieldChangedEventArgs> _handler = (_, e) => onFieldChanged(e.FieldIdentifier);

    /// <summary>
    /// The currently-subscribed <see cref="EditContext"/>, or null when nothing is subscribed. Read it
    /// BEFORE <see cref="SyncTo"/> to reach the context being swapped away from — a caller holding
    /// per-context state of its own (e.g. <see cref="FormAutoSave"/>'s accumulated
    /// <see cref="FieldIdentifier"/>s, which name the OLD model's fields) has to clean that up.
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
            Context.OnFieldChanged -= _handler;
        if (context is not null)
            context.OnFieldChanged += _handler;
        Context = context;
        return true;
    }

    /// <summary>
    /// Drops the subscription. Call from the component's dispose — see
    /// <see cref="ValidationStateSubscription.Detach"/> for why an unpaired subscription outlives the
    /// component that made it.
    /// </summary>
    public void Detach()
    {
        if (Context is not null)
        {
            Context.OnFieldChanged -= _handler;
            Context = null;
        }
    }
}
