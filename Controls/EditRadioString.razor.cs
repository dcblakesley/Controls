namespace Controls;

public partial class EditRadioString : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    [Parameter] public required Expression<Func<string>> Field { get; set; }
    [Parameter] public required List<string> Options { get; set; }
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public string? ContainerClass { get; set; }

    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public bool IsHorizontal { get; set; }
    [Parameter] public bool HasOther { get; set; }

    /// <summary> The labels around each radio button </summary>
    [Parameter] public string? LabelClass { get; set; }

    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
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
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
        _selectedOption = Value;
    }

    async Task SetOtherTextAsync(string value)
    {
        _otherText = value;
        Value = value;
        await ValueChanged.InvokeAsync(value);
    }
}