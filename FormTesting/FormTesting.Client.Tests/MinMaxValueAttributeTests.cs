namespace FormTesting.Client.Tests;

/// <summary>
/// Direct coverage of <see cref="MinValueAttribute"/>/<see cref="MaxValueAttribute"/>'s <c>IsValid</c>
/// contract -- the <see cref="System.ComponentModel.DataAnnotations.ValidationAttribute"/> half of the
/// feature. <see cref="AttributesHelperTests"/> covers the other half: the MinNumber/MaxNumber/MinDate/
/// MaxDate extensions that resolve a rendered bound (including the [Range] fallback).
/// </summary>
public class MinMaxValueAttributeTests
{
    // Null / leniency

    [Fact]
    public void IsValid_returns_true_for_null_value()
    {
        // Null is [Required]'s job, not this attribute's -- same convention as RangeAttribute.
        Assert.True(new MinValueAttribute(5).IsValid(null));
        Assert.True(new MaxValueAttribute(5).IsValid(null));
    }

    [Fact]
    public void IsValid_is_lenient_when_a_text_bound_does_not_parse_as_a_number()
    {
        var attr = new MinValueAttribute("not-a-number");
        Assert.True(attr.IsValid(5));
    }

    [Fact]
    public void IsValid_is_lenient_when_a_date_text_bound_is_checked_against_a_numeric_value()
    {
        // "2024-01-01" is not a parseable number, so it can't constrain a numeric field -- degrade to
        // "no constraint" instead of throwing.
        var attr = new MinValueAttribute("2024-01-01");
        Assert.True(attr.IsValid(5));
    }

    [Fact]
    public void IsValid_is_lenient_for_an_unsupported_value_type()
    {
        var attr = new MinValueAttribute(5);
        Assert.True(attr.IsValid(new object()));
    }

    [Fact]
    public void IsValid_is_lenient_for_TimeOnly_when_the_bound_is_not_a_string()
    {
        // The int/double ctors are meaningless for a TimeOnly field -- only the string ctor applies.
        var attr = new MinValueAttribute(5);
        Assert.True(attr.IsValid(new TimeOnly(1, 0)));
    }

