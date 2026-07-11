// ReSharper disable SimplifyConditionalTernaryExpression

namespace Controls;

/// <summary> Edit control for string values, displays as a text input. Supports masking and URL display in read-only mode.</summary>
public partial class EditString : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary> Placeholder text to display in the input when empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary> Non-Edit Mode only, MaskText is a string that will be displayed before the current value </summary>
    /// <example> MaskText='****-****-' with the value 'abcd-efgh-ijkl' would display '****-****-ijkl'</example>
    [Parameter] public string? MaskText { get; set; }

    /// <summary> Non-Edit mode will be a link </summary>
    [Parameter] public string? Url { get; set; }

    /// <summary> Only used with Urls, Sets target="UrlTarget" in the link </summary>
    [Parameter] public string? UrlTarget { get; set; }

    /// <summary> Sets the autocomplete attribute on the input element. Defaults to "one-time-code" to prevent browser autofill/extensions from intercepting input events.</summary>
    [Parameter] public string Autocomplete { get; set; } = "one-time-code";

    bool _showMaskedValue;

    /// <summary>
    /// The href to render in read-only link mode: the <see cref="Url"/> when it is relative or uses an
    /// allow-listed scheme (http/https/mailto); otherwise null, so a <c>javascript:</c> / <c>data:</c>
    /// URL (e.g. bound from model data) can't render a script-executing link. When null the control
    /// falls back to plain read-only text.
    /// </summary>
    string? SafeUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return null;
            // Absolute URLs must use an allow-listed scheme; relative URLs (no scheme) are fine.
            if (Uri.TryCreate(Url, UriKind.Absolute, out var uri))
                return uri.Scheme is "http" or "https" or "mailto" ? Url : null;
            return Url;
        }
    }

    /// <summary> rel for the read-only link; hardens <c>target="_blank"</c> against reverse tabnabbing. </summary>
    string? UrlRel => string.Equals(UrlTarget, "_blank", StringComparison.OrdinalIgnoreCase) ? "noopener noreferrer" : null;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditString)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Trivial parser — same as Microsoft's InputText: pass the string through.
    // `out string` (not `string?`) for net8 base-signature compat.
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null!;
        return true;
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);

    string? GetMaskValue()
    {
        if (string.IsNullOrEmpty(MaskText) || CurrentValue == null)
            return CurrentValue;

        if (MaskText.Length == 1)
        {
            // If MaskText is a single character, return it as a mask for the entire value
            return new string(MaskText[0], CurrentValue.Length);
        }

        return MaskText.Length > CurrentValue.Length
            ? MaskText
            : MaskText + CurrentValue[MaskText.Length..];
    }
}
