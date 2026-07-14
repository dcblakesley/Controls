using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit smoke tests for <see cref="EditCheckedEnumList{TEnum}"/>. Mirrors the string-list tests
/// but covers the enum-display-name path and the Sort option.
/// </summary>
public class EditCheckedEnumListTests : TestContext
{
    static RenderFragment WithForm(ColorListModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    class ColorListModel
    {
        public List<Color> Colors { get; set; } = [];
    }

    [Fact]
    public void Renders_one_checkbox_per_enum_value()
    {
        var model = new ColorListModel { Colors = [Color.Red] };
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        // Color has 4 values: Red, Green, Blue, PaleYellow.
        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(4, checkboxes.Count);
        Assert.Single(checkboxes, c => c.HasAttribute("checked"));
    }

    [Fact]
    public void UseStyledCheckbox_renders_a_custom_drawn_box_per_option()
    {
        var model = new ColorListModel { Colors = [Color.Red] };
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "UseStyledCheckbox", true);
            b.CloseComponent();
        }));

        // Color has 4 values: Red, Green, Blue, PaleYellow.
        Assert.Equal(4, cut.FindAll(".edit-checkbox-wrap").Count);
        Assert.Equal(4, cut.FindAll("input.edit-checkbox-input-styled").Count);
        Assert.Equal(4, cut.FindAll("label.edit-checkbox-label-styled").Count);
    }

    [Fact]
    public void Labels_use_GetName_attribute_precedence()
    {
        var model = new ColorListModel();
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var labelText = string.Join("|", cut.FindAll(".edit-checkbox-label").Select(l => l.TextContent.Trim()));
        // [EnumDisplayName("Forest Green")] wins over [Display]
        Assert.Contains("Forest Green", labelText);
        // [Display(Name = "Sky Blue")]
        Assert.Contains("Sky Blue", labelText);
        // camelCase split
        Assert.Contains("Pale Yellow", labelText);
    }

    [Fact]
    public void Clicking_toggles_enum_value_in_list()
    {
        var model = new ColorListModel { Colors = [Color.Red] };
        var captured = new List<Color>();
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<Color>>(this, v => captured = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        // Find Green's checkbox by its value attribute (enum.ToString()).
        var greenBox = cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "Green");
        greenBox.Change(true);

        Assert.Contains(Color.Green, captured);
        Assert.Contains(Color.Red, captured);
    }

    [Fact]
    public void Toggling_builds_a_new_list_and_does_not_mutate_the_original()
    {
        // Regression: ToggleAsync must build a NEW list rather than mutating the bound one in place,
        // so change detection fires and the caller's own collection isn't altered behind its back.
        var original = new List<Color> { Color.Red };
        var model = new ColorListModel { Colors = original };
        List<Color>? captured = null;
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<Color>>(this, v => captured = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "Green").Change(true);

        Assert.NotNull(captured);
        Assert.Equal(2, captured!.Count);             // new list carries both values
        Assert.Single(original);                       // the original list was not mutated in place
        Assert.False(ReferenceEquals(captured, original));
    }

    [Fact]
    public void Read_only_mode_renders_ReadOnlyValue_per_selected_enum_with_display_name()
    {
        var model = new ColorListModel { Colors = [Color.Green, Color.Blue] };
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input[type=checkbox]"));
        var text = string.Join("|", cut.FindAll(".edit-readonly-value").Select(r => r.TextContent.Trim()));
        Assert.Contains("Forest Green", text);
        Assert.Contains("Sky Blue", text);
    }

    [Fact]
    public void Sort_true_orders_options_alphabetically_by_display_name()
    {
        var model = new ColorListModel();
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Sort", true);
            b.CloseComponent();
        }));

        var labels = cut.FindAll(".edit-checkbox-label").Select(l => l.TextContent.Trim()).ToList();
        // Sorted by GetName: "Forest Green", "Pale Yellow", "Red", "Sky Blue".
        var sorted = labels.OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, labels);
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_hides_component_when_list_is_empty()
    {
        var model = new ColorListModel { Colors = [] };
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_shows_component_when_list_has_items()
    {
        var model = new ColorListModel { Colors = [Color.Red] };
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find(".edit-control-wrapper"));
    }
}
