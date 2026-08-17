namespace Controls;

/// <summary> 
/// Used within other edit controls to display the value when EditMode is false.
/// </summary>
public partial class ReadOnlyValue
{
    [Parameter] public required string Id { get; set; }

    /// <summary>
    /// False when no <see cref="FormLabel"/> element exists for this value to be named by, which
    /// suppresses <c>aria-labelledby</c> so it can't dangle at a missing <c>lbltext-{id}</c>. Only the
    /// per-option rows of a read-only checked list set this — each carries its own derived id and no
    /// label of its own. A <em>hidden</em> label is not such a case: <see cref="FormLabel"/> still
    /// renders the <c>lbltext-{id}</c> naming anchor (visually hidden), and dropping the reference
    /// would leave the value with no accessible name at all. Defaults to true.
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
    /// description/tooltip/error-message ids, the same references its editor carries in edit mode.
    /// Null by default, but EVERY call site in the library now passes its control's cached
    /// <c>_describedBy</c>: a read-only field that omits it drops its description, its tooltip text and
    /// its validation message from the value's announcement entirely, which is precisely the
    /// information a reader has no other way to reach once the editor is gone. The elements those
    /// tokens point at all render in read-only mode too (<c>FormLabel</c> and
    /// <c>FieldValidationDisplay</c> are outside every <c>@if (ShowEditor)</c>), so nothing dangles.
    /// <para>
    /// Two tokens are deliberately EXCLUDED in read-only, by the callers rather than here, because the
    /// elements only exist alongside an editor: <c>count-{id}</c> (gated by
    /// <c>EditTextInputBase.HasCharacterCount</c>, which ANDs in <c>ShowEditor</c>) and
    /// <c>EditDateNative</c>'s <c>format-{id}</c> typed-entry hint (which is why that control passes
    /// <c>_describedBy</c>, not its own <c>EffectiveDescribedBy</c>).
    /// </para>
    /// </summary>
    [Parameter] public string? AriaDescribedBy { get; set; }
}