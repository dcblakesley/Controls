using System.Globalization;

namespace FormTesting.Client.Tests;

/// <summary>
/// Unit tests for <see cref="ColorMath"/> — the pure parse/format/convert layer behind
/// <c>ColorPicker</c> and <c>EditColor</c>. No rendering involved; the component-level
/// behavior that sits on top of these is covered by <c>EditColorTests</c> (bUnit) and
/// <c>EditColorE2ETests</c> (the JS-driven drag paths).
/// </summary>
public class ColorMathTests
{
    // ----- Parsing: hex ------------------------------------------------------

    [Theory]
    [InlineData("#ff0000")]
    [InlineData("ff0000")]      // the leading # is optional
    [InlineData("#FF0000")]     // case-insensitive
    [InlineData("#f00")]        // 3-digit shorthand doubles each nibble
    [InlineData("f00")]
    [InlineData("  #ff0000  ")] // surrounding whitespace is trimmed
    public void Parses_every_spelling_of_opaque_red(string text)
    {
        Assert.True(ColorMath.TryParse(text, out var color));
        Assert.Equal(new ColorMath.Rgba(255, 0, 0, 1d), color);
    }

    [Fact]
    public void Parses_8_digit_hex_alpha_onto_the_0_to_1_scale()
    {
        Assert.True(ColorMath.TryParse("#ff000080", out var color));
        Assert.Equal(255, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(0, color.B);
        Assert.Equal(128 / 255d, color.A, 6);
    }

    [Fact]
    public void Parses_4_digit_hex_alpha_by_doubling_the_nibble()
    {
        // #f008 -> #ff000088
        Assert.True(ColorMath.TryParse("#f008", out var color));
        Assert.Equal(new ColorMath.Rgba(255, 0, 0, 136 / 255d), color);
    }

    [Fact]
    public void Parses_a_6_digit_hex_with_mixed_channels()
    {
        Assert.True(ColorMath.TryParse("#1a2b3c", out var color));
        Assert.Equal(0x1a, color.R);
        Assert.Equal(0x2b, color.G);
        Assert.Equal(0x3c, color.B);
        Assert.Equal(1d, color.A);
    }

    // ----- Parsing: rgb()/rgba() --------------------------------------------

    [Fact]
    public void Parses_rgb_function_form()
    {
        Assert.True(ColorMath.TryParse("rgb(18, 52, 86)", out var color));
        Assert.Equal(new ColorMath.Rgba(18, 52, 86, 1d), color);
    }

    [Fact]
    public void Parses_rgba_function_form_including_its_alpha()
    {
        Assert.True(ColorMath.TryParse("rgba(255, 0, 0, 0.5)", out var color));
        Assert.Equal(255, color.R);
        Assert.Equal(0.5d, color.A);
    }

    [Theory]
    [InlineData("RGB(18, 52, 86)")]         // case-insensitive function name
    [InlineData("rgb(18 52 86)")]           // CSS Color 4 space-separated
    [InlineData("rgb(18,52,86)")]           // no spaces
    [InlineData("rgba(18 52 86 / 1)")]      // slash-separated alpha
    public void Accepts_the_css_color_4_spellings_of_the_same_rgb(string text)
    {
        Assert.True(ColorMath.TryParse(text, out var color));
        Assert.Equal(18, color.R);
        Assert.Equal(52, color.G);
        Assert.Equal(86, color.B);
        Assert.Equal(1d, color.A);
    }

    [Fact]
    public void Accepts_a_percentage_alpha()
    {
        Assert.True(ColorMath.TryParse("rgba(0, 0, 0, 50%)", out var color));
        Assert.Equal(0.5d, color.A);
    }

    [Fact]
    public void Clamps_out_of_range_channels_and_alpha_rather_than_failing()
    {
        Assert.True(ColorMath.TryParse("rgba(300, -20, 40, 2.5)", out var color));
        Assert.Equal(255, color.R);
        Assert.Equal(0, color.G);
        Assert.Equal(40, color.B);
        Assert.Equal(1d, color.A);
    }

    // ----- Parsing: rejections ---------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nope")]
    [InlineData("#")]
    [InlineData("#ff")]          // 2 digits is not a hex color length
    [InlineData("#fffff")]       // 5 digits either
    [InlineData("#ff00gg")]      // g is not a hex digit
    [InlineData("#1234567")]     // 7 digits
    [InlineData("rgb(1, 2)")]    // too few components
    [InlineData("rgb(1, 2, 3, 4, 5)")]
    [InlineData("rgb(100%, 0%, 0%)")] // channel percentages are deliberately unsupported
    [InlineData("rgb(1, 2, x)")]
    [InlineData("rgb 1 2 3")]    // no parentheses
    [InlineData("rgb(1, 2, 3")]  // unclosed
    [InlineData("hsl(0, 100%, 50%)")]
    // NumberStyles.Float accepts all three of these spellings, and Math.Clamp propagates NaN instead of
    // clamping it -- so without an IsFinite guard these parsed as "true" with a NaN/infinite channel or
    // alpha, and the NaN then reached the swatch style, the handle offsets and the emitted hex.
    [InlineData("rgb(NaN, 0, 0)")]
    [InlineData("rgb(0, Infinity, 0)")]
    [InlineData("rgb(0, 0, -Infinity)")]
    [InlineData("rgba(255, 0, 0, NaN)")]
    [InlineData("rgba(255, 0, 0, Infinity)")]
    [InlineData("rgba(255, 0, 0, -Infinity)")]
    [InlineData("rgba(255, 0, 0, NaN%)")]
    public void Rejects_text_that_is_not_a_supported_color(string? text)
    {
        Assert.False(ColorMath.TryParse(text, out var color));
        Assert.Equal(default(ColorMath.Rgba), color);
    }

    // ----- Formatting -------------------------------------------------------

    [Fact]
    public void ToHex_emits_lowercase_6_digit_hex_for_an_opaque_color()
    {
        Assert.Equal("#ff00aa", ColorMath.ToHex(new ColorMath.Rgba(255, 0, 170, 1d), allowAlpha: true));
    }

    [Fact]
    public void ToHex_appends_the_alpha_pair_only_when_translucent_and_alpha_is_allowed()
    {
        var translucent = new ColorMath.Rgba(255, 0, 0, 0.5d);
        Assert.Equal("#ff000080", ColorMath.ToHex(translucent, allowAlpha: true));
        // Alpha disabled: the channel is dropped, which is what "no alpha" has to mean for the value.
        Assert.Equal("#ff0000", ColorMath.ToHex(translucent, allowAlpha: false));
    }

    [Fact]
    public void ToHex_round_trips_an_8_digit_value_through_TryParse()
    {
        Assert.True(ColorMath.TryParse("#12345678", out var color));
        Assert.Equal("#12345678", ColorMath.ToHex(color, allowAlpha: true));
    }

    [Fact]
    public void ToRgbString_switches_spelling_on_translucency_and_alpha_support()
    {
        var opaque = new ColorMath.Rgba(18, 52, 86, 1d);
        var translucent = new ColorMath.Rgba(18, 52, 86, 0.5d);
        Assert.Equal("rgb(18, 52, 86)", ColorMath.ToRgbString(opaque, allowAlpha: true));
        Assert.Equal("rgba(18, 52, 86, 0.5)", ColorMath.ToRgbString(translucent, allowAlpha: true));
        Assert.Equal("rgb(18, 52, 86)", ColorMath.ToRgbString(translucent, allowAlpha: false));
    }

    [Fact]
    public void ToRgbString_alpha_survives_a_round_trip_through_the_8_bit_hex_quantization()
    {
        // 0.5 -> 0x80 -> 0.50196... -> "0.5" again (2 decimals is enough to be stable).
        Assert.True(ColorMath.TryParse("#ff000080", out var color));
        Assert.Equal("rgba(255, 0, 0, 0.5)", ColorMath.ToRgbString(color, allowAlpha: true));
    }

    [Fact]
    public void Formatting_is_culture_invariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE uses ',' as the decimal separator -- rgba()'s alpha must stay '.'-separated.
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.Equal("rgba(1, 2, 3, 0.5)", ColorMath.ToRgbString(new ColorMath.Rgba(1, 2, 3, 0.5d), allowAlpha: true));
            Assert.Equal("#010203", ColorMath.ToHex(new ColorMath.Rgba(1, 2, 3, 1d), allowAlpha: true));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Parsing_is_culture_invariant()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.True(ColorMath.TryParse("rgba(1, 2, 3, 0.5)", out var color));
            Assert.Equal(0.5d, color.A);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----- HSV conversion ---------------------------------------------------

    [Theory]
    [InlineData(255, 0, 0, 0d)]      // red
    [InlineData(255, 255, 0, 60d)]   // yellow
    [InlineData(0, 255, 0, 120d)]    // green
    [InlineData(0, 255, 255, 180d)]  // cyan
    [InlineData(0, 0, 255, 240d)]    // blue
    [InlineData(255, 0, 255, 300d)]  // magenta
    public void ToHsv_maps_the_six_primary_hues(byte r, byte g, byte b, double hue)
    {
        var hsv = ColorMath.ToHsv(ColorMath.Rgba.Opaque(r, g, b));
        Assert.Equal(hue, hsv.H, 6);
        Assert.Equal(1d, hsv.S, 6);
        Assert.Equal(1d, hsv.V, 6);
    }

    [Fact]
    public void ToHsv_reports_black_as_zero_saturation_and_zero_value()
    {
        var hsv = ColorMath.ToHsv(ColorMath.Rgba.Opaque(0, 0, 0));
        Assert.Equal(0d, hsv.H);
        Assert.Equal(0d, hsv.S);
        Assert.Equal(0d, hsv.V);
    }

    [Fact]
    public void ToHsv_reports_white_as_zero_saturation_and_full_value()
    {
        var hsv = ColorMath.ToHsv(ColorMath.Rgba.Opaque(255, 255, 255));
        Assert.Equal(0d, hsv.H);
        Assert.Equal(0d, hsv.S);
        Assert.Equal(1d, hsv.V);
    }

    [Fact]
    public void ToHsv_reports_grey_as_achromatic_at_the_matching_value()
    {
        var hsv = ColorMath.ToHsv(ColorMath.Rgba.Opaque(128, 128, 128));
        Assert.Equal(0d, hsv.S);
        Assert.Equal(128 / 255d, hsv.V, 6);
    }

    [Fact]
    public void FromHsv_treats_hue_360_and_hue_0_as_the_same_color()
    {
        var atZero = ColorMath.FromHsv(new ColorMath.Hsv(0d, 1d, 1d), 1d);
        var atThreeSixty = ColorMath.FromHsv(new ColorMath.Hsv(360d, 1d, 1d), 1d);
        Assert.Equal(atZero, atThreeSixty);
        Assert.Equal(ColorMath.Rgba.Opaque(255, 0, 0), atZero);
    }

    [Fact]
    public void FromHsv_wraps_a_negative_hue()
    {
        Assert.Equal(
            ColorMath.FromHsv(new ColorMath.Hsv(300d, 1d, 1d), 1d),
            ColorMath.FromHsv(new ColorMath.Hsv(-60d, 1d, 1d), 1d));
    }

    [Fact]
    public void FromHsv_clamps_saturation_value_and_alpha()
    {
        var clamped = ColorMath.FromHsv(new ColorMath.Hsv(0d, 5d, 5d), 9d);
        Assert.Equal(ColorMath.Rgba.Opaque(255, 0, 0), clamped);
        var floored = ColorMath.FromHsv(new ColorMath.Hsv(0d, -1d, -1d), -1d);
        Assert.Equal(new ColorMath.Rgba(0, 0, 0, 0d), floored);
    }

    [Fact]
    public void FromHsv_at_zero_value_is_black_for_every_hue()
    {
        for (var h = 0; h < 360; h += 30)
        {
            Assert.Equal(ColorMath.Rgba.Opaque(0, 0, 0), ColorMath.FromHsv(new ColorMath.Hsv(h, 1d, 0d), 1d));
        }
    }

    [Fact]
    public void FromHsv_at_zero_saturation_is_grey_for_every_hue()
    {
        for (var h = 0; h < 360; h += 30)
        {
            Assert.Equal(ColorMath.Rgba.Opaque(255, 255, 255), ColorMath.FromHsv(new ColorMath.Hsv(h, 0d, 1d), 1d));
        }
    }

    [Theory]
    [InlineData("#ff0000")]
    [InlineData("#00ff00")]
    [InlineData("#0000ff")]
    [InlineData("#1a2b3c")]
    [InlineData("#808080")]
    [InlineData("#ffffff")]
    [InlineData("#000000")]
    [InlineData("#7f3ac1")]
    public void Rgb_to_hsv_and_back_round_trips_exactly(string hex)
    {
        Assert.True(ColorMath.TryParse(hex, out var original));
        var back = ColorMath.FromHsv(ColorMath.ToHsv(original), original.A);
        Assert.Equal(original, back);
        Assert.Equal(hex, ColorMath.ToHex(back, allowAlpha: true));
    }

    [Fact]
    public void Hsv_round_trip_preserves_alpha_untouched()
    {
        Assert.True(ColorMath.TryParse("#1a2b3c40", out var original));
        var back = ColorMath.FromHsv(ColorMath.ToHsv(original), original.A);
        Assert.Equal(original.A, back.A);
        Assert.Equal("#1a2b3c40", ColorMath.ToHex(back, allowAlpha: true));
    }

    [Fact]
    public void AlphaByte_quantizes_the_0_to_1_scale()
    {
        Assert.Equal(0, ColorMath.AlphaByte(0d));
        Assert.Equal(128, ColorMath.AlphaByte(0.5d));
        Assert.Equal(255, ColorMath.AlphaByte(1d));
        Assert.Equal(255, ColorMath.AlphaByte(4d));   // clamps
        Assert.Equal(0, ColorMath.AlphaByte(-4d));
    }
}
