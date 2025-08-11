namespace Controls;

public partial class EditRadio<TValue> : InputRadioGroup<TValue>, IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; } [Parameter] public bool IsRequired { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public Expression<Func<TValue>>? Field { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    FieldIdentifier _fieldIdentifier;
    List<Attribute>? _attributes;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    protected bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var effectiveHiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        
        if (effectiveHiding == HidingMode.None)
            return true;

        var value = Value;
        var isNull = value == null;
        var isDefault = isNull || EqualityComparer<TValue>.Default.Equals(value, default);

        return effectiveHiding switch
        {
            HidingMode.WhenReadOnlyAndNull => IsEditMode || !isNull,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || !isDefault,
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}
