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
/// This is why every predicate below takes the property's own type: <see cref="IsMin(decimal, System.Type?)"/>/<see cref="IsMax(decimal, System.Type?)"/>
/// take the real CLR <see cref="System.Type"/> the DOM layer has from the generic
/// <c>EditNumber&lt;T&gt;</c>; the <c>string</c> overloads take the message layer's only type context —
/// <c>Type.ToString()</c> text, with <c>Nullable&lt;T&gt;</c>'s <c>"System.Nullable`1[...]"</c> spelling
/// unwrapped first. A <see langword="null"/>/unresolvable property type is conservative BY DESIGN — it
/// always answers "not a sentinel" (a real bound), never "collapse this to no-bound-at-all": a caller
/// with no type context cannot tell "no bound" from "long fits an int", so it must not guess.
/// </para>
/// <para>
/// <b>Unreachable is vacuous too.</b> An exact row match alone is not the whole gate, or the six
/// integral types with no row of their own (<c>sbyte</c>/<c>byte</c>/<c>short</c>/<c>ushort</c>/
/// <c>uint</c>/<c>ulong</c>) would match nothing and keep every bound: <c>[Range(0, int.MaxValue)]</c>
/// on a <c>short?</c> rendered <c>max="2147483647"</c> and "Must be between 0 and 2147483647" — a
/// ceiling the type cannot reach and a number the user can never satisfy their way past. So a row also
/// counts when its extreme sits AT OR OUTSIDE what the bound property can represent on that side
/// (<see cref="Representable"/>). Both halves are required: the second is still restricted to values
/// that are SOME type's extreme, which is what keeps <c>[Range(0, 255)]</c> on a <c>byte</c> naming
/// both bounds (255 is byte's own ceiling, but no row's) while <c>[Range(int.MinValue, int.MaxValue)]</c>
/// on a <c>long</c> stays the real "must fit in an int" constraint it is (int's extremes are rows, but
/// they sit strictly INSIDE long's range).
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

    // What each numeric type can actually hold, which is the second way a row above qualifies: an
    // extreme at or outside this range is a bound the property could never reach, i.e. "no bound" for
    // that property whether or not the type has an extreme row of its own. Held as double purely so
    // one table can span decimal's, float's and double's magnitudes as well as the integral ones --
    // only ROW extremes are ever compared against PROPERTY extremes here, and the two are equal only
    // when the types are (which the exact-row check has already answered), so double's precision at
    // long/ulong magnitudes never decides an answer. char is included because it is an integral type a
    // model can declare and reflection can hand us, not because it is a sensible [Range] target.
    static readonly (Type Type, string TypeName, double Min, double Max)[] Representable =
    [
        (typeof(sbyte), typeof(sbyte).ToString(), sbyte.MinValue, sbyte.MaxValue),
        (typeof(byte), typeof(byte).ToString(), byte.MinValue, byte.MaxValue),
        (typeof(short), typeof(short).ToString(), short.MinValue, short.MaxValue),
        (typeof(ushort), typeof(ushort).ToString(), ushort.MinValue, ushort.MaxValue),
        (typeof(char), typeof(char).ToString(), char.MinValue, char.MaxValue),
        (typeof(int), typeof(int).ToString(), int.MinValue, int.MaxValue),
        (typeof(uint), typeof(uint).ToString(), uint.MinValue, uint.MaxValue),
        (typeof(long), typeof(long).ToString(), long.MinValue, long.MaxValue),
        (typeof(ulong), typeof(ulong).ToString(), ulong.MinValue, ulong.MaxValue),
        (typeof(decimal), typeof(decimal).ToString(), (double)decimal.MinValue, (double)decimal.MaxValue),
        (typeof(float), typeof(float).ToString(), float.MinValue, float.MaxValue),
        (typeof(double), typeof(double).ToString(), double.MinValue, double.MaxValue),
    ];

    /// <summary>
    /// True when <paramref name="bound"/> spells "no floor" for <paramref name="propertyType"/>
    /// (<see cref="Nullable{T}"/> unwrapped first) — that type's own extreme, or another type's
    /// extreme at or below everything <paramref name="propertyType"/> can hold. <see langword="false"/>
    /// whenever <paramref name="propertyType"/> is <see langword="null"/> -- conservative by design,
    /// see the type-gating remarks above.
    /// </summary>
    internal static bool IsMin(decimal bound, Type? propertyType)
    {
        var type = Unwrap(propertyType);
        if (type is null) return false;
        var property = RangeOf(type);
        foreach (var extreme in Extremes)
            if (extreme.Min == bound && Qualifies(extreme.Type, type, property, isMin: true)) return true;
        return false;
    }

    /// <summary>
    /// True when <paramref name="bound"/> spells "no ceiling" for <paramref name="propertyType"/>.
    /// See <see cref="IsMin(decimal, System.Type?)"/> for the two ways a bound qualifies and for the
    /// null/unknown-type rule.
    /// </summary>
    internal static bool IsMax(decimal bound, Type? propertyType)
    {
        var type = Unwrap(propertyType);
        if (type is null) return false;
        var property = RangeOf(type);
        foreach (var extreme in Extremes)
            if (extreme.Max == bound && Qualifies(extreme.Type, type, property, isMin: false)) return true;
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
        var property = RangeOf(typeName);
        foreach (var extreme in Extremes)
            if (extreme.MinText() == bound && Qualifies(extreme.TypeName, typeName, property, isMin: true)) return true;
        return false;
    }

    /// <inheritdoc cref="IsMin(string, string?)"/>
    internal static bool IsMax(string bound, string? valueType)
    {
        var typeName = UnwrapTypeName(valueType);
        if (typeName is null) return false;
        var property = RangeOf(typeName);
        foreach (var extreme in Extremes)
            if (extreme.MaxText() == bound && Qualifies(extreme.TypeName, typeName, property, isMin: false)) return true;
        return false;
    }

    // The two ways a matching extreme row counts for this property: it is the property's own row, or
    // it lies at/outside what the property can represent on this side. Split by Type and by type name
    // only because the two layers have different type context; the rule is the same.
    static bool Qualifies(Type rowType, Type propertyType, (double Min, double Max)? property, bool isMin) =>
        rowType == propertyType || Unreachable(RangeOf(rowType), property, isMin);

    static bool Qualifies(string rowTypeName, string propertyTypeName, (double Min, double Max)? property, bool isMin) =>
        rowTypeName == propertyTypeName || Unreachable(RangeOf(rowTypeName), property, isMin);

    static bool Unreachable((double Min, double Max)? row, (double Min, double Max)? property, bool isMin) =>
        row is { } r && property is { } p && (isMin ? r.Min <= p.Min : r.Max >= p.Max);

    static (double Min, double Max)? RangeOf(Type type)
    {
        foreach (var row in Representable)
            if (row.Type == type) return (row.Min, row.Max);
        return null;
    }

    static (double Min, double Max)? RangeOf(string typeName)
    {
        foreach (var row in Representable)
            if (row.TypeName == typeName) return (row.Min, row.Max);
        return null;
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
