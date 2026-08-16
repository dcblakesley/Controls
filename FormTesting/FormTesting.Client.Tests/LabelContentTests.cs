using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="EditControlBase{TValue}.LabelContent"/> — a <see cref="RenderFragment"/> alternative to
/// the plain <c>Label</c> string, mirroring the house <c>Tab.TitleContent</c> pattern
/// (<c>TitleContent ?? Title</c>). These tests pin the constraints that make this feature easy to get
/// wrong (see <see cref="FormLabel"/>'s class remarks): the custom content must land INSIDE the naming
/// anchor (<c>lbltext-{id}</c>), never replace the <c>&lt;label&gt;</c>/<c>&lt;legend&gt;</c> itself, and
/// must compose with the required star, the tooltip trigger (which stays OUTSIDE the anchor), and
/// <c>EditBool</c>'s <c>NestedInput</c> checkbox shape — across all four of <see cref="FormLabel"/>'s
/// rendering branches. <c>Label</c> itself keeps driving validation-message text and other
/// label-derived defaults (e.g. the tooltip trigger's own accessible name) even when only
/// <c>LabelContent</c> is set, since those read the resolved string, never the fragment.
/// </summary>
public class LabelContentTests : BunitContext
{
    // A small, deliberately "phrasing content only" label fragment: a decorative aria-hidden icon
    // followed by text -- exactly the shape documented as safe (no nested button/link).
    static RenderFragment CustomLabel(string iconClass, string text) => b =>
    {
        b.OpenElement(0, "span");
        b.AddAttribute(1, "class", iconClass);
        b.AddAttribute(2, "aria-hidden", "true");
        b.CloseElement();
        b.AddContent(3, text);
    };

    [Fact]
    public void Unset_LabelContent_leaves_the_naming_anchor_holding_only_the_label_text()
    {
        // Username carries no [Required] and no Tooltip, so the visible-label branch renders nothing
        // but the anchor itself -- the simplest possible pin of "nothing changed" for the null path.
        var model = new PersonModel();
        Expression<Func<string>> field = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var label = cut.Find("label.edit-label");
        var anchor = cut.Find("#lbltext-Username");

        Assert.Equal("Username", anchor.TextContent);
        Assert.Empty(anchor.Children);
        // The anchor is the label's only child -- no extra wrapper, no leftover markup. (AngleSharp
        // re-wraps each traversal, so identity checks across separate Find/Children calls always fail;
        // compare by id instead.)
        Assert.Single(label.Children);
        Assert.Equal("lbltext-Username", label.Children[0].Id);
    }

