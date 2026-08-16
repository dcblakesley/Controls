using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditNumberE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Number;

    [Fact]
    public async Task First_number_input_accepts_value_and_round_trips_to_bound_display()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var input = Page.Locator("section.demo-section").First.Locator("input[type=number]").First;
        await input.FillAsync("42");
        await input.PressAsync("Tab");

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("42");
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_swaps_inputs_for_ReadOnlyValue()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator("input[type=number]").First).Not.ToBeVisibleAsync();
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToBeVisibleAsync();
    }

    // Section order on the demo page: 0 basic, 1 disabled, 2 custom style, 3 validation,
    // 4 Min/Max/Placeholder, 5 Prefix/Suffix, 6 placeholder attribute, 7 Min/Max attribute,
    // 8 ShowStepper.
    ILocator StepperSection => Page.Locator("section.demo-section").Nth(8);

    [Fact]
    public async Task A_real_click_on_the_plus_button_increments_the_displayed_value()
    {
        // The bUnit suite covers the arithmetic through synthetic clicks; this proves the button is
        // actually hit-testable in a browser -- it sits inside a joined group whose adjoining borders
        // overlap by a negative margin, which is exactly the sort of layout that can put a neighbour
        // on top of the target.
        await NavigateAsync();
        var field = StepperSection.Locator(".edit-number-stepper", new() { Has = Page.Locator("#StepperBasic") });
        var input = field.Locator("input[type=number]");
        await Expect(input).ToHaveValueAsync("3");

        await field.Locator(".edit-number-step-up").ClickAsync();
        await Expect(input).ToHaveValueAsync("4");

        await field.Locator(".edit-number-step-down").ClickAsync();
        await field.Locator(".edit-number-step-down").ClickAsync();
        await Expect(input).ToHaveValueAsync("2");
    }

    [Fact]
    public async Task A_button_whose_bound_is_already_reached_is_disabled_and_takes_no_click()
    {
        await NavigateAsync();
        // StepperAtMax is pinned at Max=10; StepperAtMin at Min=5.
        var atMax = StepperSection.Locator(".edit-number-stepper", new() { Has = Page.Locator("#StepperAtMax") });
        var atMin = StepperSection.Locator(".edit-number-stepper", new() { Has = Page.Locator("#StepperAtMin") });

        await Expect(atMax.Locator(".edit-number-step-up")).ToBeDisabledAsync();
        await Expect(atMax.Locator(".edit-number-step-down")).ToBeEnabledAsync();
        await Expect(atMin.Locator(".edit-number-step-down")).ToBeDisabledAsync();
        await Expect(atMin.Locator(".edit-number-step-up")).ToBeEnabledAsync();

        // Force past Playwright's own actionability guard: the browser must be the thing refusing it.
        await atMax.Locator(".edit-number-step-up").ClickAsync(new LocatorClickOptions { Force = true });
        await Expect(atMax.Locator("input[type=number]")).ToHaveValueAsync("10");

        // ...and stepping away from the bound re-enables it.
        await atMax.Locator(".edit-number-step-down").ClickAsync();
        await Expect(atMax.Locator("input[type=number]")).ToHaveValueAsync("9");
        await Expect(atMax.Locator(".edit-number-step-up")).ToBeEnabledAsync();
    }

    [Fact]
    public async Task The_stepper_buttons_are_skipped_by_Tab()
    {
        // tabindex="-1" is the documented deviation: the native input's own Up/Down arrows are the
        // keyboard path, so tabbing a form never has to pass three stops per numeric field.
        await NavigateAsync();
        var input = StepperSection.Locator(".edit-number-stepper", new() { Has = Page.Locator("#StepperBasic") })
            .Locator("input[type=number]");
        await input.FocusAsync();

        await Page.Keyboard.PressAsync("Tab");

        // The next focused element is the following field's input, not a stepper button.
        var focusedId = await Page.EvaluateAsync<string>("() => document.activeElement?.id ?? ''");
        Assert.Equal("StepperAtMax", focusedId);
    }

    [Fact]
    public async Task Visual_baseline_stepper_section()
    {
        await NavigateAsync();
        await Expect(StepperSection).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(StepperSection, "stepper-section");
    }
}
