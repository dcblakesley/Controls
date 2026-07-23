namespace Controls;

/// <summary> Edit control for boolean values, displays as a checkbox.</summary>
public partial class EditBool : EditControlBase<bool>
{
    // Component-specific parameters

    /// <summary>
    /// Obsolete compile-time guard: no longer used — <c>@bind-Value</c> alone supplies the accessor
    /// this used to require. This inert stub exists only so a leftover <c>Field="..."</c> attribute
    /// is a compile error instead of silently building and throwing at runtime. Remove the attribute
    /// from your markup.
    /// </summary>
    [Obsolete("Field is no longer used -- @bind-Value alone is sufficient. Remove this attribute.", error: true)]
    [Parameter] public Expression<Func<bool>>? Field { get; set; }

    /// <summary> When true, allows the checkbox to receive focus even when disabled. Defaults to true.</summary>
    [Parameter] public bool AllowFocusWhenDisabled { get; set; } = true;

    /// <summary> Text shown by the read-only view when the value is true. Defaults to "Yes". </summary>
    [Parameter] public string TrueText { get; set; } = "Yes";

    /// <summary> Text shown by the read-only view when the value is false. Defaults to "No". </summary>
    [Parameter] public string FalseText { get; set; } = "No";

    /// <summary>
    /// When true, falls back to the legacy behavior of rendering a disabled checkbox in read-only mode.
    /// Defaults to false — read-only mode now uses <see cref="ReadOnlyValue"/> with <see cref="TrueText"/>/<see cref="FalseText"/> like the other controls.
    /// </summary>
    [Parameter] public bool RenderAsCheckboxWhenReadOnly { get; set; }

    /// <summary>
    /// When true, renders the checkbox with a custom-drawn box (hidden native input + a sibling
    /// element that draws the visual state) instead of the bare native checkbox. Use this when you
    /// need <c>border-radius</c> or other shape styling that a native checkbox + accent-color cannot
    /// render in any current browser. Null (default) falls through to <see cref="FormOptions"/>, then
    /// any enclosing <see cref="Controls.FormDefaults"/>, then <see cref="FormOptions.DefaultUseStyledCheckbox"/>
    /// — see <see cref="Controls.FormDefaults"/> to set this once for a whole app or MFE.
    /// </summary>
    [Parameter] public bool? UseStyledCheckbox { get; set; }

    /// <summary> <see cref="UseStyledCheckbox"/> resolved through the FormOptions/FormDefaults/static chain. </summary>
    bool EffectiveUseStyledCheckbox => EditControlInit.UseStyledCheckbox(UseStyledCheckbox, FormOptions, FormDefaults);

    /// <summary>
    /// When true, the checkbox renders in the native "mixed" visual state (a dash/square instead of
    /// checked or empty) — AntD's <c>indeterminate</c>, used for a "select all" checkbox whose
    /// children are partially selected. Visual only, per AntD semantics: it does not change
    /// <see cref="InputBase{TValue}.CurrentValue"/> or what a click toggles it to. Applied to the DOM
    /// via JS after render (there is no HTML attribute for it) — see <see cref="OnAfterRenderAsync"/>;
    /// degrades to a plain checked/unchecked box with no JS runtime (server prerender, tests). Mirrors
    /// the UI-kit <c>Table</c>'s header "select all" checkbox, which shares the same JS helper.
    /// </summary>
    [Parameter] public bool Indeterminate { get; set; }

    [Inject] IJSRuntime JS { get; set; } = default!;

    string _displayLabel = string.Empty;
    string? _displayDescription;

    IJSObjectReference? _jsModule;
    // false (not null) is the "nothing applied yet" baseline -- a freshly-mounted native checkbox is
    // never indeterminate, so the overwhelmingly common case (Indeterminate left at its false default)
    // never pays a JS round-trip at all. Table's identical mirror starts at null instead because its
    // header checkbox is rare and always wants an explicit first sync; EditBool renders on every plain
    // checkbox in every form, so skipping the no-op call here matters far more than there.
    bool _lastIndeterminate;
    bool? _lastUseStyledCheckbox;
    // Tracks ShouldHideLabel for the same reason as _lastUseStyledCheckbox: EditBool.razor renders
    // the checkbox fragment from two structurally different branches depending on this (wrapped
    // inside the visible <label> vs. a sibling of a visually-hidden one), so a runtime flip remounts
    // a fresh <input> (indeterminate == false) even though the fragment reference didn't change.
    bool? _lastShouldHideLabel;
    bool _disposed;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(ValueExpression ?? throw new InvalidOperationException(
            $"{nameof(EditBool)} requires a two-way @bind-Value binding (which supplies {nameof(ValueExpression)})."));
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Resolved per parameter change (not per render — DisplayDescription used to be evaluated
        // twice each render) so a dynamic Label (localization switch, runtime text) is reflected;
        // resolving only at init froze the first value forever. No-op until InitState has run.
        if (_attributes is not null)
        {
            _displayLabel = Label ?? _attributes.GetLabelText(FieldIdentifier);
            _displayDescription = Description ?? _attributes.Description();
        }
    }

