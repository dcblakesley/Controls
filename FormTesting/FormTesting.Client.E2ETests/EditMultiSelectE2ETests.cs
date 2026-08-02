using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditMultiSelectE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.MultiSelect;

    [Fact]
    public async Task First_select_renders_in_multiple_mode_with_a_tag()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select-multiple").First;
        await Expect(select).ToBeVisibleAsync();
        // The model preselects one color, so a removable tag should be present.
        await Expect(select.Locator(".wss-select-selection-item-content").First).ToBeVisibleAsync();
    }

    // ----- document.activeElement through tag removal (bUnit has no JS runtime, so it can only
    // observe the interop call, never whether real DOM focus actually landed/stayed on the input --
    // see Select.razor.cs RemoveAsync's restoreFocus remarks) -----------------------------------------

    [Fact]
    public async Task Backspace_burst_keeps_focus_on_the_input_and_the_tag_remove_button_restores_it()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select-multiple").First;
        var input = select.Locator("input.wss-select-selection-search-input");

        // The model preselects "Green"; add two more tags via the dropdown so the Backspace burst
        // below has more than one to remove.
        await select.ClickAsync();
        await Expect(select.Locator(".wss-select-item-option").First).ToBeVisibleAsync();
        await select.Locator(".wss-select-item-option", new() { HasTextString = "Red" }).ClickAsync();
        await select.Locator(".wss-select-item-option", new() { HasTextString = "Blue" }).ClickAsync();
        await Expect(select.Locator(".wss-select-selection-item-content")).ToHaveCountAsync(3);
        await Expect(input).ToBeFocusedAsync(); // SelectAsync's multi-mode branch re-focuses after each pick

        // Backspace removes from the end (most-recently-added first) via RemoveAsync(restoreFocus:
        // false) -- correct only because a keydown on the input never blurs it in the first place.
        await input.PressAsync("Backspace");
        await Expect(select.Locator(".wss-select-selection-item-content")).ToHaveCountAsync(2);
        await Expect(input).ToBeFocusedAsync();

        await input.PressAsync("Backspace");
        await Expect(select.Locator(".wss-select-selection-item-content")).ToHaveCountAsync(1);
        await Expect(input).ToBeFocusedAsync();

        // One tag left (Green). Remove it via its own X button instead -- a real mouse click blurs
        // the input first, so this path must explicitly restore focus (RemoveAsync(restoreFocus: true)).
        await select.Locator(".wss-select-selection-item-remove").First.ClickAsync();
        await Expect(select.Locator(".wss-select-selection-item-content")).ToHaveCountAsync(0);
        await Expect(input).ToBeFocusedAsync();
    }
}
