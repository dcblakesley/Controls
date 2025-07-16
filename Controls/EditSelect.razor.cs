namespace Controls;

/// <summary>
/// Select component where you create the options within the markup yourself. <br/>
/// If you want an Enum to back the select, use <see cref="EditSelectEnum{TValue}"/> instead. <br/>
/// If you want to use a list of strings to back the select, use <see cref="EditSelectString{TValue}"/> instead.
/// </summary>
public partial class EditSelect<TValue>
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true; 
    [Parameter] public string? OuterClass { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldShowComponent => true;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}