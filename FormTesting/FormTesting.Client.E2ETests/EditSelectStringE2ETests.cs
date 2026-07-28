using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectStringE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.SelectString;

    [Fact]
    public async Task First_select_renders_string_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator("select").First;
        var optionCount = await select.Locator("option").CountAsync();
        Assert.True(optionCount >= 2, $"Expected select to have at least 2 string options, got {optionCount}.");
    }
}
