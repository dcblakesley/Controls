using System.ComponentModel.DataAnnotations;
using System.Globalization;

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
    public void Date_must_be_a_date_message_rewritten()
    {
        // EditDate/EditDateNative/EditDateRange's ParsingErrorMessage default, same shape as the
        // numeric parse message.
        var msg = ValidationHelper.GetValidationMessage(
            "The BirthDate field must be a date.", "BirthDate", "Birth Date", valueType: "System.DateTime");
        Assert.Equal("Must be a date", msg);
    }

    [Fact]
    public void Date_must_be_a_date_message_rewritten_with_label()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The BirthDate field must be a date.", "BirthDate", "Birth Date", valueType: "System.DateTime",
            includeLabel: true);
        Assert.Equal("Birth Date must be a date.", msg);
    }

    [Fact]
    public void Numeric_range_with_int_max_sentinel_renders_as_min_only()
    {
        // [Range(1, int.MaxValue)] — only the minimum is meaningful.
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

    // ----- Both bounds sentinel: nothing left to name -------------------------------------------
    // [Range(int.MinValue, int.MaxValue)] is written purely to trigger numeric parsing validation --
    // neither bound is a real constraint. Before this fix, no branch matched a both-sentinel pair (the
    // one-sided branches each require exactly one side to be a sentinel) and `return message` handed
    // back the raw framework text with both extremes spelled out verbatim -- exactly what the whole
    // rewrite exists to suppress. The fix falls back to the same "Must be a number" wording the
    // parse-failure path already uses, since a fully-unbounded [Range] carries no information beyond
    // "this must parse as a number".

    [Fact]
    public void Numeric_range_with_int_both_sentinels_falls_back_to_must_be_a_number()
    {
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {int.MinValue} and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int32");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_long_both_sentinels_falls_back_to_must_be_a_number()
    {
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {long.MinValue} and {long.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int64");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_decimal_both_sentinels_falls_back_to_must_be_a_number()
    {
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Balance must be between {decimal.MinValue} and {decimal.MaxValue}.",
            "Balance", "Balance", valueType: "System.Decimal");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_double_both_sentinels_falls_back_to_must_be_a_number()
    {
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Reading must be between {double.MinValue} and {double.MaxValue}.",
            "Reading", "Reading", valueType: "System.Double");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_float_both_sentinels_falls_back_to_must_be_a_number()
    {
        // RangeAttribute's numeric ctor only takes double bounds, so a float literal widens before the
        // message is formatted -- mirrors the float sentinel candidates' own widened spelling.
        var minText = ((double)float.MinValue).ToString();
        var maxText = ((double)float.MaxValue).ToString();
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Reading must be between {minText} and {maxText}.",
            "Reading", "Reading", valueType: "System.Single");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_both_sentinels_and_includeLabel_uses_the_labeled_wording()
    {
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {int.MinValue} and {int.MaxValue}.",
            "Quantity", "Item Quantity", valueType: "System.Int32", includeLabel: true);
        Assert.Equal("Item Quantity must be a number.", msg);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Numeric_range_with_both_sentinels_is_detected_under_the_validation_time_culture(string cultureName)
    {
        // Same culture hazard as the one-sided sentinel detection: the candidates must be produced
        // under the culture active at validation time, not frozen as invariant-format literals.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);

            var msg = ValidationHelper.GetValidationMessage(
                $"The field Reading must be between {double.MinValue} and {double.MaxValue}.",
                "Reading", "Reading", valueType: "System.Double");
            Assert.Equal("Must be a number", msg);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----- Both-sentinel collapse is TYPE-GATED, not just value-gated ---------------------------
    // [Range(int.MinValue, int.MaxValue)] is the vacuous "trigger numeric parsing only" idiom on an
    // int -- but on a LONG it's a genuine "must fit in an int" constraint (5000000000 violates it).
    // Before this fix, RangeSentinels.IsMin/IsMax matched ANY extreme in the table regardless of the
    // bound property's own type, so a long field with this attribute wrongly rendered "Must be a
    // number" for an out-of-int-range value that IS a number. The collapse (and the one-sided
    // rewrite) must only fire when the bound is vacuous for the field actually being validated.

    [Fact]
    public void Numeric_range_with_int_both_sentinels_collapses_on_a_nullable_int_property_too()
    {
        // Nullable<T>'s Type.ToString() spelling ("System.Nullable`1[System.Int32]") must unwrap to
        // the same row as "System.Int32" -- an int? field is still int's own type.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {int.MinValue} and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Nullable`1[System.Int32]");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Numeric_range_with_int_both_sentinels_on_a_long_property_is_a_real_bounded_range()
    {
        // Same attribute text as the int case above, but the field is long -- int.MinValue/int.MaxValue
        // are NOT long's own extremes, so this is a genuine (if generous) bounded range, not "no bound
        // at all". Collapsing this to "Must be a number" would hide a real constraint that
        // 5000000000 actually violates.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {int.MinValue} and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int64");
        Assert.Equal($"Must be between {int.MinValue} and {int.MaxValue}", msg);
    }

    [Fact]
    public void Mixed_int_min_long_max_on_a_long_property_is_one_sided_on_the_real_int_floor()
    {
        // The max IS long's own sentinel (vacuous, long.MaxValue); the min is int.MinValue, a real
        // (if unusual) floor for a long field -- classified per-side, exactly like the pre-existing
        // one-sided int/byte/short tests above, just with the roles of "which side is real" flipped by
        // the field's own wider type instead of a narrower literal.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {int.MinValue} and {long.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int64");
        Assert.Equal($"Must be at least {int.MinValue}", msg);
    }

    [Fact]
    public void Numeric_range_with_long_both_sentinels_still_collapses_on_a_long_property()
    {
        // Confirms the type-gated rewrite still recognizes long's OWN extremes as vacuous on a long
        // field -- the fix narrows "which extremes count", it doesn't stop long from ever collapsing.
        // (Mirrors Numeric_range_with_long_both_sentinels_falls_back_to_must_be_a_number above; kept
        // as an explicit type-gating case for this section's own record.)
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between {long.MinValue} and {long.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int64");
        Assert.Equal("Must be a number", msg);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Int_extremes_on_a_wider_long_property_are_a_real_bounded_range_under_any_culture(string cultureName)
    {
        // Same culture hazard as the sentinel-detection theories above: int.MinValue/int.MaxValue's
        // text (and NumberRangeString's rendering of it) must be produced under the culture active at
        // validation time, and the type-gated verdict (a real bound here, not a sentinel) must hold
        // regardless of which culture spells the negative sign.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var minText = int.MinValue.ToString();
            var maxText = int.MaxValue.ToString();

            var msg = ValidationHelper.GetValidationMessage(
                $"The field Quantity must be between {minText} and {maxText}.",
                "Quantity", "Quantity", valueType: "System.Int64");

            Assert.Equal($"Must be between {minText} and {maxText}", msg);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // ----- Zero-as-a-real-floor (not a sentinel) ------------------------------------------------
    // byte/uint/ulong/ushort.MinValue.ToString() are ALL "0" — IsTypeMinSentinel used to treat "0"
    // itself as a type-min sentinel, so the ubiquitous [Range(0, ...)] "non-negative" idiom lost its
    // real floor. AttributesHelper.IsRangeSentinel already excluded 0 for DOM min/max rendering; the
    // message rewrite must agree with it.

    [Fact]
    public void Range_zero_min_with_int_max_sentinel_renders_as_min_only_at_least_zero()
    {
        // [Range(0, int.MaxValue)] — the ubiquitous "non-negative" idiom. Before the fix, "0" was
        // misread as a min sentinel too, so BOTH bounds looked like sentinels and neither one-sided
        // branch matched — the raw framework text (with 2147483647 verbatim) reached the user.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between 0 and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Int32");
        Assert.Equal("Must be at least 0", msg);
    }

    [Fact]
    public void Range_zero_min_with_a_concrete_max_renders_both_bounds()
    {
        // [Range(0, 100)] — "0" must NOT be read as a sentinel here, or the real floor is lost and
        // the message silently degrades to a max-only "Cannot exceed 100".
        var msg = ValidationHelper.GetValidationMessage(
            "The field Percent must be between 0 and 100.",
            "Percent", "Percent", valueType: "System.Int32");
        Assert.Equal("Must be between 0 and 100", msg);
    }

    [Fact]
    public void Range_with_decimal_min_sentinel_still_renders_max_only()
    {
        // decimal.MinValue is NOT "0" — unlike byte/uint/ulong/ushort.MinValue, it must keep being
        // treated as a sentinel after the zero-collision exclusion above.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Balance must be between {decimal.MinValue} and 100.",
            "Balance", "Balance", valueType: "System.Decimal");
        Assert.Equal("Cannot exceed 100", msg);
    }

    [Fact]
    public void Numeric_range_with_float_max_sentinel_renders_as_min_only()
    {
        // [Range(-100f, float.MaxValue)] — RangeAttribute's ctor only takes double bounds, so the
        // float literal widens to a double whose ToString() is Microsoft's textual form of
        // float.MaxValue-as-a-double, not float.MaxValue.ToString() itself (mirrors the float.MinValue
        // candidate — see the sentinel set's own remarks). Before the fix, only the min side of this
        // pair rewrote correctly; the max side showed the raw huge number.
        var maxText = ((double)float.MaxValue).ToString();
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Reading must be between -100 and {maxText}.",
            "Reading", "Reading", valueType: "System.Single");
        Assert.Equal("Must be at least -100", msg);
    }

    // ----- The narrow-integer extremes are REAL bounds, not sentinels ---------------------------
    // The message layer used to treat short/sbyte/byte/ushort/uint/ulong extremes as "no bound",
    // which the DOM-attribute layer never did — so the rendered min/max and the message disagreed on
    // 8 of the 12 numeric extremes. The predicate does see the bound property's CLR type, but these
    // magnitudes carry no row in the sentinel table under ANY property type: at 127/255/32767 a real
    // bound is far more likely than a vacuous one, so no bound of that magnitude is ever suppressed —
    // not even on the type it belongs to (the byte/255 case below). What the property type decides is
    // the other direction: one of the table's OWN extremes (int/long/decimal/float/double) is vacuous
    // for any property that cannot reach it — int.MaxValue on a short. See the narrow-type section
    // further down.

    [Fact]
    public void Range_with_a_byte_MaxValue_ceiling_names_both_bounds()
    {
        // [Range(1, 255)] on an int Quantity renders min="1" max="255"; the message used to say only
        // "Must be at least 1" — vacuous for an entry of 300 and silent about the ceiling just
        // violated.
        var msg = ValidationHelper.GetValidationMessage(
            "The field Quantity must be between 1 and 255.",
            "Quantity", "Quantity", valueType: "System.Int32");
        Assert.Equal("Must be between 1 and 255", msg);
    }

    [Fact]
    public void Range_with_a_short_MinValue_floor_names_both_bounds()
    {
        // [Range(-32768, 100)] renders min="-32768"; the message used to claim there was no floor.
        var msg = ValidationHelper.GetValidationMessage(
            "The field Offset must be between -32768 and 100.",
            "Offset", "Offset", valueType: "System.Int32");
        Assert.Equal("Must be between -32768 and 100", msg);
    }

    [Fact]
    public void Range_spanning_a_byte_type_in_full_names_both_bounds()
    {
        // [Range(0, 255)] on a byte IS vacuous — but the predicate can't know that (it sees "255",
        // not the property type), and naming both bounds is merely redundant where suppressing a real
        // 255 ceiling is wrong. The DOM layer already renders both here, so this is what agreement
        // costs.
        var msg = ValidationHelper.GetValidationMessage(
            "The field Level must be between 0 and 255.",
            "Level", "Level", valueType: "System.Byte");
        Assert.Equal("Must be between 0 and 255", msg);
    }

    // ----- A bound the property type cannot REACH is vacuous, whoever's extreme it is ------------
    // The other half of type-gating, and the half an exact-row-only gate loses: short/sbyte/byte/
    // ushort/uint/ulong have no extreme row of their own, so demanding an exact row match made every
    // sentinel invisible to them and [Range(0, int.MaxValue)] on a short? started saying "Must be
    // between 0 and 2147483647" (and rendering max="2147483647") -- a ceiling the type cannot even
    // represent, so the message named a limit no entry could ever be measured against. A row therefore
    // also counts when its extreme sits at or outside what the bound property can hold. Both halves
    // stay necessary: only the five table extremes can be suppressed this way (so 255 on a byte is
    // still named, above), and int's extremes on a LONG are strictly inside long's range, so they
    // remain the real "must fit in an int" constraint they are.

    [Theory]
    [InlineData("System.Int16")]
    [InlineData("System.Byte")]
    [InlineData("System.Nullable`1[System.Int16]")]
    public void Range_max_a_narrow_type_cannot_reach_renders_as_min_only(string valueType)
    {
        // [Range(0, int.MaxValue)] -- the "non-negative integer" idiom -- written on a short/byte.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between 0 and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: valueType);
        Assert.Equal("Must be at least 0", msg);
    }

    [Fact]
    public void Range_min_a_narrow_type_cannot_reach_renders_as_max_only()
    {
        // [Range(int.MinValue, 100)] on a short: no short is below int.MinValue, so the floor is the
        // "[Range] demands BOTH bounds" spelling and only the cap is a real constraint.
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Offset must be between {int.MinValue.ToString()} and 100.",
            "Offset", "Offset", valueType: "System.Int16");
        Assert.Equal("Cannot exceed 100", msg);
    }

    [Fact]
    public void Range_max_a_wide_type_can_reach_stays_a_real_bound()
    {
        // The same annotation on a long?, which CAN hold values above int.MaxValue: the ceiling is a
        // genuine "must fit in an int" bound and the unreachability rule above must not swallow it.
        // (Nullable spelling on purpose -- the reflected property type is what the rule gates on.)
        var msg = ValidationHelper.GetValidationMessage(
            $"The field Quantity must be between 0 and {int.MaxValue}.",
            "Quantity", "Quantity", valueType: "System.Nullable`1[System.Int64]");
        Assert.Equal($"Must be between 0 and {int.MaxValue}", msg);
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void A_bound_a_narrow_type_cannot_reach_is_detected_under_the_validation_time_culture(string cultureName)
    {
        // Same culture hazard as every other sentinel theory here: the candidate text is produced under
        // the culture active at validation time (de-DE spells int.MinValue's sign differently), and the
        // unreachability verdict must not depend on which culture spelled the bound.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var minText = int.MinValue.ToString();

            var msg = ValidationHelper.GetValidationMessage(
                $"The field Offset must be between {minText} and 100.",
                "Offset", "Offset", valueType: "System.Int16");

            Assert.Equal("Cannot exceed 100", msg);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    // Every numeric type extreme, each paired with a concrete bound on the other side (a
    // both-sentinel [Range] is a fully-unbounded annotation with nothing to rewrite). The message
    // layer and the DOM-attribute layer must reach the SAME verdict on each: a bound the message
    // presents as absent can't show up in the DOM as min="-32768", and vice versa. Both sides are
    // given the SAME bound-property type (decimal, matching the RangeAttribute's own OperandType
    // below) -- that's what makes "agree" meaningful post-type-gating: an extreme that belongs to some
    // OTHER type (e.g. int.MinValue here, where the field is decimal) is a real bound on both layers,
    // not a coincidental shared blind spot.
    //
    // double/float's own extremes are deliberately absent from this decimal-typed set: their magnitude
    // (~1.8E308 / ~3.4E38) exceeds decimal's own range (~7.9E28), so a decimal-typed [Range] literally
    // cannot carry one -- decimal.TryParse rejects the text before either layer ever asks
    // RangeSentinels a type-gating question, and a real RangeAttribute(typeof(decimal), thatText, ...)
    // would itself misbehave at validation time. Pairing them here would test two layers separately
    // (and coincidentally) failing to parse an ill-formed bound, not whether their SENTINEL verdicts
    // agree -- that pairing already has its own coverage in the both-sentinel section above (one
    // fact per type, matched to its own type).
    public static TheoryData<string, string> TypeExtremeRangeBounds()
    {
        var inv = CultureInfo.InvariantCulture;
        var data = new TheoryData<string, string>();
        foreach (var min in new[]
        {
            sbyte.MinValue.ToString(inv), short.MinValue.ToString(inv), int.MinValue.ToString(inv),
            long.MinValue.ToString(inv), decimal.MinValue.ToString(inv),
        })
            data.Add(min, "100");

        foreach (var max in new[]
        {
            sbyte.MaxValue.ToString(inv), byte.MaxValue.ToString(inv), short.MaxValue.ToString(inv),
            ushort.MaxValue.ToString(inv), int.MaxValue.ToString(inv), uint.MaxValue.ToString(inv),
            long.MaxValue.ToString(inv), ulong.MaxValue.ToString(inv), decimal.MaxValue.ToString(inv),
        })
            data.Add("1", max);

        return data;
    }

    [Theory]
    [MemberData(nameof(TypeExtremeRangeBounds))]
    public void Rendered_bounds_and_message_bounds_agree_on_every_numeric_type_extreme(string minText, string maxText)
    {
        // Invariant culture throughout so the [Range] limit text, the framework message text and the
        // sentinel candidates (produced under CurrentCulture) are all the same spelling.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var attrs = new List<Attribute>
            {
                new RangeAttribute(typeof(decimal), minText, maxText) { ParseLimitsInInvariantCulture = true },
            };

            var msg = ValidationHelper.GetValidationMessage(
                $"The field Value must be between {minText} and {maxText}.",
                "Value", "Value", valueType: "System.Decimal");

            Assert.Equal(attrs.MinNumber(typeof(decimal)) is not null, msg.Contains(minText, StringComparison.Ordinal));
            Assert.Equal(attrs.MaxNumber(typeof(decimal)) is not null, msg.Contains(maxText, StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    public void Float_extreme_sentinels_are_detected_under_the_validation_time_culture(string cultureName)
    {
        // The float extremes have to be produced under the culture active at validation time, exactly
        // like every other candidate: RangeAttribute formats its message then, and de-DE writes
        // "3,4028234663852886E+38". Freezing the invariant-format literal meant no branch matched
        // outside a '.'-decimal culture and the raw scientific-notation text -- the very thing the
        // rewrite exists to suppress -- reached the user. Both the max side ([Range(-100f,
        // float.MaxValue)]) and its min-side mirror ([Range(float.MinValue, 100f)]) are pinned.
        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(cultureName);

            var maxText = ((double)float.MaxValue).ToString();
            var flooredOnly = ValidationHelper.GetValidationMessage(
                $"The field Reading must be between -100 and {maxText}.",
                "Reading", "Reading", valueType: "System.Single");
            Assert.Equal("Must be at least -100", flooredOnly);

            var minText = ((double)float.MinValue).ToString();
            var cappedOnly = ValidationHelper.GetValidationMessage(
                $"The field Reading must be between {minText} and 100.",
                "Reading", "Reading", valueType: "System.Single");
            Assert.Equal("Cannot exceed 100", cappedOnly);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    // ----- MaxLengthList punctuation -------------------------------------------------------------

    [Fact]
    public void MaxLength_list_message_rewritten_without_a_trailing_period()
    {
        // Every neighboring message (MinLengthList, MaxLengthString, etc.) omits the trailing
        // period; the unlabeled MaxLengthList wording used to be the one outlier.
        var msg = ValidationHelper.GetValidationMessage(
            "The field Tags must be a string or array type with a maximum length of '5'.",
            "Tags", "Tags", valueType: "System.Collections.Generic.List`1[System.String]", max: 5);
        Assert.Equal("Cannot exceed 5 selections", msg);
    }

    [Fact]
    public void MaxLength_list_message_with_label_matches_the_unlabeled_wording()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Tags must be a string or array type with a maximum length of '5'.",
            "Tags", "Chosen Tags", valueType: "System.Collections.Generic.List`1[System.String]", max: 5,
            includeLabel: true);
        Assert.Equal("Chosen Tags cannot exceed 5 selections", msg);
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

    // ----- [Display(Name)] candidate ------------------------------------------------------------
    // DataAnnotations formats its messages with ValidationContext.DisplayName, which resolves
    // [Display(Name = "…")] — so the message contains no trace of the member name and the exact-match
    // rewrites have to be tried under the display-name spelling too. These call the overload whose
    // argument order is (message, fieldName, displayName, label, valueType, …).

    [Fact]
    public void Required_message_under_a_display_name_is_rewritten()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Given Name field is required.", "FirstName", "Given Name", "Given Name", "System.String");
        Assert.Equal("Required", msg);
    }

    [Fact]
    public void Required_message_under_a_display_name_with_includeLabel_includes_label()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Given Name field is required.", "FirstName", "Given Name", "Given Name", "System.String",
            includeLabel: true);
        Assert.Equal("Given Name is required.", msg);
    }

    [Fact]
    public void StringLength_message_under_a_display_name_is_rewritten()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Given Name must be a string with a minimum length of 2 and a maximum length of 50.",
            "FirstName", "Given Name", "Given Name", "System.String", max: 50, min: 2);
        Assert.Equal("Must be between 2 and 50 characters", msg);
    }

    [Fact]
    public void MinLength_message_under_a_display_name_is_rewritten_with_the_list_wording()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The field Chosen Tags must be a string or array type with a minimum length of '2'.",
            "Tags", "Chosen Tags", "Chosen Tags", "System.Collections.Generic.List`1[System.String]", min: 2);
        Assert.Equal("Must select at least 2 options", msg);
    }

    [Fact]
    public void Member_name_message_still_rewrites_when_a_display_name_is_also_supplied()
    {
        // Both candidates are tried, not just the display name: the numeric/date parse messages are
        // formatted by the controls themselves with the raw member name, even on a [Display]-decorated
        // property.
        var msg = ValidationHelper.GetValidationMessage(
            "The Age field must be a number.", "Age", "Years Old", "Years Old", "System.Int32");
        Assert.Equal("Must be a number", msg);
    }

    [Fact]
    public void Display_name_message_passes_through_when_no_display_name_candidate_is_supplied()
    {
        // The regression this candidate exists for: with only the member name to match on, the raw
        // framework text reached the user.
        const string raw = "The Given Name field is required.";
        var msg = ValidationHelper.GetValidationMessage(raw, "FirstName", null, "Given Name", "System.String");
        Assert.Equal(raw, msg);
    }

    [Fact]
    public void Display_name_equal_to_the_member_name_is_harmless()
    {
        var msg = ValidationHelper.GetValidationMessage(
            "The Name field is required.", "Name", "Name", "Full Name", "System.String");
        Assert.Equal("Required", msg);
    }
}
