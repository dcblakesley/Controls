using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditRadioEnumE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.RadioEnum;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditRadioEnum Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_radio_group_renders_one_radio_per_enum_value()
    {
        await NavigateAsync();
        var radios = Page.Locator("section.demo-section").First.Locator("input[type=radio]");
        var count = await radios.CountAsync();
        Assert.True(count >= 2, $"Expected first enum radio group to have multiple options, got {count}.");
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
