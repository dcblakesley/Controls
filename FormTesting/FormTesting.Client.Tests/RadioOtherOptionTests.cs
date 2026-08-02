using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// The two radio controls' "Other" free-text option, which used to behave in opposite ways on a
/// switch away from Other: <see cref="EditRadioString"/> wiped the typed text (an accidental
/// mis-click was unrecoverable) while <see cref="EditRadioEnum{TEnum}"/> never cleared it (a stale
/// OtherValue submitted attached to a non-Other choice). Both now preserve-but-don't-submit: the box
/// keeps showing the text, the model stops carrying it, and selecting Other again re-commits it.
/// Also covers the read-only "Other: " separator and the shared per-option wrapper element.
/// </summary>
public class RadioOtherOptionTests : BunitContext
{
    static IRenderedComponent<ContainerFragment> RenderRadioString(BunitContext ctx, PersonModel model, Action<string?> onChanged)
    {
        Expression<Func<string>> field = () => model.Name;
        return ctx.Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string?>(ctx, onChanged));
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.AddAttribute(5, "HasOther", true);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void EditRadioString_keeps_the_typed_other_text_on_screen_but_off_the_model_after_a_switch_away()
    {
        var model = new PersonModel { Name = "" };
        string? captured = null;
        var cut = RenderRadioString(this, model, v => captured = v);

        var otherRadio = cut.Find("#rb-Name-other");
        otherRadio.Change(otherRadio.GetAttribute("value"));
        cut.Find("#txt-Name-custom-value").Input("bespoke");
        Assert.Equal("bespoke", captured);

        // The mis-click: a real option takes over the bound value...
        cut.Find("#rb-Name-a").Change("a");
        Assert.Equal("a", captured);

        // ...but the typed text survives in the (now disabled) box, so it can be got back.
        var box = cut.Find("#txt-Name-custom-value");
        Assert.Equal("bespoke", box.GetAttribute("value"));
        Assert.True(box.HasAttribute("disabled"));

        otherRadio = cut.Find("#rb-Name-other");
        otherRadio.Change(otherRadio.GetAttribute("value"));
        Assert.Equal("bespoke", captured);
    }

    [Fact]
    public void EditRadioString_wraps_every_default_mode_option_in_the_shared_option_row()
    {
        // edit-controls.css documents .edit-radio-option as the per-option flex row, and
        // EditRadioEnum emits it for every option; this control emitted it only around its Other row.
        var model = new PersonModel { Name = "" };
        var cut = RenderRadioString(this, model, _ => { });

        var rows = cut.FindAll(".edit-radio-option");
        Assert.Equal(3, rows.Count); // two options + the Other row
        Assert.All(rows, row => Assert.NotNull(row.QuerySelector("input[type=radio]")));
        // The Other row keeps its long-standing consumer hook alongside the shared class.
        Assert.Single(cut.FindAll(".edit-radio-option.edit-radio-other-option-container"));
    }

    static IRenderedComponent<ContainerFragment> RenderRadioEnum(
        BunitContext ctx, PersonModel model, Action<Priority?> onValue, Action<string?> onOther, bool isEditMode = true, string? otherValue = "")
    {
        Expression<Func<Priority?>> field = () => model.Priority;
        return ctx.Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(ctx, onValue));
            b.AddAttribute(4, "HasOtherOption", true);
            b.AddAttribute(5, "OtherValue", otherValue);
            b.AddAttribute(6, "OtherValueChanged", EventCallback.Factory.Create<string?>(ctx, onOther));
            b.AddAttribute(7, "IsEditMode", isEditMode);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void EditRadioEnum_clears_the_other_text_from_the_model_but_keeps_showing_it_after_a_switch_away()
    {
        // Critical is the last enum value, so with HasOtherOption it IS the Other slot.
        var model = new PersonModel { Priority = Priority.Critical };
        Priority? capturedValue = null;
        string? capturedOther = "unset";
        var cut = RenderRadioEnum(this, model, v => capturedValue = v, v => capturedOther = v);

        cut.Find("input.edit-radio-other-input").Input("details");
        Assert.Equal("details", capturedOther);

        // Switching away must take the Other text off the model — it used to stay there, submitted
        // alongside a choice it doesn't belong to.
        cut.Find("#rb-Priority-Low").Change("Low");
        Assert.Equal(Priority.Low, capturedValue);
        Assert.Null(capturedOther);

        var box = cut.Find("input.edit-radio-other-input");
        Assert.Equal("details", box.GetAttribute("value")); // still visible, still recoverable
        Assert.True(box.HasAttribute("disabled"));

        cut.Find("#rb-Priority-Critical").Change("Critical");
        Assert.Equal("details", capturedOther); // re-committed
    }

    [Fact]
    public void EditRadioEnum_read_only_with_an_empty_other_text_renders_no_dangling_separator()
    {
        var model = new PersonModel { Priority = Priority.Critical };
        var cut = RenderRadioEnum(this, model, _ => { }, _ => { }, isEditMode: false);

        Assert.Equal("Critical", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void EditRadioEnum_read_only_with_an_other_text_still_renders_it_after_the_separator()
    {
        var model = new PersonModel { Priority = Priority.Critical };
        var cut = RenderRadioEnum(this, model, _ => { }, _ => { }, isEditMode: false, otherValue: "details");

        Assert.Equal("Critical: details", cut.Find(".edit-readonly-value").TextContent.Trim());
    }
}
