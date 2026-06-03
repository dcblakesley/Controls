using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Interaction coverage for controls that were previously render-only or untested: the tri-state
/// <see cref="EditBoolNullRadio"/>, <see cref="EditRadioString"/> selection, <see cref="EditTextArea"/>,
/// and the culture-sensitive number formatting in <see cref="EditNumber{T}"/>.
/// </summary>
public class RadioTextAreaAndCultureTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("none", null)]
    public void EditBoolNullRadio_selecting_an_option_sets_the_tri_state_value(string suffix, bool? expected)
    {
        // Start from a value that differs from the expected result, so selecting the option is always a
        // real change (InputBase doesn't fire ValueChanged when the value is unchanged).
        bool? start = expected == true ? false : true;
        var model = new PersonModel { IsSubscribed = start };
        bool? captured = start;
        var changed = false;
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<bool?>(this, v => { captured = v; changed = true; }));
            b.CloseComponent();
        }));

        cut.Find($"#rb-IsSubscribed-{suffix}").Change(suffix == "none" ? "" : suffix);

        Assert.True(changed);
        Assert.Equal(expected, captured);
    }

    [Fact]
    public void EditRadioString_selecting_an_option_updates_the_bound_value()
    {
        var model = new PersonModel { Name = "a" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        cut.Find("#rb-Name-b").Change("b");

        Assert.Equal("b", captured);
    }

    [Fact]
    public void EditTextArea_renders_a_textarea_with_the_requested_rows()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Rows", 5);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Equal("5", textarea.GetAttribute("rows"));
        Assert.Equal("hello", textarea.GetAttribute("value"));
    }

    [Fact]
    public void EditNumber_formats_invariantly_regardless_of_the_current_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE"); // decimal separator is ','
            var model = new PersonModel { Price = 1.5m };
            Expression<Func<decimal?>> field = () => model.Price;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditNumber<decimal?>>(0);
                b.AddAttribute(1, "Value", model.Price);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Field", field);
                b.CloseComponent();
            }));

            // EditNumber routes formatting through InvariantCulture, so the decimal point stays "."
            // even under de-DE (whose culture default would render "1,5").
            Assert.Equal("1.5", cut.Find("input[type=number]").GetAttribute("value"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
