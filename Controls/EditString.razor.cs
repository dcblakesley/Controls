// ReSharper disable SimplifyConditionalTernaryExpression

namespace Controls;

public partial class EditString : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public required Expression<Func<string>> Field { get; set; }

    [Parameter] public string? Id { get; set; }  
    [Parameter] public string? IdPrefix { get; set; }

    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public string? OuterClass { get; set; }

    /// <summary> Non-Edit Mode only, MaskText is a string that will be displayed before the current value </summary>
    /// <example> MaskText='****-****-' with the value 'abcd-efgh-ijkl' would display '****-****-ijkl'</example>
    [Parameter] public string? MaskText { get; set; }

    /// <summary> Non-Edit mode will be a link </summary>
    [Parameter] public string? Url { get; set; }

    /// <summary> Only used with Urls, Sets target="UrlTarget" in the link </summary>
    [Parameter] public string? UrlTarget { get; set; }

    bool ShouldShowComponent()
    {
        switch (Hiding)
        {
            // Look at direct settings first, these override FormOptions
            case HidingMode.None:
                return true;
            case HidingMode.WhenNull:
                return CurrentValue != null;
            case HidingMode.WhenNullOrEmpty:
                return !string.IsNullOrEmpty(CurrentValue);
            case HidingMode.WhenReadOnlyAndNull:
                return !IsEditMode || CurrentValue != null;
            case HidingMode.WhenReadOnlyAndNullOrEmpty:
                return !IsEditMode || !string.IsNullOrEmpty(CurrentValue);

            // No direct setting, check FormOptions
            default:
                if (FormOptions == null)
                    return true;
                return FormOptions.Hiding switch
                {
                    null or HidingMode.None => true,
                    HidingMode.WhenNull => CurrentValue != null,
                    HidingMode.WhenNullOrEmpty => !string.IsNullOrEmpty(CurrentValue),
                    HidingMode.WhenReadOnlyAndNull => !IsEditMode || CurrentValue != null,
                    _ => true
                };
        }
    }

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
    string _isRequired;
    bool _showMaskedValue = false;
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}