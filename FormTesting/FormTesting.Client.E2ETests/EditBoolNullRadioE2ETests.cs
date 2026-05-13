using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditBoolNullRadioE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.BoolNullRadio;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditBoolNullRadio Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Basic_section_renders_three_radio_options_yes_no_not_set()
    {
        await NavigateAsync();
        var radios = Page.Locator("section.demo-section").First.Locator("input[type=radio]");
        await Expect(radios).ToHaveCountAsync(3);
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
