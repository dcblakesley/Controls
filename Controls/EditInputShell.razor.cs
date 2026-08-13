namespace Controls;

/// <summary>
/// Internal-use shell shared by <see cref="EditString"/>, <see cref="EditNumber{T}"/>,
/// <see cref="EditTextArea"/>, and <see cref="EditDateNative{T}"/>: wraps the host's editor element
/// (passed as <see cref="ChildContent"/>) together with the standard <see cref="InvalidIcon"/>, and
/// — once a host starts setting one of the affix parameters — the AntD-style prefix/suffix/clear/
/// count/password-toggle chrome. A host that sets none of <see cref="Prefix"/>, <see cref="Suffix"/>,
/// <see cref="AllowClear"/>, <see cref="CountText"/>, or <see cref="ShowPasswordToggle"/> gets
/// exactly today's markup back (see <see cref="UsesAffixLayout"/>), so adopting the shell is a
/// no-DOM-change refactor until a control actually starts passing affix content.
/// </summary>
public partial class EditInputShell
{
    /// <summary>Optional leading affix content (e.g. a currency symbol or icon). Non-null switches
    /// the shell into affix-mode layout.</summary>
    [Parameter] public RenderFragment? Prefix { get; set; }

    /// <summary>Optional custom trailing affix content, rendered after the clear button and count
    /// span but before the password toggle (locked order — see the class remarks). Non-null
    /// switches the shell into affix-mode layout.</summary>
    [Parameter] public RenderFragment? Suffix { get; set; }

    /// <summary>Whether the host supports clear-to-null. True switches the shell into affix-mode
    /// layout regardless of <see cref="IsClearable"/> — the affix wrapper stays in place as the user
    /// types so the box never resizes; the button itself only appears while <see cref="IsClearable"/>
    /// is also true.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary>Whether the clear button should render right now — the host computes this (typically
    /// "has a non-empty value and the editor is enabled").</summary>
    [Parameter] public bool IsClearable { get; set; }

    /// <summary>Raised when the clear button is activated. The host clears its bound value and
    /// refocuses the editor.</summary>
    [Parameter] public EventCallback OnClear { get; set; }

    /// <summary>
    /// The clear button's <c>aria-label</c>. Null (the default) renders the generic <c>"Clear"</c> --
    /// fine for a single instance, but a form with two <see cref="AllowClear"/> fields then renders two
    /// buttons with an identical accessible name, which a screen-reader user browsing a button list
    /// can't tell apart (TXT-4). A host that can resolve its own field's label passes a field-specific
    /// name instead -- see <see cref="EditTextInputBase.EffectiveClearButtonLabel"/>, which folds it
    /// into <c>"Clear {label}"</c>.
    /// </summary>
    [Parameter] public string? ClearButtonLabel { get; set; }

    /// <summary>Non-null renders the character-count span (e.g. <c>"12"</c> or <c>"12 / 100"</c>)
    /// and switches the shell into affix-mode layout. Null renders no count span. The span itself is
    /// <c>aria-hidden</c> — see <see cref="CountId"/> for what assistive tech gets instead.</summary>
    [Parameter] public string? CountText { get; set; }

    /// <summary>
    /// The id for the visually-hidden span carrying <see cref="CountAccessibleText"/>, which the
    /// host's own <c>aria-describedby</c> references (<c>count-{id}</c>). Null omits the whole
    /// assistive-tech half of the counter: there is nothing for a describedby to point at, so an
    /// unreferenced sr-only span would only add browse-mode noise.
    /// </summary>
    /// <remarks>
    /// A host that sets <see cref="CountText"/> passes this and
    /// <see cref="CountAccessibleText"/>/<see cref="CountLimitStatus"/> together — see
    /// <see cref="EditTextInputBase"/>, where all four come off one <c>ShowCount</c>.
    /// </remarks>
    [Parameter] public string? CountId { get; set; }

    /// <summary>The spoken character count (<see cref="BuildCountAccessibleText"/>), rendered into the
    /// <see cref="CountId"/> span. The visible <see cref="CountText"/> is <c>aria-hidden</c> in its
    /// favour.</summary>
    [Parameter] public string? CountAccessibleText { get; set; }

    /// <summary>The near-the-limit announcement (<see cref="BuildCountLimitStatus"/>), rendered into a
    /// visually-hidden <c>role="status"</c> region. Null/empty leaves the region present but silent —
    /// it has to exist before it has anything to say, or the first announcement is missed.</summary>
    [Parameter] public string? CountLimitStatus { get; set; }

