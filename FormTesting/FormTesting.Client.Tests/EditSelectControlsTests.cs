using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for the AntDesign-style form selects (EditSelectSearch / EditMultiSelect),
/// which wrap the Select engine in the EditControlBase / EditControlListBase pattern. Confirms
/// id/ARIA wiring, the selected-value display, and edit/read-only switching.
/// </summary>
public class EditSelectControlsTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static List<SelectOption<Priority?>> PriorityOptions() =>
    [
        new(Priority.Low, "Low"),
        new(Priority.Medium, "Medium"),
        new(Priority.High, "High"),
        new(Priority.Critical, "Critical")
    ];

    static List<SelectOption<Color>> ColorOptions() =>
    [
        new(Color.Red, "Red"),
        new(Color.Green, "Green"),
        new(Color.Blue, "Blue")
    ];

    [Fact]
    public void EditSelectSearch_renders_search_input_with_resolved_id_and_aria_required()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        var input = cut.Find("input.wss-select-selection-search-input");
        Assert.Equal("Priority", input.Id);
        // Priority is [Required] on PersonModel.
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.Equal("combobox", input.GetAttribute("role"));
    }

    [Fact]
    public void EditSelectSearch_combobox_aria_expanded_is_lowercase_false_when_closed()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        // Must be a lowercase ARIA boolean ("false"), not Blazor's bool ToString ("False").
        Assert.Equal("false", cut.Find("input.wss-select-selection-search-input").GetAttribute("aria-expanded"));
    }

    [Fact]
    public void EditSelectSearch_shows_selected_option_label()
    {
        var model = new PersonModel { Priority = Priority.High };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        Assert.Contains("High", cut.Find(".wss-select-selection-item").TextContent);
    }

    [Fact]
    public void EditSelectSearch_read_only_renders_ReadOnlyValue_not_dropdown()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", PriorityOptions());
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".wss-select"));
        Assert.Contains("Medium", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditMultiSelect_renders_multiple_wrapper_with_selected_tag()
    {
        var model = new PersonModel { FavoriteColors = [Color.Green] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find(".wss-select-multiple"));
        Assert.Contains("Green", cut.Find(".wss-select-selection-item-content").TextContent);
    }

    [Fact]
    public void EditMultiSelect_tag_remove_and_clear_are_labelled_buttons()
    {
        // Tag-remove and clear must be real buttons (keyboard-operable) with accessible names.
        var model = new PersonModel { FavoriteColors = [Color.Green] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.CloseComponent();
        }));

        var remove = cut.Find("button.wss-select-selection-item-remove");
        Assert.Equal("Remove Green", remove.GetAttribute("aria-label"));

        var clear = cut.Find("button.wss-select-clear");
        Assert.Equal("Clear all selections", clear.GetAttribute("aria-label"));
    }

    [Fact]
    public void EditMultiSelect_open_listbox_is_marked_multiselectable()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;   // tolerate the scroll/position JS module imports
        var model = new PersonModel { FavoriteColors = [Color.Green] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.CloseComponent();
        }));

        cut.Find(".wss-select").Click();   // open the dropdown
        var listbox = cut.Find("[role=listbox]");
        Assert.Equal("true", listbox.GetAttribute("aria-multiselectable"));
    }

    [Fact]
    public void EditMultiSelect_read_only_renders_joined_labels()
    {
        var model = new PersonModel { FavoriteColors = [Color.Green, Color.Blue] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".wss-select"));
        var ro = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("Green", ro);
        Assert.Contains("Blue", ro);
    }

    // Model whose list property carries a count-based annotation, so selecting options can flip validity.
    class MinTwoColorsModel
    {
        [Required, MinLength(2)]
        public List<Color> Picks { get; set; } = [];
    }

    [Fact]
    public void EditMultiSelect_selecting_to_satisfy_MinLength_clears_the_error_in_the_same_click()
    {
        // Regression: OnValuesChanged must write back to the model (ValueChanged) BEFORE NotifyFieldChanged,
        // otherwise the validator reads the stale pre-change value and the error state lags one click behind.
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;   // tolerate the scroll/position JS module imports
        var model = new MinTwoColorsModel { Picks = [Color.Green] };
        var editContext = new EditContext(model);
        Expression<Func<List<Color>>> field = () => model.Picks;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditMultiSelect<Color>>(1);
                content.AddAttribute(2, "Value", model.Picks);
                content.AddAttribute(3, "ValueChanged",
                    EventCallback.Factory.Create<List<Color>>(this, v => model.Picks = v)); // simulate @bind-Value
                content.AddAttribute(4, "ValueExpression", field);
                content.AddAttribute(5, "Options", ColorOptions());
                content.CloseComponent();
            }));
            b.CloseComponent();
        });
        var fi = editContext.Field(nameof(MinTwoColorsModel.Picks));

        cut.InvokeAsync(() => editContext.Validate());
        Assert.NotEmpty(editContext.GetValidationMessages(fi)); // one selection -> MinLength(2) fails

        cut.Find(".wss-select").Click();                        // open the dropdown
        // Click the "Blue" option (Green is already selected) -> two selections satisfies MinLength(2).
        cut.FindAll("[role=option]").First(o => o.TextContent.Contains("Blue")).Click();

        Assert.Equal(2, model.Picks.Count);
        Assert.Empty(editContext.GetValidationMessages(fi)); // requirement met -> no lingering error
    }

    [Fact]
    public void Select_wrapper_and_backdrop_carry_tabindex_for_touch_click_synthesis()
    {
        // iOS-class WebKit only synthesizes a click from a tap when the target chain contains a
        // focusable element. The single-mode tap usually lands on the selected-value span, so
        // without tabindex="-1" on the wrapper the select never opens on touch — and without it
        // on the backdrop, tap-outside never closes it.
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".wss-select");
        Assert.Equal("-1", wrapper.GetAttribute("tabindex"));

        wrapper.Click(); // open, so the backdrop renders
        Assert.Equal("-1", cut.Find(".wss-select-backdrop").GetAttribute("tabindex"));
    }
}
