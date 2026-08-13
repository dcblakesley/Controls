using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// The shared label/naming pipeline — <see cref="AttributesHelper.GetLabelText"/>, the camel-case
/// splitter behind it and <see cref="EnumHelpers.GetName"/>, <c>FormLabel</c> and <c>LabelTooltip</c> —
/// which feeds every Edit* control's visible label, its <c>aria-describedby</c> targets and the
/// validation messages that name the field. Covers the 2026-08-13 audit's INF-1 (the auto-generated
/// label shredded every acronym), INF-5 (a hidden label deleted the tooltip text outright) and INF-6
/// (every tooltip trigger was accessibly named "More information"), plus LST-1's grouped-required cue.
/// </summary>
public class A11yLabelPipelineTests : BunitContext
{
    // ---------- INF-1: the auto-generated label must not shred acronyms ----------

    // Property names only — no [DisplayName]/[Display] — so every one of these takes tier 1 of the
    // labelling contract: auto-generate from the property name.
    class AcronymModel
    {
        public string? Name { get; set; }
        public string? BirthDate { get; set; }
        public string? ID { get; set; }
        public string? URLPath { get; set; }
        public string? HTTPStatus { get; set; }
        public string? CustomerSSN { get; set; }
        public string? XMLParser { get; set; }
        public string? IOError { get; set; }
        public string? AValue { get; set; }
        public string? Address2 { get; set; }
        public string? Address1Line { get; set; }
    }

    readonly AcronymModel _acronyms = new();

    static string LabelFor<T>(Expression<Func<T>> field) =>
        AttributesHelper.GetExpressionCustomAttributes(field).GetLabelText(FieldIdentifier.Create(field));

    [Fact]
    public void Auto_generated_labels_keep_acronyms_whole()
    {
        // Every one of these used to be split on EVERY capital: "U R L Path", "I D", "H T T P Status",
        // "Customer S S N" — in the visible label AND in every message that names the field. The
        // existing coverage was all single-capital-per-word input, which is why it survived.
        Assert.Equal("URL Path", LabelFor(() => _acronyms.URLPath));
        Assert.Equal("ID", LabelFor(() => _acronyms.ID));
        Assert.Equal("HTTP Status", LabelFor(() => _acronyms.HTTPStatus));
        Assert.Equal("Customer SSN", LabelFor(() => _acronyms.CustomerSSN));
        Assert.Equal("XML Parser", LabelFor(() => _acronyms.XMLParser));
        Assert.Equal("IO Error", LabelFor(() => _acronyms.IOError));
        // A one-letter "acronym" is still a boundary: the run of capitals ends into a lower-case word.
        Assert.Equal("A Value", LabelFor(() => _acronyms.AValue));
    }

    [Fact]
    public void Auto_generated_labels_are_unchanged_for_ordinary_names()
    {
        // The regression surface of the acronym fix: single-capital-per-word input must render
        // byte-identically, since bUnit assertions, e2e selectors and visual baselines all pin it.
        Assert.Equal("Birth Date", LabelFor(() => _acronyms.BirthDate));
        Assert.Equal("Name", LabelFor(() => _acronyms.Name));
        // Digits: no space is inserted BEFORE one (Address2 stays whole — the pre-existing rule, left
        // alone deliberately), but a digit does still open the next word, so this boundary reads as
        // it always did.
        Assert.Equal("Address2", LabelFor(() => _acronyms.Address2));
        Assert.Equal("Address1 Line", LabelFor(() => _acronyms.Address1Line));
    }

    // Public because it appears in a [Theory] method's signature, which xUnit requires to be public.
    public enum AcronymStatus
    {
        InProgress,
        ID,
        XMLExport,
        HTTPStatus,
        PDFFileReady
    }

    [Theory]
    [InlineData(AcronymStatus.InProgress, "In Progress")]
    [InlineData(AcronymStatus.ID, "ID")]
    [InlineData(AcronymStatus.XMLExport, "XML Export")]
    [InlineData(AcronymStatus.HTTPStatus, "HTTP Status")]
    [InlineData(AcronymStatus.PDFFileReady, "PDF File Ready")]
    public void Enum_member_names_keep_acronyms_whole(AcronymStatus value, string expected)
    {
        // The other half of the same rule: enum member names take the identical path, and every
        // radio/select/checked-list option label and read-only view renders from it.
        Assert.Equal(expected, value.GetName());
    }

