namespace Controls;

/// <summary>Severity of a notification box.</summary>
public enum NotificationType { Success, Info, Warning, Error }

/// <summary>A single notification box tracked by an <see cref="INotificationService"/>.</summary>
public class NotificationItem : IToastItem
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

    /// <summary>
    /// Shows a notification and returns its id, which can be passed to <see cref="Remove"/> to dismiss
    /// that one notification programmatically. The case for it is a sticky notification
    /// (<c>duration: 0</c>), which has no auto-dismiss timer of its own and otherwise waits for the
    /// user's close button.
    /// </summary>
    Guid Success(string message, string? description = null, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Info(string message, string? description = null, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Warning(string message, string? description = null, double? duration = null);
    /// <inheritdoc cref="Success"/>
    Guid Error(string message, string? description = null, double? duration = null);
    void Remove(Guid id);
    void Clear();

    /// <summary>
    /// Pauses <paramref name="id"/>'s auto-dismiss countdown (WCAG 2.2.1) -- call while the user is
    /// hovering the pointer over the notification or has kept keyboard focus inside it, and pair
    /// with <see cref="Resume"/> on the corresponding leave/blur. A no-op for a sticky notification
    /// (no timer to pause) or an id no longer tracked. <c>NotificationContainer</c>/
    /// <c>WasmNotificationContainer</c> already wire this from <see cref="NotificationListView"/>'s
    /// hover/focus events -- most consumers never call it directly.
    /// </summary>
    void Pause(Guid id);

    /// <summary>
    /// Resumes <paramref name="id"/>'s auto-dismiss countdown from a fresh full duration -- not the
    /// time remaining when <see cref="Pause"/> was called. See <see cref="ToastQueue{TItem}.Resume"/>
    /// for why that's the simplest correct behavior. A no-op for a sticky notification or an id no
    /// longer tracked.
    /// </summary>
    void Resume(Guid id);
}
