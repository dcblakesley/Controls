using System.Text.RegularExpressions;

namespace Controls;

/// <summary>
/// Pure, instance-independent date/culture arithmetic shared by <see cref="DatePicker"/> and
/// <see cref="DateRangePicker"/> (and, via the promoted quarter/week display statics, by
/// <see cref="EditDatePicker{T}"/>'s read-only view). Every member takes its inputs explicitly --
/// no component state, no <c>Value</c>/<c>Min</c>/<c>Max</c> -- so it can be shared without either
/// picker needing an instance of the other.
/// </summary>
internal static class PickerMath
{
    public static DateTime FirstOfMonth(DateTime value) => new(value.Year, value.Month, 1);

    public static DateTime FirstOfYear(DateTime value) => new(value.Year, 1, 1);

    // The quarter (1-4) `value`'s month falls in.
    public static int QuarterOf(DateTime value) => (value.Month - 1) / 3 + 1;

    // The 1st of `quarter`'s (1-4) first month in `year`.
    public static DateTime QuarterStart(int year, int quarter) => new(year, (quarter - 1) * 3 + 1, 1);

    // The 1st of the quarter containing `value`.
    public static DateTime QuarterStart(DateTime value) => QuarterStart(value.Year, QuarterOf(value));

    // The first day of the calendar week containing `day`, per `firstDayOfWeek`. Shared by GridDays
    // (the 42-cell layout) and Home/End keyboard navigation so they can never disagree.
    public static DateTime WeekStart(DateTime day, DayOfWeek firstDayOfWeek)
    {
        var lead = ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        return day.AddDays(-lead);
    }

    // The ISO-ish week number of the calendar week starting on `weekStart`, per `culture`'s week
    // rule -- shared by DatePicker's own WeekNumberOf display and FormatWeekDisplay/
    // TryParseWeekShorthand below.
    public static int WeekNumberOf(DateTime weekStart, CultureInfo culture, DayOfWeek firstDayOfWeek) =>
        culture.Calendar.GetWeekOfYear(weekStart, culture.DateTimeFormat.CalendarWeekRule, firstDayOfWeek);

    // A fixed 6-row (42-cell) grid — covers every month/first-day combination, so the panel height
    // never jumps while navigating. Leading/trailing cells are the adjacent months' days.
    public static IEnumerable<DateTime> GridDays(DateTime month, DayOfWeek firstDayOfWeek)
    {
        var start = WeekStart(month, firstDayOfWeek);
        for (var i = 0; i < 42; i++)
        {
            yield return start.AddDays(i);
        }
    }

    // The displayed month, clamped so the 42-cell grid can never overflow DateTime's range.
    // `offsetMonths` carries a panel adjustment (DateRangePicker's right panel, or a keyboard/select
    // move anchoring the other panel) through the same clamp -- DatePicker calls this with the
    // default 0 (a single panel needs no offset).
    public static DateTime ClampView(DateTime firstOfMonth, int offsetMonths = 0)
    {
        var index = firstOfMonth.Year * 12 + (firstOfMonth.Month - 1) + offsetMonths;
        index = Math.Clamp(index, 1 * 12 + 1, 9998 * 12 + 10); // 0001-02 .. 9998-11
        return new DateTime(index / 12, index % 12 + 1, 1);
    }

    // Clamps a decade-start candidate so the decade's own leading/trailing dimmed cells
    // (decadeStart-1, decadeStart+10) always land inside DateTime's representable [1, 9999] year
    // range -- the year-grid's equivalent of ClampView's one-month buffer for the day grid. The
    // reachable extremes are the 10-19 decade (dimmed leading cell 9) and the 9980-9989 decade
    // (dimmed trailing cell 9990); years 1-9 and 9991-9999 are unreachable via the GRID (though
    // still typeable -- TryParseDate has no such margin), the same trade-off ClampView makes for
    // the very first/last representable month.
    public static int ClampDecadeStart(int year) => Math.Clamp(year, 11, 9989) / 10 * 10;

    // Range-mode equivalent of ClampDecadeStart for a picker showing TWO adjacent decades (D and
    // D+10, e.g. DateRangePicker's Mode="Year"): D's own dimmed leading cell (D-1) needs the same
    // >= 1 margin ClampDecadeStart already gives, but the SECOND panel's dimmed TRAILING cell
    // ((D+10)+10 = D+20) also needs to stay <= 9999, which requires D <= 9979 before flooring (vs.
    // ClampDecadeStart's own 9989) -- one extra decade of headroom for the second panel's own
    // margin. The reachable extremes are the 10-19/20-29 decade pair (dimmed leading cell 9) and
    // the 9970-9979/9980-9989 pair (dimmed trailing cell 9990 on the second panel).
    public static int ClampDecadeStartForRange(int year) => Math.Clamp(year, 11, 9979) / 10 * 10;

