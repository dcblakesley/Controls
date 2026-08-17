namespace Controls;

/// <summary>
/// Drop-in auto-save for a whole form: one component inside an <see cref="EditForm"/> replaces a
/// per-field <c>@bind-Value:after="SaveAsync"</c> on every control. Renders nothing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Place it INSIDE the form, after the validator.</b> The <see cref="EditContext"/> cascades DOWN
/// from <see cref="EditForm"/>, so this has to sit under it — <see cref="FormDefaults"/> (which wraps
/// an app/MFE ROOT, above any form) cannot see one and is deliberately not where this lives. And
/// <see cref="EditContext.OnFieldChanged"/> handlers run in SUBSCRIPTION order, so a
/// <see cref="DataAnnotationsValidator"/> written after this component would not yet have validated
/// the changed field when <see cref="SaveWhenInvalid"/>'s gate reads the message store:
/// <code>
/// &lt;EditForm Model="model"&gt;
///     &lt;DataAnnotationsValidator /&gt;
///     &lt;FormAutoSave OnSave="SaveAsync" /&gt;
///     ...
/// &lt;/EditForm&gt;
/// </code>
/// The ordering only actually BITES at <see cref="DebounceMilliseconds"/> 0, where the flush runs
/// inside the notification itself and therefore inside the same handler chain: put the component
/// first there and the gate reads a message store the validator has not touched yet, so the invalid
/// value is saved. At any non-zero debounce the flush happens a debounce later — long after every
/// <c>OnFieldChanged</c> handler has run — and the order is irrelevant. Write it after the validator
/// regardless: it costs nothing and it is correct in both modes.
/// </para>
/// <para>
/// <b>Why one subscription beats N callbacks.</b> Every control family in this library notifies the
/// <see cref="EditContext"/> — the scalar controls through <see cref="InputBase{TValue}"/>, the
/// list-bound family explicitly on every path (<c>EditControlListBase.SetValueAsync</c>), which
/// includes the paths a per-field <c>:after</c> cannot reach at all, such as <c>EditFile</c>'s REMOVE
/// button. One subscription therefore sees strictly more than N hand-wired callbacks, and one
/// <see cref="IDisposable"/> replaces N of them.
/// </para>
/// <para>
/// <b>Volume, and why the debounce is on by default.</b> The counts are pinned by
/// <c>FieldChangedNotificationTests</c>: <c>EditString</c>/<c>EditTextArea</c> default to
/// <see cref="UpdateTrigger.Input"/>, i.e. one notification per KEYSTROKE; <c>EditRange</c> and
/// <c>EditColor</c> commit once per animation frame while a handle is dragged; the three radio groups
/// that forward their <c>ValueExpression</c> to an inner <c>InputRadioGroup</c> (<c>EditRadio</c>,
/// <c>EditRadioEnum</c>, <c>EditRadioString</c>) notify TWICE per click; <c>EditDateRange</c> notifies
/// once per endpoint, so a two-click pick is two. <see cref="DebounceMilliseconds"/> collapses all of
/// those into one save. On Blazor Server, where an un-debounced per-keystroke save is a network round
/// trip per character, also pair this with
/// <c>&lt;FormDefaults UpdateOn="UpdateTrigger.Change"&gt;</c> to commit text on blur instead.
/// </para>
/// <para>
/// <b>Parse failures are notifications too.</b> Typing <c>abc</c> into an <c>EditNumber</c> (or a bad
/// date into <c>EditDate</c>/<c>EditDateNative</c>/<c>EditDateRange</c>, or bad hex into
/// <c>EditColor</c>) deliberately raises <c>OnFieldChanged</c> while the model still holds the OLD
/// value and the field is now invalid — <see cref="InputBase{TValue}"/>'s own convention, which those
/// controls mirror. That is precisely the false positive <see cref="SaveWhenInvalid"/> (false by
/// default) exists to swallow.
/// </para>
/// </remarks>
public sealed class FormAutoSave : ComponentBase, IDisposable
{
    [CascadingParameter] EditContext? CurrentEditContext { get; set; }

