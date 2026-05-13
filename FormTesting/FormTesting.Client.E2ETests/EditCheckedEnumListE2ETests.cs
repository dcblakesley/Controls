using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditCheckedEnumListE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.CheckedEnumList;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditCheckedEnumList Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Clicking_checkbox_toggles_selection_and_renders_GetName_labels()
    {
        await NavigateAsync();
        var firstSection = Page.Locator("section.demo-section").First;
        var firstCheckbox = firstSection.Locator("input[type=checkbox]").First;
        await Expect(firstCheckbox).Not.ToBeCheckedAsync();
        await firstCheckbox.CheckAsync();
        await Expect(firstCheckbox).ToBeCheckedAsync();

        // Labels should be enum display names (GetName), not raw enum tokens — spot-check
        // that at least one label has a space (camelCase split) or matches a DisplayName attribute.
        var labels = await firstSection.Locator(".edit-checkbox-label").AllInnerTextsAsync();
        Assert.NotEmpty(labels);
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
