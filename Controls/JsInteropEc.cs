﻿namespace Controls;

/// <summary>
/// JsInterop for Edit Controls. Contains common JavaScript interop methods used by edit controls.
/// </summary>
public static class JsInteropEc
{
    /// <summary>
    /// Used when submission fails due to validation errors.
    /// Typically used with EditForm.OnSubmitFailed OnInvalidSubmit
    /// </summary>
    /// <param name="jsRuntime"></param>
    /// <returns></returns>
    public static async Task FocusFirstInvalidField(IJSRuntime jsRuntime)
    {
        await Task.Delay(1); // Allow time for validation to complete
        await jsRuntime!.InvokeVoidAsync("focusFirstInvalidField");
    }

    public static async Task Log(IJSRuntime jsRuntime, string text)
    {
        await Task.Delay(1); // Allow time for validation to complete
        await jsRuntime!.InvokeVoidAsync("log", text);
        await jsRuntime!.InvokeVoidAsync("logError", text);
        await jsRuntime!.InvokeVoidAsync("logWarn", text);
        await jsRuntime!.InvokeVoidAsync("logInfo", text);
    }
}