    // The years offered by a year select: Min/Max years when set, otherwise ±10 around the
    // displayed year — always including the displayed year itself so the select never shows a
    // value that isn't in its option list.
    public static (int From, int To) YearRange(int displayedYear, DateTime? min, DateTime? max)
    {
        var from = min?.Year ?? displayedYear - 10;
        var to = max?.Year ?? displayedYear + 10;
        if (displayedYear < from) from = displayedYear;
        if (displayedYear > to) to = displayedYear;
        // DateTime's year range is [1, 9999] — an unclamped ±10 offset (or a Min/Max year near
        // either edge) can offer a year outside it, and constructing `new DateTime(year, ...)` for
        // one throws (circuit-killing on Blazor Server). See OnYearSelectChanged for the matching
        // clamp on the value actually selected.
        return (Math.Clamp(from, 1, 9999), Math.Clamp(to, 1, 9999));
    }

    // Maps a keydown's Key to the day it should move focus to, or null when the key isn't a
    // navigation key. AddDays/AddMonths throws at the DateTime.MinValue/MaxValue edge — the caller
    // treats that as the key being a no-op there rather than letting the exception escape.
    public static DateTime? NextFocusDay(DateTime current, string key, DayOfWeek firstDayOfWeek)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddDays(-1),
                "ArrowRight" => current.AddDays(1),
                "ArrowUp" => current.AddDays(-7),
                "ArrowDown" => current.AddDays(7),
                "Home" => WeekStart(current, firstDayOfWeek),
                "End" => WeekStart(current, firstDayOfWeek).AddDays(6),
                "PageUp" => current.AddMonths(-1),
                "PageDown" => current.AddMonths(1),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // Maps a keydown's Key to the month it should move focus to, or null when the key isn't a
    // navigation key -- shared by DatePicker's Mode="Month" grid and DateRangePicker's Month range
    // mode. The 3-column grid makes Up/Down a +/-3 (one row) step; Home/End jump to the first/last
    // month of the focused row. AddMonths/AddYears throws at the DateTime.MinValue/MaxValue edge --
    // the caller treats that as the key being a no-op there.
    public static DateTime? NextFocusMonth(DateTime current, string key)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddMonths(-1),
                "ArrowRight" => current.AddMonths(1),
                "ArrowUp" => current.AddMonths(-3),
                "ArrowDown" => current.AddMonths(3),
                "Home" => MonthRowStart(current),
                "End" => MonthRowStart(current).AddMonths(2),
                "PageUp" => current.AddYears(-1),
                "PageDown" => current.AddYears(1),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // The 1st of the first month in the 3-month row containing `month` (rows are Jan-Mar, Apr-Jun,
    // Jul-Sep, Oct-Dec) — shared by Home/End so they can never disagree about row bounds.
    public static DateTime MonthRowStart(DateTime month) => new(month.Year, (month.Month - 1) / 3 * 3 + 1, 1);

