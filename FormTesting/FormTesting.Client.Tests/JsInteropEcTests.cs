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
}
