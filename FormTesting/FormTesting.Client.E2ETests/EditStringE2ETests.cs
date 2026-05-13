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
}
