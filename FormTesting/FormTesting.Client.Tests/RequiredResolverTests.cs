using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the alternative-validation-stack story: <see cref="FormOptions.RequiredResolver"/> (the
/// FluentValidation bridge point) drives the star + <c>aria-required</c> without any [Required]
/// attribute; the three-state <c>IsRequired</c> parameter can force required-ness off; and messages
/// added by a non-DataAnnotations validator (simulated with a raw <see cref="ValidationMessageStore"/>,
/// which is all any validator does) render verbatim with the full invalid-state ARIA wiring.
/// </summary>
public class RequiredResolverTests : TestContext
{
    // EditForm(editContext) -> CascadingValue<FormOptions> -> inner. No DataAnnotationsValidator:
    // these tests exercise the validator-agnostic paths.
    IRenderedFragment RenderForm(EditContext editContext, FormOptions formOptions, RenderFragment inner) =>
        Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<CascadingValue<FormOptions>>(0);
                formContent.AddAttribute(1, "Value", formOptions);
                formContent.AddAttribute(2, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });

    [Fact]
    public void RequiredResolver_shows_star_and_aria_required_without_the_attribute()
    {
        // Username carries no [Required] — the form-level resolver is the only source, exactly
        // the shape of a FluentValidation NotEmpty rule surfaced through the bridge.
        var model = new PersonModel { Username = "bob" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Username;
        var formOptions = new FormOptions
        {
            RequiredResolver = f => f.FieldName == nameof(PersonModel.Username),
        };
        var cut = RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Username);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("true", input.GetAttribute("aria-required"));
        Assert.NotNull(cut.Find(".edit-label-required-star")); // star and aria-required agree
    }

    [Fact]
    public void Conditional_resolver_updates_star_and_aria_required_together_on_rerender()
    {
        // The resolver reads mutable model state (a conditional-required rule — the feature's
        // stated purpose). A parent re-render must move BOTH signals: the bases recompute
        // aria-required each parameter cycle, and the star must follow because the control passes
        // its RESOLVED required-ness to FormLabel (one computation site). Round-6 regression: the
        // FormLabel input guard alone left the star frozen while aria-required updated.
        var required = false;
        var model = new PersonModel { Username = "bob" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Username;
        var formOptions = new FormOptions { RequiredResolver = _ => required };
        var cut = RenderComponent<EditForm>(ps => ps
            .Add(f => f.EditContext, editContext)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<CascadingValue<FormOptions>>(0);
                formContent.AddAttribute(1, "Value", formOptions);
                formContent.AddAttribute(2, "ChildContent", (RenderFragment)(content =>
                {
                    content.OpenComponent<EditString>(0);
                    content.AddAttribute(1, "Value", model.Username);
                    content.AddAttribute(2, "ValueExpression", field);
                    content.CloseComponent();
                }));
                formContent.CloseComponent();
            })));

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-required"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));

        // Flip the condition the resolver reads, then re-parameterize from the top (what a real
        // parent re-render does when the model state driving the condition changes).
        required = true;
        cut.SetParametersAndRender();

        Assert.Equal("true", cut.Find("input.edit-string-input").GetAttribute("aria-required"));
        Assert.NotNull(cut.Find(".edit-label-required-star"));

        // And back off again — the star must not latch.
        required = false;
        cut.SetParametersAndRender();

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-required"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));
    }

    [Fact]
    public void RequiredResolver_returning_false_leaves_the_field_optional()
    {
        var model = new PersonModel { Username = "bob" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Username;
        var formOptions = new FormOptions { RequiredResolver = _ => false };
        var cut = RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Username);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-required"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));
    }

    [Fact]
    public void IsRequired_false_suppresses_star_and_aria_required_despite_Required_attribute()
    {
        // The force-off half of the three-state escape hatch: a RequiredAttribute-derived
        // conditional (e.g. RequiredIf) whose condition is currently off can now drop the star.
        var model = new PersonModel { Name = "Alice" }; // Name has [Required]
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(4, "IsRequired", false);
            content.CloseComponent();
        });

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("aria-required"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));
    }

    [Fact]
    public void Messages_from_a_non_DataAnnotations_validator_render_verbatim_with_invalid_aria()
    {
        // Simulates FluentValidation (or any custom validator): messages arrive via a
        // ValidationMessageStore, not DataAnnotations. They must display untouched — the
        // ValidationHelper rewrite only matches the .NET DataAnnotations templates — and the
        // invalid-state ARIA (aria-invalid, aria-errormessage) must still activate.
        var model = new PersonModel { Username = "" };
        var editContext = new EditContext(model);
        var store = new ValidationMessageStore(editContext);
        Expression<Func<string>> field = () => model.Username;
        var cut = RenderForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Username);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        const string fvStyleMessage = "'Username' must not be empty.";
        cut.InvokeAsync(() =>
        {
            store.Add(editContext.Field(nameof(PersonModel.Username)), fvStyleMessage);
            editContext.NotifyValidationStateChanged();
        });

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.Equal("error-msg-Username", input.GetAttribute("aria-errormessage"));
        Assert.Contains(fvStyleMessage, cut.Find("#error-msg-Username").TextContent);
    }
}
