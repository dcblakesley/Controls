// ReSharper disable SimplifyConditionalTernaryExpression

namespace Controls;

public partial class EditString : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public required Expression<Func<string>> Field { get; set; }

    [Parameter] public string? Id { get; set; }  
    [Parameter] public string? IdPrefix { get; set; }

    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? Placeholder { get; set; }
    [Parameter] public string? ContainerClass { get; set; }
    
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    /// <summary> Non-Edit Mode only, MaskText is a string that will be displayed before the current value </summary>
    /// <example> MaskText='****-****-' with the value 'abcd-efgh-ijkl' would display '****-****-ijkl'</example>
    [Parameter] public string? MaskText { get; set; }

    /// <summary> Non-Edit mode will be a link </summary>
    [Parameter] public string? Url { get; set; }

    /// <summary> Only used with Urls, Sets target="UrlTarget" in the link </summary>
    [Parameter] public string? UrlTarget { get; set; }

    bool ShouldShowComponent()
    {
        var value = CurrentValue;
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var isEditMode = (FormOptions?.IsEditMode ?? true) && IsEditMode;

        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenReadOnlyAndNull => isEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => isEditMode || !string.IsNullOrEmpty(value),
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => !string.IsNullOrEmpty(value),
            _ => true
        };
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    string? GetMaskValue()
    {
        if (string.IsNullOrEmpty(MaskText) || CurrentValue == null)
        {
            return CurrentValue;
        }

        return MaskText.Length > CurrentValue.Length 
            ? MaskText 
            : MaskText + CurrentValue[MaskText.Length..];
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