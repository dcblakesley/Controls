namespace Controls;

/// <summary>Severity of a notification box.</summary>
public enum NotificationType { Success, Info, Warning, Error }

/// <summary>A single notification box tracked by an <see cref="INotificationService"/>.</summary>
public class NotificationItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public NotificationType Type { get; set; }
    public string Message { get; set; } = "";
    public string? Description { get; set; }
    public double Duration { get; set; } = 4.5; // seconds; 0 = sticky
}

/// <summary>
/// Top-right notification service. Two ways to use it:
/// <list type="bullet">
/// <item><b>Blazor Server (or WASM) — scoped, recommended:</b> register with
/// <c>builder.Services.AddWssControlsToasts()</c>, <c>@inject INotificationService</c>, and drop one
/// <c>&lt;NotificationContainer /&gt;</c> at the app root. State is per-DI-scope (per circuit on
/// Server).</item>
/// <item><b>WASM only — registration-free static:</b> call <see cref="WasmNotificationService"/>'s
/// static methods and drop <c>&lt;WasmNotificationContainer /&gt;</c>. Its process-static state is
/// unsafe on Server.</item>
/// </list>
/// Both render identically (the same <c>wss-notification-*</c> markup).
/// </summary>
public interface INotificationService
{
    /// <summary>The currently-visible notifications.</summary>
    IReadOnlyList<NotificationItem> Items { get; }

    /// <summary>Raised whenever <see cref="Items"/> changes so a container can re-render.</summary>
    event Action? OnChange;

    void Success(string message, string? description = null, double? duration = null);
    void Info(string message, string? description = null, double? duration = null);
    void Warning(string message, string? description = null, double? duration = null);
    void Error(string message, string? description = null, double? duration = null);
    void Remove(Guid id);
    void Clear();
}
