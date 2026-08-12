using Controls.Demo;
using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="UiKitGallery"/> ships in the published <c>WssBlazorControls.Demo</c> package, so a
/// consumer can drop it onto a page of any render mode. These tests pin that it renders on every one
/// of them — including Blazor Server, where the static toast containers it embeds refuse to run.
/// </summary>
/// <remarks><c>[Collection]</c>: serializes against every other class touching the process-static
/// Wasm toast services — see the collection's definition for why.</remarks>
[Collection(WasmStaticToastCollection.Name)]
public class UiKitGalleryHostTests : BunitContext
{
    public UiKitGalleryHostTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void The_gallery_still_renders_on_Blazor_Server_minus_the_static_toast_demo()
    {
        // The embedded <WasmMessageContainer />/<WasmNotificationContainer /> throw by design on the
        // Server renderer (their state is process-static and shared by every circuit). Unguarded, that
        // exception took the WHOLE gallery down -- an unrecoverable failure in a published package,
        // where it previously just degraded.
        WasmMessageService.Clear();
        WasmNotificationService.Clear();
        try
        {
            SetRendererInfo(new RendererInfo("Server", isInteractive: true));

            var cut = Render<UiKitGallery>();

            // The rest of the gallery is intact...
            Assert.NotEmpty(cut.FindAll(".demo-section"));
            Assert.NotEmpty(cut.FindAll(".wss-table"));
            // ...the toast demo is replaced by an explanation rather than simply vanishing...
            Assert.Single(cut.FindAll("[data-test-id=static-toasts-unavailable]"));
            Assert.Contains("MessageContainer", cut.Find("[data-test-id=static-toasts-unavailable]").TextContent);
            // ...and nothing that would have thrown is on the page.
            Assert.Empty(cut.FindAll("[data-test-id=bottom-left-notification-btn]"));
        }
        finally
        {
            WasmMessageService.Clear();
            WasmNotificationService.Clear();
        }
    }

    [Theory]
    [InlineData("WebAssembly", true)]
    [InlineData("WebView", true)]
    public void The_gallery_renders_the_real_toast_containers_on_a_single_user_host(string rendererName, bool isInteractive)
    {
        WasmMessageService.Clear();
        WasmNotificationService.Clear();
        try
        {
            SetRendererInfo(new RendererInfo(rendererName, isInteractive));

            var cut = Render<UiKitGallery>();

            Assert.Empty(cut.FindAll("[data-test-id=static-toasts-unavailable]"));
            Assert.Single(cut.FindAll("[data-test-id=bottom-left-notification-btn]"));

            // The containers are live, not just present: pushing a message through the static service
            // reaches the DOM. This is the path the repo's own WASM-hosted e2e exercises.
            WasmMessageService.Success("Saved!", duration: 0);
            cut.WaitForAssertion(() => Assert.Contains("Saved!", cut.Find(".wss-msg-content").TextContent));
        }
        finally
        {
            WasmMessageService.Clear();
            WasmNotificationService.Clear();
        }
    }
}
