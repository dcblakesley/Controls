namespace Controls.Helpers;

/// <summary>
/// Shared initialization logic for edit controls. Eliminates the boilerplate that every
/// <c>Edit*.razor.cs</c> would otherwise duplicate in <c>OnInitialized</c> and the
/// <c>ShowEditor</c>/<c>ShouldHideLabel</c> computed properties — plus <see cref="TryConvert{T}"/>,
/// the one parse body two controls share for the same "can't share a base" reason.
/// </summary>
/// <remarks>
/// A static helper rather than a base class so the same logic can be shared by the control bases
/// that can't share a common ancestor: <see cref="EditControlBase{TValue}"/> (an
/// <see cref="Microsoft.AspNetCore.Components.Forms.InputBase{TValue}"/>),
/// <see cref="EditControlListBase{TItem}"/> (a plain <c>ComponentBase</c> that binds a collection),
/// and <c>EditRadio&lt;TValue&gt;</c> (which must inherit <c>InputRadioGroup&lt;TValue&gt;</c> to supply
/// the <c>InputRadioContext</c> its child <c>InputRadio</c>s resolve).
/// </remarks>
public static class EditControlInit
{
    /// <summary>
    /// Resolves the three standard derived values every edit control needs: the rendered HTML id,
    /// the attribute list from the model property, and the FieldIdentifier. (Required-ness is
    /// resolved separately via <see cref="IsRequired"/> — it also depends on the control's
    /// <c>IsRequired</c> parameter and the form's <see cref="FormOptions.RequiredResolver"/>.)
    /// </summary>
    public static (string Id, List<Attribute> Attributes, FieldIdentifier FieldIdentifier) Init<T>(
        Expression<Func<T>> field,
        string? id,
        FormGroupOptions? formGroupOptions,
        string? idPrefix)
    {
        var fieldIdentifier = FieldIdentifier.Create(field);
        var attributes = AttributesHelper.GetExpressionCustomAttributes(field);
        var resolvedId = AttributesHelper.GetId(id, formGroupOptions, idPrefix, fieldIdentifier);
        return (resolvedId, attributes, fieldIdentifier);
    }

    /// <summary>
    /// <see cref="Init"/> plus the paired <see cref="RegisterField"/>, in the one call every control
    /// base makes from its own <c>InitState</c>. Reads the id/prefix straight off
    /// <paramref name="control"/>, which is also the registration's owner.
    /// </summary>
    /// <remarks>
    /// The two halves belong together: a control that resolves its state but forgets to register is a
    /// field the validation summary can't link to, and the pairing was previously re-typed in
    /// <see cref="EditControlBase{TValue}"/>, <see cref="EditControlListBase{TItem}"/> and
    /// <c>EditRadio</c> (whose base class, <c>InputRadioGroup</c>, keeps it out of the other two's
    /// inheritance chain — the standing reason this helper class exists at all).
    /// </remarks>
    public static (string Id, List<Attribute> Attributes, FieldIdentifier FieldIdentifier) InitAndRegister<T>(
        Expression<Func<T>> field, IEditControl control, FormOptions? formOptions, FormGroupOptions? formGroupOptions)
    {
        var state = Init(field, control.Id, formGroupOptions, control.IdPrefix);
        RegisterField(formOptions, state.FieldIdentifier, state.Id, control);
        return state;
    }

