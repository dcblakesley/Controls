namespace Controls;

/// <summary> Select a string from Options (List of strings) </summary>
public partial class EditSelectString<TValue> : IEditControl
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
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component specific parameters
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }
    [Parameter] public required List<string> Options { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

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

        var effectiveHiding = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var value = Field.Compile().Invoke();
        var isEditMode = (FormOptions == null) || FormOptions.IsEditMode;

        return effectiveHiding switch
        {
            HidingMode.None => true,
            HidingMode.WhenReadOnlyAndNull => isEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => isEditMode || (value != null && value.ToString() != ""),
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => value != null && value.ToString() != "",
            _ => true
        };
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);
}