    /// <summary>
    /// Invoked when a debounce window closes (or immediately per notification when
    /// <see cref="DebounceMilliseconds"/> is 0) with the <see cref="EditContext"/> and the fields that
    /// changed. Await your persistence call from it — the next save waits per
    /// <see cref="Concurrency"/>.
    /// </summary>
    [Parameter, EditorRequired] public EventCallback<FormAutoSaveEventArgs> OnSave { get; set; }

    /// <summary>
    /// How long to wait after the last field change before saving — a TRAILING debounce, so each new
    /// notification pushes the deadline out and a burst produces exactly one save. Default 500.
    /// Zero (or negative) fires per notification instead, which is opt-in for a reason: see the volume
    /// discussion in this class's remarks.
    /// </summary>
    [Parameter] public int DebounceMilliseconds { get; set; } = 500;

    /// <summary>
    /// Whether to save while the form has outstanding validation messages. False (default) skips the
    /// save and KEEPS the accumulated fields pending, so the next successful save still reports
    /// everything that changed since the last one. The check reads the
    /// <see cref="EditContext"/>'s CURRENT messages rather than calling
    /// <see cref="EditContext.Validate"/> — re-validating would light up every untouched field's error
    /// the moment the user typed in one of them.
    /// </summary>
    /// <remarks>
    /// A skipped save is never TERMINAL. Besides the next field change, this component also re-attempts
    /// it on <see cref="EditContext.OnValidationStateChanged"/>, so messages cleared from OUTSIDE the
    /// form — a server-side <see cref="ValidationMessageStore"/> clear followed by
    /// <see cref="EditContext.NotifyValidationStateChanged"/>, which the
    /// <see cref="FormOptions.RequiredResolver"/>/FluentValidation bridge relies on — release the
    /// pending work without waiting for the user to touch another field. Without that a field that
    /// simply stays invalid (a conditionally-required property the user cannot currently satisfy) would
    /// gate off auto-save for the WHOLE form indefinitely.
    /// </remarks>
    [Parameter] public bool SaveWhenInvalid { get; set; }

    /// <summary> What happens when a field changes while a save is still in flight. Default
    /// <see cref="AutoSaveConcurrency.CoalesceTrailing"/>. </summary>
    [Parameter] public AutoSaveConcurrency Concurrency { get; set; } = AutoSaveConcurrency.CoalesceTrailing;

    /// <summary>
    /// Optional per-field filter, called with each changed <see cref="FieldIdentifier"/>. Returning
    /// false ignores that change entirely — it neither arms the debounce nor joins
    /// <see cref="FormAutoSaveEventArgs.ChangedFields"/>. Null (default) accepts every field.
    /// </summary>
    [Parameter] public Func<FieldIdentifier, bool>? ShouldSave { get; set; }

    /// <summary>
    /// Invoked when <see cref="OnSave"/> throws, with the exception AND the fields the failed save was
    /// carrying (see <see cref="FormAutoSaveFailureEventArgs"/>). When this is NOT wired the exception
    /// is re-dispatched through <see cref="ComponentBase.DispatchExceptionAsync"/> instead, i.e. it
    /// surfaces exactly as an exception thrown from a lifecycle method would — caught by an enclosing
    /// <c>ErrorBoundary</c>, otherwise fatal to the circuit. Failures are never silently swallowed;
    /// wiring this is how you opt into handling them yourself, and a handler that throws in turn is
    /// itself re-dispatched rather than lost.
    /// </summary>
    [Parameter] public EventCallback<FormAutoSaveFailureEventArgs> OnSaveFailed { get; set; }

