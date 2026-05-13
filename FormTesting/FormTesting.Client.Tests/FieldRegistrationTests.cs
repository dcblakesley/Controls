using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Verifies the Phase 2a contract: field registration into <see cref="FormOptions.FieldIdentifiers"/>
/// happens during control init and survives conditional rendering — previously, registration lived
/// in <c>FieldValidationDisplay.OnInitialized</c> and was skipped whenever the control was hidden,
/// so the validation summary couldn't link to those fields.
/// </summary>
public class FieldRegistrationTests : TestContext
{
    static RenderFragment WithFormAndOptions(PersonModel model, FormOptions formOptions, RenderFragment inner)
        => builder =>
        {
            builder.OpenComponent<CascadingValue<FormOptions>>(0);
            builder.AddAttribute(1, "Value", formOptions);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<EditForm>(0);
                b.AddAttribute(1, "Model", model);
                b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        };

    [Fact]
    public void Visible_EditString_registers_field()
    {
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
    }

    [Fact]
    public void Hidden_via_IsHidden_still_registers_field()
    {
        // IsHidden=true skips the entire wrapper render (ShouldShowComponent returns false), so
        // the old FieldValidationDisplay-based registration would silently miss this field.
        // Phase 2a moved registration into InitState which runs regardless.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "IsHidden", true);
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
    }

    [Fact]
    public void Hidden_via_HidingMode_WhenNull_still_registers_field()
    {
        // HidingMode-based hiding also skips the wrapper. Same contract: registration must survive.
        var model = new PersonModel { Name = null! };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNull);
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
    }

    [Fact]
    public void List_control_also_registers_via_EditControlListBase()
    {
        // EditCheckedStringList lives on the sibling EditControlListBase. Same contract applies.
        var model = new PersonModel { Tags = [] };
        var formOptions = new FormOptions();
        Expression<Func<List<string>>> field = () => model.Tags;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "Field", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault); // hides because list is empty
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags");
    }

    [Fact]
    public void EditRadio_registers_via_explicit_InitState_call()
    {
        // EditRadio inherits InputRadioGroup<T> (not EditControlBase) so it registers manually
        // — verify that path works too.
        var model = new PersonModel { Priority = Priority.Low };
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> field = () => model.Priority;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditRadio<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.AddAttribute(4, "ChildContent", (RenderFragment)(_ => { }));
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Priority");
    }
}
