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

    [Fact]
    public async Task IsOptionDisabled_disables_only_the_matching_radio()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Last;
        // Animal enum order: Cat, Dog, Bird, Fish -- Bird (index 2) is disabled by the demo's predicate.
        var radios = section.Locator("input[type=radio]");

        await Expect(radios.Nth(0)).ToBeEnabledAsync();  // Cat
        await Expect(radios.Nth(1)).ToBeEnabledAsync();  // Dog
        await Expect(radios.Nth(2)).ToBeDisabledAsync(); // Bird
        await Expect(radios.Nth(3)).ToBeEnabledAsync();  // Fish
    }
}