    /// <summary>
    /// Textarea-only layout: when true and <see cref="CountText"/> is non-null, the count renders as
    /// <c>&lt;span class="edit-textarea-count"&gt;</c> after the suffix span — a direct child of the
    /// affix wrapper, landing on its own line under the editor — instead of inside
    /// <c>edit-input-suffix</c> alongside the clear/password buttons. Matches AntD <c>TextArea</c>'s
    /// <c>showCount</c> placement (below-right) versus <c>Input</c>'s (inline, trailing). No effect
    /// when <see cref="CountText"/> is null; doesn't itself switch on affix-mode layout (CountText
    /// already does that).
    /// </summary>
    [Parameter] public bool CountBelow { get; set; }

    /// <summary>Whether to render the password show/hide toggle button. True switches the shell into
    /// affix-mode layout.</summary>
    [Parameter] public bool ShowPasswordToggle { get; set; }

    /// <summary>Whether the password value is currently shown as plain text (drives the toggle
    /// button's icon and <c>aria-pressed</c>; its <c>aria-label</c> deliberately does not move —
    /// see the markup's remarks).</summary>
    [Parameter] public bool IsPasswordRevealed { get; set; }

    /// <summary>Raised when the password toggle button is activated.</summary>
    [Parameter] public EventCallback OnTogglePassword { get; set; }

    /// <summary>
    /// The password toggle's <c>aria-label</c>. Null (the default) renders the generic
    /// <c>"Show password"</c> -- same collision <see cref="ClearButtonLabel"/>'s remarks describe: a
    /// Password/Confirm-Password pair otherwise renders two toggles both named "Show password". See
    /// <see cref="EditString.EffectiveShowPasswordButtonLabel"/> for the field-specific override, which
    /// folds the label in as <c>"Show {label} password"</c>. The name stays CONSTANT across both
    /// reveal states either way -- only <see cref="IsPasswordRevealed"/>'s <c>aria-pressed</c> moves.
    /// </summary>
    [Parameter] public string? ShowPasswordButtonLabel { get; set; }

    /// <summary>Whether the host field currently has a validation error — forwarded to
    /// <see cref="InvalidIcon"/> and, in affix mode, adds <c>edit-input-affix-invalid</c> to the
    /// wrapper.</summary>
    [Parameter] public bool IsInvalid { get; set; }

    /// <summary>The editor element (<c>&lt;input&gt;</c>/<c>&lt;textarea&gt;</c>/<c>&lt;InputDate&gt;</c>).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Whether the host control is currently disabled — added as <c>edit-input-affix-disabled</c> to
    /// the affix wrapper, and set as native <c>disabled</c> on the password toggle button. There's no
    /// <c>:disabled</c> pseudo-class for a wrapper <c>div</c>, so the
    /// four hosts (EditString/EditNumber/EditTextArea/EditDateNative) pass their own <c>IsDisabled</c>
    /// through unconditionally and the <c>.edit-theme</c> opt-in theme keys off this class instead —
    /// the same C#-owned-class approach <c>Select</c>'s <c>wss-select-disabled</c> uses. No effect in
    /// legacy mode (the wrapper doesn't render there; the editor's own native <c>:disabled</c> covers
    /// unthemed and themed styling).
    /// </summary>
    /// <remarks>
    /// The toggle stays rendered rather than being dropped: the field's trailing chrome keeps its
    /// width when a form disables it. Native <c>disabled</c> is what actually matters — a disabled
    /// field's value must not be revealable, and the attribute also removes the button from the tab
    /// order (the clear button needs no equivalent: hosts compute <see cref="IsClearable"/> as
    /// false while disabled, so it isn't rendered at all).
    /// </remarks>
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary>
    /// Extra class(es) appended to the affix wrapper's class list — e.g. a host's <see cref="SizeClass"/>
    /// token, so <c>edit-input-sm</c>/<c>edit-input-lg</c> land on the themed wrapper the same way
    /// they land on the editor. No effect in legacy mode (the wrapper doesn't render there).
    /// </summary>
    [Parameter] public string? WrapperClass { get; set; }

    /// <summary>
    /// True when any affix feature is in use — the single computation site both the shell and its
    /// hosts must agree on. Hosts call this with their own parameter values (before setting any of
    /// them, so today's controls always get <c>false</c>) to decide whether to drop the
    /// <c>edit-input-legacy-padding</c> class (<c>padding-inline-end: 2rem</c>, reserving room for
    /// InvalidIcon inside <c>.edit-input-with-icon</c> -- see edit-controls.css) and add
    /// <c>edit-affix-input</c> to the editor instead, keeping that decision in lockstep with the
    /// shell's own layout choice.
    /// </summary>
    public static bool UsesAffixLayout(RenderFragment? prefix, RenderFragment? suffix, bool allowClear, string? countText, bool showPasswordToggle) =>
        prefix is not null || suffix is not null || allowClear || countText is not null || showPasswordToggle;

