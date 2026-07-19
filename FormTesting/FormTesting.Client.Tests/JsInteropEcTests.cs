// One test below constructs a FormDefaults directly (not via cascaded markup) just to pass a non-null
// instance through; setting the [Parameter] AssetBase that way is exactly what BL0005 warns about,
// but it's intentional and safe here (mirrors JsModuleUrlTests' rationale).
#pragma warning disable BL0005

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

    [Fact]
    public async Task FocusFirstInvalidField_never_throws_when_js_is_unavailable()
    {
        // Same best-effort contract as FocusById above: strict-mode JSInterop's "no configured setup"
        // failure isn't a JSException, so this exercises the outer catch-all in InvokeBestEffortAsync
        // rather than the missing-global lazy-import branch (that branch needs a real browser — see
        // the e2e coverage in FormTesting.Client.E2ETests) and only passes if it's swallowed.
        await JsInteropEc.FocusFirstInvalidField(JSInterop.JSRuntime);
    }

    [Fact]
    public async Task FocusById_never_throws_when_js_is_unavailable_even_with_formDefaults()
    {
        // Passing a non-null formDefaults must not change the no-throw contract even when JS is
        // unavailable entirely (the lazy-import branch that would consult it never gets reached here).
        await JsInteropEc.FocusById(JSInterop.JSRuntime, "some-id", new FormDefaults { AssetBase = "https://mfe.example.com" });
    }
}
