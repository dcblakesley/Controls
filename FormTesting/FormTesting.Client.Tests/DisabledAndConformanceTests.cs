using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers two previously-untested areas: the <c>IsDisabled</c> parameter (declared on every control but
/// never set in a test) and the hand-maintained <see cref="IEditControl"/> surface on <c>EditRadio</c>
/// (which can't inherit the shared base and so re-declares the interface by hand).
/// </summary>
public class DisabledAndConformanceTests : TestContext
{
    static RenderFragment WithForm(PersonModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void EditString_IsDisabled_renders_a_disabled_input()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.True(cut.Find("input.edit-string-input").HasAttribute("disabled"));
    }

    [Fact]
    public void EditSelectEnum_IsDisabled_renders_a_disabled_select()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.True(cut.Find("select.edit-select-select").HasAttribute("disabled"));
    }

    [Fact]
    public void EditCheckedStringList_IsDisabled_disables_every_checkbox()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.AddAttribute(4, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.All(cut.FindAll("input[type=checkbox]"), c =>
        {
            Assert.True(c.HasAttribute("disabled"));
            Assert.Equal("true", c.GetAttribute("aria-disabled")); // lowercase ARIA boolean, not "True"
        });
    }

    [Fact]
    public void EditRadioString_Other_text_input_respects_IsDisabled()
    {
        var model = new PersonModel { Name = "bespoke" }; // custom value -> Other selected, text box active
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "a", "b" });
            b.AddAttribute(5, "HasOther", true);
            b.AddAttribute(6, "IsDisabled", true);
            b.CloseComponent();
        }));

        // With IsDisabled the Other free-text box must be disabled too. Because Other is the selected
        // option here, the old `disabled="_selectedOption != OtherName"` left it editable — it wrote to
        // the model per keystroke while every radio was disabled. Sibling EditRadioEnum already guarded this.
        Assert.True(cut.Find("#txt-Name-custom-value").HasAttribute("disabled"));
    }

    [Fact]
    public void EditRadio_forwards_the_consumers_class_to_the_group_fieldset_in_edit_mode()
    {
        // EditRadio renders no radio inputs of its own (they come from ChildContent), so the group
        // fieldset is the element that must carry InputBase's CssClass — previously the consumer's
        // class only appeared in the read-only branch and was dropped entirely in edit mode.
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-radio-class");
            b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.Contains("my-radio-class", cut.Find("fieldset.edit-radio-fieldset").ClassList);
    }

    [Fact]
    public void EditRadio_IsDisabled_natively_disables_its_InputRadio_children()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsDisabled", true);
            b.AddAttribute(5, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
                cb.OpenComponent<InputRadio<string>>(2);
                cb.AddAttribute(3, "Value", "b");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        // InputRadioGroup renders no element of its own, so `disabled` must come from the wrapping
        // fieldset — fieldset[disabled] natively disables every descendant radio.
        Assert.True(cut.Find("fieldset.edit-radio-disable-scope").HasAttribute("disabled"));
        Assert.Equal(2, cut.FindAll("input[type=radio]").Count);
    }

    [Fact]
    public void EditRadio_enabled_has_no_disabled_scope_and_selection_still_binds()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        string? changed = null;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, v => changed = v));
            b.AddAttribute(5, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
                cb.OpenComponent<InputRadio<string>>(2);
                cb.AddAttribute(3, "Value", "b");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-radio-disable-scope").HasAttribute("disabled"));
        cut.FindAll("input[type=radio]")[1].Change(new ChangeEventArgs { Value = "b" });
        Assert.Equal("b", changed);
    }

    [Fact]
    public void EditRadioEnum_IsDisabled_disables_every_radio()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsDisabled", true);
            b.CloseComponent();
        }));

        var radios = cut.FindAll("input[type=radio]");
        Assert.NotEmpty(radios);
        Assert.All(radios, r => Assert.True(r.HasAttribute("disabled")));
    }

    [Fact]
    public void EditRadio_declares_a_parameter_for_every_IEditControl_member()
    {
        // EditRadio can't inherit EditControlBase (it must inherit InputRadioGroup to supply the
        // InputRadioContext its <InputRadio> children consume), so it re-declares the IEditControl
        // surface by hand. Guard against drift: every IEditControl property must exist as a [Parameter].
        var radioParameters = typeof(EditRadio<string>)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet();

        var missing = typeof(IEditControl).GetProperties()
            .Select(p => p.Name)
            .Where(name => !radioParameters.Contains(name))
            .ToList();

        Assert.True(missing.Count == 0,
            $"EditRadio<T> is missing [Parameter] declarations for IEditControl members: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EditCheckedStringList_read_only_with_null_value_does_not_throw()
    {
        var model = new PersonModel { Tags = null! }; // required only guarantees set, not non-null
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("input[type=checkbox]")); // read-only renders without an NRE on null Value
    }

    [Fact]
    public void EditCheckedStringList_read_only_option_id_is_sanitized()
    {
        var model = new PersonModel { Tags = new() { "New York" } };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "New York" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        // The per-option read-only id must be sanitized via ToId() — no raw space (invalid/duplicate id).
        Assert.DoesNotContain(' ', cut.Find(".edit-readonly-value").Id ?? "");
    }

    [Fact]
    public void EditCheckedStringList_renders_its_id_in_edit_mode_so_the_validation_summary_link_resolves()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        // ValidationView links each error to "#{id}"; in edit mode the control must render that id on
        // a real element (the fieldset) or the skip-to-field summary link dangles. The cbx-/lbl-/
        // error-msg- ids are all decorated, so only the fieldset can carry the bare {id}.
        Assert.Single(cut.FindAll("#Tags"));
        Assert.Equal("Tags", cut.Find("fieldset.edit-checkedList-fieldset").Id);
    }
}