    /// <summary>
    /// Re-resolves a control's element id from its CURRENT <see cref="IEditControl.Id"/>/
    /// <see cref="IEditControl.IdPrefix"/> and the cascaded group name, and — when the answer changed
    /// — writes it back through <paramref name="id"/> and moves the control's
    /// <see cref="FormOptions"/> registration onto it. Returns whether it changed, so the caller can
    /// skip any further per-id work (a rebuilt <c>aria-describedby</c>) on the overwhelmingly common
    /// no-change parameter cycle.
    /// </summary>
    /// <remarks>
    /// Called from every control root's <c>OnParametersSet</c>. The id used to be resolved once in
    /// <c>OnInitialized</c> and never again, so a runtime <c>IdPrefix</c>/<c>Id</c> change (a control
    /// re-used for a different row/record, a form group renaming itself) left the element rendering
    /// under a stale id while the label's <c>for</c>, the <c>aria-describedby</c>/<c>aria-errormessage</c>
    /// targets and the <see cref="FormOptions.FieldIds"/> entry the validation summary links to all
    /// kept pointing at ids that no longer existed. <see cref="EditDisplay"/> — the one control that
    /// already re-resolved every parameter cycle — is the pattern this generalizes.
    /// <para>
    /// Re-registering (rather than unregister + register) is deliberate: the
    /// <see cref="FieldIdentifier"/> is unchanged, and <see cref="FormOptions.RegisterField"/> already
    /// treats a repeat call from the same owner as "this control's id moved", updating both
    /// <see cref="FormOptions.FieldIds"/> and its own owner record in place. Unregistering first would
    /// briefly drop a field that a second control may be sharing.
    /// </para>
    /// </remarks>
    public static bool SyncResolvedId(ref string id, IEditControl control, FormOptions? formOptions,
        FormGroupOptions? formGroupOptions, FieldIdentifier fieldIdentifier)
    {
        var resolved = AttributesHelper.GetId(control.Id, formGroupOptions, control.IdPrefix, fieldIdentifier);
        if (string.Equals(resolved, id, StringComparison.Ordinal))
            return false;
        id = resolved;
        RegisterField(formOptions, fieldIdentifier, resolved, control);
        return true;
    }

    /// <summary>
    /// Returns <paramref name="expression"/>, or throws the standard "this control needs a two-way
    /// binding" diagnostic naming <paramref name="control"/>. The compiler-supplied
    /// <c>ValueExpression</c> (or <c>StartExpression</c>/<c>EndExpression</c>) is what all of a
    /// control's derived state comes from, so its absence has to fail loudly rather than render a
    /// control with no id, no model attributes and no field registration.
    /// </summary>
    /// <param name="expression">The compiler-populated accessor, or null when the consumer bound one-way.</param>
    /// <param name="control">The control being initialized — supplies the name in the message.</param>
    /// <param name="bindAttribute">The markup attribute that supplies it, e.g. <c>@bind-Value</c>.</param>
    /// <param name="expressionParameterName">The parameter that attribute populates, e.g. <c>ValueExpression</c>.</param>
    public static Expression<Func<T>> RequireBinding<T>(
        Expression<Func<T>>? expression, object control,
        string bindAttribute = "@bind-Value", string expressionParameterName = "ValueExpression") =>
        expression ?? throw new InvalidOperationException(
            $"{ControlName(control)} requires a two-way {bindAttribute} binding (which supplies {expressionParameterName}).");

    /// <summary>
    /// A control's simple type name for diagnostics — <c>EditNumber</c>, not the CLR's
    /// <c>EditNumber`1</c>. Reproduces exactly what the per-control <c>nameof(EditNumber&lt;T&gt;)</c>
    /// in each control's own "requires a two-way @bind-Value binding" message produced, so hoisting
    /// that message onto the control bases left every control's text byte-identical.
    /// </summary>
    /// <remarks>
    /// Lives here rather than on a base for this class's usual reason: both control bases need it and
    /// they share no ancestor. <c>GetType().Name</c> is trim/AOT-safe — it reads the runtime type's own
    /// name and needs no member metadata — which is what lets the message move off the call site, where
    /// a compile-time <c>nameof</c> was previously the only option.
    /// </remarks>
    public static string ControlName(object control)
    {
        var name = control.GetType().Name;
        // A generic type's runtime name carries the CLR arity suffix (`EditNumber`1`); nameof() never
        // did, so trim it back to the bare name.
        var arity = name.IndexOf('`');
        return arity < 0 ? name : name[..arity];
    }

