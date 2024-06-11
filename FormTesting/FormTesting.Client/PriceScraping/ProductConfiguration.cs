
using FormTesting.Client.BasicEditors;
using MongoDB.Bson.Serialization.Attributes;

namespace FormTesting.Client.PriceScraping;

public static class FakePriceScrapingApp
{
    static Random _random = new Random();

    static bool _isInitialized = false;
    public static User User { get; set; } = new() { Id = 22777, Name = "Dave" };
    public static List<ProductScrapingConfiguration> ScrapingConfigurations { get; set; } = [];
    public static List<ScrapingResults> ScrapingResults { get; set; } = [];
    public static List<ItemsGroup> ItemsGroups { get; set; } = [];
    public static List<Schedule> Schedules { get; set; } = [];

    // Method to add test data
    public static void Initialize()
    {
        if (_isInitialized) return;
        
        User  = new();

        CreateScrapingConfigurations();
        CreateSchedules();
        CreateResults();
        _isInitialized = true;
    }

    static void CreateScrapingConfigurations()
    {
        ScrapingConfigurations =
        [
            new("950AGM24L", "AAA","GRILL COUNTER MANUAL LP 24X24,54K BTU, 4\"ADJ LEG", 1, GetDate(30), GetDate(2),
            [
                "https://www.webstaurantstore.com/wolf-agm24-nat-natural-gas-24-heavy-duty-gas-countertop-griddle-with-manual-controls-54-000-btu/950AGM24N.html",
                "https://www.katom.com/290-AGM24NG.html",
                "https://www.restaurantsupply.com/wolf-agm24-nat-natural-gas-24-heavy-duty-gas-countertop-griddle-with-2-burners-and-manual-controls-54-000-btu",
                "https://www.acitydiscount.com/Wolf-Commercial-24-W-x-24-Heavy-Duty-Manual-Countertop-Gas-Griddle-AGM24.0.222880.1.1.htm?ppcid=32&link=agm24&clk=222880&pos=1",
                "https://www.jesrestaurantequipment.com/wolf-agm24.html",
                "https://www.kitchenrestock.com/wolf-agm24_nat-heavy-duty-griddle-countertop-gas.html"
            ]),
            new("950AGM36Laa", "DDD", "Test Product 2", 1, GetDate(-30), GetDate(3), ["https://www.google.com"]),
            new("950AGM48Lbb","ABC", "Test Product 3", 1, GetDate(-30), GetDate(4), []),
            new("950AGM60Lcc", "ABC", "Test Product 4", 1, GetDate(-30), GetDate(5), []),
            new("950AGM72Ldd", "ZZZ", "Test Product 5", 1, GetDate(-30), GetDate(6), []),
            new("ab95", "ZZZ", "Test Product 6", 1, GetDate(-30), GetDate(7), []),
            new("ab99", "ZZZ", "Test Product 7", 1, GetDate(-30), GetDate(8), []),
            new("ab37", "ZZZ", "Test Product 8", 1, GetDate(-30), GetDate(9), []),
            new("abc5", "ZZZ", "Test Product 9", 1, GetDate(-30), GetDate(10), []),
            new("abc6", "ZZZ", "Test Product 10", 1, GetDate(-30), GetDate(11), []),

        ];
    }
    static void CreateResults()
    {
        // Scraping results for the first product
        var scrapingResults = new ScrapingResults()
        {
            Id = "1",
            ItemNumber = "950AGM24L",
            Timestamp = GetDate(1),
            Results =
            [
                new(
                    "https://www.webstaurantstore.com/wolf-agm24-nat-natural-gas-24-heavy-duty-gas-countertop-griddle-with-manual-controls-54-000-btu/950AGM24N.html",
                    1100, 900, 800),
                new("https://www.katom.com/290-AGM24NG.html", 1000, 900, 800),
                new(
                    "https://www.restaurantsupply.com/wolf-agm24-nat-natural-gas-24-heavy-duty-gas-countertop-griddle-with-2-burners-and-manual-controls-54-000-btu",
                    1200, 900, 800),
                new(
                    "https://www.acitydiscount.com/Wolf-Commercial-24-W-x-24-Heavy-Duty-Manual-Countertop-Gas-Griddle-AGM24.0.222880.1.1.htm?ppcid=32&link=agm24&clk=222880&pos=1",
                    900, 900, 800),
                new("https://www.jesrestaurantequipment.com/wolf-agm24.html", 500, 0, 0){HasIssues = true},
                new("https://www.kitchenrestock.com/wolf-agm24_nat-heavy-duty-griddle-countertop-gas.html", 1000, 900,
                    800),
            ]
        };
        ScrapingResults.Add(scrapingResults);
    }
    static void CreateSchedules()
    {
        Schedules =
        [
            new() { Id = "1", Name = "Critical", Value = 1, Type = ScheduleType.Days },
            new() { Id = "2", Name = "Very High Priority", Value = 1, Type = ScheduleType.Weeks },
            new() { Id = "3", Name = "High Priority", Value = 2, Type = ScheduleType.Weeks },
            new() { Id = "4", Name = "Normal Priority", Value = 1, Type = ScheduleType.Months },
            new() { Id = "5", Name = "Low Priority", Value = 3, Type = ScheduleType.Months },
            new() { Id = "6", Name = "Very Low Priority", Value = 6, Type = ScheduleType.Months },
        ];

    }

