using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="EditRadioEnum{TEnum}"/>'s SECOND bound property — the "Other" free-text box's
/// <c>OtherValue</c>/<c>OtherValueChanged</c> pair — and the <c>OtherValueExpression</c> that turns it
/// into a real field.
/// </summary>
/// <remarks>
/// <para>
/// The gap: that pair had no <c>ValueExpression</c>, no <see cref="FieldIdentifier"/>, no
/// <see cref="FormOptions"/> registration and never called <c>NotifyFieldChanged</c> — so typing in the
/// box wrote the model and raised ZERO events. A <c>FormAutoSave</c> (or any other
/// <c>OnFieldChanged</c>-driven consumer) silently lost the free text.
/// </para>
/// <para>
/// The fix is opt-in by binding: <c>@bind-OtherValue</c> supplies <c>OtherValueExpression</c>, which
/// is what enables the notification and the registration. A consumer wiring the pair by hand keeps
/// today's exact behavior rather than being made to throw — both paths are pinned below.
/// <see cref="EditRadioString"/> needs none of this: its Other text IS the bound value.
/// </para>
/// </remarks>
public class EditRadioEnumOtherValueBindingTests : BunitContext
{
    // Two bound properties, which PersonModel has no matching pair of. Critical is the LAST enum value,
    // so with HasOtherOption it is the "Other" slot.
    class ReasonModel
    {
        public Priority? Priority { get; set; }
        public string? Reason { get; set; }
    }

    class RequiredReasonModel
    {
        public Priority? Priority { get; set; }
        [Required]
        public string? Reason { get; set; }
    }

    static string OtherBoxId => "other-Priority";

