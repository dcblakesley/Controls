using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests: render each control inside an EditForm and confirm the basic markup,
/// ARIA attributes, and edit/read-only mode switching all work after the EditControlBase refactor.
/// </summary>
public class ControlSmokeTests : TestContext
{
    /// <summary>
    /// Wraps an inner render fragment in an EditForm so the controls get the cascading EditContext.
    /// Programmatic component construction also requires <c>ValueExpression</c> explicitly — Blazor's
    /// <c>@bind-Value</c> macro normally synthesizes it from the markup but we don't have that luxury here.
    /// </summary>
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

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
    public void ReadOnlyValue_omits_aria_labelledby_when_the_label_is_hidden()
    {
        // No FormLabel renders (lbl-Name absent), so the read-only value must not dangle aria-labelledby.
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

        Assert.False(cut.Find(".edit-readonly-value").HasAttribute("aria-labelledby"));
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
