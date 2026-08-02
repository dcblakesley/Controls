namespace Controls.Helpers;

/// <summary>
/// The single definition of a <see cref="RangeAttribute"/> bound that means "no real bound" — the
/// property-type extreme an annotation carries only because <c>[Range]</c> demands BOTH bounds even
/// when just one is meaningful (<c>[Range(0, int.MaxValue)]</c>, <c>[Range(int.MinValue, 100)]</c>,
/// <c>[Range(0, double.MaxValue)]</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two layers consume this and MUST agree, or one form tells the user two different things about the
/// same annotation: <see cref="AttributesHelper"/> decides whether the bound renders as the input's
/// <c>min</c>/<c>max</c> attribute, and <see cref="ValidationHelper"/> decides whether the rewritten
/// range message names it. They used to keep a private list each and drifted apart on 8 of the 12
/// numeric extremes — <c>[Range(1, 255)]</c> on an <c>int</c> rendered <c>max="255"</c> while the
/// message said only "Must be at least 1", vacuous for an entry of 300 and silent about the ceiling
/// the user had just violated. Both layers now call the predicates below, so adding or removing an
/// extreme is one edit that moves both together.
/// </para>
/// <para>
/// Deliberately narrow. Neither layer knows the bound property's CLR type — the verdict rests on the
/// bound value/text alone — so an extreme qualifies only when that magnitude reads as "no bound" in
/// practice:
/// </para>
/// <list type="bullet">
/// <item><c>int</c>/<c>long</c>/<c>decimal</c>/<c>double</c>/<c>float</c> extremes are IN. They are
/// what <see cref="RangeAttribute"/>'s own constructors make natural to write (<c>(int,int)</c>,
/// <c>(double,double)</c>, and the <c>(Type,string,string)</c> spelling <c>long</c>/<c>decimal</c>
/// need), and no real-world bound has those magnitudes.</item>
/// <item><c>sbyte</c>/<c>byte</c>/<c>short</c>/<c>ushort</c>/<c>uint</c>/<c>ulong</c> extremes are
/// OUT. 127, −128, 255, 32767, 65535 and friends are overwhelmingly REAL bounds
/// (<c>[Range(1, 255)]</c> on an <c>int Quantity</c>), and suppressing one hides the constraint the
/// user needs to read. The cost is that a genuinely vacuous <c>[Range(0, 255)]</c> on a <c>byte</c>
/// names both bounds — a merely redundant message, where the reverse mistake is a wrong one.</item>
/// <item>Every unsigned type's minimum is <c>0</c>, which is a real floor everywhere it appears
/// (<c>[Range(0, 100)]</c>), so it is never a sentinel on either layer.</item>
/// </list>
/// <para>
/// The predicates are side-aware (min vs. max) on both layers: a type's <em>maximum</em> appearing as
/// a <em>minimum</em> is a real floor (<c>[Range(2147483647, 5000000000)]</c> on a <c>long</c>), not a
/// sentinel.
/// </para>
/// </remarks>
internal static class RangeSentinels
{
    // One row per type whose extremes count, so "do we cover this type" is a single decision instead
    // of two independently-edited min/max lists.
    //
    // Min/Max are the decimal forms the DOM-attribute layer compares against, null where no decimal
    // can hold the value (double's and float's extremes). Those never reach that layer anyway --
    // AttributesHelper drops any bound that won't convert to decimal before it asks about sentinels,
    // which is exactly what makes [Range(0, double.MaxValue)] render min="0" with no max.
    //
    // MinText/MaxText are FACTORIES, not cached strings, and are deliberately not cached anywhere:
    // RangeAttribute formats its message under the culture active at validation time, so the
    // candidates have to be produced under that same culture. A set frozen at first static touch
    // stopped matching the moment the culture diverged (de-DE writes "-1,79...E+308", sv-SE uses
    // U+2212 for the minus), silently degrading the one-sided rewrite; a per-culture-NAME cache still
    // returns wrong-culture hits for same-name cultures with customized number formats (CultureInfo
    // clones, Windows user-override vs GetCultureInfo instances). Only the message-rewrite path pays
    // for it, and only while a " must be between " message is actually being rewritten, where a
    // dozen short ToString calls are noise.
    static readonly (decimal? Min, decimal? Max, Func<string> MinText, Func<string> MaxText)[] Extremes =
    [
        (int.MinValue, int.MaxValue, () => int.MinValue.ToString(), () => int.MaxValue.ToString()),
        (long.MinValue, long.MaxValue, () => long.MinValue.ToString(), () => long.MaxValue.ToString()),
        (decimal.MinValue, decimal.MaxValue, () => decimal.MinValue.ToString(), () => decimal.MaxValue.ToString()),
        (null, null, () => double.MinValue.ToString(), () => double.MaxValue.ToString()),
        (null, null, () => float.MinValue.ToString(), () => float.MaxValue.ToString()),
        // RangeAttribute's numeric ctor only takes double bounds, so a float literal WIDENS before the
        // message is ever formatted: [Range(-100f, float.MaxValue)] emits Microsoft's textual form of
        // float.MaxValue-as-a-double, which differs from float.MaxValue.ToString() -- both spellings
        // have to be candidates.
        (null, null, () => ((double)float.MinValue).ToString(), () => ((double)float.MaxValue).ToString()),
    ];

    /// <summary> True when <paramref name="bound"/> is a type extreme spelling "no floor". </summary>
    internal static bool IsMin(decimal bound)
    {
        foreach (var extreme in Extremes)
            if (extreme.Min == bound) return true;
        return false;
    }

    /// <summary> True when <paramref name="bound"/> is a type extreme spelling "no ceiling". </summary>
    internal static bool IsMax(decimal bound)
    {
        foreach (var extreme in Extremes)
            if (extreme.Max == bound) return true;
        return false;
    }

    /// <summary>
    /// <see cref="IsMin(decimal)"/> over the bound as <see cref="RangeAttribute"/> spelled it in its
    /// message — the only form the message-rewrite layer ever has.
    /// </summary>
    internal static bool IsMin(string bound)
    {
        foreach (var extreme in Extremes)
            if (extreme.MinText() == bound) return true;
        return false;
    }

    /// <inheritdoc cref="IsMin(string)"/>
    internal static bool IsMax(string bound)
    {
        foreach (var extreme in Extremes)
            if (extreme.MaxText() == bound) return true;
        return false;
    }
}
