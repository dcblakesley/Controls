using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Byte-identical-markup coverage for EditString's read-only <c>MaskText</c> view (finding 68 of the
/// 2026-07-30 audit): the two masked/revealed branches used to be copy-pasted 8-line blocks, collapsed
/// here into a local <c>MaskedValueRow</c> RenderFragment. These tests pin down the exact markup shape
/// (id/aria wiring, button label/class, icon glyph) that existed before the collapse, plus the toggle
/// interaction, so the DRY refactor can't silently change behavior.
/// </summary>
public class EditStringMaskedValueTests : BunitContext
{
    IRenderedComponent<ContainerFragment> RenderMasked(PersonModel model, Expression<Func<string>> field) =>
        Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

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
}
