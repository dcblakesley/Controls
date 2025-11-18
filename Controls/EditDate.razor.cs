namespace Controls;

/// <summary> Edit control for date and date/time values, displays as a date input with customizable format.</summary>
public partial class EditDate<T> : IEditControl
{
    // Cascading parameters
    [CascadingParameter] public FormOptions? FormOptions { get; set; } 
    [CascadingParameter] public FormGroupOptions? FormGroupOptions { get; set; }

    // IEditControl interface properties
    /// <inheritdoc/>
    [Parameter] public string? Id { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? IdPrefix { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? Label { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? Description { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? Tooltip { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public string? ContainerClass { get; set; } 
    /// <inheritdoc/>
    [Parameter] public bool IsRequired { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    /// <inheritdoc/>
    [Parameter] public HidingMode? Hiding { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsHidden { get; set; }
    
    /// <inheritdoc/>
    [Parameter] public bool IsEditMode { get; set; } = true;
    
    /// <inheritdoc/>
    [Parameter] public bool IsDisabled { get; set; }

    // Component-specific properties
    /// <summary> Expression that binds to the date/datetime property in the model.</summary>
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    
    /// <summary> Format string for displaying the date in read-only mode. Defaults to "MM-dd-yyyy".</summary>
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