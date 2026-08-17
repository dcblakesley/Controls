using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit coverage for <see cref="FormAutoSave"/> — the form-level replacement for a per-field
/// <c>@bind-Value:after</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>No test here sleeps.</b> The debounce runs on an injected <see cref="TimeProvider"/>, and
/// <see cref="ManualTimeProvider"/> below advances it by hand — so "the window closed" is a statement
/// the test makes, not one it waits for. Every <c>Advance</c> is issued through
/// <c>cut.InvokeAsync</c> so the timer callback fires ON the renderer's dispatcher, where
/// <c>InvokeAsync</c> runs inline and a synchronous <c>OnSave</c> has completed by the time
/// <c>Advance</c> returns. (CI runs on <c>windows-latest</c>; a wall-clock debounce test would flake
/// there and nowhere else.)
/// </para>
/// <para>
/// The notification COUNTS these tests lean on — one per keystroke, two per radio click, one per
/// list mutation — are measured separately in <see cref="FieldChangedNotificationTests"/>.
/// </para>
/// </remarks>
public class FormAutoSaveTests : BunitContext
{
    public FormAutoSaveTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // ───────────────────────────── deterministic clock ─────────────────────────────

    /// <summary>
    /// A hand-advanced <see cref="TimeProvider"/>. Deliberately hand-rolled rather than taken from
    /// <c>Microsoft.Extensions.TimeProvider.Testing</c>: this project's bUnit/AngleSharp versions are
    /// pinned, and ~50 lines is a cheaper price than a new package in that graph. Only what
    /// <see cref="FormAutoSave"/> uses is implemented — a one-shot timer, re-armable via
    /// <see cref="ITimer.Change"/>.
    /// </summary>
    sealed class ManualTimeProvider : TimeProvider
    {
        readonly List<ManualTimer> _timers = [];
        DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            _timers.Add(timer);
            timer.Change(dueTime, period);
            return timer;
        }

        /// <summary> Moves the clock forward and fires every timer whose due time has now passed. </summary>
        public void Advance(TimeSpan by)
        {
            _now += by;
            // Snapshot: a callback may re-arm its own timer (the trailing debounce does exactly that).
            foreach (var timer in _timers.ToArray())
                timer.FireIfDue(_now);
        }

        /// <summary> Live (undisposed) timers. Lets a disposal test assert the debounce timer went away
        /// WITHOUT depending on nothing having happened, which a negative assertion alone can't prove. </summary>
        public int TimerCount => _timers.Count;

        /// <summary> Live timers with a due time set. The debounce timer is created once and re-armed,
        /// so DISARMING it (an EditContext swap) leaves <see cref="TimerCount"/> unchanged — this is
        /// what tells "the timer was disarmed" apart from "it fired and found nothing to do". </summary>
        public int ArmedTimerCount => _timers.Count(t => t.IsArmed);

        internal void Forget(ManualTimer timer) => _timers.Remove(timer);

