using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Controls.Helpers;

namespace FormTesting.Client;

public class Person
{
    [Required] public string StringSelect { get; set; } = "";

    [ValidateComplexType]
    public Animal Animal { get; set; } = new();

    // Width
    public int EditorWidth { get; set; } = 400;
    [MinLength(1)]
    public string? Label { get; set; }

    // Labels
    public string Name { get; set; } = "Green";
    public string FirstName { get; set; } = "Green";
    [DisplayName("MiddleName modified with [DisplayNameAttribute]")]
    public string MiddleName { get; set; } = "";
    [DisplayName("<span style='color: cyan'>Last </span><span style='color: lime'>Name</span><span style='color: magenta'> with embedded html</span>")]
    public string LastName { get; set; } = "";

    // Descriptions
    [Description("This is a description for Dog")]
    public string Dog { get; set; } = "";
    [Description("<div style='color: lime; font-weight: bold'>Cats don't like dogs or water</div>")]
    public string Cat { get; set; } = "";

    // Validation
    [Required]
    public string Horse { get; set; } = "";
    [Required(ErrorMessage = "<div style='color: lime; font-weight: bold'>Required: Zebras don't like<ul><li style=\"color: red\">Lions</li><li style=\"color: cyan\">Newspapers</li></ul></div>")]
    public string Zebra { get; set; } = "";
    [MinLength(3)] public string Lion { get; set; } = "a";
    [MaxLength(5)] public string Tiger { get; set; } = "abcdefghi";

    // Controls
    [DisplayName("EditString")] public string EditString { get; set; } = "";
    [DisplayName("EditTextArea")] public string EditTextArea { get; set; } = "";
    [DisplayName("EditNumber")] public double EditNumber { get; set; } = 2.5;
    [DisplayName("EditBool")] public bool EditBool { get; set; }
    [DisplayName("EditDate")] public DateTime EditDate { get; set; } = DateTime.UtcNow;
    [DisplayName("EditSelectEnum")] public AnimalType EditSelectEnum { get; set; }
    [DisplayName("EditSelect")] public int EditSelect { get; set; }
    [DisplayName("EditRadioString")] public string EditRadioString { get; set; } = "";
    [DisplayName("EditRadioEnum")] public AnimalType EditRadioEnum { get; set; } = AnimalType.Fish;

}

public enum AnimalType
{
    Cat = 0,

    [EnumDisplayName("Puppy Dog")]
    Dog = 1,

    [EnumDisplayName("Tweety Bird")]
    Bird = 2,

    [EnumDisplayName("Gold Fish")]
    Fish = 3
}
public class Animal
{
    [Required]
    public string Name { get; set; } = "";
}