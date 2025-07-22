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

    /// <summary>
    /// Used when hiding is based on the value of the field, typically
    /// used for hiding controls that have a null backing field/property
    /// </summary>
    [Parameter] public HidingMode? Hiding { get; set; }

    /// <summary> Used when hiding is based on some other condition, such as "IsControlXyzAvailable" </summary>
    [Parameter] public bool IsHidden { get; set; }

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
        if (IsHidden)
            return false;
        
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