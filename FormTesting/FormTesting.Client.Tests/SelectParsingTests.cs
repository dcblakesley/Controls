using Controls.Helpers;

// These tests construct components directly to exercise the protected parser; setting the [Parameter]
// Field that way is exactly what BL0005 warns about, but it's intentional and safe here.
#pragma warning disable BL0005

namespace FormTesting.Client.Tests;

/// <summary>
/// Tests the shared select parsing (SelectParsing) that EditSelect / EditSelectString /
/// EditSelectEnum / EditRadioEnum delegate to, plus the throw-contract of the callback-bound
/// controls (EditBool / EditSelectSearch) that intentionally don't parse string input.
/// </summary>
public class SelectParsingTests
{
    [Fact]
    public void StringOrConvert_passes_strings_through()
    {
        var ok = SelectParsing.TryParseStringOrConvert<string>("hello", "Name", out var result, out var err);
        Assert.True(ok);
        Assert.Equal("hello", result);
        Assert.Null(err);
    }

    [Fact]
    public void StringOrConvert_converts_a_valid_value_type()
    {
        var ok = SelectParsing.TryParseStringOrConvert<int>("42", "Age", out var result, out _);
        Assert.True(ok);
        Assert.Equal(42, result);
    }

    [Fact]
    public void StringOrConvert_parses_fractional_values_invariantly_regardless_of_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // de-DE's thousands-separator tolerance read "1.5" as 15 under CurrentCulture parsing,
            // while the bound value formatted back as "1,5" — matching no option.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var ok = SelectParsing.TryParseStringOrConvert<double>("1.5", "Price", out var result, out _);
            Assert.True(ok);
            Assert.Equal(1.5, result);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void StringOrConvert_rejects_an_invalid_value_with_message()
    {
        var ok = SelectParsing.TryParseStringOrConvert<int>("abc", "Age", out _, out var err);
        Assert.False(ok);
        Assert.Equal("The Age field is not valid.", err);
    }

    [Fact]
    public void StringOrConvert_empty_clears_a_string_to_null()
    {
        // Previously "" was passed straight through, so a string? could never return to null via the UI.
        var ok = SelectParsing.TryParseStringOrConvert<string?>("", "Name", out var result, out var err);
        Assert.True(ok);
        Assert.Null(result);
        Assert.Null(err);
    }

    [Fact]
    public void StringOrConvert_empty_clears_a_nullable_value_type_to_null()
    {
        var ok = SelectParsing.TryParseStringOrConvert<int?>("", "Age", out var result, out var err);
        Assert.True(ok);
        Assert.Null(result);
        Assert.Null(err);
    }

    [Fact]
    public void StringOrConvert_empty_is_default_for_a_non_nullable_value_type_without_error()
    {
        // Previously "" was fed to BindConverter and failed with "The Age field is not valid."; it now
        // clears to default(int) with success.
        var ok = SelectParsing.TryParseStringOrConvert<int>("", "Age", out var result, out var err);
        Assert.True(ok);
        Assert.Equal(0, result);
        Assert.Null(err);
    }

    [Fact]
    public void FormatInvariant_formats_fractional_values_invariantly_regardless_of_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // The format side used to be InputBase's default value?.ToString() (current culture), so a
            // double 1.5 rendered "1,5" under de-DE and matched no <option value="1.5"> — the mirror of
            // the parse-side bug above. FormatInvariant keeps the "." so it round-trips with the parse.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.Equal("1.5", SelectParsing.FormatInvariant(1.5));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatInvariant_passes_strings_through_and_maps_null_to_null()
    {
        Assert.Equal("hello", SelectParsing.FormatInvariant("hello"));
        Assert.Null(SelectParsing.FormatInvariant<string?>(null));
        Assert.Null(SelectParsing.FormatInvariant<int?>(null));
    }

    [Fact]
    public void FormatInvariant_formats_an_enum_by_name()
    {
        // Enums are IFormattable but ignore culture — they must still format by name so the value
        // round-trips with TryParseEnum, not as the underlying number.
        Assert.Equal("High", SelectParsing.FormatInvariant(Priority.High));
    }

    [Fact]
    public void FormatInvariant_round_trips_with_the_invariant_parse_under_a_foreign_culture()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var formatted = SelectParsing.FormatInvariant(1.5);                       // "1.5"
            var ok = SelectParsing.TryParseStringOrConvert<double>(formatted, "Price", out var back, out _);
            Assert.True(ok);
            Assert.Equal(1.5, back);                                                  // parses back to the same value
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void FormatInvariant_formats_a_DateOnly_as_ISO_yyyy_MM_dd()
    {
        // EditSelect<DateOnly> with the natural <option value="2026-06-15">: the invariant *display*
        // format was "06/15/2026", matched no option, and the select snapped back to unselected. The
        // date special-casing now emits the ISO literal an author actually writes.
        Assert.Equal("2026-06-15", SelectParsing.FormatInvariant(new DateOnly(2026, 6, 15)));
    }

