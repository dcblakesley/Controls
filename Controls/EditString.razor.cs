// ReSharper disable SimplifyConditionalTernaryExpression

namespace Controls;

/// <summary> Edit control for string values, displays as a text input. Supports masking and URL display in read-only mode.</summary>
public partial class EditString : EditTextInputBase
{
    // Component-specific parameters. The shared text-editor surface lives on the two bases:
    // Placeholder/MaxLength/AllowClear/ShowCount (+ their Effective*/IsClearable/CountText/Clear
    // members) on EditTextInputBase, which EditTextArea inherits too; Size and UpdateOn (+
    // UpdateEventName) on EditTextControlBase<TValue>, which EditNumber/EditDateNative share as well.

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime (Blazor validates
    /// unmatched component parameters at <c>SetParametersAsync</c> time, not compile time). Remove
    /// the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<string?>>? Field { get; set; }

    /// <summary> Non-Edit Mode only, MaskText is a string that will be displayed before the current value </summary>
    /// <example> MaskText='****-****-' with the value 'abcd-efgh-ijkl' would display '****-****-ijkl'</example>
    [Parameter] public string? MaskText { get; set; }

    /// <summary> Non-Edit mode will be a link </summary>
    [Parameter] public string? Url { get; set; }

    /// <summary> Only used with Urls, Sets target="UrlTarget" in the link </summary>
    [Parameter] public string? UrlTarget { get; set; }

    /// <summary>
    /// Sets the autocomplete attribute on the input element. Falls back to the bound property's
    /// <c>[Autocomplete]</c> when unset, then to "one-time-code" to prevent browser autofill/extensions
    /// from intercepting input events -- see <see cref="EffectiveAutocomplete"/>.
    /// </summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>
    /// The autocomplete token actually rendered: the <see cref="Autocomplete"/> parameter, else the
    /// model property's <c>[Autocomplete]</c>, else <c>"one-time-code"</c> (the control's built-in
    /// default).
    /// </summary>
    string EffectiveAutocomplete => Autocomplete ?? _attributes.Autocomplete() ?? "one-time-code";

    /// <summary> Optional leading affix content (e.g. a currency symbol or icon), rendered by <see cref="EditInputShell"/>. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional custom trailing affix content, rendered by <see cref="EditInputShell"/> after the clear button and character count but before the password toggle (locked order). Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary>
    /// Renders the input as <c>type="password"</c> with a show/hide toggle (via <see cref="EditInputShell"/>).
    /// Independent of the read-only <see cref="MaskText"/> feature. Falls back to the bound property's
    /// <c>[DataType(DataType.Password)]</c> when unset -- see <see cref="EffectiveIsPassword"/>.
    /// </summary>
    [Parameter] public bool? IsPassword { get; set; }

    /// <summary>
    /// Whether the input actually renders as a password field: the <see cref="IsPassword"/> parameter,
    /// else the model property's <c>[DataType(DataType.Password)]</c>. False when neither is set,
    /// matching the control's old default.
    /// </summary>
    bool EffectiveIsPassword => IsPassword ?? _attributes.IsPasswordField();

    bool _showMaskedValue;
    bool _passwordRevealed;

    /// <summary>
    /// True once any affix parameter is in use -- the single computation site
    /// <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the shell always
    /// agree on which layout renders.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(Prefix, Suffix, AllowClear, CountText, EffectiveIsPassword);

    /// <summary>
    /// The input's <c>class</c> attribute. Legacy mode with <see cref="EditTextControlBase{TValue}.Size"/>
    /// at its default reproduces today's exact string (so a no-new-params render stays byte-identical);
    /// affix mode adds <c>edit-affix-input</c> per <see cref="EditInputShell"/>'s contract, and a
    /// non-default Size appends its <see cref="EditInputShell.SizeClass"/> token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass(
        UseAffixLayout ? "edit-input edit-string-input edit-affix-input" : "edit-input edit-string-input",
        Size, CssClass);

