using System.Globalization;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
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

    // The Mode="Month" grid always renders exactly 12 buttons, Jan..Dec in order -- no outside/
    // leading cells to skip, unlike Day() above.
    static IElement MonthButton(IRenderedComponent<DatePicker> cut, int monthNumber) =>
        cut.FindAll(".wss-picker-month-btn")[monthNumber - 1];

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

    [Fact]
    public void Default_focus_day_skips_a_disabled_candidate_and_lands_on_the_first_enabled_day()
    {
        // Aug 14 (the bound value) is disabled by Min = Aug 20 — the naive default (bound value,
        // else today, else the 1st) would land the roving tabindex on a disabled button, making the
        // grid keyboard-unreachable (Tab would skip straight past it). The far-future year keeps
        // "today" out of view too, so the only viable fallback is the first enabled in-month day:
        // Aug 20 itself.
        var cut = RenderPicker(p => p
            .Add(c => c.Value, new DateTime(9000, 8, 14))
            .Add(c => c.Min, new DateTime(9000, 8, 20)));

        Open(cut);

        var focusStop = Day(cut, 20);
        Assert.Equal("0", focusStop.GetAttribute("tabindex"));
        Assert.False(focusStop.HasAttribute("disabled"));
        Assert.Equal("-1", Day(cut, 21).GetAttribute("tabindex"));
    }

    [Fact]
    public void Prev_and_next_month_buttons_disable_at_the_min_and_max_month()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 2, 1))
            .Add(c => c.Max, new DateTime(2026, 2, 28)));

        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        // The view (February) sits on both Min's and Max's month, so navigating either direction
        // would land on a month entirely outside [Min, Max] — both nav buttons disable, matching the
        // year select's month-of-Min/Max granularity.
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.True(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Prev_and_next_month_buttons_stay_enabled_one_month_inside_min_and_max()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 1, 1))
            .Add(c => c.Max, new DateTime(2026, 3, 31)));

        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Non_gregorian_default_calendar_cultures_still_render_gregorian_years()
    {
        // th-TH's default calendar is Thai Buddhist (year = Gregorian + 543). Formatting the day
        // aria-label straight through CurrentCulture would show a Buddhist year (2569) that
        // contradicts the plain Gregorian year the year-select text renders (2026) for the exact
        // same grid. The picker is a Gregorian-calendar control regardless of culture — every
        // picker-internal format should agree on 2026.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

            Open(cut);

            var yearOption = cut.FindAll(".wss-picker-month-header select")[1].QuerySelector("option[selected]")!;
            Assert.Equal("2026", yearOption.TextContent);

            var ariaLabel = cut.Find("[data-date='2026-02-14']").GetAttribute("aria-label")!;
            Assert.Contains("2026", ariaLabel);
            Assert.DoesNotContain("2569", ariaLabel);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----- Mode="Month" -------------------------------------------------------

    [Fact]
    public void Month_mode_renders_a_twelve_button_grid_with_the_year_header()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));

        Open(cut);

        var dialog = cut.Find(".wss-picker-dropdown");
        Assert.Contains("wss-picker-dropdown-single", dialog.ClassList);
        Assert.Empty(cut.FindAll(".wss-picker-week-header")); // no day-grid chrome in this mode
        Assert.Empty(cut.FindAll(".wss-picker-grid"));

        var buttons = cut.FindAll(".wss-picker-month-btn");
        Assert.Equal(12, buttons.Count);
        Assert.Equal("Jan", buttons[0].TextContent);
        Assert.Equal("Dec", buttons[11].TextContent);

        var feb = MonthButton(cut, 2);
        Assert.Equal("2026-02-01", feb.GetAttribute("data-date"));
        Assert.Equal("February 2026", feb.GetAttribute("aria-label"));
        Assert.Equal("true", feb.GetAttribute("aria-pressed"));

        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Month_click_commits_the_first_of_month_and_closes()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        MonthButton(cut, 5).Click(); // May

        Assert.Equal(new DateTime(2026, 5, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Month_mode_selected_and_disabled_states_honor_min_and_max_at_month_granularity()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 3, 1))
            .Add(c => c.Max, new DateTime(2026, 10, 31)));

        Open(cut);

        var feb = MonthButton(cut, 2);
        Assert.Equal("true", feb.GetAttribute("aria-pressed")); // Feb14's month
        Assert.True(feb.HasAttribute("disabled")); // entirely before Min's month (March)

        Assert.False(MonthButton(cut, 3).HasAttribute("disabled")); // Min's own month
        Assert.False(MonthButton(cut, 10).HasAttribute("disabled")); // Max's own month
        Assert.True(MonthButton(cut, 11).HasAttribute("disabled")); // entirely after Max's month
    }

    [Fact]
    public void Month_mode_marks_the_current_month_as_aria_current()
    {
        // View the year containing today so the "today" cell is guaranteed to render, without
        // hardcoding which month that is (keeps the test valid regardless of when it runs).
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, new DateTime(DateTime.Today.Year, 1, 1)));

        Open(cut);

        Assert.Equal("date", MonthButton(cut, DateTime.Today.Month).GetAttribute("aria-current"));
        if (DateTime.Today.Month != 1)
        {
            Assert.Null(MonthButton(cut, 1).GetAttribute("aria-current"));
        }
    }

    [Fact]
    public void Month_mode_arrow_keys_move_the_roving_tabindex()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));
        Open(cut);

        Assert.Equal("0", MonthButton(cut, 2).GetAttribute("tabindex")); // Feb14's month by default

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", MonthButton(cut, 3).GetAttribute("tabindex"));
        Assert.Equal("-1", MonthButton(cut, 2).GetAttribute("tabindex"));

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", MonthButton(cut, 6).GetAttribute("tabindex")); // +3 -> one grid row down

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("0", MonthButton(cut, 5).GetAttribute("tabindex"));

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("0", MonthButton(cut, 2).GetAttribute("tabindex"));
    }

    [Fact]
    public void Month_mode_home_and_end_move_to_the_start_and_end_of_the_focused_row()
    {
        // May (month 5) sits in the Apr-Jun grid row.
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, new DateTime(2026, 5, 14)));
        Open(cut);

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", MonthButton(cut, 4).GetAttribute("tabindex"));

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", MonthButton(cut, 6).GetAttribute("tabindex"));
    }

    [Fact]
    public void Month_mode_pagedown_steps_a_year_and_moves_the_displayed_view()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));
        Open(cut);

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" });

        Assert.Equal("2027", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", MonthButton(cut, 2).GetAttribute("tabindex")); // same month, new year

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "PageUp" });
        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Prev_and_next_year_buttons_step_the_view_in_month_mode()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));
        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Previous year", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next year", buttons[1].GetAttribute("aria-label"));

        buttons[0].Click(); // prev year
        Assert.Equal("2025", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));

        cut.FindAll(".wss-picker-nav")[1].Click(); // next year, back to 2026
        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Prev_and_next_year_buttons_disable_at_the_min_and_max_year()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 1, 1))
            .Add(c => c.Max, new DateTime(2026, 12, 31)));

        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.True(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Year_select_retargets_the_month_grid()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));

        Open(cut);
        cut.Find(".wss-picker-month-header select").Change("2027");

        Assert.Equal("2027", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2027-01-01", MonthButton(cut, 1).GetAttribute("data-date"));
    }

    [Fact]
    public void Typed_text_in_month_mode_parses_and_normalizes_to_the_first_of_month()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-date");
        input.Input("03/2026"); // MM/yyyy -- Month mode's default format
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 3, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Effective_format_and_placeholder_default_per_mode_when_unset()
    {
        // Bypass the RenderPicker fixture (it always forces Format) so the mode default applies.
        var dateCut = RenderComponent<DatePicker>(p => p.Add(c => c.Value, Feb14));
        Assert.Equal("02/14/2026", dateCut.Find(".wss-picker-input-date").GetAttribute("value"));
        Assert.Equal("Select date", dateCut.Find(".wss-picker-input-date").GetAttribute("placeholder"));

        var monthCut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14));
        Assert.Equal("02/2026", monthCut.Find(".wss-picker-input-date").GetAttribute("value"));
        Assert.Equal("Select month", monthCut.Find(".wss-picker-input-date").GetAttribute("placeholder"));

        var yearCut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14));
        Assert.Equal("2026", yearCut.Find(".wss-picker-input-date").GetAttribute("value"));
        Assert.Equal("Select year", yearCut.Find(".wss-picker-input-date").GetAttribute("placeholder"));

        var quarterCut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20)));
        Assert.Equal("2026-Q3", quarterCut.Find(".wss-picker-input-date").GetAttribute("value"));
        Assert.Equal("Select quarter", quarterCut.Find(".wss-picker-input-date").GetAttribute("placeholder"));

        // Feb 14, 2026 falls in the week starting Feb 8 (culture-default Sunday first day, same
        // assumption GridDays/WeekdayHeaders make elsewhere in this class) -- week 7 of 2026.
        var weekCut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.Value, Feb14));
        Assert.Equal("2026-W07", weekCut.Find(".wss-picker-input-date").GetAttribute("value"));
        Assert.Equal("Select week", weekCut.Find(".wss-picker-input-date").GetAttribute("placeholder"));
    }

    // ----- Mode="Time" ----------------------------------------------------------

    static IReadOnlyList<IElement> TimeSelects(IRenderedComponent<DatePicker> cut) =>
        cut.FindAll(".wss-picker-time-row select");

    [Fact]
    public void Time_mode_renders_three_selects_with_the_spec_option_counts_and_aria_labels()
    {
        var cut = RenderComponent<DatePicker>(p => p.Add(c => c.Mode, DatePickerMode.Time));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-grid")); // no day calendar in this mode
        Assert.Empty(cut.FindAll(".wss-picker-month-header"));

        var selects = TimeSelects(cut);
        Assert.Equal(3, selects.Count);
        Assert.Equal("Hour", selects[0].GetAttribute("aria-label"));
        Assert.Equal("Minute", selects[1].GetAttribute("aria-label"));
        Assert.Equal("Second", selects[2].GetAttribute("aria-label"));
        Assert.Equal(24, selects[0].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[1].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[2].QuerySelectorAll("option").Length);

        Assert.NotEmpty(cut.FindAll(".wss-picker-ok"));
    }

    [Fact]
    public void Opening_time_mode_with_a_null_value_shows_midnight_but_commits_nothing()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal("0", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", selects[1].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", selects[2].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Null(value); // opening never commits by itself
    }

    [Fact]
    public void Changing_a_time_select_commits_a_today_anchored_value_without_closing()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // hour

        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 13, 0, 0), value);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open -- OK is the close signal
    }

    [Fact]
    public void Time_mode_ok_button_closes_the_panel()
    {
        var cut = RenderComponent<DatePicker>(p => p.Add(c => c.Mode, DatePickerMode.Time));

        Open(cut);
        cut.Find(".wss-picker-ok").Click();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    // ----- Mode="DateTime" -------------------------------------------------------

    [Fact]
    public void Datetime_mode_renders_the_day_calendar_and_the_time_row_together()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 45, 30)));

        Open(cut);

        Assert.Equal(42, cut.FindAll(".wss-picker-day").Count); // the same day calendar as Date mode
        Assert.Equal(3, TimeSelects(cut).Count);
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok"));
    }

    [Fact]
    public void Datetime_mode_day_click_commits_the_date_and_preserves_the_time_without_closing()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 45, 30))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(2026, 2, 20, 13, 45, 30), value);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // this mode leaves the panel open
    }

    [Fact]
    public void Datetime_mode_day_click_with_no_prior_value_commits_midnight()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 20), value);
    }

    [Fact]
    public void Datetime_mode_time_select_change_commits_preserving_the_date()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 45, 30))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        TimeSelects(cut)[1].Change("50"); // minute

        Assert.Equal(new DateTime(2026, 2, 14, 13, 50, 30), value);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Typed_datetime_text_round_trips_through_the_effective_format()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-date");
        input.Input("02/14/2026 13:45:30"); // DateTime mode's default format
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 2, 14, 13, 45, 30), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown")); // typed Enter-commit still closes
    }

    [Fact]
    public void Min_and_max_still_disable_days_in_datetime_mode()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 45, 30))
            .Add(c => c.Min, new DateTime(2026, 2, 10))
            .Add(c => c.Max, new DateTime(2026, 2, 20)));

        Open(cut);

        Assert.True(Day(cut, 9).HasAttribute("disabled"));
        Assert.False(Day(cut, 10).HasAttribute("disabled"));
        Assert.False(Day(cut, 20).HasAttribute("disabled"));
        Assert.True(Day(cut, 21).HasAttribute("disabled"));
    }

    [Fact]
    public void Datetime_mode_ok_button_closes_the_panel()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Value, Feb14));

        Open(cut);
        cut.Find(".wss-picker-ok").Click();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Date_mode_day_click_still_closes_regression_guard()
    {
        // Guards OnDayClickAsync's new Mode branch: Mode.Date must still close on a single click,
        // exactly like before Mode.DateTime existed.
        DateTime? value = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Mode, DatePickerMode.Date)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(2026, 2, 20), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    // ----- Mode="Year" ----------------------------------------------------------

    // The Mode="Year" grid always renders exactly 12 buttons: decadeStart-1 .. decadeStart+10, in
    // order -- index 0 and 11 are the dimmed adjacent-decade cells.
    static IElement YearButton(IRenderedComponent<DatePicker> cut, int index) =>
        cut.FindAll(".wss-picker-month-btn")[index];

    [Fact]
    public void Year_mode_renders_a_twelve_cell_decade_grid_with_two_outside_years()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)); // 2026 -> decade 2020-2029

        Open(cut);

        var dialog = cut.Find(".wss-picker-dropdown");
        Assert.Contains("wss-picker-dropdown-single", dialog.ClassList);
        Assert.Empty(cut.FindAll(".wss-picker-week-header")); // no day-grid chrome in this mode

        var buttons = cut.FindAll(".wss-picker-month-btn");
        Assert.Equal(12, buttons.Count);

        Assert.Equal("2019", buttons[0].TextContent);
        Assert.Contains("wss-picker-month-btn-outside", buttons[0].ClassList);
        Assert.Equal("2020", buttons[1].TextContent);
        Assert.DoesNotContain("wss-picker-month-btn-outside", buttons[1].ClassList);
        Assert.Equal("2029", buttons[10].TextContent);
        Assert.DoesNotContain("wss-picker-month-btn-outside", buttons[10].ClassList);
        Assert.Equal("2030", buttons[11].TextContent);
        Assert.Contains("wss-picker-month-btn-outside", buttons[11].ClassList);

        Assert.Equal("2020-2029", cut.Find(".wss-picker-decade-label").TextContent);
        Assert.Equal("true", YearButton(cut, 7).GetAttribute("aria-pressed")); // 2026
        Assert.Equal("2026", YearButton(cut, 7).GetAttribute("aria-label"));
        Assert.Equal("2026-01-01", YearButton(cut, 7).GetAttribute("data-date"));
    }

    [Fact]
    public void Year_click_commits_january_first_and_closes()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        YearButton(cut, 4).Click(); // 2023

        Assert.Equal(new DateTime(2023, 1, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Clicking_an_outside_year_cell_commits_it_too()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        YearButton(cut, 11).Click(); // the dimmed trailing 2030 cell

        Assert.Equal(new DateTime(2030, 1, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Year_mode_min_and_max_disable_years_outside_range_at_year_granularity()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2022, 6, 1))
            .Add(c => c.Max, new DateTime(2027, 3, 1)));

        Open(cut);

        Assert.True(YearButton(cut, 2).HasAttribute("disabled"));  // 2021 -- entirely before Min's year
        Assert.False(YearButton(cut, 3).HasAttribute("disabled")); // 2022 -- Min's own year
        Assert.False(YearButton(cut, 8).HasAttribute("disabled")); // 2027 -- Max's own year
        Assert.True(YearButton(cut, 9).HasAttribute("disabled"));  // 2028 -- entirely after Max's year
    }

    [Fact]
    public void Year_mode_marks_the_current_year_as_aria_current()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, new DateTime(DateTime.Today.Year, 1, 1)));

        Open(cut);

        var currentYearText = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        var currentButton = cut.FindAll(".wss-picker-month-btn").First(b => b.TextContent == currentYearText);
        Assert.Equal("date", currentButton.GetAttribute("aria-current"));
    }

    [Fact]
    public void Year_mode_arrow_keys_move_the_roving_tabindex()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)); // 2026
        Open(cut);

        Assert.Equal("0", YearButton(cut, 7).GetAttribute("tabindex")); // 2026 by default

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", YearButton(cut, 8).GetAttribute("tabindex")); // 2027
        Assert.Equal("-1", YearButton(cut, 7).GetAttribute("tabindex"));

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("0", YearButton(cut, 7).GetAttribute("tabindex")); // back to 2026

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("0", YearButton(cut, 4).GetAttribute("tabindex")); // -3 -> 2023
    }

    [Fact]
    public void Year_mode_home_and_end_move_to_the_start_and_end_of_the_focused_row()
    {
        // 2023 sits in the 2022-2024 row (rows group from the dimmed leading cell 2019: 2019-21,
        // 2022-24, 2025-27, 2028-30).
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, new DateTime(2023, 6, 1)));
        Open(cut);

        Assert.Equal("0", YearButton(cut, 4).GetAttribute("tabindex")); // default focus = value's year

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", YearButton(cut, 3).GetAttribute("tabindex")); // 2022

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", YearButton(cut, 5).GetAttribute("tabindex")); // 2024
    }

    [Fact]
    public void Prev_and_next_decade_buttons_step_the_view()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14));
        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Previous decade", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next decade", buttons[1].GetAttribute("aria-label"));

        buttons[0].Click(); // prev decade
        Assert.Equal("2010-2019", cut.Find(".wss-picker-decade-label").TextContent);

        cut.FindAll(".wss-picker-nav")[1].Click(); // next decade, back to 2020s
        Assert.Equal("2020-2029", cut.Find(".wss-picker-decade-label").TextContent);
    }

    [Fact]
    public void Prev_and_next_decade_buttons_disable_at_the_min_and_max_decade()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2020, 1, 1))
            .Add(c => c.Max, new DateTime(2029, 12, 31)));

        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.True(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Prev_decade_button_disables_at_the_earliest_representable_decade()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, new DateTime(15, 1, 1))); // ClampDecadeStart's floor -> decade 10-19
        Open(cut);

        var buttons = cut.FindAll(".wss-picker-nav");
        Assert.True(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Year_mode_pagedown_and_pageup_flip_the_decade()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Value, Feb14)); // 2026, decade 2020-2029
        Open(cut);

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" });
        Assert.Equal("2030-2039", cut.Find(".wss-picker-decade-label").TextContent);

        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "PageUp" });
        Assert.Equal("2020-2029", cut.Find(".wss-picker-decade-label").TextContent);
    }

    [Fact]
    public void Typed_text_in_year_mode_parses_and_normalizes_to_january_first()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("2026"); // yyyy -- Year mode's default format
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 1, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    // ----- Mode="Quarter" --------------------------------------------------------

    // The Mode="Quarter" grid always renders exactly 4 buttons, Q1..Q4 in order.
    static IElement QuarterButton(IRenderedComponent<DatePicker> cut, int quarterNumber) =>
        cut.FindAll(".wss-picker-quarter-grid .wss-picker-month-btn")[quarterNumber - 1];

    [Fact]
    public void Quarter_mode_renders_a_four_button_row_with_the_year_header()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))); // Q3 2026

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-month-grid")); // the quarter grid, not the month grid

        var buttons = cut.FindAll(".wss-picker-quarter-grid .wss-picker-month-btn");
        Assert.Equal(4, buttons.Count);
        Assert.Equal("Q1", buttons[0].TextContent);
        Assert.Equal("Q4", buttons[3].TextContent);

        var q3 = buttons[2];
        Assert.Equal("2026-07-01", q3.GetAttribute("data-date"));
        Assert.Equal("Q3 2026", q3.GetAttribute("aria-label"));
        Assert.Equal("true", q3.GetAttribute("aria-pressed"));

        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Quarter_click_commits_the_quarter_start_and_closes()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        QuarterButton(cut, 4).Click(); // Q4

        Assert.Equal(new DateTime(2026, 10, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Quarter_mode_min_and_max_disable_quarters_outside_range_at_quarter_granularity()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))
            .Add(c => c.Min, new DateTime(2026, 4, 15))  // Q2
            .Add(c => c.Max, new DateTime(2026, 9, 1))); // Q3

        Open(cut);

        Assert.True(QuarterButton(cut, 1).HasAttribute("disabled"));  // Q1 entirely before Min's quarter
        Assert.False(QuarterButton(cut, 2).HasAttribute("disabled")); // Q2 -- Min's own quarter
        Assert.False(QuarterButton(cut, 3).HasAttribute("disabled")); // Q3 -- Max's own quarter
        Assert.True(QuarterButton(cut, 4).HasAttribute("disabled"));  // Q4 entirely after Max's quarter
    }

    [Fact]
    public void Quarter_mode_marks_the_current_quarter_as_aria_current()
    {
        var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(DateTime.Today.Year, 1, 1)));

        Open(cut);

        Assert.Equal("date", QuarterButton(cut, currentQuarter).GetAttribute("aria-current"));
    }

    [Fact]
    public void Quarter_mode_arrow_keys_move_the_roving_tabindex_and_cross_the_year()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 11, 1))); // Q4 2026
        Open(cut);

        Assert.Equal("0", QuarterButton(cut, 4).GetAttribute("tabindex"));

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        // Q4 2026 + 1 quarter -> Q1 2027, the view follows.
        Assert.Equal("2027", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", QuarterButton(cut, 1).GetAttribute("tabindex"));

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", QuarterButton(cut, 4).GetAttribute("tabindex"));
    }

    [Fact]
    public void Quarter_mode_up_and_down_are_no_ops()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))); // Q3
        Open(cut);

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        Assert.Equal("0", QuarterButton(cut, 3).GetAttribute("tabindex"));

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        Assert.Equal("0", QuarterButton(cut, 3).GetAttribute("tabindex"));
    }

    [Fact]
    public void Quarter_mode_home_and_end_move_to_the_years_first_and_last_quarter()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))); // Q3
        Open(cut);

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "Home" });
        Assert.Equal("0", QuarterButton(cut, 1).GetAttribute("tabindex"));

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "End" });
        Assert.Equal("0", QuarterButton(cut, 4).GetAttribute("tabindex"));
    }

    [Fact]
    public void Quarter_mode_pageup_and_pagedown_step_a_year_keeping_the_same_quarter()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Value, new DateTime(2026, 8, 20))); // Q3 2026
        Open(cut);

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" });
        Assert.Equal("2027", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", QuarterButton(cut, 3).GetAttribute("tabindex")); // still Q3

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "PageUp" });
        Assert.Equal("2026", cut.Find(".wss-picker-month-header select").QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Quarter_text_with_a_dash_parses_and_normalizes_to_the_quarter_start()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("2026-Q3");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 7, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Quarter_text_without_a_dash_and_lowercase_q_also_parses()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("2026q3");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 7, 1), value);
    }

    [Fact]
    public void Quarter_text_with_no_year_is_rejected()
    {
        var cut = RenderComponent<DatePicker>(p => p.Add(c => c.Mode, DatePickerMode.Quarter));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("q3");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Null(cut.Instance.Value); // nothing committed
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open
    }

    [Fact]
    public void Typed_plain_date_in_quarter_mode_normalizes_to_its_quarter_start()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("08/20/2026");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 7, 1), value);
    }

    [Fact]
    public void Explicit_format_in_quarter_mode_is_used_verbatim()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Format, "yyyy-MM")
            .Add(c => c.Value, new DateTime(2026, 8, 20)));

        // No .NET format token renders the quarter number -- an explicit Format formats Value
        // verbatim via ToString (no quarter-start normalization applied for display), same as every
        // other mode. (A value the picker itself commits is already quarter-start-shaped; this only
        // differs for a raw Value a consumer binds in directly, as here.)
        Assert.Equal("2026-08", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    // ----- Mode="Week" / ShowWeekNumbers -----------------------------------------

    // Feb 14, 2026 is a Saturday; with Sunday as the first day of the week its row spans
    // Feb 8 (Sun) - Feb 14 (Sat), the 2nd of the 6 rows GridWeekRows renders for the open Feb-2026
    // view (Feb 1, 2026 itself is a Sunday, so the grid's first row starts exactly on it).
    static readonly DateTime FeblyWeekStart = new(2026, 2, 8);

    [Fact]
    public void Week_mode_renders_six_week_number_rows_instead_of_the_flat_grid()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-grid")); // the rows layout, not the flat 42-cell grid
        Assert.NotEmpty(cut.FindAll(".wss-picker-grid-week")); // Week mode's own hover/selected scope
        Assert.Equal(6, cut.FindAll(".wss-picker-week-row").Count);
        Assert.Equal(6, cut.FindAll(".wss-picker-week-no").Count);
        Assert.Equal(42, cut.FindAll(".wss-picker-cell").Count);
        Assert.Equal(42, cut.FindAll(".wss-picker-day").Count);
        Assert.Single(cut.FindAll(".wss-picker-week-no-header")); // weekday header's leading cell
    }

    [Fact]
    public void Week_click_commits_the_week_start_and_closes()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Day(cut, 10).Click(); // Feb 10 -- same row as Feb 14 (Feb 8-14)

        Assert.Equal(FeblyWeekStart, value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Week_mode_marks_the_row_containing_the_value_as_selected_and_suppresses_day_styling()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14)); // week Feb 8-14, the grid's 2nd row

        Open(cut);

        var rows = cut.FindAll(".wss-picker-week-row");
        Assert.Equal(6, rows.Count);
        Assert.Contains("wss-picker-week-row-selected", rows[1].ClassList);
        Assert.DoesNotContain("wss-picker-week-row-selected", rows[0].ClassList);

        // Every day in the selected week is pressed -- the row, not the day, is the selection unit.
        var day8 = Day(cut, 8);
        var day14 = Day(cut, 14);
        Assert.Equal("true", day8.GetAttribute("aria-pressed"));
        Assert.Equal("true", day14.GetAttribute("aria-pressed"));
        // Individual day-selected background is suppressed in Week mode.
        Assert.DoesNotContain("wss-picker-day-selected", day8.ClassList);
        Assert.DoesNotContain("wss-picker-day-selected", day14.ClassList);

        // The next row (Feb 15-21) is untouched.
        Assert.Equal("false", Day(cut, 15).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Week_mode_shows_each_rows_week_number()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14));

        Open(cut);

        var rule = CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule;
        var expected = new GregorianCalendar().GetWeekOfYear(FeblyWeekStart, rule, DayOfWeek.Sunday);

        var weekNoCells = cut.FindAll(".wss-picker-week-no");
        Assert.Equal(expected.ToString(CultureInfo.InvariantCulture), weekNoCells[1].TextContent); // row 2 = Feb 8-14
    }

    [Fact]
    public void Week_mode_keeps_day_buttons_enabled_in_a_partially_in_range_week()
    {
        // Min falls mid-week (Feb 10): Feb 8-9 are disabled at day granularity, but the Feb 8-14
        // week itself still straddles Min, so every other day in it -- including the click target
        // below -- stays enabled; only the week-granularity commit guard (IsWeekDisabledForCommit,
        // exercised by the typed-text tests below) would ever reject the whole week.
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 2, 10))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        Assert.True(Day(cut, 8).HasAttribute("disabled"));
        Assert.False(Day(cut, 10).HasAttribute("disabled"));

        Day(cut, 10).Click(); // commits the week start even though part of the week precedes Min
        Assert.Equal(FeblyWeekStart, value);
    }

    [Fact]
    public void Week_mode_min_and_max_disable_commit_at_week_granularity()
    {
        // Min = Feb 1 2026 (itself a Sunday, so also that week's own start); Max = Feb 21 2026 (the
        // Saturday ending the Feb 15-21 week). Typed plain dates (not the "yyyy-Www" shorthand) so
        // the assertions don't depend on the week-number arithmetic's own known boundary behavior
        // (see TryParseDate's comment) -- only IsWeekDisabledForCommit's guard is under test here.
        DateTime? Commit(string text)
        {
            DateTime? value = null;
            var cut = RenderComponent<DatePicker>(p => p
                .Add(c => c.Mode, DatePickerMode.Week)
                .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
                .Add(c => c.Min, new DateTime(2026, 2, 1))
                .Add(c => c.Max, new DateTime(2026, 2, 21))
                .Add(c => c.ValueChanged, (DateTime? v) => value = v));
            Open(cut);
            cut.Find(".wss-picker-input-date").Input(text);
            cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });
            return value;
        }

        Assert.Null(Commit("01/28/2026"));                            // week Jan 25-31 -- entirely before Min
        Assert.Equal(new DateTime(2026, 2, 1), Commit("02/03/2026"));  // Min's own week (Feb 1-7)
        Assert.Equal(new DateTime(2026, 2, 15), Commit("02/18/2026")); // Max's own week (Feb 15-21)
        Assert.Null(Commit("02/25/2026"));                            // week Feb 22-28 -- entirely after Max
    }

    [Fact]
    public void Week_mode_arrow_keys_still_move_the_roving_tabindex()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14));

        Open(cut);
        Assert.Equal("0", Day(cut, 14).GetAttribute("tabindex"));

        cut.Find(".wss-picker-grid-rows").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", Day(cut, 15).GetAttribute("tabindex"));
    }

    [Fact]
    public void Week_text_with_a_dash_parses_and_normalizes_to_the_week_start()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        // 2023-01-01 is itself a Sunday, so the week-1 arithmetic needs no prior-year-shift
        // adjustment -- a clean case to assert the shorthand's parse against a known date.
        cut.Find(".wss-picker-input-date").Input("2023-W08");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2023, 2, 19), value); // the Sunday starting week 8 of 2023
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Week_text_without_a_dash_and_lowercase_w_also_parses()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("2023w8");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2023, 2, 19), value);
    }

    [Fact]
    public void Week_display_shows_the_year_week_shorthand_for_a_bound_value()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, new DateTime(2023, 2, 21))); // within week 8 of 2023 (Feb 19-25)

        Assert.Equal("2023-W08", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    [Fact]
    public void Week_shorthand_round_trips_when_the_year_starts_mid_week()
    {
        // 2026-01-01 is a Thursday: under CalendarWeekRule.FirstDay the partial Jan 1-3 week is
        // numbered 1, so every later week start's number is one ahead of plain (N-1)*7 arithmetic
        // from WeekStart(Jan 1). The parse must invert the DISPLAY's GetWeekOfYear numbering --
        // whatever shorthand the input shows for a value must commit that same week back.
        DateTime? value = new DateTime(2026, 2, 18); // mid-week value, week start Feb 15 (Sunday)
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        var shown = cut.Find(".wss-picker-input-date").GetAttribute("value");
        Assert.NotNull(shown);

        Open(cut);
        cut.Find(".wss-picker-input-date").Input(shown);
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 2, 15), value); // the week start the display described
    }

    [Fact]
    public void Week_text_with_no_year_is_rejected()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("w8");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Null(cut.Instance.Value); // nothing committed
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open
    }

    [Fact]
    public void Typed_plain_date_in_week_mode_normalizes_to_its_week_start()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-input-date").Input("02/14/2026");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(FeblyWeekStart, value);
    }

    [Fact]
    public void Explicit_format_in_week_mode_is_used_verbatim()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.Format, "yyyy-MM-dd")
            .Add(c => c.Value, new DateTime(2026, 2, 14)));

        // No .NET format token renders the week number -- an explicit Format formats Value verbatim
        // via ToString, same as Quarter's equivalent case.
        Assert.Equal("2026-02-14", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    [Fact]
    public void ShowWeekNumbers_in_date_mode_adds_the_column_without_changing_commit_semantics()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.ShowWeekNumbers, true)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-grid")); // the rows layout, not the flat grid
        Assert.Equal(6, cut.FindAll(".wss-picker-week-row").Count);
        Assert.Single(cut.FindAll(".wss-picker-week-no-header"));
        // No week-selection styling outside Mode.Week -- ShowWeekNumbers only adds the column.
        Assert.Empty(cut.FindAll(".wss-picker-grid-week"));
        Assert.Empty(cut.FindAll(".wss-picker-week-row-selected"));
        Assert.Contains("wss-picker-day-selected", Day(cut, 14).ClassList); // single-day styling unaffected

        Day(cut, 10).Click(); // a day click still commits that DAY, not its week start

        Assert.Equal(new DateTime(2026, 2, 10), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    // ----- DisabledDate / DisabledTime / HideDisabledTimeOptions -----------------

    [Fact]
    public void DisabledDate_disables_matching_day_cells_and_the_typed_commit_guard_rejects_them()
    {
        // Feb 14 2026 is a Saturday (per the fixture's own convention elsewhere in this class) --
        // disable weekends and confirm both the cell attribute and the typed-text commit guard agree.
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)));

        Open(cut);

        Assert.True(Day(cut, 14).HasAttribute("disabled"));  // Saturday
        Assert.True(Day(cut, 15).HasAttribute("disabled"));  // Sunday
        Assert.False(Day(cut, 16).HasAttribute("disabled")); // Monday

        var input = cut.Find(".wss-picker-input-date");
        input.Input("02/14/2026"); // a disabled Saturday
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Feb14, cut.Instance.Value); // rejected -- unchanged
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open
    }

    [Fact]
    public void DisabledDate_at_month_granularity_disables_month_cells_using_the_month_start_argument()
    {
        var seenArgs = new List<DateTime>();
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Value, Feb14)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d =>
            {
                seenArgs.Add(d);
                return d.Month == 7;
            })));

        Open(cut);

        Assert.True(MonthButton(cut, 7).HasAttribute("disabled"));
        Assert.False(MonthButton(cut, 6).HasAttribute("disabled"));
        Assert.Contains(new DateTime(2026, 7, 1), seenArgs); // the month START, not just any July date
    }

    [Fact]
    public void Week_mode_click_is_rejected_when_disableddate_rejects_the_week_start_even_though_the_clicked_day_does_not()
    {
        // DisabledDate rejects ONLY the week start (Feb 8) -- the clicked day (Feb 10) evaluates the
        // same predicate against ITS OWN day-granularity argument and comes back false, so its button
        // stays enabled. Unlike Min/Max (where a disabled week always implies every day in it is also
        // disabled), an arbitrary predicate can disagree between the two granularities -- the click
        // path must still guard the week-start commit explicitly (see OnDayClickAsync).
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d == FeblyWeekStart))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        Assert.False(Day(cut, 10).HasAttribute("disabled")); // the clicked day's own button stays enabled

        Day(cut, 10).Click();

        Assert.Null(value); // the week-start commit itself is still rejected
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // nothing committed -- stays open
    }

    [Fact]
    public void DisabledTime_disables_listed_hour_options_and_blocks_the_select_change_commit()
    {
        DateTime? value = new DateTime(2026, 2, 14, 10, 0, 0);
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, value)
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [13])))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        var options = TimeSelects(cut)[0].QuerySelectorAll("option");
        Assert.Equal(24, options.Length); // rendered disabled, not hidden -- HideDisabledTimeOptions defaults false
        Assert.True(options.Single(o => o.GetAttribute("value") == "13").HasAttribute("disabled"));
        Assert.False(options.Single(o => o.GetAttribute("value") == "12").HasAttribute("disabled"));

        TimeSelects(cut)[0].Change("13"); // ApplyTimePartAsync's hour path

        Assert.Equal(new DateTime(2026, 2, 14, 10, 0, 0), value); // rejected -- unchanged
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void HideDisabledTimeOptions_omits_other_disabled_hours_from_the_select()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 10, 0, 0))
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [13, 14])))
            .Add(c => c.HideDisabledTimeOptions, true));

        Open(cut);

        var options = TimeSelects(cut)[0].QuerySelectorAll("option");
        // Neither 13 nor 14 is the current value (10) -- both are omitted entirely.
        Assert.DoesNotContain(options, o => o.GetAttribute("value") == "13");
        Assert.DoesNotContain(options, o => o.GetAttribute("value") == "14");
        Assert.Equal(22, options.Length); // 24 hours minus the 2 fully-hidden ones
    }

    [Fact]
    public void The_current_values_time_option_stays_selected_and_disabled_under_both_hide_flag_states()
    {
        DisabledTimeParts DisableThirteen(DateTime? date) => new(Hours: [13]);

        var shown = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 0, 0)) // the bound hour IS on the disabled list
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)DisableThirteen)
            .Add(c => c.HideDisabledTimeOptions, false));
        Open(shown);
        var shownOption = TimeSelects(shown)[0].QuerySelectorAll("option").Single(o => o.GetAttribute("value") == "13");
        Assert.True(shownOption.HasAttribute("disabled"));
        Assert.NotNull(shownOption.GetAttribute("selected"));

        var hidden = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 13, 0, 0))
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)DisableThirteen)
            .Add(c => c.HideDisabledTimeOptions, true));
        Open(hidden);
        var hiddenOptions = TimeSelects(hidden)[0].QuerySelectorAll("option");
        // Still present -- the never-jump rule: a select never silently hides the bound value's own
        // option, even though HideDisabledTimeOptions is filtering every OTHER disabled option out.
        var hiddenOption = hiddenOptions.Single(o => o.GetAttribute("value") == "13");
        Assert.True(hiddenOption.HasAttribute("disabled"));
        Assert.NotNull(hiddenOption.GetAttribute("selected"));
        Assert.Equal(24, hiddenOptions.Length); // only hour 13 is disabled, and it's the current value
    }

    [Fact]
    public void DisabledTime_receives_null_date_part_when_value_is_null()
    {
        DateTime? seenDate = new DateTime(1, 1, 1); // sentinel, overwritten by the callback below
        var sawInvocation = false;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)(date =>
            {
                seenDate = date;
                sawInvocation = true;
                return null;
            })));

        Open(cut);

        Assert.True(sawInvocation);
        Assert.Null(seenDate);
    }

    // ----- ShowSeconds / HourStep / MinuteStep / SecondStep / Use12Hours ---------

    [Fact]
    public void ShowSeconds_false_renders_two_selects()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ShowSeconds, false));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(2, selects.Count);
        Assert.Equal("Hour", selects[0].GetAttribute("aria-label"));
        Assert.Equal("Minute", selects[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowSeconds_false_with_use12hours_renders_three_selects_hour_minute_period()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ShowSeconds, false)
            .Add(c => c.Use12Hours, true));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(3, selects.Count);
        Assert.Equal("AM/PM", selects[2].GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowSeconds_false_select_change_commits_zero_seconds_over_a_stale_second()
    {
        DateTime? value = new DateTime(2026, 2, 14, 10, 30, 45);
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ShowSeconds, false)
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // only hour/minute selects exist -- no seconds to change

        Assert.Equal(13, value!.Value.Hour);
        Assert.Equal(30, value.Value.Minute); // untouched part preserved
        Assert.Equal(0, value.Value.Second);  // the pre-existing 45 never survives normalization
    }

    [Fact]
    public void ShowSeconds_false_typed_datetime_commit_zeroes_the_parsed_seconds()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.ShowSeconds, false)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-date");
        // The exact-format attempt ("MM/dd/yyyy HH:mm", no seconds token) misses; the general-parse
        // fallback still reads the seconds, and NormalizeForMode is what actually drops them.
        input.Input("02/14/2026 13:45:30");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2026, 2, 14, 13, 45, 0), value);
    }

    [Fact]
    public void MinuteStep_15_lists_only_the_stepped_options_when_the_value_is_on_lattice()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.MinuteStep, 15)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 10, 30, 0)));

        Open(cut);

        var values = TimeSelects(cut)[1].QuerySelectorAll("option").Select(o => o.GetAttribute("value"));
        Assert.Equal(["0", "15", "30", "45"], values);
    }

    [Fact]
    public void MinuteStep_15_keeps_an_off_lattice_bound_value_visible_and_selected_never_jump()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.MinuteStep, 15)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 10, 37, 0)));

        Open(cut);

        var options = TimeSelects(cut)[1].QuerySelectorAll("option");
        Assert.Equal(["0", "15", "30", "37", "45"], options.Select(o => o.GetAttribute("value")));
        Assert.Equal("37", options.Single(o => o.HasAttribute("selected")).GetAttribute("value"));
    }

    [Fact]
    public void Steps_below_one_clamp_to_one_instead_of_throwing()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.HourStep, 0)
            .Add(c => c.MinuteStep, -5)
            .Add(c => c.SecondStep, -1));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(24, selects[0].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[1].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[2].QuerySelectorAll("option").Length);
    }

    [Fact]
    public void MinuteStep_composes_with_DisabledTime_step_filtering_first_then_disabled_hide()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 10, 37, 0)) // off-lattice bound minute
            .Add(c => c.MinuteStep, 15)
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Minutes: [5, 30])))
            .Add(c => c.HideDisabledTimeOptions, true));

        Open(cut);

        var values = TimeSelects(cut)[1].QuerySelectorAll("option").Select(o => o.GetAttribute("value"));
        // 30 was on the step lattice and disabled+hidden; 5 was never a step candidate to begin with
        // (simply absent -- nothing extra to do); 37 (the bound value, off-lattice) stays visible via
        // the step's own never-jump rule even though DisabledTime never even mentions it.
        Assert.Equal(["0", "15", "37", "45"], values);
    }

    [Fact]
    public void An_off_lattice_bound_value_that_DisabledTime_also_disables_stays_selected_and_visible()
    {
        // Both never-jump rules stack on the same option: the step lattice keeps 37 present at all
        // (it isn't a multiple of 15), and HideDisabledTimeOptions' own never-jump keeps it visible
        // (disabled, but not hidden) even though it's on DisabledTime's own list.
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 10, 37, 0))
            .Add(c => c.MinuteStep, 15)
            .Add(c => c.DisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Minutes: [37])))
            .Add(c => c.HideDisabledTimeOptions, true));

        Open(cut);

        var option37 = TimeSelects(cut)[1].QuerySelectorAll("option").Single(o => o.GetAttribute("value") == "37");
        Assert.True(option37.HasAttribute("disabled"));
        Assert.NotNull(option37.GetAttribute("selected"));
    }

    [Fact]
    public void Use12Hours_renders_12_hour_option_text_for_the_current_period_and_a_period_select()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Use12Hours, true)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 14, 0, 0))); // 2 PM

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(4, selects.Count); // hour, minute, second, period

        var hourOptions = selects[0].QuerySelectorAll("option");
        Assert.Equal(12, hourOptions.Length); // just the PM half of the day
        Assert.Equal(["12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23"],
            hourOptions.Select(o => o.GetAttribute("value"))); // option VALUES stay 24h
        Assert.Equal(["12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11"],
            hourOptions.Select(o => o.TextContent)); // option TEXT is 12-hour reading order
        Assert.Equal("14", hourOptions.Single(o => o.HasAttribute("selected")).GetAttribute("value"));

        var periodSelect = selects[3];
        Assert.Equal("AM/PM", periodSelect.GetAttribute("aria-label"));
        var periodOptions = periodSelect.QuerySelectorAll("option");
        Assert.Equal(["AM", "PM"], periodOptions.Select(o => o.GetAttribute("value")));
        Assert.Equal(["AM", "PM"], periodOptions.Select(o => o.TextContent)); // culture designators
        Assert.Equal("PM", periodOptions.Single(o => o.HasAttribute("selected")).GetAttribute("value"));
    }

    [Fact]
    public void Use12Hours_picking_the_hour_2_pm_option_commits_the_24_hour_value_14()
    {
        DateTime? value = new DateTime(2026, 2, 14, 13, 0, 0); // 1 PM
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Use12Hours, true)
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        TimeSelects(cut)[0].Change("14"); // the option labeled "2" in the PM period

        Assert.Equal(14, value!.Value.Hour);
    }

    [Fact]
    public void Use12Hours_switching_period_am_to_pm_recommits_the_current_hour_shifted_by_12()
    {
        DateTime? value = new DateTime(2026, 2, 14, 9, 15, 0); // 9:15 AM
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Use12Hours, true)
            .Add(c => c.Value, value)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Assert.Equal("AM", TimeSelects(cut)[3].QuerySelector("option[selected]")!.GetAttribute("value"));

        TimeSelects(cut)[3].Change("PM");

        Assert.Equal(21, value!.Value.Hour);  // 9 % 12 + 12
        Assert.Equal(15, value.Value.Minute); // untouched
    }

    [Fact]
    public void Use12Hours_with_a_null_value_displays_12_am_and_selects_the_am_period()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Use12Hours, true));

        Open(cut);

        var selects = TimeSelects(cut);
        var selectedHour = selects[0].QuerySelector("option[selected]")!;
        Assert.Equal("0", selectedHour.GetAttribute("value")); // stays the 24h value 0 internally
        Assert.Equal("12", selectedHour.TextContent);          // displayed as 12 (AM)
        Assert.Equal("AM", selects[3].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Use12Hours_changes_the_default_effective_format_to_h_mm_ss_tt()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Use12Hours, true)
            .Add(c => c.Value, new DateTime(2026, 2, 14, 14, 5, 9)));

        Assert.Equal("2:05:09 PM", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    // ----- ShowToday / ShowNow / Presets / ExtraFooter / DefaultViewDate ---------

    [Fact]
    public void ShowToday_commits_todays_date_and_closes_in_date_mode()
    {
        DateTime? value = null;
        var cut = RenderPicker(p => p
            .Add(c => c.ShowToday, true)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        Assert.Equal("Today", cut.Find(".wss-picker-today-btn").TextContent);
        cut.Find(".wss-picker-today-btn").Click();

        Assert.Equal(DateTime.Today, value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void ShowToday_commits_the_first_of_the_current_month_and_closes_in_month_mode()
    {
        // The "one coarse mode" case -- ShowToday's normalization runs through the same
        // NormalizeForMode every other commit path uses, so Month mode lands on the 1st.
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.ShowToday, true)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-today-btn").Click();

        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void ShowToday_link_disables_when_disableddate_rejects_today_and_the_commit_guard_agrees()
    {
        DateTime? value = null;
        var cut = RenderPicker(p => p
            .Add(c => c.ShowToday, true)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d.Date == DateTime.Today))
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);

        var button = cut.Find(".wss-picker-today-btn");
        Assert.True(button.HasAttribute("disabled")); // rendered disabled, not hidden

        button.Click(); // the C# guard must reject this even though bUnit dispatches regardless
        Assert.Null(value);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // nothing committed -- stays open
    }

    [Fact]
    public void No_footer_renders_in_date_mode_when_showtoday_is_false_regression_guard()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-footer"));
        Assert.Empty(cut.FindAll(".wss-picker-today-btn"));
    }

    [Fact]
    public void ShowNow_commits_datetime_now_without_closing_in_time_mode()
    {
        DateTime? value = null;
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ShowNow, true)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        var before = DateTime.Now;
        Open(cut);
        Assert.Equal("Now", cut.Find(".wss-picker-today-btn").TextContent);
        cut.Find(".wss-picker-today-btn").Click();
        var after = DateTime.Now;

        Assert.NotNull(value);
        // Time mode anchors to Today (NormalizeForMode) -- +/-2s tolerates the select's own
        // whole-second truncation and the real time that elapses between the two DateTime.Now calls.
        Assert.InRange(value!.Value, before.AddSeconds(-2), after.AddSeconds(2));
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open -- OK remains the close signal
    }

    [Fact]
    public void Preset_click_resolves_at_click_time_not_at_render_time()
    {
        // The Func is invoked when the button is clicked, not when the list was built -- mutate the
        // state it reads AFTER Open() renders the sidebar but BEFORE the click, and confirm the
        // CHANGED value wins.
        DateTime? value = null;
        var resolveTo = new DateTime(2026, 1, 1);
        var presets = new List<DatePickerPreset> { new("Dynamic", () => resolveTo) };
        var cut = RenderPicker(p => p
            .Add(c => c.Presets, presets)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        resolveTo = new DateTime(2026, 6, 15);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2026, 6, 15), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Preset_click_commits_in_time_mode_and_still_closes_unlike_the_incremental_selects()
    {
        // A preset is a complete pick even in Time/DateTime mode -- unlike the time selects
        // (ApplyTimePartAsync), which never close, this closes just like every other mode.
        DateTime? value = null;
        var presets = new List<DatePickerPreset> { new("Noon", () => new DateTime(2026, 2, 14, 12, 0, 0)) };
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Presets, presets)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, DateTime.Today.Day, 12, 0, 0), value);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Guard_rejected_preset_no_ops()
    {
        DateTime? value = Feb14;
        var presets = new List<DatePickerPreset> { new("Before Min", () => new DateTime(2026, 1, 1)) };
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 2, 1))
            .Add(c => c.Presets, presets)
            .Add(c => c.ValueChanged, (DateTime? v) => value = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(Feb14, value); // rejected by Min -- unchanged
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // nothing committed -- stays open
    }

    [Fact]
    public void Presets_sidebar_renders_reusing_the_daterangepickers_preset_classes()
    {
        var presets = new List<DatePickerPreset>
        {
            new("Today", () => DateTime.Today),
            new("Tomorrow", () => DateTime.Today.AddDays(1)),
        };
        var cut = RenderPicker(p => p.Add(c => c.Presets, presets));

        Open(cut);

        var list = cut.Find(".wss-picker-presets");
        Assert.Equal("Quick picks", list.GetAttribute("aria-label"));
        var buttons = cut.FindAll(".wss-picker-preset");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Today", buttons[0].TextContent);
        Assert.Equal("Tomorrow", buttons[1].TextContent);
    }

    [Fact]
    public void No_presets_sidebar_renders_when_presets_is_unset_regression_guard()
    {
        var cut = RenderPicker(p => p.Add(c => c.Value, Feb14));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-presets"));
    }

    [Fact]
    public void ExtraFooter_renders_its_own_strip_in_date_mode()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.ExtraFooter, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"my-extra\">extra</span>"))));

        Open(cut);

        Assert.Equal("extra", cut.Find(".wss-picker-extra-footer .my-extra").TextContent);
    }

    [Fact]
    public void ExtraFooter_renders_alongside_the_existing_footer_in_time_mode()
    {
        var cut = RenderComponent<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.ExtraFooter, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"my-extra\">extra</span>"))));

        Open(cut);

        Assert.Equal("extra", cut.Find(".wss-picker-extra-footer .my-extra").TextContent);
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok")); // the existing OK footer still renders too
    }

    [Fact]
    public void DefaultViewDate_drives_the_opened_view_when_value_is_null()
    {
        var cut = RenderPicker(p => p.Add(c => c.DefaultViewDate, new DateTime(2030, 8, 1)));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("8", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2030", selects[1].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void DefaultViewDate_is_ignored_once_value_is_set()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Value, Feb14)
            .Add(c => c.DefaultViewDate, new DateTime(2030, 8, 1)));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("2", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2026", selects[1].QuerySelector("option[selected]")!.GetAttribute("value"));
    }
}