    static void CreateSubscriptions()
    {

    }
    static DateTime GetDate(int days) => DateTime.UtcNow.AddDays(-days);
}


/// <summary> Configuration for a Product. Allows the user to add Urls to be scraped and a schedule. </summary>
public class ProductScrapingConfiguration
{
    // full constructor
    public ProductScrapingConfiguration(){}
    public ProductScrapingConfiguration(string itemNumber, string categoryCode, string productName, int updatedBy, DateTime created, DateTime lastRun, List<string> urls)
    {
        Id = itemNumber;
        CategoryCode = categoryCode;
        foreach (var url in urls)
        {
            Urls.Add(new(url));
        }
        ProductName = productName;
        LastRun = lastRun;
    }

    [DisplayName("Item Number")]
    public string Id { get; set; } = "";
    public string CategoryCode { get; set; }
    public List<StringWrapper> Urls { get; set; } = [];
    public string ProductName { get; set; } = "";


    public DateTime LastRun { get; set; }
}

/// <summary> The result from a single URL. These are aggregated into a ScrapingResults object to ensure that the results are from the same time.</summary>
public class ScrapingResult
{
    // full Constructor
    public ScrapingResult(string url, decimal? pdpPrice, decimal? loggedInPrice, decimal? cartPrice, bool hasIssues = false, List<string>? issues = null)
    {
        Url = url;
        PdpPrice = pdpPrice;
        LoggedInPrice = loggedInPrice;
        CartPrice = cartPrice;
        HasIssues = hasIssues;
        Issues = issues;
    }

    public string Url { get; set; } = "";

    // Commonly retrieved data
    public decimal? PdpPrice { get; set; } 
    public decimal? LoggedInPrice { get; set; } 
    public decimal? CartPrice { get; set; }

    // Problems?
    public bool HasIssues { get; set; }
    public List<string>? Issues { get; set; }
}

/// <summary> A collection of scraping results for a single item. All scrapes for an Item should be done at the same time. </summary>
public class ScrapingResults
{
    public string? Id { get; set; }
    public string ItemNumber { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool HasIssues => Results.Any(r => r.HasIssues);

    public List<ScrapingResult> Results { get; set; } = [];
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

/// <summary> Define a set of items or categories that a user is subscribed to </summary>
public class ItemsGroup
{
    public string? Id { get; set; }

    public List<int> Subscribers { get; set; } = [];
    public string Name { get; set; } = "";
    // Description?
    public List<string> ItemNumbers { get; set; } = [];
    public List<int> CategoryIds { get; set; } = [];

    public List<string> CategoryItemNumbers()
    {
        return ["950AGM48Lbb", "950AGM60Lcc"];
    }
}

public class StringWrapper
{
    // Constructors
    public StringWrapper() { }
    public StringWrapper(string url) { Url = url; }
    public string Url { get; set; }
}

public class Schedule
{
    [BsonId]
    public string? Id { get; set; }
    public string Name { get; set; } = "";

    /// <summary> The number of days, weeks, or months </summary>
    public int Value { get; set; }
    public ScheduleType Type { get; set; }

}
public enum ScheduleType
{
    Days,
    Weeks,
    Months
}
