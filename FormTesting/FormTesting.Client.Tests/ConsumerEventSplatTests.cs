using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Proves that a consumer's own DOM event handlers — written as <c>&lt;EditString @onkeydown="H" /&gt;</c>,
/// which the Razor compiler lowers to an unmatched <c>onkeydown</c> attribute holding an
/// <see cref="EventCallback{TValue}"/> — actually REACH the control's inner editor element and RUN,
/// rather than merely being captured into <c>AdditionalAttributes</c> and dropped.
/// </summary>
/// <remarks>
/// <para>
/// This is a behavioral probe, not a markup assertion: every test below dispatches the real event on
/// the element the control rendered and asserts the consumer's delegate ran. Asserting that an
/// attribute merely appears in the markup would pass even if the value never became an event-handler
/// frame.
/// </para>
/// <para>
/// The mechanism under test is <c>AttributeSplat.Rest</c>/<c>RestWith</c> placing the captured
/// attributes on the field element (see <see cref="EditControlBase{TValue}"/>'s unmatched-attribute
/// remarks). The last test additionally covers the one case where the library injects handlers of the
/// same names — <see cref="HidingMode.WhenNull"/>'s focus tracking, which <em>chains</em> a consumer's
/// <c>onfocus</c>/<c>onblur</c> instead of clobbering them.
/// </para>
/// </remarks>
public class ConsumerEventSplatTests : BunitContext
{
    class ProbeModel
    {
        public string? Text { get; set; } = "Alice";
        public int Count { get; set; }
        public bool Flag { get; set; }
    }

    // The two consumer handlers under test, shaped exactly as the Razor compiler emits them for
    // `@onkeydown="..."` / `@onblur="..."` on a component: an unmatched attribute whose value is a
    // typed EventCallback.
    void AddConsumerHandlers(RenderTreeBuilder b, int seq, Action onKeyDown, Action onBlur)
    {
        b.AddAttribute(seq, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, onKeyDown));
        b.AddAttribute(seq + 1, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, onBlur));
    }

    // ───────────────────────────── EditString ─────────────────────────────

    [Fact]
    public void EditString_runs_a_splatted_consumer_onkeydown_and_onblur_on_its_inner_input()
    {
        var model = new ProbeModel();
        Expression<Func<string?>> field = () => model.Text;
        var keyDowns = 0;
        var blurs = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            AddConsumerHandlers(b, 3, () => keyDowns++, () => blurs++);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        input.KeyDown(new KeyboardEventArgs { Key = "Enter" });
        input.Blur();

        Assert.Equal(1, keyDowns);
        Assert.Equal(1, blurs);
    }

    // ───────────────────────────── EditNumber<int> ─────────────────────────────

    [Fact]
    public void EditNumber_runs_a_splatted_consumer_onkeydown_and_onblur_on_its_inner_input()
    {
        var model = new ProbeModel();
        Expression<Func<int>> field = () => model.Count;
        var keyDowns = 0;
        var blurs = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int>>(0);
            b.AddAttribute(1, "Value", model.Count);
            b.AddAttribute(2, "ValueExpression", field);
            AddConsumerHandlers(b, 3, () => keyDowns++, () => blurs++);
            b.CloseComponent();
        }));

        var input = cut.Find("input[type=number]");
        input.KeyDown(new KeyboardEventArgs { Key = "ArrowUp" });
        input.Blur();

        Assert.Equal(1, keyDowns);
        Assert.Equal(1, blurs);
    }

    // ───────────────────────────── EditBool ─────────────────────────────

    [Fact]
    public void EditBool_runs_a_splatted_consumer_onkeydown_and_onblur_on_its_checkbox()
    {
        var model = new ProbeModel();
        Expression<Func<bool>> field = () => model.Flag;
        var keyDowns = 0;
        var blurs = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.Flag);
            b.AddAttribute(2, "ValueExpression", field);
            AddConsumerHandlers(b, 3, () => keyDowns++, () => blurs++);
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        checkbox.KeyDown(new KeyboardEventArgs { Key = " " });
        checkbox.Blur();

        Assert.Equal(1, keyDowns);
        Assert.Equal(1, blurs);
    }

    // ────────────── HidingMode.WhenNull: library tracking CHAINS the consumer's handlers ──────────────

    [Fact]
    public void HidingMode_WhenNull_runs_both_the_librarys_focus_tracking_and_the_consumers_onfocus_onblur()
    {
        // The one case where the library owns attributes of the same names: EditControlBase's
        // WithFocusTracking injects its own onfocus/onblur so a value-driven hide can be deferred while
        // the editor holds focus. It captures a same-named consumer handler and re-invokes it rather
        // than overwriting it, so BOTH have to be observable here -- the consumer's counters, and the
        // deferred-then-applied hide that only the library's own tracking can produce.
        var model = new ProbeModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var focuses = 0;
        var blurs = 0;

        // EditForm rendered directly (not through the WithForm fragment) so cut.Render() below re-runs
        // ChildContent from the top and pushes the mutated model value down as a real parameter change
        // -- see EditStringRevealStateTests' remarks for why the fragment form doesn't.
        var cut = Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditString>(0);
                content.AddAttribute(1, "Value", model.Text);
                content.AddAttribute(2, "ValueExpression", field);
                content.AddAttribute(3, "Hiding", HidingMode.WhenNull);
                content.AddAttribute(4, "onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, () => focuses++));
                content.AddAttribute(5, "onblur", EventCallback.Factory.Create<FocusEventArgs>(this, () => blurs++));
                content.CloseComponent();
            })));

        cut.Find("input.edit-string-input").Focus();
        Assert.Equal(1, focuses); // the consumer's own onfocus ran...

        model.Text = null;
        cut.Render(); // ...and the library's tracking noticed the focus: the null must NOT hide it yet.
        Assert.NotEmpty(cut.FindAll(".edit-control-wrapper"));

        cut.Find("input.edit-string-input").Blur();
        Assert.Equal(1, blurs);                             // the consumer's own onblur ran...
        Assert.Empty(cut.FindAll(".edit-control-wrapper")); // ...and the library's tracking then applied the hide.
    }

    [Fact]
    public void HidingMode_WhenNull_does_not_re_capture_its_own_injected_handlers_across_parameter_cycles()
    {
        // The reference-equality guard in WithFocusTracking: a parameter cycle that does NOT re-supply
        // the consumer's handlers must not end up treating the library's own injected callbacks as the
        // consumer's (which would recurse) or dropping the consumer's (which would silently regress
        // their handler). Re-rendering repeatedly here keeps the consumer's counters honest.
        var model = new ProbeModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var focuses = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.AddAttribute(4, "onfocus", EventCallback.Factory.Create<FocusEventArgs>(this, () => focuses++));
            b.CloseComponent();
        }));

        cut.Find("input.edit-string-input").Focus();
        cut.Find("input.edit-string-input").Focus();

        Assert.Equal(2, focuses); // exactly once per dispatch -- no recursion, no swallowing
    }
}
