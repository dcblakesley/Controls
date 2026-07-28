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
}
