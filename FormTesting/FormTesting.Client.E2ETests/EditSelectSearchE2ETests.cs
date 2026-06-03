using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectSearchE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.SelectSearch;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditSelectSearch Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task First_select_renders_search_input()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await Expect(select).ToBeVisibleAsync();
        await Expect(select.Locator("input.wss-select-selection-search-input")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Opening_dropdown_shows_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();
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
