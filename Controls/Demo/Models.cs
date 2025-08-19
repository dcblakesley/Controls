namespace Controls.Demo;

internal class DemoModelForEditControls
{
    // Controls
    [Required]
    [MinLength(5)]
    [DisplayName("EditString")]
    [ToolTip("AAAAAAAAA BBBBBBBBBBBBB")]
    public string? EditString { get; set; } = "";

    [DisplayName("EditTextArea")]
    [ToolTip("AAAAAAAAA BBBBBBBBBBBBB")]
    public string EditTextArea { get; set; } = "";

    [Required, DisplayName("EditNumber")]
    [Range(100.5, 110.85)]
    [ToolTip("AAAAAAAAA BBBBBBBBBBBBB")]
    public double? EditNumber { get; set; } = 0;

    [DisplayName("EditBool")]
    [ToolTip("I'm a tooltip!")]
    public bool EditBool { get; set; }

    [Required]
    [ToolTip("This is a tooltip for EditBoolNull. I want to see what happens when it gets really really really really really really really really long")] 
    [DisplayName("EditBoolNullRadio")] 
    public bool? EditBoolNullRadio { get; set; }

    [DisplayName("EditDate")]
    [ToolTip("AAAAAAAAA BBBBBBBBBBBBB")]
    public DateTime EditDate { get; set; } = DateTime.Now;

    [DisplayName("EditSelectEnum")]
    public Animal EditSelectEnum { get; set; }

    [DisplayName("EditSelect")] 
    public int EditSelect { get; set; }

    [Required]
    [DisplayName("EditSelectString")]
    public string? EditSelectString { get; set; } = "";

    [DisplayName("EditRadioString")]
    public string EditRadioString { get; set; } = "";

    [Required]
    [DisplayName("EditRadioEnum")]
    public Animal? EditRadioEnum { get; set; } = null;

    [DisplayName("EditRadio")]
    public int EditRadio { get; set; } = 0;

    [DisplayName("EditCheckedStringList")]
    [Description("I'm a little description, short and stout")]
    [Required]
    [MinLength(2)]
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
    public int Id { get; set; }
    public string Name { get; set; } = "";
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