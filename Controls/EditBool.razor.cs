using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public partial class EditBool
{
    [Parameter] public string? Id { get; set; } [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required Expression<Func<bool>> Field { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [CascadingParameter] public FormOptions? FormOptions { get; set; } [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    string DisplayLabel() => Label ?? _attributes.GetLabelText(FieldIdentifier);
    string? DisplayDescription() => Description ?? _attributes.Description();
    bool ShouldShowComponent => true;
    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions?.Name, FieldIdentifier);
    }
}