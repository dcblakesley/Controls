using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditMultiSelectE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.MultiSelect;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditMultiSelect Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_select_renders_in_multiple_mode_with_a_tag()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select-multiple").First;
        await Expect(select).ToBeVisibleAsync();
        // The model preselects one color, so a removable tag should be present.
        await Expect(select.Locator(".wss-select-selection-item-content").First).ToBeVisibleAsync();
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