    // Numeric types -- boundary equality (bound itself is valid for both Min and Max), above, below

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, true)]
    [InlineData(4, false)]
    public void MinValue_int_boundary(int value, bool expectedValid)
    {
        Assert.Equal(expectedValid, new MinValueAttribute(5).IsValid(value));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(4, true)]
    [InlineData(6, false)]
    public void MaxValue_int_boundary(int value, bool expectedValid)
    {
        Assert.Equal(expectedValid, new MaxValueAttribute(5).IsValid(value));
    }

    [Fact]
    public void IsValid_handles_sbyte()
    {
        var min = new MinValueAttribute(5);
        Assert.True(min.IsValid((sbyte)5));
        Assert.True(min.IsValid((sbyte)6));
        Assert.False(min.IsValid((sbyte)4));
    }

    [Fact]
    public void IsValid_handles_byte()
    {
        var min = new MinValueAttribute(5);
        Assert.True(min.IsValid((byte)5));
        Assert.False(min.IsValid((byte)4));
    }

    [Fact]
    public void IsValid_handles_short()
    {
        var min = new MinValueAttribute(5);
        Assert.True(min.IsValid((short)5));
        Assert.False(min.IsValid((short)4));
    }

    [Fact]
    public void IsValid_handles_ushort()
    {
        var min = new MinValueAttribute(5);
        Assert.True(min.IsValid((ushort)5));
        Assert.False(min.IsValid((ushort)4));
    }

    [Fact]
    public void IsValid_handles_uint()
    {
        var max = new MaxValueAttribute(5);
        Assert.True(max.IsValid((uint)5));
        Assert.False(max.IsValid((uint)6));
    }

    [Fact]
    public void IsValid_handles_long()
    {
        var max = new MaxValueAttribute(5);
        Assert.True(max.IsValid(5L));
        Assert.False(max.IsValid(6L));
    }

    [Fact]
    public void IsValid_handles_ulong()
    {
        var max = new MaxValueAttribute(5);
        Assert.True(max.IsValid((ulong)5));
        Assert.False(max.IsValid((ulong)6));
    }

    [Fact]
    public void IsValid_handles_decimal()
    {
        var min = new MinValueAttribute(5);
        Assert.True(min.IsValid(5.0m));
        Assert.True(min.IsValid(5.01m));
        Assert.False(min.IsValid(4.99m));
    }

    [Fact]
    public void IsValid_handles_float()
    {
        var min = new MinValueAttribute(5.5);
        Assert.True(min.IsValid(5.5f));
        Assert.True(min.IsValid(6f));
        Assert.False(min.IsValid(5f));
    }

    [Fact]
    public void IsValid_handles_double()
    {
        var min = new MinValueAttribute(5.5);
        Assert.True(min.IsValid(5.5));
        Assert.True(min.IsValid(6.0));
        Assert.False(min.IsValid(5.0));

        var max = new MaxValueAttribute(5.5);
        Assert.True(max.IsValid(5.5));
        Assert.True(max.IsValid(5.0));
        Assert.False(max.IsValid(6.0));
    }

    [Fact]
    public void IsValid_returns_false_for_NaN_regardless_of_bound()
    {
        // NaN never satisfies a Min or a Max comparison -- unlike the rendering-resolution extensions,
        // the validator does not treat this as "unbounded".
        Assert.False(new MinValueAttribute(0).IsValid(double.NaN));
        Assert.False(new MaxValueAttribute(100).IsValid(double.NaN));
        Assert.False(new MinValueAttribute(0).IsValid(float.NaN));
    }

    [Fact]
    public void IsValid_compares_an_integral_value_against_an_unrepresentable_double_bound_in_double_space()
    {
        // double.MaxValue can't become a decimal, but IsValid must still validate correctly by falling
        // back to double-space comparison -- unlike AttributesHelper.MaxNumber, which treats this bound
        // as unbounded for RENDERING purposes only. The validator itself must not go silent here.
        var max = new MaxValueAttribute(double.MaxValue);
        Assert.True(max.IsValid(100));
        Assert.True(max.IsValid(int.MaxValue));

        var min = new MinValueAttribute(double.MinValue);
        Assert.True(min.IsValid(-100));
    }

    // Date/time types

    [Fact]
    public void IsValid_handles_DateTime()
    {
        var min = new MinValueAttribute("2024-06-15");
        Assert.True(min.IsValid(new DateTime(2024, 6, 15)));
        Assert.True(min.IsValid(new DateTime(2024, 6, 16)));
        Assert.False(min.IsValid(new DateTime(2024, 6, 14)));

        var max = new MaxValueAttribute("2024-06-15");
        Assert.True(max.IsValid(new DateTime(2024, 6, 15)));
        Assert.True(max.IsValid(new DateTime(2024, 6, 14)));
        Assert.False(max.IsValid(new DateTime(2024, 6, 16)));
    }

    [Fact]
    public void IsValid_handles_DateTimeOffset_by_comparing_its_DateTime_face_value()
    {
        // Matches how EditDate bridges DateTimeOffset <-> DateTime -- the offset itself is ignored.
        var min = new MinValueAttribute("2024-06-15");
        Assert.True(min.IsValid(new DateTimeOffset(2024, 6, 15, 0, 0, 0, TimeSpan.FromHours(5))));
        Assert.False(min.IsValid(new DateTimeOffset(2024, 6, 14, 0, 0, 0, TimeSpan.FromHours(-5))));
    }

    [Fact]
    public void IsValid_handles_DateOnly()
    {
        var min = new MinValueAttribute("2024-06-15");
        Assert.True(min.IsValid(new DateOnly(2024, 6, 15)));
        Assert.True(min.IsValid(new DateOnly(2024, 6, 16)));
        Assert.False(min.IsValid(new DateOnly(2024, 6, 14)));
    }

    [Fact]
    public void IsValid_handles_TimeOnly()
    {
        var min = new MinValueAttribute("13:30");
        Assert.True(min.IsValid(new TimeOnly(13, 30)));
        Assert.True(min.IsValid(new TimeOnly(14, 0)));
        Assert.False(min.IsValid(new TimeOnly(13, 0)));
    }

    // Value property + error message formatting

    [Fact]
    public void Value_property_exposes_the_constructed_bound()
    {
        Assert.Equal(5, new MinValueAttribute(5).Value);
        Assert.Equal(5.5, new MinValueAttribute(5.5).Value);
        Assert.Equal("2024-01-01", new MinValueAttribute("2024-01-01").Value);
    }

    [Fact]
    public void MinValue_default_error_message_includes_the_field_name_and_bound()
    {
        var attr = new MinValueAttribute(18);
        Assert.Equal("The Age field must be at least 18.", attr.FormatErrorMessage("Age"));
    }

    [Fact]
    public void MaxValue_default_error_message_includes_the_field_name_and_bound()
    {
        var attr = new MaxValueAttribute(65);
        Assert.Equal("The Age field must be no more than 65.", attr.FormatErrorMessage("Age"));
    }

    [Fact]
    public void Consumer_supplied_ErrorMessage_overrides_the_default_and_still_formats()
    {
        // Consumers override ErrorMessage exactly as with any other ValidationAttribute; a message with
        // no {0}/{1} placeholders is left untouched by the string.Format call.
        var attr = new MinValueAttribute(18) { ErrorMessage = "Must be an adult." };
        Assert.Equal("Must be an adult.", attr.FormatErrorMessage("Age"));
    }
}
