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
            b.AddAttribute(4, "Rows", 5);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Equal("5", textarea.GetAttribute("rows"));
        Assert.Equal("hello", textarea.GetAttribute("value"));
    }

    [Fact]
    public void EditRadioString_a_real_option_named_Other_binds_its_own_value()
    {
        var model = new PersonModel { Name = "" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "Options", new List<string> { "Other", "Something else" });
            b.CloseComponent();
        }));

        // The literal "Other" used to collide with the built-in other-option sentinel, silently
        // replacing the model value with the empty other-text.
        cut.Find("#rb-Name-Other").Change("Other");
        Assert.Equal("Other", captured);
    }

    [Fact]
    public void EditRadioString_an_option_equal_to_the_internal_sentinel_binds_its_own_value()
    {
        // L6: the sentinel is uniquified against Options, so even the literal "__wss-other__"
        // can't route through the Other branch (which would overwrite the model with the empty
        // other-text) — and the real Other radio keeps working alongside it.
        var model = new PersonModel { Name = "" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "HasOther", true);
            b.AddAttribute(6, "Options", new List<string> { "__wss-other__", "a" });
            b.CloseComponent();
        }));

        // Selecting the pathological literal option binds the literal, not the other-text.
        cut.Find("#rb-Name-__wss-other__").Change("__wss-other__");
        Assert.Equal("__wss-other__", captured);

        // The built-in Other radio (re-keyed away from the collision) still routes to the text box.
        var otherRadio = cut.Find("#rb-Name-other");
        otherRadio.Change(otherRadio.GetAttribute("value"));
        cut.Find("#txt-Name-custom-value").Input("bespoke");
        Assert.Equal("bespoke", captured);
    }

    [Fact]
    public void EditNumber_binds_on_change_not_per_keystroke()
    {
        var model = new PersonModel { Age = 1 };
        Expression<Func<int?>> field = () => model.Age;
        int? captured = null;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => captured = v));
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        // No oninput handler: per-keystroke binding flashed "must be a number" while typing partial
        // numbers ("-", "3.") because browsers report type=number as "" until the text parses.
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("5"));
        input.Change("5");
        Assert.Equal(5, captured);
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

    [Fact]
    public void EditRadioString_puts_radiogroup_role_on_the_fieldset_in_edit_mode_only()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;

        var edit = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));
        var editFieldset = edit.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("radiogroup", editFieldset.GetAttribute("role"));
        Assert.Equal("Name", editFieldset.GetAttribute("data-test-id"));

        var readOnly = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));
        // Gated off in read-only so it isn't a radiogroup with no radio children (axe aria-required-children),
        // and the fieldset id is dropped so it doesn't collide with the read-only ReadOnlyValue's id=Name.
        var roFieldset = readOnly.Find("fieldset.edit-radio-fieldset");
        Assert.False(roFieldset.HasAttribute("role"));
        Assert.False(roFieldset.HasAttribute("id"));
        Assert.Single(readOnly.FindAll("#Name")); // only the ReadOnlyValue carries id=Name in read-only
    }

    [Fact]
    public void EditRadioString_omits_aria_labelledby_when_the_label_is_hidden()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.AddAttribute(5, "IsLabelHidden", true);
            b.CloseComponent();
        }));
        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("radiogroup", fieldset.GetAttribute("role"));  // still a radiogroup in edit mode
        Assert.False(fieldset.HasAttribute("aria-labelledby"));     // but no dangling lbl- ref (no legend rendered)
    }

    [Fact]
    public void EditBool_checkbox_carries_edit_input_class()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        // edit-input is the hook the shipped :focus-visible ring / .invalid styles attach to.
        Assert.Contains("edit-input", cut.Find("input[type=checkbox]").GetAttribute("class")!);
    }

    [Fact]
    public void EditRadioString_radios_carry_edit_radio_input_class()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));
        Assert.All(cut.FindAll("input[type=radio]"),
            r => Assert.Contains("edit-radio-input", r.GetAttribute("class")!));
    }

    [Fact]
    public void EditRadioString_reflects_an_externally_changed_value()
    {
        var model = new PersonModel { Name = "a" };
        var editContext = new EditContext(model);
        Expression<Func<string?>> valueExpr = () => model.Name;

        var cut = RenderComponent<EditRadioString>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, "a")
            .Add(c => c.ValueExpression, valueExpr)
            .Add(c => c.Options, new List<string> { "a", "b", "c" }));

        Assert.True(cut.Find("#rb-Name-a").HasAttribute("checked"));
        Assert.False(cut.Find("#rb-Name-b").HasAttribute("checked"));

        // The parent supplies a new value (form reset / async load / programmatic set). The radio
        // selection used to be cached once in OnInitialized and so ignored this; it must now follow.
        cut.SetParametersAndRender(ps => ps.Add(c => c.Value, "b"));

        Assert.False(cut.Find("#rb-Name-a").HasAttribute("checked"));
        Assert.True(cut.Find("#rb-Name-b").HasAttribute("checked"));
    }

    [Fact]
    public void EditRadioString_with_a_custom_initial_value_selects_Other_and_fills_the_text_box()
    {
        var model = new PersonModel { Name = "bespoke" }; // not one of the options
        var editContext = new EditContext(model);
        Expression<Func<string?>> valueExpr = () => model.Name;

        var cut = RenderComponent<EditRadioString>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, "bespoke")
            .Add(c => c.ValueExpression, valueExpr)
            .Add(c => c.HasOther, true)
            .Add(c => c.Options, new List<string> { "a", "b" }));

        // A value matching no option resolves to the "Other" radio, with the text box pre-filled.
        Assert.True(cut.Find("#rb-Name-other").HasAttribute("checked"));
        Assert.Equal("bespoke", cut.Find("#txt-Name-custom-value").GetAttribute("value"));
    }

    [Fact]
    public void EditRadioEnum_other_text_input_has_an_accessible_name()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.CloseComponent();
        }));

        // The "Other" free-text input needs an accessible name (a placeholder is not one), matching
        // the EditRadioString sibling. It previously carried only an optional placeholder.
        var other = cut.Find("input.edit-radio-other-input");
        Assert.Equal("Custom text value input", other.GetAttribute("aria-label"));
    }
}