    [Flags]
    enum AcronymPermission
    {
        None = 0,
        ReadXML = 1,
        WriteXML = 2
    }

    [Fact]
    public void A_combined_Flags_value_still_reads_with_single_spaces()
    {
        // The guard the old implementation existed for: a [Flags] ToString() is already "A, B", and
        // splitting after the separator's space produced a memoized double space. Nothing is inserted
        // after whitespace or punctuation under the new rule either.
        Assert.Equal("Read XML, Write XML", (AcronymPermission.ReadXML | AcronymPermission.WriteXML).GetName());
    }

    [Fact]
    public void An_all_caps_option_string_is_left_alone()
    {
        // GetName also runs over plain strings — EditCheckedStringList / EditRadioString option text.
        // "USA" used to render as "U S A" in every one of those lists.
        Assert.Equal("USA", ((object)"USA").GetName());
    }

    [Fact]
    public void The_field_label_and_the_enum_member_name_resolve_through_the_same_rule()
    {
        // The two implementations of this rule had drifted into the same bug independently. They now
        // share one helper; this pins that they cannot part ways again.
        Assert.Equal(AcronymStatus.HTTPStatus.GetName(), LabelFor(() => _acronyms.HTTPStatus));
    }

    // ---------- INF-6: the tooltip trigger's accessible name ----------

    static void AddEditString(RenderTreeBuilder b, PersonModel model, Expression<Func<string>> field,
        params (string Name, object Value)[] extra)
    {
        b.OpenComponent<EditString>(0);
        b.AddAttribute(1, "Value", model.Name);
        b.AddAttribute(2, "ValueExpression", field);
        var seq = 4;
        foreach (var (name, value) in extra)
            b.AddAttribute(seq++, name, value);
        b.CloseComponent();
    }

    IRenderedComponent<ContainerFragment> RenderString(PersonModel model, Expression<Func<string>> field,
        params (string Name, object Value)[] extra) =>
        Render(WithForm(model, b => AddEditString(b, model, field, extra)));

    static string TriggerName(IRenderedComponent<ContainerFragment> cut) =>
        cut.Find("button.edit-tooltip-container").GetAttribute("aria-label")!;

