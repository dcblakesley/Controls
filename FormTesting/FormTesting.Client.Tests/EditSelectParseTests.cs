using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// End-to-end coverage of the select parse path: a native &lt;select&gt; change sets
/// CurrentValueAsString, which routes through the control's TryParseValueFromString into the shared
/// SelectParsing helper. SelectParsingTests covers the helper in isolation; these confirm the
/// controls actually delegate to it and write the parsed value back through ValueChanged.
/// </summary>
public class EditSelectParseTests : TestContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void EditSelectEnum_rebuilds_options_when_Sort_changes_at_runtime()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Sort", false);
            b.CloseComponent();
        }));

        var unsortedFirst = cut.FindAll("select option")[1].TextContent.Trim(); // [0] is the null option
        Assert.Equal("Low", unsortedFirst);

        // Previously the cached option list was frozen at init and a Sort change was ignored.
        cut.FindComponent<EditSelectEnum<Priority?>>().SetParametersAndRender(p => p.Add(x => x.Sort, true));
        var sortedFirst = cut.FindAll("select option")[1].TextContent.Trim();
        Assert.Equal("Critical", sortedFirst);
    }

    enum EmptyEnum { }

    class EmptyEnumModel
    {
        public EmptyEnum? Choice { get; set; }
    }

    [Fact]
    public void EditRadioEnum_with_empty_enum_and_other_option_renders_read_only_without_throwing()
    {
        var model = new EmptyEnumModel();
        Expression<Func<EmptyEnum?>> field = () => model.Choice;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<EmptyEnum?>>(0);
            b.AddAttribute(1, "Value", model.Choice);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "HasOtherOption", true);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        // GetOptions().Last() used to throw on an empty enum in the read-only branch.
        Assert.NotNull(cut.Find(".edit-readonly-value"));
    }

    [Fact]
    public void EditSelectString_null_value_selects_the_leading_empty_option()
    {
        var model = new PersonModel { Name = null! };
        Expression<Func<string?>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", (string?)null);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        // A null value used to render with "a" visually selected while the model stayed null.
        Assert.True(cut.Find("option[value='']").HasAttribute("selected"));
        Assert.False(cut.Find("option[value='a']").HasAttribute("selected"));
    }

    [Fact]
    public void EditSelectEnum_change_parses_and_updates_bound_value()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("High");
        Assert.Equal(Priority.High, model.Priority);
    }

    [Fact]
    public void EditSelectEnum_empty_change_clears_nullable_value()
    {
        var model = new PersonModel { Priority = Priority.High };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("");   // empty is valid (null) for a nullable enum
        Assert.Null(model.Priority);
    }

    [Fact]
    public void EditSelectEnum_invalid_change_leaves_value_unchanged()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            b.AddAttribute(4, "Field", field);
            b.CloseComponent();
        }));

        cut.Find("select").Change("NotAValidMember");   // failed parse must not corrupt the model
        Assert.Equal(Priority.Medium, model.Priority);
    }

    [Fact]
    public void EditSelectString_change_parses_and_updates_bound_value()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, v => model.Name = v));
            b.AddAttribute(4, "Field", field);
            b.AddAttribute(5, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        cut.Find("select").Change("b");
        Assert.Equal("b", model.Name);
    }

    class NullableNameModel { public string? Name { get; set; } }
    class NullableCountModel { public int? Count { get; set; } }
    class CountModel { public int Count { get; set; } }

    [Fact]
    public void EditSelectString_empty_change_clears_a_nullable_string_to_null()
    {
        var model = new NullableNameModel { Name = "a" };
        Expression<Func<string?>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => model.Name = v));
            b.AddAttribute(4, "Field", field);
            b.AddAttribute(5, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        // Selecting the blank used to store "" — a string? could never return to null. It now clears to null.
        cut.Find("select").Change("");
        Assert.Null(model.Name);
    }

    [Fact]
    public void EditSelectString_nullable_int_shows_the_blank_and_empty_change_clears_to_null()
    {
        var model = new NullableCountModel { Count = 2 };
        Expression<Func<int?>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<int?>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => model.Count = v));
            b.AddAttribute(4, "Field", field);
            b.AddAttribute(5, "Options", new List<string> { "1", "2" });
            b.CloseComponent();
        }));

        // int? is nullable → the blank renders, and selecting it clears to null (it used to fail parsing "").
        Assert.NotEmpty(cut.FindAll("option[value='']"));
        cut.Find("select").Change("");
        Assert.Null(model.Count);
    }

    [Fact]
    public void EditSelectString_non_nullable_value_type_has_no_blank_option()
    {
        var model = new CountModel { Count = 1 };
        Expression<Func<int>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "1", "2" });
            b.CloseComponent();
        }));

        // A blank on a non-nullable value type would only map to a spurious default(0), so it must not render.
        Assert.Empty(cut.FindAll("option[value='']"));
        Assert.Equal(2, cut.FindAll("select option").Count);
    }

    [Fact]
    public void EditSelectString_NullOptionText_null_suppresses_the_blank_option()
    {
        var model = new NullableNameModel { Name = "a" };
        Expression<Func<string?>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.AddAttribute(5, "NullOptionText", (string?)null);   // explicit opt-out
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("option[value='']"));
        Assert.Equal(2, cut.FindAll("select option").Count);
    }

    class RatioModel { public double Ratio { get; set; } }

    [Fact]
    public void EditSelect_double_selects_the_matching_option_under_a_foreign_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            var model = new RatioModel { Ratio = 1.5 };
            Expression<Func<double>> field = () => model.Ratio;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditSelect<double>>(0);
                b.AddAttribute(1, "Value", model.Ratio);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Field", field);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenElement(0, "option");
                    cb.AddAttribute(1, "value", "1.5");
                    cb.AddContent(2, "One and a half");
                    cb.CloseElement();
                    cb.OpenElement(3, "option");
                    cb.AddAttribute(4, "value", "2.5");
                    cb.AddContent(5, "Two and a half");
                    cb.CloseElement();
                }));
                b.CloseComponent();
            }));

            // Before the fix, the value formatted as "1,5" under de-DE, matched no <option value>, and
            // the select showed unselected. FormatValueAsString is now invariant, so "1.5" matches.
            Assert.True(cut.Find("option[value='1.5']").HasAttribute("selected"));
            Assert.False(cut.Find("option[value='2.5']").HasAttribute("selected"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    class WhenModel { public DateOnly When { get; set; } }

    [Fact]
    public void EditSelect_DateOnly_selects_the_matching_option_under_a_foreign_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // date display format is dd.MM.yyyy
            var model = new WhenModel { When = new DateOnly(2026, 6, 15) };
            Expression<Func<DateOnly>> field = () => model.When;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditSelect<DateOnly>>(0);
                b.AddAttribute(1, "Value", model.When);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Field", field);
                b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
                {
                    cb.OpenElement(0, "option");
                    cb.AddAttribute(1, "value", "2026-06-15");
                    cb.AddContent(2, "June 15");
                    cb.CloseElement();
                    cb.OpenElement(3, "option");
                    cb.AddAttribute(4, "value", "2026-07-01");
                    cb.AddContent(5, "July 1");
                    cb.CloseElement();
                }));
                b.CloseComponent();
            }));

            // Before M13 the value formatted as the invariant display "06/15/2026", matched no ISO
            // <option value>, and the select showed unselected. FormatInvariant now emits "2026-06-15".
            Assert.True(cut.Find("option[value='2026-06-15']").HasAttribute("selected"));
            Assert.False(cut.Find("option[value='2026-07-01']").HasAttribute("selected"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void EditSelectString_value_type_unmatched_default_shows_a_hidden_placeholder()
    {
        var model = new CountModel { Count = 0 };  // untouched default — no option matches "0"
        Expression<Func<int>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "1", "2", "3" });
            b.CloseComponent();
        }));

        // L5 removed the blank for value types; without a placeholder the browser would visually select
        // "1" while the model holds 0. The hidden, disabled placeholder shows blank instead of lying.
        var placeholder = cut.Find("option[hidden]");
        Assert.True(placeholder.HasAttribute("selected"));
        Assert.True(placeholder.HasAttribute("disabled"));
        Assert.Equal("", placeholder.GetAttribute("value"));   // value="" so it never submits a real value
        Assert.False(cut.Find("option[value='1']").HasAttribute("selected"));
    }

    [Fact]
    public void EditSelectString_value_type_matched_value_renders_no_placeholder()
    {
        var model = new CountModel { Count = 2 };
        Expression<Func<int>> field = () => model.Count;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "1", "2", "3" });
            b.CloseComponent();
        }));

        // Value 2 matches <option value="2">, so the real option carries selection — no placeholder needed.
        Assert.Empty(cut.FindAll("option[hidden]"));
        Assert.True(cut.Find("option[value='2']").HasAttribute("selected"));
        Assert.Equal(3, cut.FindAll("select option").Count);
    }

    [Fact]
    public void EditSelectString_nullable_string_renders_no_placeholder_keeping_the_blank_option()
    {
        var model = new NullableNameModel { Name = null };
        Expression<Func<string?>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string?>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        // ShowNullOption is true for a nullable type, so the existing leading blank still absorbs the null.
        // The placeholder is only for the suppressed-blank (value-type) case and must not appear here.
        Assert.Empty(cut.FindAll("option[hidden]"));
        Assert.True(cut.Find("option[value='']").HasAttribute("selected"));
    }
}
