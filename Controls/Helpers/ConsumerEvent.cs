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
/// through a splatted attribute dictionary — the Razor compiler emits <see cref="EventCallback{TValue}"/>
/// (typed to the handler method's own parameter type, which need not match the DOM event's usual args
/// type — e.g. a method typed <c>EventArgs</c> bound to <c>onkeydown</c> compiles to
/// <see cref="EventCallback{TValue}">EventCallback&lt;EventArgs&gt;</see>, not
/// <c>EventCallback&lt;KeyboardEventArgs&gt;</c>) or an untyped <see cref="EventCallback"/> for
/// <c>@onkeydown="H"</c> written on a component, and the renderer additionally honors bare delegates —
/// <see cref="Action{T}"/>, <see cref="Action"/>, <see cref="Func{TArgs, TResult}">Func&lt;TArgs,
/// Task&gt;</see>, and <see cref="Func{TResult}">Func&lt;Task&gt;</see>. Anything else is ignored rather
/// than throwing, since a stray value here must never break the component's own behavior.
/// </para>
/// <para>
/// Two shapes are deliberately absent as their own <c>case</c>: <c>Action&lt;object&gt;</c> and
/// <c>Func&lt;object, Task&gt;</c> already invoke today, matched by the <c>Action&lt;TArgs&gt;</c> and
/// <c>Func&lt;TArgs, Task&gt;</c> cases respectively — <c>Action</c>/<c>Func</c>'s parameter position is
/// contravariant (<c>in T</c>), so a delegate instance typed to accept <c>object</c> (or any
/// supertype of the method's <c>TArgs</c>) already satisfies a pattern typed to
/// <c>TArgs</c>. Adding an explicit case for either is not just redundant, the compiler
/// rejects it outright (CS8120, unreachable pattern). A closed <c>EventCallback&lt;TOther&gt;</c> for
/// some <c>TArgs</c>-unrelated <c>TOther</c> other than <see cref="EventArgs"/> itself
/// has no such escape hatch: <c>EventCallback&lt;T&gt;</c> isn't variant, and its
/// <c>AsUntyped()</c> escape hatch is <see langword="internal"/> to
/// <c>Microsoft.AspNetCore.Components</c> — inaccessible here and not reachable via reflection without
/// giving up trim-safety, so it stays unhandled (falls to <c>default</c>, a no-op).
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
            case EventCallback<EventArgs> baseTypedCallback:
                return baseTypedCallback.InvokeAsync(args);
            case EventCallback untypedCallback:
                return untypedCallback.InvokeAsync(args);
            case Func<TArgs, Task> asyncHandler:
                return asyncHandler(args);
            case Func<Task> asyncNoArgsHandler:
                return asyncNoArgsHandler();
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
