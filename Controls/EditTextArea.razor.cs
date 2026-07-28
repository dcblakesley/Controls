namespace Controls;

/// <summary> Edit control for multi-line string values, displays as a textarea with configurable row count.</summary>
public partial class EditTextArea : EditControlBase<string?>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<string?>>? Field { get; set; }

    /// <summary>
    /// Placeholder text to display in the textarea when empty. Falls back to the bound property's
    /// <c>[Placeholder]</c>/<c>[Display(Prompt = "…")]</c> when unset -- see <see cref="EffectivePlaceholder"/>.
    /// </summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>
    /// The placeholder actually rendered: the <see cref="Placeholder"/> parameter, else the model
    /// property's <c>[Placeholder]</c>/<c>[Display(Prompt)]</c> text. Null when neither is set, so the
    /// attribute is omitted rather than rendered empty.
    /// </summary>
    string? EffectivePlaceholder => Placeholder ?? _attributes.Placeholder();

    /// <summary>
    /// Number of visible text rows in the textarea. Falls back to the bound property's <c>[Rows]</c>
    /// (its <c>Rows</c> value, 0 meaning unset) when unset, then to 2 -- see <see cref="ResolvedRows"/>.
    /// Ignored for the initial height while <see cref="ResolvedAutoSize"/> is true -- see
    /// <see cref="ResolvedMinRows"/>.
    /// </summary>
    [Parameter] public int? Rows { get; set; }

    /// <summary> Shows a clear button (via <see cref="EditInputShell"/>) while the value is non-empty and the control is enabled. Clicking it sets the value to null and refocuses the textarea.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary>
    /// Maximum number of characters, rendered as the textarea's <c>maxlength</c> attribute. Falls back
    /// to the bound property's <c>[StringLength]</c>/<c>[MaxLength]</c> when unset -- see
    /// <see cref="EffectiveMaxLength"/>. Omitted (no browser-side cap) when neither is set.
    /// </summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary>
    /// The maximum length actually rendered: the <see cref="MaxLength"/> parameter, else the model
    /// property's <c>[StringLength]</c>/<c>[MaxLength]</c> bound. Null when neither is set, so the
    /// <c>maxlength</c> attribute is omitted rather than rendered as an arbitrary cap.
    /// </summary>
    int? EffectiveMaxLength => MaxLength ?? _attributes.MaxTextLength();

    /// <summary> Shows a character-count indicator (via <see cref="EditInputShell"/>'s <see cref="EditInputShell.CountBelow"/> layout -- below the editor, right-aligned, unlike EditString's inline count): <c>"{length}"</c> alone, or <c>"{length} / {EffectiveMaxLength}"</c> once <see cref="EffectiveMaxLength"/> is set (AntD's format).</summary>
    [Parameter] public bool ShowCount { get; set; }

    /// <summary>
    /// Grows/shrinks the textarea to fit its content as the user types (JS -- <c>edit-controls.js</c>'s
    /// <c>autoSizeTextArea</c>, invoked via <see cref="JsInteropEc.AutoSizeTextArea"/>), clamped
    /// between <see cref="ResolvedMinRows"/> (defaults to <see cref="ResolvedRows"/> when unset) and
    /// <see cref="ResolvedMaxRows"/> (unbounded when null). Degrades gracefully to the fixed
    /// <see cref="ResolvedRows"/> height with no JS available (prerender / tests). Also disables the
    /// browser's manual resize handle (<c>edit-textarea-autosize</c>), matching AntD's own TextArea
    /// autoSize behavior. Keeps growing on every keystroke even when <see cref="UpdateOn"/> resolves to
    /// <see cref="UpdateTrigger.Change"/> (commit-on-blur) -- see <see cref="AutoSizeInputAttribute"/>.
    /// Falls back to the bound property's <c>[Rows]</c> (its <c>AutoSize</c> value) when unset, then to
    /// false -- see <see cref="ResolvedAutoSize"/>.
    /// </summary>
    [Parameter] public bool? AutoSize { get; set; }

    /// <summary>
    /// AutoSize's minimum height, in text rows. Falls back to the bound property's <c>[Rows]</c> (its
    /// <c>MinRows</c> value, 0 meaning unset), then to <see cref="EffectiveRows"/> -- see
    /// <see cref="ResolvedMinRows"/>. Inert (no effect) while <see cref="ResolvedAutoSize"/> is false.
    /// </summary>
    [Parameter] public int? MinRows { get; set; }

    /// <summary>
    /// AutoSize's maximum height, in text rows. Falls back to the bound property's <c>[Rows]</c> (its
    /// <c>MaxRows</c> value, 0 meaning unset) when unset -- see <see cref="ResolvedMaxRows"/>. Null
    /// means unbounded -- the textarea keeps growing with its content. Inert (no effect) while
    /// <see cref="ResolvedAutoSize"/> is false.
    /// </summary>
    [Parameter] public int? MaxRows { get; set; }

    /// <summary>
    /// The <see cref="Rows"/> parameter resolved against the model's <c>[Rows]</c> attribute: the
    /// parameter, else the attribute's <c>Rows</c> value (0 treated as unset -- a zero-row textarea
    /// isn't meaningful and the attribute can't hold a nullable int), else 2 (the control's old
    /// default). Feeds <see cref="EffectiveRows"/> (the actually-rendered initial height) and
    /// <see cref="AutoSizeAsync"/>'s floor.
    /// </summary>
    int ResolvedRows => Rows ?? NonZero(_attributes.Rows()?.Rows) ?? 2;

    /// <summary>
    /// The <see cref="MinRows"/> parameter resolved against the model's <c>[Rows]</c> attribute: the
    /// parameter, else the attribute's <c>MinRows</c> value (0 treated as unset), else null --
    /// <see cref="EffectiveRows"/> and <see cref="AutoSizeAsync"/> supply the further fallback to
    /// <see cref="ResolvedRows"/>.
    /// </summary>
    int? ResolvedMinRows => MinRows ?? NonZero(_attributes.Rows()?.MinRows);

    /// <summary>
    /// The <see cref="MaxRows"/> parameter resolved against the model's <c>[Rows]</c> attribute: the
    /// parameter, else the attribute's <c>MaxRows</c> value (0 treated as unset), else null (unbounded).
    /// </summary>
    int? ResolvedMaxRows => MaxRows ?? NonZero(_attributes.Rows()?.MaxRows);

    /// <summary>
    /// The <see cref="AutoSize"/> parameter resolved against the model's <c>[Rows]</c> attribute: the
    /// parameter, else the attribute's <c>AutoSize</c> value, else false. <c>[Rows(AutoSize = false)]</c>
    /// is indistinguishable from unset, which is harmless -- false already is the control default, and
    /// an explicit <c>AutoSize="false"</c> parameter still overrides a true from the attribute.
    /// </summary>
    bool ResolvedAutoSize => AutoSize ?? _attributes.Rows()?.AutoSize ?? false;

    // RowsAttribute uses 0 (not null) as its "unset" sentinel for the numeric properties -- an
    // attribute can't hold a nullable int -- so every fallback through it must convert 0 back to null.
    static int? NonZero(int? value) => value is null or 0 ? null : value;

    /// <summary>
    /// Visual size, shared with the <c>Select</c> family's <see cref="SelectSize"/> (Default/Small/
    /// Large). Adds <c>edit-input-sm</c>/<c>edit-input-lg</c> to the textarea's class in both legacy
    /// and affix mode, and to the shell's affix wrapper in affix mode (via
    /// <see cref="EditInputShell.WrapperClass"/>). Only padding/font change for a textarea -- height
    /// is never locked (<see cref="Rows"/>/<see cref="AutoSize"/> still govern it). Unthemed these are
    /// inert hooks -- the opt-in <c>.edit-theme</c> section is what actually sizes them.
    /// <see cref="SelectSize.Default"/> adds no class (byte-identical legacy DOM).
    /// </summary>
    [Parameter] public SelectSize Size { get; set; }

    /// <summary>
    /// Which DOM event commits keystrokes to <see cref="InputBase{TValue}.CurrentValue"/> --
    /// <see cref="UpdateTrigger.Input"/> (<c>oninput</c>) commits on every keystroke,
    /// <see cref="UpdateTrigger.Change"/> (<c>onchange</c>) commits on blur/Enter. Resolution order:
    /// this parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>, then this
    /// control's own default of <see cref="UpdateTrigger.Input"/>. See <see cref="AutoSize"/> for how
    /// this interacts with auto-resizing.
    /// </summary>
    [Parameter] public UpdateTrigger? UpdateOn { get; set; }

    /// <summary> The resolved DOM event name ("oninput" or "onchange") driving <c>@bind-value:event</c>, per <see cref="UpdateOn"/>'s resolution order.</summary>
    protected string UpdateEventName => ResolveUpdateEvent(UpdateOn, UpdateTrigger.Input);

    [Inject] IJSRuntime JS { get; set; } = default!;

    // Captures the <textarea> so Clear() can refocus it directly -- same reasoning as
    // EditString._inputRef (the element never unmounts here, so a plain ElementReference.FocusAsync
    // is enough; no JsInteropEc by-id fallback needed for focus).
    ElementReference _textAreaRef;

    /// <summary>
    /// Whether the shell's clear button should render right now: <see cref="AllowClear"/> is set,
    /// the control isn't disabled, and the current value is non-empty.
    /// </summary>
    bool IsClearable => AllowClear && !IsDisabled && !string.IsNullOrEmpty(CurrentValue);

    /// <summary>
    /// The shell's character-count text when <see cref="ShowCount"/> is set, else null (no count
    /// renders). AntD format: <c>"{length}"</c> alone, or <c>"{length} / {EffectiveMaxLength}"</c> once
    /// <see cref="EffectiveMaxLength"/> is set. Length counts <see cref="InputBase{TValue}.CurrentValue"/>,
    /// treating null as zero.
    /// </summary>
    string? CountText => EditInputShell.BuildCountText(ShowCount, CurrentValue?.Length ?? 0, EffectiveMaxLength);

    /// <summary>
    /// True once <see cref="AllowClear"/> or <see cref="ShowCount"/> is in use -- the single
    /// computation site <see cref="EditInputShell.UsesAffixLayout"/> defines, so this control and the
    /// shell always agree on which layout renders. EditTextArea never sets Prefix/Suffix/IsPassword,
    /// so those arguments are always null/false here.
    /// </summary>
    bool UseAffixLayout => EditInputShell.UsesAffixLayout(null, null, AllowClear, CountText, false);

    /// <summary>
    /// The textarea's <c>class</c> attribute. Legacy mode (no affix params, no AutoSize) reproduces
    /// today's exact string, so a no-new-params render stays byte-identical; affix mode adds
    /// <c>edit-affix-input</c> per <see cref="EditInputShell"/>'s contract, and <see cref="ResolvedAutoSize"/>
    /// adds <c>edit-textarea-autosize</c> (disables the native resize handle).
    /// </summary>
    string InputClass
    {
        get
        {
            var classes = "edit-input edit-textarea-input";
            if (UseAffixLayout) classes += " edit-affix-input";
            if (ResolvedAutoSize) classes += " edit-textarea-autosize";
            return EditInputShell.BuildInputClass(classes, Size, CssClass);
        }
    }

    /// <summary>
    /// The initial <c>rows</c> attribute: <see cref="ResolvedMinRows"/> (falling back to
    /// <see cref="ResolvedRows"/>) while <see cref="ResolvedAutoSize"/> is true, so first paint already
    /// matches the height JS then maintains; plain <see cref="ResolvedRows"/> otherwise.
    /// </summary>
    int EffectiveRows => ResolvedAutoSize ? ResolvedMinRows ?? ResolvedRows : ResolvedRows;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditTextArea)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    // Trivial parser — same as Microsoft's InputTextArea: pass the string through.
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
    /// which raises <c>ValueChanged</c>/<c>NotifyFieldChanged</c> itself) and refocuses the textarea.
    /// Focus is best-effort -- see <see cref="_textAreaRef"/>'s remarks. Clearing bypasses the bound
    /// input event entirely, so <see cref="OnValueChangedAsync"/> never fires for it -- re-measure
    /// explicitly here when <see cref="ResolvedAutoSize"/> is on.
    /// </summary>
    async Task Clear()
    {
        CurrentValue = null;
        try { await _textAreaRef.FocusAsync(); }
        catch { /* not focusable yet (prerender/tests) */ }
        if (ResolvedAutoSize) await AutoSizeAsync();
    }

    /// <summary>
    /// Runs after every bound-value update (<c>@bind-value:after</c>) -- re-measures and resizes when
    /// <see cref="ResolvedAutoSize"/> is on, a no-op otherwise. Wired unconditionally (rather than only
    /// while AutoSize is true): unlike an explicit <c>@oninput</c> handler, <c>:after</c> never renders
    /// as a DOM attribute of its own, so attaching it doesn't touch the non-AutoSize markup at all (the
    /// S1 DOM-stability tests still pass unchanged). It fires once per *bound* event, which is
    /// <c>oninput</c> by default but becomes <c>onchange</c> (blur/Enter only) when
    /// <see cref="UpdateEventName"/> resolves to Change -- so under AutoSize + Change this alone would
    /// stop the textarea growing mid-typing; see <see cref="AutoSizeInputAttribute"/> for the patch.
    /// </summary>
    Task OnValueChangedAsync() => ResolvedAutoSize ? AutoSizeAsync() : Task.CompletedTask;

    /// <summary>
    /// Measure-only <c>oninput</c> handler, splatted onto the textarea ONLY when
    /// <see cref="ResolvedAutoSize"/> is on and <see cref="UpdateEventName"/> has resolved away from
    /// <c>"oninput"</c> (i.e. the bound commit event is <c>onchange</c>). <see cref="OnValueChangedAsync"/> re-measures via
    /// <c>@bind-value:after</c>, which only fires once per bound event -- under
    /// <see cref="UpdateTrigger.Change"/> that's blur, so without this extra handler an AutoSize
    /// textarea would stop growing as the user types. Null in every other combination (including the
    /// default oninput binding), so the splat renders no attribute at all and the S1 DOM-stability
    /// tests' byte-identical markup holds. No key collision by construction: this dictionary only
    /// ever adds "oninput" in the branch where the bound event is "onchange".
    /// </summary>
    IReadOnlyDictionary<string, object>? AutoSizeInputAttribute =>
        ResolvedAutoSize && UpdateEventName != "oninput"
            ? new Dictionary<string, object>(1) { ["oninput"] = EventCallback.Factory.Create(this, AutoSizeAsync) }
            : null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && ResolvedAutoSize) await AutoSizeAsync();
    }

    Task AutoSizeAsync() => JsInteropEc.AutoSizeTextArea(JS, _id, ResolvedMinRows ?? ResolvedRows, ResolvedMaxRows, FormDefaults);
}
