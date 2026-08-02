using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Byte-identical-markup coverage for EditBoolNullRadio's three radio options (finding 69 of the
/// 2026-07-30 audit): the false/true/null-option blocks used to be near-identical 11-line copies,
/// collapsed here into a local <c>RadioOption(bool? value, string idSuffix, string text)</c> fragment.
/// Pins down the exact attributes (id/data-test-id/name/value/checked/class/disabled) each option
/// renders, so the collapse can't silently drop or reorder one.
/// </summary>
public class EditBoolNullRadioMarkupTests : BunitContext
{
    [Fact]
    public void Each_option_renders_the_expected_name_value_id_and_class()
    {
        var model = new PersonModel { IsSubscribed = true };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.AddAttribute(5, "class", "my-class");
            b.CloseComponent();
        }));

        var falseInput = cut.Find("#rb-IsSubscribed-false");
        Assert.Equal("radio", falseInput.GetAttribute("type"));
        Assert.Equal("IsSubscribed", falseInput.GetAttribute("name"));
        Assert.Equal("false", falseInput.GetAttribute("value"));
        Assert.False(falseInput.HasAttribute("checked"));
        Assert.Equal("rb-IsSubscribed-false", falseInput.GetAttribute("data-test-id"));
        Assert.Contains("edit-radio-input", falseInput.GetAttribute("class")!);
        Assert.Contains("my-class", falseInput.GetAttribute("class")!);
        Assert.True(falseInput.HasAttribute("disabled"));

        var trueInput = cut.Find("#rb-IsSubscribed-true");
        Assert.Equal("true", trueInput.GetAttribute("value"));
        Assert.True(trueInput.HasAttribute("checked")); // Value is true
        Assert.Equal("rb-IsSubscribed-true", trueInput.GetAttribute("data-test-id"));

        var noneInput = cut.Find("#rb-IsSubscribed-none");
        Assert.Equal("", noneInput.GetAttribute("value"));
        Assert.False(noneInput.HasAttribute("checked"));
        Assert.Equal("rb-IsSubscribed-none", noneInput.GetAttribute("data-test-id"));
    }

    [Fact]
    public void The_null_option_is_checked_only_when_the_value_is_null()
    {
        var model = new PersonModel { IsSubscribed = null };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.True(cut.Find("#rb-IsSubscribed-none").HasAttribute("checked"));
        Assert.False(cut.Find("#rb-IsSubscribed-false").HasAttribute("checked"));
        Assert.False(cut.Find("#rb-IsSubscribed-true").HasAttribute("checked"));
    }

    [Fact]
    public void ShowNullOption_false_omits_only_the_null_radio()
    {
        var model = new PersonModel { IsSubscribed = false };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowNullOption", false);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll("#rb-IsSubscribed-false"));
        Assert.NotEmpty(cut.FindAll("#rb-IsSubscribed-true"));
        Assert.Empty(cut.FindAll("#rb-IsSubscribed-none"));
    }
}
