namespace Controls;

/// <summary>Severity of a <see cref="WasmNotificationService"/> notification.</summary>
public enum NotificationType { Success, Info, Warning, Error }

/// <summary>A single notification box tracked by <see cref="WasmNotificationService"/>.</summary>
public class NotificationItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public string Message { get; set; } = "";
    public string? Description { get; set; }
    public double Duration { get; set; } = 4.5; // seconds; 0 = sticky
}

/// <summary>
/// Registration-free top-right notification service for <b>Blazor WebAssembly only</b>.
/// Call the static methods and drop a single <c>&lt;WasmNotificationContainer /&gt;</c> at
/// the app root. Uses process-<c>static</c> state — safe in single-user WASM, not on Server.
/// </summary>
public static class WasmNotificationService
{
    private static readonly List<NotificationItem> _items = new();

    public static IReadOnlyList<NotificationItem> Items => _items;

    public static event Action? OnChange;

    public static void Success(string message, string? description = null, double? duration = null) => Add(NotificationType.Success, message, description, duration);
    public static void Info(string message, string? description = null, double? duration = null) => Add(NotificationType.Info, message, description, duration);
    public static void Warning(string message, string? description = null, double? duration = null) => Add(NotificationType.Warning, message, description, duration);
    public static void Error(string message, string? description = null, double? duration = null) => Add(NotificationType.Error, message, description, duration);

    public static void Remove(Guid id)
    {
        if (_items.RemoveAll(i => i.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    /// <summary>Removes all notifications (primarily for tests).</summary>
    public static void Clear()
    {
        _items.Clear();
        OnChange?.Invoke();
    }

    private static void Add(NotificationType type, string message, string? description, double? duration)
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

    private static async Task RemoveAfterAsync(NotificationItem item)
    {
        await Task.Delay(TimeSpan.FromSeconds(item.Duration));
        Remove(item.Id);
    }
}
