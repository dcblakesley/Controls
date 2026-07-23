using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditBoolE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Bool;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditBool Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_basic_checkbox_toggles_value()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var checkbox = Page.Locator("section.demo-section").First.Locator("input[type=checkbox]").First;
        await Expect(checkbox).Not.ToBeCheckedAsync();
        await checkbox.CheckAsync();
        await Expect(checkbox).ToBeCheckedAsync();

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("True");
    }

    [Fact]
    public async Task Read_only_renders_TrueText_FalseText_by_default()
    {
        await NavigateAsync();
        // Check the box first so we have a true value to read back.
        await Page.Locator("section.demo-section").First.Locator("input[type=checkbox]").First.CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToContainTextAsync("Yes");
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
    public async Task Clicking_styled_checkbox_toggles_value()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var section = Page.Locator("section.demo-section").Nth(4);
        var checkbox = section.Locator("input[type=checkbox]").First;
        await Expect(checkbox).Not.ToBeCheckedAsync();
        await checkbox.CheckAsync();
        await Expect(checkbox).ToBeCheckedAsync();

        await Expect(section.Locator(".bound-value").First).ToContainTextAsync("True");
    }

    [Fact]
    public async Task Tabbing_to_styled_checkbox_shows_focus_visible_outline_and_Space_toggles_it()
    {
        await NavigateAsync();
        var checkbox = Page.Locator("section.demo-section").Nth(4).Locator("input.edit-checkbox-input-styled").First;
        var box = Page.Locator("section.demo-section").Nth(4).Locator(".edit-checkbox-box").First;

        // .FocusAsync() (calling the DOM .focus() directly) doesn't reliably trigger :focus-visible
        // — programmatically focus the preceding section's checkbox as a neutral starting point,
        // then move onto the target via a real Tab keypress (mirrors EditStringE2ETests' pattern).
        var previousCheckbox = Page.Locator("section.demo-section").Nth(3).Locator("input[type=checkbox]").First;
        await previousCheckbox.EvaluateAsync("el => el.focus()");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(checkbox).ToBeFocusedAsync();

        var outline = await box.EvaluateAsync<string>("el => getComputedStyle(el).outlineStyle");
        Assert.NotEqual("none", outline);

        await Expect(checkbox).Not.ToBeCheckedAsync();
        await Page.Keyboard.PressAsync("Space");
        await Expect(checkbox).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Styled_checkbox_required_validation_marks_it_invalid_when_unchecked()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(5);
        var checkbox = section.Locator("input.edit-checkbox-input-styled").First;

        await Expect(checkbox).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(section.Locator(".edit-validation-message").First).ToBeVisibleAsync();

        await checkbox.CheckAsync();
        await Expect(checkbox).Not.ToHaveAttributeAsync("aria-invalid", "true");
    }

    [Fact]
    public async Task Visual_baseline_styled_checkbox_section()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(4);
        await Expect(section).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(section, "styled-checkbox-section");
    }

    [Fact]
    public async Task Indeterminate_sets_the_DOM_property_without_checking_the_box()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(6);
        var checkbox = section.Locator("input[type=checkbox]").First;

        // The demo starts with Indeterminate=true and the value unchecked.
        await Expect(checkbox).Not.ToBeCheckedAsync();
        Assert.True(await checkbox.EvaluateAsync<bool>("el => el.indeterminate"));

        // Toggling the parameter off clears the DOM property (JS re-applied, not just skipped).
        await section.Locator("button", new() { HasTextString = "Toggle indeterminate" }).ClickAsync();
        await Expect(checkbox).Not.ToBeCheckedAsync();
        Assert.False(await checkbox.EvaluateAsync<bool>("el => el.indeterminate"));

        // Indeterminate is visual-only: clicking the checkbox still toggles the bound value normally.
        await checkbox.CheckAsync();
        await Expect(checkbox).ToBeCheckedAsync();
    }

    [Fact]
    public async Task Indeterminate_styled_checkbox_draws_the_mixed_square_not_the_checkmark()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(7);
        var input = section.Locator("input.edit-checkbox-input-styled").First;
        var box = section.Locator(".edit-checkbox-box").First;
        await Expect(input).ToBeVisibleAsync();

        Assert.True(await input.EvaluateAsync<bool>("el => el.indeterminate"));
        var mixedBoxColor = await box.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        var mixedSquareColor = await box.EvaluateAsync<string>("el => getComputedStyle(el, '::after').backgroundColor");
        // AntD's mixed state keeps the box unfilled -- the primary color only appears in the ::after square.
        Assert.NotEqual(mixedBoxColor, mixedSquareColor);

        // Checking it (still indeterminate) then flips to the filled/checkmark look once Indeterminate
        // is toggled off, matching Table's identical header-checkbox behavior.
        await section.Locator("button", new() { HasTextString = "Toggle indeterminate" }).ClickAsync();
        Assert.False(await input.EvaluateAsync<bool>("el => el.indeterminate"));
    }

    [Fact]
    public async Task Visual_baseline_indeterminate_styled_section()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(7);
        await Expect(section).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(section, "indeterminate-styled-section");
    }

    [Fact]
    public async Task Indeterminate_survives_a_runtime_IsLabelHidden_flip()
    {
        // EditBool.razor renders the checkbox in two different tree positions depending on
        // ShouldHideLabel (wrapped inside the visible <label> vs. as a sibling of a visually-hidden
        // one) -- flipping that parameter at runtime remounts the <input>, which resets the DOM
        // `indeterminate` property to false. The mirror in EditBool.razor.cs must notice the remount
        // and re-apply Indeterminate rather than trusting its stale "already synced" flag.
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Nth(8);
        var checkbox = section.Locator("input[type=checkbox]").First;

        Assert.True(await checkbox.EvaluateAsync<bool>("el => el.indeterminate"));

        await section.Locator("button", new() { HasTextString = "Toggle hide label" }).ClickAsync();

        checkbox = section.Locator("input[type=checkbox]").First;
        Assert.True(await checkbox.EvaluateAsync<bool>("el => el.indeterminate"));

        // Flip back -- the mirror must re-sync on the return trip too, not just the first flip.
        await section.Locator("button", new() { HasTextString = "Toggle hide label" }).ClickAsync();
        checkbox = section.Locator("input[type=checkbox]").First;
        Assert.True(await checkbox.EvaluateAsync<bool>("el => el.indeterminate"));
    }
}