    [Fact]
    public void Tooltip_trigger_is_named_after_the_field_it_belongs_to()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field, ("Tooltip", "Legal name as it appears on your ID"));

        // Every trigger used to be aria-label="More information", so browsing a form by button list
        // gave N identical entries with no way to tell which field each belonged to. PersonModel.Name
        // carries [DisplayName("Full Name")], so that is the resolved label.
        Assert.Equal("More information about Full Name", TriggerName(cut));
    }

    [Fact]
    public void Tooltip_trigger_name_follows_the_labelling_precedence()
    {
        // Not just the property name: the trigger is named from whatever FormLabel resolved, so an
        // explicit Label parameter (highest precedence) wins here too.
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field, ("Label", "Legal Name"), ("Tooltip", "As printed"));

        Assert.Equal("More information about Legal Name", TriggerName(cut));
    }

    [Fact]
    public void FormLabel_TooltipTriggerLabel_overrides_the_generated_sentence()
    {
        // The localization escape hatch, matching the toast containers' CloseButtonLabel: the
        // generated sentence is English, so it has to be replaceable.
        var cut = Render<FormLabel>(p => p
            .Add(l => l.Attributes, new List<Attribute>())
            .Add(l => l.Label, "Farbe")
            .Add(l => l.Tooltip, "Ein Hinweis")
            .Add(l => l.TooltipTriggerLabel, "Weitere Informationen zu Farbe"));

        Assert.Equal("Weitere Informationen zu Farbe",
            cut.Find("button.edit-tooltip-container").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_trigger_falls_back_to_the_bare_phrase_when_there_is_no_label_to_name()
    {
        // EditDisplay renders a FormLabel with no Label and no attribute list, so there is nothing to
        // interpolate — the trigger keeps exactly the name it has always had rather than reading
        // "More information about ".
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Tooltip, "ounces per can")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Equal("More information", cut.Find("button.edit-tooltip-container").GetAttribute("aria-label"));
    }

    [Fact]
    public void A_standalone_LabelTooltip_can_name_its_own_trigger()
    {
        // The Table column-header info icon: no FormLabel above it, so it names itself — both to
        // distinguish it from its neighbors and to localize it.
        var named = Render<LabelTooltip>(p => p
            .Add(t => t.Id, "esd-info")
            .Add(t => t.Tooltip, "Estimated ship date for the full PO.")
            .Add(t => t.TriggerLabel, "More information about ESD"));
        Assert.Equal("More information about ESD",
            named.Find("button.edit-tooltip-container").GetAttribute("aria-label"));

        var unnamed = Render<LabelTooltip>(p => p
            .Add(t => t.Id, "esd-info")
            .Add(t => t.Tooltip, "Estimated ship date for the full PO."));
        Assert.Equal("More information", unnamed.Find("button.edit-tooltip-container").GetAttribute("aria-label"));
    }

    // ---------- INF-5: a hidden label must not delete the tooltip text ----------

    [Fact]
    public void Hidden_label_folds_a_tooltip_only_field_into_the_sr_only_description()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field, ("IsLabelHidden", true), ("Tooltip", "Format: first last"));

        // With no Description of its own, this field previously rendered NO help text at all in this
        // mode: no trigger, no desc- element and no aria-describedby token. The tooltip was the one
        // piece of help that reached nobody — sighted or not — rather than merely being hidden.
        var description = cut.Find("#desc-Name");
        Assert.Contains("edit-sr-only", description.ClassList);
        Assert.Contains("Format: first last", description.TextContent);

        var describedBy = (cut.Find("input.edit-string-input").GetAttribute("aria-describedby") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("desc-Name", describedBy);
        // The interactive trigger is still deliberately absent — there is no visual affordance to
        // reach it in this mode — so tooltip-Name must not be referenced either.
        Assert.DoesNotContain("tooltip-Name", describedBy);
        Assert.Empty(cut.FindAll("#tooltip-Name"));
        // Nothing in the token list may point at a missing element.
        foreach (var token in describedBy) Assert.NotNull(cut.Find("#" + token));
    }

    [Fact]
    public void Hidden_label_keeps_both_the_description_and_the_tooltip_when_a_field_has_both()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field, ("IsLabelHidden", true),
            ("Description", "Format: first last"), ("Tooltip", "Middle names are optional"));

        var text = cut.Find("#desc-Name").TextContent;
        Assert.Contains("Format: first last", text);
        Assert.Contains("Middle names are optional", text);
        // One element, not two: aria-describedby names a single desc-{id}.
        Assert.Single(cut.FindAll("#desc-Name"));
    }

    [Fact]
    public void A_visible_label_does_not_duplicate_the_tooltip_into_the_description()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field,
            ("Description", "Format: first last"), ("Tooltip", "Middle names are optional"));

        // The fold-in is strictly the hidden-label fallback. With a real trigger rendered, repeating
        // its text in desc- would announce the same help twice.
        Assert.DoesNotContain("Middle names are optional", cut.Find("#desc-Name").TextContent);
        Assert.Contains("Middle names are optional", cut.Find("#tooltip-Name").TextContent);
        Assert.Contains("tooltip-Name", cut.Find("input.edit-string-input").GetAttribute("aria-describedby")!);
    }

    // ---------- LST-1: a required group needs a cue assistive tech can actually reach ----------

    // FormLabel inside a real <fieldset>, the shape the checked-list controls render — the legend has
    // to be parsed in its proper parent for the assertions below to mean anything.
    IRenderedComponent<ContainerFragment> RenderLegend(bool requiredTextIncluded,
        bool isLabelHidden = false, FormOptions? formOptions = null)
    {
        RenderFragment legend = b =>
        {
            b.OpenElement(0, "fieldset");
            b.OpenComponent<FormLabel>(1);
            b.AddAttribute(2, "Attributes", new List<Attribute>());
            b.AddAttribute(3, "Id", "Colors");
            b.AddAttribute(4, "Label", "Favorite Colors");
            b.AddAttribute(5, "IsLegend", true);
            b.AddAttribute(6, "IsRequired", (bool?)true);
            b.AddAttribute(7, "IsLabelHidden", isLabelHidden);
            b.AddAttribute(8, "IsRequiredTextIncluded", requiredTextIncluded);
            b.CloseComponent();
            b.CloseElement();
        };

        return Render(formOptions is null ? legend : Cascading(formOptions, legend));
    }

    static RenderFragment Cascading(FormOptions formOptions, RenderFragment inner) => b =>
    {
        b.OpenComponent<CascadingValue<FormOptions>>(0);
        b.AddAttribute(1, "Value", formOptions);
        b.AddAttribute(2, "ChildContent", inner);
        b.CloseComponent();
    };

    [Fact]
    public void A_grouped_legend_can_carry_a_visually_hidden_required_cue()
    {
        // The star is aria-hidden and accessible-name computation skips it, and ARIA 1.2 forbids
        // aria-required on role="group" — so a required checkbox-list group had no required cue for
        // assistive tech anywhere until a submit attempt produced a validation message.
        var cut = RenderLegend(requiredTextIncluded: true);

        var legend = cut.Find("legend");
        var srOnly = legend.QuerySelector("span.edit-sr-only");
        Assert.NotNull(srOnly);
        Assert.Equal("(required)", srOnly!.TextContent.Trim());
        // NOT aria-hidden — the whole point is that it reaches the accessible name.
        Assert.False(srOnly.HasAttribute("aria-hidden"));
        // ...and it reads after the label ("Favorite Colors, required"), not before it.
        Assert.Matches(@"Favorite Colors\s*\(required\)", legend.TextContent);
        // The visible star is unchanged — this is an addition, not a replacement.
        Assert.NotNull(legend.QuerySelector(".edit-label-required-star"));
    }

    [Fact]
    public void The_required_cue_survives_a_hidden_label()
    {
        // Hiding the label is a layout decision; it must not take the required state with it (the
        // hidden legend renders no star at all, so this is the only cue left).
        var cut = RenderLegend(requiredTextIncluded: true, isLabelHidden: true);
        Assert.Contains("(required)", cut.Find("legend").TextContent);
    }

    [Fact]
    public void The_required_cue_survives_a_hidden_star()
    {
        // It stands in for aria-required, which nothing else drops when the star is hidden, so the
        // star's own visibility switch must not silence it.
        var cut = RenderLegend(requiredTextIncluded: true,
            formOptions: new FormOptions { IsRequiredStarHidden = true });

        Assert.Empty(cut.FindAll(".edit-label-required-star"));
        Assert.Contains("(required)", cut.Find("legend").TextContent);
    }

    [Fact]
    public void The_required_cue_is_opt_in_so_it_cannot_double_announce()
    {
        // Off by default: every control whose field, or whose radiogroup fieldset, already carries
        // aria-required must not also put the word into its accessible name, or assistive tech reads
        // "... required, required".
        Assert.Null(RenderLegend(requiredTextIncluded: false).Find("legend")
            .QuerySelector("span.edit-sr-only"));
        Assert.DoesNotContain("(required)",
            RenderLegend(requiredTextIncluded: false, isLabelHidden: true).Find("legend").TextContent);
    }

    [Fact]
    public void The_required_cue_is_scoped_to_the_legend_and_never_reaches_a_plain_label()
    {
        // A plain label names a field that carries aria-required itself; the cue exists only for the
        // grouping roles that can't.
        var cut = Render<FormLabel>(p => p
            .Add(l => l.Attributes, new List<Attribute>())
            .Add(l => l.Label, "Full Name")
            .Add(l => l.IsRequired, true)
            .Add(l => l.IsRequiredTextIncluded, true));

        Assert.DoesNotContain("(required)", cut.Find("label").TextContent);
    }

    // ---------- The lbltext-{id} naming anchor ----------
    //
    // The LabelTooltip trigger has to render INSIDE the label/legend (a <legend> is only a legend
    // while it is the fieldset's first child, so it can't be a sibling), and accessible-name
    // computation folds a descendant button's own name into the name it builds from content. So the
    // name is taken from a span around the label text instead of from the whole label element.

    [Fact]
    public void The_naming_anchor_excludes_the_tooltip_trigger_but_the_label_element_still_contains_it()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderString(model, field, ("Tooltip", "Legal name as it appears on your ID"));

        // The whole point: naming a field from lbl-Name announced "Full Name More information about
        // Full Name". lbltext-Name is just the label text.
        var anchor = cut.Find("#lbltext-Name");
        Assert.Equal("Full Name", anchor.TextContent.Trim());
        Assert.DoesNotContain("More information", anchor.TextContent);
        Assert.Empty(anchor.QuerySelectorAll("button"));

        // ...while the trigger really is still a descendant of the label element (that is the whole
        // reason the anchor has to exist), and the label/for association is untouched.
        var label = cut.Find("#lbl-Name");
        Assert.NotNull(label.QuerySelector("button.edit-tooltip-container"));
        Assert.Equal("Name", label.GetAttribute("for"));
        Assert.Equal("Name", cut.Find("input.edit-string-input").Id);
        // The trigger keeps its own distinct name, and the field keeps its tooltip description.
        Assert.Equal("More information about Full Name", TriggerName(cut));
        Assert.Contains("tooltip-Name", cut.Find("input.edit-string-input").GetAttribute("aria-describedby")!);
    }

    [Fact]
    public void The_naming_anchor_renders_in_every_branch_including_a_hidden_label()
    {
        var model = new PersonModel { Name = "x" };
        Expression<Func<string>> field = () => model.Name;

        // Hidden label: the anchor must still exist, or every aria-labelledby aimed at it dangles.
        var hidden = RenderString(model, field, ("IsLabelHidden", true));
        Assert.Equal("Full Name", hidden.Find("#lbltext-Name").TextContent.Trim());
        Assert.Contains("edit-sr-only", hidden.Find("#lbl-Name").ClassList);

        // Read-only (non-labelable) branch keeps it too.
        var readOnly = RenderString(model, field, ("IsEditMode", false));
        Assert.Equal("Full Name", readOnly.Find("#lbltext-Name").TextContent.Trim());
    }

    [Fact]
    public void A_radio_group_is_named_from_the_anchor_not_the_legend()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Tooltip", "Highest first");
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("lbltext-Priority", fieldset.GetAttribute("aria-labelledby"));
        var anchor = cut.Find("#lbltext-Priority");
        Assert.DoesNotContain("More information", anchor.TextContent);
        // The reference resolves to a real element — a dangling aria-labelledby leaves the group
        // unnamed, which is worse than the verbose name it replaced.
        Assert.NotNull(cut.Find("#" + fieldset.GetAttribute("aria-labelledby")));
    }

    [Fact]
    public void The_grouped_required_cue_lives_INSIDE_the_naming_anchor()
    {
        // The one that would silently un-fix LST-1. A role="group" fieldset cannot carry
        // aria-required (ARIA 1.2), so the sr-only "(required)" being part of the accessible NAME is
        // the entire mechanism. If a refactor ever moves it out of lbltext- — or retargets
        // aria-labelledby past it — the markup still looks correct in the legend while assistive tech
        // silently stops hearing "required". This asserts both halves at once.
        var cut = RenderLegend(requiredTextIncluded: true);

        var anchor = cut.Find("#lbltext-Colors");
        Assert.Contains("(required)", anchor.TextContent);
        Assert.DoesNotContain("More information", anchor.TextContent);
        // Ordering reads naturally: label first, cue second, separated by real whitespace so the
        // computed name is "Favorite Colors (required)" and not "Favorite Colors(required)".
        Assert.Matches(@"Favorite Colors\s+\(required\)", anchor.TextContent);
        // The visual star stays OUT of the anchor (it is aria-hidden and contributes nothing anyway).
        Assert.Empty(anchor.QuerySelectorAll(".edit-label-required-star"));
        Assert.NotNull(cut.Find("legend").QuerySelector(".edit-label-required-star"));
    }

    [Fact]
    public void A_required_radio_group_keeps_aria_required_as_its_only_required_signal()
    {
        // The radio fieldsets DO support aria-required (ARIA 1.2 allows it on radiogroup), so they
        // must never opt into the legend text as well.
        var model = new PersonModel { Priority = Priority.Low }; // [Required]
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("true", cut.Find("fieldset").GetAttribute("aria-required"));
        Assert.DoesNotContain("(required)", cut.Find("legend").TextContent);
    }
}
