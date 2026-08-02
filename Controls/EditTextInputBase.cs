namespace Controls;

/// <summary>
/// Base class for the two string-bound text editors — <see cref="EditString"/> (an
/// <c>&lt;input&gt;</c>) and <see cref="EditTextArea"/> (a <c>&lt;textarea&gt;</c>). Sits between them
/// and <see cref="EditTextControlBase{TValue}"/> (which the numeric/native-date controls share too),
/// and hoists the surface the two declared as byte-identical copies: the
/// <see cref="Placeholder"/>/<see cref="EffectivePlaceholder"/> and
/// <see cref="MaxLength"/>/<see cref="EffectiveMaxLength"/> pairs, the clear affordance
/// (<see cref="AllowClear"/>, <see cref="IsClearable"/>, <see cref="Clear"/> and the
/// <see cref="_editorRef"/> it refocuses), the character counter (<see cref="ShowCount"/>,
/// <see cref="CountText"/>), the pass-through <see cref="TryParseValueFromString"/>, the
/// empty-string-is-default rule (<see cref="IsValueDefault"/>), and the shared
/// <see cref="UpdateTrigger.Input"/> commit default.
/// </summary>
/// <remarks>
/// <para>
/// The bound type is fixed to <c>string?</c> — this base exists precisely because both controls edit
/// text, so nothing here is generic.
/// </para>
/// <para>
/// What stays on the two controls: <c>UseAffixLayout</c> and <c>InputClass</c> (EditString feeds
/// <see cref="EditInputShell.UsesAffixLayout"/> its Prefix/Suffix/password arguments, EditTextArea
/// passes those as null/false and adds its own AutoSize class token — different arguments, and the
/// shared logic already lives in <see cref="EditInputShell"/>'s statics), plus everything genuinely
/// single-control: EditString's mask/URL/autocomplete/password features and EditTextArea's
/// rows/AutoSize measurement. The one behavioral seam is clearing: EditTextArea must re-measure
/// afterwards, which it does by overriding <see cref="OnClearedAsync"/> rather than by re-declaring
/// <see cref="Clear"/>.
/// </para>
/// </remarks>
public abstract class EditTextInputBase : EditTextControlBase<string?>
{
    /// <summary>
    /// Placeholder text to display in the editor when empty. Falls back to the bound property's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt = "…")]</c> when unset -- see <see cref="EffectivePlaceholder"/>.
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// The placeholder actually rendered: the <see cref="Placeholder"/> parameter, else the model
    /// property's <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> text. Null when neither is set, so the
    /// attribute is omitted rather than rendered empty.
    /// </summary>
    protected string? EffectivePlaceholder => Placeholder ?? _attributes.Placeholder();

    /// <summary>
    /// Maximum number of characters, rendered as the editor's <c>maxlength</c> attribute. Falls back to
    /// the bound property's <c>[StringLength]</c>/<c>[MaxLength]</c> when unset -- see
    /// <see cref="EffectiveMaxLength"/>. Omitted (no browser-side cap) when neither is set.
    /// </summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>
    /// The maximum length actually rendered: the <see cref="MaxLength"/> parameter, else the model
    /// property's <c>[StringLength]</c>/<c>[MaxLength]</c> bound. Null when neither is set, so the
    /// <c>maxlength</c> attribute is omitted rather than rendered as an arbitrary cap.
    /// </summary>
    protected int? EffectiveMaxLength => MaxLength ?? _attributes.MaxTextLength();

    /// <summary> Shows a clear button (via <see cref="EditInputShell"/>) while the value is non-empty and the control is enabled. Clicking it sets the value to null and refocuses the editor.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary>
    /// Shows a character-count indicator (via <see cref="EditInputShell"/>): <c>"{length}"</c> alone,
    /// or <c>"{length} / {EffectiveMaxLength}"</c> once <see cref="EffectiveMaxLength"/> is set (AntD's
    /// format).
    /// </summary>
    /// <remarks>
    /// Only the placement differs between the two controls, and each one's markup picks it:
    /// <see cref="EditString"/> renders the count inline in the affix row, while
    /// <see cref="EditTextArea"/> passes <see cref="EditInputShell.CountBelow"/> to put it below the
    /// editor, right-aligned.
    /// </remarks>
    [Parameter] public bool ShowCount { get; set; }

    // Captures the <input>/<textarea> (assigned by each control's @ref) so Clear() can refocus it
    // directly -- unlike EditFile's RemoveFile, the element never unmounts here, so a plain
    // ElementReference.FocusAsync (Select/PickerBase's pattern) is enough; no JsInteropEc by-id
    // fallback needed.
    protected ElementReference _editorRef;

