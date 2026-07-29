using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// EditBool's checkbox mode now renders through <see cref="FormLabel"/> (via its <c>NestedInput</c>
/// slot — a checkbox label has to wrap its own input) instead of re-implementing the label inline.
/// These tests pin the invariant that drift used to break — the visible required star and
/// <c>aria-required</c> come from one computation site, so a required checkbox can never announce
/// itself required to assistive tech with nothing visible to match — plus the parts of the DOM
/// contract the rewrite had to preserve (the checkbox nesting the <c>.edit-checkbox-label &gt; input</c>
/// CSS depends on, the <c>desc-{id}</c>/<c>tooltip-{id}</c> ARIA targets, and the visually-hidden
/// label path, where the checkbox must stay OUTSIDE the <c>.edit-sr-only</c> label). Disposal is
/// covered here too: EditBool implements <c>IAsyncDisposable</c>, which suppresses Blazor's
/// <c>IDisposable</c> path entirely, so its field unregistration only happens because
/// <c>DisposeAsync</c> chains to the synchronous dispose by hand.
/// </summary>
public class EditBoolLabelTests : BunitContext
{
    class TermsModel
    {
        // [Required] on a bool is the attribute-driven half of the required resolution (the star and
        // aria-required both read it); MustBeTrue-style validation semantics are irrelevant here.
        [Required]
        public bool AcceptTerms { get; set; }

        public bool Optional { get; set; }
    }

    static RenderFragment Bool(Expression<Func<bool>> field, bool value,
        params (string Name, object Value)[] extras) => b =>
    {
        b.OpenComponent<EditBool>(0);
        b.AddAttribute(1, "Value", value);
        b.AddAttribute(2, "ValueExpression", field);
        var seq = 3;
        foreach (var (name, val) in extras)
            b.AddAttribute(seq++, name, val);
        b.CloseComponent();
    };

    [Fact]
    public void Required_checkbox_renders_the_star_and_aria_required_together()
    {
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.AcceptTerms;
        var cut = Render(WithForm(model, null, Bool(field, model.AcceptTerms)));

        // The invariant: both signals or neither. The star was missing here for as long as EditBool
        // hand-rolled its checkbox label, while aria-required was emitted from the same resolution.
        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-required"));
        var star = cut.Find(".edit-label-required-star");
        Assert.Equal("*", star.TextContent);
        Assert.Equal("true", star.GetAttribute("aria-hidden"));

        // ...and the star sits in the checkbox's own label, after the box, so the row reads
        // "[box] * Accept Terms".
        var children = cut.Find("label.edit-checkbox-label").Children.ToList();
        var boxIndex = children.FindIndex(c => c.LocalName == "input");
        var starIndex = children.FindIndex(c => c.ClassList.Contains("edit-label-required-star"));
        Assert.True(boxIndex >= 0, "the checkbox must be a direct child of its label");
        Assert.True(starIndex > boxIndex, $"star ({starIndex}) must follow the box ({boxIndex})");
    }

    [Fact]
    public void Required_via_the_IsRequired_parameter_also_renders_both_signals()
    {
        // The parameter path (RequiredIf-style conditional required-ness) resolves through the same
        // helper as the attribute path.
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.Optional;
        var cut = Render(WithForm(model, null, Bool(field, model.Optional, ("IsRequired", true))));

        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-required"));
        Assert.NotNull(cut.Find(".edit-label-required-star"));
    }

    [Fact]
    public void Required_styled_checkbox_renders_the_star_too()
    {
        // UseStyledCheckbox swaps the fragment for a wrapper span + input; the label around it (and
        // therefore the star) is unchanged.
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.AcceptTerms;
        var cut = Render(WithForm(model, null, Bool(field, model.AcceptTerms, ("UseStyledCheckbox", true))));

        var label = cut.Find("label.edit-checkbox-label.edit-checkbox-label-styled");
        Assert.NotNull(label.QuerySelector(".edit-label-required-star"));
        Assert.NotNull(label.QuerySelector("span.edit-checkbox-wrap > input.edit-checkbox-input-styled"));
        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-required"));
    }

