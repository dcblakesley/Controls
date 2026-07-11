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
    public void Disposed_MessageContainer_unsubscribes_from_the_service()
    {
        Services.AddWssControlsToasts();
        var svc = Services.GetRequiredService<IMessageService>();
        var cut = RenderComponent<MessageContainer>();

        DisposeComponents();

        // If the container leaked its OnChange subscription, this would StateHasChanged a
        // disposed component and throw.
        svc.Success("after dispose", duration: 0);
        Assert.Single(svc.Items);
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
    public void Loading_returns_an_id_that_dismisses_the_sticky_toast()
    {
        // A loading toast is sticky (duration 0) and has no close button, so the only way to
        // dismiss just it (rather than Clear()-ing everything) is the id returned from Loading.
        var svc = new MessageService();
        svc.Success("other", duration: 0);
        var loadingId = svc.Loading("Saving...");

        Assert.Equal(2, svc.Items.Count);

        svc.Remove(loadingId);

        Assert.Single(svc.Items);                                 // only the loading toast went away
        Assert.DoesNotContain(svc.Items, m => m.Id == loadingId);
        Assert.Contains(svc.Items, m => m.Content == "other");    // the other toast survived
    }

    [Fact]
    public void AddWssControlsToasts_registers_both_services_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddWssControlsToasts();

        Assert.Contains(services, d => d.ServiceType == typeof(IMessageService) && d.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(services, d => d.ServiceType == typeof(INotificationService) && d.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Error_message_goes_to_an_assertive_alert_region()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Error("boom", duration: 0);

        var cut = RenderComponent<MessageContainer>();
        // The error renders inside the always-present assertive region...
        var assertive = cut.Find(".wss-msg-region[role=alert]");
        Assert.Equal("assertive", assertive.GetAttribute("aria-live"));
        Assert.Contains("boom", assertive.TextContent);
        // ...while the polite region stays present (so it's ready for later polite toasts) but empty.
        var polite = cut.Find(".wss-msg-region[role=status]");
        Assert.Equal("polite", polite.GetAttribute("aria-live"));
        Assert.Empty(polite.QuerySelectorAll(".wss-msg"));
    }

    [Fact]
    public void Non_error_message_goes_to_a_polite_status_region()
    {
        Services.AddWssControlsToasts();
        Services.GetRequiredService<IMessageService>().Info("hi", duration: 0);

        var cut = RenderComponent<MessageContainer>();
        var polite = cut.Find(".wss-msg-region[role=status]");
        Assert.Equal("polite", polite.GetAttribute("aria-live"));
        Assert.Contains("hi", polite.TextContent);
        // No spurious alert: the assertive region is present but empty.
        Assert.Empty(cut.Find(".wss-msg-region[role=alert]").QuerySelectorAll(".wss-msg"));
    }

    [Fact]
    public void Loading_with_no_duration_stays_sticky()
    {
        var svc = new MessageService();
        svc.Loading("working");   // duration defaults to 0 -> no auto-dismiss timer
        Assert.Single(svc.Items);
    }

    [Fact]
    public async Task Message_auto_removes_after_its_duration()
    {
        var svc = new MessageService();
        svc.Success("bye", duration: 0.05);   // 50ms
        Assert.Single(svc.Items);

        // Poll to a deadline instead of racing one fixed wall-clock wait against the real
        // auto-dismiss timer. RemoveAfterAsync's post-Task.Delay continuation resumes on the
        // threadpool, which can be starved under CI load, so a single Task.Delay(400) was
        // intermittently too short. A bounded poll passes on a slow box without making a fast one wait.
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (svc.Items.Count > 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        Assert.Empty(svc.Items);
    }

    [Fact]
    public async Task Clear_cancels_pending_auto_dismiss()
    {
        var svc = new MessageService();
        svc.Success("x", duration: 0.05);
        svc.Clear();
        Assert.Empty(svc.Items);

        // The cancelled timer must not throw or resurrect/double-remove later.
        await Task.Delay(250);
        Assert.Empty(svc.Items);
    }
}
