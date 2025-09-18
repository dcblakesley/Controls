namespace Controls;

/// <summary> Provides checkboxes for each input string (in Options), binds to a List of selected strings. </summary>
public partial class EditCheckedStringList : IEditControl
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
    [Parameter] public bool IsLabelHidden { get; set; }

    // IEditControl state properties
    [Parameter] public HidingMode? Hiding { get; set; }
    [Parameter] public bool IsHidden { get; set; }
    [Parameter] public bool IsEditMode { get; set; } = true;
    [Parameter] public bool IsDisabled { get; set; }
    
    // EditCheckedStringList specific
    [Parameter] public required List<string> Value { get; set; }
    [Parameter] public EventCallback<List<string>> ValueChanged { get; set; }
    [Parameter] public required Expression<Func<List<string>>> Field { get; set; }
    [Parameter] public List<string> Options { get; set; } = [];
    [Parameter] public string Css { get; set; } = "";

    /// <summary> Labels for the checkboxes. </summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally. </summary>
    [Parameter] public bool IsHorizontal { get; set; }

    // Fields
    string _id = string.Empty;
    string _isRequired = "false";
    List<Attribute>? _attributes;
    FieldIdentifier _fieldIdentifier;
    bool hasError;

    // Methods
    protected override void OnInitialized()
    {
        _fieldIdentifier = FieldIdentifier.Create(Field);
        _attributes = AttributesHelper.GetExpressionCustomAttributes(Field);
        _id = AttributesHelper.GetId(Id, FormGroupOptions, null, _fieldIdentifier);
        _isRequired = _attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }

    async Task SetAsync(string str)
    {
        if (Value.Contains(str))
            Value.Remove(str);
        else
            Value.Add(str);

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
    bool ShouldHideLabel => IsLabelHidden || (FormOptions?.IsLabelHidden ?? false);

                                
    [Parameter] public EventCallback<IReadOnlyList<string>> FieldChanged { get; set; }

}