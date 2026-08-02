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
    /// <c>[Autocomplete]</c> when unset, then to a built-in default that prevents browser
    /// autofill/extensions from intercepting input events -- see <see cref="EffectiveAutocomplete"/>.
    /// </summary>
    [Parameter] public string? Autocomplete { get; set; }

    /// <summary>
    /// The autocomplete token actually rendered: the <see cref="Autocomplete"/> parameter, else the
    /// model property's <c>[Autocomplete]</c>, else the control's built-in default --
    /// <c>"new-password"</c> for a password field (see <see cref="EffectiveIsPassword"/>),
    /// <c>"one-time-code"</c> otherwise.
    /// </summary>
    /// <remarks>
    /// Both defaults exist to keep autofill out of the way; they differ because "out of the way"
    /// differs by field. <c>"one-time-code"</c> is the general suppressor, but on a password field it
    /// is a lie the platform acts on: iOS and Android read it as "this is an SMS/OTP field" and offer
    /// the one-time-code keyboard affordance over the password the user is actually typing.
    /// <c>"new-password"</c> is the standard token for exactly this case -- it suppresses filling a
    /// stored credential without claiming the field is something it isn't. (The non-password default
    /// stays <c>"one-time-code"</c>: that is a locked decision, not an oversight.)
    /// </remarks>
    string EffectiveAutocomplete =>
        Autocomplete ?? _attributes.Autocomplete() ?? (EffectiveIsPassword ? "new-password" : "one-time-code");

    /// <summary> Optional leading affix content (e.g. a currency symbol or icon), rendered by <see cref="EditInputShell"/>. Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary> Optional custom trailing affix content, rendered by <see cref="EditInputShell"/> after the clear button and character count but before the password toggle (locked order). Setting this switches the control into the shell's AntD-style affix layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary>
    /// Marks the field as a secret. In edit mode it renders as <c>type="password"</c> with a show/hide
    /// toggle (via <see cref="EditInputShell"/>); in read-only mode it renders the same masked row
    /// <see cref="MaskText"/> produces, bulleted to the value's length -- a password field must not
    /// print its secret as plain text just because the form switched to read-only. An explicit
    /// <see cref="MaskText"/> still wins there (see <see cref="EffectiveMaskText"/>). Falls back to the
    /// bound property's <c>[DataType(DataType.Password)]</c> when unset -- see
    /// <see cref="EffectiveIsPassword"/>.
    /// </summary>
    [Parameter] public bool? IsPassword { get; set; }

    /// <summary>
    /// Whether the input actually renders as a password field: the <see cref="IsPassword"/> parameter,
    /// else the model property's <c>[DataType(DataType.Password)]</c>. False when neither is set,
    /// matching the control's old default.
    /// </summary>
    bool EffectiveIsPassword => IsPassword ?? _attributes.IsPasswordField();

    /// <summary>
    /// The bullet a password field's read-only mask is built from (U+2022, matching what a browser
    /// paints in a <c>type="password"</c> input) -- see <see cref="EffectiveMaskText"/>. Spelled as an
    /// escape so the character can't be mangled by a source-encoding round trip.
    /// </summary>
    const string PasswordMask = "\u2022";

    /// <summary>
    /// The mask the read-only view actually applies: <see cref="MaskText"/> when the consumer set one,
    /// else a single bullet for a password field (which, by the single-character rule in
    /// <see cref="GetMaskValue"/>, repeats to cover the whole value), else null -- no masked row.
    /// </summary>
    /// <remarks>
    /// Read-only mode used to key off <see cref="MaskText"/> alone, so a field declared secret through
    /// <see cref="IsPassword"/> or <c>[DataType(DataType.Password)]</c> printed its value in the clear
    /// the moment the form went read-only -- the control knew it was a secret and disclosed it anyway.
    /// A set <see cref="MaskText"/> still wins: it is the more specific instruction, and a consumer who
    /// asked for "last four visible" on a secret field meant it.
    /// </remarks>
    string? EffectiveMaskText => string.IsNullOrEmpty(MaskText) ? (EffectiveIsPassword ? PasswordMask : null) : MaskText;

    /// <summary>
    /// What the debug bound-value echo (<see cref="BoundValueDisplay"/>, shown only while
    /// <see cref="FormOptions.ShowBoundValues"/> is on) prints: the value, or a redacted stand-in for a
    /// password field. That flag is a development aid, but it is set form-wide -- so a form that turned
    /// it on to inspect its models was writing every password bound to it into the DOM in plain text,
    /// where it also reaches anything that scrapes the rendered page.
    /// </summary>
    string? BoundValueText => EffectiveIsPassword ? $"({CurrentValue?.Length ?? 0} chars, hidden)" : CurrentValueAsString;

    bool _showMaskedValue;
    bool _passwordRevealed;

    /// <summary>
    /// Drops both reveal states the moment the thing they reveal is no longer the thing the user
    /// asked to see. Neither is a parameter, so nothing else would ever clear them: an instance that
    /// leaves and re-enters edit mode, has <see cref="IsPassword"/> flipped, or (in read-only mode)
    /// is handed a different record's value keeps rendering revealed, re-exposing a secret the user
    /// never asked for a second time. Reuse without a <c>@key</c> is the sharp case — the component
    /// instance survives, so revealing record A's masked value would show record B's in the clear.
    /// </summary>
    /// <remarks>
    /// The two states reset on deliberately different triggers. <see cref="_passwordRevealed"/>
    /// resets when the editor stops being a revealable password box at all (read-only mode, or
    /// password-ness switched off) but NOT on a value change: in edit mode every keystroke changes
    /// the value, so that rule would un-reveal the box mid-typing. <see cref="_showMaskedValue"/> is
    /// the read-only counterpart and gets both triggers — it is meaningless in edit mode, and a
    /// read-only value only changes when the control is handed different data, which is exactly the
    /// record-swap case.
    /// </remarks>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (!ShowEditor || !EffectiveIsPassword) _passwordRevealed = false;
        if (ShowEditor || ValueChangedSinceLastParameters) _showMaskedValue = false;
    }

    /// <summary>
    /// True once any affix parameter is in use -- the single computation site
    /// <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the shell always
    /// agree on which layout renders.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(Prefix, Suffix, AllowClear, CountText, EffectiveIsPassword);

    /// <summary>
    /// The input's <c>class</c> attribute. Legacy mode carries <c>edit-input-legacy-padding</c> (the
    /// trailing-edge space InvalidIcon needs, formerly an inline style -- see
    /// <see cref="EditInputShell.UsesAffixLayout"/>'s remarks) with <see cref="EditTextControlBase{TValue}.Size"/>
    /// at its default otherwise reproducing today's exact string; affix mode adds <c>edit-affix-input</c>
    /// per <see cref="EditInputShell"/>'s contract instead, and a non-default Size appends its
    /// <see cref="EditInputShell.SizeClass"/> token.
    /// </summary>
    string InputClass => EditInputShell.BuildInputClass(
        UseAffixLayout ? "edit-input edit-string-input edit-affix-input" : "edit-input edit-string-input edit-input-legacy-padding",
        Size, CssClass);

    /// <summary>
    /// The href to render in read-only link mode: the <see cref="Url"/>, preprocessed the same way a
    /// browser preprocesses an href before parsing it, when the result is a same-origin-relative URL
    /// or uses an allow-listed scheme (http/https/mailto); otherwise null, so a <c>javascript:</c> /
    /// <c>data:</c> URL (e.g. bound from model data) can't render a script-executing link. When null
    /// the control falls back to plain read-only text.
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
    /// <para>
    /// Two further shapes reach the "unparseable, so it must be a safe relative URL" fall-through and
    /// are rejected before it. (1) Preprocessing can consume the entire string -- a <see cref="Url"/>
    /// that is nothing but a C0 control (<c>U+0001</c>, say) survives the
    /// <see cref="string.IsNullOrWhiteSpace"/> guard above, because a C0 control is not .NET
    /// whitespace, and then trims away to nothing; an empty <c>href</c> resolves to the current
    /// document, so the "link" silently reloads the page on click. (2) A
    /// protocol-relative URL (<c>//evil.example/x</c>) has no scheme to parse, so it looks relative to
    /// <see cref="Uri.TryCreate(string?, UriKind, out Uri?)"/> while a browser resolves it
    /// cross-origin against the page's own scheme -- and because browsers normalize backslashes to
    /// forward slashes for special schemes, <c>/\evil.example/x</c>, <c>\\evil.example/x</c> and
    /// <c>\/evil.example/x</c> all resolve the same way. Any two leading slash-or-backslash characters
    /// are therefore rejected: the fall-through's promise is a <em>same-origin</em> relative link, and
    /// none of those are one.
    /// </para>
    /// </remarks>
    string? SafeUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return null;
            var trimmed = TrimLeadingAndTrailingC0OrSpace(Url);
            var stripped = StripAsciiTabAndNewlines(trimmed);
            if (stripped.Length == 0) return null;
            if (stripped.Length >= 2 && IsSlashOrBackslash(stripped[0]) && IsSlashOrBackslash(stripped[1])) return null;
            // Absolute URLs must use an allow-listed scheme; relative URLs (no scheme) are fine.
            if (Uri.TryCreate(stripped, UriKind.Absolute, out var uri))
                return uri.Scheme is "http" or "https" or "mailto" ? stripped : null;
            return stripped;
        }
    }

    /// <summary> True for the two characters a browser treats interchangeably as a URL path separator (it normalizes <c>\</c> to <c>/</c> for special schemes). </summary>
    static bool IsSlashOrBackslash(char c) => c is '/' or '\\';

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

    /// <summary>
    /// Whether the read-only link opens in a new browsing context the user did not ask for --
    /// <c>_blank</c>, matched case-insensitively like every other <see cref="UrlTarget"/> keyword
    /// check here. Drives the visually-hidden "(opens in new tab)" suffix inside the link.
    /// </summary>
    /// <remarks>
    /// Only <c>_blank</c>, not the named targets <see cref="UrlRel"/> also hardens. A named target
    /// reuses an existing context when one by that name is open, so "opens in a new tab" would be a
    /// claim the control can't make; <c>_blank</c> always creates one.
    /// </remarks>
    bool OpensInNewTab => string.Equals(UrlTarget, "_blank", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// rel for the read-only link: <c>"noopener noreferrer"</c> for any <see cref="UrlTarget"/> that
    /// can hand another browsing context a <c>window.opener</c> handle on this page, null for the
    /// keywords that can't.
    /// </summary>
    /// <remarks>
    /// A <em>named</em> target (<c>UrlTarget="vendor"</c>) is the case that most needs this: it opens
    /// or reuses a separate browsing context whose <c>window.opener</c> points back here, so that
    /// document can navigate this page out from under the user -- reverse tabnabbing. <c>_blank</c>
    /// gets the same treatment for defence in depth, though every current browser already implies
    /// <c>noopener</c> for it. Only the same-context keywords are exempt: <c>_self</c>,
    /// <c>_parent</c>, <c>_top</c> (and no target at all) reuse a context that is already ours, so
    /// there is no opener to sever -- and <c>noreferrer</c> would needlessly drop the referrer on a
    /// navigation within our own frame tree. Unrecognized keyword-looking values are treated as named
    /// targets, which is what the HTML spec says a browser does with them too.
    /// </remarks>
    string? UrlRel =>
        string.IsNullOrEmpty(UrlTarget)
        || string.Equals(UrlTarget, "_self", StringComparison.OrdinalIgnoreCase)
        || string.Equals(UrlTarget, "_parent", StringComparison.OrdinalIgnoreCase)
        || string.Equals(UrlTarget, "_top", StringComparison.OrdinalIgnoreCase)
            ? null
            : "noopener noreferrer";

    /// <summary>
    /// Toggles the password reveal state driving the shell's show/hide button. Inert while the
    /// control is disabled: the shell renders that button with native <c>disabled</c>, so a browser
    /// won't fire the click at all, but the value of a disabled field must not be revealable through
    /// any path that can still reach this handler (a programmatic <c>.click()</c>, a test harness
    /// that dispatches to disabled elements) — the guard, not the attribute, is what makes that true.
    /// </summary>
    void TogglePasswordVisibility()
    {
        if (IsDisabled) return;
        _passwordRevealed = !_passwordRevealed;
    }

    /// <summary>
    /// Toggles the read-only masked row between the mask and the real value.
    /// </summary>
    /// <remarks>
    /// A named method rather than the inline lambda it replaced, and that matters beyond style: two
    /// <c>EventCallback</c>s built from the same method group compare equal, so Blazor's diff retains
    /// the button's event-handler id when it patches the element in place — and assigns a fresh one
    /// only when the element is actually recreated. That makes the id an observable proxy for "the
    /// button survived the toggle", which is what the single-render-site shape (see EditString.razor)
    /// exists to guarantee and what <c>EditStringMaskedValueTests</c> pins. A fresh lambda per render
    /// compares unequal every time and would hide the difference.
    /// </remarks>
    void ToggleMaskedValue() => _showMaskedValue = !_showMaskedValue;

    /// <summary>
    /// The masked read-only text: <see cref="EffectiveMaskText"/> followed by whatever tail of the
    /// value it doesn't cover (or the mask alone once it's at least as long as the value). A
    /// single-character mask is the special case -- it repeats to cover the whole value rather than
    /// prefixing it, which is also how a password field's bullet mask covers its secret.
    /// </summary>
    /// <remarks>
    /// Both paths count in text elements rather than <c>char</c>s where it matters, because a UTF-16
    /// code unit is not a character the user can see. A single-character mask repeats once per
    /// grapheme cluster, so an astral character (an emoji, say) is replaced by one mask glyph instead
    /// of being widened into two, and a combining sequence by one instead of one per combining mark.
    /// A multi-character mask's cut point is a UTF-16 offset that can land between the two halves of a
    /// surrogate pair; keeping the orphaned low half would render a replacement character right after
    /// the mask, so the cut moves forward to swallow the whole pair. A cut landing between a base
    /// character and its combining marks is deliberately left alone: the marks render against the
    /// mask's last glyph, which is odd-looking but discloses nothing and never mojibakes.
    /// </remarks>
    string? GetMaskValue()
    {
        var mask = EffectiveMaskText;
        if (string.IsNullOrEmpty(mask) || CurrentValue == null)
            return CurrentValue;

        // A single-character mask covers the whole value: one mask character per visible character,
        // so the mask's width matches the width of what it replaces. This is also the path a password
        // field takes -- its bullet is a single-character mask.
        if (mask.Length == 1)
            return new string(mask[0], GraphemeCount(CurrentValue));

        if (mask.Length >= CurrentValue.Length)
            return mask;

        var tailStart = mask.Length;
        if (char.IsLowSurrogate(CurrentValue[tailStart])) tailStart++;
        return MaskText + CurrentValue[tailStart..];
    }

    /// <summary>
    /// Counts grapheme clusters -- what <c>new StringInfo(value).LengthInTextElements</c> returns,
    /// without allocating the <c>StringInfo</c>.
    /// </summary>
    static int GraphemeCount(string value)
    {
        var count = 0;
        for (var remaining = value.AsSpan(); !remaining.IsEmpty; count++)
            remaining = remaining[StringInfo.GetNextTextElementLength(remaining)..];
        return count;
    }
}
