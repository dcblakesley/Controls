using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="FormDefaults"/> scopes control defaults to a render tree (per app / per MFE / per
/// circuit) instead of the process-wide <see cref="FormOptions"/> statics. Verifies the resolution
/// chain for both settings it carries: FormOptions instance value → cascaded FormDefaults →
/// FormOptions static default. The statics themselves are deliberately never mutated here — they're
/// process-wide, and xUnit runs test classes in parallel.
/// </summary>
public class FormDefaultsTests : TestContext
{
    // EditForm(editContext) -> DataAnnotationsValidator -> [FormDefaults] -> [CascadingValue<FormOptions>] -> EditString(Name).
    IRenderedFragment RenderNameField(EditContext editContext, PersonModel model,
        (bool? StarHidden, bool? ShowFieldName)? defaults, FormOptions? formOptions)
    {
        Expression<Func<string>> field = () => model.Name;
        RenderFragment control = b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Field", field);
            b.CloseComponent();
        };
        var inner = control;
        if (formOptions is not null)
        {
            var next = inner;
            inner = b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", next);
                b.CloseComponent();
            };
        }
        if (defaults is not null)
        {
            var next = inner;
            inner = b =>
            {
                b.OpenComponent<FormDefaults>(0);
                b.AddAttribute(1, "IsRequiredStarHidden", defaults.Value.StarHidden);
                b.AddAttribute(2, "ShowFieldNameInValidation", defaults.Value.ShowFieldName);
                b.AddAttribute(3, "ChildContent", next);
                b.CloseComponent();
            };
        }
        var content = inner;
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenRegion(1);
                content(formContent);
                formContent.CloseRegion();
            }));
            b.CloseComponent();
        });
    }

    // The ShowFieldNameInValidation setting only affects the *visual* message; the sibling
    // screen-reader copy (#error-msg-*) always includes the field name by design.
    static string VisualMessage(IRenderedFragment cut) =>
        cut.Find("div.edit-validation-message:not(.edit-sr-only)").TextContent;

    [Fact]
    public void Without_FormDefaults_star_shows_and_validation_includes_the_field_name()
    {
        var model = new PersonModel(); // Name [Required] empty
        var editContext = new EditContext(model);
        var cut = RenderNameField(editContext, model, defaults: null, formOptions: null);

        cut.InvokeAsync(() => editContext.Validate());

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        Assert.Contains("Full Name is required", VisualMessage(cut));
    }

    [Fact]
    public void FormDefaults_hides_the_star_and_strips_the_field_name_from_validation()
    {
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var cut = RenderNameField(editContext, model, (StarHidden: true, ShowFieldName: false), formOptions: null);

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Equal("Required", VisualMessage(cut).Trim());
    }

    [Fact]
    public void FormOptions_instance_values_win_over_cascaded_FormDefaults()
    {
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var formOptions = new FormOptions { IsRequiredStarHidden = false, ShowFieldNameInValidation = true };
        var cut = RenderNameField(editContext, model, (StarHidden: true, ShowFieldName: false), formOptions);

        cut.InvokeAsync(() => editContext.Validate());

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        Assert.Contains("Full Name is required", VisualMessage(cut));
    }

    [Fact]
    public void Null_FormDefaults_values_fall_through_to_the_static_defaults()
    {
        // FormDefaults present but with both settings unset — behavior must match no-FormDefaults.
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var cut = RenderNameField(editContext, model, (StarHidden: null, ShowFieldName: null), formOptions: null);

        cut.InvokeAsync(() => editContext.Validate());

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        Assert.Contains("Full Name is required", VisualMessage(cut));
    }

    // EditForm(editContext) -> validator -> outer FormDefaults -> inner FormDefaults -> EditString(Name).
    // The MFE composition shape: host page defaults wrapping an MFE root's own (partial) overrides.
    IRenderedFragment RenderNested(EditContext editContext, PersonModel model,
        (bool? StarHidden, bool? ShowFieldName) outer, (bool? StarHidden, bool? ShowFieldName) inner)
    {
        Expression<Func<string>> field = () => model.Name;
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenComponent<FormDefaults>(1);
                formContent.AddAttribute(2, "IsRequiredStarHidden", outer.StarHidden);
                formContent.AddAttribute(3, "ShowFieldNameInValidation", outer.ShowFieldName);
                formContent.AddAttribute(4, "ChildContent", (RenderFragment)(mid =>
                {
                    mid.OpenComponent<FormDefaults>(0);
                    mid.AddAttribute(1, "IsRequiredStarHidden", inner.StarHidden);
                    mid.AddAttribute(2, "ShowFieldNameInValidation", inner.ShowFieldName);
                    mid.AddAttribute(3, "ChildContent", (RenderFragment)(leaf =>
                    {
                        leaf.OpenComponent<EditString>(0);
                        leaf.AddAttribute(1, "Value", model.Name);
                        leaf.AddAttribute(2, "ValueExpression", field);
                        leaf.AddAttribute(3, "Field", field);
                        leaf.CloseComponent();
                    }));
                    mid.CloseComponent();
                }));
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });
    }

    [Fact]
    public void Nested_FormDefaults_chain_per_property_instead_of_shadowing()
    {
        // Host sets star hiding; the MFE root sets only the validation-name default. The star
        // setting must fall through to the OUTER FormDefaults, not skip past it to the static.
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var cut = RenderNested(editContext, model,
            outer: (StarHidden: true, ShowFieldName: null),
            inner: (StarHidden: null, ShowFieldName: false));

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Equal("Required", VisualMessage(cut).Trim());
    }

    [Fact]
    public void Inner_FormDefaults_wins_over_the_outer_for_the_property_it_sets()
    {
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var cut = RenderNested(editContext, model,
            outer: (StarHidden: true, ShowFieldName: true),
            inner: (StarHidden: false, ShowFieldName: null));

        cut.InvokeAsync(() => editContext.Validate());

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        Assert.Contains("Full Name is required", VisualMessage(cut));
    }

    [Fact]
    public void FormOptions_with_unset_values_falls_through_to_FormDefaults()
    {
        // A form that cascades FormOptions (as most do, for field registration) but leaves the two
        // nullable settings alone must still pick up the tree-level defaults.
        var model = new PersonModel();
        var editContext = new EditContext(model);
        var cut = RenderNameField(editContext, model, (StarHidden: true, ShowFieldName: false), new FormOptions());

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Equal("Required", VisualMessage(cut).Trim());
    }
}