        internal sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state) : ITimer
        {
            DateTimeOffset? _dueAt;

            public bool IsArmed => _dueAt is not null;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                // Only the one-shot form is used; a periodic timer would need a repeat schedule here.
                Assert.Equal(Timeout.InfiniteTimeSpan, period);
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : owner.GetUtcNow() + dueTime;
                return true;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (_dueAt is not { } due || due > now) return;
                _dueAt = null;
                callback(state);
            }

            public void Dispose()
            {
                _dueAt = null;
                owner.Forget(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }
        }
    }

    // ───────────────────────────── render helpers ─────────────────────────────

    // Deliberately annotation-free: most tests here are about WHEN a save fires, and the shared
    // PersonModel's [Required]/[StringLength(MinimumLength = 2)] would silently gate half of them
    // through the validity check instead. TinyModel below is the model for the validity tests.
    class AutoSaveModel
    {
        public string Name { get; set; } = "";
        public int? Age { get; set; }
        public Priority? Priority { get; set; }
    }

    sealed class SaveRecord
    {
        public List<FormAutoSaveEventArgs> Calls { get; } = [];
        public List<FormAutoSaveFailureEventArgs> Failures { get; } = [];
        public List<string[]> FieldNames => [.. Calls.Select(c => c.ChangedFields.Select(f => f.FieldName).ToArray())];
        public List<string[]> FailedFieldNames => [.. Failures.Select(f => f.ChangedFields.Select(x => x.FieldName).ToArray())];
    }

    // <EditForm EditContext> -> DataAnnotationsValidator -> FormAutoSave -> the control(s) under test.
    // The validator is FIRST on purpose: OnFieldChanged handlers run in subscription order, so the
    // SaveWhenInvalid gate only sees a freshly-validated field if the validator subscribed first.
    IRenderedComponent<ContainerFragment> RenderForm(
        EditContext editContext,
        Action<RenderTreeBuilder> fields,
        Action<AutoSaveAttributes> configure)
    {
        var config = new AutoSaveAttributes();
        configure(config);
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<FormAutoSave>(1);
                var seq = 2;
                foreach (var (name, value) in config.Values)
                    content.AddAttribute(seq++, name, value);
                content.CloseComponent();
                fields(content);
            }));
            b.CloseComponent();
        });
    }

    sealed class AutoSaveAttributes
    {
        public List<(string Name, object Value)> Values { get; } = [];
        public AutoSaveAttributes Set(string name, object value) { Values.Add((name, value)); return this; }
    }

    // The standard wiring: a manual clock plus an OnSave that records what it was handed.
    AutoSaveAttributes Standard(SaveRecord record, ManualTimeProvider clock, Func<FormAutoSaveEventArgs, Task>? onSave = null) =>
        new AutoSaveAttributes()
            .Set("TimeProvider", clock)
            .Set("OnSave", EventCallback.Factory.Create<FormAutoSaveEventArgs>(this, args =>
            {
                record.Calls.Add(args);
                return onSave?.Invoke(args) ?? Task.CompletedTask;
            }));

    static void EditStringField(RenderTreeBuilder content, AutoSaveModel model, Expression<Func<string>> field, object receiver)
    {
        content.OpenComponent<EditString>(10);
        content.AddAttribute(11, "Value", model.Name);
        content.AddAttribute(12, "ValueExpression", field);
        content.AddAttribute(13, "ValueChanged",
            EventCallback.Factory.Create<string?>(receiver, v => model.Name = v ?? ""));
        content.CloseComponent();
    }

    static void EditNumberField(RenderTreeBuilder content, AutoSaveModel model, Expression<Func<int?>> field, object receiver)
    {
        content.OpenComponent<EditNumber<int?>>(20);
        content.AddAttribute(21, "Value", model.Age);
        content.AddAttribute(22, "ValueExpression", field);
        content.AddAttribute(23, "ValueChanged",
            EventCallback.Factory.Create<int?>(receiver, v => model.Age = v));
        content.CloseComponent();
    }

    static Task AdvanceAsync<T>(IRenderedComponent<T> cut, ManualTimeProvider clock, int ms = 500)
        where T : IComponent =>
        cut.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(ms)));

    // One dispatcher round trip. Work already queued on the renderer's synchronization context (a
    // completed save's continuation, and the next loop iteration it starts) runs before this does,
    // because the queue is FIFO -- so awaiting it is a deterministic barrier, not a delay.
    static Task DrainAsync<T>(IRenderedComponent<T> cut) where T : IComponent => cut.InvokeAsync(() => { });

    // ───────────────────────────── contract ─────────────────────────────

    [Fact]
    public void Throws_when_rendered_without_a_cascading_EditContext()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Render<FormAutoSave>(ps => ps
            .Add(c => c.OnSave, EventCallback.Factory.Create<FormAutoSaveEventArgs>(this, _ => { }))));
        Assert.Contains(nameof(EditContext), ex.Message);
        Assert.Contains(nameof(EditForm), ex.Message);
    }

    [Fact]
    public void Renders_nothing_at_all_the_forms_markup_is_byte_identical_with_and_without_it()
    {
        // Asserting an empty <form> on a form with no fields would be true by construction. The real
        // claim is that adding this component changes the DOM by NOTHING -- not even a comment/marker
        // node -- so the comparison is against the same form rendered without it.
        var model = new AutoSaveModel { Name = "a", Age = 1 };
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;

        string Markup(bool withAutoSave) => Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", new EditContext(model));
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                if (withAutoSave)
                {
                    content.OpenComponent<FormAutoSave>(1);
                    content.AddAttribute(2, "TimeProvider", clock);
                    content.AddAttribute(3, "OnSave", EventCallback.Factory.Create<FormAutoSaveEventArgs>(
                        this, args => record.Calls.Add(args)));
                    content.CloseComponent();
                }
                EditStringField(content, model, field, this);
            }));
            b.CloseComponent();
        }).Find("form").InnerHtml;

        var withIt = Markup(true);
        Assert.Contains("<input", withIt); // a real field rendered -- the comparison isn't vacuous
        Assert.Equal(Normalize(Markup(false)), Normalize(withIt));
    }

    // bUnit surfaces the renderer's own bookkeeping as blazor: attributes whose values are global
    // counters/GUIDs (handler ids, element references) -- they differ between two renders of the SAME
    // markup, so the comparison above is about the DOM, not about render bookkeeping.
    static string Normalize(string html) =>
        Regex.Replace(html, "(blazor:[a-zA-Z-]+=\")[^\"]*\"", "$1\"");

    // ───────────────────────────── debounce ─────────────────────────────

    [Fact]
    public async Task A_burst_of_keystrokes_collapses_into_one_save()
    {
        // EditString defaults to UpdateTrigger.Input: one OnFieldChanged per character.
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Values));

        var input = cut.Find("input");
        input.Input("a");
        input.Input("ab");
        input.Input("abc");
        Assert.Empty(record.Calls); // nothing yet -- the window is still open

        await AdvanceAsync(cut, clock);

        Assert.Single(record.Calls);
        Assert.Equal(["Name"], record.FieldNames[0]); // de-duplicated to the one field
        Assert.Same(editContext, record.Calls[0].EditContext);
    }

    [Fact]
    public async Task The_debounce_is_trailing_each_keystroke_pushes_the_deadline_out()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Values));

        var input = cut.Find("input");
        input.Input("a");
        await AdvanceAsync(cut, clock, 400); // 400ms of the 500ms window
        Assert.Empty(record.Calls);

        input.Input("ab");                   // re-arms: the deadline moves to 400+500
        await AdvanceAsync(cut, clock, 400); // 800ms total -- still short of 900
        Assert.Empty(record.Calls);

        await AdvanceAsync(cut, clock, 100); // now 900
        Assert.Single(record.Calls);
    }

    [Fact]
    public void DebounceMilliseconds_zero_fires_per_notification()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Set("DebounceMilliseconds", 0).Values));

        var input = cut.Find("input");
        input.Input("a");
        input.Input("ab");
        input.Input("abc");

        Assert.Equal(3, record.Calls.Count); // no clock involved at all on this path
        Assert.All(record.FieldNames, names => Assert.Equal(["Name"], names));
    }

    [Fact]
    public async Task A_radio_groups_double_notification_collapses_into_one_save_with_one_field()
    {
        // EditRadioEnum notifies TWICE per click (see FieldChangedNotificationTests) -- the debounce and
        // the de-duplicated ChangedFields between them make that invisible to the consumer.
        var model = new AutoSaveModel { Name = "a", Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(10);
            content.AddAttribute(11, "Value", model.Priority);
            content.AddAttribute(12, "ValueExpression", field);
            content.AddAttribute(13, "ValueChanged",
                EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            content.CloseComponent();
        }, c => c.Values.AddRange(Standard(record, clock).Values));

        var radios = cut.FindAll("input[type=radio]");
        radios[1].Change(radios[1].GetAttribute("value"));

        await AdvanceAsync(cut, clock);

        Assert.Single(record.Calls);
        Assert.Equal(["Priority"], record.FieldNames[0]);
    }

    [Fact]
    public async Task ChangedFields_are_de_duplicated_in_first_seen_order()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            content.OpenComponent<EditNumber<int?>>(20);
            content.AddAttribute(21, "Value", model.Age);
            content.AddAttribute(22, "ValueExpression", ageField);
            content.AddAttribute(23, "ValueChanged",
                EventCallback.Factory.Create<int?>(this, v => model.Age = v));
            content.CloseComponent();
        }, c => c.Values.AddRange(Standard(record, clock).Values));

        cut.Find("#Age").Change("42"); // Age first...
        cut.Find("#Name").Input("ab");    // ...then Name...
        cut.Find("#Age").Change("43"); // ...then Age again (already seen)

        await AdvanceAsync(cut, clock);

        Assert.Single(record.Calls);
        Assert.Equal(["Age", "Name"], record.FieldNames[0]);
    }

    // ───────────────────────────── validity gate ─────────────────────────────

    class TinyModel
    {
        [Required, StringLength(3)]
        public string Code { get; set; } = "ab";
    }

    IRenderedComponent<ContainerFragment> RenderTiny(TinyModel model, EditContext editContext,
        SaveRecord record, ManualTimeProvider clock, Action<AutoSaveAttributes>? extra = null)
    {
        Expression<Func<string>> field = () => model.Code;
        return RenderForm(editContext, content =>
        {
            content.OpenComponent<EditString>(10);
            content.AddAttribute(11, "Value", model.Code);
            content.AddAttribute(12, "ValueExpression", field);
            content.AddAttribute(13, "ValueChanged",
                EventCallback.Factory.Create<string?>(this, v => model.Code = v ?? ""));
            content.CloseComponent();
        }, c =>
        {
            c.Values.AddRange(Standard(record, clock).Values);
            extra?.Invoke(c);
        });
    }

    [Fact]
    public async Task An_invalid_form_is_not_saved_and_the_pending_fields_survive_until_it_is_valid_again()
    {
        var model = new TinyModel();
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var cut = RenderTiny(model, editContext, record, clock);

        cut.Find("input").Input("toolong"); // StringLength(3) fails
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls);

        cut.Find("input").Input("ok");      // valid again
        await AdvanceAsync(cut, clock);

        // One save, reporting the field that changed while the form was invalid -- nothing was lost.
        // ONE is also the point: becoming valid re-attempts the skipped save through the debounce
        // rather than on the spot, so the keystroke that fixed the field lands in the same save
        // instead of a second one right behind it.
        Assert.Single(record.Calls);
        Assert.Equal(["Code"], record.FieldNames[0]);
        Assert.Equal("ok", model.Code);
    }

    [Fact]
    public async Task SaveWhenInvalid_true_saves_anyway()
    {
        var model = new TinyModel();
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var cut = RenderTiny(model, editContext, record, clock, c => c.Set("SaveWhenInvalid", true));

        cut.Find("input").Input("toolong");
        await AdvanceAsync(cut, clock);

        Assert.Single(record.Calls);
    }

    [Fact]
    public async Task A_parse_failure_does_not_trigger_a_save_of_the_stale_value()
    {
        // EditNumber notifies on an unparseable entry while the model still holds the OLD value
        // (pinned in FieldChangedNotificationTests) -- the validity gate is what keeps that out of a save.
        var model = new AutoSaveModel { Name = "a", Age = 30 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<int?>> field = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditNumber<int?>>(10);
            content.AddAttribute(11, "Value", model.Age);
            content.AddAttribute(12, "ValueExpression", field);
            content.AddAttribute(13, "ValueChanged",
                EventCallback.Factory.Create<int?>(this, v => model.Age = v));
            content.CloseComponent();
        }, c => c.Values.AddRange(Standard(record, clock).Values));

        cut.Find("input").Change("abc");
        await AdvanceAsync(cut, clock);

        Assert.Empty(record.Calls);
        Assert.Equal(30, model.Age);
    }

    // ─────────────────────── validity gate: a skip is never terminal ───────────────────────

    [Fact]
    public async Task A_skipped_save_is_re_attempted_when_the_messages_are_cleared_from_OUTSIDE_the_form()
    {
        // The jam this closes: the gate skips, and nothing re-attempts it until the user changes
        // another FIELD. A server-side ValidationMessageStore clear + NotifyValidationStateChanged --
        // which this library explicitly supports (RequiredResolver / FluentValidation) -- changes no
        // field, so the pending work sat there forever. Worse, the message may belong to a property the
        // user cannot currently fill, in which case auto-save is dead for the whole form.
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var store = new ValidationMessageStore(editContext);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Values));

        await cut.InvokeAsync(() =>
        {
            store.Add(editContext.Field(nameof(AutoSaveModel.Age)), "the server rejected this");
            editContext.NotifyValidationStateChanged();
        });

        cut.Find("input").Input("a");
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls); // gated -- Name is pending and unsaved

        await cut.InvokeAsync(() =>
        {
            store.Clear();
            editContext.NotifyValidationStateChanged();
        });
        await AdvanceAsync(cut, clock);

        // EXACTLY once, carrying what was pending.
        Assert.Single(record.Calls);
        Assert.Equal(["Name"], record.FieldNames[0]);

        // And it does not fire again: the re-attempt drained the pending set like any other save.
        await cut.InvokeAsync(editContext.NotifyValidationStateChanged);
        await AdvanceAsync(cut, clock);
        Assert.Single(record.Calls);
    }

    [Fact]
    public async Task A_validation_state_change_does_not_short_circuit_an_open_debounce_window()
    {
        // The re-attempt is scoped to a save the gate actually SKIPPED. Validation state changes
        // constantly while typing (the validator raises one per keystroke), and reacting to those would
        // turn the debounce into "save on the first keystroke".
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Values));

        cut.Find("input").Input("a");
        await cut.InvokeAsync(editContext.NotifyValidationStateChanged);
        Assert.Empty(record.Calls); // still mid-window

        await AdvanceAsync(cut, clock);
        Assert.Single(record.Calls); // one save, when the window closed -- not two
    }

    // ───────────────────────────── ShouldSave filter ─────────────────────────────

    [Fact]
    public async Task ShouldSave_false_ignores_the_field_entirely_it_neither_arms_nor_joins_the_batch()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            content.OpenComponent<EditNumber<int?>>(20);
            content.AddAttribute(21, "Value", model.Age);
            content.AddAttribute(22, "ValueExpression", ageField);
            content.AddAttribute(23, "ValueChanged",
                EventCallback.Factory.Create<int?>(this, v => model.Age = v));
            content.CloseComponent();
        }, c => c.Values.AddRange(Standard(record, clock)
            .Set("ShouldSave", (Func<FieldIdentifier, bool>)(f => f.FieldName != nameof(AutoSaveModel.Name)))
            .Values));

        // The filtered field alone: no timer is armed, so no save ever happens.
        cut.Find("#Name").Input("abc");
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls);

        // An accepted field alongside it: one save, and the filtered field is absent from the batch.
        cut.Find("#Age").Change("42");
        cut.Find("#Name").Input("abcd");
        await AdvanceAsync(cut, clock);

        Assert.Single(record.Calls);
        Assert.Equal(["Age"], record.FieldNames[0]);
    }

    // ───────────────────────────── concurrency ─────────────────────────────

    [Fact]
    public async Task CoalesceTrailing_queues_at_most_one_further_run_however_many_changes_arrive()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var gates = new List<TaskCompletionSource>();
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            content.OpenComponent<EditNumber<int?>>(20);
            content.AddAttribute(21, "Value", model.Age);
            content.AddAttribute(22, "ValueExpression", ageField);
            content.AddAttribute(23, "ValueChanged",
                EventCallback.Factory.Create<int?>(this, v => model.Age = v));
            content.CloseComponent();
        }, c => c.Values.AddRange(Standard(record, clock, _ =>
        {
            var gate = new TaskCompletionSource();
            gates.Add(gate);
            return gate.Task;
        }).Values));

        cut.Find("#Name").Input("a");
        await AdvanceAsync(cut, clock);
        Assert.Single(record.Calls); // save #1 is in flight, blocked on gates[0]

        // Three separate debounce windows close while it is still running...
        cut.Find("#Age").Change("42");
        await AdvanceAsync(cut, clock);
        cut.Find("#Name").Input("ab");
        await AdvanceAsync(cut, clock);
        cut.Find("#Name").Input("abc");
        await AdvanceAsync(cut, clock);
        Assert.Single(record.Calls); // ...and none of them starts a second save

        gates[0].SetResult();
        await DrainAsync(cut);

        // Exactly ONE trailing run, carrying everything that accumulated, in first-seen order.
        Assert.Equal(2, record.Calls.Count);
        Assert.Equal(["Age", "Name"], record.FieldNames[1]);
        Assert.Equal(2, gates.Count);
    }

    [Fact]
    public async Task Concurrent_starts_a_second_save_while_the_first_is_still_running()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var gates = new List<TaskCompletionSource>();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock, _ =>
                {
                    var gate = new TaskCompletionSource();
                    gates.Add(gate);
                    return gate.Task;
                })
                .Set("Concurrency", AutoSaveConcurrency.Concurrent).Values));

        cut.Find("input").Input("a");
        await AdvanceAsync(cut, clock);
        cut.Find("input").Input("ab");
        await AdvanceAsync(cut, clock);

        Assert.Equal(2, record.Calls.Count); // both in flight at once
        Assert.All(gates, g => Assert.False(g.Task.IsCompleted));

        gates[0].SetResult();
        gates[1].SetResult();
        await DrainAsync(cut);
        Assert.Equal(2, record.Calls.Count); // and no trailing run is manufactured
    }

    // ───────────────────────────── failure handling ─────────────────────────────

    [Fact]
    public async Task OnSaveFailed_receives_the_exception_and_the_fields_and_saving_continues_afterwards()
    {
        // TWO DIFFERENT fields on purpose. With one field for both saves this test cannot tell "the
        // failed save's fields were carried forward" from "they were dropped and the second save simply
        // reported its own field" -- which is exactly how a failed save silently losing everything it
        // was carrying went unnoticed.
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var shouldThrow = true;
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            EditNumberField(content, model, ageField, this);
        }, c => c.Values.AddRange(Standard(record, clock, _ =>
                shouldThrow ? throw new InvalidOperationException("save endpoint down") : Task.CompletedTask)
            .Set("OnSaveFailed", EventCallback.Factory.Create<FormAutoSaveFailureEventArgs>(
                this, args => record.Failures.Add(args)))
            .Values));

        cut.Find("#Age").Change("42");
        await AdvanceAsync(cut, clock);

        var failure = Assert.Single(record.Failures);
        Assert.Equal("save endpoint down", failure.Exception.Message);
        Assert.Same(editContext, failure.EditContext);
        // The consumer is told WHICH fields the failed save was carrying, so it can compensate.
        Assert.Equal(["Age"], record.FailedFieldNames[0]);

        // The component is not stuck: a later change still saves (the in-flight flag was released)...
        shouldThrow = false;
        cut.Find("#Name").Input("ab");
        await AdvanceAsync(cut, clock);

        Assert.Equal(2, record.Calls.Count);
        Assert.Single(record.Failures);
        // ...and it carries the failed save's field along, FIRST, because it changed first. Without
        // that, a consumer PATCHing by ChangedFields never persists Age at all.
        Assert.Equal(["Age", "Name"], record.FieldNames[1]);
    }

    [Fact]
    public async Task A_permanently_failing_save_keeps_re_attempting_its_fields_on_every_later_flush()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            EditNumberField(content, model, ageField, this);
        }, c => c.Values.AddRange(Standard(record, clock, _ => throw new InvalidOperationException("still down"))
            .Set("OnSaveFailed", EventCallback.Factory.Create<FormAutoSaveFailureEventArgs>(
                this, args => record.Failures.Add(args)))
            .Values));

        cut.Find("#Age").Change("42");
        await AdvanceAsync(cut, clock);
        cut.Find("#Name").Input("ab");
        await AdvanceAsync(cut, clock);

        // Nothing accumulates twice and nothing falls out: each attempt carries everything still unsaved.
        Assert.Equal(2, record.Calls.Count);
        Assert.Equal(["Age"], record.FieldNames[0]);
        Assert.Equal(["Age", "Name"], record.FieldNames[1]);
        Assert.Equal(2, record.Failures.Count);
        Assert.Equal(["Age", "Name"], record.FailedFieldNames[1]);
    }

    [Fact]
    public async Task A_failing_TRAILING_run_keeps_its_fields_too()
    {
        // The coalesced trailing run is a second save inside the same flush loop -- it has to restore
        // its fields on failure exactly as the leading one does.
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        var gate = new TaskCompletionSource();
        Expression<Func<string>> nameField = () => model.Name;
        Expression<Func<int?>> ageField = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            EditStringField(content, model, nameField, this);
            EditNumberField(content, model, ageField, this);
        }, c => c.Values.AddRange(Standard(record, clock, _ => record.Calls.Count switch
            {
                1 => gate.Task,                                                  // the leading save blocks
                2 => throw new InvalidOperationException("trailing failed"),     // the trailing one fails
                _ => Task.CompletedTask
            })
            .Set("OnSaveFailed", EventCallback.Factory.Create<FormAutoSaveFailureEventArgs>(
                this, args => record.Failures.Add(args)))
            .Values));

        cut.Find("#Name").Input("a");
        await AdvanceAsync(cut, clock);
        Assert.Single(record.Calls); // in flight

        cut.Find("#Age").Change("42"); // queues the trailing run
        await AdvanceAsync(cut, clock);

        gate.SetResult();
        await DrainAsync(cut);

        Assert.Equal(2, record.Calls.Count);
        Assert.Equal(["Age"], record.FieldNames[1]);
        Assert.Equal(["Age"], record.FailedFieldNames[0]);

        // Age is still pending, so the next flush retries it alongside whatever changed since.
        cut.Find("#Name").Input("ab");
        await AdvanceAsync(cut, clock);
        Assert.Equal(3, record.Calls.Count);
        Assert.Equal(["Age", "Name"], record.FieldNames[2]);
    }

    sealed class NullErrorBoundaryLogger : IErrorBoundaryLogger
    {
        public ValueTask LogErrorAsync(Exception exception) => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Without_OnSaveFailed_the_exception_propagates_to_an_ErrorBoundary()
    {
        // The documented alternative to swallowing: DispatchExceptionAsync re-raises it as if it came
        // from a lifecycle method, so an ErrorBoundary catches it (and an unguarded app fails loudly)
        // instead of it dying unobserved inside the discarded debounce task.
        Services.AddSingleton<IErrorBoundaryLogger>(new NullErrorBoundaryLogger());
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;

        var cut = Render(b =>
        {
            b.OpenComponent<ErrorBoundary>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(eb =>
            {
                eb.OpenComponent<EditForm>(0);
                eb.AddAttribute(1, "EditContext", editContext);
                eb.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
                {
                    content.OpenComponent<FormAutoSave>(0);
                    content.AddAttribute(1, "TimeProvider", clock);
                    content.AddAttribute(2, "OnSave", EventCallback.Factory.Create<FormAutoSaveEventArgs>(
                        this, args => { record.Calls.Add(args); throw new InvalidOperationException("boom"); }));
                    content.CloseComponent();
                    EditStringField(content, model, field, this);
                }));
                eb.CloseComponent();
            }));
            b.AddAttribute(2, "ErrorContent", (RenderFragment<Exception>)(ex => eb =>
            {
                eb.OpenElement(0, "p");
                eb.AddAttribute(1, "class", "boundary");
                eb.AddContent(2, ex.Message);
                eb.CloseElement();
            }));
            b.CloseComponent();
        });

        cut.Find("input").Input("a");
        await cut.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(500)));
        await cut.InvokeAsync(() => { });

        Assert.Single(record.Calls);
        Assert.Equal("boom", cut.Find("p.boundary").TextContent);
    }

    [Fact]
    public async Task A_throwing_OnSaveFailed_handler_is_itself_surfaced_not_swallowed()
    {
        // "Failures are never silently swallowed" has to hold for the failure HANDLER too: unguarded,
        // its exception escaped into the fire-and-forget flush task and nothing anywhere saw it.
        Services.AddSingleton<IErrorBoundaryLogger>(new NullErrorBoundaryLogger());
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;

        var cut = Render(b =>
        {
            b.OpenComponent<ErrorBoundary>(0);
            b.AddAttribute(1, "ChildContent", (RenderFragment)(eb =>
            {
                eb.OpenComponent<EditForm>(0);
                eb.AddAttribute(1, "EditContext", editContext);
                eb.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
                {
                    content.OpenComponent<FormAutoSave>(0);
                    content.AddAttribute(1, "TimeProvider", clock);
                    content.AddAttribute(2, "OnSave", EventCallback.Factory.Create<FormAutoSaveEventArgs>(
                        this, args => { record.Calls.Add(args); throw new InvalidOperationException("save endpoint down"); }));
                    content.AddAttribute(3, "OnSaveFailed", EventCallback.Factory.Create<FormAutoSaveFailureEventArgs>(
                        this, _ => throw new NotSupportedException("handler boom")));
                    content.CloseComponent();
                    EditStringField(content, model, field, this);
                }));
                eb.CloseComponent();
            }));
            b.AddAttribute(2, "ErrorContent", (RenderFragment<Exception>)(ex => eb =>
            {
                eb.OpenElement(0, "p");
                eb.AddAttribute(1, "class", "boundary");
                eb.AddContent(2, ex.Message);
                eb.CloseElement();
            }));
            b.CloseComponent();
        });

        cut.Find("input").Input("a");
        await cut.InvokeAsync(() => clock.Advance(TimeSpan.FromMilliseconds(500)));
        await cut.InvokeAsync(() => { });

        // BOTH reach the boundary: the handler's own failure, and the save failure it was handling
        // (which nobody else would ever see, since the one consumer channel for it just threw).
        var text = cut.Find("p.boundary").TextContent;
        Assert.Contains("handler boom", text);
        Assert.Contains("save endpoint down", text);
    }

    // ───────────────────────────── lifetime ─────────────────────────────

    [Fact]
    public async Task Disposal_detaches_the_subscription_and_cancels_an_armed_debounce()
    {
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Values.AddRange(Standard(record, clock).Values));

        cut.Find("#Name").Input("a");
        Assert.Equal(1, clock.TimerCount); // the debounce is armed and mid-window

        await DisposeComponentsAsync();

        // Positive evidence rather than "nothing happened": the timer is gone, so there is nothing
        // left that COULD fire, and advancing past the deadline is a formality.
        Assert.Equal(0, clock.TimerCount);
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls);

        // The subscription is detached too -- the EditContext outlives the component, so a later
        // notification must not re-arm anything (which is what an un-detached handler would do).
        editContext.NotifyFieldChanged(editContext.Field(nameof(AutoSaveModel.Name)));
        Assert.Equal(0, clock.TimerCount);
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls);
    }

    [Fact]
    public async Task An_EditContext_swap_re_points_the_subscription_and_drops_the_old_models_pending_fields()
    {
        // Driven through the EditContexts directly rather than through a control: InputBase refuses to
        // change EditContext after init ("does not support changing the EditContext dynamically"), so a
        // bound control can't be under the cascade being swapped. The unit under test is the
        // re-pointing itself.
        var first = new AutoSaveModel { Age = 1 };
        var second = new AutoSaveModel { Age = 2 };
        var contexts = new[] { new EditContext(first), new EditContext(second) };
        var record = new SaveRecord();
        var clock = new ManualTimeProvider();

        // Held in a variable and re-supplied on the second render: bUnit's Render(builder) sets a FRESH
        // parameter collection, so omitting ChildContent would unmount the component instead of
        // re-pointing it -- and the test would then pass for the wrong reason.
        var child = (RenderFragment)(content =>
        {
            content.OpenComponent<FormAutoSave>(0);
            content.AddAttribute(1, "TimeProvider", clock);
            content.AddAttribute(2, "OnSave", EventCallback.Factory.Create<FormAutoSaveEventArgs>(
                this, args => record.Calls.Add(args)));
            content.CloseComponent();
        });
        var cut = Render<CascadingValue<EditContext>>(ps => ps
            .Add(c => c.Value, contexts[0])
            .Add(c => c.ChildContent, child));

        contexts[0].NotifyFieldChanged(contexts[0].Field(nameof(AutoSaveModel.Name))); // pending on the FIRST model
        Assert.Equal(1, clock.ArmedTimerCount);

        // Swap the context mid-window.
        cut.Render(ps => ps.Add(c => c.Value, contexts[1]).Add(c => c.ChildContent, child));
        Assert.Single(cut.FindComponents<FormAutoSave>()); // same instance, re-pointed -- not remounted

        // BOTH halves, asserted separately -- "no save happened" alone would pass if only one of them
        // worked. The armed debounce is disarmed (the timer object survives; its due time doesn't)...
        Assert.Equal(0, clock.ArmedTimerCount);
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls); // ...and the old model's pending FieldIdentifier was dropped, not saved

        // The old context is detached...
        contexts[0].NotifyFieldChanged(contexts[0].Field(nameof(AutoSaveModel.Name)));
        await AdvanceAsync(cut, clock);
        Assert.Empty(record.Calls);

        // ...and the new one is live.
        contexts[1].NotifyFieldChanged(contexts[1].Field(nameof(AutoSaveModel.Age)));
        await AdvanceAsync(cut, clock);
        var call = Assert.Single(record.Calls);
        Assert.Same(contexts[1], call.EditContext);
        // ["Age"] and not ["Name", "Age"]: the positive proof that the outgoing model's pending field
        // was CLEARED, rather than merely never having been flushed.
        Assert.Equal(["Age"], record.FieldNames[0]);
    }

    [Fact]
    public async Task With_no_OnSave_wired_nothing_is_armed_and_nothing_throws()
    {
        // EditorRequired makes this a build-time diagnostic in markup; at runtime it must simply be inert.
        var model = new AutoSaveModel { Age = 1 };
        var editContext = new EditContext(model);
        var clock = new ManualTimeProvider();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content => EditStringField(content, model, field, this),
            c => c.Set("TimeProvider", clock));

        cut.Find("input").Input("a");
        await AdvanceAsync(cut, clock);
        Assert.Equal("a", model.Name);
    }
}
