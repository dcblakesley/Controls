using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the model-declared <see cref="PlaceholderAttribute"/> reaching the four select
/// controls: the resolution precedence is each control's own placeholder-ish parameter -> the bound
/// property's <c>[Placeholder]</c> attribute (via <see cref="AttributesHelper.Placeholder"/>) -> the
/// control's own built-in default. <see cref="EditSelectSearch{TValue}"/>/<see cref="EditMultiSelect{TValue}"/>
/// render the resolved text into the Select engine's placeholder span; the native
/// <see cref="EditSelectEnum{TEnum}"/>/<see cref="EditSelectString{TValue}"/> have no placeholder
/// concept, so the attribute lands on the leading blank option's text (or, when that option is absent,
/// on the hidden unmatched-value option that <see cref="SelectOptionList{TItem}"/> now renders content
/// for). Test models are declared here, private to this file, per the multi-agent split for this
/// feature (sibling agents own AttributesHelperTests.cs / TestModels.cs).
/// </summary>
public class ModelPlaceholderSelectTests : BunitContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
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

    // ----- test models (nested/private -- do not touch the shared TestModels.cs) -------------------

    class PriorityHintModel
    {
        [Placeholder("Choose a priority")]
        public Priority? Priority { get; set; }
    }

    class PlainPriorityModel
    {
        public Priority? Priority { get; set; }
    }

    // Non-nullable enum: the leading null option never renders, so a placeholder attribute can only
    // ever surface through the hidden unmatched-value option.
    class NonNullablePriorityHintModel
    {
        [Placeholder("Choose a priority")]
        public Priority Priority { get; set; }
    }

    class ColorsHintModel
    {
        [Placeholder("Choose some colors")]
        public List<Color> FavoriteColors { get; set; } = [];
    }

    class PlainColorsModel
    {
        public List<Color> FavoriteColors { get; set; } = [];
    }

    class NullableColorNameHintModel
    {
        [Placeholder("Choose a color")]
        public string? ColorName { get; set; }
    }

    // Non-nullable value type: ShowNullOption is always false regardless of NullOptionText, so an
    // unmatched default can only show the attribute text via the hidden placeholder.
    class NonNullableCountHintModel
    {
        [Placeholder("Pick a count")]
        public int Count { get; set; }
    }

    // ----- EditSelectSearch -----------------------------------------------------------------------

    [Fact]
    public void EditSelectSearch_uses_the_models_Placeholder_attribute_when_the_parameter_is_unset()
    {
        var model = new PriorityHintModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        Assert.Equal("Choose a priority", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    [Fact]
    public void EditSelectSearch_explicit_Placeholder_parameter_overrides_the_attribute()
    {
        var model = new PriorityHintModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.AddAttribute(4, "Placeholder", "Pick one");
            b.CloseComponent();
        }));

        Assert.Equal("Pick one", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    [Fact]
    public void EditSelectSearch_falls_back_to_Please_select_when_neither_attribute_nor_parameter_is_set()
    {
        var model = new PlainPriorityModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", PriorityOptions());
            b.CloseComponent();
        }));

        Assert.Equal("Please select", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    // ----- EditMultiSelect ------------------------------------------------------------------------

    [Fact]
    public void EditMultiSelect_uses_the_models_Placeholder_attribute_when_the_parameter_is_unset()
    {
        var model = new ColorsHintModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.CloseComponent();
        }));

        Assert.Equal("Choose some colors", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    [Fact]
    public void EditMultiSelect_explicit_Placeholder_parameter_overrides_the_attribute()
    {
        var model = new ColorsHintModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.AddAttribute(4, "Placeholder", "Pick some");
            b.CloseComponent();
        }));

        Assert.Equal("Pick some", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    [Fact]
    public void EditMultiSelect_falls_back_to_Please_select_when_neither_attribute_nor_parameter_is_set()
    {
        var model = new PlainColorsModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditMultiSelect<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", ColorOptions());
            b.CloseComponent();
        }));

        Assert.Equal("Please select", cut.Find(".wss-select-selection-placeholder").TextContent);
    }

    // ----- EditSelectEnum -------------------------------------------------------------------------

    [Fact]
    public void EditSelectEnum_nullable_leading_option_uses_the_models_Placeholder_attribute()
    {
        var model = new PriorityHintModel(); // Priority is null -> leading null option renders
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Choose a priority", cut.Find("option[value='']").TextContent.Trim());
    }

    [Fact]
    public void EditSelectEnum_explicit_NullOptionText_overrides_the_attribute()
    {
        var model = new PriorityHintModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "NullOptionText", "(none)");
            b.CloseComponent();
        }));

        Assert.Equal("(none)", cut.Find("option[value='']").TextContent.Trim());
    }

    [Fact]
    public void EditSelectEnum_non_nullable_hidden_placeholder_uses_the_models_Placeholder_attribute()
    {
        // (Priority)99 has no defined member, so it never matches an <option> -- the hidden
        // unmatched-value option is what renders, and it's the only slot that can show the attribute
        // text for a non-nullable enum (the leading null option never renders here).
        var model = new NonNullablePriorityHintModel { Priority = (Priority)99 };
        Expression<Func<Priority>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var placeholder = cut.Find("option[hidden]");
        Assert.True(placeholder.HasAttribute("selected"));
        Assert.Equal("Choose a priority", placeholder.TextContent.Trim());
    }

    // ----- EditSelectString -----------------------------------------------------------------------

    [Fact]
    public void EditSelectString_nullable_leading_option_uses_the_models_Placeholder_attribute()
    {
        var model = new NullableColorNameHintModel(); // ColorName is null -> leading null option renders
        Expression<Func<string?>> field = () => model.ColorName;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.ColorName);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        Assert.Equal("Choose a color", cut.Find("option[value='']").TextContent.Trim());
    }

    [Fact]
    public void EditSelectString_explicit_NullOptionText_overrides_the_attribute()
    {
        var model = new NullableColorNameHintModel();
        Expression<Func<string?>> field = () => model.ColorName;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.ColorName);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "NullOptionText", "(none)");
            b.CloseComponent();
        }));

        Assert.Equal("(none)", cut.Find("option[value='']").TextContent.Trim());
    }

    [Fact]
    public void EditSelectString_NullOptionText_null_still_suppresses_the_option_even_with_a_Placeholder_attribute()
    {
        // NullOptionText=null is the consumer's explicit "no leading option" opt-out. A [Placeholder]
        // on the model must never resurrect it -- that would re-show an option the consumer deliberately
        // removed. Binding to a matched value too, so no hidden unmatched-value placeholder appears either.
        var model = new NullableColorNameHintModel { ColorName = "a" };
        Expression<Func<string?>> field = () => model.ColorName;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.ColorName);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "NullOptionText", (string?)null);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("option[value='']"));
        Assert.Equal(2, cut.FindAll("select option").Count);
    }

    [Fact]
    public void EditSelectString_value_type_hidden_placeholder_uses_the_models_Placeholder_attribute()
    {
        // A non-nullable value type never shows the leading blank (ShowNullOption is always false),
        // so an untouched default that matches no option can only show the attribute text via the
        // hidden unmatched-value placeholder.
        var model = new NonNullableCountHintModel { Count = 0 };
        Expression<Func<int>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "1", "2", "3" });
            b.CloseComponent();
        }));

        var placeholder = cut.Find("option[hidden]");
        Assert.True(placeholder.HasAttribute("selected"));
        Assert.Equal("Pick a count", placeholder.TextContent.Trim());
    }
}
