namespace Controls;

public partial class EditDate<T> : IEditControl
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl properties
    [Parameter] public string? Id { get; set; } 
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public string? ContainerClass { get; set; }

    // Component-specific properties
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);

    string GetDisplayValue() => DateTime.Parse(CurrentValueAsString!).ToUniversalTime().ToLocalTime().ToString(DateFormat);
    bool ShouldShowComponent()
    {
        // Determine the effective hiding mode: direct parameter overrides FormOptions
        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        // Get the current value as string (null if not set)
        var value = CurrentValueAsString;

        // Apply hiding logic
        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => !string.IsNullOrEmpty(value),
            HidingMode.WhenReadOnlyAndNull => !IsEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => !IsEditMode || !string.IsNullOrEmpty(value),
            _ => true
        };
    }
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}