    [Fact]
    public void FormatInvariant_round_trips_date_and_time_types_through_the_invariant_parse()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            // A foreign culture whose date display differs from ISO — the format and parse are both pinned
            // to InvariantCulture, so each date type must format to a literal its own invariant parse accepts.
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            RoundTrips(new DateOnly(2026, 6, 15));
            RoundTrips(new DateTime(2026, 6, 15, 14, 30, 45));                                   // whole seconds — option values carry no sub-second precision
            RoundTrips(new DateTimeOffset(2026, 6, 15, 14, 30, 45, TimeSpan.FromHours(-5)));      // offset must survive the round trip
            RoundTrips(new TimeOnly(14, 30, 45));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }

        static void RoundTrips<T>(T value)
        {
            var formatted = SelectParsing.FormatInvariant(value);
            var ok = SelectParsing.TryParseStringOrConvert<T>(formatted, "When", out var back, out var err);
            Assert.True(ok, $"{typeof(T).Name} failed to parse back from '{formatted}': {err}");
            Assert.Equal(value, back);
        }
    }

    [Fact]
    public void FormatInvariant_formats_each_date_type_as_the_literal_an_author_writes()
    {
        // Round-tripping through the parse isn't enough — the formatted string must EQUAL the option
        // value an author naturally writes, or the browser shows the select as unselected while the
        // model holds the value (the M13/M14 desync). One canonical authored form per type:
        Assert.Equal("2026-06-15", SelectParsing.FormatInvariant(new DateOnly(2026, 6, 15)));
        Assert.Equal("2026-06-15T14:30:45", SelectParsing.FormatInvariant(new DateTime(2026, 6, 15, 14, 30, 45)));
        Assert.Equal("2026-06-15T14:30:45-05:00", SelectParsing.FormatInvariant(new DateTimeOffset(2026, 6, 15, 14, 30, 45, TimeSpan.FromHours(-5))));
        Assert.Equal("2026-06-15T14:30:45+00:00", SelectParsing.FormatInvariant(new DateTimeOffset(2026, 6, 15, 14, 30, 45, TimeSpan.Zero)));
        Assert.Equal("14:30:45", SelectParsing.FormatInvariant(new TimeOnly(14, 30, 45)));
    }

    [Fact]
    public void FormatInvariant_sub_second_DateTimeOffset_falls_back_to_the_full_round_trip_form()
    {
        // A sub-second value can't match any authored option anyway; "O" keeps the fraction so the
        // formatted string still parses back to the exact value instead of silently truncating.
        var value = new DateTimeOffset(2026, 6, 15, 14, 30, 45, 500, TimeSpan.FromHours(2));
        var formatted = SelectParsing.FormatInvariant(value);
        Assert.Equal("2026-06-15T14:30:45.5000000+02:00", formatted);
        Assert.True(SelectParsing.TryParseStringOrConvert<DateTimeOffset>(formatted, "When", out var back, out _));
        Assert.Equal(value, back);
    }

    [Fact]
    public void ParseEnum_parses_a_valid_member()
    {
        var ok = SelectParsing.TryParseEnum<Priority>("High", typeof(Priority), isNullable: false, "Priority", out var result, out _);
        Assert.True(ok);
        Assert.Equal(Priority.High, result);
    }

    [Fact]
    public void ParseEnum_empty_is_valid_null_for_a_nullable_enum()
    {
        var ok = SelectParsing.TryParseEnum<Priority?>("", typeof(Priority), isNullable: true, "Priority", out var result, out var err);
        Assert.True(ok);
        Assert.Null(result);
        Assert.Null(err);
    }

    [Fact]
    public void ParseEnum_empty_is_required_for_a_non_nullable_enum()
    {
        var ok = SelectParsing.TryParseEnum<Priority>("", typeof(Priority), isNullable: false, "Priority", out _, out var err);
        Assert.False(ok);
        Assert.Equal("The Priority field is required.", err);
    }

    [Fact]
    public void ParseEnum_rejects_an_unknown_member()
    {
        var ok = SelectParsing.TryParseEnum<Priority>("Nope", typeof(Priority), isNullable: false, "Priority", out _, out var err);
        Assert.False(ok);
        Assert.Equal("The Priority field is not valid.", err);
    }

    // Callback-bound controls must fail loudly if anyone binds CurrentValueAsString.
    class ProbeBool : Controls.EditBool { public bool Parse() => TryParseValueFromString("x", out _, out _); }
    class ProbeSelectSearch : Controls.EditSelectSearch<string> { public bool Parse() => TryParseValueFromString("x", out _, out _); }

    [Fact]
    public void EditBool_does_not_support_string_parsing()
        => Assert.Throws<NotSupportedException>(() => new ProbeBool().Parse());

    [Fact]
    public void EditSelectSearch_does_not_support_string_parsing()
        => Assert.Throws<NotSupportedException>(() => new ProbeSelectSearch().Parse());
}
