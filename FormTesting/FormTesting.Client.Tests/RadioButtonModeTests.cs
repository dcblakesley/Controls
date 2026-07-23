using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// <c>OptionType="Button"</c> on <see cref="EditRadioString"/> and <see cref="EditRadioEnum{TEnum}"/> —
/// Ant Design's segmented "button" radio look. Covers default-mode DOM stability (no new params used),
/// button-mode rendering/classes, <c>ButtonStyle</c>/<c>Size</c> class composition, <c>IsHorizontal</c>
/// being ignored, <c>HasOther</c>/<c>HasOtherOption</c> composition, and that selection still works.
/// </summary>
public class RadioButtonModeTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ----- EditRadioString: default-mode DOM stability -----------------------------------------

    [Fact]
    public void EditRadioString_OptionType_unused_renders_the_legacy_container_with_no_button_classes()
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

        Assert.NotEmpty(cut.FindAll(".edit-radio-buttons-container-vertical"));
        Assert.Empty(cut.FindAll(".edit-radio-button-group"));
        Assert.Empty(cut.FindAll(".edit-radio-button-wrap"));
        Assert.Empty(cut.FindAll("input.edit-radio-button-input"));
        Assert.All(cut.FindAll("input[type=radio]"), r => Assert.Contains("edit-radio-input", r.GetAttribute("class")!));
    }

    // ----- EditRadioString: button mode ----------------------------------------------------------

    [Fact]
    public void EditRadioString_button_mode_renders_a_button_group_with_hidden_inputs_and_button_labels()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.CloseComponent();
        }));

        Assert.Single(cut.FindAll(".edit-radio-button-group"));
        var wraps = cut.FindAll(".edit-radio-button-wrap");
        Assert.Equal(3, wraps.Count);
        Assert.Equal(3, cut.FindAll("input.edit-radio-button-input").Count);
        var labels = cut.FindAll("label.edit-radio-button");
        Assert.Equal(3, labels.Count);

        // Native for/id association -- not :has(), matching the styled-checkbox technique.
        var firstInput = cut.Find("input.edit-radio-button-input");
        Assert.Equal(firstInput.Id, labels[0].GetAttribute("for"));

        // Default (Outline) style adds no solid modifier class.
        Assert.Empty(cut.FindAll(".edit-radio-button-group-solid"));
    }

    [Fact]
    public void EditRadioString_button_mode_ignores_IsHorizontal()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "IsHorizontal", false); // still renders the button-group layout, not vertical
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-radio-button-group"));
        Assert.Empty(cut.FindAll(".edit-radio-buttons-container-horizontal"));
        Assert.Empty(cut.FindAll(".edit-radio-buttons-container-vertical"));
    }

    [Fact]
    public void EditRadioString_button_mode_ButtonStyle_Solid_adds_the_solid_modifier_class()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "ButtonStyle", RadioButtonStyle.Solid);
            b.CloseComponent();
        }));

        Assert.Contains("edit-radio-button-group-solid", cut.Find(".edit-radio-button-group").ClassList);
    }

    [Theory]
    [InlineData(SelectSize.Default, null)]
    [InlineData(SelectSize.Small, "edit-radio-button-group-sm")]
    [InlineData(SelectSize.Large, "edit-radio-button-group-lg")]
    public void EditRadioString_button_mode_Size_maps_to_the_expected_group_class(SelectSize size, string? expectedClass)
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "Size", size);
            b.CloseComponent();
        }));

        var group = cut.Find(".edit-radio-button-group");
        if (expectedClass is null)
        {
            Assert.DoesNotContain("edit-radio-button-group-sm", group.ClassList);
            Assert.DoesNotContain("edit-radio-button-group-lg", group.ClassList);
        }
        else
        {
            Assert.Contains(expectedClass, group.ClassList);
        }
    }

    [Fact]
    public void EditRadioString_button_mode_HasOther_joins_the_button_row_with_a_separate_text_input_below()
    {
        var model = new PersonModel { Name = "" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "HasOther", true);
            b.CloseComponent();
        }));

        // 3 buttons total: a, b, Other.
        Assert.Equal(3, cut.FindAll(".edit-radio-button-wrap").Count);
        Assert.Contains(cut.FindAll("label.edit-radio-button"), l => l.TextContent.Trim() == "Other");
        // The Other free-text input still renders as a normal input, outside the button group.
        var textInput = cut.Find("#txt-Name-custom-value");
        Assert.DoesNotContain(textInput, cut.Find(".edit-radio-button-group").Children);
    }

    [Fact]
    public void EditRadioString_button_mode_IsOptionDisabled_disables_the_matching_hidden_input()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "IsOptionDisabled", (Func<string, bool>)(o => o == "b"));
            b.CloseComponent();
        }));

        var inputs = cut.FindAll("input.edit-radio-button-input");
        Assert.False(inputs.First(i => i.GetAttribute("value") == "a").HasAttribute("disabled"));
        Assert.True(inputs.First(i => i.GetAttribute("value") == "b").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadioString_button_mode_selecting_an_option_still_updates_the_bound_value()
    {
        var model = new PersonModel { Name = "a" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "OptionType", RadioOptionType.Button);
            b.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.CloseComponent();
        }));

        cut.FindAll("input.edit-radio-button-input").First(i => i.GetAttribute("value") == "b").Change("b");
        Assert.Equal("b", captured);
    }

    // ----- EditRadioEnum: button mode --------------------------------------------------------------

    [Fact]
    public void EditRadioEnum_OptionType_unused_renders_the_legacy_container_with_no_button_classes()
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

        Assert.Empty(cut.FindAll(".edit-radio-button-group"));
        Assert.NotEmpty(cut.FindAll(".edit-radio-option"));
    }

    [Fact]
    public void EditRadioEnum_button_mode_renders_one_button_per_enum_value()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "OptionType", RadioOptionType.Button);
            b.CloseComponent();
        }));

        // Priority has 4 values.
        Assert.Equal(4, cut.FindAll(".edit-radio-button-wrap").Count);
        Assert.Equal(4, cut.FindAll("input.edit-radio-button-input").Count);
    }

    [Fact]
    public void EditRadioEnum_button_mode_HasOtherOption_keeps_the_free_text_input_outside_the_group()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "OptionType", RadioOptionType.Button);
            b.AddAttribute(4, "HasOtherOption", true);
            b.CloseComponent();
        }));

        Assert.Equal(4, cut.FindAll(".edit-radio-button-wrap").Count);
        var textInput = cut.Find("input.edit-radio-other-input");
        Assert.DoesNotContain(textInput, cut.Find(".edit-radio-button-group").Children);
    }

    [Fact]
    public void EditRadioEnum_button_mode_IsOptionDisabled_disables_the_matching_hidden_input()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "OptionType", RadioOptionType.Button);
            b.AddAttribute(4, "IsOptionDisabled", (Func<Priority?, bool>)(p => p == Priority.High));
            b.CloseComponent();
        }));

        var inputs = cut.FindAll("input.edit-radio-button-input");
        Assert.True(inputs.First(i => i.GetAttribute("value") == "High").HasAttribute("disabled"));
        Assert.False(inputs.First(i => i.GetAttribute("value") == "Low").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadioEnum_button_mode_selecting_an_option_still_updates_the_bound_value()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Priority? captured = null;
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "OptionType", RadioOptionType.Button);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => captured = v));
            b.CloseComponent();
        }));

        cut.FindAll("input.edit-radio-button-input").First(i => i.GetAttribute("value") == "High").Change("High");
        Assert.Equal(Priority.High, captured);
    }
}
