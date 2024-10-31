using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public partial class EditNumber<T>
{
    [Parameter] public string? Id { get; set; }
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public decimal Step { get; set; }
    [Parameter] public string? Label { get; set; }
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldShowComponent => true;
    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, _fieldIdentifier);
    }
}