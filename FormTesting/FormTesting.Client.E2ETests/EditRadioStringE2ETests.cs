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
        var section = Page.Locator("section.demo-section").Nth(5);
        // _basicOptions order: Red, Green, Blue, Yellow -- Green (index 1) is disabled by the demo's predicate.
        var radios = section.Locator("input[type=radio]");

        await Expect(radios.Nth(0)).ToBeEnabledAsync();  // Red
        await Expect(radios.Nth(1)).ToBeDisabledAsync(); // Green
        await Expect(radios.Nth(2)).ToBeEnabledAsync();  // Blue
        await Expect(radios.Nth(3)).ToBeEnabledAsync();  // Yellow
    }

    [Fact]
    public async Task Button_mode_renders_a_joined_button_row_and_selecting_one_checks_it()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(6); // OptionType.Button, Outline
        var buttons = section.Locator("label.edit-radio-button");
        await Expect(buttons.First).ToBeVisibleAsync();
        Assert.Equal(4, await buttons.CountAsync());

        // Clicking the visible button label toggles the hidden radio input behind it (native for/id
        // association) -- the standard visually-hidden-input + label technique needs no JS.
        var greenInput = section.Locator("input.edit-radio-button-input[value=Green]");
        await Expect(greenInput).Not.ToBeCheckedAsync();
        await buttons.Filter(new() { HasTextString = "Green" }).ClickAsync();
        await Expect(greenInput).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Button_mode_checked_style_differs_between_outline_and_solid()
    {
        await NavigateAsync();
        var outlineLabel = Page.Locator("section.demo-section").Nth(6).Locator("label.edit-radio-button").Filter(new() { HasTextString = "Blue" });
        var solidLabel = Page.Locator("section.demo-section").Nth(7).Locator("label.edit-radio-button").Filter(new() { HasTextString = "Blue" });

        // Both demos start on "Blue" (the checked option) per the model defaults.
        var outlineBg = await outlineLabel.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        var solidBg = await solidLabel.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.NotEqual(outlineBg, solidBg); // solid fills with the primary color; outline does not
    }

    [Fact]
    public async Task Button_mode_ignores_IsHorizontal_and_composes_with_Size()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(8); // Size=Small / Size=Large
        var smallButton = section.Locator("label.edit-radio-button").First;
        var largeButton = section.Locator("label.edit-radio-button").Last;

        var smallHeight = await smallButton.EvaluateAsync<double>("el => el.getBoundingClientRect().height");
        var largeHeight = await largeButton.EvaluateAsync<double>("el => el.getBoundingClientRect().height");
        Assert.True(largeHeight > smallHeight, $"Expected the Large button ({largeHeight}px) to be taller than Small ({smallHeight}px).");
    }

    [Fact]
    public async Task Button_mode_HasOther_joins_the_row_and_keeps_the_text_input_separate()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(9);
        var otherButton = section.Locator("label.edit-radio-button").Filter(new() { HasTextString = "Other" });
        await Expect(otherButton).ToBeVisibleAsync();

        var textInput = section.Locator("#txt-ButtonWithOther-custom-value");
        await Expect(textInput).ToBeDisabledAsync(); // Other not selected yet

        await otherButton.ClickAsync();
        await Expect(textInput).ToBeEnabledAsync();
        await textInput.FillAsync("custom value");
        await Expect(textInput).ToHaveValueAsync("custom value");
    }

    [Fact]
    public async Task Button_mode_IsOptionDisabled_disables_the_matching_button()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(10);
        var greenInput = section.Locator("input.edit-radio-button-input[value=Green]");
        await Expect(greenInput).ToBeDisabledAsync();
    }

    [Fact]
    public async Task Visual_baseline_button_mode_outline_section()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(6);
        await Expect(section).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(section, "button-mode-outline-section");
    }

    [Fact]
    public async Task Visual_baseline_button_mode_solid_section()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(7);
        await Expect(section).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(section, "button-mode-solid-section");
    }
}
