using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// <c>UseStyledCheckbox</c>'s resolution chain, using <see cref="EditBool"/> as the exemplar —
/// <see cref="EditCheckedStringList"/>, <see cref="EditCheckedEnumList{TEnum}"/>, and the UI-kit
/// <c>Table</c> all resolve through the same <see cref="EditControlInit.UseStyledCheckbox"/> helper
/// (Table minus the <see cref="FormOptions"/> tier — it has no FormOptions of its own). Verifies:
/// per-control parameter → <see cref="FormOptions.UseStyledCheckbox"/> → cascaded
/// <see cref="FormDefaults"/> → the <see cref="FormOptions.DefaultUseStyledCheckbox"/> static. The
/// static itself is deliberately never mutated here — it's process-wide, and xUnit runs test classes
/// in parallel (mirrors FormDefaultsTests' approach).
/// </summary>
public class UseStyledCheckboxTests : TestContext
{
    // EditForm(model) -> [CascadingValue<FormOptions>] -> [FormDefaults] -> EditBool(IsActive).
    IRenderedFragment RenderCheckbox(bool? perControl, FormOptions? formOptions, bool? formDefaults)
    {
        var model = new PersonModel();
        Expression<Func<bool>> field = () => model.IsActive;
        RenderFragment control = b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            if (perControl is not null)
                b.AddAttribute(3, "UseStyledCheckbox", perControl);
            b.CloseComponent();
        };
        var inner = control;
        if (formOptions is not null)
        {
            var next = inner;
            inner = b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", next);
                b.CloseComponent();
            };
        }
        if (formDefaults is not null)
        {
            var next = inner;
            inner = b =>
            {
                b.OpenComponent<FormDefaults>(0);
                b.AddAttribute(1, "UseStyledCheckbox", formDefaults);
                b.AddAttribute(2, "ChildContent", next);
                b.CloseComponent();
            };
        }
        var content = inner;
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "Model", model);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content));
            b.CloseComponent();
        });
    }

    static bool IsStyled(IRenderedFragment cut) => cut.FindAll(".edit-checkbox-wrap").Count > 0;

    [Fact]
    public void Nothing_set_renders_the_native_checkbox()
    {
        var cut = RenderCheckbox(perControl: null, formOptions: null, formDefaults: null);

        Assert.False(IsStyled(cut));
        Assert.NotEmpty(cut.FindAll("input.edit-input"));
    }

    [Fact]
    public void Instance_parameter_true_wins_even_when_FormOptions_and_FormDefaults_say_false()
    {
        var formOptions = new FormOptions { UseStyledCheckbox = false };
        var cut = RenderCheckbox(perControl: true, formOptions, formDefaults: false);

        Assert.True(IsStyled(cut));
    }

    [Fact]
    public void Instance_parameter_false_wins_even_when_FormOptions_and_FormDefaults_say_true()
    {
        var formOptions = new FormOptions { UseStyledCheckbox = true };
        var cut = RenderCheckbox(perControl: false, formOptions, formDefaults: true);

        Assert.False(IsStyled(cut));
    }

    [Fact]
    public void FormOptions_true_styles_it_when_there_is_no_instance_override()
    {
        var formOptions = new FormOptions { UseStyledCheckbox = true };
        var cut = RenderCheckbox(perControl: null, formOptions, formDefaults: null);

        Assert.True(IsStyled(cut));
    }

    [Fact]
    public void FormDefaults_true_styles_it_when_FormOptions_leaves_it_unset()
    {
        // A form that cascades FormOptions (as most do, for field registration) but leaves
        // UseStyledCheckbox null must still pick up the tree-level default.
        var cut = RenderCheckbox(perControl: null, formOptions: new FormOptions(), formDefaults: true);

        Assert.True(IsStyled(cut));
    }

    [Fact]
    public void FormOptions_instance_value_wins_over_cascaded_FormDefaults()
    {
        var formOptions = new FormOptions { UseStyledCheckbox = false };
        var cut = RenderCheckbox(perControl: null, formOptions, formDefaults: true);

        Assert.False(IsStyled(cut));
    }
}
