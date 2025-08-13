namespace Controls;

/// <summary>
/// JsInterop for Edit Controls. Contains common JavaScript interop methods used by edit controls.
/// </summary>
public static class JsInteropEc
{
    /// <summary>
    /// Used when submission fails due to validation errors.
    /// Typically used with EditForm.OnSubmitFailed
    /// </summary>
    /// <param name="jsRuntime"></param>
    /// <returns></returns>
    public static async Task FocusFirstInvalidField(IJSRuntime jsRuntime)
    {
        await Task.Delay(100); // Allow time for validation to complete
        await jsRuntime!.InvokeVoidAsync("focusFirstInvalidField");
    }
}