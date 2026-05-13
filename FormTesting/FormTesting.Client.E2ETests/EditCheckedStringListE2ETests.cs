using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditCheckedStringListE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.CheckedStringList;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditCheckedStringList Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_checkbox_toggles_selection()
    {
        await NavigateAsync();
        var firstCheckbox = Page.Locator("section.demo-section").First.Locator("input[type=checkbox]").First;
        await Expect(firstCheckbox).Not.ToBeCheckedAsync();
        await firstCheckbox.CheckAsync();
        await Expect(firstCheckbox).ToBeCheckedAsync();
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
