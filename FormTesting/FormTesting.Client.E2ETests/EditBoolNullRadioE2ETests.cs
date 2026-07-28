using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditBoolNullRadioE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.BoolNullRadio;

    [Fact]
    public async Task Basic_section_renders_three_radio_options_yes_no_not_set()
    {
        await NavigateAsync();
        var radios = Page.Locator("section.demo-section").First.Locator("input[type=radio]");
        await Expect(radios).ToHaveCountAsync(3);
    }
}
