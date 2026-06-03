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
    /// Resolves the four standard derived values every edit control needs:
    /// the FieldIdentifier, the attribute list from the model property, the rendered HTML id,
    /// and the <c>"true"</c>/<c>"false"</c> string used for <c>aria-required</c>.
    /// </summary>
    public static (string Id, string IsRequired, List<Attribute> Attributes, FieldIdentifier FieldIdentifier) Init<T>(
        Expression<Func<T>> field,
        string? id,
        FormGroupOptions? formGroupOptions,
        string? idPrefix)
    {
        var fieldIdentifier = FieldIdentifier.Create(field);
        var attributes = AttributesHelper.GetExpressionCustomAttributes(field);
        var resolvedId = AttributesHelper.GetId(id, formGroupOptions, idPrefix, fieldIdentifier);
        var isRequired = attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
        return (resolvedId, isRequired, attributes, fieldIdentifier);
    }

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
}