    // Maps a keydown's Key to the quarter it should move focus to, or null when the key isn't a
    // navigation key (Up/Down included -- a no-op in a single-row quarter grid). Shared by
    // DatePicker's Mode="Quarter" grid and DateRangePicker's Quarter range mode. Left/Right step a
    // quarter (retargeting the view when they cross a year boundary is the caller's job -- see
    // DatePicker.OnQuarterGridKeyDown / DateRangePicker.OnQuarterGridKeyDown); Home/End jump to the
    // year's first/last quarter; PageUp/PageDown step a year, keeping the same quarter. AddMonths/
    // the DateTime constructor throw at the DateTime.MinValue/MaxValue edge -- the caller treats
    // that as the key being a no-op there, same as NextFocusMonth.
    public static DateTime? NextFocusQuarter(DateTime current, string key)
    {
        try
        {
            return key switch
            {
                "ArrowLeft" => current.AddMonths(-3),
                "ArrowRight" => current.AddMonths(3),
                "Home" => QuarterStart(current.Year, 1),
                "End" => QuarterStart(current.Year, 4),
                "PageUp" => QuarterStart(current.Year - 1, QuarterOf(current)),
                "PageDown" => QuarterStart(current.Year + 1, QuarterOf(current)),
                _ => (DateTime?)null,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // Maps a keydown's Key to the year it should move focus to, or null when the key isn't a
    // navigation key. Shared by DatePicker's Mode="Year" grid and DateRangePicker's Year range
    // mode -- `decadeStart` is the CALLER's currently-displayed decade (DatePicker has one;
    // DateRangePicker picks whichever of its two panels' decades the current focus belongs to),
    // used only for Home/End's row grouping. Plain int arithmetic (unlike NextFocusMonth/
    // NextFocusQuarter's DateTime.AddX, this can't throw) -- clamped to DateTime's representable
    // year range instead so a move at the very edge is a no-op there.
    public static DateTime? NextFocusYear(DateTime current, string key, int decadeStart)
    {
        var year = current.Year;
        int? next = key switch
        {
            "ArrowLeft" => year - 1,
            "ArrowRight" => year + 1,
            "ArrowUp" => year - 3,
            "ArrowDown" => year + 3,
            "Home" => YearRowStart(year, decadeStart),
            "End" => YearRowStart(year, decadeStart) + 2,
            "PageUp" => year - 10,
            "PageDown" => year + 10,
            _ => (int?)null,
        };
        return next is { } y && y is >= 1 and <= 9999 ? new DateTime(y, 1, 1) : null;
    }

    // The 1st year of the 3-year row (within the *displayed* 12-cell decade grid, decadeStart-1
    // through decadeStart+10) containing `year` -- shared by Home/End so they can never disagree
    // about row bounds. Depends on the currently displayed decade (`decadeStart`) rather than
    // `year`'s own natural decade: the grid's two dimmed adjacent-decade cells belong to
    // neighboring decades, so grouping purely by each year's own decade would split a row unevenly
    // right at the boundary.
    public static int YearRowStart(int year, int decadeStart)
    {
        var offset = year - (decadeStart - 1);
        return decadeStart - 1 + offset / 3 * 3;
    }

    // The weekday header row, ordered to match GridDays' first-day-of-week so the header and grid
    // can never disagree — both derive from WeekStart/firstDayOfWeek. AntD shows the CLDR "short"
    // two-letter form ("Su"), which .NET doesn't expose (ShortestDayNames is the one-letter
    // "narrow" form, ambiguous for Tue/Thu and Sat/Sun), so truncate AbbreviatedDayNames instead —
    // already <= 2 chars in single-glyph cultures (ja, zh). Decorative only: aria-hidden, day
    // buttons carry full "D"-format labels.
    public static IEnumerable<string> WeekdayHeaders(CultureInfo culture, DayOfWeek firstDayOfWeek)
    {
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        for (var i = 0; i < 7; i++)
        {
            var name = names[((int)firstDayOfWeek + i) % 7];
            yield return name.Length <= 2 ? name : name[..2];
        }
    }

    public static string MonthName(CultureInfo culture, int month) =>
        culture.DateTimeFormat.AbbreviatedMonthNames[month - 1];

    // Central per-mode normalization, shared by DatePicker's TryParseDate and SetValueAsync so every
    // commit path (click, typed text, select change) agrees on the same shape of value.
    public static DateTime NormalizeForMode(DatePickerMode mode, DayOfWeek firstDayOfWeek, bool showSeconds, DateTime value) => mode switch
    {
        DatePickerMode.Date => value.Date,
        DatePickerMode.Month => FirstOfMonth(value),
        // showSeconds false zeroes the second here too (not just in ApplyTimePartAsync's own compose
        // step) so a typed-text commit -- which never goes through ApplyTimePartAsync -- can't leave
        // a stale nonzero second in place.
        DatePickerMode.DateTime => new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, showSeconds ? value.Second : 0),
        // Anchored to today at commit time -- mirrors what EditDate produces for a DateTime bound to
        // a Time input, where BindConverter/DateTime.TryParse("HH:mm:ss") yields today's date.
        DatePickerMode.Time => DateTime.Today + new TimeSpan(value.Hour, value.Minute, showSeconds ? value.Second : 0),
        DatePickerMode.Year => new DateTime(value.Year, 1, 1),
        DatePickerMode.Quarter => QuarterStart(value),
        DatePickerMode.Week => WeekStart(value, firstDayOfWeek),
        _ => value.Date,
    };

    // Matches a typed quarter shorthand: "2026-Q3", "2026Q3", "2026 q3" -- 1-4 digit year, optional
    // dash/whitespace, case-insensitive Q, quarter digit 1-4. Compiled because TryParseQuarterShorthand
    // is tried on every keystroke's eventual Enter-commit in Quarter mode.
    static readonly Regex _quarterPattern = new(@"^\s*(\d{1,4})\s*-?\s*[Qq]\s*([1-4])\s*$", RegexOptions.Compiled);

    // Matches a typed week shorthand: "2026-W08", "2026W8", "2026 w08" -- 1-4 digit year, optional
    // dash/whitespace, case-insensitive W, 1-2 digit week number. Compiled for the same reason as
    // _quarterPattern above.
    static readonly Regex _weekPattern = new(@"^\s*(\d{1,4})\s*-?\s*[Ww]\s*(\d{1,2})\s*$", RegexOptions.Compiled);

    // Quarter mode's null-Format typed-text parse: the pure regex+arithmetic core of DatePicker's
    // TryParseDate special case. Returns false (leaving `value` at its default) for anything that
    // doesn't match the shorthand -- the caller falls through to the general DateTime parse, same as
    // any other malformed text.
    public static bool TryParseQuarterShorthand(string text, out DateTime value)
    {
        var match = _quarterPattern.Match(text);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
            year is >= 1 and <= 9999)
        {
            var quarter = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            value = QuarterStart(year, quarter);
            return true;
        }
        value = default;
        return false;
    }

    // Week mode's null-Format typed-text parse: the pure regex+arithmetic core of DatePicker's
    // TryParseDate special case -- the exact inverse of FormatWeekDisplay's display: walk the week
    // starts whose calendar year is the typed year and return the one GetWeekOfYear numbers N. Plain
    // arithmetic from WeekStart(Jan 1) can't do this -- under CalendarWeekRule.FirstDay a year that
    // doesn't begin on firstDayOfWeek numbers its partial first week 1, so every later week start is
    // one ahead of the (N-1)*7 offset and a displayed week wouldn't round-trip. A week number the
    // display never produces for that year (e.g. W01 when Jan 1's week started in December) finds no
    // match and returns false, same as any other malformed text.
    public static bool TryParseWeekShorthand(string text, CultureInfo culture, DayOfWeek firstDayOfWeek, out DateTime value)
    {
        var match = _weekPattern.Match(text);
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var year) &&
            year is >= 1 and <= 9999)
        {
            var week = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            try
            {
                // First week start whose calendar year is `year` (WeekStart(Jan 1) itself may
                // belong to the prior December), then at most 53 boundary steps.
                var s = WeekStart(new DateTime(year, 1, 1), firstDayOfWeek);
                if (s.Year < year) s = s.AddDays(7);
                for (; s.Year == year; s = s.AddDays(7))
                {
                    if (WeekNumberOf(s, culture, firstDayOfWeek) == week)
                    {
                        value = s;
                        return true;
                    }
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                // WeekStart/AddDays overflowed at the DateTime range edge (year 1 / 9999) --
                // fall through to the general parse below, same as any other malformed text.
            }
        }
        value = default;
        return false;
    }

