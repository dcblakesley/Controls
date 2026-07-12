using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditStringE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.String;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditString Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_input_accepts_text_and_round_trips_to_bound_value_display()
    {
        await NavigateAsync();
        // Turn on "Display bound values" so we can verify the @bind round-trip from the DOM.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var input = Page.Locator("section.demo-section").First.Locator("input.edit-string-input").First;
        await input.FillAsync("hello world");
        await input.PressAsync("Tab"); // commit binding on blur

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("hello world");
    }

    [Fact]
    public async Task Tooltip_escape_dismisses_while_the_trigger_stays_focused()
    {
        await NavigateAsync();
        var trigger = Page.Locator(".edit-tooltip-container").First;
        var content = Page.Locator(".edit-tooltip-content").First;

        // Focus shows the tooltip (keyboard path).
        await trigger.FocusAsync();
        await Expect(content).ToBeVisibleAsync();

        // Escape must dismiss it even though the trigger keeps focus (WCAG 1.4.13) — the CSS
        // :focus reveal used to override the aria-hidden state and keep it visible until blur.
        await Page.Keyboard.PressAsync("Escape");
        await Expect(content).Not.ToBeVisibleAsync();
        await Expect(trigger).ToBeFocusedAsync();

        // Re-triggering still works after a dismissal.
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(content).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_swaps_inputs_for_ReadOnlyValue()
    {
        await NavigateAsync();
        // Edit-mode toggle starts true; click to enter read-only.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        // The first demo-section's first EditString should no longer render its input.
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator("input.edit-string-input").First).Not.ToBeVisibleAsync();
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToBeVisibleAsync();
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
    public async Task Percentage_width_on_the_input_resolves_against_the_control_column()
    {
        // Regression: .edit-input-with-icon used to shrink-wrap (align-self: flex-start), which made
        // a consumer width:100% on the editor circular per the CSS sizing spec — it silently resolved
        // to auto and the input stayed at its intrinsic default size. The custom-styling demo section
        // sets width:100%; the input must now span (nearly) its purple container's inner width.
        await NavigateAsync();
        var input = Page.Locator("input.my-custom-input");
        var container = Page.Locator(".my-custom-container");

        var inputBox = await input.BoundingBoxAsync();
        var containerBox = await container.BoundingBoxAsync();
        Assert.NotNull(inputBox);
        Assert.NotNull(containerBox);

        // Container has 10px padding per side; allow slack for borders/rounding. A collapsed input
        // renders ~180px (Chromium's default size="20" width), far below this threshold.
        Assert.True(inputBox.Width >= containerBox.Width - 25,
            $"input width {inputBox.Width}px should fill the container ({containerBox.Width}px wide) — percentage width collapsed");
    }
}
