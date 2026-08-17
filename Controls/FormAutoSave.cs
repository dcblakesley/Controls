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
    /// Invoked with the exception when <see cref="OnSave"/> throws. When this is NOT wired the
    /// exception is re-dispatched through <see cref="ComponentBase.DispatchExceptionAsync"/> instead,
    /// i.e. it surfaces exactly as an exception thrown from a lifecycle method would — caught by an
    /// enclosing <c>ErrorBoundary</c>, otherwise fatal to the circuit. Failures are never silently
    /// swallowed; wiring this is how you opt into handling them yourself.
    /// </summary>
    [Parameter] public EventCallback<Exception> OnSaveFailed { get; set; }

    /// <summary>
    /// The clock the debounce runs on. Null (default) uses <see cref="System.TimeProvider.System"/>.
    /// Supplied so the debounce is deterministically testable — a fake provider lets a test advance
    /// time by hand instead of sleeping, in this library's suite and in a consumer's tests of their own
    /// auto-saving form. It is the only parameter here that isn't closing a hazard from the volume
    /// analysis above, and it is the .NET-idiomatic seam for exactly this.
    /// </summary>
    [Parameter] public TimeProvider? TimeProvider { get; set; }

    // Fully qualified: the parameter above shadows the type name inside this class.
    System.TimeProvider EffectiveTimeProvider => TimeProvider ?? System.TimeProvider.System;

    // The same subscribe/re-point/detach discipline the edit controls use for the OTHER EditContext
    // event, through the shared implementation. Created lazily: its handler is an instance method
    // group, which a field initializer can't take.
    FieldChangedSubscription? _subscription;

    // Fields seen since the last save that actually ran, de-duplicated, in first-seen order. A List
    // (not a HashSet) because the ORDER is part of the contract -- "what changed, in the order the
    // user touched it" -- and a form has few enough fields that the linear Contains is free.
    readonly List<FieldIdentifier> _pending = [];

    // One timer for the component's lifetime, re-armed (never re-created) per notification: ITimer.Change
    // with a fresh due time IS the trailing debounce, and re-creating one per keystroke would allocate
    // a timer per character.
    ITimer? _debounceTimer;

    // Single-threaded by construction: every mutation below happens on the renderer's dispatcher (the
    // EditContext notification arrives on it, and the timer callback is marshalled onto it by
    // InvokeAsync), so no lock is needed and none would help -- Blazor Server's circuit and WASM's
    // single thread both serialize this.
    bool _saving;
    bool _trailingQueued;
    bool _disposed;

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
        if (_subscription.SyncTo(CurrentEditContext))
        {
            // A new context means a new model: the pending FieldIdentifiers name the OLD model's
            // fields, and an in-flight debounce armed for it would save the wrong form. Both go.
            _pending.Clear();
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

        if (DebounceMilliseconds <= 0)
        {
            StartFlush();
            return;
        }

        // Re-arming an already-armed timer replaces its due time, which is what makes this trailing:
        // a burst of keystrokes keeps pushing the deadline out and only the pause fires it.
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
        _ = InvokeAsync(FlushAsync);
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
            _trailingQueued = false;

            // Read the CURRENT message store rather than re-validating (see SaveWhenInvalid). This is
            // why the component belongs after <DataAnnotationsValidator />: its OnFieldChanged handler
            // has to have validated the changed field before this runs.
            if (!SaveWhenInvalid && CurrentEditContext?.GetValidationMessages().Any() == true)
                return; // _pending deliberately survives -- the next successful save reports it all

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
            if (OnSaveFailed.HasDelegate)
                await OnSaveFailed.InvokeAsync(ex);
            else
                // Not swallowed: re-raised as if it had been thrown from a lifecycle method, so an
                // ErrorBoundary catches it and an unguarded app still fails loudly. Without this the
                // exception would die unobserved inside the discarded StartFlush task.
                await DispatchExceptionAsync(ex);
        }
    }

    /// <summary>
    /// Detaches the one subscription and cancels any armed debounce. A pending save is deliberately
    /// NOT flushed: the component is going away because its form is, and firing a persistence call
    /// into a torn-down page is worse than dropping the last few hundred milliseconds of typing.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        _subscription?.Detach();
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
/// </param>
public sealed record FormAutoSaveEventArgs(EditContext EditContext, IReadOnlyList<FieldIdentifier> ChangedFields);
