
using FormTesting.Client.BasicEditors;

namespace FormTesting.Client.PriceScraping;

public static class FakePriceScrapingApp
{
    public static List<ProductScrapingConfiguration> ScrapingConfigurations { get; set; } = [];
    public static List<ScrapingResult> ScrapingResults { get; set; } = [];

    // public static void AddTestData()
    // public static void AddTestProductScrapingConfigurations()


}

public class ProductScrapingConfiguration
{
    public ProductScrapingConfiguration(){}
    // full constructor
    public ProductScrapingConfiguration(string id, int categoryId, List<string> urls, string productName, int updatedBy, DateTime created, DateTime lastUpdated, DateTime lastRun)
    {
        Id = id;
        CategoryId = categoryId;
        Urls = urls;
        ProductName = productName;
        UpdatedBy = updatedBy;
        Created = created;
        LastUpdated = lastUpdated;
        LastRun = lastRun;
    }

    [DisplayName("Item Number")]
    public string Id { get; set; } = "";
    public int CategoryId { get; set; }
    public List<string> Urls { get; set; } = [];
    public string ProductName { get; set; } = "";
    public int UpdatedBy { get; set; }

    public DateTime Created { get; set; }
    public DateTime LastUpdated { get; set; }
    public DateTime LastRun { get; set; }
}

public class ScrapingResult
{
    public string? Id { get; set; }
    public string ItemNumber { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // Commonly retrieved data
    public decimal? PdpPrice { get; set; } 
    public decimal? LoggedInPrice { get; set; } 
    public decimal? CartPrice { get; set; }

    // Problems?
    public bool HasIssues { get; set; }
    public List<string>? Issues { get; set; }
}

public class ScrapingUser
{
    public int Id { get; set; }
}


