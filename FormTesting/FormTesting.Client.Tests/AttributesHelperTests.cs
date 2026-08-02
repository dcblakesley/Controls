using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

public class AttributesHelperTests
{
    readonly PersonModel _model = new();

    FieldIdentifier FieldOf<T>(System.Linq.Expressions.Expression<Func<T>> field)
        => FieldIdentifier.Create(field);

    [Fact]
    public void GetId_uses_explicit_id_when_provided()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId("custom-id", null, null, fid);
        Assert.Equal("custom-id", id);
    }

    [Fact]
    public void GetId_falls_back_to_field_name_with_spaces_stripped()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, null, null, fid);
        Assert.Equal("Name", id);
    }

    [Fact]
    public void GetId_prefixes_with_FormGroupOptions_name()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, new FormGroupOptions { Name = "billing" }, null, fid);
        Assert.Equal("billing-Name", id);
    }

    [Fact]
    public void GetId_layers_idPrefix_on_top_of_group_name()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId(null, new FormGroupOptions { Name = "billing" }, "form1", fid);
        Assert.Equal("form1-billing-Name", id);
    }

    [Fact]
    public void GetId_explicit_id_wins_over_prefixes()
    {
        var fid = FieldOf(() => _model.Name);
        var id = AttributesHelper.GetId("explicit", new FormGroupOptions { Name = "billing" }, "form1", fid);
        Assert.Equal("explicit", id);
    }

    [Fact]
    public void GetId_ignores_an_empty_idPrefix()
    {
        // An empty (not null) IdPrefix — the shape a consumer's unset string variable arrives as —
        // must not contribute a separator, the same way an empty FormGroupOptions name doesn't.
        var fid = FieldOf(() => _model.Name);
        Assert.Equal("Name", AttributesHelper.GetId(null, null, "", fid));
        Assert.Equal("billing-Name", AttributesHelper.GetId(null, new FormGroupOptions { Name = "billing" }, "", fid));
    }

    [Fact]
    public void GetLabelText_uses_DisplayName_attribute_when_present()
    {
        var fid = FieldOf(() => _model.Name);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Name);
        Assert.Equal("Full Name", attrs.GetLabelText(fid));
    }

    [Fact]
    public void GetLabelText_splits_camelCase_when_no_attribute()
    {
        var fid = FieldOf(() => _model.BirthDate);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.BirthDate);
        Assert.Equal("Birth Date", attrs.GetLabelText(fid));
    }

    class DisplayAttributeModel
    {
        [Display(Name = "Given Name")]
        public string FirstName { get; set; } = "";
    }

    [Fact]
    public void GetLabelText_honors_DataAnnotations_Display_Name()
    {
        // [Display(Name)] is what DataAnnotations itself uses in its messages — ignoring it gave
        // the raw camel-split label and defeated every ValidationHelper message rewrite.
        var model = new DisplayAttributeModel();
        var fid = FieldOf(() => model.FirstName);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => model.FirstName);
        Assert.Equal("Given Name", attrs.GetLabelText(fid));
    }

    // A stand-in for a generated resources class: a public static string property whose name matches
    // the [Display(Name)] key, which DisplayAttribute.GetName() resolves through the resource type.
    public class LocalizedLabelResources
    {
        public static string GreetingLabel => "Localized Greeting";
    }

    class LocalizedDisplayModel
    {
        [Display(Name = nameof(LocalizedLabelResources.GreetingLabel), ResourceType = typeof(LocalizedLabelResources))]
        public string Greeting { get; set; } = "";
    }

    [Fact]
    public void GetLabelText_resolves_a_localized_Display_through_its_ResourceType()
    {
        // Reading raw .Name surfaced the resource KEY ("GreetingLabel"); GetName() resolves it through
        // the resource type to the localized text.
        var model = new LocalizedDisplayModel();
        var fid = FieldOf(() => model.Greeting);
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => model.Greeting);
        Assert.Equal("Localized Greeting", attrs.GetLabelText(fid));
    }

    [Fact]
    public void GetMinAndMaxLengths_reads_StringLength_attribute()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Name);
        var (min, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(2, min);
        Assert.Equal(100, max);
    }

    [Fact]
    public void GetMinAndMaxLengths_reads_separate_MinLength_MaxLength_attributes()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.Username);
        var (min, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(2, min);
        Assert.Equal(10, max);
    }

    [Fact]
    public void GetMinAndMaxLengths_takes_the_tighter_max_when_StringLength_and_MaxLength_overlap()
    {
        // Both validators run, so the effective max is the SMALLER of the two (5). Math.Max would
        // report the looser 10 and break FieldValidationDisplay's MaxLength message rewrite.
        var attrs = new List<Attribute> { new StringLengthAttribute(10), new MaxLengthAttribute(5) };
        var (_, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(5, max);
    }

    [Fact]
    public void GetMinAndMaxLengths_takes_the_tighter_max_regardless_of_which_attribute_is_smaller()
    {
        var attrs = new List<Attribute> { new StringLengthAttribute(5), new MaxLengthAttribute(10) };
        var (_, max) = AttributesHelper.GetMinAndMaxLengths(attrs);
        Assert.Equal(5, max);
    }

    [Fact]
    public void Description_extension_pulls_DescriptionAttribute()
    {
        var attrs = AttributesHelper.GetExpressionCustomAttributes(() => _model.BirthDate);
        Assert.Equal("The person's birth date", attrs.Description());
    }

    [Fact]
    public void Placeholder_extension_pulls_PlaceholderAttribute()
    {
        var attrs = new List<Attribute> { new PlaceholderAttribute("Enter a value") };
        Assert.Equal("Enter a value", attrs.Placeholder());
    }

    [Fact]
    public void Placeholder_extension_falls_back_to_Display_Prompt_when_no_PlaceholderAttribute()
    {
        var attrs = new List<Attribute> { new DisplayAttribute { Prompt = "e.g. jsmith@example.com" } };
        Assert.Equal("e.g. jsmith@example.com", attrs.Placeholder());
    }

    [Fact]
    public void Placeholder_extension_prefers_PlaceholderAttribute_over_Display_Prompt()
    {
        // Both present -- [Placeholder] is the more specific/newer attribute, so it wins over
        // DataAnnotations' own [Display(Prompt=…)] watermark slot.
        var attrs = new List<Attribute>
        {
            new PlaceholderAttribute("Wins"),
            new DisplayAttribute { Prompt = "Loses" }
        };
        Assert.Equal("Wins", attrs.Placeholder());
    }

    [Fact]
    public void Placeholder_extension_returns_null_when_neither_attribute_present()
    {
        var attrs = new List<Attribute>();
        Assert.Null(attrs.Placeholder());
    }

    [Fact]
    public void GetExpressionMember_throws_for_non_member_expression()
    {
        Assert.Throws<ArgumentException>(() =>
            AttributesHelper.GetExpressionMember<int>(() => 1 + 1));
    }

    // MinNumber / MaxNumber

    [Fact]
    public void MinNumber_and_MaxNumber_prefer_MinValue_and_MaxValue_over_Range()
    {
        // [MinValue]/[MaxValue] are the more specific/newer attributes -- same precedence rule as
        // Placeholder() winning over [Display(Prompt)].
        var attrs = new List<Attribute>
        {
            new MinValueAttribute(5),
            new MaxValueAttribute(50),
            new RangeAttribute(1, 100)
        };
        Assert.Equal(5m, attrs.MinNumber());
        Assert.Equal(50m, attrs.MaxNumber());
    }

    [Fact]
    public void MinNumber_and_MaxNumber_fall_back_to_Range_int_ctor()
    {
        var attrs = new List<Attribute> { new RangeAttribute(1, 120) };
        Assert.Equal(1m, attrs.MinNumber());
        Assert.Equal(120m, attrs.MaxNumber());
    }

    [Fact]
    public void MinNumber_and_MaxNumber_fall_back_to_Range_double_ctor()
    {
        var attrs = new List<Attribute> { new RangeAttribute(0.5, 99.5) };
        Assert.Equal(0.5m, attrs.MinNumber());
        Assert.Equal(99.5m, attrs.MaxNumber());
    }

    [Fact]
    public void MaxNumber_treats_a_double_MaxValue_Range_bound_as_unbounded()
    {
        // The ubiquitous one-sided idiom: [Range(0, double.MaxValue)] must render min="0" with no max
        // attribute at all, not throw OverflowException or produce max="1.79E+308".
        var attrs = new List<Attribute> { new RangeAttribute(0, double.MaxValue) };
        Assert.Equal(0m, attrs.MinNumber());
        Assert.Null(attrs.MaxNumber());
    }

    [Fact]
    public void MinNumber_treats_a_double_MinValue_Range_bound_as_unbounded()
    {
        var attrs = new List<Attribute> { new RangeAttribute(double.MinValue, 100) };
        Assert.Null(attrs.MinNumber());
        Assert.Equal(100m, attrs.MaxNumber());
    }

    [Fact]
    public void Int_extreme_Range_bounds_are_unbounded_like_ValidationHelpers_one_sided_rewrite()
    {
        // The integer-typed spelling of the one-sided idiom. ValidationHelper already rewrites these
        // sentinels into one-sided messages ("Cannot exceed 100"), so rendering must agree -- a bound
        // the message layer presents as absent can't appear in the DOM as min="-2147483648".
        var capped = new List<Attribute> { new RangeAttribute(int.MinValue, 100) };
        Assert.Null(capped.MinNumber());
        Assert.Equal(100m, capped.MaxNumber());

        var floored = new List<Attribute> { new RangeAttribute(0, int.MaxValue) };
        Assert.Equal(0m, floored.MinNumber());
        Assert.Null(floored.MaxNumber());
    }

    [Fact]
    public void Long_extreme_Range_string_bounds_are_unbounded()
    {
        var attrs = new List<Attribute>
        {
            new RangeAttribute(typeof(long), long.MinValue.ToString(CultureInfo.InvariantCulture), "100")
            {
                ParseLimitsInInvariantCulture = true
            }
        };
        Assert.Null(attrs.MinNumber());
        Assert.Equal(100m, attrs.MaxNumber());
    }

    [Fact]
    public void Explicit_MinValue_and_MaxValue_attributes_are_never_sentinel_suppressed()
    {
        // The sentinel rule exists only because [Range] forces both bounds to be written. [MinValue]/
        // [MaxValue] are one-sided by design, so an extreme written there is intentional and renders.
        var attrs = new List<Attribute> { new MinValueAttribute(int.MinValue), new MaxValueAttribute(int.MaxValue) };
        Assert.Equal((decimal)int.MinValue, attrs.MinNumber());
        Assert.Equal((decimal)int.MaxValue, attrs.MaxNumber());
    }

    [Fact]
    public void MinNumber_treats_a_NaN_MinValue_bound_as_unbounded()
    {
        var attrs = new List<Attribute> { new MinValueAttribute(double.NaN) };
        Assert.Null(attrs.MinNumber());
    }

    [Fact]
    public void MinValue_string_ctor_parses_invariantly_even_under_a_comma_decimal_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            var attrs = new List<Attribute> { new MinValueAttribute("1.5") };
            Assert.Equal(1.5m, attrs.MinNumber());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinNumber_Range_string_ctor_honors_ParseLimitsInInvariantCulture_true()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            var attrs = new List<Attribute>
            {
                new RangeAttribute(typeof(double), "1.5", "99.5") { ParseLimitsInInvariantCulture = true }
            };
            // "1.5" only parses as 1.5 under invariant -- under de-DE, '.' is not the decimal separator.
            Assert.Equal(1.5m, attrs.MinNumber());
            Assert.Equal(99.5m, attrs.MaxNumber());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinNumber_Range_string_ctor_defaults_to_CurrentCulture_when_flag_is_unset()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            // ParseLimitsInInvariantCulture left at its default (false) -- matches RangeAttribute's own
            // validation, which also uses CurrentCulture unless the flag is explicitly opted in.
            var attrs = new List<Attribute> { new RangeAttribute(typeof(double), "1,5", "99,5") };
            Assert.Equal(1.5m, attrs.MinNumber());
            Assert.Equal(99.5m, attrs.MaxNumber());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinNumber_returns_null_when_no_bound_attribute_is_present()
    {
        Assert.Null(new List<Attribute>().MinNumber());
    }

    [Fact]
    public void MinNumber_returns_null_for_a_null_attrs_list()
    {
        List<Attribute>? attrs = null;
        Assert.Null(attrs.MinNumber());
    }

    // MinDate / MaxDate

    [Fact]
    public void MinDate_and_MaxDate_prefer_MinValue_and_MaxValue_over_Range()
    {
        var attrs = new List<Attribute>
        {
            new MinValueAttribute("2024-01-01"),
            new MaxValueAttribute("2024-12-31"),
            new RangeAttribute(typeof(DateTime), "2020-01-01", "2020-12-31")
        };
        Assert.Equal(new DateTime(2024, 1, 1), attrs.MinDate());
        Assert.Equal(new DateTime(2024, 12, 31), attrs.MaxDate());
    }

    [Fact]
    public void MinDate_and_MaxDate_fall_back_to_a_typeof_DateTime_Range()
    {
        var attrs = new List<Attribute>
        {
            new RangeAttribute(typeof(DateTime), "2024-01-01", "2024-12-31")
        };
        Assert.Equal(new DateTime(2024, 1, 1), attrs.MinDate());
        Assert.Equal(new DateTime(2024, 12, 31), attrs.MaxDate());
    }

    [Fact]
    public void MinDate_ignores_a_numeric_Range_attribute()
    {
        // A [Range(int, int)] has OperandType typeof(int) -- MinDate must not mistake it for a date bound.
        var attrs = new List<Attribute> { new RangeAttribute(1, 120) };
        Assert.Null(attrs.MinDate());
        Assert.Null(attrs.MaxDate());
    }

    [Fact]
    public void MinValue_string_ctor_for_a_date_bound_parses_invariantly()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var attrs = new List<Attribute> { new MinValueAttribute("2024-01-01") };
            Assert.Equal(new DateTime(2024, 1, 1), attrs.MinDate());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinDate_and_MaxDate_Range_honor_ParseLimitsInInvariantCulture_true()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            // "Januar"/"Dezember" are German month names -- InvariantCulture only understands the
            // English "January"/"December", so parsing under the invariant flag must fail (null).
            var attrs = new List<Attribute>
            {
                new RangeAttribute(typeof(DateTime), "1 Januar 2024", "31 Dezember 2024")
                {
                    ParseLimitsInInvariantCulture = true
                }
            };
            Assert.Null(attrs.MinDate());
            Assert.Null(attrs.MaxDate());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinDate_and_MaxDate_Range_default_to_CurrentCulture_when_flag_is_unset()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var attrs = new List<Attribute>
            {
                new RangeAttribute(typeof(DateTime), "1 Januar 2024", "31 Dezember 2024")
            };
            Assert.Equal(new DateTime(2024, 1, 1), attrs.MinDate());
            Assert.Equal(new DateTime(2024, 12, 31), attrs.MaxDate());
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MinDate_returns_null_when_no_bound_attribute_is_present()
    {
        Assert.Null(new List<Attribute>().MinDate());
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(5, 5)]
    [InlineData(-1, -1)] // not itself a meaningful cap, but only 0 is the documented sentinel
    public void NonZero_int_converts_the_zero_sentinel_to_null(int? value, int? expected) =>
        Assert.Equal(expected, AttributesHelper.NonZero(value));

    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, null)]
    [InlineData(1024L, 1024L)]
    public void NonZero_long_converts_the_zero_sentinel_to_null(long? value, long? expected) =>
        Assert.Equal(expected, AttributesHelper.NonZero(value));

    // Positive -- contrast with NonZero: negative is ALSO a sentinel, not a real bound.

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(-1, null)] // unlike NonZero, a negative value is unset too
    [InlineData(5, 5)]
    public void Positive_int_converts_zero_and_negative_to_null(int? value, int? expected) =>
        Assert.Equal(expected, AttributesHelper.Positive(value));

    [Theory]
    [InlineData(null, null)]
    [InlineData(0L, null)]
    [InlineData(-1L, null)] // unlike NonZero, a negative value is unset too
    [InlineData(1024L, 1024L)]
    public void Positive_long_converts_zero_and_negative_to_null(long? value, long? expected) =>
        Assert.Equal(expected, AttributesHelper.Positive(value));
}
