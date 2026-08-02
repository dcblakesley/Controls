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

    /// <summary> Shows a clear button (via <see cref="EditInputShell"/>) while the editor's text is non-empty and the control is enabled. Clicking it empties the value (see <see cref="Clear"/>) and refocuses the editor.</summary>
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
    /// the control isn't disabled, and the editor's text (see <see cref="EditorText"/>) is non-empty.
    /// </summary>
    protected bool IsClearable => AllowClear && !IsDisabled && !string.IsNullOrEmpty(EditorText);

    /// <summary>
    /// The shell's character-count text when <see cref="ShowCount"/> is set, else null (no count
    /// renders). AntD format: <c>"{length}"</c> alone, or <c>"{length} / {EffectiveMaxLength}"</c>
    /// once <see cref="EffectiveMaxLength"/> is set. Length counts <see cref="EditorText"/>,
    /// treating null as zero.
    /// </summary>
    protected string? CountText => EditInputShell.BuildCountText(ShowCount, EditorText?.Length ?? 0, EffectiveMaxLength);

    /// <summary>
    /// The id of the shell's visually-hidden spoken-count span, which this control's
    /// <c>aria-describedby</c> references — see <see cref="HasCharacterCount"/>. Null when no count
    /// renders, so nothing unreferenced is emitted.
    /// </summary>
    protected string? CountId => ShowCount ? $"count-{_id}" : null;

    /// <summary>The spoken form of <see cref="CountText"/> — see <see cref="EditInputShell.BuildCountAccessibleText"/>.</summary>
    protected string? CountAccessibleText =>
        EditInputShell.BuildCountAccessibleText(ShowCount, EditorText?.Length ?? 0, EffectiveMaxLength);

    /// <summary>The near-the-limit live-region text — see <see cref="EditInputShell.BuildCountLimitStatus"/>.</summary>
    protected string? CountLimitStatus =>
        EditInputShell.BuildCountLimitStatus(ShowCount, EditorText?.Length ?? 0, EffectiveMaxLength);

    /// <inheritdoc/>
    /// <remarks>
    /// Both halves are required. <see cref="ShowCount"/> because a field with no counter must keep a
    /// byte-identical <c>aria-describedby</c>; <see cref="EditControlBase{TValue}.ShowEditor"/>
    /// because the count lives in <see cref="EditInputShell"/>, which the read-only views don't
    /// render — a <c>count-{id}</c> token there would dangle.
    /// </remarks>
    protected override bool HasCharacterCount => ShowCount && ShowEditor;

    // ───────────────────── live editor text under a commit-on-blur binding ─────────────────────

    /// <summary>
    /// Whether the affix chrome needs the editor's live text — i.e. one of the two features that
    /// reflect what is currently typed (<see cref="ShowCount"/>, <see cref="AllowClear"/>) is on AND
    /// the bound commit event has resolved away from <c>oninput</c>, so
    /// <see cref="InputBase{TValue}.CurrentValue"/> only moves on blur.
    /// </summary>
    /// <remarks>
    /// Both halves matter. Without the feature check the extra handler would attach to every
    /// commit-on-blur editor, and the affix-free legacy DOM must stay byte-identical for controls
    /// that use none of this. Without the event check it would collide with the bound
    /// <c>oninput</c> the default <see cref="UpdateTrigger.Input"/> binding already renders.
    /// </remarks>
    protected bool TracksLiveText => (ShowCount || AllowClear) && UpdateEventName != "oninput";

    // The editor's text as of the last oninput, captured only while TracksLiveText is on. Null means
    // "nothing fresher than CurrentValue" -- an empty string is a real captured value (the user
    // deleted everything), which is exactly the case the clear button has to react to, so this can't
    // collapse into a plain null check.
    string? _liveText;

    /// <summary>
    /// The text the affix chrome (count, clear button) reflects: the live editor text when one has
    /// been captured since the last commit, else <see cref="InputBase{TValue}.CurrentValue"/>.
    /// </summary>
    /// <remarks>
    /// Under the default per-keystroke binding the two are always the same and this is just
    /// <see cref="InputBase{TValue}.CurrentValue"/>. Under <see cref="UpdateTrigger.Change"/> —
    /// per-control or cascaded from <see cref="FormDefaults"/> — <see cref="InputBase{TValue}.CurrentValue"/>
    /// doesn't move until blur, which used to freeze the counter at its pre-typing value for the whole
    /// time the user was typing and make the clear button appear a gesture late. The bound value's own
    /// commit timing is untouched: this only feeds what the chrome displays.
    /// </remarks>
    protected string? EditorText => _liveText ?? CurrentValue;

    /// <summary>
    /// The extra <c>oninput</c> handler, splatted onto the editor element by
    /// <see cref="EditorInputAttributes"/>. Records the live text for <see cref="EditorText"/>;
    /// the component re-renders afterwards the way it does for any component event handler, which is
    /// what actually moves the count/clear chrome mid-typing. Overridden by <see cref="EditTextArea"/>,
    /// which shares this one handler for its AutoSize re-measure.
    /// </summary>
    protected virtual Task OnEditorInputAsync(ChangeEventArgs e)
    {
        if (TracksLiveText) _liveText = e.Value as string ?? string.Empty;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs after every bound-value commit (<c>@bind-value:after</c>): drops any captured live text,
    /// which the commit has just made redundant — <see cref="InputBase{TValue}.CurrentValue"/> now
    /// holds exactly what the editor shows.
    /// </summary>
    /// <remarks>
    /// The commit is the one live-text staleness case <see cref="OnParametersSet"/> can't catch: under
    /// <see cref="UpdateTrigger.Change"/> the blur that commits doesn't necessarily re-parameterize the
    /// control (nothing forces the parent to re-render), so without this the chrome would keep
    /// describing the pre-blur keystrokes. Wired unconditionally on both editors: <c>:after</c> never
    /// renders as a DOM attribute of its own, so attaching it leaves the markup byte-identical.
    /// Overridden by <see cref="EditTextArea"/>, which re-measures its AutoSize height here.
    /// </remarks>
    protected virtual Task OnValueCommittedAsync()
    {
        _liveText = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether the editor needs the extra <c>oninput</c> handler at all. <see cref="EditTextArea"/>
    /// widens this: its AutoSize re-measure needs the same handler under the same commit-on-blur
    /// condition, and one element can only carry one <c>oninput</c>.
    /// </summary>
    protected virtual bool NeedsEditorInputHandler => TracksLiveText;

    /// <summary>
    /// The extra <c>oninput</c> attribute, splatted onto the editor element (<c>@attributes</c>) —
    /// null in every case that doesn't need it, and a null splat renders no attribute at all, so the
    /// legacy affix-free DOM stays byte-identical. Never collides with the bound commit event by
    /// construction: <see cref="NeedsEditorInputHandler"/> is only true where
    /// <see cref="EditTextControlBase{TValue}.UpdateEventName"/> has resolved to <c>"onchange"</c>.
    /// </summary>
    protected IReadOnlyDictionary<string, object>? EditorInputAttributes =>
        NeedsEditorInputHandler
            ? new Dictionary<string, object>(1) { ["oninput"] = EventCallback.Factory.Create<ChangeEventArgs>(this, OnEditorInputAsync) }
            : null;

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
    /// The shell's clear button action: empties the value (via <see cref="InputBase{TValue}.CurrentValue"/>,
    /// which raises <c>ValueChanged</c>/<c>NotifyFieldChanged</c> itself), refocuses the editor, then
    /// runs the derived control's <see cref="OnClearedAsync"/> hook. Focus is best-effort -- see
    /// <see cref="_editorRef"/>'s remarks.
    /// </summary>
    /// <remarks>
    /// The empty string, not null, and for two reasons. It is the same model value the user's own
    /// deletion path produces (a text editor emptied by hand reports <c>""</c>, never null), so the
    /// two gestures that mean "there is no text here" can't disagree about what lands in the model --
    /// and it matches AntD's <c>allowClear</c>. It also keeps the control on screen: under
    /// <see cref="HidingMode.WhenNull"/> the null answer made <see cref="EditControlBase{TValue}.ShouldShowComponent"/>
    /// unmount the whole control the instant its clear button was clicked, with no editor left to type
    /// a new value into. Consumers that need to distinguish "cleared" from "empty" should read the
    /// empty string, or use <see cref="HidingMode.WhenNullOrDefault"/> if the intent was to hide
    /// empties too.
    /// </remarks>
    protected async Task Clear()
    {
        CurrentValue = string.Empty;
        _liveText = null;
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
    /// <remarks>
    /// Also resyncs <see cref="EditorText"/> to the bound value: a programmatic assignment (or the
    /// blur that finally commits what was typed) makes any captured live text stale, and so does
    /// turning <see cref="TracksLiveText"/> off at runtime, which detaches the handler that would
    /// otherwise refresh it.
    /// </remarks>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        ValueChangedSinceLastParameters = !string.Equals(CurrentValue, _lastParameterValue, StringComparison.Ordinal);
        _lastParameterValue = CurrentValue;

        if (ValueChangedSinceLastParameters || !TracksLiveText) _liveText = null;
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
