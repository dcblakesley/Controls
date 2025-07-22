namespace Controls;

internal class Person
{
    // Controls
    [Required, DisplayName("EditString")] internal string? EditString { get; set; } = "";
    [DisplayName("EditTextArea")] internal string EditTextArea { get; set; } = "";
    [DisplayName("EditNumber")] internal double? EditNumber { get; set; } = 0;
    [DisplayName("EditBool")] internal bool EditBool { get; set; }
    [DisplayName("EditDate")] public DateTime EditDate { get; set; }
    [DisplayName("EditSelectEnum")] internal AnimalType EditSelectEnum { get; set; }
    [DisplayName("EditSelect")] internal int EditSelect { get; set; }
    [Required, DisplayName("EditSelectString")] internal string? EditSelectString { get; set; } = "";
    [DisplayName("EditRadioString"), Description("Hello, does this work?")] internal string EditRadioString { get; set; } = "";
    [Required, DisplayName("EditRadioEnum")] internal AnimalType? EditRadioEnum { get; set; } = AnimalType.Cat;
    [DisplayName("EditCheckedStringList"), Description("I'm a little description, short and stout")]
    internal List<string> EditCheckedStringList { get; set; } = [];
}

internal enum AnimalType
{
    Cat = 0,

    [EnumDisplayName("Puppy Dog")]
    Dog = 1,

    [EnumDisplayName("Tweety Bird")]
    Bird = 2,

    [EnumDisplayName("Gold Fish")]
    Fish = 3
}
internal class Animal
{
    [Required]
    internal string Name { get; set; } = "";
}

internal class Plant
{
    internal string Name { get; set; } = "";
    internal int Id { get; set; }
    public override string ToString() => Name;

    internal static List<Plant> GetTestData()
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