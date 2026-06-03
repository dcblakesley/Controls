using System.Collections.Concurrent;

namespace Controls;

/// <summary>
/// Default <see cref="INotificationService"/> implementation. Holds notification state for a single
/// DI scope (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The
/// static <see cref="WasmNotificationService"/> facade reuses this same logic over a process-static
/// instance.
/// </summary>
public sealed class NotificationService : INotificationService, IDisposable
{
    private readonly List<NotificationItem> _items = new();

    // One cancellation source per auto-dismissing notification, so Remove/Clear/Dispose can cancel a
    // pending Task.Delay instead of leaving it to fire later against torn-down state.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _timers = new();

    /// <inheritdoc/>
    public IReadOnlyList<NotificationItem> Items => _items;

    /// <inheritdoc/>
    public event Action? OnChange;

    /// <inheritdoc/>
    public void Success(string message, string? description = null, double? duration = null) => Add(NotificationType.Success, message, description, duration);
    /// <inheritdoc/>
    public void Info(string message, string? description = null, double? duration = null) => Add(NotificationType.Info, message, description, duration);
    /// <inheritdoc/>
    public void Warning(string message, string? description = null, double? duration = null) => Add(NotificationType.Warning, message, description, duration);
    /// <inheritdoc/>
    public void Error(string message, string? description = null, double? duration = null) => Add(NotificationType.Error, message, description, duration);

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

    private void Add(NotificationType type, string message, string? description, double? duration)
    {
        var item = new NotificationItem
        {
            Type = type,
            Message = message,
            Description = description,
            Duration = duration ?? 4.5
        };
        _items.Add(item);
        OnChange?.Invoke();

        if (item.Duration > 0)
        {
            var cts = new CancellationTokenSource();
            _timers[item.Id] = cts;
            _ = RemoveAfterAsync(item, cts.Token);
        }
    }

    private async Task RemoveAfterAsync(NotificationItem item, CancellationToken token)
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
