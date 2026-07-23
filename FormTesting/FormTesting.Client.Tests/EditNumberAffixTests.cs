using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditNumber's slice-S3 AntD-parity parameters (Min/Max/Placeholder/Prefix/Suffix) — the
/// DOM-stability regression for the no-new-params case already lives in
/// <see cref="EditInputShellTests"/> (slice S1) and is re-run by the same test pass; this file only
/// adds coverage for the new surface. EditNumber does not get AllowClear/ShowCount/IsPassword (see
/// the master spec's D3 EditNumber bullet) -- there's no clear/count/password toggle to test here.
/// </summary>
public class EditNumberAffixTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void With_no_new_params_the_input_has_no_min_max_or_placeholder_and_stays_in_legacy_mode()
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

        var input = cut.Find("input.edit-number-input");
        Assert.False(input.HasAttribute("min"));
        Assert.False(input.HasAttribute("max"));
        Assert.False(input.HasAttribute("placeholder"));
        Assert.DoesNotContain("edit-affix-input", input.ClassList);
        Assert.Equal("padding-inline-end: 2rem", input.GetAttribute("style"));
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper"));
        Assert.NotEmpty(cut.FindAll(".edit-input-with-icon"));
    }

    [Fact]
    public void Min_and_Max_render_InvariantCulture_attributes_and_are_omitted_when_null()
    {
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;

        var withBounds = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Min", 1m);
            b.AddAttribute(5, "Max", 120m);
            b.CloseComponent();
        }));
        var input = withBounds.Find("input.edit-number-input");
        Assert.Equal("1", input.GetAttribute("min"));
        Assert.Equal("120", input.GetAttribute("max"));

        var withoutBounds = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        var plain = withoutBounds.Find("input.edit-number-input");
        Assert.False(plain.HasAttribute("min"));
        Assert.False(plain.HasAttribute("max"));
    }

    [Fact]
    public void Placeholder_renders_the_placeholder_attribute()
    {
        var model = new PersonModel { Age = null };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Placeholder", "Enter your age");
            b.CloseComponent();
        }));

        Assert.Equal("Enter your age", cut.Find("input.edit-number-input").GetAttribute("placeholder"));
    }

    [Fact]
    public void Prefix_and_Suffix_render_fragments_land_in_the_shells_prefix_and_suffix_spans()
    {
        var model = new PersonModel { Price = 19.99m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$")));
            b.AddAttribute(5, "Suffix", (RenderFragment)(rb => rb.AddContent(0, "USD")));
            b.CloseComponent();
        }));

        Assert.Equal("$", cut.Find(".edit-input-prefix").TextContent);
        Assert.Contains("USD", cut.Find(".edit-input-suffix").TextContent);
        Assert.Contains("edit-affix-input", cut.Find("input.edit-number-input").ClassList);
    }
}
