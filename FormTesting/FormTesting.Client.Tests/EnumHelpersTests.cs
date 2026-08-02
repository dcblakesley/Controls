using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace FormTesting.Client.Tests;

public class EnumHelpersTests
{
    /// <summary>
    /// Stand-in for a generated <c>.resx</c> designer class. <see cref="DisplayAttribute"/> resolves a
    /// <c>ResourceType</c>-backed name by looking up a public static <c>string</c> PROPERTY of that name
    /// and re-invoking its getter on every read, so a hand-written holder exercises the localized path
    /// with no satellite assembly. Must be public: the attribute rejects a non-visible resource type.
    /// </summary>
    public static class FakeUiResources
    {
        public static string Greeting =>
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "Bonjour" : "Hello";
    }

    enum LocalizedGreeting
    {
        [Display(Name = nameof(FakeUiResources.Greeting), ResourceType = typeof(FakeUiResources))]
        Greeting
    }

    [Fact]
    public void GetName_re_resolves_a_ResourceType_backed_Display_name_per_culture()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal("Hello", LocalizedGreeting.Greeting.GetName());

            // The bug: the display-name cache was keyed (type, member) with no culture, but
            // DisplayAttribute.GetName() resolves against CurrentUICulture — so the first render froze
            // one language for the whole process (on Blazor Server, for every other user's circuit too).
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");
            Assert.Equal("Bonjour", LocalizedGreeting.Greeting.GetName());

            // Switching back proves the second call re-resolved rather than re-freezing on French.
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            Assert.Equal("Hello", LocalizedGreeting.Greeting.GetName());
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void GetName_with_no_attribute_splits_camelCase()
    {
        Assert.Equal("Pale Yellow", Color.PaleYellow.GetName());
    }