    /// <summary>
    /// The clock the debounce runs on. Null (default) uses <see cref="System.TimeProvider.System"/>.
    /// Supplied so the debounce is deterministically testable — a fake provider lets a test advance
    /// time by hand instead of sleeping, in this library's suite and in a consumer's tests of their own
    /// auto-saving form. It is the only parameter here that isn't closing a hazard from the volume
    /// analysis above, and it is the .NET-idiomatic seam for exactly this.
    /// </summary>
    /// <remarks>
    /// <b>Read ONCE</b>, when the debounce timer is first created (i.e. on the first accepted field
    /// change at a non-zero <see cref="DebounceMilliseconds"/>). The one timer lives for the
    /// component's lifetime and is re-armed rather than re-created, so swapping this parameter
    /// afterwards has no effect — the debounce goes on running on the provider it started with. Supply
    /// it once, before the form is interacted with.
    /// </remarks>
    [Parameter] public TimeProvider? TimeProvider { get; set; }

    // Fully qualified: the parameter above shadows the type name inside this class.
    System.TimeProvider EffectiveTimeProvider => TimeProvider ?? System.TimeProvider.System;

    // The same subscribe/re-point/detach discipline the edit controls use for the OTHER EditContext
    // event, through the shared implementation. Created lazily: its handler is an instance method
    // group, which a field initializer can't take.
    FieldChangedSubscription? _subscription;

    // The second subscription, and the reason it exists: a save skipped by the validity gate has to be
    // RE-ATTEMPTED when validation state changes, or the skip is terminal (see SaveWhenInvalid's
    // remarks). Same lazy creation, same re-point/detach discipline.
    ValidationStateSubscription? _validationSubscription;

    // Fields seen since the last save that actually ran, de-duplicated, in first-seen order. A List
    // (not a HashSet) because the ORDER is part of the contract -- "what changed, in the order the
    // user touched it" -- and a form has few enough fields that the linear Contains is free.
    readonly List<FieldIdentifier> _pending = [];

    // One timer for the component's lifetime, re-armed (never re-created) per notification: ITimer.Change
    // with a fresh due time IS the trailing debounce, and re-creating one per keystroke would allocate
    // a timer per character.
    ITimer? _debounceTimer;

    // Single-threaded by construction, with ONE exception: every mutation below happens on the
    // renderer's dispatcher (the EditContext notifications arrive on it, and the timer callback is
    // marshalled onto it by InvokeAsync), so no lock is needed and none would help -- Blazor Server's
    // circuit and WASM's single thread both serialize this. The exception is _disposed, which Dispose
    // writes on the dispatcher and the timer callback READS on a thread-pool thread before marshalling
    // (StartFlush): volatile, so that read can't be hoisted or served from a stale cache. FlushAsync
    // re-checks it on the dispatcher anyway, so the volatile is belt-and-braces rather than the whole
    // guarantee.
    bool _saving;
    bool _trailingQueued;
    volatile bool _disposed;

    // Set when the validity gate skips a save (and cleared the moment that skip is re-attempted).
    // Scopes the OnValidationStateChanged handler to the one case it exists for: without it, every
    // validation-state change on a form with pending work would try to flush, including the constant
    // stream a DataAnnotationsValidator raises while the user types.
    bool _validityGated;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
        {
            throw new InvalidOperationException($"{nameof(FormAutoSave)} requires a cascading parameter " +
                                                $"of type {nameof(EditContext)}. For example, you can use {nameof(FormAutoSave)} inside " +
                                                $"an {nameof(EditForm)}.");
        }

        _subscription ??= new FieldChangedSubscription(OnFieldChanged);
        _validationSubscription ??= new ValidationStateSubscription(OnValidationStateChanged);
        _validationSubscription.SyncTo(CurrentEditContext);
        if (_subscription.SyncTo(CurrentEditContext))
        {
            // A new context means a new model: the pending FieldIdentifiers name the OLD model's
            // fields, and an in-flight debounce armed for it would save the wrong form. Both go, along
            // with any gated-skip state, which belonged to the outgoing form's validity.
            //
            // Reachable in principle only: DataAnnotationsValidator and every InputBase-derived control
            // THROW when their EditContext changes, so a real <EditForm> whose EditContext is swapped
            // tears the subtree down instead of re-pointing it, and this component never sees the swap.
            // The branch is kept because it is the correct behavior for the one shape that can reach it
            // (a bare CascadingValue<EditContext> around nothing else, which is how it is tested) and
            // because the alternative -- saving the previous model's fields into the new context -- is
            // the worst possible failure mode.
            _pending.Clear();
            _validityGated = false;
            _debounceTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    // Renders nothing at all -- this component is behavior, not markup. BuildRenderTree is left empty
    // rather than emitting a comment/marker node so the form's DOM is byte-identical with and without it.
    /// <inheritdoc/>
    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder) { }

