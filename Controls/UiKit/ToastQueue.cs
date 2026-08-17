using System.Collections.Concurrent;

namespace Controls;

/// <summary>Minimal shape <see cref="ToastQueue{TItem}"/> needs from a tracked toast item.</summary>
public interface IToastItem
{
    Guid Id { get; }
    /// <summary>Seconds until auto-dismiss; 0 (or less) means sticky (no timer).</summary>
    double Duration { get; }
}

/// <summary>
/// Shared add/remove/clear/timer-management engine backing <see cref="MessageService"/> and
/// <see cref="NotificationService"/>. The two differ only in their own severity-specific <c>Add</c>
/// overloads (and <see cref="MessageService"/>'s extra <see cref="MessageType.Loading"/> case) — this
/// owns everything else: the tracked item list, one auto-dismiss timer per item, and the
/// <see cref="OnChange"/> notification a container subscribes to for re-rendering.
/// </summary>
public sealed class ToastQueue<TItem> : IDisposable where TItem : IToastItem
{
    private readonly List<TItem> _items = new();

    // One cancellation source per auto-dismissing item, so Remove/Clear/Dispose can cancel a
    // pending Task.Delay instead of leaving it to fire later against torn-down state.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();

    // Guards _items. The auto-dismiss Task.Delay continuation (RemoveAfterAsync) resumes on a
    // threadpool thread and would otherwise mutate the list while the renderer enumerates Items on
    // the circuit thread (Blazor Server) — a "collection modified"/torn-read race. The Items getter
    // returns a snapshot so callers always enumerate a stable copy. OnChange is raised outside the
    // lock to avoid re-entrancy if a handler reads Items.
    private readonly object _gate = new();

    /// <summary>The currently-tracked items, snapshotted under the lock.</summary>
    public IReadOnlyList<TItem> Items
    {
        get { lock (_gate) { return _items.ToArray(); } }
    }

    /// <summary>Raised whenever <see cref="Items"/> changes so a container can re-render.</summary>
    public event Action? OnChange;

    /// <summary>Tracks <paramref name="item"/> and starts its auto-dismiss timer when its
    /// <see cref="IToastItem.Duration"/> is positive.</summary>
    public void Add(TItem item)
    {
        lock (_gate) { _items.Add(item); }
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            StartTimer(item);
        }
    }

    /// <summary>
    /// Pauses <paramref name="id"/>'s auto-dismiss countdown without removing the toast (WCAG 2.2.1):
    /// cancels its pending timer, leaving the item tracked so <see cref="Resume"/> can restart it.
    /// A silent no-op in every case that isn't "an item with a live timer": a sticky item (Duration
    /// &lt;= 0 never had one), an id no longer tracked (already removed/cleared), an id already
    /// paused, and a call that races the timer's own expiry -- the same TryRemove ownership
    /// handshake <c>CancelTimer</c> uses elsewhere guarantees exactly one of "Pause" or "the timer
    /// firing" wins that race, never both.
    /// </summary>
    public void Pause(Guid id) => CancelTimer(id);

    /// <summary>
    /// Resumes <paramref name="id"/>'s auto-dismiss countdown from a fresh full
    /// <see cref="IToastItem.Duration"/> -- not the time remaining when <see cref="Pause"/> was
    /// called. Restarting the full duration is the simplest correct behavior: tracking a partial
    /// remainder would need a wall-clock timestamp threaded through Pause/Resume for a difference a
    /// user is unlikely to notice at toast-scale (3-4.5s) durations. Calling Resume on an item that
    /// was never paused -- e.g. a mouseleave and a focusout both firing as the pointer and keyboard
    /// focus leave the same toast together -- is not an error; it just restarts an already-running
    /// timer, which is harmless. A no-op for a sticky item (no timer to restart) or an id no longer
    /// tracked (already removed/cleared).
    /// </summary>
    public void Resume(Guid id)
    {
        TItem? item;
        lock (_gate) { item = _items.FirstOrDefault(i => i.Id == id); }
        if (item is null || item.Duration <= 0)
        {
            return;
        }

        StartTimer(item);
    }

    public void Remove(Guid id)
    {
        CancelTimer(id);
        bool removed;
        lock (_gate) { removed = _items.RemoveAll(i => i.Id == id) > 0; }
        if (removed)
        {
            OnChange?.Invoke();
        }
    }

    public void Clear()
    {
        CancelAllTimers();
        lock (_gate) { _items.Clear(); }
        OnChange?.Invoke();
    }

    // Shared by Add (always a fresh id, so the claim below is a no-op there) and Resume (which can
    // legitimately race a still-running timer for the same id -- e.g. two Resume calls back to
    // back). Claims any existing entry for item.Id the same TryRemove way CancelTimer does before
    // installing the new one, so this can never Cancel() a source CancelTimer already disposed, or
    // vice versa, and two StartTimer calls for the same id can't both think they own the slot.
    private void StartTimer(TItem item)
    {
        if (_timers.TryRemove(item.Id, out var old))
        {
            old.Cancel();
            old.Dispose();
        }

        var cts = new CancellationTokenSource();
        // Read the token BEFORE publishing the source. The instant `cts` is in the dictionary another
        // thread can claim it (Pause/Remove/Clear/Dispose all TryRemove), Cancel it and Dispose it --
        // and CancellationTokenSource.Token throws ObjectDisposedException after that, out of THIS
        // thread's Pause/Resume call. A token captured first stays usable: Cancel always precedes
        // Dispose here, so the worst case is an already-cancelled token, which Task.Delay honors
        // without ever touching the disposed source.
        var token = cts.Token;
        _timers[item.Id] = cts;
        _ = RemoveAfterAsync(item, token);
    }

    private async Task RemoveAfterAsync(TItem item, CancellationToken token)
    {
        try
        {
            // Task.Delay rejects anything over ~24.8 days (int.MaxValue ms) — cap absurd caller
            // durations there instead of throwing into this fire-and-forget task.
            var ms = Math.Min(item.Duration * 1000, int.MaxValue - 1);
            await Task.Delay(TimeSpan.FromMilliseconds(ms), token);
        }
        catch (OperationCanceledException)
        {
            return; // removed/cleared/disposed before the delay elapsed (TaskCanceledException derives from this)
        }
        catch (ObjectDisposedException)
        {
            return; // the source was claimed and disposed as this fire-and-forget task started
        }

        Remove(item.Id);
    }

    private void CancelTimer(Guid id)
    {
        if (_timers.TryRemove(id, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    // Claims each entry the same exclusive way CancelTimer does (TryRemove is the ownership handshake)
    // instead of walking a Values snapshot: a toast expiring on a threadpool thread runs
    // Remove -> CancelTimer concurrently with a user's Clear()/Dispose(), and a snapshot hands BOTH
    // sides the same CancellationTokenSource -- whichever loses the race then Cancel()s a source the
    // winner already disposed, throwing ObjectDisposedException out of Clear() (a circuit error on
    // Blazor Server) or into the fire-and-forget timer task. Enumerating the dictionary itself is
    // lock-free and tolerates concurrent mutation, and TryRemove guarantees exactly one canceller per
    // entry. No trailing Clear(): every key seen here is removed by the claim, and blanket-clearing
    // would silently drop (uncancelled) any entry a concurrent Add slipped in mid-enumeration.
    private void CancelAllTimers()
    {
        foreach (var entry in _timers)
        {
            CancelTimer(entry.Key);
        }
    }

    /// <summary>Cancels any pending auto-dismiss timers (called when the owning service is disposed).</summary>
    public void Dispose() => CancelAllTimers();
}
