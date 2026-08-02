using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the WASM-only toast/notification services. The services hold process-static
/// state, so each test clears before and after and uses duration:0 (sticky) to avoid background-timer
/// removal racing the assertions.
/// </summary>
/// <remarks>
/// The containers themselves now fail fast off the browser (their static state is shared by every
/// circuit on Server), and bUnit runs with <c>OperatingSystem.IsBrowser() == false</c> — that guard is
/// asserted below, and the rendering assertions go through the shared <c>MessageListView</c> /
/// <c>NotificationListView</c> the containers delegate to, which is where that DOM actually comes from.
/// </remarks>
public class WasmToastTests : BunitContext
{
    [Fact]
    public void MessageContainer_throws_when_hosted_outside_the_browser()
    {
        // A Server app dropping in <WasmMessageContainer /> used to compile and work in single-user
        // dev, then leak one user's messages onto every other circuit in production.
        WasmMessageService.Clear();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Render<WasmMessageContainer>());
            Assert.Contains("WebAssembly-only", ex.Message);
            Assert.Contains("MessageContainer", ex.Message); // points at the DI-scoped replacement
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void NotificationContainer_throws_when_hosted_outside_the_browser()
    {
        WasmNotificationService.Clear();
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Render<WasmNotificationContainer>());
            Assert.Contains("WebAssembly-only", ex.Message);
            Assert.Contains("NotificationContainer", ex.Message);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    [Fact]
    public void Message_service_items_render_with_their_type_icon()
    {
        WasmMessageService.Clear();
        try
        {
            WasmMessageService.Success("Saved!", duration: 0);
            var cut = Render<MessageListView>(p => p.Add(c => c.Items, WasmMessageService.Items));

            Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent);
            Assert.NotNull(cut.Find(".wss-msg-icon-success"));
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void Notification_service_items_render_and_close_removes_from_the_service()
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("Heads up", "the details", duration: 0);
            var cut = Render<NotificationListView>(p => p
                .Add(c => c.Items, WasmNotificationService.Items)
                .Add(c => c.OnRemove, EventCallback.Factory.Create<Guid>(this, WasmNotificationService.Remove)));

            Assert.Contains("Heads up", cut.Find(".wss-notification-message").TextContent);
            Assert.Contains("the details", cut.Find(".wss-notification-description").TextContent);

            cut.Find(".wss-notification-close").Click();

            // The container re-reads Items from the service on OnChange; here the assertion is on the
            // service itself, which is the state the close button actually mutates.
            Assert.Empty(WasmNotificationService.Items);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    [Fact]
    public void Default_notification_placement_adds_no_modifier_class()
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("x", duration: 0);
            var cut = Render<NotificationListView>(p => p.Add(c => c.Items, WasmNotificationService.Items));

            var container = cut.Find(".wss-notification-container");
            Assert.DoesNotContain("wss-notification-topleft", container.ClassList);
            Assert.DoesNotContain("wss-notification-bottomright", container.ClassList);
            Assert.DoesNotContain("wss-notification-bottomleft", container.ClassList);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    [Fact]
    public void Static_notification_facade_forwards_the_new_notifications_id()
    {
        // The static facade has to forward the id the four add methods now return, or Remove(Guid) is
        // just as unreachable here as it was on the interface.
        WasmNotificationService.Clear();
        try
        {
            var id = WasmNotificationService.Success("sticky", duration: 0);
            Assert.NotEqual(Guid.Empty, id);

            WasmNotificationService.Remove(id);

            Assert.Empty(WasmNotificationService.Items);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    [Theory]
    [InlineData(NotificationPlacement.TopLeft, "wss-notification-topleft")]
    [InlineData(NotificationPlacement.BottomRight, "wss-notification-bottomright")]
    [InlineData(NotificationPlacement.BottomLeft, "wss-notification-bottomleft")]
    public void NotificationListView_forwards_Placement_to_its_class(NotificationPlacement placement, string expectedClass)
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("x", duration: 0);
            var cut = Render<NotificationListView>(p => p
                .Add(c => c.Items, WasmNotificationService.Items)
                .Add(c => c.Placement, placement));

            Assert.Contains(expectedClass, cut.Find(".wss-notification-container").ClassList);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }
}