    // Renders the control inside an EditForm over `editContext`, optionally supplying
    // OtherValueExpression -- the single switch every test here turns on or off.
    IRenderedComponent<ContainerFragment> RenderRadioEnum(
        ReasonModel model, EditContext editContext, bool bindOther, Action<string?>? onOther = null)
    {
        Expression<Func<Priority?>> valueField = () => model.Priority;
        Expression<Func<string?>> otherField = () => model.Reason;
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditRadioEnum<Priority?>>(0);
                content.AddAttribute(1, "Value", model.Priority);
                content.AddAttribute(2, "ValueExpression", valueField);
                content.AddAttribute(3, "ValueChanged",
                    EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
                content.AddAttribute(4, "HasOtherOption", true);
                content.AddAttribute(5, "OtherValue", model.Reason);
                content.AddAttribute(6, "OtherValueChanged", EventCallback.Factory.Create<string?>(this, v =>
                {
                    model.Reason = v;
                    onOther?.Invoke(v);
                }));
                if (bindOther)
                    content.AddAttribute(7, "OtherValueExpression", otherField);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });
    }

    static List<string> Track(EditContext editContext)
    {
        var notified = new List<string>();
        editContext.OnFieldChanged += (_, e) => notified.Add(e.FieldIdentifier.FieldName);
        return notified;
    }

    // ───────────────────────────── bound: it notifies ─────────────────────────────

    [Fact]
    public void Typing_in_the_other_box_notifies_the_OtherValue_field_exactly_once()
    {
        var model = new ReasonModel { Priority = Priority.Critical }; // Other already selected
        var editContext = new EditContext(model);
        var notified = Track(editContext);
        var cut = RenderRadioEnum(model, editContext, bindOther: true);

        cut.Find($"#{OtherBoxId}").Input("bespoke");

        Assert.Equal("bespoke", model.Reason);
        Assert.Equal(["Reason"], notified);
    }

    [Fact]
    public void Re_committing_the_same_other_text_is_silent()
    {
        // Guarded by the same OtherValue != value test as the model write, matching every other
        // control's "same value, no event" dedup.
        var model = new ReasonModel { Priority = Priority.Critical, Reason = "bespoke" };
        var editContext = new EditContext(model);
        var notified = Track(editContext);
        var cut = RenderRadioEnum(model, editContext, bindOther: true);

        cut.Find($"#{OtherBoxId}").Input("bespoke");

        Assert.Empty(notified);
    }

    [Fact]
    public void Switching_away_from_other_notifies_the_OtherValue_field_it_clears()
    {
        // The control takes the free text OFF the model when a real option wins (while still showing
        // it). That is a write to the bound property, so it has to be heard too -- otherwise an
        // auto-save persists the enum change while missing the text going away.
        var model = new ReasonModel { Priority = Priority.Critical, Reason = "bespoke" };
        var editContext = new EditContext(model);
        var notified = Track(editContext);
        var cut = RenderRadioEnum(model, editContext, bindOther: true);

        var radios = cut.FindAll("input[type=radio]");
        radios[0].Change(radios[0].GetAttribute("value")); // Low -- not the Other slot

        Assert.Null(model.Reason);
        Assert.Contains("Reason", notified);
        // The enum's own two notifications (inner InputRadioGroup + this control) are unaffected.
        Assert.Equal(2, notified.Count(n => n == "Priority"));
    }

    /// <summary>
    /// A stand-in for a real consumer's <c>@bind-OtherValue</c> page: its <see cref="EventCallback"/>s
    /// name a COMPONENT as receiver, so Blazor re-renders it after each one and the control sees the
    /// updated <c>Value</c>/<c>OtherValue</c> parameters echoed back. The other tests' render fragments
    /// are owned by the test class, which is not an <c>IHandleEvent</c>, so no echo happens there —
    /// fine for a single write, but the switch-BACK branch is explicitly gated on
    /// <c>string.IsNullOrEmpty(OtherValue)</c> and needs the echo of the preceding clear.
    /// </summary>
    sealed class RadioEnumHost : ComponentBase
    {
        [Parameter] public ReasonModel Model { get; set; } = default!;
        [Parameter] public EditContext Context { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", Context);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditRadioEnum<Priority?>>(0);
                content.AddAttribute(1, "Value", Model.Priority);
                content.AddAttribute(2, "ValueExpression", (Expression<Func<Priority?>>)(() => Model.Priority));
                content.AddAttribute(3, "ValueChanged",
                    EventCallback.Factory.Create<Priority?>(this, v => Model.Priority = v));
                content.AddAttribute(4, "HasOtherOption", true);
                content.AddAttribute(5, "OtherValue", Model.Reason);
                content.AddAttribute(6, "OtherValueChanged",
                    EventCallback.Factory.Create<string?>(this, v => Model.Reason = v));
                content.AddAttribute(7, "OtherValueExpression", (Expression<Func<string?>>)(() => Model.Reason));
                content.CloseComponent();
            }));
            b.CloseComponent();
        }
    }

    [Fact]
    public void Switching_back_to_other_re_commits_the_preserved_text_and_notifies()
    {
        var model = new ReasonModel { Priority = Priority.Critical, Reason = "bespoke" };
        var editContext = new EditContext(model);
        var cut = Render<RadioEnumHost>(ps => ps.Add(c => c.Model, model).Add(c => c.Context, editContext));

        var radios = cut.FindAll("input[type=radio]");
        radios[0].Change(radios[0].GetAttribute("value")); // away: Reason cleared off the model
        Assert.Null(model.Reason);

        var notified = Track(editContext); // count only the trip back
        radios = cut.FindAll("input[type=radio]");
        radios[^1].Change(radios[^1].GetAttribute("value")); // back to the Other slot

        Assert.Equal("bespoke", model.Reason);
        Assert.Contains("Reason", notified);
    }

    // ───────────────────────────── bound: it registers ─────────────────────────────

    [Fact]
    public void The_OtherValue_field_registers_under_the_free_text_boxs_own_element_id()
    {
        var model = new ReasonModel { Priority = Priority.Critical };
        var editContext = new EditContext(model);
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> valueField = () => model.Priority;
        Expression<Func<string?>> otherField = () => model.Reason;

        var cut = Render(RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(0);
            content.AddAttribute(1, "Value", model.Priority);
            content.AddAttribute(2, "ValueExpression", valueField);
            content.AddAttribute(3, "HasOtherOption", true);
            content.AddAttribute(4, "OtherValue", model.Reason);
            content.AddAttribute(5, "OtherValueExpression", otherField);
            content.CloseComponent();
        }));

        Assert.Contains(formOptions.FieldIdentifiers, f => f.FieldName == nameof(ReasonModel.Reason));
        var reason = formOptions.FieldIdentifiers.First(f => f.FieldName == nameof(ReasonModel.Reason));
        Assert.Equal(OtherBoxId, formOptions.FieldIds[reason]);
        Assert.NotNull(cut.Find($"#{OtherBoxId}")); // the id the registration points at really renders
    }

    [Fact]
    public void A_validation_message_on_the_OtherValue_property_now_gets_a_summary_link_to_its_box()
    {
        // The behavioral consequence worth calling out: binding OtherValue enrolls it in the
        // validation summary. Before, an annotation on that property produced a message nothing could
        // link to, because no control had registered the field.
        var model = new RequiredReasonModel { Priority = Priority.Critical };
        var editContext = new EditContext(model);
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> valueField = () => model.Priority;
        Expression<Func<string?>> otherField = () => model.Reason;

        var cut = Render(RenderValidatedForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(0);
            content.AddAttribute(1, "Value", model.Priority);
            content.AddAttribute(2, "ValueExpression", valueField);
            content.AddAttribute(3, "HasOtherOption", true);
            content.AddAttribute(4, "OtherValue", model.Reason);
            content.AddAttribute(5, "OtherValueExpression", otherField);
            content.CloseComponent();
            content.OpenComponent<ValidationView>(10);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var links = cut.FindAll("a.validation-summary-message");
        Assert.Contains(links, a => a.GetAttribute("href") == $"#{OtherBoxId}");
    }

    [Fact]
    public async Task Disposal_drops_the_OtherValue_registration_too()
    {
        var model = new ReasonModel { Priority = Priority.Critical };
        var editContext = new EditContext(model);
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> valueField = () => model.Priority;
        Expression<Func<string?>> otherField = () => model.Reason;

        Render(RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(0);
            content.AddAttribute(1, "Value", model.Priority);
            content.AddAttribute(2, "ValueExpression", valueField);
            content.AddAttribute(3, "HasOtherOption", true);
            content.AddAttribute(4, "OtherValue", model.Reason);
            content.AddAttribute(5, "OtherValueExpression", otherField);
            content.CloseComponent();
        }));
        Assert.Equal(2, formOptions.FieldIdentifiers.Count);

        await DisposeComponentsAsync();

        // Both registrations go -- an unpaired second one would grow FormOptions on every
        // mount/unmount cycle and leave a dead link in the summary.
        Assert.Empty(formOptions.FieldIdentifiers);
    }

    // ───────────────────────────── unbound: unchanged ─────────────────────────────

    [Fact]
    public void Without_OtherValueExpression_the_box_still_writes_the_model_but_stays_silent()
    {
        // Graceful, not a throw: a consumer driving OtherValue/OtherValueChanged by hand keeps exactly
        // the behavior they have today. The notification is opt-in by binding.
        var model = new ReasonModel { Priority = Priority.Critical };
        var editContext = new EditContext(model);
        var notified = Track(editContext);
        string? captured = null;
        var cut = RenderRadioEnum(model, editContext, bindOther: false, onOther: v => captured = v);

        cut.Find($"#{OtherBoxId}").Input("bespoke");

        Assert.Equal("bespoke", model.Reason);
        Assert.Equal("bespoke", captured);
        Assert.Empty(notified);
    }

    [Fact]
    public void Without_OtherValueExpression_nothing_extra_is_registered()
    {
        var model = new ReasonModel { Priority = Priority.Critical };
        var editContext = new EditContext(model);
        var formOptions = new FormOptions();
        Expression<Func<Priority?>> valueField = () => model.Priority;

        Render(RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(0);
            content.AddAttribute(1, "Value", model.Priority);
            content.AddAttribute(2, "ValueExpression", valueField);
            content.AddAttribute(3, "HasOtherOption", true);
            content.AddAttribute(4, "OtherValue", model.Reason);
            content.CloseComponent();
        }));

        Assert.Equal([nameof(ReasonModel.Priority)], formOptions.FieldIdentifiers.Select(f => f.FieldName));
    }

    [Fact]
    public void Without_OtherValueExpression_a_switch_away_still_clears_the_model_silently()
    {
        var model = new ReasonModel { Priority = Priority.Critical, Reason = "bespoke" };
        var editContext = new EditContext(model);
        var notified = Track(editContext);
        var cut = RenderRadioEnum(model, editContext, bindOther: false);

        var radios = cut.FindAll("input[type=radio]");
        radios[0].Change(radios[0].GetAttribute("value"));

        Assert.Null(model.Reason);
        Assert.Equal(["Priority", "Priority"], notified); // the enum only -- no Reason event
    }
}
