namespace Controls.Helpers;

/// <summary>
/// Shared initialization logic for edit controls. Eliminates the boilerplate that every
/// <c>Edit*.razor.cs</c> would otherwise duplicate in <c>OnInitialized</c> and the
/// <c>ShowEditor</c>/<c>ShouldHideLabel</c> computed properties.
/// </summary>
/// <remarks>
/// A static helper rather than a base class because each edit control inherits a different
/// Microsoft <c>Input*</c> base (<see cref="Microsoft.AspNetCore.Components.Forms.InputText"/>,
/// <c>InputNumber&lt;T&gt;</c>, <c>InputDate&lt;T&gt;</c>, etc.) — single inheritance precludes
/// a unified <c>EditControlBase&lt;T&gt;</c>.
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
}
