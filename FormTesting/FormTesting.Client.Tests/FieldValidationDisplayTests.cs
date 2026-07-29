using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// The validation-message pipeline end to end: a real DataAnnotations failure through
/// <c>FieldValidationDisplay</c>'s rewrite. The regression these exist for — DataAnnotations formats
/// its messages with <c>ValidationContext.DisplayName</c>, i.e. <c>[Display(Name = "…")]</c> when the
/// property carries one, so a decorated property's message matched none of <c>ValidationHelper</c>'s
/// member-name candidates and the raw framework text ("The Given Name field is required.") rendered.
/// </summary>
public class FieldValidationDisplayTests : BunitContext
{
    // Message text of the two rendered regions for a single EditString, after a real validation pass.
    // The screen-reader region always includes the label; the visible one follows
    // FormOptions.ShowFieldNameInValidation (defaulted to false here so the rewritten short form,
    // which shares no words with the framework text, is what gets asserted).
    (string ScreenReader, string Visible) RenderMessages(
        object model, Expression<Func<string>> field, string value, string fieldName, bool showFieldName = false)
    {
        var editContext = new EditContext(model);
        var options = new FormOptions { ShowFieldNameInValidation = showFieldName };
        var cut = Render(RenderValidatedForm(editContext, options, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", value);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        // One inner <div> per message; every case below produces exactly one failing rule.
        return (cut.Find($"#error-msg-{fieldName} > div").TextContent,
            cut.Find(".edit-validation-message:not(.edit-sr-only) > div").TextContent);
    }

    [Fact]
    public void Required_message_is_rewritten_for_a_Display_Name_property()
    {
        var model = new DisplayNameValidationModel();
        var (screenReader, visible) = RenderMessages(model, () => model.FirstName, model.FirstName, nameof(model.FirstName));
        Assert.Equal("Required", visible);
        Assert.Equal("Given Name is required.", screenReader);
    }

    [Fact]
    public void Required_message_for_a_Display_Name_property_keeps_the_label_when_the_field_name_is_shown()
    {
        var model = new DisplayNameValidationModel();
        var (_, visible) = RenderMessages(
            model, () => model.FirstName, model.FirstName, nameof(model.FirstName), showFieldName: true);
        Assert.Equal("Given Name is required.", visible);
    }

    [Fact]
    public void StringLength_message_is_rewritten_for_a_Display_Name_property()
    {
        // "a" has no [Required] to trip and fails only MinimumLength, so exactly one message renders.
        var model = new DisplayNameValidationModel { ShortName = "a" };
        var (screenReader, visible) = RenderMessages(model, () => model.ShortName, model.ShortName, nameof(model.ShortName));
        Assert.Equal("Must be between 2 and 50 characters", visible);
        Assert.Equal("Chosen Name must be between 2 and 50 characters", screenReader);
    }

    [Fact]
    public void DisplayName_attribute_property_is_unaffected()
    {
        // [DisplayName] never reaches DataAnnotations, so its framework message keeps the member name —
        // the member-name candidate has to stay the first thing tried.
        var model = new DisplayNameValidationModel();
        var (screenReader, visible) = RenderMessages(model, () => model.Nickname, model.Nickname, nameof(model.Nickname));
        Assert.Equal("Required", visible);
        Assert.Equal("Nick Name is required.", screenReader);
    }

    [Fact]
    public void Undecorated_property_is_unaffected()
    {
        var model = new DisplayNameValidationModel();
        var (screenReader, visible) = RenderMessages(model, () => model.PlainField, model.PlainField, nameof(model.PlainField));
        Assert.Equal("Required", visible);
        Assert.Equal("Plain Field is required.", screenReader);
    }

    [Fact]
    public void Localized_Display_Name_is_resolved_under_the_culture_active_at_render_time()
    {
        // Two renders, two UI cultures. DisplayAttribute.GetName() re-invokes its resource property on
        // every call, so the match candidate must be read live: a per-type cache primed by the first
        // render would keep matching "Given Name" and let the raw Spanish-named message through.
        // (The framework's own message template stays English — .NET ships no localized BCL resources.)
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            var english = new DisplayNameValidationModel();
            var (screenReaderEn, visibleEn) = RenderMessages(
                english, () => english.LocalizedName, english.LocalizedName, nameof(english.LocalizedName));
            Assert.Equal("Required", visibleEn);
            Assert.Equal("Given Name is required.", screenReaderEn);

            CultureInfo.CurrentUICulture = new CultureInfo("es-ES");
            var spanish = new DisplayNameValidationModel();
            var (screenReaderEs, visibleEs) = RenderMessages(
                spanish, () => spanish.LocalizedName, spanish.LocalizedName, nameof(spanish.LocalizedName));
            Assert.Equal("Required", visibleEs);
            Assert.Equal("Nombre is required.", screenReaderEs);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}

// Every property here pins one naming-attribute combination against the framework message it produces,
// which is why this model lives with its tests instead of in TestModels.cs.
public class DisplayNameValidationModel
{
    // [Display(Name)] IS what DataAnnotations formats with: "The Given Name field is required."
    [Required]
    [Display(Name = "Given Name")]
    public string FirstName { get; set; } = "";

    [StringLength(50, MinimumLength = 2)]
    [Display(Name = "Chosen Name")]
    public string ShortName { get; set; } = "";

    // [DisplayName] is invisible to DataAnnotations: "The Nickname field is required."
    [Required]
    [DisplayName("Nick Name")]
    public string Nickname { get; set; } = "";

    [Required]
    public string PlainField { get; set; } = "";

    [Required]
    [Display(Name = nameof(ValidationDisplayResources.GivenName), ResourceType = typeof(ValidationDisplayResources))]
    public string LocalizedName { get; set; } = "";
}

// Stand-in for a generated .resx accessor. DisplayAttribute resolves ResourceType by looking up a
// public static string property named after Name and invoking its getter on every GetName() call, so a
// culture-sensitive getter is exactly how a real localized display name behaves.
public static class ValidationDisplayResources
{
    public static string GivenName =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "Nombre" : "Given Name";
}
