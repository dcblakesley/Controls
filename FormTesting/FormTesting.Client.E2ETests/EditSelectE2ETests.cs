using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Select;

    [Fact]
    public async Task First_select_renders_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator("select").First;
        var optionCount = await select.Locator("option").CountAsync();
        Assert.True(optionCount > 1, $"Expected first select to have multiple options, got {optionCount}.");
    }
}
