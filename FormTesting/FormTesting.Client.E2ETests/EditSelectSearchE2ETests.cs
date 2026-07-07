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
    public async Task Enter_picks_an_option_without_submitting_the_enclosing_form()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section", new() { HasTextString = "Inside a submitting form" });
        var select = section.Locator(".wss-select").First;

        // Open, highlight an option, and commit it with Enter — the browser's implicit form
        // submission used to fire alongside the selection.
        await select.ClickAsync();
        var input = select.Locator("input.wss-select-selection-search-input");
        await input.PressAsync("ArrowDown");
        await input.PressAsync("Enter");

        await Expect(select.Locator(".wss-select-selection-item")).Not.ToBeEmptyAsync(); // something got picked
        await Expect(section.Locator(".submit-count")).ToHaveTextAsync("Submits: 0");

        // Sanity: the button still submits.
        await section.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Expect(section.Locator(".submit-count")).ToHaveTextAsync("Submits: 1");
    }

    [Fact]
    public async Task Escape_closes_only_the_open_dropdown()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();

        await select.Locator("input.wss-select-selection-search-input").PressAsync("Escape");
        await Expect(Page.Locator(".wss-select-item-option")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Tab_away_closes_the_dropdown_and_frees_the_page()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();

        // Tab moves focus out — the dropdown (and its click-swallowing backdrop) must go with it.
        await select.Locator("input.wss-select-selection-search-input").PressAsync("Tab");
        await Expect(Page.Locator(".wss-select-item-option")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".wss-select-backdrop")).ToHaveCountAsync(0);
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
