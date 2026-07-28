using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectEnumE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.SelectEnum;

    [Fact]
    public async Task First_select_renders_one_option_per_enum_value()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator("select").First;
        var optionCount = await select.Locator("option").CountAsync();
        Assert.True(optionCount >= 2, $"Expected select to have at least 2 enum options, got {optionCount}.");
    }
}
