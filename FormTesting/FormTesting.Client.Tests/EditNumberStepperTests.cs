using System.ComponentModel;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit coverage for <see cref="EditNumber{T}"/>'s opt-in <c>ShowStepper</c> group. The arithmetic,
/// clamping, disabled-at-bound state and accessible names are all pure C#/DOM, so they belong here;
/// the real-click round trip and the group's visual baseline live in <c>EditNumberE2ETests</c>.
/// </summary>
public class EditNumberStepperTests : BunitContext
{
    class StepperModel
    {
        [DisplayName("Quantity")]
        public int Quantity { get; set; }

        public int? Optional { get; set; }

        [DisplayName("Amount")]
        public decimal Amount { get; set; }
    }

    IRenderedComponent<ContainerFragment> RenderQuantity(StepperModel model, Action<RenderTreeBuilder>? extra = null)
    {
        Expression<Func<int>> field = () => model.Quantity;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int>>(0);
            b.AddAttribute(1, "Value", model.Quantity);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int>(this, v => model.Quantity = v));
            extra?.Invoke(b);
            b.CloseComponent();
        }));
    }

    IRenderedComponent<ContainerFragment> RenderOptional(StepperModel model, Action<RenderTreeBuilder>? extra = null)
    {
        Expression<Func<int?>> field = () => model.Optional;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Optional);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => model.Optional = v));
            b.AddAttribute(4, "ShowStepper", true);
            extra?.Invoke(b);
            b.CloseComponent();
        }));
    }

    IRenderedComponent<ContainerFragment> RenderAmount(StepperModel model, Action<RenderTreeBuilder>? extra = null)
    {
        Expression<Func<decimal>> field = () => model.Amount;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal>>(0);
            b.AddAttribute(1, "Value", model.Amount);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<decimal>(this, v => model.Amount = v));
            b.AddAttribute(4, "ShowStepper", true);
            extra?.Invoke(b);
            b.CloseComponent();
        }));
    }

    static void Decrease(IRenderedComponent<ContainerFragment> cut) => cut.Find(".edit-number-step-down").Click();
    static void Increase(IRenderedComponent<ContainerFragment> cut) => cut.Find(".edit-number-step-up").Click();

    // ----- Opt-in --------------------------------------------------------------

    [Fact]
    public void Without_ShowStepper_no_group_wrapper_or_buttons_render_at_all()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model);

        Assert.Empty(cut.FindAll(".edit-number-stepper"));
        Assert.Empty(cut.FindAll("button"));
        // The editor itself is untouched -- still the legacy shell layout the default render has
        // always produced.
        Assert.NotEmpty(cut.FindAll(".edit-input-with-icon"));
        Assert.Equal("3", cut.Find("input.edit-number-input").GetAttribute("value"));
    }

    [Fact]
    public void ShowStepper_wraps_the_editor_in_a_group_with_a_button_on_each_side()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b => b.AddAttribute(4, "ShowStepper", true));

        var group = cut.Find(".edit-number-stepper");
        var children = group.Children;
        Assert.Equal(3, children.Length);
        Assert.Contains("edit-number-step-down", children[0].ClassList);
        Assert.Contains("edit-input-with-icon", children[1].ClassList); // the editor, unchanged
        Assert.Contains("edit-number-step-up", children[2].ClassList);
        // Not tab stops: the native arrow keys are the keyboard path (see ShowStepper's remarks).
        Assert.Equal("-1", children[0].GetAttribute("tabindex"));
        Assert.Equal("-1", children[2].GetAttribute("tabindex"));
        // type="button" so a press never submits the enclosing EditForm.
        Assert.Equal("button", children[0].GetAttribute("type"));
        Assert.Equal("button", children[2].GetAttribute("type"));
    }

    // ----- Stepping ------------------------------------------------------------

    [Fact]
    public void The_plus_and_minus_buttons_move_the_bound_value_by_one_step()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b => b.AddAttribute(4, "ShowStepper", true));

        Increase(cut);
        Assert.Equal(4, model.Quantity);

        Decrease(cut);
        Decrease(cut);
        Assert.Equal(2, model.Quantity);
    }

    [Fact]
    public void An_explicit_Step_is_the_amount_one_press_applies()
    {
        var model = new StepperModel { Quantity = 10 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Step", 5m);
        });

        Increase(cut);
        Assert.Equal(15, model.Quantity);
    }

    [Fact]
    public void A_fractional_Step_moves_a_decimal_field_by_that_fraction()
    {
        // step="any" would be what a bare decimal field renders, and "any" carries no number -- the
        // explicit Step is what makes a press worth 0.25 rather than the 1 that fallback produces.
        var model = new StepperModel { Amount = 1.5m };
        var cut = RenderAmount(model, b => b.AddAttribute(5, "Step", 0.25m));

        Increase(cut);
        Assert.Equal(1.75m, model.Amount);

        Decrease(cut);
        Decrease(cut);
        Assert.Equal(1.25m, model.Amount);
    }

    [Fact]
    public void With_no_Step_configured_at_all_a_press_is_worth_one()
    {
        // A decimal T renders step="any" (no number in it) -- the press still has to move by
        // something, and AntD's own handlers use 1.
        var model = new StepperModel { Amount = 2m };
        var cut = RenderAmount(model);

        Increase(cut);
        Assert.Equal(3m, model.Amount);
    }

    [Fact]
    public void A_null_value_steps_from_zero()
    {
        var model = new StepperModel { Optional = null };
        var cut = RenderOptional(model);

        Increase(cut);
        Assert.Equal(1, model.Optional);
    }

    [Fact]
    public void A_null_value_steps_down_from_zero_too()
    {
        var model = new StepperModel { Optional = null };
        var cut = RenderOptional(model);

        Decrease(cut);
        Assert.Equal(-1, model.Optional);
    }

    // ----- Clamping + disabled-at-bound ---------------------------------------

    [Fact]
    public void Stepping_clamps_to_Min_and_Max_rather_than_overshooting()
    {
        var model = new StepperModel { Quantity = 8 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Step", 5m);
            b.AddAttribute(6, "Min", 0m);
            b.AddAttribute(7, "Max", 10m);
        });

        Increase(cut); // 8 + 5 = 13, clamped
        Assert.Equal(10, model.Quantity);

        var low = new StepperModel { Quantity = 2 };
        var lowCut = RenderQuantity(low, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Step", 5m);
            b.AddAttribute(6, "Min", 0m);
            b.AddAttribute(7, "Max", 10m);
        });

        Decrease(lowCut); // 2 - 5 = -3, clamped
        Assert.Equal(0, low.Quantity);
    }

    [Fact]
    public void A_value_sitting_at_Max_disables_the_plus_button_only()
    {
        var model = new StepperModel { Quantity = 10 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Min", 0m);
            b.AddAttribute(6, "Max", 10m);
        });

        Assert.True(cut.Find(".edit-number-step-up").HasAttribute("disabled"));
        Assert.False(cut.Find(".edit-number-step-down").HasAttribute("disabled"));
    }

    [Fact]
    public void A_value_sitting_at_Min_disables_the_minus_button_only()
    {
        var model = new StepperModel { Quantity = 5 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Min", 5m);
            b.AddAttribute(6, "Max", 50m);
        });

        Assert.True(cut.Find(".edit-number-step-down").HasAttribute("disabled"));
        Assert.False(cut.Find(".edit-number-step-up").HasAttribute("disabled"));
    }

    [Fact]
    public void Reaching_a_bound_by_pressing_disables_that_button_on_the_next_render()
    {
        var model = new StepperModel { Quantity = 9 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Max", 10m);
        });
        Assert.False(cut.Find(".edit-number-step-up").HasAttribute("disabled"));

        Increase(cut);

        Assert.Equal(10, model.Quantity);
        Assert.True(cut.Find(".edit-number-step-up").HasAttribute("disabled"));
    }

    [Fact]
    public void Model_declared_MinValue_MaxValue_drive_the_bounds_too()
    {
        // The bounds resolve through EffectiveMin/EffectiveMax, so the model-attribute fallback the
        // min/max ATTRIBUTES already use governs the buttons as well -- no second resolution path.
        var model = new BoundedModel { Count = 4 };
        Expression<Func<int>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int>(this, v => model.Count = v));
            b.AddAttribute(4, "ShowStepper", true);
            b.CloseComponent();
        }));

        Increase(cut);
        Assert.Equal(4, model.Count); // already at [MaxValue(4)] -- the clamp is a no-op
        Assert.True(cut.Find(".edit-number-step-up").HasAttribute("disabled"));

        Decrease(cut);
        Decrease(cut);
        Decrease(cut);
        Assert.Equal(2, model.Count); // [MinValue(2)] floor
        Assert.True(cut.Find(".edit-number-step-down").HasAttribute("disabled"));
    }

    class BoundedModel
    {
        [MinValue(2)]
        [MaxValue(4)]
        public int Count { get; set; }
    }

    // ----- Disabled / read-only ------------------------------------------------

    [Fact]
    public void A_disabled_control_disables_both_buttons()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "IsDisabled", true);
        });

        Assert.True(cut.Find(".edit-number-step-down").HasAttribute("disabled"));
        Assert.True(cut.Find(".edit-number-step-up").HasAttribute("disabled"));
    }

    [Fact]
    public void Read_only_mode_renders_the_formatted_value_and_no_stepper_at_all()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "IsEditMode", false);
        });

        Assert.Empty(cut.FindAll(".edit-number-stepper"));
        Assert.Empty(cut.FindAll("button"));
        Assert.Equal("3", cut.Find(".edit-readonly-value").TextContent);
    }

    // ----- Accessible names + size --------------------------------------------

    [Fact]
    public void The_buttons_fold_the_fields_own_label_into_their_accessible_names()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b => b.AddAttribute(4, "ShowStepper", true));

        // [DisplayName("Quantity")] on the model -- a form with two stepper fields would otherwise
        // render two buttons both named "Decrease".
        Assert.Equal("Decrease Quantity", cut.Find(".edit-number-step-down").GetAttribute("aria-label"));
        Assert.Equal("Increase Quantity", cut.Find(".edit-number-step-up").GetAttribute("aria-label"));
        // The glyphs themselves stay out of the accessibility tree.
        Assert.Equal("true", cut.Find(".edit-number-step-down svg").GetAttribute("aria-hidden"));
        Assert.Equal("true", cut.Find(".edit-number-step-up svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void The_button_labels_are_overridable_for_localization()
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "DecreaseButtonLabel", "Moins");
            b.AddAttribute(6, "IncreaseButtonLabel", "Plus");
        });

        Assert.Equal("Moins", cut.Find(".edit-number-step-down").GetAttribute("aria-label"));
        Assert.Equal("Plus", cut.Find(".edit-number-step-up").GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(SelectSize.Default, null)]
    [InlineData(SelectSize.Small, "edit-input-sm")]
    [InlineData(SelectSize.Large, "edit-input-lg")]
    public void The_group_carries_the_same_size_token_the_editor_does(SelectSize size, string? expected)
    {
        var model = new StepperModel { Quantity = 3 };
        var cut = RenderQuantity(model, b =>
        {
            b.AddAttribute(4, "ShowStepper", true);
            b.AddAttribute(5, "Size", size);
        });

        var group = cut.Find(".edit-number-stepper");
        if (expected is null)
            Assert.Equal(["edit-number-stepper"], group.ClassList.ToArray());
        else
            Assert.Contains(expected, group.ClassList);
    }

    [Fact]
    public void The_stepper_composes_with_the_affix_layout_a_Prefix_switches_on()
    {
        var model = new StepperModel { Amount = 5m };
        var cut = RenderAmount(model, b => b.AddAttribute(5, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$"))));

        var group = cut.Find(".edit-number-stepper");
        Assert.Contains("edit-input-affix-wrapper", group.Children[1].ClassList);
        Assert.Equal("$", cut.Find(".edit-input-prefix").TextContent);

        Increase(cut);
        Assert.Equal(6m, model.Amount);
    }
}