    [Fact]
    public void Optional_checkbox_renders_neither_the_star_nor_aria_required()
    {
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.Optional;
        var cut = Render(WithForm(model, null, Bool(field, model.Optional)));

        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("aria-required"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));
    }

    [Fact]
    public void IsRequiredStarHidden_suppresses_the_star_but_keeps_aria_required()
    {
        // The star is a visual convention a consumer can opt out of (FormOptions -> FormDefaults ->
        // static chain, resolved in FormLabel); aria-required is not — hiding the star must never
        // silently drop the accessible signal.
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.AcceptTerms;
        var cut = Render(WithForm(model, new FormOptions { IsRequiredStarHidden = true },
            Bool(field, model.AcceptTerms)));

        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Equal("true", cut.Find("input[type=checkbox]").GetAttribute("aria-required"));
    }

    [Fact]
    public void Label_description_and_tooltip_keep_their_ids_and_association()
    {
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.Optional;
        var cut = Render(WithForm(model, null, Bool(field, model.Optional,
            ("Description", "Opt in if you like"), ("Tooltip", "More about this flag"))));

        var checkbox = cut.Find("input[type=checkbox]");
        var label = cut.Find("label.edit-checkbox-label");

        // The label still points at the checkbox by id and still wraps it as a DIRECT child --
        // `.edit-checkbox-label > input[type="checkbox"]` in edit-controls.css depends on both.
        Assert.Equal(checkbox.Id, label.GetAttribute("for"));
        Assert.NotNull(cut.Find("label.edit-checkbox-label > input[type=checkbox]"));
        Assert.Contains("Optional", label.TextContent);

        // Description and tooltip keep the ids aria-describedby was built from.
        var description = cut.Find("p.edit-label-description");
        Assert.Equal($"desc-{checkbox.Id}", description.Id);
        Assert.Equal("Opt in if you like", description.TextContent.Trim());
        Assert.Equal($"tooltip-{checkbox.Id}", cut.Find("[role=tooltip]").Id);
        var describedBy = (checkbox.GetAttribute("aria-describedby") ?? "").Split(' ');
        Assert.Contains($"desc-{checkbox.Id}", describedBy);
        Assert.Contains($"tooltip-{checkbox.Id}", describedBy);
    }

    [Fact]
    public void Hidden_label_names_the_checkbox_without_visually_hiding_it()
    {
        // FormLabel's NestedInput slot renders inside the <label> -- except on the IsLabelHidden path,
        // where the label carries .edit-sr-only and nesting the checkbox in it would hide the control
        // the label exists to name. It must render as a sibling there instead.
        var model = new TermsModel();
        Expression<Func<bool>> field = () => model.AcceptTerms;
        var cut = Render(WithForm(model, null, Bool(field, model.AcceptTerms, ("IsLabelHidden", true))));

        var srLabel = cut.Find("label.edit-sr-only");
        var checkbox = cut.Find("input[type=checkbox]");
        Assert.Equal(checkbox.Id, srLabel.GetAttribute("for"));
        Assert.Contains("Accept Terms", srLabel.TextContent);
        Assert.Empty(srLabel.QuerySelectorAll("input"));
        // Sibling of the hidden label, directly under the control wrapper -- i.e. still visible.
        Assert.NotNull(cut.Find(".edit-control-wrapper > input[type=checkbox]"));

        // A hidden label suppresses the visible label row entirely -- star included (aria-required is
        // the only required signal left, exactly as for every other control).
        Assert.Empty(cut.FindAll("label.edit-checkbox-label"));
        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Equal("true", checkbox.GetAttribute("aria-required"));
    }

    [Fact]
    public void Disposing_an_EditBool_unregisters_its_field()
    {
        // EditBool declares IAsyncDisposable, and Blazor's disposal treats the two dispose interfaces
        // as mutually exclusive -- so InputBase's IDisposable.Dispose (and with it EditControlBase's
        // Dispose(bool), which drops the registration) is only reached because EditBool.DisposeAsync
        // calls it. Without that chaining, a checkbox removed behind a conditional @if leaves a dead
        // FieldIdentifier in the long-lived per-form FormOptions that ValidationView links to.
        var model = new TermsModel();
        var formOptions = new FormOptions();
        Expression<Func<bool>> field = () => model.AcceptTerms;
        var show = true;

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    if (!show) return;
                    inner.OpenComponent<EditBool>(0);
                    inner.AddAttribute(1, "Value", model.AcceptTerms);
                    inner.AddAttribute(2, "ValueExpression", field);
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            })));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "AcceptTerms");

        show = false;
        cut.Render(ps => ps.Add(f => f.Model, model));

        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "AcceptTerms");
        Assert.False(formOptions.FieldIds.ContainsKey(FieldIdentifier.Create(field)));
    }
}
