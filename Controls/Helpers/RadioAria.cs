namespace Controls.Helpers;

/// <summary>
/// The <c>&lt;fieldset&gt;</c> id/test-id/ARIA attribute block that the three radio-group controls —
/// <c>EditRadio&lt;TValue&gt;</c>, <c>EditRadioEnum&lt;TEnum&gt;</c> and <c>EditRadioString</c> — render
/// identically, returned for an <c>@attributes</c> splat so the nine attributes and their gating have
/// one authoring site instead of three.
/// </summary>
/// <remarks>
/// A static helper rather than a base-class property for this assembly's usual reason:
/// <c>EditRadio&lt;TValue&gt;</c> must inherit <c>InputRadioGroup&lt;TValue&gt;</c> (its consumer-authored
/// child <c>InputRadio</c>s resolve that cascading context), while the other two inherit
/// <c>RadioGroupControlBase&lt;TValue&gt;</c>, so no shared ancestor can host it.
/// <para>
/// The checkbox-list pair (<c>EditCheckedEnumList</c>/<c>EditCheckedStringList</c>) deliberately does
/// NOT use this: ARIA 1.2 supports <c>aria-required</c>/<c>aria-invalid</c>/<c>aria-errormessage</c> on
/// <c>radiogroup</c> but not on <c>group</c>, so those fieldsets carry only the id/test-id/role trio
/// (see the comment in each of their markup files) and share nothing worth extracting.
/// </para>
/// </remarks>
internal static class RadioAria
{
    /// <summary>
    /// Builds the fieldset's attribute set, or <c>null</c> in read-only mode so no splat is emitted at
    /// all — <c>class</c> stays written out per control, since each composes it differently.
    /// </summary>
    /// <param name="showEditor">The control's <c>ShowEditor</c>. False returns null: a
    /// <c>role="radiogroup"</c> with no radio children trips axe's <c>aria-required-children</c>, and the
    /// bare id would collide with the <c>ReadOnlyValue</c> that takes it over in read-only mode.</param>
    /// <param name="id">The control's resolved element id (<c>_id</c>).</param>
    /// <param name="ariaRequired">The resolved <c>aria-required</c> value (<c>"true"</c> or null) — <c>_isRequired</c>.</param>
    /// <param name="isInvalid">The control's <c>IsInvalid</c>; gates <c>aria-invalid</c> and <c>aria-errormessage</c>.</param>
    /// <param name="describedBy">The control's cached <c>aria-describedby</c> token list (<c>_describedBy</c>).</param>
    /// <param name="errorMsgId">The control's cached validation-message element id (<c>_errorMsgId</c>).</param>
    /// <param name="isHorizontal">
    /// True when the options are laid out in a row — the control's own <c>IsHorizontal</c>, OR'd with
    /// its <c>OptionType == RadioOptionType.Button</c> (the segmented button mode is inherently
    /// horizontal whatever the flag says). Emits <c>aria-orientation="horizontal"</c>: the APG Radio
    /// Group pattern's default assumption is vertical (Up/Down arrows), so a horizontal group that
    /// never says so leaves the user reaching for the wrong keys. Defaults to false, which emits
    /// nothing at all — <c>vertical</c> is the role's implicit value and spelling it out is noise.
    /// </param>
    public static IReadOnlyDictionary<string, object>? Fieldset(
        bool showEditor, string id, string? ariaRequired, bool isInvalid,
        string? describedBy, string? errorMsgId, bool isHorizontal = false)
    {
        if (!showEditor)
            return null;

        // Every optional entry is ADDED only when it has a value rather than stored with a null value:
        // an omitted attribute is what the a11y suite pins (no aria-required="false"/
        // aria-invalid="false" noise), and a null dictionary value is not a reliable way to spell
        // "omit" across every render path.
        //
        // aria-labelledby points at lbltext-{id} -- FormLabel's naming anchor, the span holding just
        // the label text -- NOT at the lbl-{id} legend that contains it. The legend also contains the
        // LabelTooltip trigger, and accessible-name computation folds a descendant button's own name
        // in, so naming the group from the whole legend made every tooltipped radio group announce as
        // "Priority More information about Priority". Unconditional either way: FormLabel renders the
        // anchor in all four of its branches, hidden label included, so the reference never dangles.
        var attributes = new Dictionary<string, object>(9)
        {
            ["id"] = id,
            ["data-test-id"] = id,
            ["role"] = "radiogroup",
            ["aria-labelledby"] = $"lbltext-{id}"
        };
        if (isHorizontal)
            attributes["aria-orientation"] = "horizontal";
        if (ariaRequired is not null)
            attributes["aria-required"] = ariaRequired;
        if (isInvalid)
            attributes["aria-invalid"] = "true";
        if (!string.IsNullOrEmpty(describedBy))
            attributes["aria-describedby"] = describedBy;
        if (isInvalid && !string.IsNullOrEmpty(errorMsgId))
            attributes["aria-errormessage"] = errorMsgId;
        return attributes;
    }
}
