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
    /// description and tooltip when they exist. Computed once at init — the result is stable for the
    /// control's lifetime — so the markup binds a cached string instead of re-interpolating it (and
    /// never references a missing <c>desc-</c>/<c>tooltip-</c> element).
    /// </summary>
    public static string BuildDescribedBy(string id, bool hasDescription, bool hasTooltip)
    {
        var describedBy = $"error-msg-{id}";
        if (hasDescription) describedBy += $" desc-{id}";
        if (hasTooltip) describedBy += $" tooltip-{id}";
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
        string id, bool shouldHideLabel, string? description, string? tooltip, List<Attribute>? attributes)
    {
        var errorMsgId = $"error-msg-{id}";
        var hasDescription = !shouldHideLabel && !string.IsNullOrEmpty(description ?? attributes.Description());
        var hasTooltip = !shouldHideLabel && !string.IsNullOrEmpty(tooltip ?? attributes.Tooltip());
        return (errorMsgId, BuildDescribedBy(id, hasDescription, hasTooltip));
    }

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
}
