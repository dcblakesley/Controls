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

    string DisplayLabel() => Label ?? Attributes.GetLabelText(FieldIdentifier);
    string? DisplayDescription() => Description ?? Attributes.Description();

    string _isRequired = "false";

    protected override void OnInitialized()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (Attributes == null)
            return;
        _isRequired = Attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
    }
}