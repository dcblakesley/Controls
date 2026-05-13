using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Select;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditSelect Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_select_renders_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator("select").First;
        var optionCount = await select.Locator("option").CountAsync();
        Assert.True(optionCount > 1, $"Expected first select to have multiple options, got {optionCount}.");
    }

    [Fact]
    public async Task Visual_baseline_basic_section()
    {
        await NavigateAsync();
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(firstSection, "basic-section");
    }
}
