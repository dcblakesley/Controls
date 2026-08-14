using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Counts <see cref="EditContext.OnValidationStateChanged"/> notifications around the picker-backed
/// controls' parse-error retirement (<c>EditColor</c>, <c>EditDate&lt;T&gt;</c>, <c>EditDateRange</c>).
/// </summary>
/// <remarks>
/// <para>
/// Each of those controls retires its parse-error message from TWO channels — the picker's
/// <c>OnValidCommit</c> (raised on every accepted commit, dedup or not) and, when the value actually
/// changed, <c>ValueChanged</c> — and the clear used to end in
/// <see cref="EditContext.NotifyValidationStateChanged"/> unconditionally. That made an ordinary
/// value-changing commit notify three times where one was needed, and a DEDUPED commit (every drag
/// frame clamped at a track edge, every retyped-the-same-date Enter) notify once where nothing at all
/// had changed. On Blazor Server each notification re-renders every <c>ValidationSummary</c>/
/// <c>ValidationView</c> subscriber over a network round trip, so the count is the behavior.
/// </para>
/// <para>
/// The interesting states are all AFTER a parse error has existed at least once: the clear is guarded
/// on a lazily-created <see cref="ValidationMessageStore"/>, so a control that has never seen a bad
/// entry was already a no-op. Every test below therefore establishes and retires an error first, then
/// resets the counter — otherwise it would pass with the guard deleted.
/// </para>
/// </remarks>
public class ValidationNotifyCountTests : BunitContext
{
    public ValidationNotifyCountTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the JS imports

    // A mutable counter the render helpers can hand back while the EditContext keeps writing to it.
    sealed class Counter
    {
        public int Count;
        public void Reset() => Count = 0;
    }

    class ColorModel
    {
        public string? Brand { get; set; }
    }

