namespace Controls;

/// <summary>
/// JsInterop for Edit Controls. Contains common JavaScript interop methods used by edit controls.
/// </summary>
/// <remarks>
/// Every method here is best-effort and never throws to the caller. Two distinct failure shapes are
/// tolerated the same way: JS interop being unavailable at all (server prerender, bUnit tests, a
/// torn-down circuit) is simply swallowed; a <see cref="JSException"/> that looks like
/// <c>window.WssEditControls</c> itself being undefined -- the cross-origin micro-frontend case,
/// where the host page never linked <c>edit-controls.js</c> via a classic <c>&lt;script&gt;</c> tag --
/// triggers one lazy side-effect <c>import()</c> of the module (resolved through
/// <see cref="JsModuleUrl.Resolve"/>, honoring the caller's cascaded
/// <see cref="Controls.FormDefaults.EffectiveAssetBase"/>) and one retry of the original call. If the
/// retry also fails (e.g. the import 404s), the failure is swallowed there too.
/// </remarks>
public static class JsInteropEc
{
    /// <summary>
    /// Used when submission fails due to validation errors.
    /// Typically used with EditForm.OnSubmitFailed / OnInvalidSubmit.
    /// Yields once so any pending validation state changes flush before we look up the invalid field.
    /// Best-effort -- never throws to the caller (see the class remarks).
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// used only if a lazy re-import of <c>edit-controls.js</c> is needed (see the class remarks).
    /// Null keeps the existing relative <c>./_content/...</c> import path.</param>
    public static async Task FocusFirstInvalidField(IJSRuntime jsRuntime, FormDefaults? formDefaults = null)
    {
        await Task.Yield(); // let validation state changes finish rendering before we query the DOM
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.focusFirstInvalidField");
    }

    /// <summary>
    /// Focuses the element with the given id, if present. Best-effort — a no-op when the id isn't
    /// found or JS is unavailable (prerender / tests); see the class remarks for the missing-global
    /// (cross-origin MFE) fallback.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="id">The element id to focus.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task FocusById(IJSRuntime jsRuntime, string id, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.focusById", id);

    /// <summary>
    /// Logs a message to the browser console. Best-effort; see the class remarks.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="text">The text to log.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task Log(IJSRuntime jsRuntime, string text, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.log", text);

    // Shared best-effort call: try the global first (the common case once edit-controls.js has loaded
    // via a classic <script> tag). A JSException there usually means window.WssEditControls itself is
    // undefined -- the cross-origin MFE case, where the host page never linked the script -- so import
    // the module once as a side-effect ES module (it assigns onto window itself; see edit-controls.js)
    // and retry. Any other failure (JS interop not available at all: prerender, tests; or the retry
    // also failing, e.g. the import 404s) is swallowed -- every call here is a nicety, never fatal to
    // the caller. Circuit teardown is caught first: JSDisconnectedException derives from JSException,
    // and a disconnected circuit must return immediately rather than attempt the doomed import+retry.
    static async Task InvokeBestEffortAsync(
        IJSRuntime jsRuntime, FormDefaults? formDefaults, string identifier, params object?[] args)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(identifier, args);
            return;
        }
        catch (JSDisconnectedException)
        {
            return; // Circuit torn down -- the import+retry below would be pointless interop calls.
        }
        catch (JSException)
        {
            // Fall through to the one-time lazy import + retry below.
        }
        catch
        {
            return; // JS interop not available at all (prerender / tests)
        }

        try
        {
            await jsRuntime.InvokeVoidAsync("import", JsModuleUrl.Resolve(formDefaults, "edit-controls.js"));
            await jsRuntime.InvokeVoidAsync(identifier, args);
        }
        catch { /* still unavailable (import 404s, JS gone) -- never fatal */ }
    }
}
