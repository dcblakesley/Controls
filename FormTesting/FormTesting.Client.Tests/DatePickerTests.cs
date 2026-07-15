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
}
