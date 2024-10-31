namespace FormTesting.Client;

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