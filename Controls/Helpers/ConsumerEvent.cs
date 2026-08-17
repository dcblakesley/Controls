namespace Controls.Helpers;

/// <summary>
/// Re-invokes a consumer's own splatted DOM event handler from a component handler bound to the
/// <em>same</em> element.
/// </summary>
/// <remarks>
/// <para>
/// Blazor does not merge duplicate attribute names: an explicit <c>@onkeydown="..."</c> written after
/// an <c>@attributes</c> splat wins outright, so a same-named handler the consumer splatted is
/// silently discarded rather than chained. Where a component owns an event on the very element it
/// splats onto, it therefore has to withhold that event name from the splat
/// (<see cref="AttributeSplat.RestExcept"/>) and re-invoke the consumer's handler itself — which is
/// what this helper does.
/// </para>
/// <para>
/// <strong>Ordering is fixed: the component's own behavior runs FIRST, then the consumer's handler.</strong>
/// The workaround consumers reach for today is a wrapping element, where the inner element's handler
/// (the library's) fires before the ancestor's (theirs) by ordinary bubbling — so library-first is the
/// ordering their code already assumes.
/// </para>
/// <para>
/// The component's handler must chain <em>unconditionally</em>, including on the paths where its own
/// logic early-returns (disabled, <c>Keyboard="false"</c>, a key it doesn't handle). Only the
/// component's state mutation is suppressed in those cases; a consumer listening for keystrokes on a
/// disabled control must still hear them.
/// </para>
/// <para>
/// Type-pattern dispatch rather than reflection, so this stays trim/AOT-clean
/// (<c>Controls.csproj</c> sets <c>IsAotCompatible</c>). It covers the shapes Blazor actually accepts
/// through a splatted attribute dictionary — the Razor compiler emits a typed
/// <see cref="EventCallback{TValue}"/> for <c>@onkeydown="H"</c> written on a component, and the
/// renderer additionally honors bare delegates — and anything else is ignored rather than throwing,
/// since a stray value here must never break the component's own behavior.
/// </para>
/// </remarks>
internal static class ConsumerEvent
{
    /// <summary>
    /// Invokes the handler stored under <paramref name="eventName"/> in the captured
    /// <paramref name="attributes"/>, if the consumer supplied one. A no-op (and allocation-free)
    /// otherwise, which is the overwhelmingly common case.
    /// </summary>
    public static Task InvokeAsync<TArgs>(
        IReadOnlyDictionary<string, object>? attributes, string eventName, TArgs args)
        where TArgs : EventArgs =>
        attributes is not null && attributes.TryGetValue(eventName, out var handler)
            ? InvokeAsync(handler, args)
            : Task.CompletedTask;

    /// <summary>
    /// Invokes an already-captured consumer handler. Used where the component pulled the handler out
    /// of the dictionary ahead of time (<see cref="EditControlBase{TValue}"/>'s focus tracking).
    /// </summary>
    public static Task InvokeAsync<TArgs>(object? handler, TArgs args) where TArgs : EventArgs
    {
        switch (handler)
        {
            case EventCallback<TArgs> typedCallback:
                return typedCallback.InvokeAsync(args);
            case EventCallback untypedCallback:
                return untypedCallback.InvokeAsync(args);
            case Func<TArgs, Task> asyncHandler:
                return asyncHandler(args);
            case Action<TArgs> syncHandler:
                syncHandler(args);
                return Task.CompletedTask;
            case Action bareHandler:
                bareHandler();
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }
}
