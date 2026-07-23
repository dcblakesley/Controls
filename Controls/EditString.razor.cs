// ReSharper disable SimplifyConditionalTernaryExpression

namespace Controls;

/// <summary> Edit control for string values, displays as a text input. Supports masking and URL display in read-only mode.</summary>
public partial class EditString : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime (Blazor validates
    /// unmatched component parameters at <c>SetParametersAsync</c> time, not compile time). Remove
    /// the attribute from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<string?>>? Field { get; set; }

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

    /// <summary> Optional leading affix content (e.g. a currency symbol or icon), rendered by <see cref="EditInputShell"/>. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional custom trailing affix content, rendered by <see cref="EditInputShell"/> after the clear button and character count but before the password toggle (locked order). Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary> Shows a clear button (via <see cref="EditInputShell"/>) while the value is non-empty and the control is enabled. Clicking it sets the value to null and refocuses the input.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary> Maximum number of characters, rendered as the input's <c>maxlength</c> attribute. Omitted (no browser-side cap) when null.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary> Shows a character-count indicator (via <see cref="EditInputShell"/>): <c>"{length}"</c> alone, or <c>"{length} / {MaxLength}"</c> once <see cref="MaxLength"/> is set (AntD's format).</summary>
    [Parameter] public bool ShowCount { get; set; }

    /// <summary> Renders the input as <c>type="password"</c> with a show/hide toggle (via <see cref="EditInputShell"/>). Independent of the read-only <see cref="MaskText"/> feature.</summary>
    [Parameter] public bool IsPassword { get; set; }

    bool _showMaskedValue;
    bool _passwordRevealed;
    // Captures the <input> so Clear() can refocus it directly -- unlike EditFile's RemoveFile, the
    // input never unmounts here, so a plain ElementReference.FocusAsync (Select/PickerBase's pattern)
    // is enough; no JsInteropEc by-id fallback needed.
    ElementReference _inputRef;

    /// <summary>
    /// Whether the shell's clear button should render right now: <see cref="AllowClear"/> is set,
    /// the control isn't disabled, and the current value is non-empty.
    /// </summary>
    bool IsClearable => AllowClear && !IsDisabled && !string.IsNullOrEmpty(CurrentValue);

    /// <summary>
    /// The shell's character-count text when <see cref="ShowCount"/> is set, else null (no count
    /// span renders). AntD format: <c>"{length}"</c> alone, or <c>"{length} / {MaxLength}"</c> once
    /// <see cref="MaxLength"/> is set. Length counts <see cref="InputBase{TValue}.CurrentValue"/>,
    /// treating null as zero.
    /// </summary>
    string? CountText => !ShowCount
        ? null
        : MaxLength is null
            ? $"{CurrentValue?.Length ?? 0}"
            : $"{CurrentValue?.Length ?? 0} / {MaxLength}";

    /// <summary>
    /// True once any affix parameter is in use -- the single computation site
    /// <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the shell always
    /// agree on which layout renders.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(Prefix, Suffix, AllowClear, CountText, IsPassword);

    /// <summary>
    /// The input's <c>class</c> attribute. Legacy mode reproduces today's exact string (so a
    /// no-new-params render stays byte-identical); affix mode adds <c>edit-affix-input</c> per
    /// <see cref="EditInputShell"/>'s contract.
    /// </summary>
    string InputClass => UseAffixLayout
        ? $"edit-input edit-string-input edit-affix-input {CssClass}"
        : $"edit-input edit-string-input {CssClass}";

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

    // Trivial parser — same as Microsoft's InputText: pass the string through. `out string`
    // (not `string?`) because InputBase<T>'s abstract signature declares it non-nullable.
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null!;
        return true;
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);

    /// <summary>
    /// The shell's clear button action: sets the value to null (via <see cref="InputBase{TValue}.CurrentValue"/>,
    /// which raises <c>ValueChanged</c>/<c>NotifyFieldChanged</c> itself) and refocuses the input.
    /// Focus is best-effort -- see <see cref="_inputRef"/>'s remarks.
    /// </summary>
    async Task Clear()
    {
        CurrentValue = null;
        try { await _inputRef.FocusAsync(); }
        catch { /* not focusable yet (prerender/tests) */ }
    }

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
