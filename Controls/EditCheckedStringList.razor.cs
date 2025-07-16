namespace Controls;

public partial class EditCheckedStringList : IEditControl
{
    [CascadingParameter] EditContext EditContext { get; set; } = null!;
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public required List<string> Value { get; set; }
    [Parameter] public EventCallback<List<string>> ValueChanged { get; set; }
    [Parameter] public required Expression<Func<List<string>>> Field { get; set; }
    [Parameter] public string Css { get; set; } = "";
    [Parameter] public string? Id { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public List<string> Options { get; set; } = [];

    [Parameter] public string? LabelClass { get; set; }

    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? Placeholder { get; set; }

    /// <summary> Not supported yet </summary>
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public string? OuterClass { get; set; }

    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool hasError;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, null, _fieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        hasError = EditContext.GetValidationMessages(_fieldIdentifier).Any();
    }

    async Task SetAsync(string str)
    {
        if (Value.Contains(str))
        {
            Value.Remove(str);
        }
        else
        {
            Value.Add(str);
        }

        await ValueChanged.InvokeAsync(Value);
    }

    /// <summary> Not supported yet </summary>
    bool ShouldShowComponent() => true;

    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    

}