    /// <summary>Quarter mode's null-<c>Format</c> display for <paramref name="value"/> —
    /// <c>"yyyy-Qn"</c> (e.g. "2026-Q3") in <paramref name="culture"/>'s digits. Shared by
    /// <see cref="DatePicker"/>'s own display and <see cref="EditDatePicker{T}"/>'s read-only
    /// view.</summary>
    public static string FormatQuarterDisplay(DateTime value, CultureInfo culture) =>
        $"{value.Year.ToString(culture)}-Q{QuarterOf(value).ToString(culture)}";

    /// <summary>Week mode's null-<c>Format</c> display for <paramref name="value"/> —
    /// <c>"yyyy-Www"</c> (e.g. "2026-W08") in <paramref name="culture"/>'s digits, where the year is
    /// the WEEK START's calendar year (deterministic at year-boundary weeks, unlike
    /// <paramref name="value"/>'s own year, which can disagree with the week it falls in). Shared by
    /// <see cref="DatePicker"/>'s own display and <see cref="EditDatePicker{T}"/>'s read-only
    /// view.</summary>
    public static string FormatWeekDisplay(DateTime value, CultureInfo culture, DayOfWeek firstDayOfWeek)
    {
        var weekStart = WeekStart(value, firstDayOfWeek);
        return $"{weekStart.Year.ToString(culture)}-W{WeekNumberOf(weekStart, culture, firstDayOfWeek).ToString("00", culture)}";
    }
}
