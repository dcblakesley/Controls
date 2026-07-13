namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the static JS-interop helpers — both invoke globals from edit-controls.js, which
/// consumers must load via a script tag (see the README Quick Start).
/// </summary>
public class JsInteropEcTests : TestContext
{
    [Fact]
    public async Task FocusFirstInvalidField_invokes_the_global_function()
    {
        var planned = JSInterop.SetupVoid("WssEditControls.focusFirstInvalidField");
        planned.SetVoidResult();

        await JsInteropEc.FocusFirstInvalidField(JSInterop.JSRuntime);

        Assert.Single(planned.Invocations);
    }

    [Fact]
    public async Task Log_passes_the_text_through()
    {
        var planned = JSInterop.SetupVoid("WssEditControls.log", "hello");
        planned.SetVoidResult();

        await JsInteropEc.Log(JSInterop.JSRuntime, "hello");

        Assert.Single(planned.Invocations);
    }

    [Fact]
    public async Task FocusById_is_a_no_op_when_js_is_unavailable()
    {
        // The XML contract promises best-effort ("a no-op when ... JS is unavailable"). Strict-mode
        // JSInterop throws on any unconfigured call — the same way a prerender IJSRuntime does —
        // so this passes only if FocusById swallows the failure instead of surfacing it.
        await JsInteropEc.FocusById(JSInterop.JSRuntime, "some-id");
    }
}
