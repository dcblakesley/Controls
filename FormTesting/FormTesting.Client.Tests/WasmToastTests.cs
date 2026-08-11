using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the WASM-only toast/notification services. The services hold process-static
/// state, so each test clears before and after and uses duration:0 (sticky) to avoid background-timer
/// removal racing the assertions.
/// </summary>
/// <remarks>
/// The containers fail fast on the Blazor Server renderer only (their static state is shared by every
/// circuit there), which the host-guard tests below drive through bUnit's <c>SetRendererInfo</c>. The
/// rendering assertions go through the shared <c>MessageListView</c> / <c>NotificationListView</c> the
/// containers delegate to, which is where that DOM actually comes from.
/// </remarks>
public class WasmToastTests : BunitContext
{
    // The four RendererInfo.Name values the framework's own renderers report. "Server" is the only
    // host where one process serves more than one user, so it is the only one the containers refuse.
    static readonly RendererInfo StaticSsr = new("Static", isInteractive: false);
    static readonly RendererInfo Server = new("Server", isInteractive: true);
    static readonly RendererInfo WebAssembly = new("WebAssembly", isInteractive: true);
    static readonly RendererInfo WebView = new("WebView", isInteractive: true);

    [Fact]
    public void MessageContainer_throws_on_the_Blazor_Server_renderer()
    {
        // A Server app dropping in <WasmMessageContainer /> compiles and works in single-user dev,
        // then leaks one user's messages onto every other circuit in production.
        WasmMessageService.Clear();
        try
        {
            SetRendererInfo(Server);
            var ex = Assert.Throws<InvalidOperationException>(() => Render<WasmMessageContainer>());
            Assert.Contains("Blazor Server", ex.Message);
            Assert.Contains("InteractiveAuto", ex.Message); // names the nondeterministic Auto case
            Assert.Contains("MessageContainer", ex.Message); // points at the DI-scoped replacement
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void NotificationContainer_throws_on_the_Blazor_Server_renderer()
    {
        WasmNotificationService.Clear();
        try
        {
            SetRendererInfo(Server);
            var ex = Assert.Throws<InvalidOperationException>(() => Render<WasmNotificationContainer>());
            Assert.Contains("Blazor Server", ex.Message);
            Assert.Contains("InteractiveAuto", ex.Message);
            Assert.Contains("NotificationContainer", ex.Message);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }

    public static TheoryData<string, bool> PermittedRenderers() => new()
    {
        // WebView is Blazor Hybrid (MAUI/WPF/WinForms BlazorWebView): outside the browser, so the old
        // !OperatingSystem.IsBrowser() guard hard-threw there -- yet Hybrid is one process serving
        // exactly ONE user, which is the very condition that makes the process-static state safe.
        { "WebView", true },
        { "WebAssembly", true },
        // A WASM app's own prerender pass. OnAfterRender does not run during prerender, but the guard
        // must not reject it if the host ever renders statically and interactively in one process.
        { "Static", false },
    };

    [Theory]
    [MemberData(nameof(PermittedRenderers))]
    public void Containers_render_on_every_single_user_host(string rendererName, bool isInteractive)
    {
        WasmMessageService.Clear();
        WasmNotificationService.Clear();
        try
        {
            SetRendererInfo(new RendererInfo(rendererName, isInteractive));

            Assert.NotNull(Render<WasmMessageContainer>().Instance);
            Assert.NotNull(Render<WasmNotificationContainer>().Instance);
        }
        finally
        {
            WasmMessageService.Clear();
            WasmNotificationService.Clear();
        }
    }

    [Fact]
    public void A_permitted_container_actually_renders_the_static_services_items()
    {
        // Not just "it did not throw": the container must be live and wired to the static service on
        // a Hybrid host, which is the whole point of permitting it.
        WasmMessageService.Clear();
        try
        {
            SetRendererInfo(WebView);
            var cut = Render<WasmMessageContainer>();

            WasmMessageService.Success("Saved!", duration: 0);
            cut.WaitForAssertion(() => Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent));
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void The_static_prerender_renderer_is_not_mistaken_for_a_server_circuit()
    {
        // Guards the exact discrimination the fix rests on: "Static" is a prerender pass whose output
        // is thrown away, "Server" is a live multi-circuit host. Only the latter is refused.
        Assert.NotEqual(StaticSsr.Name, Server.Name);
        SetRendererInfo(StaticSsr);
        Assert.NotNull(Render<WasmNotificationContainer>().Instance);
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

    // ---- M1: message close button (WASM static facade) ----

    [Fact]
    public void Message_service_items_render_and_close_removes_from_the_service()
    {
        WasmMessageService.Clear();
        try
        {
            WasmMessageService.Success("Saved!", duration: 0);
            var cut = Render<MessageListView>(p => p
                .Add(c => c.Items, WasmMessageService.Items)
                .Add(c => c.OnRemove, EventCallback.Factory.Create<Guid>(this, WasmMessageService.Remove)));

            Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent);

            cut.Find(".wss-msg-close").Click();

            // Same pattern as the notification test above: assert on the service itself, which is
            // the state the close button actually mutates.
            Assert.Empty(WasmMessageService.Items);
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public void WasmMessageContainer_close_button_removes_the_message()
    {
        WasmMessageService.Clear();
        try
        {
            SetRendererInfo(WebView);
            WasmMessageService.Success("Saved!", duration: 0);
            var cut = Render<WasmMessageContainer>();

            cut.WaitForAssertion(() => Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent));

            cut.Find(".wss-msg-close").Click();

            Assert.Empty(WasmMessageService.Items);
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    // ---- S7: static facade Pause/Resume forward to the underlying service ----

    [Fact]
    public async Task WasmMessageService_Pause_and_Resume_forward_to_the_underlying_service()
    {
        WasmMessageService.Clear();
        try
        {
            var id = WasmMessageService.Success("hover me", duration: 0.05); // 50ms

            WasmMessageService.Pause(id);
            await Task.Delay(250); // well past the original duration
            Assert.Single(WasmMessageService.Items);

            WasmMessageService.Resume(id);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (WasmMessageService.Items.Count > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.Empty(WasmMessageService.Items);
        }
        finally
        {
            WasmMessageService.Clear();
        }
    }

    [Fact]
    public async Task WasmNotificationService_Pause_and_Resume_forward_to_the_underlying_service()
    {
        WasmNotificationService.Clear();
        try
        {
            var id = WasmNotificationService.Info("hover me", duration: 0.05); // 50ms

            WasmNotificationService.Pause(id);
            await Task.Delay(250);
            Assert.Single(WasmNotificationService.Items);

            WasmNotificationService.Resume(id);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (WasmNotificationService.Items.Count > 0 && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.Empty(WasmNotificationService.Items);
        }
        finally
        {
            WasmNotificationService.Clear();
        }
    }
}
