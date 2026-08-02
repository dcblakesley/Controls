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
/// Deliberately narrow. An extreme qualifies only when that magnitude reads as "no bound" in practice:
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
/// <para>
/// <b>Type-gated, not just value-gated.</b> "Is this bound text equal to SOME type's extreme" is not
/// enough by itself — it must be the extreme for the BOUND PROPERTY'S OWN type (<see cref="Nullable{T}"/>
/// unwrapped). <c>[Range(int.MinValue, int.MaxValue)]</c> written on a <c>long</c> property is a
/// genuine "must fit in an int" constraint — <c>5000000000</c> violates it — not the "no bound" idiom
/// it is on an <c>int</c> property; a mixed pair like <c>(int.MinValue, long.MaxValue)</c> on a
/// <c>long</c> is one-sided (the max is long's own vacuous ceiling, the min is a real int-sized floor).
/// This is why every predicate below takes the property's own type and requires an EXACT row match
/// (not "any row"): <see cref="IsMin(decimal, System.Type?)"/>/<see cref="IsMax(decimal, System.Type?)"/>
/// take the real CLR <see cref="System.Type"/> the DOM layer has from the generic
/// <c>EditNumber&lt;T&gt;</c>; the <c>string</c> overloads take the message layer's only type context —
/// <c>Type.ToString()</c> text, with <c>Nullable&lt;T&gt;</c>'s <c>"System.Nullable`1[...]"</c> spelling
/// unwrapped first. A <see langword="null"/>/unresolvable property type is conservative BY DESIGN — it
/// always answers "not a sentinel" (a real bound), never "collapse this to no-bound-at-all": a caller
/// with no type context cannot tell "no bound" from "long fits an int", so it must not guess.
/// </para>
/// </remarks>
internal static class RangeSentinels
{
    // One row per type whose extremes count, so "do we cover this type" is a single decision instead
    // of two independently-edited min/max lists. Float gets two rows because RangeAttribute's numeric
    // ctor only takes double bounds, so a float literal widens before the message is ever formatted --
    // both spellings have to be recognized as "float's own" extreme.
    //
    // Min/Max are the decimal forms the DOM-attribute layer compares against, null where no decimal
    // can hold the value (double's and float's extremes). Those never reach that layer anyway --
    // AttributesHelper drops any bound that won't convert to decimal before it asks about sentinels,
    // which is exactly what makes [Range(0, double.MaxValue)] render min="0" with no max.
    //
    // TypeName is Type.ToString() computed once -- the exact spelling FieldValidationDisplay's
    // reflection-derived _valueType carries (System.Int32, System.Int64, ...), so the string-based
    // overloads below can gate on it without re-deriving it per call.
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
    static readonly (Type Type, string TypeName, decimal? Min, decimal? Max, Func<string> MinText, Func<string> MaxText)[] Extremes =
    [
        (typeof(int), typeof(int).ToString(), int.MinValue, int.MaxValue, () => int.MinValue.ToString(), () => int.MaxValue.ToString()),
        (typeof(long), typeof(long).ToString(), long.MinValue, long.MaxValue, () => long.MinValue.ToString(), () => long.MaxValue.ToString()),
        (typeof(decimal), typeof(decimal).ToString(), decimal.MinValue, decimal.MaxValue, () => decimal.MinValue.ToString(), () => decimal.MaxValue.ToString()),
        (typeof(double), typeof(double).ToString(), null, null, () => double.MinValue.ToString(), () => double.MaxValue.ToString()),
        (typeof(float), typeof(float).ToString(), null, null, () => float.MinValue.ToString(), () => float.MaxValue.ToString()),
        // RangeAttribute's numeric ctor only takes double bounds, so a float literal WIDENS before the
        // message is ever formatted: [Range(-100f, float.MaxValue)] emits Microsoft's textual form of
        // float.MaxValue-as-a-double, which differs from float.MaxValue.ToString() -- both spellings
        // have to be candidates, both tagged as float's own.
        (typeof(float), typeof(float).ToString(), null, null, () => ((double)float.MinValue).ToString(), () => ((double)float.MaxValue).ToString()),
    ];

    /// <summary>
    /// True when <paramref name="bound"/> is <paramref name="propertyType"/>'s OWN extreme spelling
    /// "no floor" (<see cref="Nullable{T}"/> unwrapped first). <see langword="false"/> whenever
    /// <paramref name="propertyType"/> is <see langword="null"/> -- conservative by design, see the
    /// type-gating remarks above.
    /// </summary>
    internal static bool IsMin(decimal bound, Type? propertyType)
    {
        var type = Unwrap(propertyType);
        if (type is null) return false;
        foreach (var extreme in Extremes)
            if (extreme.Type == type && extreme.Min == bound) return true;
        return false;
    }

    /// <summary>
    /// True when <paramref name="bound"/> is <paramref name="propertyType"/>'s OWN extreme spelling
    /// "no ceiling". See <see cref="IsMin(decimal, System.Type?)"/> for the null/unknown-type rule.
    /// </summary>
    internal static bool IsMax(decimal bound, Type? propertyType)
    {
        var type = Unwrap(propertyType);
        if (type is null) return false;
        foreach (var extreme in Extremes)
            if (extreme.Type == type && extreme.Max == bound) return true;
        return false;
    }

    /// <summary>
    /// <see cref="IsMin(decimal, System.Type?)"/> over the bound as <see cref="RangeAttribute"/>
    /// spelled it in its message — the only form the message-rewrite layer ever has — gated on
    /// <paramref name="valueType"/>, the bound property's own CLR type as <c>Type.ToString()</c> text
    /// (<c>"System.Nullable`1[...]"</c> unwrapped first). Null/empty/unrecognized is conservative —
    /// always "not a sentinel" — same rule as the decimal overload.
    /// </summary>
    internal static bool IsMin(string bound, string? valueType)
    {
        var typeName = UnwrapTypeName(valueType);
        if (typeName is null) return false;
        foreach (var extreme in Extremes)
            if (extreme.TypeName == typeName && extreme.MinText() == bound) return true;
        return false;
    }

    /// <inheritdoc cref="IsMin(string, string?)"/>
    internal static bool IsMax(string bound, string? valueType)
    {
        var typeName = UnwrapTypeName(valueType);
        if (typeName is null) return false;
        foreach (var extreme in Extremes)
            if (extreme.TypeName == typeName && extreme.MaxText() == bound) return true;
        return false;
    }

    static Type? Unwrap(Type? type) => type is null ? null : Nullable.GetUnderlyingType(type) ?? type;

    /// <summary>
    /// Strips <c>Nullable&lt;T&gt;</c>'s <c>Type.ToString()</c> wrapper (<c>"System.Nullable`1[System.Int32]"</c>
    /// → <c>"System.Int32"</c>) so a nullable-typed property gates on the same row as its non-nullable
    /// form. Null/empty in, null out -- the caller treats that as "type unknown", not "type is the
    /// empty string".
    /// </summary>
    static string? UnwrapTypeName(string? valueType)
    {
        if (string.IsNullOrEmpty(valueType))
            return null;
        const string prefix = "System.Nullable`1[";
        return valueType.StartsWith(prefix, StringComparison.Ordinal) && valueType.EndsWith(']')
            ? valueType[prefix.Length..^1]
            : valueType;
    }
}
