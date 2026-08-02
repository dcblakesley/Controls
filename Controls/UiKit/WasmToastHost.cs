namespace Controls;

/// <summary>
/// Host check shared by <c>WasmMessageContainer</c> and <c>WasmNotificationContainer</c>.
/// </summary>
internal static class WasmToastHost
{
    // Microsoft.AspNetCore.Components.RendererInfo.Name, as each host renderer reports it (verified
    // against the installed 10.0 framework -- RemoteRenderer, StaticHtmlRenderer and
    // WebAssemblyRenderer each construct their RendererInfo from a literal):
    //   "Static"      - SSR / the prerender pass of an interactive component.
    //   "Server"      - a Blazor Server circuit, INCLUDING InteractiveAuto's server phase.
    //   "WebAssembly" - the browser runtime.
    //   "WebView"     - Blazor Hybrid (MAUI / WPF / WinForms BlazorWebView).
    const string BlazorServerRenderer = "Server";

    /// <summary>
    /// Whether the component is running on the one renderer where a process-wide static is shared by
    /// more than one user: Blazor Server, where a single process serves every circuit.
    /// </summary>
    /// <remarks>
    /// This deliberately asks "is this a multi-circuit server host", not "is this not the browser".
    /// The previous <c>!OperatingSystem.IsBrowser()</c> test also rejected Blazor Hybrid, where
    /// <c>IsBrowser()</c> is false but the process serves exactly ONE user — which is precisely the
    /// condition that makes process-static toast state safe. WebView and WebAssembly are therefore
    /// both permitted, as is a WASM app's own "Static" prerender pass, while
    /// <c>InteractiveAuto</c>'s server phase still reports "Server" and is still caught.
    /// </remarks>
    internal static bool IsSharedServerHost(RendererInfo renderer) => renderer.Name == BlazorServerRenderer;
}
