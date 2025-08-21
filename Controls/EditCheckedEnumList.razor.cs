namespace Controls;

/// <summary>
/// Provides checkboxes for each enum value, binds to a List of selected enum values.
/// Combines enum handling from EditSelectEnum/EditRadioEnum with checkbox functionality from EditCheckedStringList.
/// </summary>
public partial class EditCheckedEnumList<TEnum> : IEditControl
{
    [CascadingParameter] EditContext EditContext { get; set; } = null!;

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

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    
    // EditCheckedEnumList specific
    [Parameter] public required List<TEnum> Value { get; set; }
    [Parameter] public EventCallback<List<TEnum>> ValueChanged { get; set; }
    [Parameter] public required Expression<Func<List<TEnum>>> Field { get; set; }

    /// <summary> If true, the enum values will be sorted by their display names. </summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary> Labels for the checkboxes. </summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally. </summary>
    [Parameter] public bool IsHorizontal { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    Type _type;
    Type _underlyingType;
    bool _isNullable;

    // Methods
    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, IdPrefix, _fieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";

        // Handle nullable enum types
        _type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(_type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(_type)! : _type;
    }

    List<TEnum> GetOptions()
    {
        var enumValues = Enum.GetValues(_underlyingType).Cast<TEnum>().ToList();

        // Sort values if needed
        if (Sort)
        {
            // Sort by display name that would appear in the UI
            enumValues = enumValues.OrderBy(x =>
            {
                // Get display name from DisplayAttribute if present
                var memberInfo = _underlyingType.GetMember(x.ToString() ?? string.Empty);
                if (memberInfo.Length > 0)
                {
                    var displayAttr = memberInfo[0].GetCustomAttribute<DisplayAttribute>();
                    if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
                    {
                        return displayAttr.Name;
                    }
                }
                // Otherwise use enum name or GetName extension
                return x.GetName();
            }).ToList();
        }

        return enumValues;
    }

    async Task SetAsync(TEnum enumValue)
    {
        if (Value.Contains(enumValue))
            Value.Remove(enumValue);
        else
            Value.Add(enumValue);

        // Notify EditContext about the change
        EditContext?.NotifyFieldChanged(_fieldIdentifier);
        await ValueChanged.InvokeAsync(Value);
    }

    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;
        
        // Get effective hiding mode (component's setting overrides form's setting)
        var effectiveHidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;

        if (effectiveHidingMode == HidingMode.None)
            return true;

        // Check if list is null or empty
        var isNull = Value == null;
        var isDefault = isNull || Value.Count == 0;

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

    bool ShowEditor => (IsEditMode && FormOptions == null) || (IsEditMode && FormOptions!.IsEditMode);
}