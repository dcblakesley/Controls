namespace Controls;

/// <summary> Edit control for selecting an enum value using radio buttons. Supports sorting and an optional "Other" option with text input.</summary>
public partial class EditRadioEnum<TEnum> : IEditControl
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

    // Component specific parameters
    /// <summary> Expression that binds to the enum property in the model.</summary>
    [Parameter] public required Expression<Func<TEnum>> Field { get; set; }
    
    /// <summary> When true, displays radio buttons horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }
    
    /// <summary> When true, sorts the enum options alphabetically by their display name. When false, uses the enum's numeric order.</summary>
    [Parameter] public bool Sort { get; set; }

    /// <summary> The labels around each radio button</summary>
    [Parameter] public string? LabelClass { get; set; }

    // Other Option
    /// <summary> When true, includes an "Other" option with a text input field. The last enum value is treated as the "Other" option.</summary>
    [Parameter] public bool HasOtherOption { get; set; } = false;
    
    /// <summary> Placeholder text for the "Other" option text input.</summary>
    [Parameter] public string? OtherPlaceholder { get; set; }
    
    /// <summary> The text value entered in the "Other" option text input.</summary>
    [Parameter] public string? OtherValue { get; set; }
    
    /// <summary> Event callback that fires when the OtherValue changes.</summary>
    [Parameter] public EventCallback<string?> OtherValueChanged { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    Type _type = null!;
    Type _underlyingType = null!;
    bool _isNullable;
    List<TEnum?>? _cachedOptions;

    // Methods
    protected override void OnInitialized()
    {
        base.OnInitialized();
        (_id, _isRequired, _attributes, _fieldIdentifier) = EditControlInit.Init(Field, Id, FormGroupOptions, IdPrefix);

        // Handle nullable enum types
        _type = typeof(TEnum);
        _isNullable = Nullable.GetUnderlyingType(_type) != null;
        _underlyingType = _isNullable ? Nullable.GetUnderlyingType(_type)! : _type;
        _cachedOptions = BuildOptions();
    }
    List<TEnum?> GetOptions() => _cachedOptions!;
    List<TEnum?> BuildOptions()
    {
        var enumValues = Enum.GetValues(_underlyingType).Cast<TEnum>().ToList();

        // If HasOtherOption is true, remove the last enum value to add it back later
        TEnum? otherOption = default;
        if (HasOtherOption && enumValues.Count > 0)
        {
            otherOption = enumValues.Last();
            enumValues.RemoveAt(enumValues.Count - 1);
        }

        // Sort by the same display name the UI shows so sort order matches what the user sees.
        // EnumHelpers.GetName caches its lookup, so this stays cheap on subsequent renders.
        if (Sort)
            enumValues = enumValues.OrderBy(x => x!.GetName()).ToList();

        // Add back the "other" option at the end if it exists
        if (HasOtherOption && otherOption != null)
        {
            enumValues.Add(otherOption);
        }


        return enumValues.Cast<TEnum?>().ToList();
    }
    protected override bool TryParseValueFromString(string? value, out TEnum result, out string validationErrorMessage)
    {
        // Handle null/empty for nullable enums
        if (string.IsNullOrEmpty(value))
        {
            if (_isNullable)
            {
                result = default!;
                validationErrorMessage = null!;
                return true;
            }
            result = default!;
            validationErrorMessage = $"The {FieldIdentifier.FieldName} field is required.";
            return false;
        }

        // Try parsing the enum value
        if (Enum.TryParse(_underlyingType, value, out object? parsedValue))
        {
            result = (TEnum)parsedValue;
            validationErrorMessage = null!;
            return true;
        }

        result = default!;
        validationErrorMessage = $"The {FieldIdentifier.FieldName} field is not valid.";
        return false;
    }
    async Task OnOtherValueChanged(ChangeEventArgs e)
    {
        var value = e.Value?.ToString();
        if (OtherValue != value)
        {
            await OtherValueChanged.InvokeAsync(value);
        }
    }
    bool ShowEditor => EditControlInit.ShowEditor(IsEditMode, FormOptions);
    bool ShouldHideLabel => EditControlInit.ShouldHideLabel(IsLabelHidden, FormOptions);
    bool ShouldShowComponent()
    {
        if (IsHidden)
            return false;

        var hidingMode = Hiding ?? FormOptions?.Hiding ?? HidingMode.None;
        var value = Value;

        return hidingMode switch
        {
            HidingMode.None => true,
            HidingMode.WhenNull => value != null,
            HidingMode.WhenNullOrDefault => value != null && !value.Equals(default(TEnum)),
            HidingMode.WhenReadOnlyAndNull => IsEditMode || value != null,
            HidingMode.WhenReadOnlyAndNullOrDefault => IsEditMode || (value != null && !value.Equals(default(TEnum))),
            _ => true
        };
    }
}