    /// <summary>
    /// Maps <see cref="SelectSize"/> (shared with the <c>Select</c> family) to the <c>.edit-theme</c>
    /// size class token, or null for <see cref="SelectSize.Default"/> (adds no class, so a
    /// no-new-params render stays byte-identical). Single computation site for all four Size-bearing
    /// hosts (EditString/EditNumber/EditTextArea/EditDateNative) — each appends this to its own editor
    /// class string and passes it through as <see cref="WrapperClass"/>.
    /// </summary>
    public static string? SizeClass(SelectSize size) => size switch
    {
        SelectSize.Small => "edit-input-sm",
        SelectSize.Large => "edit-input-lg",
        _ => null
    };

    /// <summary>
    /// Appends <see cref="SizeClass"/>'s token (if any) and the host's own <c>CssClass</c> to a
    /// control-specific base class string — the single computation site for the tail every
    /// Size-bearing host (EditDateNative/EditNumber/EditString/EditTextArea) repeats after building its
    /// own affix-mode-dependent prefix.
    /// </summary>
    public static string BuildInputClass(string baseClasses, SelectSize size, string? cssClass)
    {
        var sizeClass = SizeClass(size);
        var classes = sizeClass is null ? baseClasses : $"{baseClasses} {sizeClass}";
        return $"{classes} {cssClass}";
    }

    /// <summary>
    /// The shell's character-count text (AntD format: <c>"{length}"</c> alone, or
    /// <c>"{length} / {maxLength}"</c> once <paramref name="maxLength"/> is set), or null when
    /// <paramref name="showCount"/> is false -- the single computation site shared by EditString and
    /// EditTextArea.
    /// </summary>
    public static string? BuildCountText(bool showCount, int length, int? maxLength) =>
        !showCount ? null : maxLength is null ? $"{length}" : $"{length} / {maxLength}";

    /// <summary>
    /// The spoken form of <see cref="BuildCountText"/> — <c>"12 of 100 characters"</c>, or
    /// <c>"12 characters"</c> with no maximum — for the visually-hidden span
    /// <c>aria-describedby</c> points at. Null when <paramref name="showCount"/> is false.
    /// </summary>
    /// <remarks>
    /// The visible span can't do this job: <c>"12 / 100"</c> is read as "twelve slash one hundred" or
    /// (worse) "twelve one hundred" depending on the screen reader's punctuation level, and as a bare
    /// unlabelled number it is meaningless out of its visual context. So the visible span is
    /// <c>aria-hidden</c> and this text is what AT gets — which also fixes the other half of the
    /// problem: the count was reachable in browse mode as orphan noise but was NOT part of the
    /// field's description, so a user focusing the field never learned there was a limit at all.
    /// </remarks>
    public static string? BuildCountAccessibleText(bool showCount, int length, int? maxLength) =>
        !showCount ? null
            : maxLength is { } max ? $"{length} of {max} characters"
            : length == 1 ? "1 character"
            : $"{length} characters";

    /// <summary>
    /// The near-the-limit live-region text — <c>"N characters remaining"</c>, or
    /// <c>"Character limit reached"</c> at or past the maximum — or null when the user is nowhere
    /// near it (the overwhelmingly common case), which renders an empty region that says nothing.
    /// </summary>
    /// <remarks>
    /// Announcing every keystroke's count would be a firehose that drowns out the typing itself, so
    /// this only speaks inside the last <c>min(10, 10% of the maximum)</c> characters (floor 1): ten
    /// characters is a useful warning distance for a long field but half of a twenty-character one,
    /// and 10% of a thousand-character field is a warning that arrives far too early. The
    /// limit-reached case is the one that most needs saying — <c>maxlength</c> silently truncates a
    /// paste, with no other signal that anything was dropped.
    /// </remarks>
    public static string? BuildCountLimitStatus(bool showCount, int length, int? maxLength)
    {
        if (!showCount || maxLength is not { } max || max <= 0) return null;

        var remaining = max - length;
        if (remaining > Math.Clamp(max / 10, 1, 10)) return null;
        return remaining switch
        {
            <= 0 => "Character limit reached",
            1 => "1 character remaining",
            _ => $"{remaining} characters remaining"
        };
    }

    bool UseAffixLayout => UsesAffixLayout(Prefix, Suffix, AllowClear, CountText, ShowPasswordToggle);
}
