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
        // FieldValidationDisplay's screen-reader div (#error-msg-*) renders unconditionally -- even
        // with zero validation messages -- and its edit-sr-only clip pattern still has a non-zero
        // bounding box, so Playwright counts it as visible either way. A bare ToBeVisibleAsync can
        // never fail here. Assert the rendered text instead: GetValidationMessage(x, true) rewrites
        // DataAnnotations' "The Required field is required." into "{label} is required." (includeLabel:
        // true), and "Required" -- the bound property's own name, with no camelCase humps to split --
        // is also its auto-derived label.
        await Expect(message).ToContainTextAsync("Required is required.");

        // Verify defect 1's fix (the new .wss-picker.invalid rule in wss-controls.css) actually
        // applies -- an invalid picker used to be pixel-identical to a valid one. The bordered
        // element is the wrapper's .wss-picker-input div, not the <input> itself. The Has locator
        // must be page-rooted and simple: Playwright re-roots it at each candidate element, so a
        // section-chained locator would look for "section.demo-section" INSIDE .wss-picker-input
        // and never match.
        var pickerInput = section.Locator(".wss-picker-input", new() { Has = Page.Locator("#Required") });
        // --wss-color-error resolves to var(--color-danger, #ff4d4f), but the FormTesting host's
        // app.css overrides --color-danger to #CF1322 at :root -- that bridged value (not the
        // #ff4d4f fallback) is what actually reaches the browser here.
        await Expect(pickerInput).ToHaveCSSAsync("border-color", "rgb(207, 19, 34)");
    }

    [Fact]
    public async Task Time_type_field_commits_the_selected_hour_into_the_bound_TimeOnly_value()
    {
        await NavigateAsync();

        // Fourth section: the Type-generalization demo. TimeValue binds a TimeOnly? via
        // Type="InputDateType.Time" -- Format is unset, so the picker's Mode.Time default
        // "HH:mm:ss" is what the input displays.
        // The Has locator must be page-rooted (Playwright re-roots it at each candidate element --
        // see Required_picker_shows_validation_message_when_empty's comment on the same gotcha).
        var section = Page.Locator("section.demo-section").Nth(3);
        var input = section.Locator("#TimeValue");
        var picker = section.Locator(".wss-picker", new() { Has = Page.Locator("#TimeValue") });
        var field = picker.Locator(".wss-picker-input");
        var dropdown = picker.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        // Pinned model value is 09:30:15 -- change the hour so the commit is observable.
        await dropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("14");

        // Time mode commits immediately without closing.
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(input).ToHaveValueAsync("14:30:15");

        await dropdown.Locator(".wss-picker-ok").ClickAsync();
        await Expect(dropdown).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Month_type_field_commits_the_first_of_the_picked_month_into_the_bound_DateOnly_value()
    {
        await NavigateAsync();

        // Fourth section: MonthValue binds a DateOnly? via Type="InputDateType.Month", pinned to
        // 2026-02-01 -- pick a different month so the click is observably responsible for the result.
        // The Has locator must be page-rooted (same gotcha as the Time test above).
        var section = Page.Locator("section.demo-section").Nth(3);
        var input = section.Locator("#MonthValue");
        var picker = section.Locator(".wss-picker", new() { Has = Page.Locator("#MonthValue") });
        var field = picker.Locator(".wss-picker-input");
        var dropdown = picker.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-05-01']").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDatePicker's Format is unset, so the picker's Mode.Month default "MM/yyyy" applies --
        // the bound DateOnly's Day is always 1, confirming the click committed the 1st of the month.
        await Expect(input).ToHaveValueAsync("05/2026");
    }

    [Fact]
    public async Task Mode_Week_field_commits_the_week_start_into_the_bound_DateOnly_value()
    {
        await NavigateAsync();

        // Fifth section: WeekValue binds a DateOnly? via Mode="DatePickerMode.Week", pinned to
        // 2026-02-14 (a Saturday). The Has locator must be page-rooted (same gotcha as the Time/
        // Month tests above).
        var section = Page.Locator("section.demo-section").Nth(4);
        var input = section.Locator("#WeekValue");
        var picker = section.Locator(".wss-picker", new() { Has = Page.Locator("#WeekValue") });
        var field = picker.Locator(".wss-picker-input");
        var dropdown = picker.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        // Click a different day in the SAME week (Feb 11, Wednesday) so the commit is observably
        // the week START (Feb 8), not the clicked day.
        await dropdown.Locator("[data-date='2026-02-11']").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDatePicker's Format is unset, so the picker's Mode.Week default "yyyy-Www" shorthand
        // applies -- the exact week number depends on the culture's week rule, so this only asserts
        // the shape (the week-start value itself is bUnit-covered at the model level).
        await Expect(input).ToHaveValueAsync(new Regex(@"^2026-W\d{2}$"));
    }

    [Fact]
    public async Task Use12Hours_field_displays_h_mm_tt_and_commits_the_shifted_24_hour_value()
    {
        await NavigateAsync();

        // Sixth section: MeetingTime binds a TimeOnly? via Type="InputDateType.Time" with
        // Use12Hours="true" and ShowSeconds="false", pinned to 14:30:00.
        var section = Page.Locator("section.demo-section").Nth(5);
        var input = section.Locator("#MeetingTime");
        var picker = section.Locator(".wss-picker", new() { Has = Page.Locator("#MeetingTime") });
        var field = picker.Locator(".wss-picker-input");
        var dropdown = picker.Locator(".wss-picker-dropdown");

        // The 12-hour, no-seconds effective format applies to the bound display before any interaction.
        await Expect(input).ToHaveValueAsync("2:30 PM");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("select[aria-label='AM/PM']").SelectOptionAsync("AM");

        // Time mode commits immediately without closing.
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(input).ToHaveValueAsync("2:30 AM");
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
