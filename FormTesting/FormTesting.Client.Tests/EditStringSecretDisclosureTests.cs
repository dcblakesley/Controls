using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Three ways an EditString marked as a secret (<c>IsPassword</c>, or
/// <c>[DataType(DataType.Password)]</c>) used to disclose it anyway: read-only mode printed the value
/// in plain text (masking keyed off <c>MaskText</c> alone), the <c>FormOptions.ShowBoundValues</c>
/// debug echo wrote it into the DOM, and the <c>autocomplete</c> fallback told mobile platforms the
/// field was an SMS one-time code.
/// </summary>
public class EditStringSecretDisclosureTests : BunitContext
{
    const string Bullet = "\u2022";

    class SecretModel
    {
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        public string? Plain { get; set; }
    }

    static string Bullets(int count) => string.Concat(Enumerable.Repeat(Bullet, count));

    IRenderedComponent<ContainerFragment> RenderString(
        object model, Action<RenderTreeBuilder> configure, FormOptions? formOptions = null) =>
        Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            configure(b);
            b.CloseComponent();
        }));

    // ───────────────────────────── autocomplete fallback ───────────────────────────────────────

    [Fact]
    public void A_password_field_falls_back_to_new_password_not_one_time_code()
    {
        // "one-time-code" is the right suppressor for an ordinary field, but on a password field iOS
        // and Android read it as "SMS/OTP field" and offer the one-time-code affordance over the
        // password the user is typing.
        var model = new SecretModel { Plain = "hunter2" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
        });

        Assert.Equal("new-password", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    [Fact]
    public void The_DataType_Password_attribute_reaches_the_autocomplete_fallback_too()
    {
        var model = new SecretModel { Password = "hunter2" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
        });

        Assert.Equal("new-password", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    [Fact]
    public void A_non_password_field_keeps_the_one_time_code_default()
    {
        // Locked decision -- the general default does not change.
        var model = new SecretModel { Plain = "Alice" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
        });

        Assert.Equal("one-time-code", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    [Fact]
    public void An_explicit_Autocomplete_still_wins_on_a_password_field()
    {
        var model = new SecretModel { Password = "hunter2" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Autocomplete", "current-password");
        });

        Assert.Equal("current-password", cut.Find("input.edit-string-input").GetAttribute("autocomplete"));
    }

    // ───────────────────────── ShowBoundValues debug echo ──────────────────────────────────────

    [Fact]
    public void The_bound_value_echo_redacts_a_password_instead_of_printing_it()
    {
        // ShowBoundValues is a development aid, but it's form-wide: a form that switched it on to
        // inspect its models was writing every password bound to it into the DOM in the clear.
        var model = new SecretModel { Password = "hunter2" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
        }, new FormOptions { ShowBoundValues = true });

        // Only the echo is in scope here: the input's own `value` attribute still carries the text,
        // which is what a password input is -- the browser masks the glyphs, not the DOM property.
        var echo = cut.Find(".bound-value");
        Assert.Equal("(7 chars, hidden)", echo.TextContent);
        Assert.DoesNotContain("hunter2", echo.OuterHtml);
    }

    [Fact]
    public void The_bound_value_echo_still_prints_an_ordinary_value()
    {
        var model = new SecretModel { Plain = "Alice" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
        }, new FormOptions { ShowBoundValues = true });

        Assert.Equal("Alice", cut.Find(".bound-value").TextContent);
    }

    // ─────────────────────────── read-only password masking ────────────────────────────────────

    [Fact]
    public void A_read_only_password_masks_with_bullets_and_offers_the_reveal_toggle()
    {
        var model = new SecretModel { Password = "hunter2" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
        });

        Assert.Equal(Bullets(7), cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
        Assert.DoesNotContain("hunter2", cut.Markup);

        var toggle = cut.Find(".edit-masked-value button");
        Assert.Equal("Show value", toggle.GetAttribute("aria-label"));

        toggle.Click();
        Assert.Equal("hunter2", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
        Assert.Equal("Hide value", cut.Find(".edit-masked-value button").GetAttribute("aria-label"));

        cut.Find(".edit-masked-value button").Click();
        Assert.Equal(Bullets(7), cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
    }

    [Fact]
    public void The_IsPassword_parameter_masks_read_only_mode_the_same_way()
    {
        var model = new SecretModel { Plain = "s3cret" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.AddAttribute(5, "IsEditMode", false);
        });

        Assert.Equal(Bullets(6), cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
    }

    [Fact]
    public void The_bullet_mask_counts_visible_characters_not_UTF16_units()
    {
        // Same single-character-mask semantics MaskText="*" already had -- one glyph per grapheme.
        var emoji = char.ConvertFromUtf32(0x1F600);              // one grapheme, two UTF-16 chars
        var model = new SecretModel { Plain = emoji + "ab" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.AddAttribute(5, "IsEditMode", false);
        });

        Assert.Equal(Bullets(3), cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
    }

    [Fact]
    public void An_explicit_MaskText_still_wins_over_the_password_bullet_mask()
    {
        // The more specific instruction: a consumer who asked for "last four visible" on a secret
        // field meant it.
        var model = new SecretModel { Password = "abcdefgh" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
        });

        Assert.Equal("****-fgh", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
        Assert.DoesNotContain(Bullet, cut.Markup);
    }

    [Fact]
    public void A_read_only_password_never_renders_as_a_link()
    {
        // Same precedence the mask already had over Url: the text on screen isn't the real value.
        var model = new SecretModel { Password = "hunter2" };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Url", "https://example.com");
            b.AddAttribute(5, "IsEditMode", false);
        });

        Assert.Empty(cut.FindAll("a"));
        Assert.Equal(Bullets(7), cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void A_read_only_password_with_no_value_falls_through_to_the_plain_read_only_value(string? value)
    {
        // Nothing to mask and nothing for the toggle to reveal -- the same empty-value fall-through
        // MaskText already takes.
        var model = new SecretModel { Password = value };
        Expression<Func<string?>> field = () => model.Password;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Password);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
        });

        Assert.Empty(cut.FindAll(".edit-masked-value"));
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal("Password", cut.Find("div.edit-readonly-value").GetAttribute("id"));
    }

    [Fact]
    public void An_ordinary_read_only_field_is_untouched()
    {
        // The masked row must not start appearing for fields nobody marked secret.
        var model = new SecretModel { Plain = "Alice" };
        Expression<Func<string?>> field = () => model.Plain;
        var cut = RenderString(model, b =>
        {
            b.AddAttribute(1, "Value", model.Plain);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
        });

        Assert.Empty(cut.FindAll(".edit-masked-value"));
        Assert.Equal("Alice", cut.Find("div.edit-readonly-value").TextContent);
    }
}
