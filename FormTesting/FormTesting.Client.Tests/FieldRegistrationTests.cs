using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

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
            b.AddAttribute(2, "ValueExpression", field);
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
            b.AddAttribute(4, "ChildContent", (RenderFragment)(_ => { }));
            b.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Priority");
    }

    [Fact]
    public void Disposing_one_of_two_controls_sharing_a_field_keeps_the_registration()
    {
        // Two list controls bound to the same property share one FieldIdentifiers entry (RegisterField
        // dedups). Disposing one (page section + edit modal, modal closes) must not drop the shared
        // entry while the other still renders — only the last registrant's dispose removes it.
        var model = new PersonModel { Tags = [] };
        var formOptions = new FormOptions();
        Expression<Func<List<string>>> field = () => model.Tags;
        var showFirst = true;
        var showSecond = true;

        void AddList(RenderTreeBuilder b, int seq)
        {
            b.OpenComponent<EditCheckedStringList>(seq);
            b.AddAttribute(seq + 1, "Value", model.Tags);
            b.AddAttribute(seq + 2, "ValueExpression", field);
            b.AddAttribute(seq + 3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }

        var cut = RenderComponent<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    if (showFirst)
                        AddList(inner, 0);
                    if (showSecond)
                        AddList(inner, 10);
                }));
                b.CloseComponent();
            })));

        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags");

        showSecond = false;
        cut.SetParametersAndRender(ps => ps.Add(f => f.Model, model));
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags"); // survivor keeps the entry

        showFirst = false;
        cut.SetParametersAndRender(ps => ps.Add(f => f.Model, model));
        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags"); // last one out removes it
    }

    [Fact]
    public void Registering_the_same_field_twice_does_not_duplicate()
    {
        // Two controls bound to the same property (or one re-created) must not grow FieldIdentifiers.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        _ = Render(WithFormAndOptions(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();

            b.OpenComponent<EditString>(4);
            b.AddAttribute(5, "Value", model.Name);
            b.AddAttribute(6, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
    }
}
