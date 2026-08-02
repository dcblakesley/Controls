using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Exercises the validation/error-state rendering path that the suite previously never touched: that an
/// invalid field reports <c>aria-invalid</c> + the rewritten message, that <c>aria-errormessage</c> is
/// emitted only while invalid, that grouped controls surface the state on their <c>role="radiogroup"</c>
/// container, and that <see cref="ValidationView"/> links to each error (and guards its EditContext).
/// </summary>
public class ValidationStateTests : BunitContext
{
    [Fact]
    public void Invalid_required_field_renders_aria_invalid_and_the_rewritten_message()
    {
        var model = new PersonModel(); // Name = "" -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal("error-msg-Name", input.GetAttribute("aria-errormessage"));
        // The element aria-errormessage points at carries the (ValidationHelper-rewritten) message.
        var message = cut.Find("#error-msg-Name").TextContent;
        Assert.Contains("required", message);
        Assert.DoesNotContain("The Full Name field", message); // confirms the rewrite ran, not the raw .NET text
    }

    [Fact]
    public void Valid_field_omits_aria_invalid_and_aria_errormessage()
    {
        var model = new PersonModel { Name = "Alice" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var input = cut.Find("input.edit-string-input");
        Assert.Null(input.GetAttribute("aria-invalid"));            // attribute omitted when valid
        Assert.False(input.HasAttribute("aria-errormessage"));      // only present while invalid (ARIA spec)
    }

    [Fact]
    public void Invalid_radio_group_sets_aria_invalid_on_the_radiogroup_container()
    {
        var model = new PersonModel(); // Name = "" -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditRadioString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(4, "Options", new List<string> { "a", "b" });
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var group = cut.Find("[role=radiogroup]");
        Assert.Equal("true", group.GetAttribute("aria-invalid"));
        Assert.Equal("true", group.GetAttribute("aria-required"));
        Assert.Equal("error-msg-Name", group.GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void Checkbox_list_reactively_marks_each_checkbox_aria_invalid_when_validation_fails()
    {
        var model = new PersonModel { Tags = [] };
        var editContext = new EditContext(model);
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditCheckedStringList>(0);
                content.AddAttribute(1, "Value", model.Tags);
                content.AddAttribute(2, "ValueExpression", field);
                content.AddAttribute(3, "Options", new List<string> { "a", "b" });
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        // Initially valid — no aria-invalid.
        Assert.All(cut.FindAll("input[type=checkbox]"), c => Assert.Null(c.GetAttribute("aria-invalid")));

        // Push an error AFTER the first render and notify. List controls are ComponentBase (not
        // InputBase), so this only updates if the base subscribes to OnValidationStateChanged.
        // (Tags has no DataAnnotation, so the message is pushed via a store directly.)
        var store = new ValidationMessageStore(editContext);
        cut.InvokeAsync(() =>
        {
            store.Add(editContext.Field(nameof(PersonModel.Tags)), "Pick at least one");
            editContext.NotifyValidationStateChanged();
        });

        var checkboxes = cut.FindAll("input[type=checkbox]");
        Assert.NotEmpty(checkboxes);
        Assert.All(checkboxes, c => Assert.Equal("true", c.GetAttribute("aria-invalid")));
    }

    [Fact]
    public void ValidationView_links_to_each_invalid_field()
    {
        var model = new PersonModel(); // Name [Required] empty
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
            content.OpenComponent<ValidationView>(4);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var links = cut.FindAll("a.validation-summary-message");
        Assert.NotEmpty(links);
        Assert.Contains(links, a => a.GetAttribute("href") == "#Name");
    }

    [Fact]
    public void ValidationView_throws_when_rendered_without_a_cascading_EditContext()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Render<ValidationView>());
        Assert.Contains(nameof(EditContext), ex.Message);
    }

    [Fact]
    public void Scalar_control_renders_standalone_without_an_EditForm()
    {
        // InputBase supports standalone use (null EditContext) since .NET 8. IsInvalid must guard the
        // null context, else aria-invalid=@(IsInvalid...) NREs on first render. (Regression: H2.)
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;

        var cut = Render(b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        });

        var input = cut.Find("input.edit-string-input");
        Assert.Null(input.GetAttribute("aria-invalid")); // no context -> no validation -> not invalid
    }

    [Fact]
    public void EditRadio_renders_standalone_without_an_EditForm()
    {
        // EditRadio inherits InputRadioGroup (also standalone-capable since .NET 8); its hand-rolled
        // IsInvalid needs the same null-context guard as the shared base. (Regression: H2.)
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;

        var cut = Render(b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
                cb.OpenComponent<InputRadio<string>>(2);
                cb.AddAttribute(3, "Value", "b");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        });

        var group = cut.Find("[role=radiogroup]");
        Assert.Null(group.GetAttribute("aria-invalid"));
        Assert.Equal(2, cut.FindAll("input[type=radio]").Count);
    }

    [Fact]
    public void ValidationView_links_use_the_resolved_id_including_IdPrefix()
    {
        var model = new PersonModel(); // Name [Required] empty
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(4, "IdPrefix", "foo");
            content.CloseComponent();
            content.OpenComponent<ValidationView>(5);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        // The summary link must target the control's actual id (foo-Name), not a recomputed "#Name".
        var link = cut.Find("a.validation-summary-message");
        Assert.Equal("#foo-Name", link.GetAttribute("href"));
        Assert.Single(cut.FindAll("#foo-Name")); // ...and the control really renders that id
    }

    [Fact]
    public void ValidationView_re_points_its_validation_subscription_at_a_swapped_EditContext()
    {
        // ValidationView hand-rolled this sequence; it now runs the shared ValidationStateSubscription
        // (the same one EditControlParametersBase drives). The contract to keep: after a context swap,
        // a validation-state change on the NEW context re-renders the summary all by itself — nothing
        // else re-renders it between the swap and the assertion.
        var model = new PersonModel();
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var fieldIdentifier = FieldIdentifier.Create(field);
        formOptions.RegisterField(fieldIdentifier, "Name");

        RenderFragment content = b =>
        {
            b.OpenComponent<CascadingValue<FormOptions>>(0);
            b.AddAttribute(1, "Value", formOptions);
            b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<ValidationView>(0);
                inner.CloseComponent();
            }));
            b.CloseComponent();
        };

        var first = new EditContext(model);
        var cut = Render<CascadingValue<EditContext>>(ps => ps
            .Add(c => c.Value, first)
            .Add(c => c.ChildContent, content));
        Assert.Empty(cut.FindAll("a.validation-summary-message"));

        var second = new EditContext(model);
        cut.Render(ps => ps
            .Add(c => c.Value, second)
            .Add(c => c.ChildContent, content));

        var store = new ValidationMessageStore(second);
        cut.InvokeAsync(() =>
        {
            store.Add(fieldIdentifier, "Boom");
            second.NotifyValidationStateChanged();
        });

        var links = cut.FindAll("a.validation-summary-message");
        Assert.Single(links);
        Assert.Equal("Boom", links[0].TextContent);
    }
}
