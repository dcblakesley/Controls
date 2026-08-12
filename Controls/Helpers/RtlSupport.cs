using System.Globalization;

namespace Controls.Helpers;

/// <summary>
/// Shared answer to "should horizontal arrow keys run right-to-left?" for the kit's keyboard
/// handlers (Tabs, the picker grids). The stylesheet already mirrors layout under
/// <c>dir="rtl"</c> via logical properties, but ArrowLeft/ArrowRight are <em>physical</em> keys:
/// per the APG, in a mirrored layout the physical Right arrow must still move focus visually
/// rightward, which means "previous item" instead of "next". Callers swap their horizontal arrow
/// arms when this is true.
/// </summary>
/// <remarks>
/// Culture-derived rather than a per-component parameter: the ambient
/// <see cref="CultureInfo.CurrentUICulture"/> flows per-circuit on Blazor Server (with request
/// localization) and process-wide on WASM, so it tracks the same signal apps use to set
/// <c>dir="rtl"</c> in the first place — and it needs no JS interop to read the DOM. A page whose
/// <c>dir</c> disagrees with the UI culture (rare; usually a bug in the host) will get
/// culture-side arrow behavior. Vertical arrows, Home/End, and PageUp/PageDown are logical, not
/// physical-horizontal, and never swap.
/// </remarks>
internal static class RtlSupport
{
    /// <summary>
    /// True when the ambient UI culture reads right-to-left (Arabic, Hebrew, Persian, …) and
    /// horizontal arrow-key handlers should therefore invert their ArrowLeft/ArrowRight arms.
    /// </summary>
    public static bool IsRightToLeft => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
}
