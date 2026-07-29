namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the WASM-only toast/notification containers. The services hold
/// process-static state, so each test clears before and after and uses duration:0 (sticky)
/// to avoid background-timer removal racing the assertions.
/// </summary>
public class WasmToastTests : BunitContext
{
    [Fact]
    public void MessageContainer_renders_active_message_with_type_icon()
    {
        WasmMessageService.Clear();
        try
        {
            WasmMessageService.Success("Saved!", duration: 0);
            var cut = Render<WasmMessageContainer>();

            Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent);
            Assert.NotNull(cut.Find(".wss-msg-icon-success"));
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void NotificationContainer_renders_then_close_removes()
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("Heads up", "the details", duration: 0);
            var cut = Render<WasmNotificationContainer>();

            Assert.Contains("Heads up", cut.Find(".wss-notification-message").TextContent);
            Assert.Contains("the details", cut.Find(".wss-notification-description").TextContent);

            cut.Find(".wss-notification-close").Click();
            Assert.Empty(cut.FindAll(".wss-notification"));
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    [Fact]
    public void NotificationContainer_default_placement_adds_no_modifier_class()
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("x", duration: 0);
            var cut = Render<WasmNotificationContainer>();

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
            var cut = Render<WasmNotificationContainer>();
            Assert.NotEqual(Guid.Empty, id);

            WasmNotificationService.Remove(id);

            Assert.Empty(WasmNotificationService.Items);
            // The container re-renders off OnChange via InvokeAsync, so it lands on the renderer's
            // dispatcher rather than synchronously on this thread.
            cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".wss-notification")));
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
    public void NotificationContainer_forwards_Placement_to_its_class(NotificationPlacement placement, string expectedClass)
    {
        WasmNotificationService.Clear();
        try
        {
            WasmNotificationService.Info("x", duration: 0);
            var cut = Render<WasmNotificationContainer>(p => p.Add(c => c.Placement, placement));

            Assert.Contains(expectedClass, cut.Find(".wss-notification-container").ClassList);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }
}
