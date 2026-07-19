using System.Globalization;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the single-date <see cref="DatePicker"/> UI-kit control: open/close, the
/// one-click pick, Min/Max day disabling, typed input, clearing, month/year navigation and the
/// ARIA wiring. The JS-owned behaviors (viewport flip/clamp, Enter submit-suppression, focus-out
/// close) are covered by the e2e suite — bUnit does not execute JavaScript.
/// </summary>
public class DatePickerTests : TestContext
{
    public DatePickerTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    // Fixed inputs so nothing depends on the machine's culture or the test run's date.
    static readonly DateTime Feb14 = new(2026, 2, 14);

    IRenderedComponent<DatePicker> RenderPicker(
        Action<ComponentParameterCollectionBuilder<DatePicker>>? configure = null) =>
        RenderComponent<DatePicker>(p =>
        {
            p.Add(c => c.Format, "MM/dd/yyyy");
            p.Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday);
            configure?.Invoke(p);
        });

    static void Open(IRenderedComponent<DatePicker> cut) =>
        cut.Find(".wss-picker-input").Click();

    // The in-month day button for the given day number (skips the leading adjacent-month cells,
    // which carry wss-picker-day-outside).
    static IElement Day(IRenderedComponent<DatePicker> cut, int dayNumber) =>
        cut.FindAll(".wss-picker-day")
            .First(b => !b.ClassList.Contains("wss-picker-day-outside") &&
                        b.TextContent == dayNumber.ToString("00", CultureInfo.InvariantCulture));

    [Fact]
    public void Closed_picker_renders_the_field_only_with_the_spec_placeholder()
    {
        var cut = RenderPicker();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Empty(cut.FindAll(".wss-picker-backdrop"));
        Assert.Equal("Select date", cut.Find(".wss-picker-input-date").GetAttribute("placeholder"));
        Assert.Equal("false", cut.Find(".wss-picker-input-date").GetAttribute("aria-expanded"));
        Assert.Contains("wss-picker-single", cut.Find(".wss-picker").ClassList);
    }

    [Fact]
    public void Field_click_opens_a_single_month_dialog_anchored_on_the_value()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);

        var dialog = cut.Find(".wss-picker-dropdown");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Contains("wss-picker-dropdown-single", dialog.ClassList);
        Assert.Equal("true", cut.Find(".wss-picker-input-date").GetAttribute("aria-expanded"));

        var months = cut.FindAll(".wss-picker-month");
        Assert.Single(months);
        Assert.Equal("2", months[0].QuerySelectorAll("select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2026", months[0].QuerySelectorAll("select")[1].QuerySelector("option[selected]")!.GetAttribute("value"));
        // 6 fixed rows of 7 — the panel height never jumps while navigating months.
        Assert.Equal(42, cut.FindAll(".wss-picker-day").Count);
    }

    [Fact]
    public void One_day_click_commits_and_closes()
    {
        DateTime? value = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(2026, 2, 20), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void The_bound_value_renders_selected_and_pressed()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);

        var selected = Day(cut, 14);
        Assert.Contains("wss-picker-day-selected", selected.ClassList);
        Assert.Equal("true", selected.GetAttribute("aria-pressed"));
        Assert.Equal("false", Day(cut, 15).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Min_and_max_disable_out_of_range_days()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 2, 10))
            .Add(c => c.Max, new DateTime(2026, 2, 20)));

        Open(cut);

        Assert.True(Day(cut, 9).HasAttribute("disabled"));
        Assert.False(Day(cut, 10).HasAttribute("disabled"));
        Assert.False(Day(cut, 20).HasAttribute("disabled"));
        Assert.True(Day(cut, 21).HasAttribute("disabled"));
    }

    [Fact]
    public void Typed_text_commits_on_enter_and_closes()
    {
        DateTime? value = null;
        var cut = RenderPicker(p => p
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-date");
        input.Input("03/05/2026");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 3, 5), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown")); // a single-date pick is complete — close
    }

    [Fact]
    public void Invalid_typed_text_reverts_and_keeps_the_dropdown_open()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);
        var input = cut.Find(".wss-picker-input-date");
        input.Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Feb14, cut.Instance.Value); // unchanged
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // nothing committed — stay open
        Assert.Equal("02/14/2026", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    [Fact]
    public void Clearing_typed_text_commits_null_on_enter()
    {
        DateTime? value = Feb14;
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Null(value);
    }

    [Fact]
    public void Clear_button_clears_the_value()
    {
        DateTime? value = Feb14;
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        cut.Find(".wss-picker-clear").Click();

        Assert.Null(value);
    }

    [Fact]
    public void Escape_and_backdrop_click_close_without_committing()
    {
        DateTime? value = Feb14;
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));

        Open(cut);
        cut.Find(".wss-picker-backdrop").Click();
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));

        Assert.Equal(Feb14, value);
    }

    [Fact]
    public void Month_and_year_selects_retarget_the_grid()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);
        var selects = cut.FindAll(".wss-picker-month-header select");
        selects[0].Change("5");    // May
        selects = cut.FindAll(".wss-picker-month-header select");
        selects[1].Change("2027"); // 2027

        // May 2027 starts on a Saturday (Sunday-start grid → 6 leading outside days).
        var days = cut.FindAll(".wss-picker-day");
        Assert.Equal(6, days.TakeWhile(d => d.ClassList.Contains("wss-picker-day-outside")).Count());
        Assert.Contains("wss-picker-day-outside", days[0].ClassList);
        Assert.Equal("01", days[6].TextContent);
    }

    [Fact]
    public void Disabled_picker_does_not_open()
    {
        var cut = RenderPicker(p => p.Add(c => c.Disabled, true));

        cut.Find(".wss-picker-input").Click();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Contains("wss-picker-disabled", cut.Find(".wss-picker").ClassList);
    }

    [Fact]
    public void Year_select_options_are_clamped_to_the_datetime_range_and_selecting_the_max_does_not_throw()
    {
        // Unclamped, ±10 around 9998 would offer up to 10008 — outside DateTime's [1, 9999] years,
        // and constructing `new DateTime(10008, ...)` throws (circuit-killing on Blazor Server).
        var cut = RenderPicker(p => p.Add(c => c.Value, new DateTime(9998, 2, 14)));
        Open(cut);

        var yearSelect = cut.FindAll(".wss-picker-month-header select")[1];
        var options = yearSelect.QuerySelectorAll("option");
        Assert.Equal("9999", options.Last().GetAttribute("value"));

        var ex = Record.Exception(() => yearSelect.Change("9999"));
        Assert.Null(ex);
    }

    [Fact]
    public void Weekday_header_row_matches_the_grids_first_day_of_week()
    {
        var sunday = RenderPicker(p => p.Add(c => c.Value, Feb14)); // fixture pins Sunday
        Open(sunday);
        var sundayNames = sunday.FindAll(".wss-picker-week-day").Select(d => d.TextContent).ToList();
        Assert.Equal(new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }, sundayNames);
        Assert.Equal("true", sunday.Find(".wss-picker-week-header").GetAttribute("aria-hidden"));

        var monday = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Value, Feb14));
        Open(monday);
        Assert.Equal("Mo", monday.FindAll(".wss-picker-week-day")[0].TextContent);
    }

    [Fact]
    public void Prev_and_next_month_buttons_step_the_view()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));
        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Previous month", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next month", buttons[1].GetAttribute("aria-label"));

        buttons[0].Click(); // prev
        Assert.Equal("1", cut.FindAll(".wss-picker-month-header select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));

        cut.FindAll(".wss-picker-nav")[1].Click(); // next, back to Feb
        Assert.Equal("2", cut.FindAll(".wss-picker-month-header select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Prev_button_disables_at_the_earliest_representable_month()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, new DateTime(1, 2, 1))); // ClampView's floor
        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void The_selected_day_carries_the_roving_tabindex_by_default()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));
        Open(cut);

        Assert.Equal("0", Day(cut, 14).GetAttribute("tabindex"));
        Assert.Equal("-1", Day(cut, 15).GetAttribute("tabindex"));
        Assert.Equal("2026-02-14", Day(cut, 14).GetAttribute("data-date"));
    }

    [Fact]
    public void Arrow_keys_move_the_roving_tabindex_day()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));
        Open(cut);

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", Day(cut, 15).GetAttribute("tabindex"));
        Assert.Equal("-1", Day(cut, 14).GetAttribute("tabindex"));

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", Day(cut, 22).GetAttribute("tabindex"));
    }

    [Fact]
    public void PageDown_steps_a_month_and_moves_the_displayed_view()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));
        Open(cut);

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" });

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("3", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", Day(cut, 14).GetAttribute("tabindex"));
    }

    [Fact]
    public void Home_and_end_move_to_the_start_and_end_of_the_focused_week()
    {
        // Feb 14 2026 is a Saturday; a Sunday-start week runs Feb 8 - Feb 14.
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));
        Open(cut);

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", Day(cut, 8).GetAttribute("tabindex"));

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", Day(cut, 14).GetAttribute("tabindex"));
    }
}
