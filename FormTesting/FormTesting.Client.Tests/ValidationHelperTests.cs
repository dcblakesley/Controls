namespace FormTesting.Client.Tests;

public class ValidationHelperTests
{
    [Fact]
    public void Required_message_rewritten_to_short_form()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Name field is required.", "Name", "Full Name", valueType: "System.String");
        Assert.Equal("Required", msg);
    }

    [Fact]
    public void Required_message_with_includeLabel_includes_label()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Name field is required.", "Name", "Full Name", valueType: "System.String", includeLabel: true);
        Assert.Equal("Full Name is required.", msg);
    }

    [Fact]
    public void StringLength_with_min_and_max_rewritten_to_range()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Name must be a string with a minimum length of 2 and a maximum length of 100.",
            "Name", "Full Name", valueType: "System.String", max: 100, min: 2);
        Assert.Equal("Must be between 2 and 100 characters", msg);
    }

    [Fact]
    public void StringLength_with_only_max_rewritten_to_max_only()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Name must be a string with a maximum length of 100.",
            "Name", "Full Name", valueType: "System.String", max: 100);
        Assert.Equal("Cannot contain more than 100 characters", msg);
    }

    [Fact]
    public void Number_must_be_a_number_message_rewritten()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Age field must be a number.", "Age", "Age", valueType: "System.Int32");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_int_max_sentinel_renders_as_min_only()
    {
        // [Range(1, int.MaxValue)] — only the minimum is meaningful.
        // (Using 1 not 0 because "0" also matches byte.MinValue/uint.MinValue/etc., which would
        // make the minimum look like a sentinel too — pre-existing behavior of the helper.)
        var msg = ValidationHelper.GetValidationMessage(
            $"The field FloorValue must be between 1 and {int.MaxValue}.",
            "FloorValue", "Floor Value", valueType: "System.Int32");
        Assert.Equal("Must be at least 1", msg);
    }

    [Fact]
    public void Numeric_range_with_int_min_sentinel_renders_as_max_only()
    {
        // [Range(int.MinValue, 100)] — only the maximum is meaningful.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field CappedValue must be between {int.MinValue} and 100.",
            "CappedValue", "Capped Value", valueType: "System.Int32");
        Assert.Equal("Cannot exceed 100", msg);
    }

    [Fact]
    public void Numeric_range_with_both_concrete_bounds_renders_full_range()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Age must be between 1 and 120.",
            "Age", "Age", valueType: "System.Int32");
        Assert.Equal("Must be between 1 and 120", msg);
    }

    [Fact]
    public void Sentinel_detection_follows_a_runtime_culture_switch()
    {
        // RangeAttribute formats its message under the validation-time culture, so the sentinel
        // match must too. The old sets were frozen at first static touch: touch them under one
        // culture, switch to a culture with different numeric text (de-DE decimal comma, or a
        // different negative sign), and [Range(double.MinValue, x)] stopped rewriting one-sided.
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("en-US");
            // Prime under en-US (this is what froze the old cache).
            _ = ValidationHelper.GetValidationMessage(
                $"The field X must be between {double.MinValue} and 100.", "X", "X", valueType: "System.Double");

            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            var msg = ValidationHelper.GetValidationMessage(
                $"The field CappedValue must be between {double.MinValue} and 100.",
                "CappedValue", "Capped Value", valueType: "System.Double");
            Assert.Equal("Cannot exceed 100", msg);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Numeric_range_handles_multi_word_field_names()
    {
        // Regex-based parsing must tolerate field names with spaces — the old split-by-space
        // implementation crashed on these because parts[6]/parts[8] wouldn't line up.
        var msg = ValidationHelper.GetValidationMessage(
            "The field Order Total must be between 1 and 100.",
            "Order Total", "Order Total", valueType: "System.Int32");
        Assert.Equal("Must be between 1 and 100", msg);
    }

    [Fact]
    public void Unknown_message_passes_through_unchanged()
    {
        var unknown = "Some message we don't recognize.";
        var msg = ValidationHelper.GetValidationMessage(unknown, "Name", "Full Name", valueType: "System.String");
        Assert.Equal(unknown, msg);
    }

    [Fact]
    public void Numeric_range_with_includeLabel_prefixes_label()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Age must be between 1 and 120.",
            "Age", "Age", valueType: "System.Int32", includeLabel: true);
        Assert.Equal("Age must be between 1 and 120", msg);
    }
}
