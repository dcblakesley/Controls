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
}
