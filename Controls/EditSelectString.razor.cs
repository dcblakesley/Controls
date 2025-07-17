namespace Controls;

/// <summary> Select a string from Options (List of strings) </summary>
public partial class EditSelectString<TValue> : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? ContainerClass { get; set; }
    [Parameter] public string? Description { get; set; }

    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public HidingMode? Hiding { get; set; }
    
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }
    [Parameter] public required List<string> Options { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
   
    bool ShouldShowComponent()
    {
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

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}