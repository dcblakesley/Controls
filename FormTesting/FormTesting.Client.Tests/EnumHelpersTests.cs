namespace FormTesting.Client.Tests;

public class EnumHelpersTests
{
    [Fact]
    public void GetName_with_no_attribute_splits_camelCase()
    {
        Assert.Equal("Pale Yellow", Color.PaleYellow.GetName());
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

    enum TypeA
    {
        [EnumDisplayName("Type A display")] Same
    }

    enum TypeB
    {
        [EnumDisplayName("Type B display")] Same
    }
}
