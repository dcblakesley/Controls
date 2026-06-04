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
            b.AddAttribute(3, "Field", field);
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
            b.AddAttribute(3, "Field", field);
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
            b.AddAttribute(3, "Field", field);
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
            b.AddAttribute(3, "Field", field);
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
            b.AddAttribute(2, "Field", field);
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
            b.AddAttribute(2, "Field", field);
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
            b.AddAttribute(2, "Field", field);
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
            b.AddAttribute(2, "Field", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".wss-select"));
        var ro = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("Green", ro);
        Assert.Contains("Blue", ro);
    }
}
