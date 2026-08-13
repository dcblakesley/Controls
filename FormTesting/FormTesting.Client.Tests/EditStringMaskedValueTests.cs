using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for EditString's read-only <c>MaskText</c> view. The first three tests hold the markup
/// shape of the two states (id/aria wiring, group semantics, the toggle's stable name + pressed
/// state, icon glyph, the state-only live region) and the fact that toggling between them patches one
/// row in place rather than swapping two render regions. The rest pin the masking semantics
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
        // A bare div is role-generic, where ARIA prohibits naming -- the aria-labelledby above was
        // inert. role="group" is both accurate (a value plus the control that reveals it) and
        // nameable, and it can carry the field's describedby the way the link branch already does.
        Assert.Equal("group", wrapper.GetAttribute("role"));
        Assert.Equal("error-msg-Name", wrapper.GetAttribute("aria-describedby"));

        var span = cut.Find(".edit-masked-value .edit-readonly-value");
        Assert.Equal("****-fgh", span.TextContent);

        var button = cut.Find(".edit-masked-value button");
        Assert.Equal("Show value", button.GetAttribute("aria-label"));
        Assert.Equal("false", button.GetAttribute("aria-pressed"));
        Assert.Contains("edit-icon-eye-invisible", button.ClassList);
        Assert.Equal("eye-invisible", cut.Find(".edit-masked-value svg").GetAttribute("data-icon"));

        // The state is announced, but never the value: a masked value is by definition something the
        // page was asked not to show, so it must not be piped through a live region.
        var status = cut.Find(".edit-masked-value span[role=\"status\"]");
        Assert.Contains("edit-sr-only", status.ClassList);
        Assert.Equal("Value hidden", status.TextContent);
    }

    [Fact]
    public void Clicking_the_toggle_reveals_the_full_value_and_flips_only_the_pressed_state()
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

        // The accessible NAME is stable and names the action; only aria-pressed moves. A toggle whose
        // name and pressed state both flip ("Hide value, pressed") is ambiguous about whether the
        // press already happened.
        var button = cut.Find(".edit-masked-value button");
        Assert.Equal("Show value", button.GetAttribute("aria-label"));
        Assert.Equal("true", button.GetAttribute("aria-pressed"));
        Assert.Contains("edit-icon-eye", button.ClassList);
        Assert.DoesNotContain("edit-icon-eye-invisible", button.ClassList);
        Assert.Equal("eye", cut.Find(".edit-masked-value svg").GetAttribute("data-icon"));
        Assert.Equal("Value shown", cut.Find(".edit-masked-value span[role=\"status\"]").TextContent);

        // Toggling again returns to the masked state.
        cut.Find(".edit-masked-value button").Click();
        Assert.Equal("false", cut.Find(".edit-masked-value button").GetAttribute("aria-pressed"));
        Assert.Equal("Value hidden", cut.Find(".edit-masked-value span[role=\"status\"]").TextContent);
    }

    [Fact]
    public void Toggling_patches_the_masked_row_in_place_instead_of_rebuilding_it()
    {
        // The regression: the two states used to render from two sibling @if/@else call sites of a
        // shared RenderFragment. Each call site is its own render REGION, so toggling removed one
        // region's elements and inserted the other's -- the focused <button> was destroyed and
        // rebuilt under the user's finger (focus falls to <body>, NVDA's virtual buffer resets).
        //
        // Blazor retains an element's event-handler id when it patches that element in place, and
        // assigns a fresh one when the element is recreated -- so the id bUnit records in
        // `blazor:onclick` (what .Click() dispatches through) is an observable proxy for element
        // identity. It only works because the handler is a method group: two EventCallbacks over the
        // same method compare equal, where a fresh lambda per render would not (see
        // EditString.ToggleMaskedValue's remarks).
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderMasked(model, field);

        var before = cut.Find(".edit-masked-value button").GetAttribute("blazor:onclick");
        Assert.False(string.IsNullOrEmpty(before)); // guards the proxy itself: no id, no signal

        cut.Find(".edit-masked-value button").Click();

        Assert.Equal(before, cut.Find(".edit-masked-value button").GetAttribute("blazor:onclick"));

        // ...and the row's element skeleton is identical in both states, which is the DOM-level
        // precondition for that in-place patch: attributes and text change, elements do not.
        Assert.Equal(MaskedRowShape(RenderMasked(model, field)), MaskedRowShape(cut));

        cut.Find(".edit-masked-value button").Click();
        Assert.Equal(before, cut.Find(".edit-masked-value button").GetAttribute("blazor:onclick"));
    }

    [Fact]
    public void The_handler_id_element_identity_proxy_does_move_when_the_row_is_genuinely_rebuilt()
    {
        // Negative control for the test above: without this, "the id didn't change" could just mean
        // the id never changes. Leaving read-only mode destroys the masked row outright, and coming
        // back builds a new one -- so the same proxy must report a different element.
        var isEditMode = false;
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<EditString>(0);
                b.AddAttribute(1, "Value", model.Name);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "MaskText", "****-");
                b.AddAttribute(5, "IsEditMode", isEditMode);
                b.CloseComponent();
            })));

        var before = cut.Find(".edit-masked-value button").GetAttribute("blazor:onclick");

        isEditMode = true;
        cut.Render();
        isEditMode = false;
        cut.Render();

        Assert.NotEqual(before, cut.Find(".edit-masked-value button").GetAttribute("blazor:onclick"));
    }

    /// <summary>
    /// The masked row's element skeleton — tag names plus attribute names (values excluded, and so is
    /// the icon's <c>&lt;svg&gt;</c> subtree, which is markup content the diff replaces wholesale
    /// without touching the button that holds the focus).
    /// </summary>
    static string MaskedRowShape(IRenderedComponent<ContainerFragment> cut)
    {
        var row = cut.Find(".edit-masked-value");
        return string.Join(" | ", new[] { row }.Concat(row.Children)
            .Select(e => $"{e.LocalName}[{string.Join(',', e.Attributes.Select(a => a.Name).Order(StringComparer.Ordinal))}]"));
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
        // ReadOnlyValue's own empty case (LST-2): real, visible fallback text -- not aria-hidden --
        // holding the line's height open while still reaching assistive technology.
        var placeholder = readOnly.QuerySelector("span");
        Assert.False(placeholder!.HasAttribute("aria-hidden"));
        Assert.Equal("Not Set", placeholder.TextContent);
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