    // Public so it can be an [InlineData] parameter type on a public test method.
    [Flags]
    public enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        FullControl = 4
    }

    [Theory]
    [InlineData(Access.Read | Access.Write, "Read, Write")]
    [InlineData(Access.Read | Access.Write | Access.FullControl, "Read, Write, Full Control")]
    public void GetName_of_a_combined_Flags_value_keeps_one_space_after_each_comma(Access value, string expected)
    {
        // A combined value's ToString() is already "Read, Write" -- the camel-case split used to insert
        // a space before EVERY upper-case letter, including the one that already followed the
        // separator's space, yielding "Read,  Write". Single-flag names still split normally
        // (FullControl -> "Full Control").
        Assert.Equal(expected, value.GetName());
    }

    [Fact]
    public void GetName_of_a_combined_Flags_value_is_stable_across_the_memoized_second_call()
    {
        // GetName caches per (enum type, member name), so the malformed spacing was cached for the
        // process lifetime once any one render produced it -- the second call must come back identical.
        var value = Access.Read | Access.FullControl;
        var first = value.GetName();
        var second = value.GetName();

        Assert.Equal("Read, Full Control", first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void GetName_prefers_EnumDisplayName_attribute()
    {
        // [EnumDisplayName("Forest Green")] should win.
        Assert.Equal("Forest Green", Color.Green.GetName());
    }

    [Fact]
    public void GetName_falls_back_to_Display_attribute_when_EnumDisplayName_missing()
    {
        // [Display(Name = "Sky Blue")] applies because no [EnumDisplayName] is present.
        Assert.Equal("Sky Blue", Color.Blue.GetName());
    }

    [Fact]
    public void GetName_returns_raw_name_for_single_word_member()
    {
        Assert.Equal("Red", Color.Red.GetName());
    }

    [Fact]
    public void GetName_is_cached_per_type_and_member()
    {
        // Calling twice returns the same string instance — confirms the cache is in play.
        var first = Color.PaleYellow.GetName();
        var second = Color.PaleYellow.GetName();
        Assert.Same(first, second);
    }

    [Fact]
    public void GetName_does_not_collide_across_enum_types_with_same_member_name()
    {
        // Regression test for the type-blind cache that the refactor fixed:
        // Both TypeA.Same and TypeB.Same have member name "Same" but distinct display names.
        Assert.Equal("Type A display", TypeA.Same.GetName());
        Assert.Equal("Type B display", TypeB.Same.GetName());
    }

    [Fact]
    public void GetName_returns_empty_for_null()
    {
        // GetName now tolerates null (checked-list / radio controls bind nullable enums); previously
        // it dereferenced the argument and the call sites carried CS8604 warnings.
        object? value = null;
        Assert.Equal(string.Empty, value.GetName());
    }

    [Theory]
    [InlineData("Hello World", "Hello-World")]
    [InlineData("Foo Bar Baz", "Foo-Bar-Baz")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData("a/b!c", "abc")]
    [InlineData("with-hyphen_and_underscore", "with-hyphen_and_underscore")]
    public void ToId_string_sanitizes_to_valid_html_id(string input, string expected)
    {
        Assert.Equal(expected, input.ToId());
    }

    [Fact]
    public void ToId_object_returns_empty_for_null()
    {
        object? value = null;
        Assert.Equal(string.Empty, value.ToId());
    }

    [Fact]
    public void ToId_stays_correct_after_the_id_cache_saturates()
    {
        // Fill well past the 10k cap with distinct strings (fills the process-wide id cache), then
        // confirm conversion still works. Past saturation the cache stops growing — and stops calling
        // the lock-acquiring Count — so memoization is lost, but the computed result is unaffected.
        for (var i = 0; i < 10_050; i++)
            _ = $"opt {i}!".ToId();

        Assert.Equal("saturated-item", "saturated item".ToId());
        Assert.Equal("abc", "a/b!c".ToId());
    }

    [Fact]
    public void ToId_object_handles_enum_with_punctuation_in_display_name()
    {
        // The whole point of .ToId() — Color.Green's display name is "Forest Green",
        // raw enum value is "Green", ToId returns "Green" (the C# name).
        Assert.Equal("Green", Color.Green.ToId());
    }

    // ----- ToUniqueIds: the de-duplication CheckboxOptionList / the radio hosts render against -------

    [Fact]
    public void ToUniqueIds_keeps_the_plain_sanitized_segment_for_an_ordinary_list()
    {
        // The shape every existing test, e2e selector and visual baseline pins must be untouched.
        Assert.Equal(new[] { "a", "b", "Hello-World" }, EnumHelpers.ToUniqueIds(new[] { "a", "b", "Hello World" }));
    }

    [Fact]
    public void ToUniqueIds_falls_back_to_the_index_when_sanitizing_leaves_nothing()
    {
        // Every all-CJK option sanitized to "" — so every checkbox/radio shared one id, and per HTML's
        // label-for resolution every label toggled the FIRST input.
        var ids = EnumHelpers.ToUniqueIds(new[] { "赤", "青", "緑" });

        Assert.Equal(new[] { "0", "1", "2" }, ids);
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public void ToUniqueIds_disambiguates_two_options_that_sanitize_alike()
    {
        // "a!" and "a?" both sanitize to "a": the first claim keeps it, the collider takes its index.
        Assert.Equal(new[] { "a", "1-a" }, EnumHelpers.ToUniqueIds(new[] { "a!", "a?" }));
    }

    [Fact]
    public void ToUniqueIds_leaves_a_reserved_segment_to_the_caller()
    {
        // EditRadioString reserves "other" for its built-in Other radio, so a real option of that name
        // yields instead of shadowing it (the built-in's rb-{id}-other is pinned by several tests).
        Assert.Equal(new[] { "0-other", "b" }, EnumHelpers.ToUniqueIds(new[] { "other", "b" }, reserved: "other"));
    }

    [Fact]
    public void ToUniqueIds_leaves_every_reserved_segment_to_the_caller()
    {
        // SelectOptionList reserves both of its synthetic option ids at once ({Id}-option-none for the
        // leading blank option, {Id}-option-placeholder for the hidden unmatched-value one).
        Assert.Equal(
            new[] { "0-none", "1-placeholder", "b" },
            EnumHelpers.ToUniqueIds(new[] { "none", "placeholder", "b" }, "none", "placeholder"));
    }

    [Fact]
    public void ToUniqueIds_ignores_a_null_reserved_segment()
    {
        // EditRadioString passes `HasOther ? "other" : null` -- the null form must reserve nothing.
        Assert.Equal(new[] { "other", "b" }, EnumHelpers.ToUniqueIds(new[] { "other", "b" }, (string?)null));
    }

    [Fact]
    public void ToUniqueIds_re_prefixes_when_the_index_qualified_form_is_itself_taken()
    {
        // Pathological: index 2's "a" collides, and its "2-a" fallback is a literal option at index 0,
        // so it has to prefix again rather than duplicate that id.
        Assert.Equal(new[] { "2-a", "a", "2-2-a" }, EnumHelpers.ToUniqueIds(new[] { "2-a", "a", "a" }));
    }

    [Fact]
    public void ToUniqueIds_returns_an_empty_array_for_an_empty_list()
    {
        Assert.Empty(EnumHelpers.ToUniqueIds(Array.Empty<string>()));
    }

    enum TypeA
    {
        [EnumDisplayName("Type A display")] Same
    }

    enum TypeB
    {
        [EnumDisplayName("Type B display")] Same
    }
}
