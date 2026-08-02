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
    /// The href to render in read-only link mode: the <see cref="Url"/>, with all ASCII tab/CR/LF
    /// characters stripped, when it is relative or uses an allow-listed scheme (http/https/mailto);
    /// otherwise null, so a <c>javascript:</c> / <c>data:</c> URL (e.g. bound from model data) can't
    /// render a script-executing link. When null the control falls back to plain read-only text.
    /// </summary>
    /// <remarks>
    /// The WHATWG URL basic parser strips all ASCII tab/newline (<c>\t\r\n</c>) from a URL before
    /// parsing it, so a browser given <c>href="java&#9;script:alert(1)"</c> re-forms and runs
    /// <c>javascript:alert(1)</c> on click. <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/>
    /// does not strip those characters -- it just fails to parse the string as absolute, which used to
    /// fall through to the "anything unparseable is a safe relative URL" branch below and return the
    /// raw (unstripped) value verbatim. Stripping first, before the scheme check, makes the allow-list
    /// see exactly what the browser will see -- and returning the stripped value (not <see cref="Url"/>
    /// itself) means the rendered <c>href</c> never contains the bypass characters either.
    /// </remarks>
    string? SafeUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return null;
            var stripped = StripAsciiTabAndNewlines(Url);
            // Absolute URLs must use an allow-listed scheme; relative URLs (no scheme) are fine.
            if (Uri.TryCreate(stripped, UriKind.Absolute, out var uri))
                return uri.Scheme is "http" or "https" or "mailto" ? stripped : null;
            return stripped;
        }
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