    /// <summary>
    /// Registers a control's field (and its resolved element id) with the form so the validation
    /// summary can link to it. <paramref name="owner"/> is the registering control instance, so two
    /// controls bound to the same property share one entry until the last of them unregisters.
    /// </summary>
    /// <remarks>
    /// Paired with <see cref="UnregisterField"/> — a control that registers MUST unregister on
    /// dispose. <see cref="FormOptions"/> is per-form and long-lived, so a control removed behind a
    /// conditional <c>@if</c> would otherwise leave a dead <see cref="FieldIdentifier"/> that
    /// <see cref="ValidationView"/> links to and re-iterates every render, growing with each
    /// mount/unmount cycle. The two halves sit here as one pair so a control base written against
    /// this helper can't implement only the registering side (as the scalar bases once did).
    /// </remarks>
    public static void RegisterField(FormOptions? formOptions, FieldIdentifier fieldIdentifier, string id, object owner) =>
        formOptions?.RegisterField(fieldIdentifier, id, owner);

    /// <summary>
    /// Drops the registration <see cref="RegisterField"/> added. Called from a control's dispose, and
    /// before re-registering when a bound model (and therefore the <see cref="FieldIdentifier"/>) is
    /// swapped out from under a control that supports it.
    /// </summary>
    public static void UnregisterField(FormOptions? formOptions, FieldIdentifier fieldIdentifier, object owner) =>
        formOptions?.UnregisterField(fieldIdentifier, owner);

    /// <summary>
    /// The single source of truth for whether a field is required — used by both the FormLabel
    /// star and <c>aria-required</c> so the two signals can never disagree. Resolution order:
    /// an explicitly-set <see cref="IEditControl.IsRequired"/> parameter wins outright
    /// (<c>true</c> forces required — e.g. RequiredIf; <c>false</c> forces optional — e.g. a
    /// <see cref="RequiredAttribute"/>-derived conditional attribute whose condition is off);
    /// otherwise a <see cref="RequiredAttribute"/> on the model property OR the form-level
    /// <see cref="FormOptions.RequiredResolver"/> (the FluentValidation bridge point) marks it
    /// required. The resolver is skipped for a default <see cref="FieldIdentifier"/> (no model —
    /// e.g. FormLabel used standalone) so consumer lambdas never see a null Model.
    /// </summary>
    public static bool IsRequired(List<Attribute>? attributes, bool? isRequiredParam,
        FormOptions? formOptions, FieldIdentifier fieldIdentifier)
    {
        if (isRequiredParam is not null)
            return isRequiredParam.Value;
        if (attributes?.Any(x => x is RequiredAttribute) ?? false)
            return true;
        var resolver = formOptions?.RequiredResolver;
        return resolver is not null && fieldIdentifier.Model is not null && resolver(fieldIdentifier);
    }

    /// <summary>
    /// The <c>aria-required</c> value (<c>"true"</c> when <see cref="IsRequired"/> resolves true,
    /// else <c>null</c> so the attribute is omitted rather than rendered as a noisy <c>"false"</c>).
    /// </summary>
    public static string? AriaRequired(List<Attribute>? attributes, bool? isRequiredParam,
        FormOptions? formOptions, FieldIdentifier fieldIdentifier) =>
        IsRequired(attributes, isRequiredParam, formOptions, fieldIdentifier) ? "true" : null;

    /// <summary> True when the editor input should render. False renders the read-only view instead. </summary>
    public static bool ShowEditor(bool isEditMode, FormOptions? formOptions) =>
        isEditMode && (formOptions?.IsEditMode ?? true);

    /// <summary> True when the label/legend should be suppressed for this control. </summary>
    public static bool ShouldHideLabel(bool isLabelHidden, FormOptions? formOptions) =>
        isLabelHidden || (formOptions?.IsLabelHidden ?? false);

    /// <summary>
    /// Resolves EditBool / EditCheckedStringList / EditCheckedEnumList's styled-checkbox switch (and
    /// Table's row-selection checkboxes, which pass <c>null</c> for <paramref name="formOptions"/>): an
    /// explicit per-control <c>UseStyledCheckbox</c> parameter wins outright; otherwise the form's
    /// <see cref="FormOptions.UseStyledCheckbox"/>, then any enclosing <see cref="FormDefaults"/>
    /// (app/MFE-root default), then the process-wide <see cref="FormOptions.DefaultUseStyledCheckbox"/>.
    /// </summary>
    public static bool UseStyledCheckbox(bool? perControlValue, FormOptions? formOptions, FormDefaults? formDefaults) =>
        perControlValue ?? formOptions?.UseStyledCheckbox ?? formDefaults?.EffectiveUseStyledCheckbox ?? FormOptions.DefaultUseStyledCheckbox;