    [Fact]
    public void LabelContent_renders_inside_the_naming_anchor_instead_of_the_label_text()
    {
        var model = new PersonModel();
        Expression<Func<string>> field = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "LabelContent", CustomLabel("custom-icon", "Custom Username"));
            b.CloseComponent();
        }));

        var anchor = cut.Find("#lbltext-Username");
        Assert.NotNull(anchor.QuerySelector("span.custom-icon"));
        Assert.Equal("Custom Username", anchor.TextContent.Trim());
    }

    [Fact]
    public void Required_marker_still_renders_alongside_LabelContent()
    {
        // Name carries [Required] -- the star and the custom content must coexist, with the star
        // staying outside the anchor exactly as it does for the plain-text path.
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "LabelContent", CustomLabel("name-icon", "Name (custom)"));
            b.CloseComponent();
        }));

        var star = cut.Find(".edit-label-required-star");
        Assert.Equal("*", star.TextContent);

        var label = cut.Find("label.edit-label");
        var anchor = cut.Find("#lbltext-Name");
        Assert.Contains("Name (custom)", anchor.TextContent);
        Assert.Contains(label.Children, c => c.ClassList.Contains("edit-label-required-star"));
        Assert.DoesNotContain(anchor.Children, c => c.ClassList.Contains("edit-label-required-star"));
    }

    [Fact]
    public void Tooltip_still_renders_outside_the_naming_anchor_and_keeps_its_own_name_with_LabelContent()
    {
        var model = new PersonModel();
        Expression<Func<string>> field = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Tooltip", "Helpful info");
            b.AddAttribute(4, "LabelContent", CustomLabel("username-icon", "Custom Username"));
            b.CloseComponent();
        }));

        var anchor = cut.Find("#lbltext-Username");
        Assert.Empty(anchor.QuerySelectorAll("button.edit-tooltip-container"));
        var trigger = cut.Find("button.edit-tooltip-container");

        // The trigger's default accessible name is built from the resolved plain Label ("Username"),
        // never from LabelContent -- otherwise a button nesting the custom content's own name in would
        // reproduce the exact "Full Name More information about Full Name" bug this anchor exists to
        // avoid.
        Assert.Equal("More information about Username", trigger.GetAttribute("aria-label"));
    }

    [Fact]
    public void LabelContent_composes_with_EditBool_NestedInput_and_keeps_aria_labelledby_resolving()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "LabelContent", CustomLabel("status-icon", "Active status"));
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        var labelledBy = checkbox.GetAttribute("aria-labelledby");
        Assert.Equal($"lbltext-{checkbox.Id}", labelledBy);

        var anchor = cut.Find($"#{labelledBy}");
        Assert.NotNull(anchor.QuerySelector("span.status-icon"));
        Assert.Equal("Active status", anchor.TextContent.Trim());

        // The checkbox still nests inside its own label, ahead of the anchor -- LabelContent must not
        // disturb the `.edit-checkbox-label > input` shape edit-controls.css depends on.
        var label = cut.Find("label.edit-checkbox-label");
        Assert.Equal("input", label.Children[0].LocalName);
    }

    [Fact]
    public void LabelContent_works_on_a_fieldset_legend_control()
    {
        var model = new PersonModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "LabelContent", CustomLabel("colors-icon", "Pick colors"));
            b.CloseComponent();
        }));

        var legend = cut.Find("legend.edit-label-legend");
        var anchor = cut.Find("#lbltext-FavoriteColors");
        Assert.NotNull(anchor.QuerySelector("span.colors-icon"));
        Assert.Contains("Pick colors", anchor.TextContent);
        Assert.Contains(legend.Children, c => c.Id == "lbltext-FavoriteColors");

        // The fieldset's own aria-labelledby still resolves to the anchor holding the custom content.
        var fieldset = cut.Find("fieldset");
        Assert.Equal("lbltext-FavoriteColors", fieldset.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void LabelContent_works_in_the_hidden_label_branch()
    {
        var model = new PersonModel();
        Expression<Func<string>> field = () => model.Username;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Username);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsLabelHidden", true);
            b.AddAttribute(4, "LabelContent", CustomLabel("hidden-icon", "Hidden Username"));
            b.CloseComponent();
        }));

        var srLabel = cut.Find("label.edit-sr-only");
        var anchor = cut.Find("#lbltext-Username");
        Assert.Contains(srLabel.Children, c => c.Id == "lbltext-Username");
        Assert.NotNull(anchor.QuerySelector("span.hidden-icon"));
        Assert.Contains("Hidden Username", anchor.TextContent);
    }

    [Fact]
    public void LabelContent_works_in_the_hidden_legend_branch()
    {
        var model = new PersonModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsLabelHidden", true);
            b.AddAttribute(4, "LabelContent", CustomLabel("colors-icon", "Pick colors"));
            b.CloseComponent();
        }));

        var legend = cut.Find("legend.edit-sr-only");
        var anchor = cut.Find("#lbltext-FavoriteColors");
        Assert.Contains(legend.Children, c => c.Id == "lbltext-FavoriteColors");
        Assert.NotNull(anchor.QuerySelector("span.colors-icon"));
        Assert.Contains("Pick colors", anchor.TextContent);
    }

    [Fact]
    public void Validation_message_text_still_derives_from_Label_when_only_LabelContent_is_set()
    {
        // Name carries [Required] + [DisplayName("Full Name")]. Only LabelContent is set here (no
        // Label) -- the visible label row should show the custom fragment, but the validation message
        // must still come from the resolved Label/DisplayName text, never from LabelContent (which
        // GetLabelText/FieldValidationDisplay/ValidationView never read).
        var model = new PersonModel { Name = "" };
        Expression<Func<string>> field = () => model.Name;
        var editContext = new EditContext(model);
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "LabelContent", CustomLabel("name-icon", "Shiny Name Field"));
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var screenReader = cut.Find("#error-msg-Name > div").TextContent;
        Assert.Contains("Full Name", screenReader);
        Assert.DoesNotContain("Shiny Name Field", screenReader);

        // The visible label row still shows the custom content -- the two are independent.
        var anchor = cut.Find("#lbltext-Name");
        Assert.Contains("Shiny Name Field", anchor.TextContent);
    }
}
