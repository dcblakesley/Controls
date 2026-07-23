using System.Globalization;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the <see cref="DateRangePicker"/> UI-kit control: open/close, the two-click
/// range pick (including the backwards swap), presets, Min/Max day disabling, typed input,
/// clearing, linked month navigation and the ARIA wiring. The JS-owned behaviors (viewport
/// flip/clamp, Enter submit-suppression, focus-out close) are covered by the e2e suite —
/// bUnit does not execute JavaScript.
/// </summary>
public class DateRangePickerTests : TestContext
{
    public DateRangePickerTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    // Fixed inputs so nothing depends on the machine's culture or the test run's date.
    static readonly DateTime Jan15 = new(2025, 1, 15);
    static readonly DateTime Feb3 = new(2025, 2, 3);

    IRenderedComponent<DateRangePicker> RenderPicker(
        Action<ComponentParameterCollectionBuilder<DateRangePicker>>? configure = null) =>
        RenderComponent<DateRangePicker>(p =>
        {
            p.Add(c => c.Format, "MM/dd/yyyy");
            p.Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday);
            configure?.Invoke(p);
        });

    static void Open(IRenderedComponent<DateRangePicker> cut) =>
        cut.Find(".wss-picker-input").Click();

    // The first month panel's in-month day button for the given day number (skips the leading
    // adjacent-month cells, which carry wss-picker-day-outside).
    static IElement Day(IRenderedComponent<DateRangePicker> cut, int panel, int dayNumber) =>
        cut.FindAll(".wss-picker-month")[panel]
            .QuerySelectorAll(".wss-picker-day")
            .First(b => !b.ClassList.Contains("wss-picker-day-outside") &&
                        b.TextContent == dayNumber.ToString("00", CultureInfo.InvariantCulture));

    // Mode="Month": the given panel's 12-button grid always renders Jan..Dec in order -- no
    // outside/leading cells to skip, unlike Day() above.
    static IElement MonthButton(IRenderedComponent<DateRangePicker> cut, int panel, int monthNumber) =>
        cut.FindAll(".wss-picker-month")[panel].QuerySelectorAll(".wss-picker-month-btn")[monthNumber - 1];

    // Mode="Quarter": the given panel's 4-button row always renders Q1..Q4 in order.
    static IElement QuarterButton(IRenderedComponent<DateRangePicker> cut, int panel, int quarterNumber) =>
        cut.FindAll(".wss-picker-month")[panel].QuerySelectorAll(".wss-picker-quarter-grid .wss-picker-month-btn")[quarterNumber - 1];

    // Mode="Year": the given panel's 12-cell grid always renders decadeStart-1 .. decadeStart+10, in
    // order -- index 0 and 11 are the dimmed adjacent-decade cells.
    static IElement YearButton(IRenderedComponent<DateRangePicker> cut, int panel, int index) =>
        cut.FindAll(".wss-picker-month")[panel].QuerySelectorAll(".wss-picker-month-btn")[index];

    [Fact]
    public void Closed_picker_renders_the_field_only_with_format_derived_placeholders()
    {
        var cut = RenderPicker();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Empty(cut.FindAll(".wss-picker-backdrop"));
        Assert.Equal("MM/DD/YYYY", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
        Assert.Equal("MM/DD/YYYY", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
        Assert.Equal("false", cut.Find(".wss-picker-input-start").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void Field_click_opens_a_dialog_showing_the_start_month_and_the_next()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.End, Feb3));

        Open(cut);

        var dialog = cut.Find(".wss-picker-dropdown");
        Assert.Equal("dialog", dialog.GetAttribute("role"));
        Assert.Equal("true", cut.Find(".wss-picker-input-start").GetAttribute("aria-expanded"));

        var months = cut.FindAll(".wss-picker-month");
        Assert.Equal(2, months.Count);
        // Left panel = January 2025 (the bound start), right = February 2025.
        Assert.Equal("1", months[0].QuerySelectorAll("select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2025", months[0].QuerySelectorAll("select")[1].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2", months[1].QuerySelectorAll("select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2025", months[1].QuerySelectorAll("select")[1].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Two_day_clicks_commit_the_range_and_close()
    {
        DateTime? start = null, end = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.End, Feb3)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        Day(cut, 0, 10).Click(); // first click: pending start, still open
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
        Day(cut, 1, 20).Click(); // second click: commits and closes

        Assert.Equal(new DateTime(2025, 1, 10), start);
        Assert.Equal(new DateTime(2025, 2, 20), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void A_backwards_second_click_swaps_the_endpoints()
    {
        DateTime? start = null, end = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        Day(cut, 1, 20).Click(); // Feb 20 first...
        Day(cut, 0, 10).Click(); // ...then Jan 10 — swapped on commit

        Assert.Equal(new DateTime(2025, 1, 10), start);
        Assert.Equal(new DateTime(2025, 2, 20), end);
    }

    [Fact]
    public void While_picking_the_grid_highlights_only_the_pending_start()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.End, Feb3));

        Open(cut);
        // The committed range renders selected endpoints + an in-range band. Three, not two:
        // Feb 03 is also visible as a grayed adjacent-month cell in the January grid, and
        // (matching AntD) adjacent-month cells still carry the range highlighting.
        Assert.Equal(3, cut.FindAll(".wss-picker-day-selected").Count);
        Assert.NotEmpty(cut.FindAll(".wss-picker-cell-in-range"));

        Day(cut, 0, 10).Click();

        // ...but a fresh pick replaces the highlight with just the pending start.
        var selected = cut.FindAll(".wss-picker-day-selected");
        Assert.Single(selected);
        Assert.Equal("10", selected[0].TextContent);
        Assert.Empty(cut.FindAll(".wss-picker-cell-in-range"));
    }

    [Fact]
    public void Escape_discards_the_pending_pick_and_keeps_the_committed_range()
    {
        DateTime? start = Jan15;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.End, Feb3)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        Day(cut, 0, 10).Click();
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(Jan15, start); // no commit happened
        Assert.Equal("01/15/2025", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Backdrop_click_closes_without_committing()
    {
        DateTime? start = Jan15;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        Day(cut, 0, 10).Click();
        cut.Find(".wss-picker-backdrop").Click();

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(Jan15, start);
    }

    [Fact]
    public void Preset_click_commits_its_resolved_range_clamped_to_min_and_closes()
    {
        DateTime? start = null, end = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2024, 12, 1))
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2024, 11, 20), new DateTime(2025, 1, 5)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        var preset = cut.Find(".wss-picker-preset");
        Assert.Equal("Fixed", preset.TextContent);
        preset.Click();

        Assert.Equal(new DateTime(2024, 12, 1), start); // clamped up to Min
        Assert.Equal(new DateTime(2025, 1, 5), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void No_presets_renders_no_sidebar()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);
        Assert.Empty(cut.FindAll(".wss-picker-presets"));
    }

    [Fact]
    public void Days_outside_min_max_are_disabled()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2025, 1, 10))
            .Add(c => c.Max, new DateTime(2025, 2, 20)));

        Open(cut);

        Assert.True(Day(cut, 0, 9).HasAttribute("disabled"));
        Assert.False(Day(cut, 0, 10).HasAttribute("disabled"));
        Assert.False(Day(cut, 1, 20).HasAttribute("disabled"));
        Assert.True(Day(cut, 1, 21).HasAttribute("disabled"));
    }

    [Fact]
    public void Clear_button_clears_both_ends()
    {
        DateTime? start = Jan15, end = Feb3;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.End, Feb3)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        cut.Find(".wss-picker-clear").Click();

        Assert.Null(start);
        Assert.Null(end);
    }

    [Fact]
    public void Clear_button_is_absent_when_empty_disabled_or_not_allowed()
    {
        Assert.Empty(RenderPicker().FindAll(".wss-picker-clear"));
        Assert.Empty(RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.Disabled, true)).FindAll(".wss-picker-clear"));
        Assert.Empty(RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.AllowClear, false)).FindAll(".wss-picker-clear"));
    }

    [Fact]
    public void Typed_start_commits_on_change_and_swaps_past_the_end()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            DateTime? start = null, end = null;
            var cut = RenderPicker(p => p
                .Add(c => c.Start, Jan15)
                .Add(c => c.End, Feb3)
                .Add(c => c.StartChanged, (DateTime? v) => start = v)
                .Add(c => c.EndChanged, (DateTime? v) => end = v));

            var input = cut.Find(".wss-picker-input-start");
            input.Input("03/10/2025"); // past the current end
            input.Change("03/10/2025");

            Assert.Equal(Feb3, start); // swapped: old end becomes the start
            Assert.Equal(new DateTime(2025, 3, 10), end);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Invalid_typed_text_reverts_to_the_bound_value()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            DateTime? start = Jan15;
            var cut = RenderPicker(p => p
                .Add(c => c.Start, Jan15)
                .Add(c => c.StartChanged, (DateTime? v) => start = v));

            var input = cut.Find(".wss-picker-input-start");
            input.Input("not a date");
            input.Change("not a date");

            Assert.Equal(Jan15, start); // unchanged
            Assert.Equal("01/15/2025", input.GetAttribute("value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Emptying_an_input_clears_that_end_only()
    {
        DateTime? start = Jan15, end = Feb3;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.End, Feb3)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        var input = cut.Find(".wss-picker-input-start");
        input.Input("");
        input.Change("");

        Assert.Null(start);
        Assert.Equal(Feb3, end);
    }

    [Fact]
    public void Month_select_keeps_the_two_panels_consecutive()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);

        // Jump the RIGHT panel to June → left must follow to May.
        cut.FindAll(".wss-picker-month")[1].QuerySelectorAll("select")[0].Change("6");

        var months = cut.FindAll(".wss-picker-month");
        Assert.Equal("5", months[0].QuerySelectorAll("select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("6", months[1].QuerySelectorAll("select")[0].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Year_select_range_is_bounded_by_min_and_max()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2017, 1, 1))
            .Add(c => c.Max, new DateTime(2026, 12, 31)));
        Open(cut);

        var yearOptions = cut.FindAll(".wss-picker-month")[0]
            .QuerySelectorAll("select")[1].QuerySelectorAll("option");
        Assert.Equal("2017", yearOptions.First().GetAttribute("value"));
        Assert.Equal("2026", yearOptions.Last().GetAttribute("value"));
    }

    [Fact]
    public void Grid_layout_honors_first_day_of_week()
    {
        // Jan 1 2025 is a Wednesday: Sunday-start grids lead with 3 outside days, Monday-start with 2.
        var sunday = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(sunday);
        var sundayLead = sunday.FindAll(".wss-picker-month")[0].QuerySelectorAll(".wss-picker-day")
            .TakeWhile(b => b.ClassList.Contains("wss-picker-day-outside")).Count();
        Assert.Equal(3, sundayLead);

        var monday = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Start, Jan15));
        Open(monday);
        var mondayLead = monday.FindAll(".wss-picker-month")[0].QuerySelectorAll(".wss-picker-day")
            .TakeWhile(b => b.ClassList.Contains("wss-picker-day-outside")).Count();
        Assert.Equal(2, mondayLead);

        // Fixed 6-row grid: always 42 cells per month.
        Assert.Equal(42, sunday.FindAll(".wss-picker-month")[0].QuerySelectorAll(".wss-picker-day").Length);
    }

    [Fact]
    public void Focusing_an_input_opens_with_that_side_underlined()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));

        cut.Find(".wss-picker-input-end").Focus();

        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
        var slots = cut.FindAll(".wss-picker-input-slot");
        Assert.DoesNotContain("wss-picker-slot-active", slots[0].ClassList);
        Assert.Contains("wss-picker-slot-active", slots[1].ClassList);

        // Picking the first day of a range moves the underline to the end side; a field-chrome
        // click (after reopening) starts back at the start side.
        cut.Find(".wss-picker-backdrop").Click();
        Open(cut);
        Assert.Contains("wss-picker-slot-active", cut.FindAll(".wss-picker-input-slot")[0].ClassList);
        Day(cut, 0, 10).Click();
        Assert.Contains("wss-picker-slot-active", cut.FindAll(".wss-picker-input-slot")[1].ClassList);
    }

    [Fact]
    public void Disabled_picker_does_not_open()
    {
        var cut = RenderPicker(p => p.Add(c => c.Disabled, true));
        Open(cut);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.True(cut.Find(".wss-picker-input-start").HasAttribute("disabled"));
    }

    [Fact]
    public void Aria_wiring_names_the_inputs_dialog_and_selects()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.Presets, new[]
        {
            new DateRangePreset("Fixed", Jan15, Feb3),
        }));
        Open(cut);

        Assert.Equal("Start date", cut.Find(".wss-picker-input-start").GetAttribute("aria-label"));
        Assert.Equal("End date", cut.Find(".wss-picker-input-end").GetAttribute("aria-label"));
        Assert.Equal("Choose date range", cut.Find(".wss-picker-dropdown").GetAttribute("aria-label"));
        Assert.Equal("Quick ranges", cut.Find(".wss-picker-presets").GetAttribute("aria-label"));
        Assert.Equal("Month", cut.FindAll(".wss-picker-month select")[0].GetAttribute("aria-label"));
        Assert.Equal("Year", cut.FindAll(".wss-picker-month select")[1].GetAttribute("aria-label"));
        // Endpoint days announce their pressed state.
        Assert.Equal("true", Day(cut, 0, 15).GetAttribute("aria-pressed"));
        Assert.Equal("false", Day(cut, 0, 16).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void A_typed_end_commit_mid_pick_finalizes_state_so_the_display_matches_the_bound_values()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));

        Open(cut);
        Day(cut, 0, 10).Click(); // pending pick: Jan 10 — _selecting is now true

        var endInput = cut.Find(".wss-picker-input-end");
        endInput.Input("02/20/2025");
        endInput.Change("02/20/2025");

        // The typed commit finalizes the field: the start side reverts to displaying the bound
        // value (Jan 15, untouched by the discarded Jan 10 pending pick) instead of contradicting it.
        Assert.Equal("01/15/2025", cut.Find(".wss-picker-input-start").GetAttribute("value"));
        Assert.Equal(new DateTime(2025, 2, 20), cut.Instance.End);

        // A later day click starts a brand-new pick rather than resurrecting the discarded Jan 10 —
        // before the fix this would have been read as the SECOND click of the old pick (committing
        // Jan 5 - Jan 10 and closing).
        Day(cut, 0, 5).Click();
        Assert.Equal("01/05/2025", cut.Find(".wss-picker-input-start").GetAttribute("value"));
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Preset_entirely_past_max_clamps_to_max_max()
    {
        DateTime? start = null, end = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Max, new DateTime(2025, 1, 31))
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Future", new DateTime(2025, 3, 1), new DateTime(2025, 3, 15)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 1, 31), start);
        Assert.Equal(new DateTime(2025, 1, 31), end);
    }

    [Fact]
    public void Preset_entirely_before_min_clamps_to_min_min()
    {
        DateTime? start = null, end = null;
        var cut = RenderPicker(p => p
            .Add(c => c.Min, new DateTime(2025, 3, 1))
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Past", new DateTime(2025, 1, 1), new DateTime(2025, 1, 15)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 3, 1), start);
        Assert.Equal(new DateTime(2025, 3, 1), end);
    }

    [Fact]
    public void Year_select_options_are_clamped_to_the_datetime_range_and_selecting_the_max_does_not_throw()
    {
        // Unclamped, ±10 around 9998 would offer up to 10008 — outside DateTime's [1, 9999] years,
        // and constructing `new DateTime(10008, ...)` throws (circuit-killing on Blazor Server).
        var cut = RenderPicker(p => p.Add(c => c.Start, new DateTime(9998, 2, 14)));
        Open(cut);

        var yearSelect = cut.FindAll(".wss-picker-month")[0].QuerySelectorAll("select")[1];
        var options = yearSelect.QuerySelectorAll("option");
        Assert.Equal("9999", options.Last().GetAttribute("value"));

        var ex = Record.Exception(() => yearSelect.Change("9999"));
        Assert.Null(ex);
    }

    [Fact]
    public void Weekday_header_row_matches_the_grids_first_day_of_week()
    {
        var sunday = RenderPicker(p => p.Add(c => c.Start, Jan15)); // fixture pins Sunday
        Open(sunday);
        var sundayNames = sunday.FindAll(".wss-picker-month")[0]
            .QuerySelectorAll(".wss-picker-week-day").Select(d => d.TextContent).ToList();
        Assert.Equal(new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }, sundayNames);
        Assert.Equal("true", sunday.FindAll(".wss-picker-week-header")[0].GetAttribute("aria-hidden"));

        var monday = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Monday)
            .Add(c => c.Start, Jan15));
        Open(monday);
        Assert.Equal("Mo", monday.FindAll(".wss-picker-week-day")[0].TextContent);
    }

    [Fact]
    public void Prev_and_next_month_buttons_appear_only_on_the_outer_edges_and_step_the_view()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);

        var buttons = cut.FindAll("button.wss-picker-nav");
        Assert.Equal(2, buttons.Count); // one prev (left panel), one next (right panel) — not four
        Assert.Equal("Previous month", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next month", buttons[1].GetAttribute("aria-label"));

        // Each panel renders an invisible stand-in for the nav button it omits, keeping the header
        // at three flex children so space-between centers the selects instead of shoving them
        // against the panel divider.
        Assert.Equal(2, cut.FindAll("span.wss-picker-nav-placeholder").Count);

        buttons[0].Click(); // prev: Jan/Feb 2025 -> Dec 2024/Jan 2025
        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("12", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2024", selects[1].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Hovering_during_a_pick_tints_the_inclusive_span_and_clears_on_pointer_leave()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);

        Day(cut, 0, 10).Click(); // pending start = Jan 10
        Day(cut, 0, 15).PointerEnter();

        var previewSelector = ".wss-picker-cell-preview, .wss-picker-cell-preview-start, .wss-picker-cell-preview-end";
        Assert.NotEmpty(cut.FindAll(".wss-picker-month")[0].QuerySelectorAll(previewSelector));

        cut.Find(".wss-picker-grid").PointerLeave();
        Assert.Empty(cut.FindAll(".wss-picker-month")[0].QuerySelectorAll(previewSelector));
    }

    [Fact]
    public void The_start_endpoint_carries_the_roving_tabindex_by_default()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15).Add(c => c.End, Feb3));
        Open(cut);

        Assert.Equal("0", Day(cut, 0, 15).GetAttribute("tabindex"));
        Assert.Equal("-1", Day(cut, 0, 16).GetAttribute("tabindex"));
        Assert.Equal("2025-01-15", Day(cut, 0, 15).GetAttribute("data-date"));
    }

    [Fact]
    public void Arrow_keys_move_the_roving_tabindex_day_without_moving_the_view_when_already_visible()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);

        // Jan 15 + 20 days lands on Feb 4 — already the right panel, so the view shouldn't move.
        for (var i = 0; i < 20; i++)
        {
            cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        }

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("1", selects[0].QuerySelector("option[selected]")!.GetAttribute("value")); // still Jan
        Assert.Equal("2", selects[2].QuerySelector("option[selected]")!.GetAttribute("value")); // still Feb
        Assert.Equal("0", Day(cut, 1, 4).GetAttribute("tabindex"));
    }

    [Fact]
    public void PageDown_that_lands_outside_both_panels_anchors_the_right_panel_to_that_month()
    {
        var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));
        Open(cut);

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" }); // Jan15 -> Feb15 (already visible)
        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" }); // Feb15 -> Mar15 (outside both)

        // A forward crossing anchors the new month as the RIGHT panel, not the left — the view
        // shifts by exactly one month (Jan/Feb -> Feb/Mar), not two (Jan/Feb -> Mar/Apr).
        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("2", selects[0].QuerySelector("option[selected]")!.GetAttribute("value")); // left = Feb
        Assert.Equal("3", selects[2].QuerySelector("option[selected]")!.GetAttribute("value")); // right = Mar
        Assert.Equal("0", Day(cut, 1, 15).GetAttribute("tabindex")); // Mar 15 focused, in the right panel
    }

    [Fact]
    public void Arrow_right_crossing_out_of_the_right_panel_shifts_the_view_by_one_month_not_two()
    {
        // Aug 31 (already the right panel, having paged there from Jul 31) + ArrowRight crosses into
        // September — one day past the visible range. Anchoring September as the LEFT panel (the
        // old, buggy behavior) would jump the view two months to [Sep, Oct] for a one-day move; the
        // fix anchors September as the RIGHT panel instead, so the view moves by exactly one month,
        // to [Aug, Sep].
        var cut = RenderPicker(p => p.Add(c => c.Start, new DateTime(2025, 7, 31)));
        Open(cut);

        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "PageDown" }); // Jul 31 -> Aug 31 (already visible, right panel)
        cut.Find(".wss-picker-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" }); // Aug 31 -> Sep 1 (crosses out)

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("8", selects[0].QuerySelector("option[selected]")!.GetAttribute("value")); // left = Aug
        Assert.Equal("9", selects[2].QuerySelector("option[selected]")!.GetAttribute("value")); // right = Sep
        Assert.Equal("0", Day(cut, 1, 1).GetAttribute("tabindex")); // Sep 1 focused, in the right panel
    }

    [Fact]
    public void Consumer_class_style_and_data_attributes_land_on_the_wrapper()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Width, "300px")
            .AddUnmatched("class", "my-picker")
            .AddUnmatched("style", "margin-top:4px")
            .AddUnmatched("data-foo", "bar"));

        var wrapper = cut.Find(".wss-picker");
        Assert.Contains("my-picker", wrapper.ClassList);
        Assert.Contains("width:300px", wrapper.GetAttribute("style"));
        Assert.Contains("margin-top:4px", wrapper.GetAttribute("style"));
        Assert.Equal("bar", wrapper.GetAttribute("data-foo"));
    }

    [Fact]
    public void Default_focus_day_skips_a_disabled_candidate_and_lands_on_the_first_enabled_day()
    {
        // Aug 14 (the bound start) is disabled by Min = Aug 20 — the naive default (an endpoint,
        // else today, else the left panel's 1st) would land the roving tabindex on a disabled
        // button, making both grids keyboard-unreachable. The far-future year keeps "today" out of
        // view too, so the only viable fallback is the first enabled in-month day: Aug 20 itself.
        var cut = RenderPicker(p => p
            .Add(c => c.Start, new DateTime(9000, 8, 14))
            .Add(c => c.Min, new DateTime(9000, 8, 20)));

        Open(cut);

        var focusStop = Day(cut, 0, 20);
        Assert.Equal("0", focusStop.GetAttribute("tabindex"));
        Assert.False(focusStop.HasAttribute("disabled"));
    }

    [Fact]
    public void Prev_month_button_disables_when_the_left_panel_is_on_mins_month()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2025, 1, 1)));
        Open(cut);

        var buttons = cut.FindAll("button.wss-picker-nav");
        Assert.True(buttons[0].HasAttribute("disabled")); // prev — left panel (January) is Min's month
        Assert.False(buttons[1].HasAttribute("disabled"));
    }

    [Fact]
    public void Next_month_button_disables_when_the_right_panel_is_on_maxs_month()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.Max, new DateTime(2025, 2, 28)));
        Open(cut);

        var buttons = cut.FindAll("button.wss-picker-nav");
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.True(buttons[1].HasAttribute("disabled")); // next — right panel (February) is Max's month
    }

    [Fact]
    public void Non_gregorian_default_calendar_cultures_still_render_gregorian_years()
    {
        // th-TH's default calendar is Thai Buddhist (year = Gregorian + 543). Formatting the day
        // aria-label straight through CurrentCulture would show a Buddhist year (2568) that
        // contradicts the plain Gregorian year the year-select text renders (2025) for the exact
        // same grid. The picker is a Gregorian-calendar control regardless of culture — every
        // picker-internal format should agree on 2025.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var cut = RenderPicker(p => p.Add(c => c.Start, Jan15));

            Open(cut);

            var yearOption = cut.FindAll(".wss-picker-month")[0].QuerySelectorAll("select")[1].QuerySelector("option[selected]")!;
            Assert.Equal("2025", yearOption.TextContent);

            var ariaLabel = cut.Find("[data-date='2025-01-15']").GetAttribute("aria-label")!;
            Assert.Contains("2025", ariaLabel);
            Assert.DoesNotContain("2568", ariaLabel);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ===================================================================================
    // Mode="DateTime" -- single-panel pick session (calendar + time row + OK)
    // ===================================================================================
    // With time enabled the dual-panel layout is abandoned entirely (see the class remarks): a
    // single panel edits one endpoint at a time, ending in an OK that confirms the active endpoint
    // and either advances to the other or (once both are resolved) commits and closes. These tests
    // use RenderComponent directly rather than the shared RenderPicker helper, since RenderPicker
    // pins Format to the Date-mode default -- DateTime/Time need their own time-aware default (or an
    // explicit override) instead.

    static IReadOnlyList<IElement> TimeSelects(IRenderedComponent<DateRangePicker> cut) =>
        cut.FindAll(".wss-picker-time-row select");

    // The single session panel's own in-month day button -- unlike Day(cut, panel, dayNumber) above,
    // there is only ever ONE `.wss-picker-month` panel in this mode, so no panel index is needed.
    static IElement SessionDay(IRenderedComponent<DateRangePicker> cut, int dayNumber) =>
        cut.FindAll(".wss-picker-day")
            .First(b => !b.ClassList.Contains("wss-picker-day-outside") &&
                        b.TextContent == dayNumber.ToString("00", CultureInfo.InvariantCulture));

    [Fact]
    public void Datetime_mode_renders_a_single_calendar_time_row_and_ok_not_dual_panels()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-panels")); // no dual-panel wrapper
        Assert.Single(cut.FindAll(".wss-picker-month")); // exactly one calendar panel
        Assert.Equal(42, cut.FindAll(".wss-picker-day").Count); // the same fixed 6-row grid as Date mode
        Assert.Equal(3, TimeSelects(cut).Count); // hour/minute/second
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok"));
        Assert.Contains("wss-picker-dropdown-single", cut.Find(".wss-picker-dropdown").ClassList);
    }

    [Fact]
    public void Datetime_mode_day_click_sets_the_pending_date_without_firing_either_callback()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15) // anchors the single panel on a known month
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        SessionDay(cut, 20).Click();

        Assert.Null(start);
        Assert.Null(end);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // still open -- nothing committed yet
        Assert.Equal("01/20/2025 00:00:00", cut.Find(".wss-picker-input-start").GetAttribute("value")); // preview, midnight default
    }

    [Fact]
    public void Datetime_mode_day_click_preserves_the_active_endpoints_pending_time_of_day()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 13, 45, 30)));

        Open(cut);
        SessionDay(cut, 20).Click(); // a fresh day pick keeps the already-committed time-of-day

        Assert.Equal("01/20/2025 13:45:30", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Datetime_mode_ok_on_a_fresh_start_side_advances_to_the_end_side_without_committing()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut); // active = start (field click)
        Assert.Contains("wss-picker-slot-active", cut.FindAll(".wss-picker-input-slot")[0].ClassList);

        SessionDay(cut, 10).Click();
        cut.Find(".wss-picker-ok").Click();

        Assert.Null(start); // End is still unresolved -- nothing committed
        Assert.Null(end);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open -- awaiting the end side
        Assert.Contains("wss-picker-slot-active", cut.FindAll(".wss-picker-input-slot")[1].ClassList); // underline moved
    }

    [Fact]
    public void Datetime_mode_second_ok_commits_both_endpoints_and_closes()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15) // anchors the single panel on January 2025
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        SessionDay(cut, 10).Click();
        TimeSelects(cut)[0].Change("9"); // hour, start side
        cut.Find(".wss-picker-ok").Click(); // confirms start; End unset -- advances

        SessionDay(cut, 20).Click(); // same panel (still January) -- end side
        TimeSelects(cut)[0].Change("14"); // hour, end side
        cut.Find(".wss-picker-ok").Click(); // both resolved -- commits + closes

        Assert.Equal(new DateTime(2025, 1, 10, 9, 0, 0), start);
        Assert.Equal(new DateTime(2025, 1, 20, 14, 0, 0), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Datetime_mode_a_backwards_pair_swaps_on_the_final_ok()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        SessionDay(cut, 20).Click(); // start side picks the LATER day first...
        TimeSelects(cut)[0].Change("14");
        cut.Find(".wss-picker-ok").Click();

        SessionDay(cut, 10).Click(); // ...end side picks the EARLIER day
        TimeSelects(cut)[0].Change("9");
        cut.Find(".wss-picker-ok").Click();

        Assert.Equal(new DateTime(2025, 1, 10, 9, 0, 0), start); // swapped to the earlier value
        Assert.Equal(new DateTime(2025, 1, 20, 14, 0, 0), end);
    }

    [Fact]
    public void Datetime_mode_escape_mid_session_discards_pending_state_and_keeps_committed_values()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.End, Feb3));

        Open(cut);
        SessionDay(cut, 20).Click();
        TimeSelects(cut)[0].Change("14");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(Jan15, cut.Instance.Start);
        Assert.Equal(Feb3, cut.Instance.End);

        // Reopening starts a brand-new session -- no ghost of the discarded pick survives.
        Open(cut);
        Assert.Equal("01/15/2025 00:00:00", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Datetime_mode_time_selects_compose_hour_minute_and_second_into_the_pending_value()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // hour
        TimeSelects(cut)[1].Change("45"); // minute
        TimeSelects(cut)[2].Change("30"); // second

        Assert.Null(start); // nothing committed yet
        Assert.Equal("01/15/2025 13:45:30", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Datetime_mode_showseconds_false_hides_the_seconds_select()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ShowSeconds, false));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(2, selects.Count);
        Assert.Equal("Hour", selects[0].GetAttribute("aria-label"));
        Assert.Equal("Minute", selects[1].GetAttribute("aria-label"));
    }

    [Fact]
    public void Datetime_mode_showseconds_false_zeroes_a_stale_second_on_time_select_change()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ShowSeconds, false)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 10, 30, 45))); // a stale 45 seconds

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // only hour/minute selects exist -- no seconds to change

        Assert.Equal("01/15/2025 13:30:00", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Datetime_mode_use12hours_renders_a_period_select_and_round_trips_pm()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Use12Hours, true)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 9, 0, 0))); // 9 AM

        Open(cut);
        var selects = TimeSelects(cut);
        Assert.Equal(4, selects.Count); // hour, minute, second, period
        Assert.Equal("AM", selects[3].QuerySelector("option[selected]")!.GetAttribute("value"));

        selects[3].Change("PM");

        Assert.Equal("01/15/2025 21:00:00", cut.Find(".wss-picker-input-start").GetAttribute("value")); // 9 -> 21
    }

    [Fact]
    public void Datetime_mode_minutestep_filters_options_and_keeps_an_off_lattice_value_visible()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.MinuteStep, 15)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 10, 37, 0))); // off-lattice bound minute

        Open(cut);

        var options = TimeSelects(cut)[1].QuerySelectorAll("option");
        Assert.Equal(["0", "15", "30", "37", "45"], options.Select(o => o.GetAttribute("value")));
        Assert.Equal("37", options.Single(o => o.HasAttribute("selected")).GetAttribute("value"));
    }

    [Fact]
    public void Datetime_mode_steps_below_one_clamp_to_one_instead_of_throwing()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.HourStep, 0)
            .Add(c => c.MinuteStep, -5)
            .Add(c => c.SecondStep, -1)
            .Add(c => c.Start, Jan15));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(24, selects[0].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[1].QuerySelectorAll("option").Length);
        Assert.Equal(60, selects[2].QuerySelectorAll("option").Length);
    }

    [Fact]
    public void Startdisabledtime_and_enddisabledtime_each_drive_the_time_row_for_their_own_side()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15) // End left unset so the first OK switches sides, not commits
            .Add(c => c.StartDisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [13])))
            .Add(c => c.EndDisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [8]))));

        Open(cut); // active = start

        var startHourOptions = TimeSelects(cut)[0].QuerySelectorAll("option");
        Assert.True(startHourOptions.Single(o => o.GetAttribute("value") == "13").HasAttribute("disabled"));
        Assert.False(startHourOptions.Single(o => o.GetAttribute("value") == "8").HasAttribute("disabled"));

        cut.Find(".wss-picker-ok").Click(); // confirms start -- switches to the end side

        var endHourOptions = TimeSelects(cut)[0].QuerySelectorAll("option");
        Assert.True(endHourOptions.Single(o => o.GetAttribute("value") == "8").HasAttribute("disabled"));
        Assert.False(endHourOptions.Single(o => o.GetAttribute("value") == "13").HasAttribute("disabled"));
    }

    [Fact]
    public void Startdisabledtime_rejects_a_select_change_that_lands_on_a_disabled_hour()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 10, 0, 0))
            .Add(c => c.StartDisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [13]))));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // rejected -- pending stays at the committed hour

        Assert.Equal("01/15/2025 10:00:00", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Hidedisabledtimeoptions_omits_other_disabled_hours_but_keeps_the_current_value_visible()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 13, 0, 0)) // the bound hour IS on the disabled list
            .Add(c => c.StartDisabledTime, (Func<DateTime?, DisabledTimeParts?>)(_ => new DisabledTimeParts(Hours: [13, 14])))
            .Add(c => c.HideDisabledTimeOptions, true));

        Open(cut);

        var options = TimeSelects(cut)[0].QuerySelectorAll("option");
        Assert.DoesNotContain(options, o => o.GetAttribute("value") == "14"); // fully hidden -- not the current value
        var thirteen = options.Single(o => o.GetAttribute("value") == "13"); // never-jump rule -- stays visible
        Assert.True(thirteen.HasAttribute("disabled"));
        Assert.NotNull(thirteen.GetAttribute("selected"));
        Assert.Equal(23, options.Length); // 24 hours minus the one fully-hidden one
    }

    [Fact]
    public void Typed_datetime_text_commits_the_endpoint_immediately_bypassing_the_session()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        var input = cut.Find(".wss-picker-input-start");
        input.Input("02/14/2026 13:45:30"); // DateTime mode's default format
        input.Change("02/14/2026 13:45:30");

        Assert.Equal(new DateTime(2026, 2, 14, 13, 45, 30), start);
    }

    [Fact]
    public void Datetime_mode_min_and_max_still_disable_calendar_days()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2025, 1, 10))
            .Add(c => c.Max, new DateTime(2025, 1, 20)));

        Open(cut);

        Assert.True(SessionDay(cut, 9).HasAttribute("disabled"));
        Assert.False(SessionDay(cut, 10).HasAttribute("disabled"));
        Assert.False(SessionDay(cut, 20).HasAttribute("disabled"));
        Assert.True(SessionDay(cut, 21).HasAttribute("disabled"));
    }

    [Fact]
    public void Datetime_mode_tints_the_span_between_the_other_endpoint_and_the_pending_day()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)); // End left unset

        Open(cut);
        SessionDay(cut, 10).Click();
        cut.Find(".wss-picker-ok").Click(); // confirm start (Jan 10), advance to the end side

        SessionDay(cut, 20).Click(); // pending end day -- tints the span between Jan 10 and Jan 20

        Assert.NotEmpty(cut.FindAll(".wss-picker-cell-preview"));
    }

    [Fact]
    public void Datetime_mode_ok_is_disabled_until_a_day_is_picked()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday));

        Open(cut);
        Assert.True(cut.Find(".wss-picker-ok").HasAttribute("disabled"));

        SessionDay(cut, 10).Click();
        Assert.False(cut.Find(".wss-picker-ok").HasAttribute("disabled"));
    }

    // ===================================================================================
    // Mode="Time" -- pick session with no calendar
    // ===================================================================================

    [Fact]
    public void Time_mode_renders_the_time_row_only_with_no_calendar()
    {
        var cut = RenderComponent<DateRangePicker>(p => p.Add(c => c.Mode, DatePickerMode.Time));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-day"));
        Assert.Empty(cut.FindAll(".wss-picker-month-header"));
        Assert.Equal(3, TimeSelects(cut).Count);
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok"));
    }

    [Fact]
    public void Time_mode_ok_is_disabled_until_something_is_resolved()
    {
        var cut = RenderComponent<DateRangePicker>(p => p.Add(c => c.Mode, DatePickerMode.Time));

        Open(cut);

        Assert.True(cut.Find(".wss-picker-ok").HasAttribute("disabled"));
    }

    [Fact]
    public void Time_mode_full_pick_session_preserves_each_endpoints_own_date_and_defaults_to_today()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 8, 0, 0)) // an existing date to preserve
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut); // active = start
        TimeSelects(cut)[0].Change("13");
        cut.Find(".wss-picker-ok").Click(); // confirms start; End unset -- switches to the end side

        Assert.Null(start); // still nothing committed

        TimeSelects(cut)[0].Change("9"); // end side -- no existing date, defaults to today
        cut.Find(".wss-picker-ok").Click(); // both resolved -- commits + closes

        Assert.Equal(new DateTime(2025, 1, 15, 13, 0, 0), start); // its own existing date preserved
        Assert.Equal(DateTime.Today.Year, end!.Value.Year);
        Assert.Equal(DateTime.Today.Month, end.Value.Month);
        Assert.Equal(DateTime.Today.Day, end.Value.Day);
        Assert.Equal(9, end.Value.Hour);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Time_mode_typed_commit_preserves_the_endpoints_own_date_and_ignores_min_and_max()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.Start, new DateTime(2025, 1, 15, 8, 0, 0))
            .Add(c => c.Min, DateTime.Today.AddDays(1)) // would reject Jan 15 if Min/Max applied
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        var input = cut.Find(".wss-picker-input-start");
        input.Input("13:45:30");
        input.Change("13:45:30");

        Assert.Equal(new DateTime(2025, 1, 15, 13, 45, 30), start); // date preserved, Min ignored
    }

    [Fact]
    public void Time_mode_typed_commit_defaults_to_today_when_the_endpoint_has_no_existing_value()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        var input = cut.Find(".wss-picker-input-start");
        input.Input("13:45:30");
        input.Change("13:45:30");

        Assert.NotNull(start);
        Assert.Equal(DateTime.Today.Year, start!.Value.Year);
        Assert.Equal(DateTime.Today.Month, start.Value.Month);
        Assert.Equal(DateTime.Today.Day, start.Value.Day);
        Assert.Equal(13, start.Value.Hour);
    }

    // ===================================================================================
    // Mode="Month"
    // ===================================================================================

    [Fact]
    public void Month_mode_dual_panels_show_consecutive_years_with_twelve_buttons_each()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)); // 2025-01-15 -> left panel 2025, right panel 2026

        Open(cut);

        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal(2, panels.Count);
        Assert.Empty(cut.FindAll(".wss-picker-week-header")); // no day-grid chrome in this mode

        var leftButtons = panels[0].QuerySelectorAll(".wss-picker-month-btn");
        var rightButtons = panels[1].QuerySelectorAll(".wss-picker-month-btn");
        Assert.Equal(12, leftButtons.Length);
        Assert.Equal(12, rightButtons.Length);
        Assert.Equal("Jan", leftButtons[0].TextContent);
        Assert.Equal("Dec", leftButtons[11].TextContent);

        Assert.Equal("2025", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2026", panels[1].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));

        var jan = MonthButton(cut, 0, 1);
        Assert.Equal("2025-01-01", jan.GetAttribute("data-date"));
        Assert.Equal("January 2025", jan.GetAttribute("aria-label"));
        Assert.Equal("true", jan.GetAttribute("aria-pressed")); // Jan15's month is the start endpoint
    }

    [Fact]
    public void Two_month_clicks_commit_the_range_normalized_and_close()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        MonthButton(cut, 0, 3).Click(); // March 2025 -- pending start
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
        MonthButton(cut, 1, 6).Click(); // June 2026 -- commits and closes

        Assert.Equal(new DateTime(2025, 3, 1), start);
        Assert.Equal(new DateTime(2026, 6, 1), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void A_backwards_month_pick_swaps_the_endpoints()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        MonthButton(cut, 1, 6).Click(); // June 2026 first...
        MonthButton(cut, 0, 3).Click(); // ...then March 2025 -- swapped on commit

        Assert.Equal(new DateTime(2025, 3, 1), start);
        Assert.Equal(new DateTime(2026, 6, 1), end);
    }

    [Fact]
    public void Month_mode_pending_start_and_hover_preview_get_the_range_tint_classes()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15));

        Open(cut);
        MonthButton(cut, 0, 3).Click(); // March 2025 -- pending start
        MonthButton(cut, 0, 6).PointerEnter(); // hover June 2025 (still the left panel)

        Assert.DoesNotContain("wss-picker-month-btn-preview", MonthButton(cut, 0, 3).ClassList); // pending start itself
        Assert.Contains("wss-picker-month-btn-preview", MonthButton(cut, 0, 4).ClassList); // between
        Assert.Contains("wss-picker-month-btn-preview", MonthButton(cut, 0, 6).ClassList); // hovered end

        cut.Find(".wss-picker-month-grid").PointerLeave();
        Assert.DoesNotContain("wss-picker-month-btn-preview", MonthButton(cut, 0, 4).ClassList);
    }

    [Fact]
    public void Month_mode_committed_range_tints_the_months_between_the_endpoints()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, new DateTime(2025, 3, 1))
            .Add(c => c.End, new DateTime(2026, 6, 1)));

        Open(cut);

        Assert.Contains("wss-picker-month-btn-in-range", MonthButton(cut, 0, 4).ClassList); // Apr 2025
        Assert.Contains("wss-picker-month-btn-in-range", MonthButton(cut, 1, 5).ClassList); // May 2026
        Assert.DoesNotContain("wss-picker-month-btn-in-range", MonthButton(cut, 0, 3).ClassList); // endpoint itself
        Assert.Equal("true", MonthButton(cut, 0, 3).GetAttribute("aria-pressed"));
        Assert.Equal("true", MonthButton(cut, 1, 6).GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Month_mode_min_and_max_disable_months_outside_range_at_month_granularity()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)
            .Add(c => c.Min, new DateTime(2025, 3, 1))
            .Add(c => c.Max, new DateTime(2026, 10, 31)));

        Open(cut);

        Assert.True(MonthButton(cut, 0, 2).HasAttribute("disabled"));   // Feb 2025 -- before Min's month
        Assert.False(MonthButton(cut, 0, 3).HasAttribute("disabled"));  // Min's own month
        Assert.False(MonthButton(cut, 1, 10).HasAttribute("disabled")); // Max's own month
        Assert.True(MonthButton(cut, 1, 11).HasAttribute("disabled"));  // after Max's month
    }

    [Fact]
    public void Month_mode_year_select_keeps_the_two_panels_consecutive()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15));
        Open(cut);

        // Jump the RIGHT panel's year select to 2030 -> left must follow to 2029.
        cut.FindAll(".wss-picker-month")[1].QuerySelector("select")!.Change("2030");

        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2029", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2030", panels[1].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Prev_and_next_year_buttons_step_the_view_and_disable_at_bounds_in_month_mode()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15) // left = 2025
            .Add(c => c.Min, new DateTime(2024, 1, 1))
            .Add(c => c.Max, new DateTime(2027, 12, 31)));
        Open(cut);

        var buttons = cut.FindAll("button.wss-picker-nav");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Previous year", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next year", buttons[1].GetAttribute("aria-label"));
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));

        buttons[0].Click(); // prev year: left 2025 -> 2024 (Min's own year)
        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2024", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.True(cut.FindAll("button.wss-picker-nav")[0].HasAttribute("disabled"));
    }

    [Fact]
    public void Typed_start_in_month_mode_commits_first_of_month_and_reanchors_the_left_panel()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-start");
        input.Input("03/2030"); // MM/yyyy -- Month mode's default format
        input.Change("03/2030");

        Assert.Equal(new DateTime(2030, 3, 1), start);
        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2030", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Effective_format_and_placeholder_default_for_month_mode()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15));

        Assert.Equal("01/2025", cut.Find(".wss-picker-input-start").GetAttribute("value"));
        Assert.Equal("MM/YYYY", cut.Find(".wss-picker-input-start").GetAttribute("placeholder"));
        Assert.Equal("MM/YYYY", cut.Find(".wss-picker-input-end").GetAttribute("placeholder"));
    }

    [Fact]
    public void Preset_commit_normalizes_to_month_granularity()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2025, 3, 15), new DateTime(2025, 6, 20)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 3, 1), start);
        Assert.Equal(new DateTime(2025, 6, 1), end);
    }

    [Fact]
    public void Month_mode_arrow_keys_move_the_roving_tabindex_and_cross_the_year_boundary()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)); // 2025-01-15 -> left = 2025 (Jan focused by default)
        Open(cut);

        Assert.Equal("0", MonthButton(cut, 0, 1).GetAttribute("tabindex"));

        // ArrowUp from January steps -3 months, crossing back into the PREVIOUS year -- the view
        // must slide by exactly one year so the crossed-into month becomes visible.
        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });

        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2024", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2025", panels[1].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", MonthButton(cut, 0, 10).GetAttribute("tabindex")); // October 2024, left panel
    }

    [Fact]
    public void Month_mode_marks_the_current_month_as_aria_current()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, new DateTime(DateTime.Today.Year, 1, 1)));
        Open(cut);

        Assert.Equal("date", MonthButton(cut, 0, DateTime.Today.Month).GetAttribute("aria-current"));
    }

    // ===================================================================================
    // Mode="Quarter"
    // ===================================================================================

    [Fact]
    public void Quarter_mode_dual_panels_show_consecutive_years_with_four_buttons_each()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(2025, 8, 20))); // Q3 2025 -> right panel 2026

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-month-grid")); // the quarter grid, not the month grid

        var panels = cut.FindAll(".wss-picker-month");
        var leftButtons = panels[0].QuerySelectorAll(".wss-picker-quarter-grid .wss-picker-month-btn");
        var rightButtons = panels[1].QuerySelectorAll(".wss-picker-quarter-grid .wss-picker-month-btn");
        Assert.Equal(4, leftButtons.Length);
        Assert.Equal(4, rightButtons.Length);
        Assert.Equal("Q1", leftButtons[0].TextContent);
        Assert.Equal("Q4", leftButtons[3].TextContent);

        Assert.Equal("2025", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2026", panels[1].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));

        var q3 = QuarterButton(cut, 0, 3);
        Assert.Equal("2025-07-01", q3.GetAttribute("data-date"));
        Assert.Equal("Q3 2025", q3.GetAttribute("aria-label"));
        Assert.Equal("true", q3.GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Two_quarter_clicks_commit_the_quarter_start_range_and_close()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(2025, 8, 20)) // Q3 2025
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        QuarterButton(cut, 0, 1).Click(); // Q1 2025 -- pending start
        QuarterButton(cut, 1, 2).Click(); // Q2 2026 -- commits and closes

        Assert.Equal(new DateTime(2025, 1, 1), start);
        Assert.Equal(new DateTime(2026, 4, 1), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Quarter_mode_min_and_max_disable_quarters_outside_range_at_quarter_granularity()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(2025, 8, 20)) // Q3 2025 -- left panel 2025
            .Add(c => c.Min, new DateTime(2025, 4, 15))   // Q2 2025
            .Add(c => c.Max, new DateTime(2026, 2, 1)));  // Q1 2026

        Open(cut);

        Assert.True(QuarterButton(cut, 0, 1).HasAttribute("disabled"));  // Q1 2025 -- before Min's quarter
        Assert.False(QuarterButton(cut, 0, 2).HasAttribute("disabled")); // Q2 2025 -- Min's own quarter
        Assert.False(QuarterButton(cut, 1, 1).HasAttribute("disabled")); // Q1 2026 -- Max's own quarter
        Assert.True(QuarterButton(cut, 1, 2).HasAttribute("disabled"));  // Q2 2026 -- after Max's quarter
    }

    [Fact]
    public void Quarter_text_with_a_dash_parses_and_commits_the_quarter_start()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-start");
        input.Input("2026-Q3");
        input.Change("2026-Q3");

        Assert.Equal(new DateTime(2026, 7, 1), start);
    }

    [Fact]
    public void Quarter_text_without_a_dash_and_lowercase_q_also_parses()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-start");
        input.Input("2026q3");
        input.Change("2026q3");

        Assert.Equal(new DateTime(2026, 7, 1), start);
    }

    [Fact]
    public void Effective_format_and_placeholder_default_for_quarter_mode()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(2025, 8, 20)));

        Assert.Equal("2025-Q3", cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void Preset_commit_normalizes_to_quarter_granularity()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2025, 2, 15), new DateTime(2025, 8, 20)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 1, 1), start); // Q1 start
        Assert.Equal(new DateTime(2025, 7, 1), end);   // Q3 start
    }

    [Fact]
    public void Quarter_mode_arrow_keys_cross_the_year_boundary()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(2025, 1, 20))); // Q1 2025 -- left panel 2025
        Open(cut);

        Assert.Equal("0", QuarterButton(cut, 0, 1).GetAttribute("tabindex"));

        cut.Find(".wss-picker-quarter-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2024", panels[0].QuerySelector("select")!.QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("0", QuarterButton(cut, 0, 4).GetAttribute("tabindex")); // Q4 2024, left panel
    }

    [Fact]
    public void Quarter_mode_marks_the_current_quarter_as_aria_current()
    {
        var currentQuarter = (DateTime.Today.Month - 1) / 3 + 1;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Quarter)
            .Add(c => c.Start, new DateTime(DateTime.Today.Year, 1, 1)));
        Open(cut);

        Assert.Equal("date", QuarterButton(cut, 0, currentQuarter).GetAttribute("aria-current"));
    }

    // ===================================================================================
    // Mode="Year"
    // ===================================================================================

    [Fact]
    public void Year_mode_dual_panels_show_consecutive_decades_with_two_outside_years_each()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, Jan15)); // 2025 -> left decade 2020-2029, right 2030-2039

        Open(cut);

        var panels = cut.FindAll(".wss-picker-month");
        var leftButtons = panels[0].QuerySelectorAll(".wss-picker-month-btn");
        var rightButtons = panels[1].QuerySelectorAll(".wss-picker-month-btn");
        Assert.Equal(12, leftButtons.Length);
        Assert.Equal(12, rightButtons.Length);

        Assert.Equal("2019", leftButtons[0].TextContent);
        Assert.Contains("wss-picker-month-btn-outside", leftButtons[0].ClassList);
        Assert.Equal("2020", leftButtons[1].TextContent);
        Assert.DoesNotContain("wss-picker-month-btn-outside", leftButtons[1].ClassList);
        Assert.Equal("2029", leftButtons[10].TextContent);
        Assert.Equal("2030", leftButtons[11].TextContent); // dimmed in the left panel...
        Assert.Contains("wss-picker-month-btn-outside", leftButtons[11].ClassList);
        Assert.Equal("2029", rightButtons[0].TextContent); // ...the right panel's own dimmed leading cell
        Assert.Contains("wss-picker-month-btn-outside", rightButtons[0].ClassList);
        Assert.Equal("2030", rightButtons[1].TextContent); // real in the right panel
        Assert.DoesNotContain("wss-picker-month-btn-outside", rightButtons[1].ClassList);

        Assert.Equal("2020-2029", panels[0].QuerySelector(".wss-picker-decade-label")!.TextContent);
        Assert.Equal("2030-2039", panels[1].QuerySelector(".wss-picker-decade-label")!.TextContent);
        Assert.Empty(panels[0].QuerySelectorAll("select")); // no year select in this mode

        var y2025 = YearButton(cut, 0, 6);
        Assert.Equal("2025", y2025.TextContent);
        Assert.Equal("true", y2025.GetAttribute("aria-pressed"));
        Assert.Equal("2025-01-01", y2025.GetAttribute("data-date"));
    }

    [Fact]
    public void Two_year_clicks_commit_the_range_normalized_and_close()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, Jan15) // left decade 2020-2029
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        YearButton(cut, 0, 4).Click(); // 2023 -- pending start
        YearButton(cut, 1, 3).Click(); // 2032 -- commits and closes

        Assert.Equal(new DateTime(2023, 1, 1), start);
        Assert.Equal(new DateTime(2032, 1, 1), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Clicking_an_outside_year_cell_commits_it_too()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        YearButton(cut, 0, 11).Click(); // the dimmed trailing 2030 cell -- pending start
        YearButton(cut, 0, 6).Click(); // 2025 -- commits (swapped: 2025 < 2030)

        Assert.Equal(new DateTime(2025, 1, 1), start);
        Assert.Equal(new DateTime(2030, 1, 1), end);
    }

    [Fact]
    public void Year_mode_min_and_max_disable_years_outside_range_at_year_granularity()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, Jan15) // left decade 2020-2029
            .Add(c => c.Min, new DateTime(2022, 6, 1))
            .Add(c => c.Max, new DateTime(2033, 3, 1)));

        Open(cut);

        Assert.True(YearButton(cut, 0, 2).HasAttribute("disabled"));  // 2021 -- before Min's year
        Assert.False(YearButton(cut, 0, 3).HasAttribute("disabled")); // 2022 -- Min's own year
        Assert.False(YearButton(cut, 1, 4).HasAttribute("disabled")); // 2033 -- Max's own year
        Assert.True(YearButton(cut, 1, 5).HasAttribute("disabled"));  // 2034 -- after Max's year
    }

    [Fact]
    public void Prev_and_next_decade_buttons_step_the_view_and_disable_at_bounds()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, Jan15) // left decade 2020-2029
            .Add(c => c.Min, new DateTime(2010, 1, 1))
            .Add(c => c.Max, new DateTime(2049, 12, 31)));
        Open(cut);

        var buttons = cut.FindAll("button.wss-picker-nav");
        Assert.Equal(2, buttons.Count);
        Assert.Equal("Previous decade", buttons[0].GetAttribute("aria-label"));
        Assert.Equal("Next decade", buttons[1].GetAttribute("aria-label"));
        Assert.False(buttons[0].HasAttribute("disabled"));
        Assert.False(buttons[1].HasAttribute("disabled"));

        buttons[0].Click(); // prev decade: left 2020s -> 2010s (Min's own decade)
        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2010-2019", panels[0].QuerySelector(".wss-picker-decade-label")!.TextContent);
        Assert.True(cut.FindAll("button.wss-picker-nav")[0].HasAttribute("disabled"));
    }

    [Fact]
    public void Typed_start_in_year_mode_commits_january_first_and_reanchors_the_left_panel()
    {
        DateTime? start = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);
        var input = cut.Find(".wss-picker-input-start");
        input.Input("2050"); // yyyy -- Year mode's default format
        input.Change("2050");

        Assert.Equal(new DateTime(2050, 1, 1), start);
        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2050-2059", panels[0].QuerySelector(".wss-picker-decade-label")!.TextContent);
    }

    [Fact]
    public void Preset_commit_normalizes_to_year_granularity()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2025, 6, 15), new DateTime(2027, 9, 1)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 1, 1), start);
        Assert.Equal(new DateTime(2027, 1, 1), end);
    }

    [Fact]
    public void Year_mode_arrow_keys_cross_the_decade_boundary()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, new DateTime(2020, 6, 1))); // left decade 2020-2029, 2020 focused
        Open(cut);

        Assert.Equal("0", YearButton(cut, 0, 1).GetAttribute("tabindex")); // 2020 is index 1 (decadeStart-1..+10)

        // First ArrowLeft only reaches 2019 -- already visible as the left panel's own dimmed
        // leading cell (decadeStart-1), so the view does not slide yet. The second ArrowLeft lands
        // on 2018, genuinely outside both panels, which slides the view back exactly one decade.
        // Re-Find each time: bUnit's DOM node can go stale across a re-render.
        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });
        cut.Find(".wss-picker-month-grid").KeyDown(new KeyboardEventArgs { Key = "ArrowLeft" });

        var panels = cut.FindAll(".wss-picker-month");
        Assert.Equal("2010-2019", panels[0].QuerySelector(".wss-picker-decade-label")!.TextContent);
        Assert.Equal("0", YearButton(cut, 0, 9).GetAttribute("tabindex")); // 2018, left panel (index 9 = decadeStart+8)
    }

    [Fact]
    public void Year_mode_marks_the_current_year_as_aria_current()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Year)
            .Add(c => c.Start, new DateTime(DateTime.Today.Year, 1, 1)));
        Open(cut);

        var currentYearText = DateTime.Today.Year.ToString(CultureInfo.InvariantCulture);
        var panels = cut.FindAll(".wss-picker-month");
        var currentButton = panels[0].QuerySelectorAll(".wss-picker-month-btn")
            .First(b => b.TextContent == currentYearText);
        Assert.Equal("date", currentButton.GetAttribute("aria-current"));
    }

    // ===================================================================================
    // Mode="Week" / ShowWeekNumbers
    // ===================================================================================
    // Jan15/Feb3 (this class's own fixtures) anchor Start/End the same way every other Date-family
    // test above does: Start=Jan15 opens on the Jan/Feb 2025 panel pair. With Sunday as the first
    // day of the week, Jan 2025's 6 rows are Dec29-Jan4, Jan5-11, Jan12-18, Jan19-25, Jan26-Feb1,
    // Feb2-8; Feb 2025's are Jan26-Feb1, Feb2-8, Feb9-15, Feb16-22, Feb23-Mar1, Mar2-8 -- Jan15
    // falls in Jan's row 2 (Jan12-18), Feb3 in Feb's row 1 (Feb2-8).

    [Fact]
    public void Week_mode_dual_panels_render_week_number_rows()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-grid")); // the rows layout, not the flat grid, in EITHER panel
        Assert.Equal(12, cut.FindAll(".wss-picker-week-row").Count); // 6 rows x 2 panels
        Assert.Equal(12, cut.FindAll(".wss-picker-week-no").Count);
        Assert.Equal(84, cut.FindAll(".wss-picker-cell").Count); // 42 x 2 panels
        Assert.Equal(84, cut.FindAll(".wss-picker-day").Count);
        Assert.Equal(2, cut.FindAll(".wss-picker-week-no-header").Count); // one per panel's weekday header
    }

    [Fact]
    public void Two_week_clicks_commit_the_week_start_range_and_close()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        Day(cut, 0, 10).Click(); // Jan 10 -- week Jan5-11 -- pending start
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));
        Day(cut, 1, 20).Click(); // Feb 20 -- week Feb16-22 -- commits and closes

        Assert.Equal(new DateTime(2025, 1, 5), start);
        Assert.Equal(new DateTime(2025, 2, 16), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void A_backwards_week_pick_swaps_the_endpoints()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        Day(cut, 1, 20).Click(); // Feb 20 (week Feb16-22) first...
        Day(cut, 0, 10).Click(); // ...then Jan 10 (week Jan5-11) -- swapped on commit

        Assert.Equal(new DateTime(2025, 1, 5), start);
        Assert.Equal(new DateTime(2025, 2, 16), end);
    }

    [Fact]
    public void Week_mode_row_level_classes_mark_endpoints_and_in_range_across_both_panels()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, new DateTime(2025, 1, 5))   // week start itself -- Jan's row 1
            .Add(c => c.End, new DateTime(2025, 2, 16)));  // week start itself -- Feb's row 3

        Open(cut);

        var leftRows = cut.FindAll(".wss-picker-month")[0].QuerySelectorAll(".wss-picker-week-row");
        var rightRows = cut.FindAll(".wss-picker-month")[1].QuerySelectorAll(".wss-picker-week-row");

        Assert.Contains("wss-picker-week-row-start", leftRows[1].ClassList);   // Jan5-11
        Assert.DoesNotContain("wss-picker-week-row-in-range", leftRows[1].ClassList);
        Assert.Contains("wss-picker-week-row-in-range", leftRows[2].ClassList); // Jan12-18
        Assert.Contains("wss-picker-week-row-in-range", leftRows[4].ClassList); // Jan26-Feb1
        Assert.Contains("wss-picker-week-row-in-range", rightRows[0].ClassList); // Jan26-Feb1 (same week, right panel)
        Assert.Contains("wss-picker-week-row-in-range", rightRows[1].ClassList); // Feb2-8
        Assert.Contains("wss-picker-week-row-in-range", rightRows[2].ClassList); // Feb9-15
        Assert.Contains("wss-picker-week-row-end", rightRows[3].ClassList);    // Feb16-22
        // A row clearly outside the range gets no range/endpoint class at all.
        Assert.DoesNotContain("wss-picker-week-row-start", leftRows[0].ClassList);
        Assert.DoesNotContain("wss-picker-week-row-in-range", leftRows[0].ClassList);
        Assert.DoesNotContain("wss-picker-week-row-end", leftRows[0].ClassList);

        // Every day in an endpoint row is pressed, and per-day/per-cell classing is suppressed --
        // the ROW carries the range styling, not individual cells.
        Assert.Equal("true", Day(cut, 0, 5).GetAttribute("aria-pressed"));
        Assert.Equal("true", Day(cut, 0, 11).GetAttribute("aria-pressed"));
        Assert.DoesNotContain("wss-picker-day-selected", Day(cut, 0, 5).ClassList);
        Assert.Empty(cut.FindAll(".wss-picker-cell-in-range"));
        Assert.Empty(cut.FindAll(".wss-picker-cell-range-start"));
    }

    [Fact]
    public void Week_mode_keeps_day_buttons_enabled_in_a_partially_in_range_week()
    {
        // Min falls mid-week (Jan 7, within the Jan5-11 row): Jan 5-6 are disabled at day
        // granularity, but the Jan5-11 week itself still straddles Min, so every other day in it --
        // including the click target below -- stays enabled; only the week-granularity commit guard
        // (exercised by the typed-text test below) would ever reject the whole week.
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15) // anchors the view on the Jan/Feb 2025 panel pair
            .Add(c => c.Min, new DateTime(2025, 1, 7))
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);

        Assert.True(Day(cut, 0, 5).HasAttribute("disabled"));
        Assert.False(Day(cut, 0, 10).HasAttribute("disabled"));

        Day(cut, 0, 10).Click(); // pending start = Jan 5, even though Jan5-6 precede Min
        Day(cut, 1, 20).Click();

        Assert.Equal(new DateTime(2025, 1, 5), start);
        Assert.Equal(new DateTime(2025, 2, 16), end);
    }

    [Fact]
    public void Week_mode_min_and_max_disable_typed_commit_at_week_granularity()
    {
        // Min = Jan 5, 2025 (itself a Sunday, so also that week's own start); Max = Feb 22, 2025
        // (the Saturday ending the Feb16-22 week). Typed plain dates (not the "yyyy-Www" shorthand)
        // so the assertions don't depend on the week-number arithmetic's own boundary behavior --
        // only IsWeekDisabledForCommit's guard is under test here.
        DateTime? Commit(string text)
        {
            DateTime? value = null;
            var cut = RenderComponent<DateRangePicker>(p => p
                .Add(c => c.Mode, DatePickerMode.Week)
                .Add(c => c.Format, "MM/dd/yyyy")
                .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
                .Add(c => c.Min, new DateTime(2025, 1, 5))
                .Add(c => c.Max, new DateTime(2025, 2, 22))
                .Add(c => c.StartChanged, (DateTime? v) => value = v));
            var input = cut.Find(".wss-picker-input-start");
            input.Input(text);
            input.Change(text);
            return value;
        }

        Assert.Null(Commit("12/29/2024"));                            // week Dec29-Jan4 -- entirely before Min
        Assert.Equal(new DateTime(2025, 1, 5), Commit("01/08/2025"));  // Min's own week (Jan5-11)
        Assert.Equal(new DateTime(2025, 2, 16), Commit("02/18/2025")); // Max's own week (Feb16-22)
        Assert.Null(Commit("02/25/2025"));                             // week Feb23-Mar1 -- entirely after Max
    }

    [Fact]
    public void Week_shorthand_start_and_end_parse_and_commit_the_correct_endpoint()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        // 2023-01-01 is itself a Sunday, so the week-1 arithmetic needs no prior-year-shift
        // adjustment -- a clean case to assert the shorthand's parse against a known date (mirrors
        // DatePickerTests' identical fixture).
        var startInput = cut.Find(".wss-picker-input-start");
        startInput.Input("2023-W08");
        startInput.Change("2023-W08");
        var endInput = cut.Find(".wss-picker-input-end");
        endInput.Input("2023w9");
        endInput.Change("2023w9");

        Assert.Equal(new DateTime(2023, 2, 19), start); // the Sunday starting week 8 of 2023
        Assert.Equal(new DateTime(2023, 2, 26), end);   // the Sunday starting week 9 of 2023
    }

    [Fact]
    public void Effective_format_and_placeholder_default_for_week_mode()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)); // week start Jan 12, 2025

        var weekStart = new DateTime(2025, 1, 12);
        var rule = CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule;
        var weekNo = new GregorianCalendar().GetWeekOfYear(weekStart, rule, DayOfWeek.Sunday);
        var expected = $"2025-W{weekNo.ToString("00", CultureInfo.InvariantCulture)}";

        Assert.Equal(expected, cut.Find(".wss-picker-input-start").GetAttribute("value"));
    }

    [Fact]
    public void ShowWeekNumbers_in_date_mode_adds_the_column_to_both_panels_without_changing_commit_semantics()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.ShowWeekNumbers, true)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);

        Assert.Empty(cut.FindAll(".wss-picker-grid")); // the rows layout, not the flat grid
        Assert.Equal(12, cut.FindAll(".wss-picker-week-row").Count); // 6 rows x 2 panels
        Assert.Equal(2, cut.FindAll(".wss-picker-week-no-header").Count);
        // No week-selection row styling outside Mode.Week -- ShowWeekNumbers only adds the column.
        Assert.Empty(cut.FindAll(".wss-picker-week-row-start"));
        Assert.Empty(cut.FindAll(".wss-picker-week-row-in-range"));
        Assert.Contains("wss-picker-day-selected", Day(cut, 0, 15).ClassList); // single-day styling unaffected

        Day(cut, 0, 10).Click(); // a day click still commits that DAY, not its week start -- pending start
        Day(cut, 1, 20).Click();

        Assert.Equal(new DateTime(2025, 1, 10), start);
        Assert.Equal(new DateTime(2025, 2, 20), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }

    [Fact]
    public void Week_mode_arrow_keys_still_move_the_roving_tabindex()
    {
        // Start is already a week start (Jan 12, 2025, a Sunday) -- DisplayRange normalizes Start to
        // its own week start for the default-focus comparison, so an already-week-start value keeps
        // the assertion unambiguous (a mid-week Start would default-focus its WEEK START, not the
        // literal bound day -- see DefaultFocusDay's DisplayRange-based comparison).
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, new DateTime(2025, 1, 12)));

        Open(cut);
        Assert.Equal("0", Day(cut, 0, 12).GetAttribute("tabindex"));

        cut.Find(".wss-picker-grid-rows").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });
        Assert.Equal("0", Day(cut, 0, 13).GetAttribute("tabindex"));
    }

    // ===================================================================================
    // DisabledDate
    // ===================================================================================

    [Fact]
    public void DisabledDate_disables_matching_day_cells_and_the_typed_commit_guard_rejects_them()
    {
        // Jan 15 2025 is a Wednesday -- disable weekends and confirm both the cell attribute and the
        // typed-text commit guard agree, mirroring DatePickerTests' identical convention.
        DateTime? start = Jan15;
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday))
            .Add(c => c.StartChanged, (DateTime? v) => start = v));

        Open(cut);

        Assert.True(Day(cut, 0, 18).HasAttribute("disabled"));  // Saturday
        Assert.True(Day(cut, 0, 19).HasAttribute("disabled"));  // Sunday
        Assert.False(Day(cut, 0, 20).HasAttribute("disabled")); // Monday

        var input = cut.Find(".wss-picker-input-start");
        input.Input("01/18/2025"); // a disabled Saturday
        input.Change("01/18/2025");

        Assert.Equal(Jan15, start); // rejected -- unchanged
        Assert.Equal("01/15/2025", input.GetAttribute("value"));
    }

    [Fact]
    public void DisabledDate_at_month_granularity_disables_month_cells_using_the_month_start_argument()
    {
        var seenArgs = new List<DateTime>();
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15) // left panel 2025, right panel 2026
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d =>
            {
                seenArgs.Add(d);
                return d.Month == 7;
            })));

        Open(cut);

        Assert.True(MonthButton(cut, 0, 7).HasAttribute("disabled"));
        Assert.False(MonthButton(cut, 0, 6).HasAttribute("disabled"));
        Assert.Contains(new DateTime(2025, 7, 1), seenArgs); // the month START, not just any July date
    }

    [Fact]
    public void DisabledDate_rejects_a_preset_whose_normalized_endpoints_land_on_a_disabled_unit()
    {
        DateTime? start = Jan15, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Jan15)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d == new DateTime(2025, 7, 1)))
            .Add(c => c.Presets, new[]
            {
                // Resolves to two ordinary mid-month days -- NEITHER is literally July 1st -- but
                // Month mode normalizes both to their own month start before committing, and the
                // resolved start's normalized month (July 2025) is exactly what DisabledDate
                // rejects, proving the guard checks the NORMALIZED result, not the raw resolved day.
                new DateRangePreset("This range", new DateTime(2025, 7, 15), new DateTime(2025, 8, 10)),
            })
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(Jan15, start); // unchanged -- the preset no-oped instead of committing
        Assert.Null(end);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // stays open -- nothing committed
    }

    [Fact]
    public void Week_mode_click_is_rejected_when_disableddate_rejects_the_week_start_even_though_the_clicked_day_does_not()
    {
        // DisabledDate rejects ONLY the week start (Jan 5) -- the clicked day (Jan 10) evaluates the
        // same predicate against ITS OWN day-granularity argument and comes back false, so its
        // button stays enabled. Unlike Min/Max (where a disabled week always implies every day in it
        // is also disabled), an arbitrary predicate can disagree between the two granularities -- the
        // click path must still guard the week-start commit explicitly (see OnWeekDayClickAsync),
        // mirroring DatePickerTests' identical case.
        var disabledWeekStart = new DateTime(2025, 1, 5); // week Jan5-11
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Jan15)
            .Add(c => c.DisabledDate, (Func<DateTime, bool>)(d => d == disabledWeekStart))
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v));

        Open(cut);

        Assert.False(Day(cut, 0, 10).HasAttribute("disabled")); // the clicked day's own button stays enabled

        Day(cut, 0, 10).Click(); // Jan 10 -- week Jan5-11 -- the week-start commit itself is rejected
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // nothing pending -- stays open

        Day(cut, 1, 20).Click(); // Feb 20 -- week Feb16-22 -- this becomes the FIRST real pending start
        Assert.Null(start);
        Assert.Null(end);
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // still open -- only one endpoint chosen

        Day(cut, 0, 12).Click(); // Jan 12 -- week Jan12-18 -- commits the pending pick (swapped)

        Assert.Equal(new DateTime(2025, 1, 12), start);
        Assert.Equal(new DateTime(2025, 2, 16), end);
    }

    // ----- ExtraFooter / DefaultViewDate / preset time-of-day ------------------------------------

    [Fact]
    public void ExtraFooter_renders_below_the_dual_panels_in_date_mode()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.ExtraFooter, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"my-extra\">extra</span>"))));

        Open(cut);

        Assert.Equal("extra", cut.Find(".wss-picker-extra-footer .my-extra").TextContent);
        // Exactly one strip -- shared across both panels, not duplicated per panel.
        Assert.Single(cut.FindAll(".wss-picker-extra-footer"));
    }

    [Fact]
    public void ExtraFooter_renders_alongside_the_ok_footer_in_datetime_session_mode()
    {
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.ExtraFooter, (RenderFragment)(b => b.AddMarkupContent(0, "<span class=\"my-extra\">extra</span>"))));

        Open(cut);

        Assert.Equal("extra", cut.Find(".wss-picker-extra-footer .my-extra").TextContent);
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok")); // the existing OK footer still renders too
    }

    [Fact]
    public void DefaultViewDate_drives_both_panels_view_when_both_endpoints_are_null()
    {
        var cut = RenderPicker(p => p.Add(c => c.DefaultViewDate, new DateTime(2030, 8, 1)));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("8", selects[0].QuerySelector("option[selected]")!.GetAttribute("value")); // left month
        Assert.Equal("2030", selects[1].QuerySelector("option[selected]")!.GetAttribute("value")); // left year
        Assert.Equal("9", selects[2].QuerySelector("option[selected]")!.GetAttribute("value")); // right month
        Assert.Equal("2030", selects[3].QuerySelector("option[selected]")!.GetAttribute("value")); // right year
    }

    [Fact]
    public void DefaultViewDate_is_ignored_once_start_is_set()
    {
        var cut = RenderPicker(p => p
            .Add(c => c.Start, Jan15)
            .Add(c => c.DefaultViewDate, new DateTime(2030, 8, 1)));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("1", selects[0].QuerySelector("option[selected]")!.GetAttribute("value"));
        Assert.Equal("2025", selects[1].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void DefaultViewDate_is_ignored_once_only_end_is_set()
    {
        // Only End set anchors the RIGHT panel on it (existing "Start is null && End is not null ->
        // panel 1" rule) -- DefaultViewDate must still be ignored, not layered in.
        var cut = RenderPicker(p => p
            .Add(c => c.End, Feb3)
            .Add(c => c.DefaultViewDate, new DateTime(2030, 8, 1)));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-month-header select");
        Assert.Equal("2", selects[2].QuerySelector("option[selected]")!.GetAttribute("value")); // right = Feb
        Assert.Equal("2025", selects[3].QuerySelector("option[selected]")!.GetAttribute("value"));
    }

    [Fact]
    public void Preset_in_datetime_mode_preserves_times_and_commits_normalized_values()
    {
        // Regression guard: OnPresetClickAsync used to truncate both endpoints to .Date before
        // normalizing, which silently zeroed a DateTime/Time preset's resolved time-of-day.
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.DateTime)
            .Add(c => c.Format, "MM/dd/yyyy HH:mm:ss")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v)
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2025, 1, 10, 9, 30, 15), new DateTime(2025, 1, 20, 14, 45, 0)),
            }));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        Assert.Equal(new DateTime(2025, 1, 10, 9, 30, 15), start);
        Assert.Equal(new DateTime(2025, 1, 20, 14, 45, 0), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown")); // still closes, like every other mode's preset
    }

    [Fact]
    public void Preset_in_time_mode_preserves_times_and_commits_normalized_values()
    {
        DateTime? start = null, end = null;
        var cut = RenderComponent<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Time)
            .Add(c => c.StartChanged, (DateTime? v) => start = v)
            .Add(c => c.EndChanged, (DateTime? v) => end = v)
            .Add(c => c.Presets, new[]
            {
                new DateRangePreset("Fixed", new DateTime(2025, 1, 10, 9, 30, 15), new DateTime(2025, 1, 20, 14, 45, 0)),
            }));

        Open(cut);
        cut.Find(".wss-picker-preset").Click();

        // Time mode's NormalizeForMode routes through DateRangePicker's own DateTime-shaped fold
        // (see NormalizeForMode's doc comment), which preserves the preset's own resolved date --
        // unlike DatePicker's single-Value Time mode, which always re-stamps to the literal today.
        Assert.Equal(new DateTime(2025, 1, 10, 9, 30, 15), start);
        Assert.Equal(new DateTime(2025, 1, 20, 14, 45, 0), end);
        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
    }
}
