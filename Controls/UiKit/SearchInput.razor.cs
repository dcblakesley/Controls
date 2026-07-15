using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// A search field (the Clark Connect / AntD <c>Input.Search</c> pattern): an optional leading
/// addon label chip, the text input, and a trailing icon-only search button — pill-rounded ends by
/// default (override <c>--wss-search-radius</c> to square them). <see cref="OnSearch"/> fires on
/// Enter and on the button; <see cref="Value"/> supports <c>@bind-Value</c> (updates per keystroke).
/// </summary>
/// <remarks>Not a form control (no <c>InputBase</c>/validation wiring) — it's a filter/toolbar
/// widget. For a validated form text field use <c>EditString</c>.</remarks>
public partial class SearchInput
{
    /// <summary>The search text. Supports <c>@bind-Value</c>; updates on every keystroke.</summary>
    [Parameter] public string? Value { get; set; }

    /// <summary>Raised with the new text on every keystroke (supports <c>@bind-Value</c>).</summary>
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Raised with the current text when the user presses Enter or clicks the search button.</summary>
    [Parameter] public EventCallback<string?> OnSearch { get; set; }

    /// <summary>Text of the leading addon chip (e.g. "POs"). Null/empty (default) renders no addon.</summary>
    [Parameter] public string? AddonLabel { get; set; }

    /// <summary>Optional addon template rendered instead of <see cref="AddonLabel"/>.</summary>
    [Parameter] public RenderFragment? AddonContent { get; set; }

    /// <summary>Input placeholder.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Disables the input and the search button.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Control width as a CSS length (e.g. "240px", "100%"). Null (default) keeps the stylesheet width.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>HTML id applied to the input — wires a consumer label / test hook.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Accessible name of the input. Defaults to <see cref="AddonLabel"/> when unset.</summary>
    [Parameter] public string? InputLabel { get; set; }

    /// <summary>Accessible name of the icon-only search button. Override to localize.</summary>
    [Parameter] public string SearchButtonLabel { get; set; } = "Search";

    /// <summary>
    /// Unmatched attributes (e.g. a consumer's <c>class</c>, <c>style</c>, or <c>data-*</c>),
    /// applied to the root <c>div.wss-search</c>. <c>class</c> and <c>style</c> merge with the
    /// component's own; the rest are splatted verbatim.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    // AntD SearchOutlined glyph (no icon-font dependency, matching the kit's other inline icons).
    static readonly MarkupString SearchIcon = new(
        "<svg viewBox=\"64 64 896 896\" width=\"1em\" height=\"1em\" fill=\"currentColor\" aria-hidden=\"true\"><path d=\"M909.6 854.5L649.9 594.8C690.2 542.7 712 479 712 412c0-80.2-31.3-155.4-87.9-212.1-56.6-56.7-132-87.9-212.1-87.9s-155.5 31.3-212.1 87.9C143.2 256.5 112 331.8 112 412c0 80.1 31.3 155.5 87.9 212.1C256.5 680.8 331.8 712 412 712c67 0 130.6-21.8 182.7-62l259.7 259.6a8.2 8.2 0 0011.6 0l43.6-43.5a8.2 8.2 0 000-11.6zM570.4 570.4C528 612.7 471.8 636 412 636s-116-23.3-158.4-65.6C211.3 528 188 471.8 188 412s23.3-116.1 65.6-158.4C296 211.3 352.2 188 412 188s116.1 23.2 158.4 65.6S636 352.2 636 412s-23.3 116.1-65.6 158.4z\"/></svg>");

    string? WidthStyle => string.IsNullOrEmpty(Width) ? null : $"width:{Width};";

    async Task OnInputAsync(ChangeEventArgs e)
    {
        Value = e.Value?.ToString();
        await ValueChanged.InvokeAsync(Value);
    }

    async Task OnKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await RaiseSearchAsync();
    }

    async Task RaiseSearchAsync()
    {
        if (Disabled) return;
        if (OnSearch.HasDelegate) await OnSearch.InvokeAsync(Value);
    }
}
