using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditRadioE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Radio;

    [Fact]
    public async Task First_radio_group_renders_consumer_defined_options()
    {
        await NavigateAsync();
        var radios = Page.Locator("section.demo-section").First.Locator("input[type=radio]");
        var count = await radios.CountAsync();
        Assert.True(count >= 2, $"Expected first radio group to have multiple options, got {count}.");
    }
}
