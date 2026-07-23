using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditRadioStringE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.RadioString;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditRadioString Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_radio_group_renders_string_options()
    {
        await NavigateAsync();
        var radios = Page.Locator("section.demo-section").First.Locator("input[type=radio]");
        var count = await radios.CountAsync();
        Assert.True(count >= 2, $"Expected first string radio group to have multiple options, got {count}.");
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
        // _basicOptions order: Red, Green, Blue, Yellow -- Green (index 1) is disabled by the demo's predicate.
        var radios = section.Locator("input[type=radio]");

        await Expect(radios.Nth(0)).ToBeEnabledAsync();  // Red
        await Expect(radios.Nth(1)).ToBeDisabledAsync(); // Green
        await Expect(radios.Nth(2)).ToBeEnabledAsync();  // Blue
        await Expect(radios.Nth(3)).ToBeEnabledAsync();  // Yellow
    }
}
