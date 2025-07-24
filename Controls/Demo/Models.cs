namespace Controls.Demo;

internal class DemoModelForEditControls
{
    // Controls
    [Required, DisplayName("EditString"), MinLength(5)] 
    public string? EditString { get; set; } = "";

    [DisplayName("EditTextArea")]
    public string EditTextArea { get; set; } = "";
    [DisplayName("EditNumber")] public double? EditNumber { get; set; } = 0;
    public bool EditBool { get; set; }
    [Required, DisplayName("EditBoolNullRadio")] public bool? EditBoolNullRadio { get; set; }
    [DisplayName("EditDate")] public DateTime EditDate { get; set; }
    [DisplayName("EditSelectEnum")] public Animal EditSelectEnum { get; set; }
    [DisplayName("EditSelect")] public int EditSelect { get; set; }
    [Required, DisplayName("EditSelectString")] public string? EditSelectString { get; set; } = "";
    [DisplayName("EditRadioString"), Description("Hello, does this work?")] public string EditRadioString { get; set; } = "";
    [Required, DisplayName("EditRadioEnum")] public Animal? EditRadioEnum { get; set; } = Animal.Cat;
    [DisplayName("EditRadio")] public int EditRadio { get; set; } = 0;
    [DisplayName("EditCheckedStringList"), Description("I'm a little description, short and stout")]
    public List<string> EditCheckedStringList { get; set; } = [];
}

public enum Animal
{
    Cat = 0,

    [EnumDisplayName("Puppy Dog")]
    Dog = 1,

    [EnumDisplayName("Tweety Bird")]
    Bird = 2,

    [EnumDisplayName("Gold Fish")]
    Fish = 3
}

public class Plant
{
    public string Name { get; set; } = "";
    public int Id { get; set; }
    public override string ToString() => Name;

    public static List<Plant> GetTestData()
    {
        return
        [
            new() { Id = 1, Name = "Rose" },
            new() { Id = 2, Name = "Daisy" },
            new() { Id = 3, Name = "Tulip" },
            new() { Id = 4, Name = "Daffodil" },
            new() { Id = 5, Name = "Lily" },
            new() { Id = 6, Name = "Orchid" },
            new() { Id = 7, Name = "Sunflower" },
        ];
    }
}