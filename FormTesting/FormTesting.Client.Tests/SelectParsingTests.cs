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

    // Callback-bound controls must fail loudly if anyone binds CurrentValueAsString. Field is set only
    // to satisfy the required member; the parser throws before it is ever read.
    class ProbeBool : Controls.EditBool { public bool Parse() => TryParseValueFromString("x", out _, out _); }
    class ProbeSelectSearch : Controls.EditSelectSearch<string> { public bool Parse() => TryParseValueFromString("x", out _, out _); }

    [Fact]
    public void EditBool_does_not_support_string_parsing()
        => Assert.Throws<NotSupportedException>(() => new ProbeBool { Field = () => true }.Parse());

    [Fact]
    public void EditSelectSearch_does_not_support_string_parsing()
        => Assert.Throws<NotSupportedException>(() => new ProbeSelectSearch { Field = () => "" }.Parse());
}
