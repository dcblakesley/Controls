namespace Controls;

/// <summary> Edit control for numeric values, displays as a number input. Supports custom formatting and step values.</summary>
public partial class EditNumber<T> : IEditControl
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
    /// <summary> Expression that binds to the numeric property in the model.</summary>
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    
    /// <summary> The increment/decrement step for the number input. Defaults to 1.0.</summary>
    [Parameter] public decimal Step { get; set; } = 1.0m;
    
    /// <summary> Optional format string for displaying the number in read-only mode (e.g., "N2" for 2 decimal places).</summary>
    [Parameter] public string? Format { get; set; }
    
    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;

    // Methods
    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, FieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var hidingMode = FormOptions?.Hiding ?? Hiding ?? HidingMode.None;

        if (hidingMode == HidingMode.None)
            return true;

        var isReadOnly = !((IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode));
        var value = Field.Compile().Invoke();

        return hidingMode switch
        {
            HidingMode.WhenReadOnlyAndNull => !isReadOnly || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => !isReadOnly || value != null && Convert.ToDouble(value) != 0,
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => value != null && Convert.ToDouble(value) != 0,
            _ => true
        };
    }
    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);
    
    string? GetFormattedNumber()
    {
        try
        {
            if (Value != null)
            {
                return Value switch
                {
                    decimal d => d.ToString(Format),
                    float f => f.ToString(Format),
                    double d => d.ToString(Format),
                    int i => i.ToString(Format),
                    long l => l.ToString(Format),
                    short s => s.ToString(Format),
                    byte b => b.ToString(Format),
                    sbyte sb => sb.ToString(Format),
                    uint ui => ui.ToString(Format),
                    ulong ul => ul.ToString(Format),
                    ushort us => us.ToString(Format),
                    _ => Value.ToString()
                };
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        return string.Empty;
    }
}