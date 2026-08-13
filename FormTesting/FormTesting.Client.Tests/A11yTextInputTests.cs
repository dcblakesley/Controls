using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the text/number-input accessibility fixes:
/// <list type="bullet">
/// <item>the <c>lbltext-{id}</c> naming-anchor wiring on EditString/EditNumber/EditTextArea's editor
/// element, and the retargeted masked-row/link <c>aria-labelledby</c> references;</item>
/// <item><see cref="ReadOnlyValue"/>'s <c>aria-describedby</c> pass-through from EditNumber/EditTextArea/
/// EditString's plain read-only branch;</item>
/// <item>TXT-2: <see cref="EditString"/>'s autocomplete purpose inference, which stops the bare
/// "one-time-code" default from misidentifying an ordinary Email/FirstName/Phone/… field;</item>
/// <item>TXT-4: the label-folded names on the shell's clear/password-toggle buttons and EditString's
/// own masked-value-reveal toggle, so two same-purpose fields on one form don't render two
/// identically-named icon-only buttons.</item>
/// </list>
/// </summary>
public class A11yTextInputTests : BunitContext
{
    // Clear()'s ElementReference.FocusAsync (and EditTextArea's AutoSize JS interop) aren't under test
    // here, but AllowClear renders regardless -- Loose tolerates the interop call some of these tests
    // don't otherwise stub out.
    public A11yTextInputTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // ───────────────────────── naming anchor (aria-labelledby -> lbltext-{id}) ─────────────────────────

