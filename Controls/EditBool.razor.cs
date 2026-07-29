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

    /// <summary>
    /// Text shown by the read-only view when the value is true. Falls back to the bound property's
    /// <c>[BoolText]</c> when unset -- see <see cref="EffectiveTrueText"/>. Defaults to "Yes".
    /// </summary>
    [Parameter] public string? TrueText { get; set; }

    /// <summary>
    /// Text shown by the read-only view when the value is false. Falls back to the bound property's
    /// <c>[BoolText]</c> when unset -- see <see cref="EffectiveFalseText"/>. Defaults to "No".
    /// </summary>
    [Parameter] public string? FalseText { get; set; }

    /// <summary>
    /// The text actually rendered when the value is true: the <see cref="TrueText"/> parameter, else
    /// the model property's <c>[BoolText(TrueText = …)]</c>, else <c>"Yes"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveTrueText => TrueText ?? _attributes.BoolText()?.TrueText ?? "Yes";

    /// <summary>
    /// The text actually rendered when the value is false: the <see cref="FalseText"/> parameter, else
    /// the model property's <c>[BoolText(FalseText = …)]</c>, else <c>"No"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveFalseText => FalseText ?? _attributes.BoolText()?.FalseText ?? "No";

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

    // JsModule owns the once-only import, the dispose-raced-the-import guard, and the no-JS degrade
    // (a null return, which reads the same as the import throwing did).
    readonly JsModule _jsModule = new("wss-checkbox.js");
    // false (not null) is the "nothing applied yet" baseline -- a freshly-mounted native checkbox is
    // never indeterminate, so the overwhelmingly common case (Indeterminate left at its false default)
    // never pays a JS round-trip at all. Table's identical mirror starts at null instead because its
    // header checkbox is rare and always wants an explicit first sync; EditBool renders on every plain
    // checkbox in every form, so skipping the no-op call here matters far more than there.
    bool _lastIndeterminate;
    bool? _lastUseStyledCheckbox;
    // Tracks ShouldHideLabel for the same reason as _lastUseStyledCheckbox: FormLabel renders the
    // NestedInput fragment from two structurally different branches depending on this (nested inside
    // the visible <label> vs. a sibling of the visually-hidden one), so a runtime flip remounts a
    // fresh <input> (indeterminate == false) even though the fragment reference didn't change.
    bool? _lastShouldHideLabel;
    bool _disposed;

    // Checkboxes don't bind via string parsing — the value is set directly through CurrentValue
    // by HandleCheckboxChange below. This matches Microsoft's InputCheckbox behavior.
    protected override bool TryParseValueFromString(string? value, out bool result, out string validationErrorMessage)
        => throw new NotSupportedException(
            $"This component does not parse string inputs. Bind to the '{nameof(CurrentValue)}' property, not '{nameof(CurrentValueAsString)}'.");

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
    // @if in EditBool.razor that gates the FormLabel carrying CheckboxFragment/StyledCheckboxFragment.
    bool CheckboxRendered => ShouldShowComponent() && (ShowEditor || RenderAsCheckboxWhenReadOnly);

    // indeterminate is a DOM property with no HTML attribute, so it can only be set from JS. Runs
    // after a render only when the mixed state actually changed (skipping a JS round-trip per
    // render), and re-applies whenever the checkbox itself was just (re)created — either because it
    // wasn't rendered at all last pass (ShouldShowComponent/ShowEditor toggled), because the
    // styled/unstyled fragment swapped, or because ShouldHideLabel flipped (FormLabel renders the
    // same NestedInput fragment from a structurally different branch in that case) — a fresh <input>
    // comes back with indeterminate == false in all three cases. Degrades to a plain checkbox with
    // no JS runtime (server prerender, tests) — mirrors Table.OnAfterRenderAsync's identical
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
        // Null = no JS runtime / module (server prerender, tests), or disposed while the import was in
        // flight (the holder cleaned up its own late-arriving reference). Either way the mirror stays
        // unset, so a later render retries, and the checkbox just shows checked/unchecked.
        var module = await _jsModule.GetAsync(JS, FormDefaults);
        if (module is null) return;
        try
        {
            await module.InvokeVoidAsync("setIndeterminate", _id, Indeterminate);
            _lastIndeterminate = Indeterminate;
        }
        catch
        {
            // Element gone / circuit dropped mid-call — same fallback, same retry.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        // Blazor treats IAsyncDisposable and IDisposable as mutually exclusive: when a component
        // implements IAsyncDisposable the renderer awaits DisposeAsync and never calls
        // IDisposable.Dispose. So InputBase's explicitly-implemented IDisposable.Dispose — which
        // unsubscribes its EditContext.OnValidationStateChanged handler and calls the Dispose(bool)
        // override that drops this control's field registration (see EditControlBase.Dispose) — only
        // runs if this method invokes it. Every other scalar control gets it for free; EditBool needs
        // the JS-module cleanup below, so it has to chain the synchronous half by hand. The cast is
        // required: InputBase implements the interface explicitly (`void IDisposable.Dispose()`), so
        // there is no public Dispose() to call.
        ((IDisposable)this).Dispose();
        await _jsModule.DisposeAsync();
    }
}
