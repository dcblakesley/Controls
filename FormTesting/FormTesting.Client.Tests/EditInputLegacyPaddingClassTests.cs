using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// EditString/EditTextArea/EditNumber's legacy-mode trailing padding (finding 71 of the 2026-07-30
/// audit): the inline <c>style="padding-inline-end: 2rem"</c> these three used to hand-duplicate is
/// now the <c>edit-input-legacy-padding</c> class (edit-controls.css), carried in legacy mode and
/// dropped in favor of <c>edit-affix-input</c> once any affix parameter switches the shell into affix
/// mode -- the two are mutually exclusive on the editor element in both directions.
/// </summary>
public class EditInputLegacyPaddingClassTests : BunitContext
{
    [Fact]
    public void EditString_carries_the_padding_class_in_legacy_mode_and_drops_it_in_affix_mode()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;

        var legacy = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        var legacyInput = legacy.Find("input.edit-string-input");
        Assert.Contains("edit-input-legacy-padding", legacyInput.ClassList);
        Assert.DoesNotContain("edit-affix-input", legacyInput.ClassList);
        Assert.False(legacyInput.HasAttribute("style"));

        var affix = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();
        }));
        var affixInput = affix.Find("input.edit-string-input");
        Assert.Contains("edit-affix-input", affixInput.ClassList);
        Assert.DoesNotContain("edit-input-legacy-padding", affixInput.ClassList);
        Assert.False(affixInput.HasAttribute("style"));
    }

    [Fact]
    public void EditNumber_carries_the_padding_class_in_legacy_mode_and_drops_it_in_affix_mode()
    {
        var model = new PersonModel { Price = 19.99m };
        Expression<Func<decimal?>> field = () => model.Price;

        var legacy = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        var legacyInput = legacy.Find("input.edit-number-input");
        Assert.Contains("edit-input-legacy-padding", legacyInput.ClassList);
        Assert.DoesNotContain("edit-affix-input", legacyInput.ClassList);

        var affix = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$")));
            b.CloseComponent();
        }));
        var affixInput = affix.Find("input.edit-number-input");
        Assert.Contains("edit-affix-input", affixInput.ClassList);
        Assert.DoesNotContain("edit-input-legacy-padding", affixInput.ClassList);
    }

    [Fact]
    public void EditTextArea_carries_the_padding_class_in_legacy_mode_and_drops_it_in_affix_mode()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;

        var legacy = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        var legacyTextArea = legacy.Find("textarea.edit-textarea-input");
        Assert.Contains("edit-input-legacy-padding", legacyTextArea.ClassList);
        Assert.DoesNotContain("edit-affix-input", legacyTextArea.ClassList);
        Assert.False(legacyTextArea.HasAttribute("style"));

        var affix = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();
        }));
        var affixTextArea = affix.Find("textarea.edit-textarea-input");
        Assert.Contains("edit-affix-input", affixTextArea.ClassList);
        Assert.DoesNotContain("edit-input-legacy-padding", affixTextArea.ClassList);
    }
}
