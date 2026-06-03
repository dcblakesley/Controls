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

    /// <inheritdoc/>
    public IReadOnlyList<MessageItem> Items => _items;

    /// <inheritdoc/>
    public event Action? OnChange;

    /// <inheritdoc/>
    public void Success(string content, double? duration = null) => Add(MessageType.Success, content, duration);
    /// <inheritdoc/>
    public void Info(string content, double? duration = null) => Add(MessageType.Info, content, duration);
    /// <inheritdoc/>
    public void Warning(string content, double? duration = null) => Add(MessageType.Warning, content, duration);
    /// <inheritdoc/>
    public void Error(string content, double? duration = null) => Add(MessageType.Error, content, duration);
    /// <inheritdoc/>
    public void Loading(string content, double? duration = null) => Add(MessageType.Loading, content, duration ?? 0);

    /// <inheritdoc/>
    public void Remove(Guid id)
    {
        CancelTimer(id);
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        CancelAllTimers();
        _items.Clear();
        OnChange?.Invoke();
    }

    private void Add(MessageType type, string content, double? duration)
    {
        var item = new MessageItem { Type = type, Content = content, Duration = duration ?? 3 };
        _items.Add(item);
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            var cts = new CancellationTokenSource();
            _timers[item.Id] = cts;
            _ = RemoveAfterAsync(item, cts.Token);
        }
    }

    private async Task RemoveAfterAsync(MessageItem item, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(item.Duration), token);
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
