namespace Controls;

/// <summary> Edit control for selecting a value using radio buttons. Create options within the markup using InputRadio components.</summary>
/// <remarks>
/// Inherits <see cref="InputRadioGroup{TValue}"/> directly rather than the shared
/// <c>EditControlBase&lt;T&gt;</c> because <c>EditRadio</c>'s public API takes
/// <see cref="InputRadio{TValue}"/> children as <c>ChildContent</c>, and those children resolve a
/// cascading <c>InputRadioContext</c> that only <see cref="InputRadioGroup{TValue}"/> supplies.
/// Replacing the base would require a parallel <c>&lt;InputRadio&gt;</c> API and break every
/// consumer — see README §10.2.0 for the intentional design. The sibling controls
/// <c>EditRadioEnum</c> and <c>EditRadioString</c> render their own <c>&lt;input type="radio"&gt;</c>
/// markup and never see <c>InputRadio</c> children, so they inherit <c>EditControlBase</c> normally.
/// </remarks>
public partial class EditRadio<TValue> : InputRadioGroup<TValue>, IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
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
    [Parameter] public bool IsRequired { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    /// <summary> Expression that binds to the property in the model.</summary>
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }
    
    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    string _id = string.Empty;
    string? _isRequired;
    FieldIdentifier _fieldIdentifier;
    List<Attribute>? _attributes;
    string _errorMsgId = string.Empty;
    string _describedBy = string.Empty;

    /// <summary> True when this field currently has a validation error (InputBase appends "invalid"). </summary>
    bool IsInvalid => CssClass?.Contains("invalid") == true;

    protected override void OnInitialized()
    {
        (_id, _isRequired, _attributes, _fieldIdentifier) = EditControlInit.Init(Field!, Id, FormGroupOptions, IdPrefix);
        // Register with FormOptions here (rather than relying on FieldValidationDisplay) so the
        // field survives HidingMode and links from the validation summary always work.
        FormOptions?.RegisterField(_fieldIdentifier, _id);

        // Mirror EditControlBase: resolve the ARIA references (recomputed in OnParametersSet too).
        (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
    }

    // InputRadioGroup uses OnParametersSet to set up the group name/context — call base first, then
    // refresh the cached ARIA references so a runtime Description/Tooltip/label change is reflected.
    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        if (_attributes is not null)
            (_errorMsgId, _describedBy) = EditControlInit.ResolveAriaRefs(_id, ShouldHideLabel, Description, Tooltip, _attributes);
    }

    bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);
    bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);

    protected bool ShouldShowComponent()
    {
        var value = Value;
        var isNull = value == null;
        return EditControlInit.ShouldShow(IsHidden, Hiding, FormOptions, ShowEditor, isNull,
            isNull || EqualityComparer<TValue>.Default.Equals(value, default));
    }
}
