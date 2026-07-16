namespace Controls.Helpers;

// Builds the specifier for the RCL's lazy `wss-*.js` module imports. Blazor's JS interop
// special-cases the "import" identifier and resolves a "./"-relative specifier against
// document.baseURI — the host page's origin, not necessarily the one serving WssBlazorControls's
// static assets (e.g. a micro-frontend embedded into a host that doesn't serve/proxy them).
// FormDefaults.AssetBase lets a render tree override that origin; unset (the default) preserves the
// existing relative behavior.
public static class JsModuleUrl
{
    public static string Resolve(FormDefaults? formDefaults, string fileName)
    {
        var baseUrl = formDefaults?.EffectiveAssetBase;
        return string.IsNullOrEmpty(baseUrl)
            ? $"./_content/WssBlazorControls/{fileName}"
            : $"{baseUrl.TrimEnd('/')}/_content/WssBlazorControls/{fileName}";
    }
}
