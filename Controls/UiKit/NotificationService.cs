namespace Controls;

/// <summary>
/// Default <see cref="INotificationService"/> implementation. Holds notification state for a single
/// DI scope (one circuit on Blazor Server) when registered via <c>AddWssControlsToasts()</c>. The
/// static <see cref="WasmNotificationService"/> facade reuses this same logic over a process-static
/// instance.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly List<NotificationItem> _items = new();

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
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
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
            _ = RemoveAfterAsync(item);
        }
    }

    private async Task RemoveAfterAsync(NotificationItem item)
    {
        await Task.Delay(TimeSpan.FromSeconds(item.Duration));
        Remove(item.Id);
    }
}
