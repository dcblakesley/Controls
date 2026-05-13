using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditDateE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Date;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditDate Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Date_input_accepts_value_and_round_trips_to_bound_display()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var input = Page.Locator("section.demo-section").First.Locator("input[type=date]").First;
        await input.FillAsync("2026-05-13");

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("2026-05-13");
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_swaps_inputs_for_ReadOnlyValue()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator("input[type=date]").First).Not.ToBeVisibleAsync();
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
