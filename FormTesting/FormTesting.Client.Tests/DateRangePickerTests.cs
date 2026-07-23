using System.Globalization;
using AngleSharp.Dom;
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

    [Fact]
    public void DateTime_time_and_week_modes_render_as_date_for_now()
    {
        // Phase 1a only implements Date/Month/Quarter/Year -- DateTime/Time/Week fold to Date until
        // a later phase (see Mode's own doc comment). Guards EffectiveMode's fallback switch.
        foreach (var mode in new[] { DatePickerMode.DateTime, DatePickerMode.Time, DatePickerMode.Week })
        {
            var cut = RenderComponent<DateRangePicker>(p => p.Add(c => c.Mode, mode).Add(c => c.Start, Jan15));
            Open(cut);
            Assert.NotEmpty(cut.FindAll(".wss-picker-grid")); // the day grid, not a month/quarter/year grid
            Assert.Equal(4, cut.FindAll(".wss-picker-month-header select").Count); // month+year selects, doubled
        }
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
}
