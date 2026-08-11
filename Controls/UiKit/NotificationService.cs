namespace Controls;

/// <summary>
/// Default <see cref="INotificationService"/> implementation. Holds notification state for a single
/// DI scope (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The
/// static <see cref="WasmNotificationService"/> facade reuses this same logic over a process-static
/// instance.
/// </summary>
public sealed class NotificationService : INotificationService, IDisposable
{
    private readonly ToastQueue<NotificationItem> _queue = new();

    /// <inheritdoc/>
    public IReadOnlyList<NotificationItem> Items => _queue.Items;

    /// <inheritdoc/>
    public event Action? OnChange
    {
        add => _queue.OnChange += value;
        remove => _queue.OnChange -= value;
    }

    /// <inheritdoc/>
    public Guid Success(string message, string? description = null, double? duration = null) => Add(NotificationType.Success, message, description, duration);
    /// <inheritdoc/>
    public Guid Info(string message, string? description = null, double? duration = null) => Add(NotificationType.Info, message, description, duration);
    /// <inheritdoc/>
    public Guid Warning(string message, string? description = null, double? duration = null) => Add(NotificationType.Warning, message, description, duration);
    /// <inheritdoc/>
    public Guid Error(string message, string? description = null, double? duration = null) => Add(NotificationType.Error, message, description, duration);

    /// <inheritdoc/>
    public void Remove(Guid id) => _queue.Remove(id);

    /// <inheritdoc/>
    public void Clear() => _queue.Clear();

    /// <inheritdoc/>
    public void Pause(Guid id) => _queue.Pause(id);

    /// <inheritdoc/>
    public void Resume(Guid id) => _queue.Resume(id);

    private Guid Add(NotificationType type, string message, string? description, double? duration)
    {
        var item = new NotificationItem
        {
            Type = type,
            Message = message,
            Description = description,
            Duration = duration ?? 4.5
        };
        _queue.Add(item);
        return item.Id;
    }

    /// <summary>Cancels any pending auto-dismiss timers (called when the DI scope is torn down).</summary>
    public void Dispose() => _queue.Dispose();
}
