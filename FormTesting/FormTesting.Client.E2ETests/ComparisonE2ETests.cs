using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// Coverage for the Comparison demo's tab strip — an APG tabs pattern implemented by hand
/// (roving tabindex + ArrowLeft/Right moving selection and focus together).
/// </summary>
public class ComparisonE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Comparison;

    [Fact]
    public async Task Arrow_keys_move_tab_selection_and_focus_together()
    {
        await NavigateAsync();

        // Default selection is the last tab (React + Ant Design + Accessibility).
        var selected = Page.Locator("[role=tab][aria-selected='true']");
        await Expect(selected).ToHaveIdAsync("tab-reactfullparity");

        await selected.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight"); // wraps from the last tab to the first

        await Expect(Page.Locator("#tab-rollyourown")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Page.Locator("#tab-rollyourown")).ToBeFocusedAsync(); // focus follows selection
        await Expect(Page.Locator("#panel-rollyourown")).ToBeVisibleAsync(); // panel swapped

        await Page.Keyboard.PressAsync("ArrowLeft"); // wraps back
        await Expect(Page.Locator("#tab-reactfullparity")).ToBeFocusedAsync();
        await Expect(Page.Locator("#panel-reactfullparity")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Only_the_selected_tab_is_a_tab_stop()
    {
        await NavigateAsync();

        // Roving tabindex: unselected tabs are removed from the Tab order...
        await Expect(Page.Locator("#tab-rollyourown")).ToHaveAttributeAsync("tabindex", "-1");
        await Expect(Page.Locator("#tab-reacttypical")).ToHaveAttributeAsync("tabindex", "-1");
        // ...while the selected tab keeps the button default (no tabindex attribute at all).
        await Expect(Page.Locator("#tab-reactfullparity")).Not.ToHaveAttributeAsync("tabindex", "-1");
    }
}