    /// <summary>
    /// Never re-renders. The initial render is forced by the framework regardless of this; every
    /// subsequent parameter change or state update would only re-emit the same empty tree, and this
    /// component's state changes on every keystroke in the form.
    /// </summary>
    protected override bool ShouldRender() => false;

    void OnFieldChanged(FieldIdentifier field)
    {
        if (_disposed || !OnSave.HasDelegate) return;
        if (ShouldSave is { } filter && !filter(field)) return;

        if (!_pending.Contains(field))
            _pending.Add(field);

        ArmOrFlush();
    }

    /// <summary>
    /// Re-attempts a save the validity gate skipped, once the form is valid again. Deliberately narrow:
    /// it does nothing unless a skip actually happened (<c>_validityGated</c>) and there is still
    /// pending work, so the ordinary validation-state churn of typing costs one field read.
    /// </summary>
    /// <remarks>
    /// It goes back through the DEBOUNCE rather than flushing on the spot, and that matters for the
    /// common shape of "the form became valid": the user fixed the offending field, which means this
    /// handler runs from the validator's own OnFieldChanged handler — BEFORE this component's, and
    /// therefore before the field they just fixed has even joined <c>_pending</c>. Flushing here would
    /// save the batch without it and then save again a debounce later. Re-arming instead lands both in
    /// the one save the user expects. (At <see cref="DebounceMilliseconds"/> 0 there is no window to
    /// re-arm, so it flushes, exactly as a field change does.)
    /// <para>
    /// Loop safety: the flag is cleared BEFORE the re-attempt, so a validation-state change raised
    /// during the resulting flush can't re-trigger the same skip; a save IN FLIGHT is left alone
    /// (<c>_saving</c>) because its own loop picks up whatever is pending when it finishes — and
    /// because a consumer's OnSave writing to a ValidationMessageStore raises this event from inside
    /// the save it would otherwise restart. A repeat skip simply sets the flag again.
    /// </para>
    /// </remarks>
    void OnValidationStateChanged()
    {
        if (_disposed || !_validityGated || _saving || _pending.Count == 0) return;
        if (!OnSave.HasDelegate) return;
        if (!SaveWhenInvalid && CurrentEditContext?.GetValidationMessages().Any() == true) return;

        _validityGated = false;
        ArmOrFlush();
    }

