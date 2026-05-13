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
}
