namespace Controls;

/// <summary>
/// JsInterop for Edit Controls. Contains common JavaScript interop methods used by edit controls.
/// </summary>
public static class JsInteropEc
{
    /// <summary>
    /// Used when submission fails due to validation errors.
    /// Typically used with EditForm.OnSubmitFailed / OnInvalidSubmit.
    /// Yields once so any pending validation state changes flush before we look up the invalid field.
    /// </summary>
    public static async Task FocusFirstInvalidField(IJSRuntime jsRuntime)
    {
        await Task.Yield(); // let validation state changes finish rendering before we query the DOM
        await jsRuntime.InvokeVoidAsync("WssEditControls.focusFirstInvalidField");
    }

    public static async Task Log(IJSRuntime jsRuntime, string text)
    {
        await jsRuntime.InvokeVoidAsync("WssEditControls.log", text);
    }
}
