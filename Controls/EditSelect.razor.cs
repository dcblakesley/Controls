namespace Controls;

/// <summary>
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
public partial class EditSelect<TValue> : IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    
    // IEditControl interface properties
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; }
    [Parameter] public bool IsRequired { get; set; }
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

    // Methods
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        // Get effective hiding mode (component's setting overrides form's setting)
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        if (hidingMode == HidingMode.None)
            return true;

        // Use the Field expression to get the current value
        var value = Field.Compile()();
        var isNull = value == null;
        var isDefault = isNull || EqualityComparer<TValue>.Default.Equals(value, default);
        var isReadOnly = !IsEditMode || (FormOptions != null && !FormOptions.IsEditMode);

        return hidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !(isReadOnly && isNull),
            HidingMode.WhenReadOnlyAndNullOrDefault => !(isReadOnly && isDefault),
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}