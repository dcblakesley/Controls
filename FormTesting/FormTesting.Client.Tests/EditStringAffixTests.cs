using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditString's slice-S2 AntD-parity parameters (Prefix/Suffix/AllowClear/MaxLength/
/// ShowCount/IsPassword) — the DOM-stability regression for the no-new-params case already lives in
/// <see cref="EditInputShellTests"/> (slice S1) and is re-run by the same test pass; this file only
/// adds coverage for the new affix-mode surface.
/// </summary>
public class EditStringAffixTests : TestContext
{
    public EditStringAffixTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate Clear()'s ElementReference.FocusAsync

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void With_no_new_params_the_input_has_no_type_attribute_and_stays_in_legacy_mode()
    {
        // IsPassword defaults to false -- the input must keep today's unspecified (text) type, not
        // gain an explicit type="text", so the DOM stays byte-identical for non-users of the feature.
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
        Assert.False(input.HasAttribute("type"));
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper"));
        Assert.NotEmpty(cut.FindAll(".edit-input-with-icon"));
    }

    [Theory]
    [InlineData("Alice", false, true)]  // non-empty + enabled -> visible
    [InlineData("", false, false)]      // empty -> hidden
    [InlineData("Alice", true, false)]  // disabled -> hidden
    public void AllowClear_button_appears_only_when_value_non_empty_and_enabled(string value, bool disabled, bool expectVisible)
    {
        var model = new PersonModel { Name = value };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.AddAttribute(5, "IsDisabled", disabled);
            b.CloseComponent();
        }));

        Assert.Equal(expectVisible, cut.FindAll(".edit-input-clear").Count == 1);
        // AllowClear alone still switches the shell into affix mode (so the box never resizes as the
        // user types down to empty), regardless of whether the button itself is showing right now.
        Assert.NotEmpty(cut.FindAll(".edit-input-affix-wrapper"));
    }

    [Fact]
    public void AllowClear_click_clears_the_bound_value_and_the_button_disappears()
    {
        var model = new PersonModel { Name = "Alice" };
        string? captured = "Alice";
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "AllowClear", true);
            b.AddAttribute(6, "ShowCount", true);
            b.CloseComponent();
        }));

        Assert.Equal("5", cut.Find(".edit-input-count").TextContent);

        cut.Find(".edit-input-clear").Click();

        Assert.Null(captured);
        Assert.Equal("0", cut.Find(".edit-input-count").TextContent);
        Assert.Empty(cut.FindAll(".edit-input-clear")); // value is now empty -- button withdraws
    }

    [Fact]
    public void MaxLength_renders_the_maxlength_attribute_and_omits_it_when_null()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;

        var withMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaxLength", 10);
            b.CloseComponent();
        }));
        Assert.Equal("10", withMax.Find("input.edit-string-input").GetAttribute("maxlength"));

        var withoutMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        Assert.False(withoutMax.Find("input.edit-string-input").HasAttribute("maxlength"));
    }

    [Fact]
    public void ShowCount_formats_with_and_without_MaxLength_and_updates_as_the_value_changes()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;

        var withoutMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));
        Assert.Equal("5", withoutMax.Find(".edit-input-count").TextContent);

        withoutMax.Find("input.edit-string-input").Input("Alicia");
        Assert.Equal("6", withoutMax.Find(".edit-input-count").TextContent);

        var withMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name); // fresh render -- model.Name is still "Alice" (no ValueChanged wired above)
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.AddAttribute(5, "MaxLength", 20);
            b.CloseComponent();
        }));
        Assert.Equal("5 / 20", withMax.Find(".edit-input-count").TextContent);
    }

    [Fact]
    public void IsPassword_toggle_reveals_and_re_hides_the_value()
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

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
        var toggle = cut.Find(".edit-input-password-toggle");
        Assert.Equal("false", toggle.GetAttribute("aria-pressed"));
        Assert.Equal("Show value", toggle.GetAttribute("aria-label"));

        toggle.Click();

        Assert.Equal("text", cut.Find("input.edit-string-input").GetAttribute("type"));
        toggle = cut.Find(".edit-input-password-toggle");
        Assert.Equal("true", toggle.GetAttribute("aria-pressed"));
        Assert.Equal("Hide value", toggle.GetAttribute("aria-label"));

        toggle.Click();

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
        Assert.Equal("false", cut.Find(".edit-input-password-toggle").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Prefix_and_Suffix_render_fragments_land_in_the_shells_prefix_and_suffix_spans()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$")));
            b.AddAttribute(5, "Suffix", (RenderFragment)(rb => rb.AddContent(0, "USD")));
            b.CloseComponent();
        }));

        Assert.Equal("$", cut.Find(".edit-input-prefix").TextContent);
        Assert.Contains("USD", cut.Find(".edit-input-suffix").TextContent);
    }
}
