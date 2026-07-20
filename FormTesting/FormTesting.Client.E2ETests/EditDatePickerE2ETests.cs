using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the EditDatePicker form control on its own demo view -- the form-integration
/// surface (label/validation wiring, the read-only swap, the day-click round trip through
/// <c>@bind-Value</c>). The underlying UI-kit <c>DatePicker</c>'s JS-interop internals
/// (placement, keyboard nav, focus handling) are already covered by <see cref="DatePickerE2ETests"/>
/// against the /uikit gallery, which drives the same component.
/// </summary>
public class EditDatePickerE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.DatePicker;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditDatePicker Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Day_click_updates_the_input_value()
    {
        await NavigateAsync();

        // First section: a single EditDatePicker bound to a fixed 2026-02-14. Pick a different
        // in-month day so the click is observably responsible for the resulting value.
        var section = Page.Locator("section.demo-section").First;
        var field = section.Locator(".wss-picker-input");
        var input = section.Locator("input.wss-picker-input-date");
        var dropdown = section.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-02-20']").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDatePicker.Format defaults to "MM/dd/yyyy" -- the demo's first section never overrides it.
        await Expect(input).ToHaveValueAsync("02/20/2026");
    }

    [Fact]
    public async Task Required_picker_shows_validation_message_when_empty()
    {
        await NavigateAsync();

        // Third section's Required EditDatePicker binds an initially-null [Required] property, and
        // the demo force-validates on every render (_form.EditContext.Validate() in OnAfterRender),
        // so the invalid state and message are already present without any interaction. Ids default
        // to the bound property's name (no Id/IdPrefix/FormGroupOptions set anywhere in this demo),
        // so "Required" is addressable directly and is unique on the page.
        var section = Page.Locator("section.demo-section").Nth(2);
        var input = section.Locator("#Required");
        var message = section.Locator("#error-msg-Required");

        await Expect(input).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(message).ToBeVisibleAsync();
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
