namespace Controls.Helpers;

/// <summary>
/// Resolves the culture the calendar pickers format and parse with. The pickers are
/// Gregorian-calendar controls: the 42-cell grids, month/year arithmetic, and Min/Max comparisons
/// are all plain <see cref="DateTime"/> (Gregorian) math regardless of culture. Most cultures
/// already default to the Gregorian calendar, so the common path returns the culture unchanged
/// (no allocation) — a handful (e.g. th-TH's Buddhist calendar, some ar-* Hijri locales) don't,
/// and formatting/parsing straight through them would glue a non-Gregorian year/month system onto
/// the Gregorian grid (a Thai Buddhist year next to a plain "2026" year-select option; day
/// aria-labels in a different calendar than the grid they sit in). For those, every picker-facing
/// format and parse — including the <c>EditDatePicker</c>/<c>EditDateRange</c> read-only display,
/// which must agree with the editable picker — routes through a clone with the calendar forced to
/// Gregorian instead: same language, always the Gregorian calendar.
/// </summary>
internal static class GregorianCultureHelper
{
    internal static CultureInfo Gregorian(CultureInfo culture)
    {
        if (culture.DateTimeFormat.Calendar is GregorianCalendar) return culture;
        if (!culture.OptionalCalendars.Any(c => c is GregorianCalendar)) return CultureInfo.InvariantCulture;
        var clone = (CultureInfo)culture.Clone();
        try
        {
            clone.DateTimeFormat.Calendar = new GregorianCalendar();
            return clone;
        }
        catch (ArgumentException)
        {
            // Vanishingly rare — virtually every ICU culture accepts Gregorian as an optional
            // calendar. InvariantCulture (Gregorian by default) keeps the contract rather than
            // formatting/parsing against a calendar that disagrees with the grid.
            return CultureInfo.InvariantCulture;
        }
    }
}
