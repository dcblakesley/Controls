using Microsoft.Extensions.DependencyInjection;

namespace FormTesting.Client.Tests;

/// <summary>
/// Tests for the scoped (Server-safe) toast variant: the DI-registered IMessageService /
/// INotificationService + their MessageContainer / NotificationContainer hosts. Unlike the static
/// Wasm* services, state lives on the instance, so it does not bleed across users/circuits.
/// </summary>
public class ScopedToastTests : TestContext
{
    [Fact]
    public void MessageContainer_renders_messages_from_injected_service()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        svc.Success("Scoped saved", duration: 0);

        var cut = RenderComponent<MessageContainer>();

        Assert.Contains("Scoped saved", cut.Find(".wss-msg-content").TextContent);
        Assert.NotNull(cut.Find(".wss-msg-icon-success"));
    }

    [Fact]
    public void NotificationContainer_renders_then_close_removes()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<INotificationService>();
        svc.Info("Scoped notice", "the details", duration: 0);

        var cut = RenderComponent<NotificationContainer>();

        Assert.Contains("Scoped notice", cut.Find(".wss-notification-message").TextContent);
        Assert.Contains("the details", cut.Find(".wss-notification-description").TextContent);

        cut.Find(".wss-notification-close").Click();
        Assert.Empty(cut.FindAll(".wss-notification"));
    }

    [Fact]
    public void Two_message_service_instances_do_not_share_state()
    {
        // The whole point of the scoped variant vs. the static WasmMessageService: independent
        // instances (e.g. different Server circuits) keep separate state.
        var a = new MessageService();
        var b = new MessageService();

        a.Success("only in a", duration: 0);

        Assert.Single(a.Items);
        Assert.Empty(b.Items);
    }

    [Fact]
    public void AddWssControlsToasts_registers_both_services_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddWssControlsToasts();

        Assert.Contains(services, d => d.ServiceType == typeof(IMessageService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(INotificationService) && d.Lifetime == ServiceLifetime.Scoped);
    }
}