    // The shared tail of "there is work to do": start (or push out) the trailing debounce, or flush
    // immediately when debouncing is off. Re-arming an already-armed timer replaces its due time, which
    // is what makes this trailing -- a burst of keystrokes keeps pushing the deadline out and only the
    // pause fires it.
    void ArmOrFlush()
    {
        if (DebounceMilliseconds <= 0)
        {
            StartFlush();
            return;
        }

        _debounceTimer ??= EffectiveTimeProvider.CreateTimer(
            _ => StartFlush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _debounceTimer.Change(TimeSpan.FromMilliseconds(DebounceMilliseconds), Timeout.InfiniteTimeSpan);
    }

    // The timer continuation runs on a thread-pool thread, NOT the renderer's synchronization context
    // -- so everything past this point is marshalled back onto the dispatcher before it touches
    // component state or invokes a consumer callback. (The DebounceMilliseconds <= 0 path is already
    // on the dispatcher; InvokeAsync recognizes that and runs inline.)
    void StartFlush()
    {
        if (_disposed) return;
        // Fire-and-forget by necessity -- nothing awaits a debounce -- but NOT discarded: an exception
        // escaping the flush would otherwise die unobserved in the dropped task, which is exactly the
        // silent swallowing this component's OnSaveFailed contract promises never happens.
        _ = ObserveFlushAsync();
    }

    async Task ObserveFlushAsync()
    {
        try
        {
            await InvokeAsync(FlushAsync);
        }
        catch (Exception ex)
        {
            // A failing OnSave (and a failing OnSaveFailed) is already routed to the consumer inside
            // FlushAsync, so anything arriving here escaped some other way -- a validator throwing
            // behind GetValidationMessages, say. Same channel, one last time.
            try
            {
                await DispatchExceptionAsync(ex);
            }
            catch
            {
                // The renderer itself refused it: the component is detached/disposed, and there is no
                // channel left to surface anything through. Swallowing beats faulting a task nobody
                // can observe.
            }
        }
    }

    async Task FlushAsync()
    {
        if (_disposed || _pending.Count == 0) return;

        if (Concurrency == AutoSaveConcurrency.CoalesceTrailing && _saving)
        {
            // At most ONE trailing run is ever queued: the in-flight save's own loop below picks this
            // flag up when it finishes, with whatever accumulated in _pending meanwhile.
            _trailingQueued = true;
            return;
        }

        do
        {
            // Read the CURRENT message store rather than re-validating (see SaveWhenInvalid). This is
            // why the component belongs after <DataAnnotationsValidator />: its OnFieldChanged handler
            // has to have validated the changed field before this runs.
            //
            // Checked BEFORE _trailingQueued is cleared, so a gated skip doesn't SPEND the queued
            // trailing run: the queue survives to be picked up by whatever releases the gate.
            if (!SaveWhenInvalid && CurrentEditContext?.GetValidationMessages().Any() == true)
            {
                // _pending deliberately survives -- the next successful save reports it all -- and the
                // flag makes the skip recoverable without a further field change (OnValidationStateChanged).
                _validityGated = true;
                return;
            }

            _trailingQueued = false;

            if (_pending.Count == 0) return;
            var changed = _pending.ToArray();
            _pending.Clear();

            _saving = true;
            try
            {
                await InvokeSaveAsync(changed);
            }
            finally
            {
                _saving = false;
            }
        }
        while (!_disposed && Concurrency == AutoSaveConcurrency.CoalesceTrailing && _trailingQueued && _pending.Count > 0);
    }

    async Task InvokeSaveAsync(IReadOnlyList<FieldIdentifier> changed)
    {
        try
        {
            await OnSave.InvokeAsync(new FormAutoSaveEventArgs(CurrentEditContext!, changed));
        }
        catch (Exception ex)
        {
            // The fields this save was carrying were taken OUT of _pending before it started (a change
            // arriving mid-save has to accumulate somewhere). A failed save therefore has to put them
            // back or they are gone for good: the next save would report only what changed afterwards,
            // and a consumer PATCHing by ChangedFields -- the entire reason that list exists -- would
            // silently never persist them. This is also what SaveWhenInvalid's skip already promised in
            // the neighbouring case.
            RestorePending(changed);
            await ReportFailureAsync(ex, changed);
        }
    }

    // Puts a failed save's fields back at the FRONT of the pending set: they changed before anything
    // that arrived while the save was in flight, and ChangedFields is documented as first-seen order.
    // De-duplicating against what accumulated meanwhile, since the same field may well be in both.
    void RestorePending(IReadOnlyList<FieldIdentifier> changed)
    {
        if (_disposed) return;
        var arrivedDuringSave = _pending.ToArray(); // empty is Array.Empty<T>() -- no allocation
        _pending.Clear();
        // `changed` came out of _pending, so it is already de-duplicated.
        _pending.AddRange(changed);
        foreach (var field in arrivedDuringSave)
            if (!_pending.Contains(field))
                _pending.Add(field);
    }

    // The one place a save failure reaches the consumer. OnSaveFailed is invoked inside its own guard
    // because a handler that throws would otherwise escape into the fire-and-forget flush task and
    // vanish -- the exact silent swallowing this component's contract rules out.
    async Task ReportFailureAsync(Exception ex, IReadOnlyList<FieldIdentifier> changed)
    {
        if (!OnSaveFailed.HasDelegate)
        {
            // Not swallowed: re-raised as if it had been thrown from a lifecycle method, so an
            // ErrorBoundary catches it and an unguarded app still fails loudly.
            await DispatchExceptionAsync(ex);
            return;
        }

        try
        {
            await OnSaveFailed.InvokeAsync(new FormAutoSaveFailureEventArgs(CurrentEditContext!, ex, changed));
        }
        catch (Exception handlerEx)
        {
            // Both, and in that order: the handler's own failure is the immediate bug, and the save
            // failure it was handling would otherwise be the one thing nobody ever sees.
            await DispatchExceptionAsync(new AggregateException(
                $"{nameof(FormAutoSave)}.{nameof(OnSaveFailed)} threw while handling a failed save.",
                handlerEx, ex));
        }
    }

    /// <summary>
    /// Detaches both subscriptions and cancels any armed debounce. A pending save is deliberately
    /// NOT flushed: the component is going away because its form is, and firing a persistence call
    /// into a torn-down page is worse than dropping the last few hundred milliseconds of typing.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _subscription?.Detach();
        _validationSubscription?.Detach();
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _pending.Clear();
    }
}

