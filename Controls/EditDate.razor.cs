namespace Controls;

public partial class EditDate<T> : IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? ContainerClass { get; set; } 
    [Parameter] public bool IsRequired { get; set; }
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component-specific properties
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    [Parameter] public string DateFormat { get; set; } = "MM-dd-yyyy";

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

    // Methods
    protected override void OnInitialized()
    {
        base.OnInitialized();
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    string GetDisplayValue()
    {
        string valueAsString = CurrentValueAsString ?? string.Empty;
        if (string.IsNullOrEmpty(valueAsString))
            return string.Empty;
            
        return DateTime.Parse(valueAsString).ToUniversalTime().ToLocalTime().ToString(DateFormat);
    }
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        // Get effective hiding mode (component's setting overrides form's setting)
        var effectiveHidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        if (effectiveHidingMode == HidingMode.None)
            return true;

        // Use the Field expression to get the current value
        var value = Field.Compile()();

        // Check if value is null
        var isNull = value == null;

        // For date types, check if it's default (DateTime.MinValue, etc.)
        var isDefault = isNull || EqualityComparer<T>.Default.Equals(value, default);

        // Special handling for DateTime and DateTimeOffset
        if (!isNull && value is DateTime dateTime)
            isDefault = dateTime == default;
        else if (!isNull && value is DateTimeOffset dateTimeOffset)
            isDefault = dateTimeOffset == default;

        // Determine if we're in read-only mode
        var isReadOnly = !IsEditMode || (FormOptions != null && !FormOptions.IsEditMode);

        return effectiveHidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !(isReadOnly && isNull),
            HidingMode.WhenReadOnlyAndNullOrDefault => !(isReadOnly && isDefault),
            HidingMode.WhenNull => !isNull,
            HidingMode.WhenNullOrDefault => !isDefault,
            _ => true
        };
    }
}