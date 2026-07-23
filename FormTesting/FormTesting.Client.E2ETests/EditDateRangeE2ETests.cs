using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the EditDateRange form control on its own demo view -- the form-integration
/// surface (label/validation wiring for both the Start and End fields independently, the read-only
/// swap, presets, and the two-click range round trip through <c>@bind-Start</c>/<c>@bind-End</c>).
/// The underlying UI-kit <c>DateRangePicker</c>'s JS-interop internals (placement, keyboard
/// nav, focus handling) are already covered by <see cref="DateRangePickerE2ETests"/> against the
/// /uikit gallery, which drives the same component.
/// </summary>
public class EditDateRangeE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.DateRange;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditDateRange Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Range_pick_updates_both_inputs()
    {
        await NavigateAsync();

        // First section: a single EditDateRange bound to a fixed 2026-02-01 -> 2026-02-14. Opening
        // anchors the left panel on Start's month (Feb 2026), so both picks below land in it.
        var section = Page.Locator("section.demo-section").First;
        var field = section.Locator(".wss-picker-input");
        var startInput = section.Locator(".wss-picker-input-start");
        var endInput = section.Locator(".wss-picker-input-end");
        var dropdown = section.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-02-05']").ClickAsync(); // first click: pending start
        await Expect(dropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click
        await dropdown.Locator("[data-date='2026-02-20']").ClickAsync(); // second click: commits + closes

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDateRange.Format defaults to "MM/dd/yyyy" -- the demo's first section never overrides it.
        await Expect(startInput).ToHaveValueAsync("02/05/2026");
        await Expect(endInput).ToHaveValueAsync("02/20/2026");
    }

    [Fact]
    public async Task Preset_click_fills_the_range()
    {
        await NavigateAsync();

        // Second section pairs an editable EditDateRange (with Presets) with a read-only sibling
        // bound to the SAME PresetStart/PresetEnd model properties -- both render an
        // .edit-control-wrapper, so scope to the first (editable, declared first in markup) to keep
        // the read-only sibling (and its duplicate ids -- see the class remarks) out of the way.
        var section = Page.Locator("section.demo-section").Nth(1);
        var editable = section.Locator(".edit-control-wrapper").First;
        var field = editable.Locator(".wss-picker-input");
        var startInput = editable.Locator(".wss-picker-input-start");
        var endInput = editable.Locator(".wss-picker-input-end");
        var dropdown = editable.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        // Deliberately NOT "Q1 2026" here even though the task spec named it: the demo's initial
        // PresetStart/PresetEnd (2026-01-01 -> 2026-03-31) already equal that preset's own range, so
        // clicking it wouldn't change anything -- the assertion would pass even if the preset click
        // were completely broken. "Q2 2026" actually differs from the bound value, so this is a real
        // regression check.
        await dropdown.Locator(".wss-picker-preset", new() { HasTextString = "Q2 2026" }).ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        await Expect(startInput).ToHaveValueAsync("04/01/2026");
        await Expect(endInput).ToHaveValueAsync("06/30/2026");
    }

    [Fact]
    public async Task Required_range_shows_validation_messages_when_empty()
    {
        await NavigateAsync();

        // Third section: a single EditDateRange bound to initially-null [Required] RequiredStart/
        // RequiredEnd properties. The demo force-validates on every render, so both fields' invalid
        // state and messages are already present without any interaction. Ids default to the bound
        // Start property's name ("RequiredStart") with "-end" appended for the End field
        // (EditDateRange.razor.cs: _endId = $"{_id}-end") -- not the RequiredEnd property name --
        // so they're addressable directly and are unique on the page.
        var section = Page.Locator("section.demo-section").Nth(2);
        var startInput = section.Locator("#RequiredStart");
        var endInput = section.Locator("#RequiredStart-end");
        var startMessage = section.Locator("#error-msg-RequiredStart");
        var endMessage = section.Locator("#error-msg-RequiredStart-end");

        await Expect(startInput).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(endInput).ToHaveAttributeAsync("aria-invalid", "true");
        // FieldValidationDisplay's screen-reader div (#error-msg-*) renders unconditionally -- even
        // with zero validation messages -- and its edit-sr-only clip pattern still has a non-zero
        // bounding box, so Playwright counts it as visible either way. A bare ToBeVisibleAsync can
        // never fail here. Assert the rendered text instead: GetValidationMessage(x, true) rewrites
        // DataAnnotations' "The {FieldName} field is required." into "{label} is required."
        // (includeLabel: true). Start's FieldValidationDisplay gets no explicit Label in this demo,
        // so its label auto-derives from the RequiredStart property's own name (camelCase split ->
        // "Required Start"); End's FieldValidationDisplay is likewise given no EndLabel, so its label
        // auto-derives from the bound RequiredEnd property -- "Required End" -- even though the DOM
        // id below is anchored to the Start field's name plus "-end".
        await Expect(startMessage).ToContainTextAsync("Required Start is required.");
        await Expect(endMessage).ToContainTextAsync("Required End is required.");
    }

    [Fact]
    public async Task Month_mode_range_commit_round_trips_through_the_form()
    {
        await NavigateAsync();

        // Fourth section: a single EditDateRange bound to MonthStart/MonthEnd (2026-02-01 ->
        // 2026-05-01) via Mode="DatePickerMode.Month". Opening anchors the left panel on Start's
        // year (2026), so both picks below land in the visible dual-panel grid without navigating.
        var section = Page.Locator("section.demo-section").Nth(3);
        var field = section.Locator(".wss-picker-input");
        var startInput = section.Locator(".wss-picker-input-start");
        var endInput = section.Locator(".wss-picker-input-end");
        var dropdown = section.Locator(".wss-picker-dropdown");

        await field.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-03-01']").ClickAsync(); // first click: pending start
        await Expect(dropdown).ToBeVisibleAsync(); // still open -- a range pick needs a second click
        await dropdown.Locator("[data-date='2027-01-01']").ClickAsync(); // second click: commits + closes

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDateRange's Format is unset, so the picker's Mode.Month default "MM/yyyy" applies.
        await Expect(startInput).ToHaveValueAsync("03/2026");
        await Expect(endInput).ToHaveValueAsync("01/2027");
    }

    [Fact]
    public async Task DateTime_mode_session_commit_updates_both_fields()
    {
        await NavigateAsync();

        // Sixth section: a single EditDateRange bound to DateTimeStart/DateTimeEnd (both already
        // committed: 2026-02-10 09:00 -> 2026-02-20 17:30) via Mode="DatePickerMode.DateTime",
        // Use12Hours="true", ShowSeconds="false".
        var section = Page.Locator("section.demo-section").Nth(5);
        var startInput = section.Locator(".wss-picker-input-start");
        var endInput = section.Locator(".wss-picker-input-end");
        var dropdown = section.Locator(".wss-picker-dropdown");

        // Both endpoints already have committed values, so touching just ONE side and hitting OK
        // is enough to commit immediately -- the OTHER side's own already-resolved value rides
        // along unchanged (see DateRangePicker.OnSessionOkAsync's doc comment on the dual-panel
        // sibling; EditDateRange forwards straight through to the same UI-kit control). Exercise
        // the START side first, focusing it directly so the session opens with it active.
        await startInput.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-02-12']").ClickAsync();
        await dropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("10"); // 10 AM -- within Start's own AM period
        await dropdown.Locator(".wss-picker-ok").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        // EditDateRange's Format is unset, so the picker's Mode.DateTime default
        // "MM/dd/yyyy h:mm tt" applies (Use12Hours=true, ShowSeconds=false).
        await Expect(startInput).ToHaveValueAsync("02/12/2026 10:00 AM");
        await Expect(endInput).ToHaveValueAsync("02/20/2026 5:30 PM"); // End's own value rides along unchanged

        // ...then the END side, proving the commit reaches BOTH bound fields independently.
        await endInput.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));

        await dropdown.Locator("[data-date='2026-02-25']").ClickAsync();
        await dropdown.Locator("select[aria-label='Hour']").SelectOptionAsync("15"); // 3 PM -- within End's own PM period
        await dropdown.Locator(".wss-picker-ok").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync();
        await Expect(startInput).ToHaveValueAsync("02/12/2026 10:00 AM"); // Start's own value rides along unchanged
        await Expect(endInput).ToHaveValueAsync("02/25/2026 3:30 PM");
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
