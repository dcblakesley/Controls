using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for EditString's read-only <c>MaskText</c> view. The first two tests are the
/// byte-identical-markup pins from finding 68 of the 2026-07-30 audit (the two masked/revealed
/// branches used to be copy-pasted 8-line blocks, collapsed into a local <c>MaskedValueRow</c>
/// RenderFragment): they hold the exact markup shape (id/aria wiring, button label/class, icon glyph)
/// that existed before the collapse, plus the toggle interaction. The rest pin the masking semantics
/// themselves -- which mask lengths produce which text, and what happens at a UTF-16 boundary.
/// </summary>
public class EditStringMaskedValueTests : BunitContext
{
    IRenderedComponent<ContainerFragment> RenderMasked(
        PersonModel model, Expression<Func<string>> field, string mask = "****-", string? url = null) =>
        Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", mask);
            b.AddAttribute(5, "IsEditMode", false);
            b.AddAttribute(6, "Url", url);
            b.CloseComponent();
        }));

    /// <summary> The masked text currently on screen. </summary>
    static string MaskedText(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find(".edit-masked-value .edit-readonly-value").TextContent;

    [Fact]
    public void Masked_state_shows_the_mask_with_a_reveal_toggle()
    {
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field);

        var wrapper = cut.Find(".edit-masked-value");
        Assert.Equal("Name", wrapper.GetAttribute("id"));
        Assert.Equal("Name", wrapper.GetAttribute("data-test-id"));
        Assert.Equal("lbl-Name", wrapper.GetAttribute("aria-labelledby"));

        var span = cut.Find(".edit-masked-value .edit-readonly-value");
        Assert.Equal("****-fgh", span.TextContent);

        var button = cut.Find(".edit-masked-value button");
        Assert.Equal("Show value", button.GetAttribute("aria-label"));
        Assert.Contains("edit-icon-eye-invisible", button.ClassList);
        Assert.Equal("eye-invisible", cut.Find(".edit-masked-value svg").GetAttribute("data-icon"));
    }

    [Fact]
    public void Clicking_the_toggle_reveals_the_full_value_with_the_hide_button()
    {
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field);

        cut.Find(".edit-masked-value button").Click();

        var wrapper = cut.Find(".edit-masked-value");
        Assert.Equal("Name", wrapper.GetAttribute("id"));
        Assert.Equal("lbl-Name", wrapper.GetAttribute("aria-labelledby"));

        var span = cut.Find(".edit-masked-value .edit-readonly-value");
        Assert.Equal("abcdefgh", span.TextContent);

        var button = cut.Find(".edit-masked-value button");
        Assert.Equal("Hide value", button.GetAttribute("aria-label"));
        Assert.Contains("edit-icon-eye", button.ClassList);
        Assert.DoesNotContain("edit-icon-eye-invisible", button.ClassList);
        Assert.Equal("eye", cut.Find(".edit-masked-value svg").GetAttribute("data-icon"));

        // Toggling again returns to the masked state -- the shared fragment's closure still targets
        // the correct instance field after the collapse.
        cut.Find(".edit-masked-value button").Click();
        Assert.Equal("Show value", cut.Find(".edit-masked-value button").GetAttribute("aria-label"));
    }

    [Theory]
    // A single-character mask is the "cover everything" shape: it repeats, it doesn't prefix.
    [InlineData("*", "abcd", "****")]
    [InlineData("x", "a", "x")]
    // A multi-character mask prefixes and keeps the tail it doesn't cover...
    [InlineData("****-", "abcdefgh", "****-fgh")]
    // ...and once it's at least as long as the value there is no tail left to show. Equal length is
    // the boundary case: the mask alone, never the mask plus an empty slice at index == Length.
    [InlineData("****", "abcd", "****")]
    [InlineData("****-****-", "abc", "****-****-")]
    public void Mask_shape_depends_on_the_mask_length_relative_to_the_value(string mask, string value, string expected)
    {
        var model = new PersonModel { Name = value };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field, mask);

        Assert.Equal(expected, MaskedText(cut));
    }

    [Fact]
    public void A_whitespace_only_mask_masks_instead_of_disclosing_the_value()
    {
        // The razor branch used IsNullOrWhiteSpace while GetMaskValue used IsNullOrEmpty, so an
        // all-whitespace mask skipped the masked branch entirely and printed the raw value -- the one
        // disagreement between the two guards, and it failed open.
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field, "   ");

        Assert.Equal("   defgh", MaskedText(cut));
        Assert.DoesNotContain("abc", MaskedText(cut));
        Assert.NotNull(cut.Find(".edit-masked-value button"));   // and it is a real masked row, with the toggle
    }

    [Fact]
    public void A_multi_character_mask_never_cuts_a_surrogate_pair_in_half()
    {
        // The cut point is a UTF-16 offset, so a 3-char mask over "ab<emoji>cd" used to land between
        // the emoji's high and low surrogate and emit the orphaned low half -- a replacement character
        // sitting right after the mask.
        var emoji = char.ConvertFromUtf32(0x1F600);          // U+1F600, one grapheme, two UTF-16 chars
        var model = new PersonModel { Name = "ab" + emoji + "cd" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field, "***");

        var text = MaskedText(cut);
        Assert.Equal("***cd", text);
        Assert.DoesNotContain('�', text);               // no replacement character
        Assert.DoesNotContain(text, c => char.IsSurrogate(c)); // and no orphaned half of the pair
    }

    [Fact]
    public void A_single_character_mask_is_one_glyph_per_visible_character_not_per_char()
    {
        // string.Length counts UTF-16 code units, so an astral character used to be masked by two
        // glyphs and a combining sequence by one per mark -- a mask visibly wider than what it hides.
        var emoji = char.ConvertFromUtf32(0x1F600);
        var model = new PersonModel { Name = emoji + "ab" };  // 3 graphemes, 4 chars
        Assert.Equal("***", MaskedText(RenderMasked(model, () => model.Name, "*")));

        var combiningAcute = (char)0x0301;                                  // U+0301 COMBINING ACUTE
        var combining = new PersonModel { Name = "e" + combiningAcute + "x" }; // 2 graphemes, 3 chars
        Assert.Equal("**", MaskedText(RenderMasked(combining, () => combining.Name, "*")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Masked_mode_with_no_value_renders_the_plain_read_only_value(string? value)
    {
        // There is nothing to mask and nothing for the eye toggle to reveal: the masked row was just
        // an empty span next to a button that did nothing visible. Fall through to ReadOnlyValue,
        // which has its own reserved-space placeholder for the empty case.
        var model = new PersonModel { Name = value! };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field);

        Assert.Empty(cut.FindAll(".edit-masked-value"));
        Assert.Empty(cut.FindAll("button"));

        var readOnly = cut.Find("div.edit-readonly-value");
        Assert.Equal("Name", readOnly.GetAttribute("id"));
        // ReadOnlyValue's own empty case: a hidden placeholder holding the line's height open.
        Assert.Equal("No Value", readOnly.QuerySelector("span[aria-hidden=\"true\"]")!.TextContent);
    }

    [Fact]
    public void The_masked_wrapper_carries_the_consumer_class()
    {
        // The masked wrapper is the read-only field element in mask mode, and `class` is documented to
        // land on the field element in every mode -- the link branch and ReadOnlyValue both already
        // carried it, so mask mode was the one hole.
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.AddAttribute(6, "class", "w-narrow");
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".edit-masked-value");
        Assert.Contains("w-narrow", wrapper.ClassList);
        Assert.Contains("edit-masked-value", wrapper.ClassList);
        // Revealing keeps it -- both rows come from the same fragment.
        cut.Find(".edit-masked-value button").Click();
        Assert.Contains("w-narrow", cut.Find(".edit-masked-value").ClassList);
    }

    [Fact]
    public void MaskText_wins_over_Url_when_both_are_set()
    {
        // Long-standing precedence, previously untested: a masked value is never also a link -- the
        // whole point of the mask is that the text on screen isn't the real value.
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field, "****-", "https://example.com");

        Assert.Empty(cut.FindAll("a"));
        Assert.Equal("****-fgh", MaskedText(cut));
    }
}
