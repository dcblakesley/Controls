using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectStringE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.SelectString;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditSelectString Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_select_renders_string_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator("select").First;
        var optionCount = await select.Locator("option").CountAsync();
        Assert.True(optionCount >= 2, $"Expected select to have at least 2 string options, got {optionCount}.");
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
