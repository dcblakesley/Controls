namespace Controls;

public partial class EditBool : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public required Expression<Func<bool?>> Field { get; set; }
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; }

    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public HidingMode? Hiding { get; set; }
    
    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }

    bool ShouldShowComponent()
    {
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => true,
            HidingMode.WhenNullOrDefault => !CurrentValue, 
            HidingMode.WhenReadOnlyAndNull => true,
            HidingMode.WhenReadOnlyAndNullOrDefault => !IsEditMode && CurrentValue,
            _ => true
        };
    }
}