    /// <summary>
    /// Whether the shell's clear button should render right now: <see cref="AllowClear"/> is set,
    /// the control isn't disabled, and the current value is non-empty.
    /// </summary>
    protected bool IsClearable => AllowClear && !IsDisabled && !string.IsNullOrEmpty(CurrentValue);

    /// <summary>
    /// The shell's character-count text when <see cref="ShowCount"/> is set, else null (no count
    /// renders). AntD format: <c>"{length}"</c> alone, or <c>"{length} / {EffectiveMaxLength}"</c>
    /// once <see cref="EffectiveMaxLength"/> is set. Length counts <see cref="InputBase{TValue}.CurrentValue"/>,
    /// treating null as zero.
    /// </summary>
    protected string? CountText => EditInputShell.BuildCountText(ShowCount, CurrentValue?.Length ?? 0, EffectiveMaxLength);

    /// <inheritdoc/>
    /// <remarks>
    /// Both string editors commit per keystroke (<see cref="UpdateTrigger.Input"/>): a text
    /// <c>&lt;input&gt;</c>/<c>&lt;textarea&gt;</c> reports its full current text on every
    /// <c>oninput</c>, so there is no partially-typed-value hazard of the kind that makes
    /// <see cref="EditNumber{T}"/> and <see cref="EditDateNative{T}"/> default to
    /// <see cref="UpdateTrigger.Change"/>.
    /// </remarks>
    protected override UpdateTrigger DefaultUpdateTrigger => UpdateTrigger.Input;

    /// <summary>
    /// The shell's clear button action: sets the value to null (via <see cref="InputBase{TValue}.CurrentValue"/>,
    /// which raises <c>ValueChanged</c>/<c>NotifyFieldChanged</c> itself), refocuses the editor, then
    /// runs the derived control's <see cref="OnClearedAsync"/> hook. Focus is best-effort -- see
    /// <see cref="_editorRef"/>'s remarks.
    /// </summary>
    protected async Task Clear()
    {
        CurrentValue = null;
        try { await _editorRef.FocusAsync(); }
        catch { /* not focusable yet (prerender/tests) */ }
        await OnClearedAsync();
    }

    /// <summary>
    /// Post-clear hook, awaited at the end of <see cref="Clear"/>. A no-op by default; overridden by
    /// <see cref="EditTextArea"/>, which has to re-measure its AutoSize height because clearing
    /// bypasses the bound input event entirely (so its <c>@bind-value:after</c> handler never fires
    /// for it).
    /// </summary>
    protected virtual Task OnClearedAsync() => Task.CompletedTask;

    // The bound value as it stood at the previous parameter set -- the baseline
    // ValueChangedSinceLastParameters compares against.
    string? _lastParameterValue;

    /// <summary>
    /// Whether <see cref="InputBase{TValue}.CurrentValue"/> differs from what it was at the previous
    /// parameter set. Recomputed by <see cref="OnParametersSet"/> before any derived override's own
    /// work runs (they chain to base first), so a derived control can hang per-value state resets off
    /// it. True on the first parameter set for a non-null initial value — there is no earlier value
    /// to have matched.
    /// </summary>
    /// <remarks>
    /// This says the value changed, not who changed it: in edit mode the user's own typing moves
    /// <see cref="InputBase{TValue}.CurrentValue"/> too (per keystroke under
    /// <see cref="UpdateTrigger.Input"/>). Only key state off this where that is either the intent or
    /// harmless — see <see cref="EditString"/>'s read-only reveal reset, which additionally requires
    /// read-only mode, where a value change can only mean the control was handed different data.
    /// </remarks>
    protected bool ValueChangedSinceLastParameters { get; private set; }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValueChangedSinceLastParameters = !string.Equals(CurrentValue, _lastParameterValue, StringComparison.Ordinal);
        _lastParameterValue = CurrentValue;
    }

    // Trivial parser — same as Microsoft's InputText/InputTextArea: pass the string through.
    // `out string` (not `string?`) because InputBase<T>'s abstract signature declares it non-nullable.
    protected override bool TryParseValueFromString(string? value, out string? result, out string validationErrorMessage)
    {
        result = value;
        validationErrorMessage = null!;
        return true;
    }

    // Empty string counts as "default" for the NullOrDefault hiding modes.
    protected override bool IsValueDefault() => string.IsNullOrEmpty(CurrentValue);
}