    class RangeModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    // A DataAnnotationsValidator is deliberately present: it is what turns a NotifyFieldChanged into a
    // validation-state notification, so the "one notification per value-changing commit" assertions
    // below actually see the legitimate one and would catch a fix that suppressed it too.
    IRenderedComponent<ContainerFragment> RenderWithCounter<TModel>(
        TModel model, Counter counter, Action<RenderTreeBuilder> control)
        where TModel : class
    {
        var editContext = new EditContext(model);
        editContext.OnValidationStateChanged += (_, _) => counter.Count++;
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                control(content);
            }));
            b.CloseComponent();
        });
    }

    // ----- EditColor ---------------------------------------------------------

    static void CommitHex(IRenderedComponent<ContainerFragment> cut, string text) =>
        cut.Find(".wss-color-picker-hex").Change(text);

    IRenderedComponent<ContainerFragment> RenderColor(ColorModel model, Counter counter)
    {
        Expression<Func<string?>> field = () => model.Brand;
        var cut = RenderWithCounter(model, counter, content =>
        {
            content.OpenComponent<EditColor>(1);
            content.AddAttribute(2, "Value", model.Brand);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<string?>(this, v => model.Brand = v));
            content.CloseComponent();
        });
        cut.Find(".wss-color-picker-trigger").Click(); // open the popup so the HEX box exists
        return cut;
    }

    [Fact]
    public void EditColor_commits_notify_validation_state_only_when_something_actually_changed()
    {
        var model = new ColorModel { Brand = "#ff0000" };
        var counter = new Counter();
        var cut = RenderColor(model, counter);

        // Establish the store (the clear is a no-op until one exists), then retire the message.
        CommitHex(cut, "nope");
        CommitHex(cut, "#00ff00");
        Assert.Equal("#00ff00", model.Brand);

        // (a) A value-changing commit with nothing outstanding: exactly the one notification the field
        // change itself earns. Used to be three -- OnValidCommit's clear, ValueChanged's clear, and this.
        counter.Reset();
        CommitHex(cut, "#0000ff");
        Assert.Equal("#0000ff", model.Brand);
        Assert.Equal(1, counter.Count);

        // (b) A DEDUPED commit with nothing outstanding: the picker drops it before ValueChanged, and
        // there is no message to retire, so nobody should hear about it at all. Used to be one.
        counter.Reset();
        CommitHex(cut, "#0000ff");
        Assert.Equal(0, counter.Count);

        // (c) A deduped commit WITH a stale message: exactly one -- the retirement. This is the case
        // OnValidCommit exists for, and it must survive the no-op guard.
        CommitHex(cut, "nope");
        counter.Reset();
        CommitHex(cut, "#0000ff");
        Assert.Equal(1, counter.Count);
        Assert.Equal(string.Empty, cut.Find("#error-msg-Brand").TextContent);
    }

    // ----- EditDate<T> -------------------------------------------------------

    static readonly DateTime Feb14 = new(2026, 2, 14);

    static void CommitDate(IRenderedComponent<ContainerFragment> cut, string text)
    {
        cut.Find(".wss-picker-input-date").Input(text);
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    [Fact]
    public void EditDate_commits_notify_validation_state_only_when_something_actually_changed()
    {
        var model = new PersonModel { BirthDate = Feb14 };
        var counter = new Counter();
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = RenderWithCounter(model, counter, content =>
        {
            content.OpenComponent<EditDate<DateTime?>>(1);
            content.AddAttribute(2, "Value", model.BirthDate);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "Format", "MM/dd/yyyy");
            content.AddAttribute(5, "ValueChanged",
                EventCallback.Factory.Create<DateTime?>(this, v => model.BirthDate = v));
            content.CloseComponent();
        });
        cut.Find(".wss-picker-input").Click(); // open

        CommitDate(cut, "not a date");
        CommitDate(cut, "03/05/2026");
        Assert.Equal(new DateTime(2026, 3, 5), model.BirthDate);

        // (a) value-changing, nothing outstanding.
        counter.Reset();
        CommitDate(cut, "03/06/2026");
        Assert.Equal(new DateTime(2026, 3, 6), model.BirthDate);
        Assert.Equal(1, counter.Count);

        // (b) the same date retyped -- accepted, deduped, nothing to retire.
        counter.Reset();
        CommitDate(cut, "03/06/2026");
        Assert.Equal(0, counter.Count);

        // (c) the same date retyped over a stale parse error -- exactly the retirement.
        CommitDate(cut, "not a date");
        counter.Reset();
        CommitDate(cut, "03/06/2026");
        Assert.Equal(1, counter.Count);
        Assert.Equal(string.Empty, cut.Find("#error-msg-BirthDate").TextContent);
    }

    // ----- EditDateRange -----------------------------------------------------

    static void CommitStart(IRenderedComponent<ContainerFragment> cut, string text)
    {
        cut.Find(".wss-picker-input-start").Input(text);
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    [Fact]
    public void EditDateRange_commits_notify_validation_state_only_when_something_actually_changed()
    {
        // Per endpoint here: the picker's OnValidCommit names which endpoint(s) it assigned, and this
        // control retires exactly those messages. The counting matters more than for the single-value
        // controls, because a commit assigning BOTH endpoints would otherwise notify twice with nothing
        // to retire on either side.
        var model = new RangeModel { Start = new DateTime(2025, 1, 15), End = new DateTime(2025, 2, 3) };
        var counter = new Counter();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = RenderWithCounter(model, counter, content =>
        {
            content.OpenComponent<EditDateRange>(1);
            content.AddAttribute(2, "Start", model.Start);
            content.AddAttribute(3, "StartExpression", startField);
            content.AddAttribute(4, "StartChanged",
                EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
            content.AddAttribute(5, "End", model.End);
            content.AddAttribute(6, "EndExpression", endField);
            content.AddAttribute(7, "EndChanged",
                EventCallback.Factory.Create<DateTime?>(this, v => model.End = v));
            content.AddAttribute(8, "Format", "MM/dd/yyyy");
            content.CloseComponent();
        });
        cut.Find(".wss-picker-input").Click(); // open

        CommitStart(cut, "not a date");
        CommitStart(cut, "01/20/2025");
        Assert.Equal(new DateTime(2025, 1, 20), model.Start);

        // (a) value-changing, nothing outstanding: the Start field change alone.
        counter.Reset();
        CommitStart(cut, "01/21/2025");
        Assert.Equal(new DateTime(2025, 1, 21), model.Start);
        Assert.Equal(1, counter.Count);

        // (b) the same date retyped -- accepted and reported (that is what retires a stale message),
        // but with nothing to retire and no value change, nobody hears about it.
        counter.Reset();
        CommitStart(cut, "01/21/2025");
        Assert.Equal(0, counter.Count);

        // (c) the same date retyped over a stale Start message -- exactly the retirement.
        CommitStart(cut, "not a date");
        counter.Reset();
        CommitStart(cut, "01/21/2025");
        Assert.Equal(1, counter.Count);
        Assert.Equal(string.Empty, cut.Find("#error-msg-Start").TextContent);
    }
}
