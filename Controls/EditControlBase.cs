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
/// the parsing that Microsoft's <c>Input*</c> classes used to provide. They must also declare their
/// own <c>Field</c> parameter (kept on the derived class because some controls — e.g.
/// <c>EditRadioEnum&lt;TEnum&gt;</c> — bind to a different generic type than they declare for Field).
/// </para>
/// <para>
/// After declaring <c>Field</c>, override <see cref="ComponentBase.OnInitialized"/> and call
/// <see cref="InitState{T}"/> with it — that's the one line each derived control needs to populate
/// the standard derived state.
/// </para>
/// </remarks>
public abstract class EditControlBase<TValue> : InputBase<TValue>, IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

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

    // Standard derived state — populated by InitState in derived class's OnInitialized.
    protected string _id = string.Empty;
    protected string? _isRequired;
    protected List<Attribute>? _attributes;
    protected FieldIdentifier _fieldIdentifier;
    // Cached ARIA references — resolved in InitState and re-resolved each OnParametersSet (see BuildDescribedBy).
    protected string _errorMsgId = string.Empty;
    protected string _describedBy = string.Empty;

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
    /// Populates <c>_id</c>, <c>_isRequired</c>, <c>_attributes</c>, and <c>_fieldIdentifier</c>
    /// from the derived control's <c>Field</c> expression, and registers the field with
    /// <see cref="FormOptions.FieldIdentifiers"/> so the validation summary can link to it.
    /// Generic so it works regardless of whether the control's Field type matches
    /// <typeparamref name="TValue"/> exactly (some controls — e.g. <c>EditRadioEnum</c> — declare
    /// <c>Expression&lt;Func&lt;TEnum&gt;&gt;</c> while inheriting
    /// <c>EditControlBase&lt;TEnum?&gt;</c>).
    /// </summary>
    /// <remarks>
    /// Registration used to live in <c>FieldValidationDisplay.OnInitialized</c>, but that
    /// component is rendered conditionally (inside <c>@if (ShouldShowComponent())</c>) — so
    /// hidden fields silently never registered, and the validation summary couldn't link to them.
    /// Registering here happens once per control init and survives any HidingMode setting.
    /// </remarks>
    protected void InitState<T>(Expression<Func<T>> field)
    {
        (_id, _attributes, _fieldIdentifier) = EditControlInit.Init(field, Id, FormGroupOptions, IdPrefix);
        // Required-ness resolves through the shared helper (IsRequired param → [Required] attribute
        // → FormOptions.RequiredResolver) so aria-required always matches the FormLabel star.
        _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _fieldIdentifier);
        FormOptions?.RegisterField(_fieldIdentifier, _id, this);

        // Resolve the ARIA references (error-msg id + aria-describedby token list). Recomputed in
        // OnParametersSet too, so a runtime Description/Tooltip/label-hidden change is reflected and
        // aria-describedby never points at a missing desc-/tooltip- element.
        (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
    }

    /// <summary>
    /// Re-resolves the cached ARIA references on parameter change (e.g. a runtime Description/Tooltip
    /// or label-hidden toggle) so aria-describedby stays accurate and never dangles. No-op until
    /// InitState has run (_attributes is null before then).
    /// </summary>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_attributes is not null)
        {
            _isRequired = EditControlInit.AriaRequired(_attributes, IsRequired, FormOptions, _fieldIdentifier);
            (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
        }
    }

    /// <summary> True when the editor input should render. False renders the read-only view. </summary>
    protected bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);

    /// <summary> True when the label should be suppressed. </summary>
    protected bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

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
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull, isNull || IsValueDefault());
    }
}
