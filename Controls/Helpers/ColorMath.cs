namespace Controls.Helpers;

/// <summary>
/// The pure color math behind <c>ColorPicker</c> and <c>EditColor</c>: CSS color text
/// in (hex in 3/4/6/8-digit form, with or without the leading <c>#</c>, plus <c>rgb()</c>/<c>rgba()</c>),
/// normalized text out, and the HSV↔RGB conversions the picker's saturation/hue/alpha tracks are
/// expressed in.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately free of any component/rendering concern (and of any reflection, so the trim/AOT
/// analyzers have nothing to say about it) — the picker keeps its own <see cref="Hsv"/> session state
/// and calls in here for every conversion. That split matters because HSV→RGB is lossy in the
/// direction the picker needs to preserve: at <c>S = 0</c> or <c>V = 0</c> every hue produces the same
/// RGB, so a picker that re-derived its hue from the committed value on each render would snap the hue
/// slider back to red the moment the user dragged brightness to black. The component holds the hue;
/// this class only converts.
/// </para>
/// <para>
/// Alpha is carried as a <c>double</c> in 0..1 (the CSS/`rgba()` scale) rather than a byte, because
/// that is the scale both the alpha slider and <c>rgba()</c> text use; only <see cref="ToHex"/>
/// quantizes it to the 8-bit <c>#rrggbbaa</c> pair, and it round-trips through that quantization
/// (<c>0.5</c> → <c>80</c> → <c>0.5</c>).
/// </para>
/// </remarks>
public static class ColorMath
{
    /// <summary>An 8-bit-per-channel color with a 0..1 <paramref name="A"/> alpha.</summary>
    public readonly record struct Rgba(byte R, byte G, byte B, double A)
    {
        /// <summary>The same color at full opacity — the common case for a hex value with no alpha pair.</summary>
        public static Rgba Opaque(byte r, byte g, byte b) => new(r, g, b, 1d);
    }

    /// <summary>
    /// A hue/saturation/value triple: <paramref name="H"/> in degrees (0..360, wrapping),
    /// <paramref name="S"/> and <paramref name="V"/> in 0..1 — the axes the picker's 2D area and hue
    /// slider move along.
    /// </summary>
    public readonly record struct Hsv(double H, double S, double V);

    /// <summary>
    /// Parses CSS color text: <c>#rgb</c>, <c>#rgba</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c> (the
    /// <c>#</c> is optional), <c>rgb(r, g, b)</c>, and <c>rgba(r, g, b, a)</c> — the latter two also
    /// accepting whitespace or <c>/</c> in place of commas (CSS Color 4's space-separated form) and a
    /// percentage alpha. Channel percentages (<c>rgb(100%, 0%, 0%)</c>) are NOT supported and fail the
    /// parse. Out-of-range channels/alpha clamp rather than fail — including an infinite one, whether
    /// spelled <c>Infinity</c> or reached by an overflowing numeral like <c>1e400</c>, since clamping is
    /// exactly what "±∞ is out of range" means. <c>NaN</c> (which <see cref="NumberStyles.Float"/> itself
    /// accepts) is the single numeric rejection: there is no range end to clamp it to. Returns false for
    /// anything else, including null/whitespace — a color control treats that as "no color", never as an
    /// error.
    /// </summary>
    public static bool TryParse(string? text, out Rgba color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var trimmed = text.Trim();
        return trimmed.StartsWith("rgb", StringComparison.OrdinalIgnoreCase)
            ? TryParseRgbFunction(trimmed, out color)
            : TryParseHex(trimmed, out color);
    }

    /// <summary>
    /// The library's normalized output form: lowercase <c>#rrggbb</c>, extended to <c>#rrggbbaa</c>
    /// only when <paramref name="allowAlpha"/> is set AND the color is actually translucent. A picker
    /// with its alpha track turned off therefore emits pure 6-digit hex even if the value it was handed
    /// carried an alpha pair — dropping the channel, which is exactly what "no alpha" has to mean for a
    /// bound string.
    /// </summary>
    public static string ToHex(Rgba color, bool allowAlpha)
    {
        var alpha = AlphaByte(color.A);
        var rgb = string.Concat("#", Hex(color.R), Hex(color.G), Hex(color.B));
        return allowAlpha && alpha < 255 ? string.Concat(rgb, Hex(alpha)) : rgb;
    }

    /// <summary>
    /// The <c>rgb()</c>/<c>rgba()</c> form shown by the picker's RGB input row. Same
    /// <paramref name="allowAlpha"/> contract as <see cref="ToHex"/>: the <c>rgba()</c> spelling (and
    /// its fourth component, rounded to 2 decimals — enough to survive a round trip through
    /// <see cref="ToHex"/>'s 8-bit quantization) appears only for a translucent color with alpha enabled.
    /// </summary>
    public static string ToRgbString(Rgba color, bool allowAlpha)
    {
        var r = color.R.ToString(CultureInfo.InvariantCulture);
        var g = color.G.ToString(CultureInfo.InvariantCulture);
        var b = color.B.ToString(CultureInfo.InvariantCulture);
        if (!allowAlpha || AlphaByte(color.A) >= 255) return $"rgb({r}, {g}, {b})";
        var a = Math.Round(Math.Clamp(color.A, 0d, 1d), 2).ToString(CultureInfo.InvariantCulture);
        return $"rgba({r}, {g}, {b}, {a})";
    }

    /// <summary>
    /// The inline <c>background-color</c> declaration for one swatch fill — shared by
    /// <c>ColorPicker</c>'s trigger/preset swatches and <c>EditColor</c>'s read-only view, so the two
    /// can never paint the same color differently.
    /// </summary>
    public static string SwatchStyle(Rgba color, bool allowAlpha) =>
        $"background-color:{ToRgbString(color, allowAlpha)};";

