namespace Controls.Helpers;

/// <summary>
/// Shared initialization logic for edit controls. Eliminates the boilerplate that every
/// <c>Edit*.razor.cs</c> would otherwise duplicate in <c>OnInitialized</c> and the
/// <c>ShowEditor</c>/<c>ShouldHideLabel</c> computed properties.
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
}
