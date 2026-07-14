using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for <see cref="EditCheckedStringList"/> — list controls were under-tested
/// relative to scalar controls. Safety net for the EditControlListBase hiding-mode rework.
/// </summary>
public class EditCheckedStringListTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void Consumer_class_and_field_state_classes_are_forwarded_to_every_checkbox()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "class", "my-check-class");
            b.CloseComponent();
        }));

        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(2, boxes.Count);
        Assert.All(boxes, box => Assert.Contains("my-check-class", box.ClassList));
        // Full InputBase parity: the EditContext field-state token rides along ("valid" at rest,
        // "modified invalid" after a failing edit — same classes the scalar controls emit).
        Assert.All(boxes, box => Assert.Contains("valid", box.ClassList));
    }

    [Fact]
    public void Consumer_class_is_forwarded_in_read_only_mode_too()
    {
        // The edit/read-only asymmetry pattern: the class must not vanish when IsEditMode is false.
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "class", "my-check-class");
            b.CloseComponent();
        }));

        Assert.Contains("my-check-class", cut.Find(".edit-readonly-value").ClassList);
    }

    [Fact]
    public void Renders_one_checkbox_per_option_with_initial_checked_state()
    {
        var model = new PersonModel { Tags = ["b"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, checkboxes.Count);
        // Only "b" should be checked.
        var checkedBoxes = checkboxes.Where(c => c.HasAttribute("checked")).ToList();
        Assert.Single(checkedBoxes);
        Assert.Equal("b", checkedBoxes[0].GetAttribute("value"));
    }

    [Fact]
    public void Clicking_unchecked_option_adds_it_to_value()
    {
        var model = new PersonModel { Tags = ["a"] };
        var captured = new List<string>();
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => captured = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        // Find the unchecked "b" checkbox and toggle it.
        var bBox = cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "b");
        bBox.Change(true);

        Assert.Contains("b", captured);
        Assert.Contains("a", captured); // existing items preserved
    }

    [Fact]
    public void Clicking_checked_option_removes_it_from_value()
    {
        var model = new PersonModel { Tags = ["a", "b"] };
        var captured = new List<string>();
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => captured = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        var aBox = cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "a");
        aBox.Change(false);

        Assert.DoesNotContain("a", captured);
        Assert.Contains("b", captured);
    }

    [Fact]
    public void UseStyledCheckbox_renders_a_custom_drawn_box_per_option()
    {
        var model = new PersonModel { Tags = ["b"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "UseStyledCheckbox", true);
            b.CloseComponent();
        }));

        Assert.Equal(3, cut.FindAll(".edit-checkbox-wrap").Count);
        Assert.Equal(3, cut.FindAll("input.edit-checkbox-input-styled").Count);
        Assert.Equal(3, cut.FindAll("label.edit-checkbox-label-styled").Count);
    }

    [Fact]
    public void UseStyledCheckbox_unset_renders_bare_native_checkboxes()
    {
        var model = new PersonModel { Tags = ["b"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-checkbox-wrap"));
        Assert.Empty(cut.FindAll(".edit-checkbox-label-styled"));
        Assert.Equal(3, cut.FindAll("input[type=checkbox]").Count);
    }

    [Fact]
    public void Read_only_mode_renders_ReadOnlyValue_per_selected_item_not_checkboxes()
    {
        var model = new PersonModel { Tags = ["a", "c"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input[type=checkbox]"));
        var readOnlyValues = cut.FindAll(".edit-readonly-value");
        Assert.Equal(2, readOnlyValues.Count);
        var text = string.Join("|", readOnlyValues.Select(r => r.TextContent.Trim()));
        Assert.Contains("a", text);
        Assert.Contains("c", text);
    }

    [Fact]
    public void Read_only_mode_with_empty_list_renders_empty_ReadOnlyValue()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        // Single empty ReadOnlyValue rendered as the "no selection" placeholder.
        Assert.Empty(cut.FindAll("input[type=checkbox]"));
        Assert.Single(cut.FindAll(".edit-readonly-value"));
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_hides_component_when_list_is_empty()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        // Component should render nothing — no wrapper, no checkboxes, no fieldset.
        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_shows_component_when_list_has_items()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find(".edit-control-wrapper"));
        Assert.Equal(3, cut.FindAll("input[type=checkbox]").Count);
    }

    [Fact]
    public void HidingMode_WhenReadOnlyAndNullOrDefault_keeps_component_in_edit_mode_even_when_empty()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "Hiding", HidingMode.WhenReadOnlyAndNullOrDefault);
            b.AddAttribute(5, "IsEditMode", true);
            b.CloseComponent();
        }));

        // Edit mode + empty list: read-only-conditional hiding should NOT hide.
        Assert.NotNull(cut.Find(".edit-control-wrapper"));
    }

    [Fact]
    public void HidingMode_WhenReadOnlyAndNullOrDefault_hides_in_read_only_when_empty()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "Hiding", HidingMode.WhenReadOnlyAndNullOrDefault);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    // Model whose list property carries a count-based annotation, so toggling boxes can flip validity.
    class MinTwoModel
    {
        [Required, MinLength(2)]
        public List<string> Picks { get; set; } = [];
    }

    [Fact]
    public void Toggling_to_satisfy_MinLength_clears_the_validation_error_in_the_same_click()
    {
        // Regression: ToggleAsync must write the new value back to the model (via ValueChanged) BEFORE
        // NotifyFieldChanged, otherwise the validator reads the stale pre-toggle value off the model and
        // the error state lags one click behind (a [MinLength(2)] error lingered after the 2nd box).
        var model = new MinTwoModel();
        var editContext = new EditContext(model);
        Expression<Func<List<string>>> field = () => model.Picks;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditCheckedStringList>(1);
                content.AddAttribute(2, "Value", model.Picks);
                content.AddAttribute(3, "ValueChanged",
                    EventCallback.Factory.Create<List<string>>(this, v => model.Picks = v)); // simulate @bind-Value
                content.AddAttribute(4, "ValueExpression", field);
                content.AddAttribute(5, "Options", new List<string> { "a", "b", "c" });
                content.CloseComponent();
            }));
            b.CloseComponent();
        });
        var fi = editContext.Field(nameof(MinTwoModel.Picks));

        cut.InvokeAsync(() => editContext.Validate());
        Assert.NotEmpty(editContext.GetValidationMessages(fi)); // empty list -> MinLength(2) fails

        cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "a").Change(true);
        cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "b").Change(true);

        Assert.Equal(2, model.Picks.Count);
        Assert.Empty(editContext.GetValidationMessages(fi)); // requirement met -> no lingering error
    }
}
