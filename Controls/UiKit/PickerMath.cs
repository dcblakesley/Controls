using System.Text.RegularExpressions;

namespace Controls;

/// <summary>
/// Pure, instance-independent date/culture arithmetic shared by <see cref="DatePicker"/> and
/// <see cref="DateRangePicker"/> (and, via the promoted display helpers —
/// <see cref="ModeDisplayFormat"/>, <see cref="ReadOnlyDisplay"/>, <see cref="FirstDayOfWeekOrCulture"/>
/// and the quarter/week formatters — by <see cref="EditDate{T}"/>'s and <see cref="EditDateRange"/>'s
/// read-only views, so edit mode and read-only mode can't drift). Every member takes its inputs explicitly --
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

    // The first day of the calendar week containing `day`, AT MIDNIGHT, per `firstDayOfWeek`. Shared
    // by GridDays (the 42-cell layout) and Home/End keyboard navigation so they can never disagree.
    //
    // The `.Date` truncation is load-bearing, not cosmetic: this is also Week mode's own
    // normalization (see NormalizeForMode), and every rendered week start -- a grid row's first cell,
    // a day click's committed unit -- is a midnight date. A time-carrying input (a consumer binding
    // `Start = 2026-03-04T09:00`, or a `DateTime.Now`-shaped preset/typed commit) previously produced
    // a 09:00 week start that equalled no rendered week start at all, so the selected week never
    // painted and no cell was a keyboard focus stop -- the exact failure the normalization exists to
    // prevent. Truncating here rather than at each caller keeps the one week-start concept single-
    // shaped for the display, the commit and the Min/Max guard alike.
    public static DateTime WeekStart(DateTime day, DayOfWeek firstDayOfWeek)
    {
        var lead = ((int)day.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        // Year 1's own first week has fewer than `lead` days behind it, so the unclamped subtraction
        // underflows DateTime.MinValue and throws -- reachable from a plain Mode="Week" picker bound
        // to default(DateTime), which walks six of these building the grid. Clamping to the days
        // actually available yields that partial first week's own start (0001-01-01), which is what
        // every caller wants there anyway. The clamp counts from day.Date (the same value subtracted
        // from below), so the result can never land before DateTime.MinValue.
        lead = Math.Min(lead, (day.Date - DateTime.MinValue).Days);
        return day.Date.AddDays(-lead);
    }

    /// <summary>The last day of the calendar week starting on <paramref name="weekStart"/> —
    /// <c>weekStart + 6 days</c>, clamped to the last representable date so the final week of year
    /// 9999 (whose 7-day span runs past <see cref="DateTime.MaxValue"/>) can't overflow and throw.
    /// The mirror image of <see cref="WeekStart"/>'s own year-1 clamp.</summary>
    public static DateTime WeekEnd(DateTime weekStart) =>
        weekStart.AddDays(Math.Min(6, (DateTime.MaxValue.Date - weekStart.Date).Days));

    /// <summary>Whether the whole calendar week starting on <paramref name="weekStart"/> is
    /// unselectable: its 7-day span falls entirely outside [<paramref name="min"/>,
    /// <paramref name="max"/>], or <paramref name="disabledDate"/> itself rejects the week start (the
    /// one place that predicate sees a week start rather than a day). The individual day buttons of a
    /// partially-in-range week stay enabled at their own day granularity — only the commit is guarded
    /// here. Shared verbatim by <see cref="DatePicker"/>'s and <see cref="DateRangePicker"/>'s own
    /// Week-mode commit guards, which were character-identical; the week end goes through
    /// <see cref="WeekEnd"/> so a typed commit in year 9999's last week can't throw.</summary>
    public static bool IsWeekDisabledForCommit(DateTime weekStart, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate) =>
        (max is { } mx && weekStart > mx.Date) || (min is { } mn && WeekEnd(weekStart) < mn.Date) ||
        (disabledDate?.Invoke(weekStart) ?? false);

    // ----- Min/Max/DisabledDate predicates, one per grid granularity -----------------------------
    // The four siblings of IsWeekDisabledForCommit above, hoisted here for the same reason it was:
    // both pickers had them character-identical. `disabledDate` is folded into every one of them (it
    // is never invoked separately anywhere else) so that every consumer -- the cell `disabled`
    // attributes, the FirstEnabled*/DefaultFocus* skip logic below, and each picker's own typed-text/
    // preset commit guards -- picks it up automatically and none of them can disagree about what
    // counts as disabled. Each takes its unit ALREADY at that granularity (the 1st of the month at
    // midnight, January 1st, the 1st of the quarter), matching what the corresponding grid renders
    // and what DisabledDate's own documented contract promises the consumer's predicate will see.

    /// <summary>Whether <paramref name="day"/> falls outside [<paramref name="min"/>,
    /// <paramref name="max"/>] at DAY granularity (both bounds are date-only, so the comparison is
    /// against their <c>.Date</c>), or <paramref name="disabledDate"/> rejects it.</summary>
    public static bool IsDayDisabled(DateTime day, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate) =>
        (min is { } mn && day < mn.Date) || (max is { } mx && day > mx.Date) ||
        (disabledDate?.Invoke(day) ?? false);

    /// <summary>The month-granularity equivalent of <see cref="IsDayDisabled"/>: a whole month is
    /// disabled once it falls entirely outside [<paramref name="min"/>, <paramref name="max"/>]'s own
    /// months. <paramref name="month"/> is already <see cref="FirstOfMonth"/>-shaped.</summary>
    public static bool IsMonthDisabled(DateTime month, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate) =>
        (min is { } mn && month < FirstOfMonth(mn)) || (max is { } mx && month > FirstOfMonth(mx)) ||
        (disabledDate?.Invoke(month) ?? false);

    /// <summary>The year-granularity equivalent, one grain up from <see cref="IsMonthDisabled"/>.
    /// <paramref name="year"/> is already <see cref="FirstOfYear"/>-shaped.</summary>
    public static bool IsYearDisabled(DateTime year, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate) =>
        (min is { } mn && year < FirstOfYear(mn)) || (max is { } mx && year > FirstOfYear(mx)) ||
        (disabledDate?.Invoke(year) ?? false);

    /// <summary>The quarter-granularity equivalent. <paramref name="quarterStart"/> is already
    /// <see cref="QuarterStart(DateTime)"/>-shaped.</summary>
    public static bool IsQuarterDisabled(DateTime quarterStart, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate) =>
        (min is { } mn && quarterStart < QuarterStart(mn)) || (max is { } mx && quarterStart > QuarterStart(mx)) ||
        (disabledDate?.Invoke(quarterStart) ?? false);

    // ----- First-enabled scanners, one per grid granularity ---------------------------------------
    // Each picker's DefaultFocus* chain ends in one of these: when neither natural candidate (the
    // bound value / today) is usable, the roving tabindex must still land on an ENABLED cell, or the
    // grid is left keyboard-unreachable (Tab skips a tabindex="0" that is also disabled). Null means
    // the whole panel is disabled, at which point the caller falls back to any deterministic unit --
    // there is nothing actionable in it either way.

    /// <summary>The first enabled IN-MONTH day of <paramref name="month"/>'s 42-cell grid (the
    /// leading/trailing adjacent-month cells are skipped), or null when every in-month day is
    /// disabled.</summary>
    public static DateTime? FirstEnabledDay(DateTime month, DayOfWeek firstDayOfWeek, DateTime? min,
        DateTime? max, Func<DateTime, bool>? disabledDate)
    {
        foreach (var day in GridDays(month, firstDayOfWeek))
        {
            if (day.Month == month.Month && day.Year == month.Year && !IsDayDisabled(day, min, max, disabledDate))
            {
                return day;
            }
        }
        return null;
    }

    /// <summary>The first enabled month of <paramref name="year"/>, or null when all 12 are disabled.</summary>
    public static DateTime? FirstEnabledMonth(int year, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate)
    {
        for (var m = 1; m <= 12; m++)
        {
            var month = new DateTime(year, m, 1);
            if (!IsMonthDisabled(month, min, max, disabledDate)) return month;
        }
        return null;
    }

    /// <summary>The first enabled quarter of <paramref name="year"/>, or null when all 4 are disabled.</summary>
    public static DateTime? FirstEnabledQuarter(int year, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate)
    {
        for (var q = 1; q <= 4; q++)
        {
            var quarterStart = QuarterStart(year, q);
            if (!IsQuarterDisabled(quarterStart, min, max, disabledDate)) return quarterStart;
        }
        return null;
    }

    /// <summary>The first enabled year of <paramref name="decadeStart"/>'s own 10 years — never one
    /// of the year grid's two dimmed adjacent-decade cells, which belong to a decade the panel isn't
    /// showing — or null when all 10 are disabled.</summary>
    public static DateTime? FirstEnabledYear(int decadeStart, DateTime? min, DateTime? max,
        Func<DateTime, bool>? disabledDate)
    {
        for (var y = decadeStart; y <= decadeStart + 9; y++)
        {
            var year = new DateTime(y, 1, 1);
            if (!IsYearDisabled(year, min, max, disabledDate)) return year;
        }
        return null;
    }

    // The ISO-ish week number of the calendar week starting on `weekStart`, per `culture`'s week
    // rule -- shared by DatePicker's own WeekNumberOf display and FormatWeekDisplay/
    // TryParseWeekShorthand below. DateTimeFormat.Calendar, not the culture's own default Calendar:
    // GregorianCultureHelper forces Gregorian by swapping the former, so reading the latter would
    // number the week in a non-Gregorian calendar (ar-SA's Umm al-Qura, th-TH's Buddhist) while every
    // other picker-facing format stayed Gregorian. Identical for the cultures that already default to
    // Gregorian, en-US included.
    public static int WeekNumberOf(DateTime weekStart, CultureInfo culture, DayOfWeek firstDayOfWeek) =>
        culture.DateTimeFormat.Calendar.GetWeekOfYear(weekStart, culture.DateTimeFormat.CalendarWeekRule, firstDayOfWeek);

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

    // ----- Physical -> logical horizontal arrow translation ---------------------------------------
    // The four navigation maps below are written for a LEFT-TO-RIGHT calendar, where ArrowRight is
    // "the next unit". Under an RTL UI culture the stylesheet mirrors every grid (the cells flow
    // right-to-left), so the PHYSICAL Right arrow has to step to the PREVIOUS unit for focus to
    // follow the key VISUALLY -- the APG rule for physical horizontal arrows in a mirrored layout.
    // See RtlSupport for why the ambient UI culture is the signal, and for why only this one pair
    // swaps: ArrowUp/ArrowDown, Home/End and PageUp/PageDown are logical moves (a row, a week, a
    // month, a year) with no visual handedness at all.
    //
    // Applied HERE -- once per map, ahead of the switch -- rather than in each picker's own grid
    // keydown handler: every grid in both pickers (DatePicker's day/month/quarter/year grids,
    // DateRangePicker's dual panels and its single-panel pick session) reaches its navigation
    // through exactly these four methods, so this is the one seam that covers all of them and the
    // only place the rule has to be stated. It is also the one ambient-culture read in an otherwise
    // inputs-explicit class -- process/circuit state, not component state, so nothing about sharing
    // this type between the two pickers changes.
    static string LogicalKey(string key) => RtlSupport.IsRightToLeft
        ? key switch
        {
            "ArrowLeft" => "ArrowRight",
            "ArrowRight" => "ArrowLeft",
            _ => key,
        }
        : key;

    // Maps a keydown's Key to the day it should move focus to, or null when the key isn't a
    // navigation key. Left/Right below are the LOGICAL directions -- LogicalKey has already swapped
    // the physical pair under an RTL culture. AddDays/AddMonths throws at the DateTime.MinValue/
    // MaxValue edge — the caller treats that as the key being a no-op there rather than letting the
    // exception escape.
    public static DateTime? NextFocusDay(DateTime current, string key, DayOfWeek firstDayOfWeek)
    {
        try
        {
            return LogicalKey(key) switch
            {
                "ArrowLeft" => current.AddDays(-1),
                "ArrowRight" => current.AddDays(1),
                "ArrowUp" => current.AddDays(-7),
                "ArrowDown" => current.AddDays(7),
                "Home" => WeekStart(current, firstDayOfWeek),
                // WeekEnd, not a bare AddDays(6): one named week-end concept, clamped the same way
                // WeekStart clamps its own edge (unreachable from a grid, whose month is clamped well
                // inside the range, but the two halves of Home/End shouldn't disagree about it).
                "End" => WeekEnd(WeekStart(current, firstDayOfWeek)),
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
    // month of the focused row. Left/Right are logical (see LogicalKey's RTL swap). AddMonths/
    // AddYears throws at the DateTime.MinValue/MaxValue edge -- the caller treats that as the key
    // being a no-op there.
    public static DateTime? NextFocusMonth(DateTime current, string key)
    {
        try
        {
            return LogicalKey(key) switch
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
    // year's first/last quarter; PageUp/PageDown step a year, keeping the same quarter. Left/Right
    // are logical (see LogicalKey's RTL swap). AddMonths/the DateTime constructor throw at the
    // DateTime.MinValue/MaxValue edge -- the caller treats that as the key being a no-op there, same
    // as NextFocusMonth.
    public static DateTime? NextFocusQuarter(DateTime current, string key)
    {
        try
        {
            return LogicalKey(key) switch
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
    // used only for Home/End's row grouping. Left/Right are logical (see LogicalKey's RTL swap).
    // Plain int arithmetic (unlike NextFocusMonth/NextFocusQuarter's DateTime.AddX, this can't
    // throw) -- clamped to DateTime's representable year range instead so a move at the very edge is
    // a no-op there.
    public static DateTime? NextFocusYear(DateTime current, string key, int decadeStart)
    {
        var year = current.Year;
        int? next = LogicalKey(key) switch
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
            yield return TwoTextElements(names[((int)firstDayOfWeek + i) % 7]);
        }
    }

    // The first two GRAPHEME CLUSTERS of `name`, not its first two chars. A plain `name[..2]` splits
    // a surrogate pair in half (a name whose second character is astral -- "a" + an emoji-plane
    // glyph -- yielded a lone high surrogate the browser draws as U+FFFD) and can sever a combining
    // mark from the base it modifies. The header is decorative and aria-hidden (each day button
    // carries its own full "D"-format name), so this is cheap insurance rather than a correctness
    // requirement -- and it stays allocation-free for every name already short enough, including
    // every ASCII and single-glyph (ja, zh) culture, which is all of the realistic ones.
    static string TwoTextElements(string name)
    {
        var span = name.AsSpan();
        var first = StringInfo.GetNextTextElementLength(span);
        if (first >= span.Length) return name;
        var take = first + StringInfo.GetNextTextElementLength(span[first..]);
        return take >= span.Length ? name : name[..take];
    }

    public static string MonthName(CultureInfo culture, int month) =>
        culture.DateTimeFormat.AbbreviatedMonthNames[month - 1];

    /// <summary>The <c>FirstDayOfWeek</c> a picker (or an <see cref="EditDate{T}"/>/<see cref="EditDateRange"/>
    /// read-only view, which has no picker instance to ask) actually uses: an explicitly-set
    /// <paramref name="explicitValue"/> wins, otherwise <paramref name="culture"/>'s own first day. The
    /// single resolution site for all four, so edit mode and the read-only display can never disagree
    /// about which day a week starts on (and therefore about a Week-mode week number).</summary>
    public static DayOfWeek FirstDayOfWeekOrCulture(DayOfWeek? explicitValue, CultureInfo culture) =>
        explicitValue ?? culture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>
    /// The default (null-<c>Format</c>) display/parse format string for <paramref name="mode"/>, built
    /// from the caller's own two base formats: <paramref name="dateBase"/> (the full date, e.g.
    /// <c>"MM/dd/yyyy"</c> for the pickers' input display, <c>"MM-dd-yyyy"</c> for the form controls'
    /// read-only display) and <paramref name="monthBase"/> (the same shape without the day, e.g.
    /// <c>"MM/yyyy"</c>). Passed rather than derived so each caller's own literals stay visible at its
    /// own call site and no string surgery can silently reshape them.
    /// </summary>
    /// <remarks>
    /// <c>DateTime</c> is <paramref name="dateBase"/> plus a space plus <see cref="TimeFormatString"/>;
    /// <c>Time</c> is that time portion alone. Year/Quarter/Week are all a bland <c>"yyyy"</c>: Quarter's
    /// and Week's real displays have no .NET format token at all (see <see cref="FormatQuarterDisplay"/>/
    /// <see cref="FormatWeekDisplay"/>, which every caller special-cases ahead of this), so for those two
    /// the value only ever matters as a typed-text exact-format fallback. An unrecognized mode falls back
    /// to <paramref name="dateBase"/>, same as <c>Date</c>.
    /// </remarks>
    public static string ModeDisplayFormat(DatePickerMode mode, string dateBase, string monthBase, bool use12Hours, bool showSeconds) => mode switch
    {
        DatePickerMode.Month => monthBase,
        DatePickerMode.DateTime => $"{dateBase} {TimeFormatString(use12Hours, showSeconds)}",
        DatePickerMode.Time => TimeFormatString(use12Hours, showSeconds),
        DatePickerMode.Year or DatePickerMode.Quarter or DatePickerMode.Week => "yyyy",
        _ => dateBase,
    };

    /// <summary>
    /// The read-only (non-editing) display shared by <see cref="EditDate{T}"/> and
    /// <see cref="EditDateRange"/>: the Quarter/Week shorthand when it applies, otherwise
    /// <paramref name="format"/>'s own <c>ToString(format, culture)</c> result, falling back to
    /// <paramref name="formatFallback"/> when the format string is one .NET rejects.
    /// </summary>
    /// <param name="mode">The control's effective picker mode — only Quarter and Week take the shorthand.</param>
    /// <param name="shorthandValue">The value to render the Quarter/Week shorthand for, or <c>null</c> to
    /// skip the shorthand entirely — which is how a caller expresses "an explicit format override applies,
    /// so use it verbatim" (matching the pickers' own <c>Format</c> contract).</param>
    /// <param name="culture">Culture for both the shorthand's digits and the <c>ToString</c> fallthrough
    /// (the callers force Gregorian here, like the pickers' own display).</param>
    /// <param name="firstDayOfWeek">The control's explicit <c>FirstDayOfWeek</c>, or null to follow
    /// <paramref name="culture"/> — resolved through <see cref="FirstDayOfWeekOrCulture"/>.</param>
    /// <param name="format">The caller's own <c>ToString(format, culture)</c>, as a delegate because each
    /// caller formats a different shape (<see cref="EditDate{T}"/> switches over its four bound CLR types;
    /// <see cref="EditDateRange"/> formats one endpoint <c>DateTime</c>).</param>
    /// <param name="formatFallback">What to render when <paramref name="format"/> throws
    /// <see cref="FormatException"/> — a consumer-supplied format string .NET rejects must degrade to
    /// something readable rather than throw mid-render.</param>
    public static string ReadOnlyDisplay(DatePickerMode mode, DateTime? shorthandValue, CultureInfo culture,
        DayOfWeek? firstDayOfWeek, Func<string> format, Func<string> formatFallback)
    {
        if (shorthandValue is { } v)
        {
            if (mode == DatePickerMode.Quarter) return FormatQuarterDisplay(v, culture);
            if (mode == DatePickerMode.Week) return FormatWeekDisplay(v, culture, FirstDayOfWeekOrCulture(firstDayOfWeek, culture));
        }
        try
        {
            return format();
        }
        catch (FormatException)
        {
            return formatFallback();
        }
    }

    // Central per-mode normalization, reached through each picker's own NormalizeForMode override, so
    // every commit path (PickerBase.TryParseDate's typed text, a click, a select change) agrees on the
    // same shape of value.
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
        // Midnight like every other arm -- WeekStart itself truncates (see its own comment), so a
        // time-carrying value normalizes to a week start that actually equals the rendered one.
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

    // Quarter mode's null-Format typed-text parse: the pure regex+arithmetic core of
    // PickerBase.TryParseDate's special case. Returns false (leaving `value` at its default) for anything that
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

    // Week mode's null-Format typed-text parse: the pure regex+arithmetic core of
    // PickerBase.TryParseDate's special case -- the exact inverse of FormatWeekDisplay's display: walk the week
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
                // A boundary step overflowed past DateTime.MaxValue (year 9999's own last week start
                // + 7 days) -- fall through to the general parse below, same as any other malformed
                // text. WeekStart itself can no longer throw here (it clamps at year 1), but the
                // AddDays walk above still can.
            }
        }
        value = default;
        return false;
    }

    /// <summary>Quarter mode's null-<c>Format</c> display for <paramref name="value"/> —
    /// <c>"yyyy-Qn"</c> (e.g. "2026-Q3") in <paramref name="culture"/>'s digits. Shared by
    /// <see cref="DatePicker"/>'s own display and <see cref="EditDate{T}"/>'s read-only
    /// view.</summary>
    public static string FormatQuarterDisplay(DateTime value, CultureInfo culture) =>
        $"{value.Year.ToString(culture)}-Q{QuarterOf(value).ToString(culture)}";

    /// <summary>Week mode's null-<c>Format</c> display for <paramref name="value"/> —
    /// <c>"yyyy-Www"</c> (e.g. "2026-W08") in <paramref name="culture"/>'s digits, where the year is
    /// the WEEK START's calendar year (deterministic at year-boundary weeks, unlike
    /// <paramref name="value"/>'s own year, which can disagree with the week it falls in). Shared by
    /// <see cref="DatePicker"/>'s own display and <see cref="EditDate{T}"/>'s read-only
    /// view.</summary>
    public static string FormatWeekDisplay(DateTime value, CultureInfo culture, DayOfWeek firstDayOfWeek)
    {
        var weekStart = WeekStart(value, firstDayOfWeek);
        return $"{weekStart.Year.ToString(culture)}-W{WeekNumberOf(weekStart, culture, firstDayOfWeek).ToString("00", culture)}";
    }

    // ----- Time-row option building (shared by DatePicker's Time/DateTime panel and
    // DateRangePicker's Time/DateTime pick session) -----------------------------------------------

    /// <summary>The option values a stepped time select offers before <c>DisabledTime</c> hides/
    /// disables any of them: every <paramref name="step"/>-th value from 0 to <paramref name="max"/>
    /// inclusive, plus <paramref name="current"/> itself if it isn't naturally on that lattice -- the
    /// NEVER-JUMP RULE for HourStep/MinuteStep/SecondStep, composing with <c>DisabledTime</c>'s own
    /// (see <c>HideDisabledTimeOptions</c>) so a select can never silently show a value that isn't
    /// the one actually bound. A <see cref="SortedSet{T}"/> both dedupes (<paramref name="current"/>
    /// may already be on the lattice) and keeps the option list in its natural ascending reading
    /// order even though <paramref name="current"/> wasn't necessarily added in numeric order.
    /// <paramref name="step"/> is trusted to already be &gt;= 1.</summary>
    public static IEnumerable<int> SteppedOptions(int max, int step, int current)
    {
        var options = new SortedSet<int>();
        for (var v = 0; v <= max; v += step) options.Add(v);
        options.Add(current);
        return options;
    }

    /// <summary>The hour values a time row's hour select offers, before <c>DisabledTime</c> hides/
    /// disables any of them: every <paramref name="step"/>-th 24-hour value (0, step, 2*step, ... &lt;=
    /// 23) via <see cref="SteppedOptions"/>, further filtered under <paramref name="use12Hours"/> to
    /// just the hours belonging to <paramref name="currentHour"/>'s own AM/PM period.
    /// <see cref="SteppedOptions"/>'s own never-jump already guarantees <paramref name="currentHour"/>
    /// survives that filter -- its period is computed from itself, so it can never be filtered OUT.
    /// The result is ascending 24h order, which -- within one period -- is already exactly the
    /// "12, 1, 2, ... 11" 12-hour reading order (h%12 rises in step with h in both halves of the
    /// day; see <see cref="HourOptionText"/> for the label itself).</summary>
    public static IEnumerable<int> HourOptions(int step, int currentHour, bool use12Hours)
    {
        var options = SteppedOptions(23, step, currentHour);
        return use12Hours ? options.Where(h => (h >= 12) == (currentHour >= 12)) : options;
    }

    /// <summary>The hour select's option TEXT for <paramref name="h"/> (always a 24h value):
    /// zero-padded 24h ("00".."23") normally, or the plain (non-zero-padded) 12-hour reading
    /// ("12", "1".."11") under <paramref name="use12Hours"/> -- matching the unpadded "h" custom
    /// format specifier a picker's own time format string uses for the same mode.</summary>
    public static string HourOptionText(int h, bool use12Hours, CultureInfo culture) => use12Hours
        ? (h % 12 == 0 ? 12 : h % 12).ToString(culture)
        : h.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>Whether <paramref name="value"/> is one of <paramref name="disabled"/>'s listed
    /// values -- null (nothing disabled in that unit) always answers false. Shared by a picker's own
    /// commit guard and its time row's per-option render check so the two can never disagree about
    /// the same hour/minute/second.</summary>
    public static bool IsTimePartDisabled(IReadOnlyCollection<int>? disabled, int value) =>
        disabled?.Contains(value) ?? false;

    /// <summary>The Time/DateTime portion of a picker's default (null-<c>Format</c>) format string:
    /// <paramref name="showSeconds"/> false drops ":ss"; <paramref name="use12Hours"/> switches the
    /// 24-hour "HH" to the unpadded 12-hour "h" plus a trailing "tt" designator (matching
    /// <see cref="HourOptionText"/>'s own unpadded 12-hour option text).</summary>
    public static string TimeFormatString(bool use12Hours, bool showSeconds) => use12Hours
        ? (showSeconds ? "h:mm:ss tt" : "h:mm tt")
        : (showSeconds ? "HH:mm:ss" : "HH:mm");

    /// <summary>
    /// The candidate value a time-row select change produces: <paramref name="current"/>'s own date part
    /// (or <see cref="DateTime.Today"/> when it has none) plus its own time-of-day (or midnight) with the
    /// one supplied <paramref name="hour"/>/<paramref name="minute"/>/<paramref name="second"/> part
    /// replaced — pass null for the parts the change didn't touch.
    /// </summary>
    /// <remarks>
    /// <paramref name="showSeconds"/> false zeroes the second here (not just in
    /// <see cref="NormalizeForMode"/>) so an hour/minute change can never be rejected by a
    /// <c>DisabledTime</c> guard over a stale second no select can even change. Shared verbatim by
    /// <see cref="DatePicker"/>'s immediate commit and <see cref="DateRangePicker"/>'s pending pick
    /// session — the two differ only in where the composed value is written, never in how it's composed.
    /// </remarks>
    public static DateTime ComposeTimePart(DateTime? current, bool showSeconds, int? hour, int? minute, int? second)
    {
        var date = current?.Date ?? DateTime.Today;
        var time = current?.TimeOfDay ?? TimeSpan.Zero;
        var seconds = showSeconds ? second ?? time.Seconds : 0;
        return date + new TimeSpan(hour ?? time.Hours, minute ?? time.Minutes, seconds);
    }
}
