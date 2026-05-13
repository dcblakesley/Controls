namespace FormTesting.Client.E2ETests;

/// <summary>
/// Single xUnit collection shared by every e2e test class — keeps the same app + browser process
/// alive across the whole test run instead of recycling per class.
/// </summary>
[CollectionDefinition(Name)]
public class PlaywrightCollection : ICollectionFixture<AppFixture>, ICollectionFixture<BrowserFixture>
{
    public const string Name = "Playwright";
}
