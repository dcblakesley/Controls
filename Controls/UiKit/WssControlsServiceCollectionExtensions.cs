using Controls;

// Placed in the DI namespace so `builder.Services.AddWssControlsToasts()` is discoverable without
// an extra using in Program.cs.
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// DI registration for the scoped, Server-safe WssBlazorControls toast services.
/// </summary>
public static class WssControlsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="IMessageService"/> and <see cref="INotificationService"/>
    /// implementations. Use these (with <c>&lt;MessageContainer /&gt;</c> /
    /// <c>&lt;NotificationContainer /&gt;</c>) on <b>Blazor Server</b>, where the static
    /// <c>WasmMessageService</c> / <c>WasmNotificationService</c> are unsafe (their process-static
    /// state would bleed across users). WASM apps can use either path.
    /// </summary>
    /// <example>
    /// <code>
    /// // Program.cs
    /// builder.Services.AddWssControlsToasts();
    /// </code>
    /// </example>
    public static IServiceCollection AddWssControlsToasts(this IServiceCollection services)
    {
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