    /// <summary>
    /// Decides whether an edit control's wrapper renders, from <see cref="IEditControl.IsHidden"/> and
    /// the effective <see cref="HidingMode"/> (per-control ?? form-wide ?? <see cref="HidingMode.None"/>).
    /// Centralizes the hiding truth table that the scalar base, the list base and <c>EditRadio</c>
    /// previously each re-implemented — they only differ in how they compute <paramref name="isNull"/>
    /// and <paramref name="isDefault"/> for their value shape.
    /// </summary>
    public static bool ShouldShow(bool isHidden, HidingMode? perControlHiding, FormOptions? formOptions,
        bool showEditor, bool isNull, bool isDefault)
    {
        if (isHidden) return false;

        var hidingMode = perControlHiding ?? formOptions?.Hiding ?? HidingMode.None;
        if (hidingMode == HidingMode.None) return true;

        return hidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !(!showEditor && isNull),
            HidingMode.WhenReadOnlyAndNullOrDefault => !(!showEditor && isDefault),
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }

    /// <summary>
    /// Builds the space-separated <c>aria-describedby</c> token list for an edit control, including
    /// only the IDs that will actually render: the validation message (always present) plus the
    /// description, tooltip and character count when they exist. Computed once at init — the result
    /// is stable for the control's lifetime — so the markup binds a cached string instead of
    /// re-interpolating it (and never references a missing
    /// <c>desc-</c>/<c>tooltip-</c>/<c>count-</c> element).
    /// </summary>
    /// <remarks>
    /// <paramref name="hasCount"/> is last in the token list, and defaulted, for the same reason it
    /// is threaded through at all: the count is the least important of the three descriptions (it is
    /// read after the field's own instructions), and every control that has no character counter must
    /// keep a byte-identical <c>aria-describedby</c>. Only <see cref="EditTextInputBase"/>'s two
    /// controls pass it, and only while their <c>ShowCount</c> is on AND the editor is actually
    /// rendering — the read-only views render no count span for the token to point at.
    /// </remarks>
    public static string BuildDescribedBy(string id, bool hasDescription, bool hasTooltip, bool hasCount = false)
    {
        var describedBy = $"error-msg-{id}";
        if (hasDescription) describedBy += $" desc-{id}";
        if (hasTooltip) describedBy += $" tooltip-{id}";
        if (hasCount) describedBy += $" count-{id}";
        return describedBy;
    }

    /// <summary>
    /// Resolves the cached ARIA reference strings — the <c>error-msg-</c> id and the full
    /// <c>aria-describedby</c> token list — for an edit control. Centralizes the block that
    /// <see cref="EditControlBase{TValue}"/>, <see cref="EditControlListBase{TItem}"/> and
    /// <c>EditRadio</c> previously each duplicated. Called from <c>InitState</c> and again on
    /// parameter changes, so a runtime <paramref name="description"/>/<paramref name="tooltip"/> or
    /// label-hidden change is reflected and <c>aria-describedby</c> never points at a missing
    /// <c>desc-</c>/<c>tooltip-</c> element.
    /// </summary>
    public static (string ErrorMsgId, string DescribedBy) ResolveAriaRefs(
        string id, bool shouldHideLabel, string? description, string? tooltip, List<Attribute>? attributes,
        bool hasCount = false)
    {
        var errorMsgId = $"error-msg-{id}";
        var hasTooltipText = !string.IsNullOrEmpty(tooltip ?? attributes.Tooltip());
        // Not gated on shouldHideLabel: FormLabel renders the desc- element either way (visually
        // hidden alongside the hidden label). Hiding the label is a layout decision, and dropping the
        // field's format instructions with it took them from sighted users too. In that mode it also
        // FOLDS THE TOOLTIP TEXT into the same element, so a hidden-label control with only a tooltip
        // still has a desc- to point at (see FormLabel's hidden branch).
        var hasDescription = !string.IsNullOrEmpty(description ?? attributes.Description())
            || (shouldHideLabel && hasTooltipText);
        // The tooltip's own REFERENCE is still gated: the trigger is an interactive hover/focus
        // widget, FormLabel renders none in the hidden-label branch -- so tooltip-{id} would dangle.
        // Only the element goes away there; the text survives via desc- above.
        var hasTooltip = !shouldHideLabel && hasTooltipText;
        return (errorMsgId, BuildDescribedBy(id, hasDescription, hasTooltip, hasCount));
    }

