namespace Controls;

/// <summary>
/// Registration-free <b>static</b> top-right notification facade for <b>Blazor WebAssembly</b>.
/// Call the static methods and drop a single <c>&lt;WasmNotificationContainer /&gt;</c> at the app
/// root.
/// <para>
/// It forwards to a single process-<c>static</c> <see cref="NotificationService"/> — safe in a
/// single-user WASM app, not on Blazor Server. <b>On Server, use the scoped
/// <see cref="INotificationService"/></b> (register with <c>AddWssControlsToasts()</c>) and
/// <c>&lt;NotificationContainer /&gt;</c> instead.
/// </para>
/// </summary>
public static class WasmNotificationService
{
    private static readonly NotificationService Instance = new();

    /// <inheritdoc cref="INotificationService.Items"/>
    public static IReadOnlyList<NotificationItem> Items => Instance.Items;

    /// <inheritdoc cref="INotificationService.OnChange"/>
    public static event Action? OnChange
    {
        add => Instance.OnChange += value;
        remove => Instance.OnChange -= value;
    }

    /// <inheritdoc cref="INotificationService.Success"/>
    public static Guid Success(string message, string? description = null, double? duration = null) => Instance.Success(message, description, duration);
    /// <inheritdoc cref="INotificationService.Info"/>
    public static Guid Info(string message, string? description = null, double? duration = null) => Instance.Info(message, description, duration);
    /// <inheritdoc cref="INotificationService.Warning"/>
    public static Guid Warning(string message, string? description = null, double? duration = null) => Instance.Warning(message, description, duration);
    /// <inheritdoc cref="INotificationService.Error"/>
    public static Guid Error(string message, string? description = null, double? duration = null) => Instance.Error(message, description, duration);

    /// <summary>Dismisses one notification by the id its factory method returned.</summary>
    public static void Remove(Guid id) => Instance.Remove(id);

    /// <summary>Removes all notifications (primarily for tests).</summary>
    public static void Clear() => Instance.Clear();
}
