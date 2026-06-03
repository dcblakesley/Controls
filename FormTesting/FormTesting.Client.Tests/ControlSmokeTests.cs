using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests: render each control inside an EditForm and confirm the basic markup,
/// ARIA attributes, and edit/read-only mode switching all work after the EditControlBase refactor.
/// </summary>
public class ControlSmokeTests : TestContext
{
    /// <summary>
    /// Wraps an inner render fragment in an EditForm so the controls get the cascading EditContext.
    /// Programmatic component construction also requires <c>ValueExpression</c> explicitly — Blazor's
    /// <c>@bind-Value</c> macro normally synthesizes it from the markup but we don't have that luxury here.
    /// </summary>
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void EditString_renders_input_with_resolved_id()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("Name", input.Id);
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.Equal("Alice", input.GetAttribute("value"));
    }

    [Fact]
    public void EditString_in_read_only_mode_renders_ReadOnlyValue_instead_of_input()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input.edit-string-input"));
        var ro = cut.Find(".edit-readonly-value");
        Assert.Contains("Alice", ro.TextContent);
    }

    [Fact]
    public void EditString_renders_required_star_when_attribute_present()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find(".edit-label-required-star"));
    }

    [Fact]
    public void EditNumber_uses_Required_attribute_for_aria_required()
    {
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input[type=number]");
        Assert.Equal("true", input.GetAttribute("aria-required"));
    }

    [Fact]
    public void EditBool_in_read_only_mode_renders_text_not_checkbox_by_default()
    {
        // The 10.1.0 default for EditBool's read-only mode — ReadOnlyValue with TrueText/FalseText.
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input[type=checkbox]"));
        Assert.Contains("Yes", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditBool_with_RenderAsCheckboxWhenReadOnly_keeps_legacy_disabled_checkbox()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "RenderAsCheckboxWhenReadOnly", true);
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.True(checkbox.HasAttribute("aria-disabled") || checkbox.HasAttribute("disabled"));
    }

    [Fact]
    public void EditSelectEnum_renders_one_option_per_enum_value_with_sanitized_ids()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        var options = cut.FindAll("option");
        Assert.Equal(4, options.Count);
        // .ToId() yields safe ids — no spaces / punctuation.
        foreach (var opt in options)
        {
            var id = opt.Id ?? "";
            Assert.DoesNotContain(' ', id);
            Assert.StartsWith("Priority-option-", id);
        }
    }

    [Fact]
    public void EditSelectEnum_uses_GetName_for_option_text()
    {
        // Verifies GetName attribute precedence works through the rendered DOM:
        // [EnumDisplayName("Forest Green")] → "Forest Green" (wins over [Display])
        // [Display(Name = "Sky Blue")] → "Sky Blue" (no EnumDisplayName)
        // PaleYellow → "Pale Yellow" (camelCase split)
        var model = new ColorOnlyModel();
        Expression<Func<Color?>> field = () => model.Color;
        var cut = Render(WithForm(new PersonModel(), b =>
        {
            b.OpenComponent<EditSelectEnum<Color?>>(0);
            b.AddAttribute(1, "Value", model.Color);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        var optionText = string.Join("|", cut.FindAll("option").Select(o => o.TextContent.Trim()));
        Assert.Contains("Forest Green", optionText);
        Assert.Contains("Sky Blue", optionText);
        Assert.Contains("Pale Yellow", optionText);
    }

    class ColorOnlyModel
    {
        public Color? Color { get; set; } = Tests.Color.Blue;
    }

    [Fact]
    public void EditCheckedStringList_renders_one_checkbox_per_option()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "Field", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, checkboxes.Count);
        Assert.Single(checkboxes, c => c.HasAttribute("checked"));
    }
}