    /// <summary>
    /// RGB → HSV. Hue is 0 for any achromatic color (black, white, every grey) — the information
    /// genuinely isn't there, which is why the picker keeps its own hue rather than round-tripping
    /// through here (see the class remarks).
    /// </summary>
    public static Hsv ToHsv(Rgba color)
    {
        var r = color.R / 255d;
        var g = color.G / 255d;
        var b = color.B / 255d;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        var h = 0d;
        if (delta > 0)
        {
            if (max == r) h = 60d * (((g - b) / delta) % 6d);
            else if (max == g) h = 60d * ((b - r) / delta + 2d);
            else h = 60d * ((r - g) / delta + 4d);
            if (h < 0) h += 360d;
        }

        return new Hsv(h, max <= 0 ? 0d : delta / max, max);
    }

    /// <summary>
    /// HSV → RGB at the given <paramref name="alpha"/>. Hue wraps (360 and 0 are the same color, and a
    /// negative hue is valid), saturation/value/alpha clamp — so a caller can step a track past its end
    /// without pre-clamping.
    /// </summary>
    public static Rgba FromHsv(Hsv hsv, double alpha)
    {
        var h = hsv.H % 360d;
        if (h < 0) h += 360d;
        var s = Math.Clamp(hsv.S, 0d, 1d);
        var v = Math.Clamp(hsv.V, 0d, 1d);

        var chroma = v * s;
        var sextant = h / 60d;
        var x = chroma * (1d - Math.Abs(sextant % 2d - 1d));
        var (r, g, b) = (int)Math.Floor(sextant) switch
        {
            0 => (chroma, x, 0d),
            1 => (x, chroma, 0d),
            2 => (0d, chroma, x),
            3 => (0d, x, chroma),
            4 => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        var m = v - chroma;
        return new Rgba(Channel(r + m), Channel(g + m), Channel(b + m), Math.Clamp(alpha, 0d, 1d));
    }

    /// <summary>The 0..255 quantization of a 0..1 alpha — the value <see cref="ToHex"/>'s trailing pair carries.</summary>
    public static byte AlphaByte(double alpha) => (byte)Math.Clamp(Math.Round(alpha * 255d), 0d, 255d);

    static byte Channel(double value) => (byte)Math.Clamp(Math.Round(value * 255d), 0d, 255d);

    static string Hex(byte value) => value.ToString("x2", CultureInfo.InvariantCulture);

    static bool TryParseHex(string text, out Rgba color)
    {
        color = default;
        var span = text.AsSpan();
        if (span.Length > 0 && span[0] == '#') span = span[1..];
        if (span.Length is not (3 or 4 or 6 or 8)) return false;
        foreach (var c in span)
        {
            if (!char.IsAsciiHexDigit(c)) return false;
        }

        if (span.Length is 3 or 4)
        {
            // Shorthand: each digit doubles (f -> ff), i.e. nibble * 17.
            var alpha = span.Length == 4 ? Nibble(span[3]) * 17 / 255d : 1d;
            color = new Rgba((byte)(Nibble(span[0]) * 17), (byte)(Nibble(span[1]) * 17), (byte)(Nibble(span[2]) * 17), alpha);
            return true;
        }

        var a = span.Length == 8 ? Pair(span[6], span[7]) / 255d : 1d;
        color = new Rgba(Pair(span[0], span[1]), Pair(span[2], span[3]), Pair(span[4], span[5]), a);
        return true;
    }

    static byte Pair(char high, char low) => (byte)(Nibble(high) * 16 + Nibble(low));

    static int Nibble(char c) => c <= '9' ? c - '0' : char.ToLowerInvariant(c) - 'a' + 10;

    static bool TryParseRgbFunction(string text, out Rgba color)
    {
        color = default;
        var open = text.IndexOf('(');
        if (open < 0 || text[^1] != ')') return false;
        var name = text[..open].TrimEnd();
        if (!name.Equals("rgb", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("rgba", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parts = text[(open + 1)..^1].Split([',', ' ', '\t', '/'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is not (3 or 4)) return false;
        if (!TryChannel(parts[0], out var r) || !TryChannel(parts[1], out var g) || !TryChannel(parts[2], out var b))
        {
            return false;
        }

        var alpha = 1d;
        if (parts.Length == 4 && !TryAlpha(parts[3], out alpha)) return false;
        color = new Rgba(r, g, b, alpha);
        return true;
    }

    // NumberStyles.Float also accepts "NaN"/"Infinity"/"-Infinity" (and the current culture's symbols
    // for them), and an ordinary-looking numeral that overflows double ("1e400") parses to an infinity
    // too. NaN is the only one of the three that has to fail: Math.Clamp PROPAGATES it rather than
    // clamping, and a NaN channel/alpha then poisons every consumer of the color -- an unrenderable
    // inline style, a `left: NaN%` handle, arrow keys that can never recover. Math.Clamp handles the
    // infinities correctly (+inf -> the range maximum, -inf -> the minimum), so they stay on the
    // documented out-of-range CLAMP path (see the class docs) rather than turning a finite-looking
    // "rgb(1e400, 0, 0)" into a parse failure.
    static bool TryChannel(string text, out byte value)
    {
        value = 0;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw) ||
            double.IsNaN(raw))
        {
            return false;
        }
        value = (byte)Math.Clamp(Math.Round(raw), 0d, 255d);
        return true;
    }

    static bool TryAlpha(string text, out double value)
    {
        value = 1d;
        var isPercent = text.EndsWith('%');
        var number = isPercent ? text[..^1] : text;
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var raw) ||
            double.IsNaN(raw))
        {
            return false;
        }
        value = Math.Clamp(isPercent ? raw / 100d : raw, 0d, 1d);
        return true;
    }
}
