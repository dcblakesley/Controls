namespace Controls;

public partial class EditDate<T>
{
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";
    [Parameter] public string? OuterClass { get; set; }
    [Parameter] public bool ShowTime { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldShowComponent => true;

    string GetDisplayValue() => DateTime.Parse(CurrentValueAsString).ToLocalTime().ToString(ShowTime ? "MM-dd-yyyy hh:mm tt" : "MM-dd-yyyy");


    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
                _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}