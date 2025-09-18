namespace Controls;

/// <summary> The Label and Description for a form field that shows up over the input. </summary>
public partial class FormLabel
{
    [Parameter] public string? Id { get; set; }
    [Parameter] public string? IdPrefix { get; set; }
    [Parameter] public required List<Attribute> Attributes { get; set; }
    [Parameter] public required FieldIdentifier FieldIdentifier { get; set; }
    [Parameter] public string? Label { get; set; }
    [Parameter] public string? Description { get; set; }
    [Parameter] public bool IsLegend { get; set; }
    [Parameter] public bool IsRequired { get; set; }
    [Parameter] public string? Tooltip { get; set; }
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