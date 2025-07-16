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

    string DisplayLabel() => Label ?? Attributes.GetLabelText(FieldIdentifier);
    string? DisplayDescription() => Description ?? Attributes.Description();
    string _isRequired = "false";

    protected override void OnInitialized()
    {
        if (Attributes != null)
        {
            _isRequired = Attributes.Any(x => x is RequiredAttribute) ? "true" : "false";
        }
    }
}