    /// <summary>
    /// The href to render in read-only link mode: the <see cref="Url"/>, preprocessed the same way a
    /// browser preprocesses an href before parsing it, when the result is relative or uses an
    /// allow-listed scheme (http/https/mailto); otherwise null, so a <c>javascript:</c> / <c>data:</c>
    /// URL (e.g. bound from model data) can't render a script-executing link. When null the control
    /// falls back to plain read-only text.
    /// </summary>
    /// <remarks>
    /// The WHATWG URL basic parser applies two preprocessing steps, in order, before it ever looks at
    /// the scheme: (1) trim any leading/trailing C0 control (<c>U+0000</c>-<c>U+001F</c>) or space
    /// (<c>U+0020</c>) from the input; (2) remove every ASCII tab/CR/LF (<c>\t\r\n</c>) from what's
    /// left, wherever it occurs. Both steps can hide a <c>javascript:</c> scheme from
    /// <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/>, which does neither -- a leading C0
    /// control isn't a valid scheme character and an embedded tab/newline splits the scheme token, so
    /// the string fails to parse as absolute either way. That used to fall through to the "anything
    /// unparseable is a safe relative URL" branch below and return the raw value verbatim, control
    /// bytes and all -- e.g. <c>"\u0001javascript:alert(1)"</c> parses as relative (a C0 control isn't a
    /// valid scheme character) yet a browser trims the leading control byte itself when resolving the
    /// href, exposing <c>javascript:</c> as the scheme, and the script runs on click. Preprocessing
    /// first, in the browser's order, before the scheme check, makes the allow-list see exactly what
    /// the browser will see -- and returning the fully-preprocessed value (never <see cref="Url"/>
    /// itself) means the rendered <c>href</c> can never contain the bypass characters either.
    /// </remarks>
    string? SafeUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return null;
            var trimmed = TrimLeadingAndTrailingC0OrSpace(Url);
            var stripped = StripAsciiTabAndNewlines(trimmed);
            // Absolute URLs must use an allow-listed scheme; relative URLs (no scheme) are fine.
            if (Uri.TryCreate(stripped, UriKind.Absolute, out var uri))
                return uri.Scheme is "http" or "https" or "mailto" ? stripped : null;
            return stripped;
        }
    }

    /// <summary>
    /// Step 1 of the WHATWG href preprocessing: trims any leading/trailing codepoint <c>&lt;= U+0020</c>
    /// (every C0 control, <c>U+0000</c>-<c>U+001F</c>, plus <c>U+0020</c> SPACE). <c>U+007F</c> (DEL) is
    /// deliberately NOT included -- it is not part of the WHATWG "C0 control or space" definition, so
    /// browsers don't trim it either; a leading DEL still isn't a valid scheme-start character, so it
    /// fails to parse as absolute in both <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/> and the
    /// browser's own URL parser, landing both in the same safe "unparseable/relative" bucket -- there is
    /// no bypass to close, and trimming it would just diverge from what the browser actually does.
    /// </summary>
    static string TrimLeadingAndTrailingC0OrSpace(string url)
    {
        var start = 0;
        var end = url.Length;
        while (start < end && url[start] <= ' ') start++;
        while (end > start && url[end - 1] <= ' ') end--;
        return start == 0 && end == url.Length ? url : url[start..end];
    }

    static string StripAsciiTabAndNewlines(string url) =>
        url.IndexOfAny(['\t', '\r', '\n']) < 0 ? url : url.Replace("\t", "").Replace("\r", "").Replace("\n", "");

    /// <summary> rel for the read-only link; hardens <c>target="_blank"</c> against reverse tabnabbing. </summary>
    string? UrlRel => string.Equals(UrlTarget, "_blank", StringComparison.OrdinalIgnoreCase) ? "noopener noreferrer" : null;

    /// <summary> Toggles the password reveal state driving the shell's show/hide button.</summary>
    void TogglePasswordVisibility() => _passwordRevealed = !_passwordRevealed;

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
