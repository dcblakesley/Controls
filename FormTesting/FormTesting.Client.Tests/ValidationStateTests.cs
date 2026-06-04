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
public class ValidationStateTests : TestContext
{
    // EditForm(editContext) -> DataAnnotationsValidator + CascadingValue<FormOptions> -> inner.
    // FormOptions is cascaded so the controls register their fields (ValidationView reads that list).
    IRenderedFragment RenderForm(EditContext editContext, FormOptions formOptions, RenderFragment inner) =>
        Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenComponent<CascadingValue<FormOptions>>(1);
                formContent.AddAttribute(2, "Value", formOptions);
                formContent.AddAttribute(3, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });

    [Fact]
    public void Invalid_required_field_renders_aria_invalid_and_the_rewritten_message()
    {
        var model = new PersonModel(); // Name = "" -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.CloseComponent();
        });

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
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.CloseComponent();
        });

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
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditRadioString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.AddAttribute(4, "Options", new List<string> { "a", "b" });
            content.CloseComponent();
        });

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
                content.AddAttribute(2, "Field", field);
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
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.CloseComponent();
            content.OpenComponent<ValidationView>(4);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var links = cut.FindAll("a.validation-summary-message");
        Assert.NotEmpty(links);
        Assert.Contains(links, a => a.GetAttribute("href") == "#Name");
    }

    [Fact]
    public void ValidationView_throws_when_rendered_without_a_cascading_EditContext()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => RenderComponent<ValidationView>());
        Assert.Contains(nameof(EditContext), ex.Message);
    }

    [Fact]
    public void ValidationView_links_use_the_resolved_id_including_IdPrefix()
    {
        var model = new PersonModel(); // Name [Required] empty
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.AddAttribute(4, "IdPrefix", "foo");
            content.CloseComponent();
            content.OpenComponent<ValidationView>(5);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        // The summary link must target the control's actual id (foo-Name), not a recomputed "#Name".
        var link = cut.Find("a.validation-summary-message");
        Assert.Equal("#foo-Name", link.GetAttribute("href"));
        Assert.Single(cut.FindAll("#foo-Name")); // ...and the control really renders that id
    }
}
