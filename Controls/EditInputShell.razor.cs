namespace Controls;

/// <summary>
/// Internal-use shell shared by <see cref="EditString"/>, <see cref="EditNumber{T}"/>,
/// <see cref="EditTextArea"/>, and <see cref="EditDate{T}"/>: wraps the host's editor element
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

    /// <summary>Non-null renders the character-count span (e.g. <c>"12"</c> or <c>"12 / 100"</c>)
    /// and switches the shell into affix-mode layout. Null renders no count span.</summary>
    [Parameter] public string? CountText { get; set; }

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
    /// button's icon, <c>aria-label</c>, and <c>aria-pressed</c>).</summary>
    [Parameter] public bool IsPasswordRevealed { get; set; }

    /// <summary>Raised when the password toggle button is activated.</summary>
    [Parameter] public EventCallback OnTogglePassword { get; set; }

    /// <summary>Whether the host field currently has a validation error — forwarded to
    /// <see cref="InvalidIcon"/> and, in affix mode, adds <c>edit-input-affix-invalid</c> to the
    /// wrapper.</summary>
    [Parameter] public bool IsInvalid { get; set; }

    /// <summary>The editor element (<c>&lt;input&gt;</c>/<c>&lt;textarea&gt;</c>/<c>&lt;InputDate&gt;</c>).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// True when any affix feature is in use — the single computation site both the shell and its
    /// hosts must agree on. Hosts call this with their own parameter values (before setting any of
    /// them, so today's controls always get <c>false</c>) to decide whether to drop the inline
    /// <c>padding-inline-end: 2rem</c> and add the <c>edit-affix-input</c> class to the editor,
    /// keeping that decision in lockstep with the shell's own layout choice.
    /// </summary>
    public static bool UsesAffixLayout(RenderFragment? prefix, RenderFragment? suffix, bool allowClear, string? countText, bool showPasswordToggle) =>
        prefix is not null || suffix is not null || allowClear || countText is not null || showPasswordToggle;

    bool UseAffixLayout => UsesAffixLayout(Prefix, Suffix, AllowClear, CountText, ShowPasswordToggle);
}
