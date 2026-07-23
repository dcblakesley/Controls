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
        var section = Page.Locator("section.demo-section").Nth(11);
        // Animal enum order: Cat, Dog, Bird, Fish -- Bird (index 2) is disabled by the demo's predicate.
        var radios = section.Locator("input[type=radio]");

        await Expect(radios.Nth(0)).ToBeEnabledAsync();  // Cat
        await Expect(radios.Nth(1)).ToBeEnabledAsync();  // Dog
        await Expect(radios.Nth(2)).ToBeDisabledAsync(); // Bird
        await Expect(radios.Nth(3)).ToBeEnabledAsync();  // Fish
    }

    [Fact]
    public async Task Button_mode_renders_one_button_per_enum_value_and_selecting_one_checks_it()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(12); // OptionType.Button, Outline
        var buttons = section.Locator("label.edit-radio-button");
        await Expect(buttons.First).ToBeVisibleAsync();
        Assert.Equal(4, await buttons.CountAsync());

        var birdInput = section.Locator("input.edit-radio-button-input[value=Bird]");
        await Expect(birdInput).Not.ToBeCheckedAsync();
        await buttons.Filter(new() { HasTextString = "Tweety Bird" }).ClickAsync();
        await Expect(birdInput).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Button_mode_checked_style_differs_between_outline_and_solid()
    {
        await NavigateAsync();
        // Both demos default to Dog (the checked option).
        var outlineLabel = Page.Locator("section.demo-section").Nth(12).Locator("label.edit-radio-button").Filter(new() { HasTextString = "Puppy Dog" });
        var solidLabel = Page.Locator("section.demo-section").Nth(13).Locator("label.edit-radio-button").Filter(new() { HasTextString = "Puppy Dog" });

        var outlineBg = await outlineLabel.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        var solidBg = await solidLabel.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.NotEqual(outlineBg, solidBg);
    }

    [Fact]
    public async Task Button_mode_HasOtherOption_joins_the_row_and_keeps_the_text_input_separate()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(14);
        var buttons = section.Locator("label.edit-radio-button");
        await Expect(buttons.First).ToBeVisibleAsync();

        // The last enum value (Fish) is re-purposed as the Other button, so it still joins the row.
        var otherButton = buttons.Last;
        var textInput = section.Locator("input.edit-radio-other-input");
        await Expect(textInput).ToBeDisabledAsync(); // Other not selected yet

        await otherButton.ClickAsync();
        await Expect(textInput).ToBeEnabledAsync();
    }

    [Fact]
    public async Task Visual_baseline_button_mode_outline_section()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(12);
        await Expect(section).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(section, "button-mode-outline-section");
    }
}
