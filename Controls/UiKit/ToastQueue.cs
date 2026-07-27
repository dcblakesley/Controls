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
            var cts = new CancellationTokenSource();
            _timers[item.Id] = cts;
            _ = RemoveAfterAsync(item, cts.Token);
        }
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

    private async Task RemoveAfterAsync(TItem item, CancellationToken token)
    {
        try
        {
            // Task.Delay rejects anything over ~24.8 days (int.MaxValue ms) — cap absurd caller
            // durations there instead of throwing into this fire-and-forget task.
            var ms = Math.Min(item.Duration * 1000, int.MaxValue - 1);
            await Task.Delay(TimeSpan.FromMilliseconds(ms), token);
        }
        catch (TaskCanceledException)
        {
            return; // removed/cleared/disposed before the delay elapsed
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

    private void CancelAllTimers()
    {
        foreach (var cts in _timers.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
        _timers.Clear();
    }

    /// <summary>Cancels any pending auto-dismiss timers (called when the owning service is disposed).</summary>
    public void Dispose() => CancelAllTimers();
}
