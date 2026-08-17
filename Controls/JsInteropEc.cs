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
    /// Moves focus to the first form field inside one <see cref="Controls.FormDefaults"/> scope —
    /// the engine behind <see cref="Controls.FormDefaults.FocusFirstField"/>, which is the only
    /// caller. <paramref name="scopeId"/> names the pair of empty <c>&lt;template&gt;</c> markers
    /// that component renders around its <c>ChildContent</c> while the feature is on
    /// (<c>{scopeId}</c> and <c>{scopeId}-end</c>), and the JS side resolves "first" from the DOM
    /// between them, in document order.
    /// </summary>
    /// <remarks>
    /// Deliberately resolved in JS rather than from any C# registry of fields: Blazor notifies
    /// non-fixed cascading-value subscribers in construction order, not document order, so a C#-side
    /// answer would be wrong whenever a form's markup order and its component-construction order
    /// disagree. The DOM is the only source of truth for "first", and it is also the only place the
    /// skip rules (disabled, readonly, <c>tabindex="-1"</c>, not rendered/visible, inert ancestor)
    /// can all be answered. Best-effort — a missing marker, an empty scope, a field that already has
    /// focus, or no JS at all (prerender / tests) each end as a silent no-op; see the class remarks
    /// for the missing-global (cross-origin MFE) fallback.
    /// </remarks>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="scopeId">The id of the scope's start marker; the end marker is <c>{scopeId}-end</c>.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task FocusFirstField(IJSRuntime jsRuntime, string scopeId, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.focusFirstField", scopeId);

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
    /// Moves focus to one input inside the group element with id <paramref name="containerId"/> — the
    /// checked one when <paramref name="preferChecked"/> is set and there is one, else the first
    /// enabled one, else nothing. Backs the radio groups' <c>FocusAsync()</c>, whose per-option
    /// <c>&lt;input&gt;</c>s are rendered by <see cref="InputRadio{TValue}"/> (or, for
    /// <c>EditRadio</c>, by consumer markup), so no <see cref="ElementReference"/> can be captured for
    /// them and no id can be computed. Best-effort — a no-op when the container isn't found, has no
    /// enabled inputs, or JS is unavailable (prerender / tests); see the class remarks for the
    /// missing-global (cross-origin MFE) fallback.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="containerId">The group element's id — the radio fieldset's, which is the control's own resolved id.</param>
    /// <param name="selector">A CSS selector for the candidate inputs, e.g. <c>input[type=radio]</c>.</param>
    /// <param name="preferChecked">
    /// True to prefer the checked input over the first enabled one. True for radio groups, whose Tab
    /// stop is the checked radio; false for checkbox lists, where every box is its own Tab stop.
    /// </param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task FocusGroupInput(
        IJSRuntime jsRuntime, string containerId, string selector, bool preferChecked, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.focusGroupInput", containerId, selector, preferChecked);

    /// <summary>
    /// Resizes the textarea with the given id to fit its content, clamped between
    /// <paramref name="minRows"/> and <paramref name="maxRows"/> (null = unbounded). Stateless —
    /// <see cref="EditTextArea"/> calls this again after every input and once after first render
    /// while its <c>AutoSize</c> parameter is true. Best-effort — a no-op when the id isn't found or
    /// JS is unavailable (prerender / tests); see the class remarks for the missing-global
    /// (cross-origin MFE) fallback.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="id">The textarea's element id.</param>
    /// <param name="minRows">The minimum height, expressed in text rows.</param>
    /// <param name="maxRows">The maximum height, expressed in text rows, or null for unbounded.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task AutoSizeTextArea(IJSRuntime jsRuntime, string id, int minRows, int? maxRows, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.autoSizeTextArea", id, minRows, maxRows);

    /// <summary>
    /// Logs a message to the browser console. Best-effort; see the class remarks.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="text">The text to log.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task Log(IJSRuntime jsRuntime, string text, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.log", text);

    /// <summary>
    /// Saves in-memory bytes to the user's machine as a download (a <c>Blob</c> + a temporary,
    /// auto-clicked anchor with a <c>download</c> attribute -- the standard cross-browser idiom, and
    /// one that isn't subject to the popup-blocker restrictions a <c>window.open</c> preview would hit
    /// after an async round-trip). Used by <see cref="EditFile"/>'s <c>AllowDownload</c> to let a user
    /// reopen a file they've already picked. Best-effort -- see the class remarks; a missing/torn-down
    /// circuit or no-JS render (prerender, tests) just means the click does nothing.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime to invoke through.</param>
    /// <param name="bytes">The file's bytes, already buffered in memory.</param>
    /// <param name="fileName">The name offered in the browser's save dialog.</param>
    /// <param name="contentType">The <c>Blob</c>'s MIME type.</param>
    /// <param name="formDefaults">The cascaded <see cref="Controls.FormDefaults"/> in scope, if any --
    /// see <see cref="FocusFirstInvalidField"/>.</param>
    public static async Task DownloadFile(
        IJSRuntime jsRuntime, byte[] bytes, string fileName, string contentType, FormDefaults? formDefaults = null) =>
        await InvokeBestEffortAsync(jsRuntime, formDefaults, "WssEditControls.downloadFile", bytes, fileName, contentType);

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
