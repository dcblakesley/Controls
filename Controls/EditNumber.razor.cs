namespace Controls;

public partial class EditNumber<T> : IEditControl
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

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }

    // Component-specific properties
    [Parameter] public required Expression<Func<T>> Field { get; set; }
    [Parameter] public decimal Step { get; set; }
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