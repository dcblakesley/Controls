namespace FormTesting.Client.Tests;

/// <summary>
/// The picker panels' internal composition pieces -- <see cref="PickerMonthHeader"/>,
/// <see cref="PickerWeekdayHeader"/>, <see cref="PickerTimeRowSlot"/> and
/// <see cref="PickerTimeRow"/> -- are NOT supported standalone controls, but Razor compiles every
/// component to a public class, so all four ship on the NuGet surface where a consumer can reach
/// them. None of them may THROW when instantiated with nothing configured: an unsupported component
/// has to degrade (render nothing, or clamp) rather than take a consumer's app down with an
/// IndexOutOfRangeException or an NRE from inside a library assembly.
/// </summary>
public class PickerInternalComponentsTests : BunitContext
{
    [Fact]
    public void PickerMonthHeader_with_no_parameters_degrades_instead_of_throwing()
    {
        var cut = Render<PickerMonthHeader>();

        // Month defaults to 0, which is not a month -- the header renders nothing rather than
        // indexing AbbreviatedMonthNames out of range.
        Assert.Empty(cut.FindAll(".wss-picker-month-header"));
    }

    [Fact]
    public void PickerMonthHeader_still_renders_a_real_month_normally()
    {
        // Regression guard for the degrade path: a header the pickers themselves configure is
        // completely unaffected.
        var cut = Render<PickerMonthHeader>(p => p
            .Add(c => c.Month, 2)
            .Add(c => c.Year, 2026)
            .Add(c => c.YearRange, (2025, 2027)));

        Assert.Single(cut.FindAll(".wss-picker-month-header"));
        Assert.Equal(2, cut.FindAll(".wss-picker-month-selects select").Count);
        Assert.Equal(12, cut.FindAll(".wss-picker-month-selects select")[0].QuerySelectorAll("option").Length);
    }

    [Fact]
    public void PickerWeekdayHeader_with_no_parameters_degrades_instead_of_throwing()
    {
        var cut = Render<PickerWeekdayHeader>();

        // Names defaults to empty, so the strip has no weekday cells -- but it must still render
        // (and not throw) rather than being a hard failure.
        Assert.Empty(cut.FindAll(".wss-picker-week-day"));
    }

    [Fact]
    public void PickerTimeRowSlot_without_a_picker_renders_nothing_instead_of_throwing()
    {
        var cut = Render<PickerTimeRowSlot>();

        // Picker is [EditorRequired] but nothing enforces that at runtime -- an unset one used to
        // NRE on the first `Picker.TimeRowHour` read.
        Assert.Empty(cut.FindAll(".wss-picker-time-row"));
    }

    [Fact]
    public void PickerTimeRow_with_no_parameters_renders_empty_selects_instead_of_throwing()
    {
        var cut = Render<PickerTimeRow>();

        // ShowSeconds/Use12Hours default false and every option list defaults to empty, so this is
        // an hour + minute pair of empty selects. Pinned so the row's own already-safe defaults
        // stay that way.
        Assert.Equal(2, cut.FindAll(".wss-picker-time-row select").Count);
    }
}
