using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

// Sample models for binding scenarios. Kept in one file so individual test classes stay focused.

public class PersonModel
{
    [Required]
    [DisplayName("Full Name")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = "";

    [Required]
    [Range(1, 120)]
    public int? Age { get; set; }

    [Required]
    [Description("The person's birth date")]
    public DateTime? BirthDate { get; set; }

    public bool IsActive { get; set; }

    // Tri-state target for EditBoolNullRadio.
    public bool? IsSubscribed { get; set; }

    // Decimal target for culture-sensitive number formatting/parsing tests.
    public decimal? Price { get; set; }

    [Required]
    public Priority? Priority { get; set; }

    public List<string> Tags { get; set; } = [];

    public List<Color> FavoriteColors { get; set; } = [];

    [MinLength(2)]
    [MaxLength(10)]
    public string Username { get; set; } = "";

    [Range(int.MinValue, 100)]
    public int CappedValue { get; set; }

    [Range(0, int.MaxValue)]
    public int FloorValue { get; set; }
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

public enum Color
{
    Red,

    [EnumDisplayName("Forest Green")]
    Green,

    [Display(Name = "Sky Blue")]
    Blue,

    PaleYellow
}

// Shared EditForm-wrapping RenderFragment builders. Consolidates what used to be ~41 byte-identical
// (mod the model parameter's declared type) private `WithForm` copies scattered across test files.
// Exposed as a project-global `using static` (see the <Using Static="true"> item in the .csproj)
// so individual test classes need no per-file import.
internal static class FormRenderHelpers
{
    // The canonical shape: <EditForm Model="model">@inner</EditForm>, no FormOptions cascaded.
    public static RenderFragment WithForm<TModel>(TModel model, RenderFragment inner)
        where TModel : class => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // HidingModeTests' variant: cascades FormOptions only when a non-null instance is supplied,
    // so form-wide options (e.g. IsEditMode) can be exercised without forcing every caller to pass one.
    public static RenderFragment WithForm<TModel>(TModel model, FormOptions? formOptions, RenderFragment inner)
        where TModel : class => builder =>
    {
        if (formOptions is not null)
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
        }
        else
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "Model", model);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
            builder.CloseComponent();
        }
    };

    // FormA11yTests' variant: conditionally adds a DataAnnotationsValidator so a submit can actually
    // run validation.
    public static RenderFragment WithValidatedForm<TModel>(TModel model, bool withValidator, RenderFragment inner)
        where TModel : class => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
        {
            if (withValidator)
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
            }
            inner(content);
        }));
        builder.CloseComponent();
    };

    // EditContext-based analog of WithForm, for tests that need a live EditContext handle (e.g. to
    // call NotifyValidationStateChanged directly) rather than binding through a model:
    // EditForm(editContext) -> CascadingValue<FormOptions> -> inner. Consolidates what used to be
    // byte-identical private `RenderForm` helpers in RequiredResolverTests and PerfGuardTests.
    public static RenderFragment RenderForm(EditContext editContext, FormOptions formOptions, RenderFragment inner) =>
        builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "EditContext", editContext);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<CascadingValue<FormOptions>>(0);
                formContent.AddAttribute(1, "Value", formOptions);
                formContent.AddAttribute(2, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            builder.CloseComponent();
        };

    // ValidationStateTests' variant: adds a DataAnnotationsValidator ahead of the CascadingValue so
    // controls register their fields AND editContext.Validate() actually populates messages.
    // EditForm(editContext) -> DataAnnotationsValidator + CascadingValue<FormOptions> -> inner.
    public static RenderFragment RenderValidatedForm(EditContext editContext, FormOptions formOptions, RenderFragment inner) =>
        builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "EditContext", editContext);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenComponent<CascadingValue<FormOptions>>(1);
                formContent.AddAttribute(2, "Value", formOptions);
                formContent.AddAttribute(3, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            builder.CloseComponent();
        };
}