    /// <summary>
    /// The whole cached ARIA state of one bound field in one call — <c>aria-required</c>
    /// (<see cref="AriaRequired"/>) plus the <c>error-msg-</c> id and <c>aria-describedby</c> token
    /// list (<see cref="ResolveAriaRefs"/>). The two are always recomputed together, at init and again
    /// on every parameter change, so this owns that pairing rather than leaving each control base to
    /// re-sequence it: <see cref="EditControlBase{TValue}"/>, <see cref="EditControlListBase{TItem}"/>,
    /// <c>EditRadio</c> and <c>EditDateRange</c> (once per bound field) all call it from both places.
    /// </summary>
    public static (string? AriaRequired, string ErrorMsgId, string DescribedBy) ResolveAriaState(
        string id, bool shouldHideLabel, string? description, string? tooltip,
        List<Attribute>? attributes, bool? isRequiredParam, FormOptions? formOptions, FieldIdentifier fieldIdentifier,
        bool hasCount = false)
    {
        var ariaRequired = AriaRequired(attributes, isRequiredParam, formOptions, fieldIdentifier);
        var (errorMsgId, describedBy) = ResolveAriaRefs(id, shouldHideLabel, description, tooltip, attributes, hasCount);
        return (ariaRequired, errorMsgId, describedBy);
    }

    /// <summary>
    /// <see cref="ResolveAriaState(string, bool, string?, string?, List{Attribute}?, bool?, FormOptions?, FieldIdentifier, bool)"/>
    /// for a control's own single bound field: everything the overload above needs except the id, the
    /// attribute list and the FieldIdentifier comes off <paramref name="control"/> itself (including
    /// the <see cref="ShouldHideLabel"/> resolution, which each caller previously repeated).
    /// </summary>
    /// <remarks>
    /// This is the form <see cref="EditControlBase{TValue}"/>, <see cref="EditControlListBase{TItem}"/>
    /// and <c>EditRadio</c> all call from their <c>RefreshAriaState</c>. The granular overload stays
    /// for the callers that don't map onto "the control's own field" —
    /// <see cref="EditDateRange"/>'s second (End) field passes a different id, attribute list and
    /// label-hidden answer than its host control's.
    /// </remarks>
    public static (string? AriaRequired, string ErrorMsgId, string DescribedBy) ResolveAriaState(
        IEditControl control, FormOptions? formOptions, string id, List<Attribute>? attributes,
        FieldIdentifier fieldIdentifier, bool hasCount = false) =>
        ResolveAriaState(id, ShouldHideLabel(control.IsLabelHidden, formOptions), control.Description, control.Tooltip,
            attributes, control.IsRequired, formOptions, fieldIdentifier, hasCount);

