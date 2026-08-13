using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Accessibility-audit coverage for the radio/boolean family (<see cref="EditRadio{TValue}"/>,
/// <see cref="EditRadioEnum{TEnum}"/>, <see cref="EditRadioString"/>, <see cref="EditBool"/>,
/// <see cref="EditBoolNullRadio"/>) -- the RAD-1 through RAD-8 findings from the 2026-08 audit wave.
/// RAD-6 (checked segmented button distinguished by color alone) is CSS-only and has no test here.
/// </summary>
public class A11yRadioBoolTests : BunitContext
{
    // ----- RAD-1: a null/empty EditRadioString option must still get a real accessible name --------

    [Fact]
    public void EditRadioString_blank_option_gets_a_visible_accessible_placeholder()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "" });
            b.CloseComponent();
        }));

        var labels = cut.FindAll("label.edit-radio-label");
        Assert.Equal(2, labels.Count);
        // The blank entry must still announce something -- not an empty accessible name.
        Assert.Contains("(blank)", labels[1].TextContent);
        // The radio's own bound VALUE is untouched -- only the rendered label text gets a fallback.
        var radios = cut.FindAll("input[type=radio]");
        Assert.Equal("", radios[1].GetAttribute("value"));
    }

    [Fact]
    public void EditRadioString_non_blank_options_are_unaffected_by_the_placeholder_fallback()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        var labels = cut.FindAll("label.edit-radio-label");
        Assert.DoesNotContain("(blank)", labels[0].TextContent);
        Assert.DoesNotContain("(blank)", labels[1].TextContent);
    }

    // ----- RAD-2: IsOptionDisabled targeting the selected option must not strand the whole group ----

    [Fact]
    public void EditRadioEnum_IsOptionDisabled_on_the_selected_value_keeps_it_natively_focusable()
    {
        var model = new PersonModel { Priority = Priority.High };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsOptionDisabled", (Func<Priority?, bool>)(p => p == Priority.High));
            b.CloseComponent();
        }));

        var high = cut.FindAll("input[type=radio]").First(r => r.GetAttribute("value") == "High");
        // RAD-2: a native `disabled` on the checked radio would strand the WHOLE group out of the Tab
        // sequence (roving tabindex gives the group's one native stop to the checked radio, and no
        // other radio takes over). It must stay natively focusable, communicating "locked" via
        // aria-disabled instead.
        Assert.False(high.HasAttribute("disabled"));
        Assert.Equal("true", high.GetAttribute("aria-disabled"));
    }

    [Fact]
    public void EditRadioEnum_group_IsDisabled_still_natively_disables_the_selected_option()
    {
        // The whole-group switch is a different story from a targeted per-option predicate: disabling
        // the entire group is expected to drop it out of the Tab sequence, same as any other disabled
        // control, so it must still natively disable every option including the selected one.
        var model = new PersonModel { Priority = Priority.High };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsDisabled", true);
            b.CloseComponent();
        }));

        var high = cut.FindAll("input[type=radio]").First(r => r.GetAttribute("value") == "High");
        Assert.True(high.HasAttribute("disabled"));
        Assert.False(high.HasAttribute("aria-disabled"));
    }

    [Fact]
    public void EditRadioString_IsOptionDisabled_on_the_selected_option_keeps_it_natively_focusable()
    {
        var model = new PersonModel { Name = "b" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsOptionDisabled", (Func<string, bool>)(o => o == "b"));
            b.CloseComponent();
        }));

        var selected = cut.Find("#rb-Name-b");
        Assert.False(selected.HasAttribute("disabled"));
        Assert.Equal("true", selected.GetAttribute("aria-disabled"));

        var notSelected = cut.Find("#rb-Name-a");
        Assert.False(notSelected.HasAttribute("aria-disabled"));
    }

    // ----- RAD-3: horizontal groups must announce their orientation ---------------------------------

    [Fact]
    public void EditRadioEnum_IsHorizontal_emits_aria_orientation_horizontal()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsHorizontal", true);
            b.CloseComponent();
        }));

        Assert.Equal("horizontal", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void EditRadioEnum_default_layout_omits_aria_orientation()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-radio-fieldset").HasAttribute("aria-orientation"));
    }

    [Fact]
    public void EditRadioString_Button_mode_emits_aria_orientation_horizontal_even_without_IsHorizontal()
    {
        // Button mode is inherently horizontal regardless of the IsHorizontal flag -- the fieldset
        // must say so even when the consumer never set IsHorizontal.
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.CloseComponent();
        }));

        Assert.Equal("horizontal", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void EditRadio_IsHorizontal_emits_aria_orientation_horizontal()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsHorizontal", true);
            b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.Equal("horizontal", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void EditRadio_default_layout_omits_aria_orientation()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-radio-fieldset").HasAttribute("aria-orientation"));
    }

    [Fact]
    public void EditBoolNullRadio_defaults_to_aria_orientation_horizontal()
    {
        // IsHorizontal defaults true on this control (unlike the other three).
        var model = new PersonModel { IsSubscribed = true };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("horizontal", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("aria-orientation"));
    }

    [Fact]
    public void EditBoolNullRadio_IsHorizontal_false_omits_aria_orientation()
    {
        var model = new PersonModel { IsSubscribed = true };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsHorizontal", false);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-radio-fieldset").HasAttribute("aria-orientation"));
    }

    // ----- naming-anchor retarget: EditBoolNullRadio's fieldset + EditBool's checkbox ----------------

    [Fact]
    public void EditBoolNullRadio_fieldset_aria_labelledby_targets_the_label_text_anchor()
    {
        var model = new PersonModel { IsSubscribed = true };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("lbltext-IsSubscribed", fieldset.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#" + fieldset.GetAttribute("aria-labelledby")));
    }

    [Fact]
    public void EditBool_checkbox_aria_labelledby_targets_the_label_text_anchor_not_the_tooltip_trigger()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Tooltip", "More info");
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.Equal("lbltext-IsActive", checkbox.GetAttribute("aria-labelledby"));
        var anchor = cut.Find("#" + checkbox.GetAttribute("aria-labelledby"));
        Assert.NotNull(anchor);
        // The naming anchor is the label TEXT only -- the tooltip trigger's own content must not fold
        // into the checkbox's accessible name.
        Assert.DoesNotContain("More info", anchor.TextContent);
    }

    // ----- RAD-4: the "Other" box's accessible name must be overridable -----------------------------

    [Fact]
    public void EditRadioEnum_OtherAriaLabel_overrides_the_default()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.AddAttribute(4, "OtherAriaLabel", "Custom priority reason");
            b.CloseComponent();
        }));

        Assert.Equal("Custom priority reason", cut.Find("input.edit-radio-other-input").GetAttribute("aria-label"));
    }

    [Fact]
    public void EditRadioString_OtherAriaLabel_overrides_the_default()
    {
        var model = new PersonModel { Name = "bespoke" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "HasOther", true);
            b.AddAttribute(5, "OtherAriaLabel", "Custom name reason");
            b.CloseComponent();
        }));

        Assert.Equal("Custom name reason", cut.Find("input.edit-radio-other-input").GetAttribute("aria-label"));
    }

    // ----- RAD-5: EditRadio's inner disable-scope fieldset must not be a second unnamed group -------

    [Fact]
    public void EditRadio_disable_scope_fieldset_carries_role_presentation()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.Equal("presentation", cut.Find("fieldset.edit-radio-disable-scope").GetAttribute("role"));
    }

    // ----- RAD-7: an empty EditRadioString with nothing to own must not claim role="radiogroup" -----

    [Fact]
    public void EditRadioString_empty_options_and_no_other_omits_the_radiogroup_role()
    {
        var model = new PersonModel { Name = "" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string>());
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-radio-fieldset").HasAttribute("role"));
    }

    [Fact]
    public void EditRadioString_empty_options_with_HasOther_keeps_the_radiogroup_role()
    {
        var model = new PersonModel { Name = "" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string>());
            b.AddAttribute(4, "HasOther", true);
            b.CloseComponent();
        }));

        Assert.Equal("radiogroup", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("role"));
    }

    [Fact]
    public void EditRadioString_non_empty_options_keeps_the_radiogroup_role()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        Assert.Equal("radiogroup", cut.Find("fieldset.edit-radio-fieldset").GetAttribute("role"));
    }

    // ----- RAD-8: a focused "Other" box must not be force-blurred by an external disable -------------

    [Fact]
    public void RadioOtherInput_focused_when_IsDisabled_flips_true_stays_natively_focusable()
    {
        // Other is selected (Critical is the last enum value/"Other" slot here), so the box starts
        // enabled. Focus it, then switch the selection away WHILE it still has focus -- an external
        // change from the box's own point of view -- and confirm it doesn't pick up native `disabled`
        // (which would force an unconditional browser blur to <body>) until it loses focus on its own.
        var model = new PersonModel { Priority = Priority.Critical };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.CloseComponent();
        }));

        cut.Find("input.edit-radio-other-input").Focus();
        cut.Find("#rb-Priority-Low").Change("Low");

        var box = cut.Find("input.edit-radio-other-input");
        Assert.False(box.HasAttribute("disabled")); // stays focusable despite the external disable
        Assert.True(box.HasAttribute("readonly"));
        Assert.Equal("true", box.GetAttribute("aria-disabled"));

        box.Blur();

        box = cut.Find("input.edit-radio-other-input");
        Assert.True(box.HasAttribute("disabled")); // now safe -- the user already moved on
        Assert.False(box.HasAttribute("readonly"));
    }

    [Fact]
    public void RadioOtherInput_never_focused_disables_natively_as_before()
    {
        // Regression guard: every pre-existing test relies on the box going natively `disabled` when
        // it was never focused in the first place (bUnit never simulates ambient focus on its own).
        var model = new PersonModel { Priority = Priority.Critical };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.CloseComponent();
        }));

        cut.Find("#rb-Priority-Low").Change("Low");

        var box = cut.Find("input.edit-radio-other-input");
        Assert.True(box.HasAttribute("disabled"));
        Assert.False(box.HasAttribute("readonly"));
    }
}