    // Checkboxes don't bind via string parsing — the value is set directly through CurrentValue
    // by HandleCheckboxChange below. This matches Microsoft's InputCheckbox behavior.
    protected override bool TryParseValueFromString(string? value, out bool result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"This component does not parse string inputs. Bind to the '{nameof(CurrentValue)}' property, not '{nameof(CurrentValueAsString)}'.");

    string DisplayLabel() => _displayLabel;
    string? DisplayDescription() => _displayDescription;

    // bool default is false. The base ShouldShowComponent already knows CurrentValue is non-null
    // here (bool is a value type), so this override only needs to flag "false == default".
    // Note: this fixes a pre-existing bug in WhenReadOnlyAndNullOrDefault where the old logic
    // (`!IsEditMode && CurrentValue`) showed only when read-only AND true — the centralized
    // behavior now correctly shows except when read-only AND default-false.
    protected override bool IsValueDefault() => !CurrentValue;

    void HandleCheckboxChange(ChangeEventArgs args)
    {
        // Only update the value if the checkbox is not disabled
        if (ShowEditor && !IsDisabled)
            CurrentValue = (bool)args.Value!;
    }

    // True while an actual <input type="checkbox"> is in the DOM (either fragment) — mirrors the
    // @if conditions in EditBool.razor that gate CheckboxFragment/StyledCheckboxFragment.
    bool CheckboxRendered => ShouldShowComponent() && (ShowEditor || RenderAsCheckboxWhenReadOnly);

    // indeterminate is a DOM property with no HTML attribute, so it can only be set from JS. Runs
    // after a render only when the mixed state actually changed (skipping a JS round-trip per
    // render), and re-applies whenever the checkbox itself was just (re)created — either because it
    // wasn't rendered at all last pass (ShouldShowComponent/ShowEditor toggled), because the
    // styled/unstyled fragment swapped, or because ShouldHideLabel flipped (EditBool.razor renders
    // the same fragment from a structurally different branch in that case) — a fresh <input> comes
    // back with indeterminate == false in all three cases. Degrades to a plain checkbox with no JS
    // runtime (server prerender, tests) — mirrors Table.OnAfterRenderAsync's identical
    // mirror-and-best-effort pattern.
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);
        if (_disposed) return;
        if (!CheckboxRendered)
        {
            // No checkbox in the DOM right now -- forget the mirror so a later reappearance (a fresh
            // native element, indeterminate == false by default) re-syncs instead of skipping.
            _lastIndeterminate = false;
            return;
        }

        var useStyledCheckbox = EffectiveUseStyledCheckbox;
        if (useStyledCheckbox != _lastUseStyledCheckbox)
        {
            // The styled/unstyled fragments render different <input> elements -- a runtime switch
            // tears down the old one and mounts a fresh one (indeterminate == false), same reasoning.
            _lastIndeterminate = false;
            _lastUseStyledCheckbox = useStyledCheckbox;
        }

        var shouldHideLabel = ShouldHideLabel;
        if (shouldHideLabel != _lastShouldHideLabel)
        {
            // Same remount reasoning as above, triggered by the label-hiding branch instead of the
            // styled/unstyled one -- either one recreates the <input>, so either one must force a
            // re-sync.
            _lastIndeterminate = false;
            _lastShouldHideLabel = shouldHideLabel;
        }

        if (_lastIndeterminate == Indeterminate) return;
        try
        {
            _jsModule ??= await JS.InvokeAsync<IJSObjectReference>(
                "import", JsModuleUrl.Resolve(FormDefaults, "wss-checkbox.js"));
            if (_disposed)
            {
                // Disposed while the import was in flight — DisposeAsync already ran with a null
                // module, so this reference is ours to clean up.
                try { await _jsModule.DisposeAsync(); } catch { }
                _jsModule = null;
                return;
            }
            await _jsModule.InvokeVoidAsync("setIndeterminate", _id, Indeterminate);
            _lastIndeterminate = Indeterminate;
        }
        catch
        {
            // No JS runtime / module — the checkbox just shows checked/unchecked.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.DisposeAsync();
            }
            catch
            {
                // Circuit may already be gone; nothing to clean up.
            }
            _jsModule = null;
        }
    }
}
