using System.Collections.Concurrent;

namespace Controls;

/// <summary>
/// Default <see cref="IMessageService"/> implementation. Holds toast state for a single DI scope
/// (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The static
/// <see cref="WasmMessageService"/> facade reuses this same logic over a process-static instance.
/// </summary>
public sealed class MessageService : IMessageService, IDisposable
{
    private readonly List<MessageItem> _items = new();

    // One cancellation source per auto-dismissing toast, so Remove/Clear/Dispose can cancel a
    // pending Task.Delay instead of leaving it to fire later against torn-down state.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();

    // Guards _items. The auto-dismiss Task.Delay continuation (RemoveAfterAsync) resumes on a
    // threadpool thread and would otherwise mutate the list while the renderer enumerates Items on
    // the circuit thread (Blazor Server) — a "collection modified"/torn-read race. The Items getter
    // returns a snapshot so callers always enumerate a stable copy. OnChange is raised outside the
    // lock to avoid re-entrancy if a handler reads Items.
    private readonly object _gate = new();

    /// <inheritdoc/>
    public IReadOnlyList<MessageItem> Items
    {
        get { lock (_gate) { return _items.ToArray(); } }
    }

    /// <inheritdoc/>
    public event Action? OnChange;

    /// <inheritdoc/>
    public Guid Success(string content, double? duration = null) => Add(MessageType.Success, content, duration);
    /// <inheritdoc/>
    public Guid Info(string content, double? duration = null) => Add(MessageType.Info, content, duration);
    /// <inheritdoc/>
    public Guid Warning(string content, double? duration = null) => Add(MessageType.Warning, content, duration);
    /// <inheritdoc/>
    public Guid Error(string content, double? duration = null) => Add(MessageType.Error, content, duration);
    /// <inheritdoc/>
    public Guid Loading(string content, double? duration = null) => Add(MessageType.Loading, content, duration ?? 0);

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public void Clear()
    {
        CancelAllTimers();
        lock (_gate) { _items.Clear(); }
        OnChange?.Invoke();
    }

    private Guid Add(MessageType type, string content, double? duration)
    {
        var item = new MessageItem { Type = type, Content = content, Duration = duration ?? 3 };
        lock (_gate) { _items.Add(item); }
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            var cts = new CancellationTokenSource();
            _timers[item.Id] = cts;
            _ = RemoveAfterAsync(item, cts.Token);
        }

        return item.Id;
    }

    private async Task RemoveAfterAsync(MessageItem item, CancellationToken token)
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

    /// <summary>Cancels any pending auto-dismiss timers (called when the DI scope is torn down).</summary>
    public void Dispose() => CancelAllTimers();
}
