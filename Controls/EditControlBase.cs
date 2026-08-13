using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// Base class for edit controls that bind to a single value. Hoists the <see cref="IEditControl"/>
/// parameters, the <see cref="FormOptions"/> / <see cref="FormGroupOptions"/> cascading parameters,
/// and the standard derived state (<c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>,
/// <c>_fieldIdentifier</c>) so derived controls only declare component-specific parameters and markup.
/// </summary>
/// <remarks>
/// <para>
/// Derived classes must implement <see cref="InputBase{TValue}.TryParseValueFromString"/> — replacing
/// the parsing that Microsoft's <c>Input*</c> classes used to provide. A control that never parses a
/// string (e.g. <c>EditBool</c>, which assigns <see cref="InputBase{TValue}.CurrentValue"/> directly)
/// throws <see cref="NotSupportedException"/> from it instead.
/// </para>
/// <para>
/// Initialization needs no code in the derived control at all: <c>@bind-Value</c> supplies
/// <see cref="InputBase{TValue}.ValueExpression"/>, and this class's <see cref="OnInitialized"/> feeds
/// it to <see cref="InitState"/>. Override <see cref="OnInitialized"/> only for extra init of the
/// control's own (e.g. <c>EditRadioEnum</c>'s option cache) — and then call <c>base.OnInitialized()</c>
/// first, so the extra work sees the derived state already populated. Forgetting the call is the whole
/// reason the wiring lives here: a control that skipped it rendered silently broken (empty <c>_id</c>,
/// no model attributes, no field registration) rather than failing loudly.
/// </para>
/// <para>
/// Derived controls do still each declare an inert <c>Field</c> parameter marked
/// <c>[Obsolete(error: true)]</c>. It is a compile-time guard only — nothing reads it and nothing
/// passes it to <see cref="InitState"/> — so that a leftover <c>Field="..."</c> attribute in consumer
/// markup is a build error rather than an unmatched-parameter throw at first render.
/// </para>
/// <para>
/// <b>Unmatched attributes.</b> <see cref="InputBase{TValue}"/> captures them into
/// <c>AdditionalAttributes</c>, but capturing is not rendering — every derived control's markup has to
/// place them, and the placement is uniform across the library:
/// <list type="bullet">
/// <item><c>class</c> travels the single <see cref="InputBase{TValue}.CssClass"/> channel onto the
/// field element (the editor, or the read-only value), merged with the EditContext's field-state
/// classes. It is never re-emitted anywhere else — <c>ContainerClass</c> is the wrapper's knob.</item>
/// <item><c>style</c> is hand-merged onto the root <c>.edit-control-wrapper</c> via
/// <c>AttributeSplat.MergeStyle</c> — the same element the list-bound controls put it on, so one rule
/// covers every control, and it keeps a consumer's declarations off the elements whose inline style is
/// JS-owned (<c>EditTextArea</c>'s AutoSize height, the <c>Select</c> engine's open-order z-index).
/// The two picker-backed controls (<see cref="EditDate{T}"/>, <c>EditDateRange</c>) are the exception:
/// they forward the whole splat, <c>style</c> included, to the inner picker's own
/// <c>AdditionalAttributes</c>, which is where their <c>class</c> has to go too (it carries the
/// EditContext state classes onto the picker wrapper).</item>
/// <item>Everything else (<c>inputmode</c>, <c>readonly</c>, <c>spellcheck</c>, <c>data-*</c>,
/// <c>aria-*</c>, …) is splatted with <c>AttributeSplat.Rest</c> onto the element it describes: the
/// editor element for the single-editor controls, the <c>role="radiogroup"</c> fieldset for the radio
/// groups, and (via a forwarded <c>AdditionalAttributes</c>) the engine wrapper for
/// <c>EditSelectSearch</c>. Always splatted FIRST, so every explicitly-written attribute beside it wins
/// — Blazor resolves duplicate attribute names left-to-right, last one wins.</item>
/// </list>
/// A control that renders no editor in read-only mode drops the splat with it, exactly as it already
/// drops the editor's own attributes; <c>class</c> and <c>style</c> still apply.
/// </para>
/// </remarks>
public abstract class EditControlBase<TValue> : InputBase<TValue>, IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    [CascadingParameter] public FormDefaults? FormDefaults { get; set; }

    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool? IsRequired { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Standard derived state — populated by InitState, which OnInitialized below calls.
    protected string _id = string.Empty;
    protected string? _isRequired;
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;
    // Cached ARIA references — resolved in InitState and re-resolved each OnParametersSet (see BuildDescribedBy).
    protected string _errorMsgId = string.Empty;
    protected string _describedBy = string.Empty;

    // False until InitState has run to completion — see Dispose.
    bool _stateInitialized;

    /// <summary>
    /// The control's fully-resolved required-ness (IsRequired parameter → [Required] attribute →
    /// FormOptions.RequiredResolver), recomputed alongside <c>_isRequired</c> each parameter cycle.
    /// Markup passes THIS to FormLabel's IsRequired (an explicit value wins outright there), so the
    /// star and <c>aria-required</c> share one computation site and can never disagree — FormLabel's
    /// own derivation path remains only for standalone use (e.g. EditDisplay).
    /// </summary>
    protected bool? IsRequiredResolved => _isRequired is not null;

    /// <summary>
    /// True when this field currently has a validation error. Read from the EditContext's messages
    /// rather than substring-matching <see cref="InputBase{TValue}.CssClass"/> — CssClass also
    /// contains the consumer's <c>class</c> attribute, so a class like "invalid-style-fix" was a
    /// permanent false positive (aria-invalid + red X). Guarded on a null <c>EditContext</c> because
    /// <see cref="InputBase{TValue}"/> supports standalone use (no surrounding <c>EditForm</c>) since
    /// .NET 8 — no context means no validation, so no error.
    /// </summary>
    protected bool IsInvalid => EditContext is not null && EditContext.GetValidationMessages(FieldIdentifier).Any();

    /// <summary>
    /// This control's name for diagnostics — <c>EditNumber</c>, not the CLR's <c>EditNumber`1</c>.
    /// </summary>
    /// <remarks>
    /// Not <c>virtual</c>: nothing needs a name other than its own type's, and the trimmed
    /// <c>GetType().Name</c> reproduces the per-control <c>nameof(EditNumber&lt;T&gt;)</c> the hoisted
    /// exception message below used to be written with, character for character — so there is nothing
    /// for a derived control to correct. See <see cref="EditControlInit.ControlName"/>.
    /// </remarks>
    protected string ControlName => EditControlInit.ControlName(this);

    /// <summary>
    /// Wires the standard derived state for every scalar control, so a derived control needs no
    /// <c>OnInitialized</c> of its own unless it has extra init to run (in which case it calls
    /// <c>base.OnInitialized()</c> first — see this class's remarks).
    /// </summary>
    protected override void OnInitialized()
    {
        // Chains to InputBase even though it doesn't override this today — every control's own
        // OnInitialized did, and hoisting them here must not quietly drop the call.
        base.OnInitialized();
        InitState(EditControlInit.RequireBinding(ValueExpression, this));
    }

    /// <summary>
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c> from
    /// the control's bound accessor (<see cref="InputBase{TValue}.ValueExpression"/>, which
    /// <c>@bind-Value</c> supplies), and registers the field with
    /// <see cref="FormOptions.FieldIdentifiers"/> so the validation summary can link to it.
    /// Called by <see cref="OnInitialized"/>; a derived control never calls it itself.
    /// </summary>
    /// <remarks>
    /// Registration used to live in <c>FieldValidationDisplay.OnInitialized</c>, but that
    /// component is rendered conditionally (inside <c>@if (ShouldShowComponent())</c>) — so
    /// hidden fields silently never registered, and the validation summary couldn't link to them.
    /// Registering here happens once per control init and survives any HidingMode setting; the
    /// paired unregister lives in <see cref="Dispose(bool)"/>.
    /// </remarks>
    protected void InitState(Expression<Func<TValue>> field)
    {
        // Resolve + register in one call, shared with the list base and EditRadio — the registration
        // is paired with the Dispose override below (see EditControlInit.RegisterField's remarks).
        (_id, _attributes, _fieldIdentifier) = EditControlInit.InitAndRegister(field, this, FormOptions, FormGroupOptions);
        _stateInitialized = true;
        RefreshAriaState();
    }

    /// <summary>
    /// Re-resolves the element id and the cached ARIA state on parameter change (e.g. a runtime
    /// Id/IdPrefix change, or a Description/Tooltip or label-hidden toggle) so the rendered id, the
    /// label's <c>for</c>, <c>aria-describedby</c> and the <see cref="FormOptions"/> registration all
    /// stay in step and never dangle. See <see cref="EditControlInit.SyncResolvedId"/>.
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        // Gated on _stateInitialized only so a control whose init threw (missing @bind-Value) can't
        // resolve an id against a default FieldIdentifier — the same guard Dispose uses.
        if (_stateInitialized)
            EditControlInit.SyncResolvedId(ref _id, this, FormOptions, FormGroupOptions, _fieldIdentifier);
        RefreshAriaState();

        // TXT-1: only these two modes can unmount a focused editor mid-edit (see ShouldShowComponent)
        // — gating the injected focus-tracking handlers to just that case keeps every
        // HidingMode.None control (the overwhelming common case) exactly as cheap as before, with no
        // extra per-keystroke dictionary allocation or focus/blur round trip.
        var effectiveHiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        if (effectiveHiding is HidingMode.WhenNull or HidingMode.WhenNullOrDefault)
            AdditionalAttributes = WithFocusTracking(AdditionalAttributes);
    }

    /// <summary>
    /// Whether this control renders a character-count element for <c>aria-describedby</c> to point at
    /// (<c>count-{id}</c>). False here and overridden only by <see cref="EditTextInputBase"/>, whose
    /// two controls have a <c>ShowCount</c> — every other control must keep a byte-identical
    /// describedby, and a control that renders no count span must never reference one.
    /// </summary>
    protected virtual bool HasCharacterCount => false;

    // aria-required plus the error-msg id and aria-describedby token list, all through the one shared
    // helper (required-ness resolves as IsRequired param → [Required] attribute →
    // FormOptions.RequiredResolver, so aria-required always matches the FormLabel star). No-op until
    // InitState has run — _attributes is null before then.
    void RefreshAriaState()
    {
        if (_attributes is null) return;
        (_isRequired, _errorMsgId, _describedBy) =
            EditControlInit.ResolveAriaState(this, FormOptions, _id, _attributes, _fieldIdentifier, HasCharacterCount);
        // INF-4: keep the validation summary's label-resolution inputs current too (see
        // FormOptions.FieldMetadata), so a runtime Label change is reflected in ValidationView's
        // rewritten message the same way it already is in this control's own FieldValidationDisplay.
        FormOptions?.RegisterFieldMetadata(_fieldIdentifier, _attributes, Label);
    }

    /// <summary>
    /// Drops the field registration <see cref="InitState"/> added, so a control removed from the
    /// render tree (e.g. behind a conditional <c>@if</c>) doesn't leave stale state for the validation
    /// summary to link to — <see cref="FormOptions"/> is per-form and long-lived, so an unpaired
    /// registration also grows <see cref="FormOptions.FieldIdentifiers"/> on every mount/unmount cycle.
    /// Mirrors <see cref="EditControlListBase{TItem}.Dispose"/>; derived overrides must chain to base.
    /// </summary>
    /// <remarks>
    /// Gated on <c>_stateInitialized</c> so a control whose init never completed disposes cleanly:
    /// a missing <c>@bind-Value</c> makes <see cref="OnInitialized"/> throw its helpful diagnostic
    /// BEFORE <see cref="InitState"/> runs, leaving <c>_fieldIdentifier</c> at <c>default</c> — and
    /// unregistering that hashes a null <c>FieldName</c>, so an <see cref="ArgumentNullException"/>
    /// out of disposal replaced the message that says what to fix.
    /// </remarks>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _stateInitialized)
            EditControlInit.UnregisterField(FormOptions, _fieldIdentifier, this);
        base.Dispose(disposing);
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    /// <summary>
    /// Resolves the effective <see cref="UpdateTrigger"/> through three levels — the control's own
    /// <paramref name="updateOn"/> parameter, then the cascaded <see cref="FormDefaults.EffectiveUpdateOn"/>,
    /// then the calling control's own built-in <paramref name="fallback"/> — and maps it to the DOM
    /// event name that drives the commit. Returns that name as a plain string, rather than leaving
    /// callers to switch on the enum, because the call site is <c>@bind-value:event</c>: the Razor
    /// compiler emits the <c>:event</c> modifier's value as a runtime expression (not a compile-time
    /// literal), so a string result plugs straight in.
    /// </summary>
    protected string ResolveUpdateEvent(UpdateTrigger? updateOn, UpdateTrigger fallback) =>
        (updateOn ?? FormDefaults?.EffectiveUpdateOn ?? fallback) == UpdateTrigger.Change ? "onchange" : "oninput";

    /// <summary>
    /// True when <see cref="InputBase{TValue}.CurrentValue"/> is the type's semantic "empty" —
    /// empty string for string controls, numeric zero for number controls, <c>default(DateTime)</c>
    /// for date controls, etc. Override in derived classes where the default semantics aren't
    /// <c>EqualityComparer&lt;T&gt;.Default.Equals(value, default)</c>. <see cref="ShouldShowComponent"/>
    /// already short-circuits the null check, so overrides only need to answer "is this value
    /// semantically empty?" — the null branch is handled for them.
    /// </summary>
    protected virtual bool IsValueDefault() =>
        EqualityComparer<TValue>.Default.Equals(CurrentValue, default!);

    /// <summary>
    /// Decides whether the control's wrapper renders at all, based on <see cref="IsHidden"/> and
    /// the effective <see cref="HidingMode"/> (per-control <see cref="Hiding"/> ?? form-wide
    /// <see cref="FormOptions.Hiding"/> ?? <see cref="HidingMode.None"/>). Centralizes the
    /// hiding logic that every scalar control used to re-implement. Override
    /// <see cref="IsValueDefault"/> rather than this method when only the "what counts as
    /// default?" question changes.
    /// </summary>
    protected virtual bool ShouldShowComponent()
    {
        var isNull = CurrentValue is null;
        var shouldShow = EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isNull || IsValueDefault());
        if (shouldShow || IsHidden || !ShowEditor)
            return shouldShow;
        // TXT-1: reached only for a VALUE-driven hide (HidingMode.WhenNull/WhenNullOrDefault) while the
        // editor is showing — defer it while the editor holds focus (_editorFocused, tracked by the
        // onfocus/onblur handlers OnParametersSet injects below) instead of unmounting the element the
        // user is currently typing into, which used to drop focus to <body> with no warning to a
        // screen-reader user. The deferred hide finally applies once the user's own blur/Tab-away moves
        // focus on its own — nothing left to rescue by then. An explicit IsHidden=true bypasses this
        // (returned above): it's a deliberate, immediate consumer decision (e.g. leaving a wizard step),
        // not a side effect of what the user just typed, and must still take effect at once.
        return _editorFocused;
    }

    // ───────────────────────── TXT-1: focus preservation under value-driven hiding ─────────────────────────

    // Whether this control's own editor currently holds DOM focus. Known only from the synthetic
    // onfocus/onblur handlers below — InputBase's CurrentValue commit path doesn't distinguish the
    // user's own keystroke from a parent reassigning the bound model, so there is no other signal to
    // read this off of without a JS round trip (and the library has no existing JS primitive that
    // queries focus state, only JsInteropEc.FocusById, which SETS it — seeded by a computed target id
    // rather than a live "is X focused" answer irrelevant here).
    bool _editorFocused;

    // A consumer's own onfocus/onblur, when they supplied one. Captured and re-invoked rather than
    // overwritten: unlike EditTextInputBase's oninput (genuinely library-owned — it drives binding),
    // onfocus/onblur are ordinary consumer-facing DOM events, so clobbering them would pay for an
    // accessibility fix with a silent behavioural regression in any app that uses them.
    object? _consumerOnFocus;
    object? _consumerOnBlur;

    async Task OnEditorFocusIn(FocusEventArgs e)
    {
        _editorFocused = true;
        await InvokeConsumerHandlerAsync(_consumerOnFocus, e);
    }

    // No explicit re-render call needed: Blazor already re-renders after any bound event handler runs,
    // which is what lets ShouldShowComponent() re-evaluate (and finally hide, if the value is still
    // empty/default) now that focus has genuinely moved on by the user's own action, rather than being
    // torn away from them.
    async Task OnEditorFocusOut(FocusEventArgs e)
    {
        _editorFocused = false;
        await InvokeConsumerHandlerAsync(_consumerOnBlur, e);
    }

    // Type-pattern dispatch rather than reflection, so this stays trim/AOT-clean (Controls.csproj sets
    // IsAotCompatible). Covers the shapes Blazor actually hands through a splatted attribute
    // dictionary; anything else is ignored rather than throwing, since a stray value here must never
    // break the control's own focus tracking.
    static Task InvokeConsumerHandlerAsync(object? handler, FocusEventArgs e)
    {
        switch (handler)
        {
            case EventCallback<FocusEventArgs> typedCallback:
                return typedCallback.InvokeAsync(e);
            case EventCallback untypedCallback:
                return untypedCallback.InvokeAsync(e);
            case Func<FocusEventArgs, Task> asyncHandler:
                return asyncHandler(e);
            case Action<FocusEventArgs> syncHandler:
                syncHandler(e);
                return Task.CompletedTask;
            case Action bareHandler:
                bareHandler();
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }

    // The dictionary WithFocusTracking returned last time, held so a re-entrant parameter cycle can be
    // detected by reference (see that method's remarks).
    Dictionary<string, object>? _focusTrackedAttributes;

    /// <summary>
    /// Injects synthetic onfocus/onblur handlers into <paramref name="attributes"/> so
    /// <see cref="ShouldShowComponent"/> can tell whether this control's own editor currently holds
    /// focus before deciding to unmount it (see <see cref="_editorFocused"/>). Every derived control's
    /// markup already splats <see cref="InputBase{TValue}.AdditionalAttributes"/> onto its editor
    /// element via <c>AttributeSplat.Rest</c> (or <c>RestWith</c>, layering its own synthetic
    /// attributes on top — e.g. <see cref="EditTextInputBase"/>'s <c>oninput</c> handler) — so
    /// reassigning this property in <see cref="OnParametersSet"/> is picked up with no change needed
    /// at any of those call sites, and without any new JS.
    /// </summary>
    /// <remarks>
    /// A same-named consumer handler is <em>chained</em>, not overwritten: it is captured into
    /// <see cref="_consumerOnFocus"/>/<see cref="_consumerOnBlur"/> and re-invoked from this control's
    /// own handler. <c>onfocus</c>/<c>onblur</c> are ordinary consumer-facing DOM events, so the
    /// "own wins" precedent <c>AttributeSplat.RestWith</c> sets for <see cref="EditTextInputBase"/>'s
    /// <c>oninput</c> (which drives binding, and is genuinely library-owned) does not extend to them.
    /// <para>
    /// The reference-equality guard matters: <see cref="OnParametersSet"/> also runs when only a
    /// cascading value changed, and in that case <see cref="InputBase{TValue}.AdditionalAttributes"/>
    /// still holds the dictionary this method returned last time. Re-processing it would capture this
    /// control's own callbacks as if they were the consumer's and recurse. Returning the cached
    /// instance also avoids re-allocating the dictionary on every parameter cycle.
    /// </para>
    /// </remarks>
    IReadOnlyDictionary<string, object> WithFocusTracking(IReadOnlyDictionary<string, object>? attributes)
    {
        if (_focusTrackedAttributes is not null && ReferenceEquals(attributes, _focusTrackedAttributes))
            return _focusTrackedAttributes;

        var merged = attributes is null
            ? new Dictionary<string, object>(2)
            : new Dictionary<string, object>(attributes);
        _consumerOnFocus = merged.GetValueOrDefault("onfocus");
        _consumerOnBlur = merged.GetValueOrDefault("onblur");
        merged["onfocus"] = EventCallback.Factory.Create<FocusEventArgs>(this, OnEditorFocusIn);
        merged["onblur"] = EventCallback.Factory.Create<FocusEventArgs>(this, OnEditorFocusOut);
        _focusTrackedAttributes = merged;
        return merged;
    }
}
