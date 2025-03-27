using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public partial class EditRadioString
{
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required Expression<Func<string>> Field { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public required List<string> Options { get; set; }
    [Parameter] public bool HasHorizontalRadioButtons { get; set; }
    [Parameter] public bool HasOther { get; set; }
    [CascadingParameter] public FormOptions? FormOptions { get; set; } [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldShowComponent => true;
    string _otherText = "";
    const string OtherName = "Other";
    string? _selectedOption;

    string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            _selectedOption = value;
            if (value == "Other")
            {
                Value = _otherText;
                ValueChanged.InvokeAsync(_otherText);
            }
            else
            {
                Value = value;
                _otherText = "";
                ValueChanged.InvokeAsync(value);
            }
        }
    }

    string _id = string.Empty;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, IdPrefix, _fieldIdentifier);
        _selectedOption = Value;
    }

    async Task SetOtherTextAsync(string value)
    {
        _otherText = value;
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }
}