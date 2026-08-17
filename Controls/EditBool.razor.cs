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
    /// <remarks>
    /// Implemented by withholding the native <c>disabled</c> attribute (the click is suppressed with
    /// <c>@onclick:preventDefault</c> instead), so the checkbox stays a real tab stop while disabled —
    /// the discoverable-but-inoperable pattern. <see cref="CanFocusWhenDisabled"/> below carries that
    /// same opt-in over to <see cref="EditControlBase{TValue}.FocusAsync"/>.
    /// </remarks>
    [Parameter] public bool AllowFocusWhenDisabled { get; set; } = true;

    /// <inheritdoc/>
    /// <remarks>
    /// The library's one exception, and only because <see cref="AllowFocusWhenDisabled"/> already made
    /// it one: a disabled <c>EditBool</c> is still in the Tab order by default, so a consumer's
    /// <c>FocusAsync()</c> is putting focus somewhere the user could have reached anyway. Set
    /// <c>AllowFocusWhenDisabled="false"</c> and it behaves like every other disabled control — natively
    /// disabled, out of the Tab order, and not programmatically focusable either.
    /// </remarks>
    protected override bool CanFocusWhenDisabled => AllowFocusWhenDisabled;

    /// <summary>
    /// The checkbox's own STATE attributes -- <c>aria-disabled</c>, <c>disabled</c>,
    /// <c>aria-required</c>, <c>aria-invalid</c>, <c>aria-errormessage</c> -- folded into the merged
    /// <c>@attributes</c> splat (<see cref="EditorAttributes"/>) rather than written as their own
    /// explicit attributes beside it. Null when this control has no opinion on any of them, so nothing
    /// is emitted and the markup stays byte-identical to writing no attributes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Not a reuse of the base <see cref="EditControlBase{TValue}.EditorStateAttributes"/> — this
    /// control's own <c>disabled</c>/<c>aria-disabled</c> pair has a different shape than every other
    /// control's, because of <see cref="AllowFocusWhenDisabled"/> (default true, see its own remarks):
    /// it withholds the native <c>disabled</c> attribute while the checkbox is non-operable (read-only
    /// or <see cref="EditControlBase{TValue}.IsDisabled"/>) so the checkbox stays a real Tab stop (the
    /// click is suppressed with <c>@onclick:preventDefault</c> instead) — the discoverable-but-
    /// inoperable pattern. <c>aria-disabled="true"</c> is announced whenever the checkbox isn't
    /// operable at all; the native <c>disabled</c> only joins that when
    /// <see cref="AllowFocusWhenDisabled"/> is turned off, matching every other control's fully-native
    /// disabled behavior.
    /// </para>
    /// <para>
    /// Written as an explicit frame beside the splat (the control's original shape), a null/false value
    /// here would DELETE a consumer's splatted same-named attribute outright rather than decline to
    /// override it — see <see cref="EditControlBase{TValue}.EditorStateAttributes"/>'s remarks for the
    /// mechanism (an explicit attribute frame written after a splat wins even when its own value is
    /// null, because <c>RenderTreeBuilder</c> still calls <c>TrackAttributeName</c> for it).
    /// </para>
    /// <para>
    /// <b>Whenever the checkbox is non-operable (<c>ariaDisabled</c> below), <c>disabled</c> is written
    /// EXPLICITLY as a dictionary entry — <c>true</c> or <c>false</c> — never left absent.</b> This is
    /// the one place this control deliberately differs from "omit the key when the value would be
    /// false": <see cref="AllowFocusWhenDisabled"/>'s whole contract is that the checkbox stays a real
    /// Tab stop while non-operable, which requires the native <c>disabled</c> attribute to be reliably
    /// ABSENT in that state — and merely omitting the dictionary key would let a consumer's own splatted
    /// <c>disabled="disabled"</c> quietly defeat that guarantee (the exact silent-erasure failure mode
    /// this whole fix exists to close, just pointed the other direction). Storing an explicit
    /// <c>false</c> here is safe and ordinary Blazor attribute rendering — a boolean <c>false</c> value
    /// always omits the attribute, dictionary-sourced or not — and it deterministically overrides the
    /// consumer's own entry the same way <c>true</c> does, because the merge that layers this
    /// dictionary over the consumer's happens in plain C# (last write wins) before any
    /// <c>RenderTreeBuilder</c> call runs; there is only ever one render-tree frame for the name, so
    /// none of the duplicate-frame erasure this file is otherwise about can apply here. Only while the
    /// checkbox is fully operable (<c>ariaDisabled</c> false) does this control have no opinion on
    /// <c>disabled</c>/<c>aria-disabled</c> at all, and the keys are omitted so the consumer's own
    /// splatted values survive untouched — the common case, and the one the erasure bug used to break.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, object>? CheckboxStateAttributes
    {
        get
        {
            var isInvalid = IsInvalid;
            var ariaDisabled = !ShowEditor || IsDisabled;
            if (!ariaDisabled && _isRequired is null && !isInvalid) return null;

            var state = new Dictionary<string, object>(5);
            if (ariaDisabled)
            {
                state["aria-disabled"] = "true";
                // Always written (true or false) while non-operable -- see the remarks above.
                state["disabled"] = !AllowFocusWhenDisabled;
            }
            if (_isRequired is { } required) state["aria-required"] = required;
            if (isInvalid)
            {
                state["aria-invalid"] = "true";
                state["aria-errormessage"] = _errorMsgId;
            }
            return state;
        }
    }

    /// <summary>
    /// The checkbox's full <c>@attributes</c> splat: the consumer's unmatched attributes with
    /// <see cref="CheckboxStateAttributes"/> layered on top (the control's own wins on collision, and
    /// contributes nothing when it has no opinion — see that property's remarks).
    /// </summary>
    IReadOnlyDictionary<string, object>? EditorAttributes =>
        AttributeSplat.RestWith(AdditionalAttributes, CheckboxStateAttributes);

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
    string EffectiveTrueText => _attributes.BoolText(TrueText, static a => a.TrueText, "Yes");

    /// <summary>
    /// The text actually rendered when the value is false: the <see cref="FalseText"/> parameter, else
    /// the model property's <c>[BoolText(FalseText = …)]</c>, else <c>"No"</c> -- the control's
    /// built-in default.
    /// </summary>
    string EffectiveFalseText => _attributes.BoolText(FalseText, static a => a.FalseText, "No");

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

        // The browser's pre-click activation steps set the DOM `indeterminate` property to false the
        // moment the user clicks -- but Indeterminate (the parameter) doesn't necessarily change, so
        // without this the mirror still says "true" while the DOM now says false, and
        // `if (_lastIndeterminate == Indeterminate) return;` in OnAfterRenderAsync blocks
        // re-application forever (a parent re-asserting Indeterminate=true after this click would be
        // silently ignored). Reset the mirror here so the next render notices the mismatch and
        // reapplies via JS.
        _lastIndeterminate = false;
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
