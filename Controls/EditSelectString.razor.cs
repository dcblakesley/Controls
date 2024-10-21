using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

/// <summary> Select a string from Options (List of strings) </summary>
public partial class EditSelectString<TValue>
{
    [Parameter] public string? Id { get; set; }
    [Parameter] public required Expression<Func<TValue>> Field { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public required List<string> Options { get; set; }
    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, _fieldIdentifier);
    }
}