    /// <summary>
    /// The shared <c>InputBase&lt;TValue&gt;.TryParseValueFromString</c> body for the controls that hand
    /// their parsing to <see cref="BindConverter"/> —
    /// <c>EditNumber&lt;T&gt;</c> (every numeric primitive plus their unsigned/nullable variants) and
    /// <c>EditDateNative&lt;T&gt;</c> (DateTime/DateTimeOffset/DateOnly/TimeOnly, likewise nullable).
    /// Ported from Microsoft's <c>InputNumber&lt;T&gt;</c>/<c>InputDate&lt;T&gt;</c>, which share the
    /// same body: convert invariantly, and on failure format <paramref name="parsingErrorMessage"/>
    /// with <paramref name="fieldName"/>. Only that message differs between the two controls, so it
    /// stays a per-control parameter (their defaults differ too).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A static helper rather than a member of the base the two controls do share
    /// (<see cref="EditTextControlBase{TValue}"/>): <see cref="BindConverter.TryConvertTo{T}"/> demands
    /// <see cref="DynamicallyAccessedMemberTypes.All"/> on its type argument, so hosting this on that
    /// base would force the annotation onto its <c>TValue</c> — and onto the string controls that
    /// inherit it and don't need it. Here the annotation stops at the two controls that actually
    /// declare it, matching this class's existing "shared by types that can't share an ancestor"
    /// rationale.
    /// </para>
    /// <para>
    /// <paramref name="validationErrorMessage"/> is a non-nullable <c>string</c> (assigned <c>null!</c>
    /// on success, exactly as the framework's originals do) so a control can forward its own
    /// <c>out</c> parameter straight through — <c>InputBase</c> declares the signature that way.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The bound value type, converted by <see cref="BindConverter"/>.</typeparam>
    public static bool TryConvert<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        string? value, string parsingErrorMessage, string fieldName, out T result, out string validationErrorMessage)
    {
        if (BindConverter.TryConvertTo<T>(value, CultureInfo.InvariantCulture, out var parsedValue))
        {
            result = parsedValue!;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = string.Format(CultureInfo.InvariantCulture, parsingErrorMessage, fieldName);
        return false;
    }

    /// <summary>
    /// The "is this bound date value semantically default/empty" check shared by
    /// <see cref="EditDate{T}"/>, <see cref="EditDateNative{T}"/>, and <see cref="EditDateRange"/>'s
    /// per-field variant -- all three bridge a value that might be any of <c>DateTime</c>,
    /// <c>DateTimeOffset</c>, <c>DateOnly</c>, <c>TimeOnly</c> (or their nullable forms) and need
    /// <c>default(DateTime)</c>/etc. (not just null) to count as empty for the <c>HidingMode</c>
    /// NullOrDefault contract -- a plain <see cref="EqualityComparer{T}.Default"/> comparison against
    /// <c>null</c> misses a boxed non-null default struct (see each control's own former copy of this
    /// switch for the same remark). Lives here rather than a shared base for this class's usual reason
    /// -- the three controls inherit from bases (or, for EditDateRange, none at all) that share no
    /// common ancestor to hang this on.
    /// </summary>
    public static bool IsDateValueDefault<T>(T value) => value switch
    {
        DateTime dt => dt == default,
        DateTimeOffset dto => dto == default,
        DateOnly d => d == default,
        TimeOnly t => t == default,
        _ => EqualityComparer<T>.Default.Equals(value, default!)
    };

    /// <summary>
    /// Builds the splat dictionary <see cref="EditDate{T}"/> and <see cref="EditDateRange"/> forward
    /// onto their inner <c>DatePicker</c>/<c>DateRangePicker</c>'s own <c>AdditionalAttributes</c>: the
    /// consumer's own unmatched attributes, then <paramref name="cssClass"/> overwriting any raw
    /// consumer <c>"class"</c> so the picker's outer wrapper picks up the EditContext validation-state
    /// styling hooks. The two controls differ only in where <paramref name="cssClass"/> comes from
    /// (<c>EditDate</c>'s own <c>CssClass</c> vs. <c>EditDateRange</c>'s <c>FieldCssClass</c>, which
    /// folds in the End field's state too) -- everything else about the splat is identical.
    /// </summary>
    public static IReadOnlyDictionary<string, object> BuildPickerAttributes(
        IReadOnlyDictionary<string, object>? additionalAttributes, string? cssClass)
    {
        var attrs = new Dictionary<string, object>();
        if (additionalAttributes is not null)
            foreach (var kv in additionalAttributes) attrs[kv.Key] = kv.Value;
        if (!string.IsNullOrEmpty(cssClass)) attrs["class"] = cssClass;
        return attrs;
    }
}
