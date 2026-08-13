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
/// <para>
/// Plus the id's own lifetime, at the bottom of the file: the resolved element id used to be computed
/// once in <c>OnInitialized</c> and never again, so a runtime <c>Id</c>/<c>IdPrefix</c> change (a
/// control re-used for a different record, a group renaming itself) left the label's <c>for</c>, the
/// <c>aria-describedby</c> targets and the <see cref="FormOptions.FieldIds"/> entry pointing at ids
/// nothing rendered any more.
/// </para>
/// </summary>
public class FieldRegistrationTests : BunitContext
{
    static RenderFragment WithFormAndOptions(PersonModel model, FormOptions formOptions, RenderFragment inner)
        => WithForm(model, formOptions, inner);

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

        var cut = Render<EditForm>(ps => ps
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
        cut.Render(ps => ps.Add(f => f.Model, model));
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags"); // survivor keeps the entry

        showFirst = false;
        cut.Render(ps => ps.Add(f => f.Model, model));
        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags"); // last one out removes it
    }

    // <EditForm><CascadingValue FormOptions>{children}</CascadingValue></EditForm>, with the children
    // supplied by a closure a test can flip between renders so a control is removed (and disposed).
    static RenderFragment<EditContext> FormOptionsScope(FormOptions formOptions, RenderFragment children)
        => _ => b =>
        {
            b.OpenComponent<CascadingValue<FormOptions>>(0);
            b.AddAttribute(1, "Value", formOptions);
            b.AddAttribute(2, "ChildContent", children);
            b.CloseComponent();
        };

