using Controls.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls;

public partial class EditString
{
    [Parameter] public required Expression<Func<string>> Field { get; set; }

    /// <summary> IDs are used for the label and input. If not provided, the Id will be automatically generated based on the name of the Property. </summary>
    [Parameter] public string? Id { get; set; }  
    [Parameter] public string? IdPrefix { get; set; }

    /// <summary> Optional, can be used to distinguish between multiple forms on the same page. </summary>
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? Placeholder { get; set; }

    /// <summary> Non-Edit Mode only, MaskText is a string that will be displayed before the current value </summary>
    /// <example> MaskText='****-****-' with the value 'abcd-efgh-ijkl' would display '****-****-ijkl'</example>
    [Parameter]
    public string? MaskText { get; set; }

    /// <summary> Non-Edit mode will be a link </summary>
    [Parameter]
    public string? Url { get; set; }

    /// <summary> Only used with Urls, Sets target="UrlTarget" in the link </summary>
    [Parameter]
    public string? UrlTarget { get; set; }

    bool ShouldShowComponent => true;
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    string? GetMaskValue()
    {
        if (MaskText == null || CurrentValue == null)
        {
            return CurrentValue;
        }

        // If the mask is longer than the current value, return the mask
        if (MaskText.Length > CurrentValue.Length)
        {
            return MaskText;
        }

        var output = MaskText + CurrentValue.Substring(MaskText.Length);
        return output;
    }

    string _id = string.Empty;
    bool _showMaskedValue = false;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions?.Name, _fieldIdentifier);
    }
}