using System.ComponentModel.DataAnnotations;
using Controls.Helpers;

namespace Controls.Demo;

// Row/record types the UI-kit demo components bind their Tables to. Namespace-level rather than
// nested because several Demo* components share them; internal because they are demo fixtures, not
// package API.

internal record Row(int Id, string Name, decimal Price);

internal record Widget(int Id, string Name, string Code, int Quantity);

internal record Product(
    int Id,
    string Name,
    decimal Price,
    DateTime Added,
    bool InStock,
    ProductCategory Category,
    string Supplier);

internal record Ticket(int Id, string Region, string Status, string Tier);

internal record ShortRow(int Id, string Name, DateTime Due, int Count, bool Active);

internal record SkuRow(string Sku, string Description, int Quantity);

internal record PoRow(string Number, DateTime? Esd, string Tracking, List<SkuRow> Skus);

// Two of the four members carry an explicit label, so PropertyColumn.Filterable's derived option
// list shows off both attribute routes (and the camel-case auto-split for the two that don't).
internal enum ProductCategory
{
    [EnumDisplayName("Kitchen & bar")] Kitchen,
    [Display(Name = "Front of house")] FrontOfHouse,
    Refrigeration,
    Smallwares,
}
