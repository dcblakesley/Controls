namespace Controls;

/// <summary> The Label and Description for a form field that shows up over the input.</summary>
public partial class FormLabel
{
    [CascadingParameter] public FormOptions? FormOptions { get; set; }

    /// <inheritdoc cref="IEditControl.Id"/>
    [Parameter] public string? Id { get; set; }
    
    /// <inheritdoc cref="IEditControl.IdPrefix"/>
    [Parameter] public string? IdPrefix { get; set; }
    
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    
    /// <inheritdoc cref="IEditControl.Label"/>
    [Parameter] public string? Label { get; set; }
    
    /// <inheritdoc cref="IEditControl.Description"/>
    [Parameter] public string? Description { get; set; }
    
    /// <summary> Used when a legend is more appropriate than a label such as when you have a group of radio buttons</summary>
    [Parameter] public bool IsLegend { get; set; }
    
    /// <inheritdoc cref="IEditControl.IsRequired"/>
    [Parameter] public bool IsRequired { get; set; }
    
    /// <inheritdoc cref="IEditControl.Tooltip"/>
    [Parameter] public string? Tooltip { get; set; }
    
    /// <inheritdoc cref="IEditControl.IsLabelHidden"/>
    [Parameter] public bool IsLabelHidden { get; set; }

    /// <summary>
    /// False when the paired element is not labelable (the read-only view renders a div, and
    /// <c>label[for]</c> must reference a labelable element) — the label then renders without
    /// <c>for</c>; the read-only view names itself via <c>aria-labelledby</c>. Controls pass
    /// <c>ShowEditor</c>. Defaults to true.
    /// </summary>
    [Parameter] public bool IsForLabelable { get; set; } = true;

    // Resolved once per parameter-change cycle; the razor binds to these instead of calling the
    // helpers on every render path (the legend + label branches in FormLabel.razor evaluate
    // DisplayLabel/DisplayDescription twice otherwise).
    string _label = string.Empty;
    string? _description;
    bool _isRequired;

    string DisplayLabel() => _label;
    string? DisplayDescription() => _description;

    protected override void OnParametersSet()
    {
        // Attributes can be null when FormLabel is used outside the Edit* controls (EditDisplay supplies
        // no attribute list). Fall back to the explicit Label/Description rather than dropping them, and
        // never call GetLabelText with the default FieldIdentifier (its FieldName would be null).
        _label = Label ?? Attributes?.GetLabelText(FieldIdentifier) ?? string.Empty;
        _description = Description ?? Attributes?.Description();
        _isRequired = Attributes?.Any(x => x is RequiredAttribute) ?? false;
    }
}