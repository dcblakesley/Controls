namespace Controls;

/// <summary> 
/// Used within other edit controls to display the value when EditMode is false.
/// </summary>
public partial class ReadOnlyValue
{
    [Parameter] public required string Id { get; set; }

    /// <summary>
    /// False when no <see cref="FormLabel"/> element exists for this value to be named by, which
    /// suppresses <c>aria-labelledby</c> so it can't dangle at a missing <c>lbl-{id}</c>. Only the
    /// per-option rows of a read-only checked list set this — each carries its own derived id and no
    /// label of its own. A <em>hidden</em> label is not such a case: <see cref="FormLabel"/> still
    /// renders the <c>lbl-{id}</c> element (visually hidden), and dropping the reference would leave
    /// the value with no accessible name at all. Defaults to true.
    /// </summary>
    [Parameter] public bool HasLabelElement { get; set; } = true;

    [Parameter] public string? CssClass { get; set; }
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// The fallback text rendered in place of <see cref="Text"/> when it's empty (LST-2) -- e.g. the
    /// default "Not Set". A parameter (not a hardcoded string) so a consumer can localize it, matching
    /// the resolution pattern <see cref="EditBoolNullRadio.NullText"/> already establishes for a
    /// comparable "nothing here" text.
    /// </summary>
    [Parameter] public string EmptyText { get; set; } = "Not Set";

    /// <summary>
    /// Optional <c>aria-describedby</c> token(s) for the rendered value (TXT-5) -- e.g. the field's own
    /// description/tooltip/error-message ids, the same references its editor carries in edit mode. Null
    /// by default, and not yet passed by any call site -- <c>EditNumber</c>/<c>EditTextArea</c>/
    /// <c>EditString</c>'s plain read-only branch (which already computes a <c>_describedBy</c> for
    /// their own editor) are the candidates to start passing it here.
    /// </summary>
    [Parameter] public string? AriaDescribedBy { get; set; }
}