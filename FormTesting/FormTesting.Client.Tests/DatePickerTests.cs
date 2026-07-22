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
}
