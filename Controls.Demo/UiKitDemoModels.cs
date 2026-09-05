using System.ComponentModel.DataAnnotations;
using Controls.Helpers;

namespace Controls.Demo;

// Row/record types the UI-kit demo components bind their Tables to. Namespace-level rather than
// nested because several Demo* components share them.

public record Row(int Id, string Name, decimal Price);

public record Widget(int Id, string Name, string Code, int Quantity);

public record Product(
    int Id,
    string Name,
    decimal Price,
    DateTime Added,
    bool InStock,
    ProductCategory Category,
    string Supplier);

public record Ticket(int Id, string Region, string Status, string Tier);

public record ShortRow(int Id, string Name, DateTime Due, int Count, bool Active);

internal record SkuRow(string Sku, string Description, int Quantity);

internal record PoRow(string Number, DateTime? Esd, string Tracking, List<SkuRow> Skus);

// Two of the four members carry an explicit label, so PropertyColumn.Filterable's derived option
// list shows off both attribute routes (and the camel-case auto-split for the two that don't).
public enum ProductCategory
{
    [EnumDisplayName("Kitchen & bar")] Kitchen,
    [Display(Name = "Front of house")] FrontOfHouse,
    Refrigeration,
    Smallwares,
}