/// <summary> What <see cref="FormAutoSave"/> does when a field changes while a save is still in flight. </summary>
public enum AutoSaveConcurrency
{
    /// <summary>
    /// Queue at most ONE trailing run. Changes arriving during a save accumulate, and exactly one
    /// further save runs when the in-flight one completes — no matter how many notifications arrived.
    /// The safe default: saves never overlap, so a slow endpoint can't be handed two writes that land
    /// out of order, and the last state always reaches the server.
    /// </summary>
    CoalesceTrailing,

    /// <summary>
    /// Start the save immediately, whether or not one is already running. Appropriate only when
    /// <see cref="FormAutoSave.OnSave"/> is genuinely order-independent (an idempotent PUT of the whole
    /// model, a local draft write): with a slow endpoint, two overlapping saves can complete in either
    /// order, and the older one landing last wins.
    /// </summary>
    Concurrent
}

/// <summary> The payload <see cref="FormAutoSave.OnSave"/> receives. </summary>
/// <param name="EditContext">The form's context — for reading validation state or the bound model.</param>
/// <param name="ChangedFields">
/// The fields that changed since the last save that actually ran, de-duplicated and in first-seen
/// order. Never empty. Note that a field changing does NOT imply its value differs from what was last
/// persisted — the same value re-committed still counts as a change once it reaches this component.
/// A previous save that FAILED contributes its fields here too, at the front (see
/// <see cref="FormAutoSaveFailureEventArgs"/>).
/// </param>
public sealed record FormAutoSaveEventArgs(EditContext EditContext, IReadOnlyList<FieldIdentifier> ChangedFields);

/// <summary> The payload <see cref="FormAutoSave.OnSaveFailed"/> receives. </summary>
/// <param name="EditContext">The form's context — the same one the failed <see cref="FormAutoSaveEventArgs"/> carried.</param>
/// <param name="Exception">What <see cref="FormAutoSave.OnSave"/> threw.</param>
/// <param name="ChangedFields">
/// The fields the failed save was carrying — de-duplicated, in first-seen order, never empty, and
/// exactly what its <see cref="FormAutoSaveEventArgs.ChangedFields"/> held.
/// <para>
/// They are NOT lost: the component puts them back at the front of its pending set, so the next flush
/// retries them ahead of anything that has changed since, and they reappear in that save's
/// <see cref="FormAutoSaveEventArgs.ChangedFields"/>. A save that fails permanently therefore keeps
/// them pending and re-attempts them on every subsequent flush — which is the right default for a
/// transient outage, and the reason this list is handed to you: it is what a consumer needs to
/// compensate (surface a "not saved" marker, write a local draft, stop retrying) when it is not.
/// </para>
/// </param>
public sealed record FormAutoSaveFailureEventArgs(
    EditContext EditContext, Exception Exception, IReadOnlyList<FieldIdentifier> ChangedFields);
