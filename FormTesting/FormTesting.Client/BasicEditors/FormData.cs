using Microsoft.AspNetCore.Components.Forms;
using System.Linq.Expressions;

namespace FormTesting.Client.BasicEditors;

public class FormData
{
    public FormData(FieldIdentifier fieldIdentifier, List<Attribute> attributes)
    {
        FieldIdentifier = fieldIdentifier;
        Attributes = attributes;
        Name = AttributesHelper.GetLabelText(Attributes, FieldIdentifier);
        Id = AttributesHelper.GetId(Id, Name);
        Description = AttributesHelper.GetDescription(Attributes);
    }

    public FieldIdentifier FieldIdentifier { get; set; }
    public EditContext EditContext { get; set; }
    public List<Attribute> Attributes { get; set; }
    public string? Description { get; set; }
    public string Id { get; set; }
    public string Name { get; set; }
    public bool IsRequired { get; set; }
}