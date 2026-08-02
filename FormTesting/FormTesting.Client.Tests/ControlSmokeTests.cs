using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests: render each control inside an EditForm and confirm the basic markup,
/// ARIA attributes, and edit/read-only mode switching all work after the EditControlBase refactor.
/// </summary>
public class ControlSmokeTests : BunitContext
{
    /// <summary>
    /// Wraps an inner render fragment in an EditForm so the controls get the cascading EditContext.
    /// Programmatic component construction also requires <c>ValueExpression</c> explicitly — Blazor's
    /// <c>@bind-Value</c> macro normally synthesizes it from the markup but we don't have that luxury here.
    /// </summary>
    [Fact]
    public void EditString_renders_input_with_resolved_id()
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
        Assert.Equal("Name", input.Id);
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.Equal("Alice", input.GetAttribute("value"));
    }

    [Fact]
    public void EditString_omits_aria_required_when_field_is_not_required()
    {
        var model = new PersonModel { Username = "bob" };
        Expression<Func<string>> field = () => model.Username; // no [Required]
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        // Optional fields omit aria-required entirely rather than emitting the noisy "false".
        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("aria-required"));
    }

    [Fact]
    public void EditString_emits_aria_required_when_IsRequired_parameter_is_set_without_the_attribute()
    {
        // The IsRequired parameter is the conditional-requiredness escape hatch (e.g. RequiredIf).
        // It must drive aria-required, not just the visible star, so the two signals agree.
        var model = new PersonModel { Username = "bob" };
        Expression<Func<string>> field = () => model.Username; // no [Required] attribute
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsRequired", true);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.NotNull(cut.Find(".edit-label-required-star")); // and the visible star, in agreement
    }

    [Fact]
    public void EditString_in_read_only_mode_renders_ReadOnlyValue_instead_of_input()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input.edit-string-input"));
        var ro = cut.Find(".edit-readonly-value");
        Assert.Contains("Alice", ro.TextContent);
    }

    [Fact]
    public void ReadOnlyValue_is_not_announced_as_an_editable_textbox()
    {
        // A display-only value must not pose as an editable textbox or be a tab stop.
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ro = cut.Find(".edit-readonly-value");
        Assert.False(ro.HasAttribute("role"));
        Assert.False(ro.HasAttribute("tabindex"));
    }

    [Fact]
    public void EditString_read_only_link_blocks_javascript_scheme()
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "javascript:alert(1)");
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));   // no script-executing link is rendered
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Theory]
    // Finding 59: Uri.TryCreate(..., UriKind.Absolute) fails to parse a URL carrying an ASCII
    // tab/CR/LF inside or right after the scheme -- the old code then fell through to "anything
    // unparseable is a safe relative URL" and returned it verbatim. The WHATWG URL parser strips all
    // ASCII tab/newline before parsing, so a browser re-forms and runs the javascript: URL on click.
    [InlineData("java\tscript:alert(1)")]   // tab inside the scheme
    [InlineData("javascript\t:alert(1)")]   // tab right after the scheme
    [InlineData("java\nscript:alert(1)")]   // LF inside the scheme
    [InlineData("java\rscript:alert(1)")]   // CR inside the scheme
    public void EditString_read_only_link_blocks_javascript_scheme_hidden_by_tab_or_newline(string maliciousUrl)
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", maliciousUrl);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));   // no script-executing link is rendered
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Theory]
    // The WHATWG URL parser has a SEPARATE, EARLIER preprocessing step than the tab/newline removal
    // above: it trims any leading/trailing C0 control (U+0000-U+001F) or space (U+0020) from the input
    // before it ever looks at the scheme. Uri.TryCreate does not do this -- a leading C0 control isn't
    // a valid scheme character, so parsing as absolute fails and (pre-fix) the old code fell through to
    // "unparseable means safe relative URL" and returned the raw string, control byte and all. The
    // browser performs its own trim when resolving the href, exposing the javascript: scheme underneath.
    [InlineData("\u0001javascript:alert(1)")]           // leading C0 control (SOH)
    [InlineData("\u001Fjavascript:alert(1)")]           // leading C0 control (US, the top of the C0 range)
    [InlineData("\u0000javascript:alert(1)")]           // leading NUL -- not DOM-exploitable (HTML5
                                                          // tokenization replaces it with U+FFFD before it
                                                          // reaches the attribute) but stripped anyway: the
                                                          // DOM isn't the only consumer of this value.
    [InlineData(" \u0001javascript:alert(1)")]          // leading space THEN control -- both trimmed
    [InlineData("\u0001java\tscript:alert(1)")]         // composed bypass: leading control + interior tab
    [InlineData("\u0001data:text/html,alert(1)")]       // leading control hiding a data: URL
    [InlineData("\u0001vbscript:alert(1)")]             // leading control hiding a vbscript: URL
    public void EditString_read_only_link_blocks_javascript_scheme_hidden_by_c0_control(string maliciousUrl)
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", maliciousUrl);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));   // no script-executing link is rendered
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditString_read_only_link_blocks_javascript_scheme_hidden_by_leading_space()
    {
        // Pre-existing behavior: Uri.TryCreate already trims an ordinary leading space itself, so this
        // was blocked before the C0 fix too. Guard it explicitly so the new explicit C0-or-space trim
        // doesn't regress the case that used to work "by accident".
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", " javascript:alert(1)");
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Theory]
    [InlineData("http://example.com", "http://example.com")]
    [InlineData("https://example.com", "https://example.com")]
    [InlineData("mailto:person@example.com", "mailto:person@example.com")]
    [InlineData("/relative/path", "/relative/path")]           // no scheme -- relative URLs are fine
    // A tab/newline embedded in an otherwise-safe URL is stripped (matching what the browser's own
    // URL parser does), not treated as a reason to block it -- and the rendered href is the stripped
    // value, not the raw one.
    [InlineData("ht\ttp://example.com", "http://example.com")]
    [InlineData("https://exa\nmple.com", "https://example.com")]
    // Leading/trailing C0 control or space is trimmed the same way -- but only at the edges: the
    // meaningful trailing content right before the trimmed control byte is preserved verbatim.
    [InlineData("\u0001https://example.com/page", "https://example.com/page")]
    [InlineData("https://example.com/page\u0001", "https://example.com/page")]
    public void EditString_read_only_link_allows_safe_schemes_and_strips_tab_or_newline(string url, string expectedHref)
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", url);
            b.CloseComponent();
        }));

        Assert.Equal(expectedHref, cut.Find("a.edit-string-link").GetAttribute("href"));
    }

    [Fact]
    public void EditString_read_only_blank_link_gets_noopener_rel()
    {
        var model = new PersonModel { Name = "Home" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.AddAttribute(6, "UrlTarget", "_blank");
            b.CloseComponent();
        }));

        var a = cut.Find("a.edit-string-link");
        Assert.Equal("https://example.com", a.GetAttribute("href"));
        Assert.Equal("noopener noreferrer", a.GetAttribute("rel"));
    }

    [Fact]
    public void EditString_read_only_link_is_named_by_the_label_AND_its_own_text()
    {
        // aria-labelledby="lbl-Name" alone OVERWRITES the link text, so every URL field announced as
        // just its label ("Email") and never its destination -- two same-labeled links in a list were
        // indistinguishable to a screen reader working through them. Referencing the element's own id
        // is legal ARIA and concatenates the link text after the label.
        var model = new PersonModel { Name = "example.com" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.CloseComponent();
        }));

        var a = cut.Find("a.edit-string-link");
        Assert.Equal("Name", a.GetAttribute("id"));            // the self-reference has to resolve
        Assert.Equal("lbl-Name Name", a.GetAttribute("aria-labelledby"));
        Assert.Equal("lbl-Name", cut.Find("label").Id);        // ...and so does the label reference
    }

    [Theory]
    [InlineData("_blank", true)]
    [InlineData("_BLANK", true)]   // keyword matching is case-insensitive, as everywhere else here
    [InlineData(null, false)]
    [InlineData("_self", false)]
    // A NAMED target reuses an existing context when one by that name is open, so "opens in a new
    // tab" would be a claim the control can't make.
    [InlineData("vendor", false)]
    public void EditString_read_only_link_announces_a_new_tab_only_for_blank(string? target, bool expectAnnouncement)
    {
        var model = new PersonModel { Name = "Home" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.AddAttribute(6, "UrlTarget", target);
            b.CloseComponent();
        }));

        var a = cut.Find("a.edit-string-link");
        var hidden = a.QuerySelector("span.edit-sr-only");
        if (expectAnnouncement)
        {
            // Inside the <a>, so the self-referencing aria-labelledby above folds it into the name.
            Assert.NotNull(hidden);
            Assert.Equal("(opens in new tab)", hidden.TextContent.Trim());
            Assert.Contains("Home", a.TextContent);   // the visible text is still the whole label
        }
        else
        {
            Assert.Null(hidden);
            Assert.Equal("Home", a.TextContent.Trim());
        }
    }

    [Theory]
    // A protocol-relative URL has no scheme, so Uri.TryCreate(..., Absolute) fails and the old code
    // fell through to "unparseable means safe relative URL" -- but a browser resolves "//host/path"
    // against the *page's* scheme and navigates cross-origin. Browsers also normalize backslashes to
    // forward slashes for special schemes, so every slash/backslash combination is the same attack.
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("\\/evil.example/x")]
    [InlineData("\\\\evil.example/x")]
    // ...and the two preprocessing steps run first, so neither can be used to smuggle one past.
    [InlineData(" //evil.example/x")]      // leading space trimmed, still protocol-relative
    [InlineData("\u0001//evil.example/x")] // leading C0 control trimmed, still protocol-relative
    [InlineData("/\t/evil.example/x")]     // interior tab stripped, still protocol-relative
    public void EditString_read_only_link_blocks_protocol_relative_urls(string url)
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", url);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));   // no cross-origin link is rendered
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Theory]
    // A URL that preprocessing empties out used to render href="" -- which resolves to the current
    // document, so clicking the "link" silently reloaded the page. Note a C0 control is NOT .NET
    // whitespace, so these get past the IsNullOrWhiteSpace guard and only vanish during the trim.
    [InlineData("\u0001")]
    [InlineData("\u0001\u0002\u0003")]
    [InlineData("\u0001\t\u0002")]
    // Ordinary whitespace-only URLs never got that far, but pin them alongside: same plain-text result.
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void EditString_read_only_link_renders_plain_text_when_the_url_preprocesses_to_nothing(string url)
    {
        var model = new PersonModel { Name = "Click" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", url);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));   // no self-reloading empty href
        Assert.Contains("Click", cut.Find(".edit-readonly-value").TextContent);
    }

    [Theory]
    // rel is about whether the target can hand another browsing context a window.opener handle on this
    // page. A NAMED target is the case that genuinely leaks it (browsers already imply noopener for
    // _blank); only the same-context keywords are exempt.
    [InlineData(null, null)]
    [InlineData("_self", null)]
    [InlineData("_SELF", null)]
    [InlineData("_parent", null)]
    [InlineData("_top", null)]
    [InlineData("_blank", "noopener noreferrer")]
    [InlineData("_BLANK", "noopener noreferrer")]   // keyword matching is case-insensitive
    [InlineData("vendor", "noopener noreferrer")]   // named target: reverse-tabnabbing surface
    [InlineData("_unknownkeyword", "noopener noreferrer")] // not a real keyword -- it names a context
    public void EditString_read_only_link_rel_is_set_for_every_target_that_can_leak_an_opener(string? target, string? expectedRel)
    {
        var model = new PersonModel { Name = "Home" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.AddAttribute(6, "UrlTarget", target);
            b.CloseComponent();
        }));

        var a = cut.Find("a.edit-string-link");
        Assert.Equal(target, a.GetAttribute("target"));   // the target itself is always passed through
        Assert.Equal(expectedRel, a.GetAttribute("rel"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EditString_read_only_link_falls_back_to_plain_text_when_the_value_is_empty(string? value)
    {
        // An <a> with no text is invisible but still clickable -- a zero-size navigation target sitting
        // in the layout, and a link with no accessible name for a screen reader to announce. With
        // nothing to label it, fall through to the plain read-only value instead.
        var model = new PersonModel { Name = value! };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Url", "https://example.com");
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("a"));
        var readOnly = cut.Find("div.edit-readonly-value");
        Assert.Equal("Name", readOnly.GetAttribute("id"));
        Assert.Equal("No Value", readOnly.QuerySelector("span[aria-hidden=\"true\"]")!.TextContent);
    }

    [Fact]
    public void EditString_renders_required_star_when_attribute_present()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find(".edit-label-required-star"));
    }

    [Fact]
    public void EditNumber_uses_Required_attribute_for_aria_required()
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

        var input = cut.Find("input[type=number]");
        Assert.Equal("true", input.GetAttribute("aria-required"));
    }

    [Fact]
    public void EditBool_in_read_only_mode_renders_text_not_checkbox_by_default()
    {
        // The 10.1.0 default for EditBool's read-only mode — ReadOnlyValue with TrueText/FalseText.
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input[type=checkbox]"));
        Assert.Contains("Yes", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditBool_with_RenderAsCheckboxWhenReadOnly_keeps_legacy_disabled_checkbox()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "RenderAsCheckboxWhenReadOnly", true);
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.True(checkbox.HasAttribute("aria-disabled") || checkbox.HasAttribute("disabled"));
    }

    [Fact]
    public void EditBool_with_UseStyledCheckbox_wraps_the_input_for_css_styling()
    {
        // Default (UseStyledCheckbox=false) renders the bare native checkbox with no wrapper --
        // opting in swaps to the hidden-input + sibling-span pattern needed for border-radius.
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "UseStyledCheckbox", true);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find("span.edit-checkbox-wrap"));
        var checkbox = cut.Find("input.edit-checkbox-input-styled");
        Assert.Equal("checkbox", checkbox.GetAttribute("type"));
        Assert.NotNull(cut.Find("span.edit-checkbox-box"));
    }

    [Fact]
    public void EditSelectEnum_renders_one_option_per_enum_value_with_sanitized_ids()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var options = cut.FindAll("option");
        Assert.Equal(5, options.Count); // 4 enum members + the leading empty/placeholder option (Priority is nullable)
        // .ToId() yields safe ids — no spaces / punctuation (the empty option is "Priority-option-none").
        foreach (var opt in options)
        {
            var id = opt.Id ?? "";
            Assert.DoesNotContain(' ', id);
            Assert.StartsWith("Priority-option-", id);
        }
    }

    [Fact]
    public void EditSelectEnum_nullable_renders_a_leading_empty_placeholder_option()
    {
        // Without it, a null value silently displays the first enum member and can't be cleared.
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "NullOptionText", "(none)");
            b.CloseComponent();
        }));

        var options = cut.FindAll("option");
        Assert.Equal("", options[0].GetAttribute("value"));    // empty option is first
        Assert.Equal("(none)", options[0].TextContent.Trim()); // labelled by NullOptionText
    }

    [Fact]
    public void EditSelectEnum_uses_GetName_for_option_text()
    {
        // Verifies GetName attribute precedence works through the rendered DOM:
        // [EnumDisplayName("Forest Green")] → "Forest Green" (wins over [Display])
        // [Display(Name = "Sky Blue")] → "Sky Blue" (no EnumDisplayName)
        // PaleYellow → "Pale Yellow" (camelCase split)
        var model = new ColorOnlyModel();
        Expression<Func<Color?>> field = () => model.Color;
        var cut = Render(WithForm(new PersonModel(), b =>
        {
            b.OpenComponent<EditSelectEnum<Color?>>(0);
            b.AddAttribute(1, "Value", model.Color);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var optionText = string.Join("|", cut.FindAll("option").Select(o => o.TextContent.Trim()));
        Assert.Contains("Forest Green", optionText);
        Assert.Contains("Sky Blue", optionText);
        Assert.Contains("Pale Yellow", optionText);
    }

    class ColorOnlyModel
    {
        public Color? Color { get; set; } = Tests.Color.Blue;
    }

    class NonNullableEnumModel
    {
        [Required] public Color BasicColor { get; set; } = Tests.Color.Blue;
    }

    [Fact]
    public void EditRadioEnum_resolves_id_and_required_from_ValueExpression_when_bound_to_a_non_nullable_enum_property()
    {
        // EditRadioEnum<TEnum> has no `where TEnum : struct` constraint, so the base class's
        // `TEnum?` is erased to plain TEnum at the CLR level -- Value/ValueExpression's real runtime
        // type always matches TEnum exactly, non-nullable model property included, so dropping Field
        // in favor of ValueExpression needs no special-case handling for this control.
        var model = new NonNullableEnumModel { BasicColor = Tests.Color.Blue };
        Expression<Func<Color>> valueExpression = () => model.BasicColor;
        var cut = Render(WithForm(new PersonModel(), b =>
        {
            b.OpenComponent<EditRadioEnum<Color>>(0);
            b.AddAttribute(1, "Value", model.BasicColor);
            b.AddAttribute(2, "ValueExpression", valueExpression);
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("BasicColor", fieldset.Id);
        Assert.Equal("true", fieldset.GetAttribute("aria-required"));
    }

    [Fact]
    public void EditCheckedStringList_renders_one_checkbox_per_option()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, checkboxes.Count);
        Assert.Single(checkboxes, c => c.HasAttribute("checked"));
    }

    [Fact]
    public void ReadOnlyValue_keeps_aria_labelledby_when_the_label_is_hidden()
    {
        // A hidden FormLabel still renders lbl-Name — visually hidden, but present — so the read-only
        // value must keep referencing it. Suppressing the reference left the value with no accessible
        // name at all.
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "IsLabelHidden", true);
            b.CloseComponent();
        }));

        Assert.Equal("lbl-Name", cut.Find("label.edit-sr-only").Id);
        Assert.Equal("lbl-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void ReadOnlyValue_keeps_aria_labelledby_when_the_label_is_shown()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("lbl-Name", cut.Find(".edit-readonly-value").GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void LabelTooltip_content_uses_lowercase_aria_hidden()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Tooltip", "Helpful hint");
            b.CloseComponent();
        }));

        // Lowercase ARIA boolean — the CSS [aria-hidden="true"] Escape-dismissal override depends
        // on it, not "True"/"False". Starts "false": reveal is pure CSS :hover/:focus, and the
        // attribute only flips to "true" while Escape-dismissed (WCAG 1.4.13).
        Assert.Equal("false", cut.Find(".edit-tooltip-content").GetAttribute("aria-hidden"));
    }
}