    [Fact]
    public void EditString_input_is_named_by_the_label_text_anchor()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("lbltext-Name", input.GetAttribute("aria-labelledby"));
        // The anchor this points at actually exists, and holds just the label text -- not the
        // tooltip trigger, which used to get folded into the same name via lbl-{id}.
        Assert.Equal("Full Name", cut.Find("#lbltext-Name").TextContent);
    }

    [Fact]
    public void EditNumber_input_is_named_by_the_label_text_anchor()
    {
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("lbltext-Age", cut.Find("input.edit-number-input").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void EditTextArea_textarea_is_named_by_the_label_text_anchor()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("lbltext-Name", cut.Find("textarea.edit-textarea-input").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void EditString_masked_row_and_link_are_named_by_the_label_text_anchor()
    {
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;

        var masked = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));
        Assert.Equal("lbltext-Name", masked.Find(".edit-masked-value").GetAttribute("aria-labelledby"));

        var link = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.CloseComponent();
        }));
        // The link keeps its OWN id in the token list too (folds the link text into the name) --
        // only the first token retargets from lbl- to lbltext-.
        Assert.Equal("lbltext-Name Name", link.Find("a.edit-string-link").GetAttribute("aria-labelledby"));
    }

    // ───────────────────────── ReadOnlyValue aria-describedby pass-through ─────────────────────────

    [Fact]
    public void EditNumber_read_only_value_carries_the_fields_describedby()
    {
        var model = new PersonModel { Price = 30m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("error-msg-Price", cut.Find(".edit-readonly-value").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void EditTextArea_read_only_value_carries_the_fields_describedby()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("error-msg-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void EditString_plain_read_only_value_carries_the_fields_describedby()
    {
        // Username, not Name -- Name has no plain-text read-only branch reachable without a mask/url
        // (this test wants the ReadOnlyValue fallback specifically, which Username's no-mask/no-url
        // value already reaches).
        var model = new PersonModel { Username = "alice" };
        Expression<Func<string>> field = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("error-msg-Username", cut.Find(".edit-readonly-value").GetAttribute("aria-describedby"));
    }

    // ───────────────────────── TXT-2: autocomplete purpose inference ─────────────────────────

    class ContactModel
    {
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PostalCode { get; set; }

        // Deliberately not in the inference table -- must still fall through to "one-time-code".
        public string? Nickname { get; set; }

        // The property name alone would infer "tel" -- the model attribute must still win over that.
        [Autocomplete("custom-token")]
        public string? Phone { get; set; }
    }

    class PasswordPurposeModel
    {
        // The property name alone would infer "email" -- being a password field must still win,
        // rendering "new-password" rather than a purpose token a password field has no business
        // carrying.
        [DataType(DataType.Password)]
        public string? Email { get; set; }
    }

    // Not static: Render(...) is an instance member of BunitContext.
    string RenderedAutocomplete<TModel>(TModel model, Expression<Func<string?>> field, string? value, string? explicitAutocomplete = null)
        where TModel : class
    {
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", value);
            b.AddAttribute(2, "ValueExpression", field);
            if (explicitAutocomplete is not null) b.AddAttribute(4, "Autocomplete", explicitAutocomplete);
            b.CloseComponent();
        }));
        return cut.Find("input.edit-string-input").GetAttribute("autocomplete")!;
    }

    [Fact]
    public void Email_property_name_infers_the_email_token()
    {
        var model = new ContactModel { Email = "a@b.com" };
        Assert.Equal("email", RenderedAutocomplete(model, () => model.Email, model.Email));
    }

    [Fact]
    public void FirstName_property_name_infers_the_given_name_token()
    {
        var model = new ContactModel { FirstName = "Alice" };
        Assert.Equal("given-name", RenderedAutocomplete(model, () => model.FirstName, model.FirstName));
    }

    [Fact]
    public void LastName_property_name_infers_the_family_name_token()
    {
        var model = new ContactModel { LastName = "Smith" };
        Assert.Equal("family-name", RenderedAutocomplete(model, () => model.LastName, model.LastName));
    }

    [Fact]
    public void PostalCode_property_name_infers_the_postal_code_token()
    {
        var model = new ContactModel { PostalCode = "12345" };
        Assert.Equal("postal-code", RenderedAutocomplete(model, () => model.PostalCode, model.PostalCode));
    }

    [Fact]
    public void An_unrecognized_property_name_still_falls_back_to_one_time_code()
    {
        // The locked-down last resort (TXT-2's remarks): a field this small mapping doesn't
        // recognize keeps today's autofill-suppressing default rather than guessing wrong.
        var model = new ContactModel { Nickname = "Al" };
        Assert.Equal("one-time-code", RenderedAutocomplete(model, () => model.Nickname, model.Nickname));
    }

    [Fact]
    public void Model_Autocomplete_attribute_wins_over_the_inferred_purpose_token()
    {
        // "Phone" alone would infer "tel" -- the explicit [Autocomplete] attribute is more specific
        // and must still win, same precedence as every other model-attribute fallback in this library.
        var model = new ContactModel { Phone = "555-1234" };
        Assert.Equal("custom-token", RenderedAutocomplete(model, () => model.Phone, model.Phone));
    }

    [Fact]
    public void Explicit_Autocomplete_parameter_wins_over_everything()
    {
        var model = new ContactModel { Email = "a@b.com" };
        Assert.Equal("explicit-token", RenderedAutocomplete(model, () => model.Email, model.Email, "explicit-token"));
    }

    [Fact]
    public void Password_field_ignores_purpose_inference_and_still_gets_new_password()
    {
        var model = new PasswordPurposeModel { Email = "hunter2" };
        Assert.Equal("new-password", RenderedAutocomplete(model, () => model.Email, model.Email));
    }

    // ───────────────────────── TXT-4: label-folded icon-only button names ─────────────────────────

    [Fact]
    public void Clear_button_name_folds_in_the_fields_label()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();
        }));

        Assert.Equal("Clear Full Name", cut.Find(".edit-input-clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void EditTextArea_clear_button_name_also_folds_in_the_fields_label()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();
        }));

        Assert.Equal("Clear Full Name", cut.Find(".edit-input-clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void Two_AllowClear_fields_on_one_form_render_distinct_clear_button_names()
    {
        // The regression TXT-4 fixes: two same-purpose fields used to render two buttons both named
        // "Clear", indistinguishable to a screen-reader user browsing a button list.
        var model = new PersonModel { Name = "Alice", Username = "alice" };
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<string>> userField = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", nameField);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();

            b.OpenComponent<EditString>(10);
            b.AddAttribute(11, "Value", model.Username);
            b.AddAttribute(12, "ValueExpression", userField);
            b.AddAttribute(14, "AllowClear", true);
            b.CloseComponent();
        }));

        var names = cut.FindAll(".edit-input-clear").Select(e => e.GetAttribute("aria-label")).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal(2, names.Distinct().Count());
        Assert.Contains("Clear Full Name", names);
        Assert.Contains("Clear Username", names);
    }

    [Fact]
    public void ClearButtonLabel_parameter_overrides_the_default()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.AddAttribute(5, "ClearButtonLabel", "Empty this field");
            b.CloseComponent();
        }));

        Assert.Equal("Empty this field", cut.Find(".edit-input-clear").GetAttribute("aria-label"));
    }

    [Fact]
    public void Password_toggle_name_folds_in_the_fields_label()
    {
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.CloseComponent();
        }));

        Assert.Equal("Show Full Name password", cut.Find(".edit-input-password-toggle").GetAttribute("aria-label"));
    }

    [Fact]
    public void Two_password_fields_on_one_form_render_distinct_toggle_names()
    {
        // The Password/Confirm-Password scenario TXT-4 calls out by name.
        var model = new PersonModel { Name = "secret", Username = "confirm" };
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<string>> userField = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", nameField);
            b.AddAttribute(4, "IsPassword", true);
            b.CloseComponent();

            b.OpenComponent<EditString>(10);
            b.AddAttribute(11, "Value", model.Username);
            b.AddAttribute(12, "ValueExpression", userField);
            b.AddAttribute(14, "IsPassword", true);
            b.CloseComponent();
        }));

        var names = cut.FindAll(".edit-input-password-toggle").Select(e => e.GetAttribute("aria-label")).ToList();
        Assert.Equal(2, names.Count);
        Assert.Equal(2, names.Distinct().Count());
    }

    [Fact]
    public void ShowPasswordButtonLabel_parameter_overrides_the_default()
    {
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.AddAttribute(5, "ShowPasswordButtonLabel", "Reveal secret");
            b.CloseComponent();
        }));

        Assert.Equal("Reveal secret", cut.Find(".edit-input-password-toggle").GetAttribute("aria-label"));
    }

    [Fact]
    public void Masked_value_toggle_name_folds_in_the_fields_label()
    {
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("Show Full Name value", cut.Find(".edit-masked-value button").GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowValueButtonLabel_parameter_overrides_the_default()
    {
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.AddAttribute(6, "ShowValueButtonLabel", "Reveal the account number");
            b.CloseComponent();
        }));

        Assert.Equal("Reveal the account number", cut.Find(".edit-masked-value button").GetAttribute("aria-label"));
    }
}