    [Fact]
    public void Disposing_a_scalar_control_unregisters_its_field()
    {
        // Scalar controls register in EditControlBase.InitState; the paired unregister lives in its
        // Dispose override. Without it, a control removed behind a conditional @if left a dead
        // FieldIdentifier (and its FieldIds entry) in the long-lived per-form FormOptions, which
        // ValidationView then links to and re-iterates every render.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var show = true;

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                if (!show) return;
                inner.OpenComponent<EditString>(0);
                inner.AddAttribute(1, "Value", model.Name);
                inner.AddAttribute(2, "ValueExpression", field);
                inner.CloseComponent();
            })));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");

        show = false;
        cut.Render(ps => ps.Add(f => f.Model, model));
        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
        Assert.False(formOptions.FieldIds.ContainsKey(FieldIdentifier.Create(field)));
    }

    [Fact]
    public void Disposing_one_of_two_scalar_controls_sharing_a_field_keeps_the_registration()
    {
        // Same owner semantics the list path has (see above), across the scalar base: two controls
        // bound to one property share a single entry, and only the last one out removes it.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var showFirst = true;
        var showSecond = true;

        void AddString(RenderTreeBuilder b, int seq)
        {
            b.OpenComponent<EditString>(seq);
            b.AddAttribute(seq + 1, "Value", model.Name);
            b.AddAttribute(seq + 2, "ValueExpression", field);
            b.CloseComponent();
        }

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                if (showFirst)
                    AddString(inner, 0);
                if (showSecond)
                    AddString(inner, 10);
            })));

        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");

        showSecond = false;
        cut.Render(ps => ps.Add(f => f.Model, model));
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name"); // survivor keeps the entry

        showFirst = false;
        cut.Render(ps => ps.Add(f => f.Model, model));
        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name"); // last one out removes it
    }

    [Fact]
    public void Disposing_the_modal_copy_restores_the_page_control_element_id()
    {
        // FieldIds is last-writer-wins, and the page-section + edit-modal pairing registers the modal
        // LAST (under its own IdPrefix). When the modal closed, the owner set kept the shared entry
        // alive but FieldIds still held the modal's dead DOM id, so ValidationView anchored
        // href="#modal-Name" at an element that no longer existed. The survivor's own id is restored.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var fieldIdentifier = FieldIdentifier.Create(field);
        var showModal = true;

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                inner.OpenComponent<EditString>(0);
                inner.AddAttribute(1, "Value", model.Name);
                inner.AddAttribute(2, "ValueExpression", field);
                inner.CloseComponent();

                if (!showModal) return;
                inner.OpenComponent<EditString>(10);
                inner.AddAttribute(11, "Value", model.Name);
                inner.AddAttribute(12, "ValueExpression", field);
                inner.AddAttribute(13, "IdPrefix", "modal");
                inner.CloseComponent();
            })));

        Assert.Equal("modal-Name", formOptions.FieldIds[fieldIdentifier]); // last writer wins

        showModal = false;
        cut.Render(ps => ps.Add(f => f.Model, model));

        // The shared registration survives (the page control still renders) and now points at the
        // page control's own element, which is still in the DOM.
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
        Assert.Equal("Name", formOptions.FieldIds[fieldIdentifier]);
        Assert.NotNull(cut.Find($"#{formOptions.FieldIds[fieldIdentifier]}"));
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

    // ----- runtime Id/IdPrefix changes ------------------------------------------------------------
    //
    // One representative per control root: EditControlBase (EditString), EditControlListBase
    // (EditCheckedStringList), EditRadio's own copy (EditRadioEnum sits on EditControlBase, so
    // EditRadio<T> is the one that isn't already covered), and EditDateRange's two-id variant.

    // Everything downstream of the resolved id, asserted together — the rendered element, the label
    // that points at it, the describedby tokens derived from it, and the registration the validation
    // summary links through.
    static void AssertIdIsWiredThrough(IRenderedComponent<EditForm> cut, FormOptions formOptions,
        string expectedId, string fieldName, string elementSelector)
    {
        var element = cut.Find(elementSelector);
        Assert.Equal(expectedId, element.Id);
        Assert.Equal(expectedId, cut.Find("label").GetAttribute("for"));
        Assert.Equal($"error-msg-{expectedId}", element.GetAttribute("aria-describedby"));
        var entry = Assert.Single(formOptions.FieldIds, kv => kv.Key.FieldName == fieldName);
        Assert.Equal(expectedId, entry.Value);
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == fieldName);
    }

    [Fact]
    public void Changing_IdPrefix_at_runtime_retargets_a_scalar_control_everywhere()
    {
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var idPrefix = "a";

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                inner.OpenComponent<EditString>(0);
                inner.AddAttribute(1, "Value", model.Name);
                inner.AddAttribute(2, "ValueExpression", field);
                inner.AddAttribute(3, "IdPrefix", idPrefix);
                inner.CloseComponent();
            })));

        AssertIdIsWiredThrough(cut, formOptions, "a-Name", "Name", "input.edit-string-input");

        idPrefix = "b";
        cut.Render(ps => ps.Add(f => f.Model, model));

        // Everything moves together, and nothing is left behind: one FieldIdentifier, one FieldIds
        // entry, and it names the id the element actually renders under.
        AssertIdIsWiredThrough(cut, formOptions, "b-Name", "Name", "input.edit-string-input");
        Assert.Empty(cut.FindAll("#a-Name"));
    }

    [Fact]
    public void Changing_IdPrefix_at_runtime_retargets_a_list_control_everywhere()
    {
        var model = new PersonModel();
        var formOptions = new FormOptions();
        Expression<Func<List<string>>> field = () => model.Tags;
        var idPrefix = "a";

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                inner.OpenComponent<EditCheckedStringList>(0);
                inner.AddAttribute(1, "Value", model.Tags);
                inner.AddAttribute(2, "ValueExpression", field);
                inner.AddAttribute(3, "Options", new List<string> { "x" });
                inner.AddAttribute(4, "IdPrefix", idPrefix);
                inner.CloseComponent();
            })));

        Assert.Equal("a-Tags", cut.Find("fieldset.edit-checkedList-fieldset").Id);

        idPrefix = "b";
        cut.Render(ps => ps.Add(f => f.Model, model));

        Assert.Equal("b-Tags", cut.Find("fieldset.edit-checkedList-fieldset").Id);
        Assert.Equal("error-msg-b-Tags", cut.Find("input[type=checkbox]").GetAttribute("aria-describedby"));
        Assert.NotNull(cut.Find("#lbl-b-Tags"));
        var entry = Assert.Single(formOptions.FieldIds, kv => kv.Key.FieldName == "Tags");
        Assert.Equal("b-Tags", entry.Value);
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Tags");
    }

    [Fact]
    public void Changing_IdPrefix_at_runtime_retargets_EditRadio_everywhere()
    {
        // EditRadio inherits InputRadioGroup, so it keeps its own copy of the id/ARIA plumbing.
        var model = new PersonModel { Priority = Priority.Low };
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> field = () => model.Priority;
        var idPrefix = "a";

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                inner.OpenComponent<EditRadio<Priority?>>(0);
                inner.AddAttribute(1, "Value", model.Priority);
                inner.AddAttribute(2, "ValueExpression", field);
                inner.AddAttribute(3, "IdPrefix", idPrefix);
                inner.AddAttribute(4, "ChildContent", (RenderFragment)(_ => { }));
                inner.CloseComponent();
            })));

        Assert.Equal("a-Priority", cut.Find("fieldset.edit-radio-fieldset").Id);

        idPrefix = "b";
        cut.Render(ps => ps.Add(f => f.Model, model));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("b-Priority", fieldset.Id);
        // aria-labelledby names the group from FormLabel's lbltext- naming anchor (the label text
        // alone), not the lbl- legend that also contains the tooltip trigger -- see FormLabel's
        // remarks. Both ids must still track the re-resolved IdPrefix.
        Assert.Equal("lbltext-b-Priority", fieldset.GetAttribute("aria-labelledby"));
        Assert.Equal("error-msg-b-Priority", fieldset.GetAttribute("aria-describedby"));
        Assert.NotNull(cut.Find("#lbl-b-Priority"));
        Assert.NotNull(cut.Find("#lbltext-b-Priority"));
        var entry = Assert.Single(formOptions.FieldIds, kv => kv.Key.FieldName == "Priority");
        Assert.Equal("b-Priority", entry.Value);
        Assert.Single(formOptions.FieldIdentifiers, fi => fi.FieldName == "Priority");
    }

    [Fact]
    public void Changing_IdPrefix_at_runtime_retargets_both_of_EditDateRanges_ids()
    {
        var model = new RangeModel();
        var formOptions = new FormOptions();
        Expression<Func<DateTime?>> start = () => model.Start;
        Expression<Func<DateTime?>> end = () => model.End;
        var idPrefix = "a";

        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, FormOptionsScope(formOptions, inner =>
            {
                inner.OpenComponent<EditDateRange>(0);
                inner.AddAttribute(1, "Start", model.Start);
                inner.AddAttribute(2, "StartExpression", start);
                inner.AddAttribute(3, "End", model.End);
                inner.AddAttribute(4, "EndExpression", end);
                inner.AddAttribute(5, "IdPrefix", idPrefix);
                inner.CloseComponent();
            })));

        Assert.NotNull(cut.Find("#a-Start"));
        Assert.NotNull(cut.Find("#a-Start-end"));

        idPrefix = "b";
        cut.Render(ps => ps.Add(f => f.Model, model));

        Assert.NotNull(cut.Find("#b-Start"));
        Assert.NotNull(cut.Find("#b-Start-end"));
        Assert.Empty(cut.FindAll("#a-Start"));
        // The End field's id is derived from Start's, and both registrations move with it.
        Assert.Equal("b-Start", formOptions.FieldIds.Single(kv => kv.Key.FieldName == "Start").Value);
        Assert.Equal("b-Start-end", formOptions.FieldIds.Single(kv => kv.Key.FieldName == "End").Value);
        Assert.Equal(2, formOptions.FieldIdentifiers.Count);
    }

    class RangeModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }
}
