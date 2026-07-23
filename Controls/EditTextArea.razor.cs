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

    /// <summary> Placeholder text to display in the textarea when empty.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary> Number of visible text rows in the textarea. Defaults to 2. Ignored for the initial
    /// height while <see cref="AutoSize"/> is true -- see <see cref="MinRows"/>.</summary>
    [Parameter] public int Rows { get; set; } = 2;

    /// <summary> Shows a clear button (via <see cref="EditInputShell"/>) while the value is non-empty and the control is enabled. Clicking it sets the value to null and refocuses the textarea.</summary>
    [Parameter] public bool AllowClear { get; set; }

    /// <summary> Maximum number of characters, rendered as the textarea's <c>maxlength</c> attribute. Omitted (no browser-side cap) when null.</summary>
    [Parameter] public int? MaxLength { get; set; }

    /// <summary> Shows a character-count indicator (via <see cref="EditInputShell"/>'s <see cref="EditInputShell.CountBelow"/> layout -- below the editor, right-aligned, unlike EditString's inline count): <c>"{length}"</c> alone, or <c>"{length} / {MaxLength}"</c> once <see cref="MaxLength"/> is set (AntD's format).</summary>
    [Parameter] public bool ShowCount { get; set; }

    /// <summary>
    /// Grows/shrinks the textarea to fit its content as the user types (JS -- <c>edit-controls.js</c>'s
    /// <c>autoSizeTextArea</c>, invoked via <see cref="JsInteropEc.AutoSizeTextArea"/>), clamped
    /// between <see cref="MinRows"/> (defaults to <see cref="Rows"/> when unset) and
    /// <see cref="MaxRows"/> (unbounded when null). Degrades gracefully to the fixed <see cref="Rows"/>
    /// height with no JS available (prerender / tests). Also disables the browser's manual resize
    /// handle (<c>edit-textarea-autosize</c>), matching AntD's own TextArea autoSize behavior.
    /// </summary>
    [Parameter] public bool AutoSize { get; set; }

    /// <summary> AutoSize's minimum height, in text rows. Defaults to <see cref="Rows"/> when unset. Inert (no effect) while <see cref="AutoSize"/> is false.</summary>
    [Parameter] public int? MinRows { get; set; }

    /// <summary> AutoSize's maximum height, in text rows. Null means unbounded -- the textarea keeps growing with its content. Inert (no effect) while <see cref="AutoSize"/> is false.</summary>
    [Parameter] public int? MaxRows { get; set; }

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
    /// renders). AntD format: <c>"{length}"</c> alone, or <c>"{length} / {MaxLength}"</c> once
    /// <see cref="MaxLength"/> is set. Length counts <see cref="InputBase{TValue}.CurrentValue"/>,
    /// treating null as zero.
    /// </summary>
    string? CountText => !ShowCount
        ? null
        : MaxLength is null
            ? $"{CurrentValue?.Length ?? 0}"
            : $"{CurrentValue?.Length ?? 0} / {MaxLength}";

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
    /// <c>edit-affix-input</c> per <see cref="EditInputShell"/>'s contract, and <see cref="AutoSize"/>
    /// adds <c>edit-textarea-autosize</c> (disables the native resize handle).
    /// </summary>
    string InputClass
    {
        get
        {
            var classes = "edit-input edit-textarea-input";
            if (UseAffixLayout) classes += " edit-affix-input";
            if (AutoSize) classes += " edit-textarea-autosize";
            return $"{classes} {CssClass}";
        }
    }

    /// <summary>
    /// The initial <c>rows</c> attribute: <see cref="MinRows"/> (falling back to <see cref="Rows"/>)
    /// while <see cref="AutoSize"/> is true, so first paint already matches the height JS then
    /// maintains; plain <see cref="Rows"/> otherwise.
    /// </summary>
    int EffectiveRows => AutoSize ? MinRows ?? Rows : Rows;

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
    /// explicitly here when AutoSize is on.
    /// </summary>
    async Task Clear()
    {
        CurrentValue = null;
        try { await _textAreaRef.FocusAsync(); }
        catch { /* not focusable yet (prerender/tests) */ }
        if (AutoSize) await AutoSizeAsync();
    }

    /// <summary>
    /// Runs after every bound-value update (<c>@bind-value:after</c>, fired once per input event) --
    /// re-measures and resizes when <see cref="AutoSize"/> is on, a no-op otherwise. Wired
    /// unconditionally (rather than only while AutoSize is true): unlike an explicit <c>@oninput</c>
    /// handler, <c>:after</c> never renders as a DOM attribute of its own -- it's pure C#-side wiring
    /// around the same "oninput" binding EditTextArea already had, so attaching it doesn't touch the
    /// non-AutoSize markup at all (the S1 DOM-stability tests still pass unchanged).
    /// </summary>
    Task OnValueChangedAsync() => AutoSize ? AutoSizeAsync() : Task.CompletedTask;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && AutoSize) await AutoSizeAsync();
    }

    Task AutoSizeAsync() => JsInteropEc.AutoSizeTextArea(JS, _id, MinRows ?? Rows, MaxRows, FormDefaults);
}
