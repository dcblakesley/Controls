using System.Linq.Expressions;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers finding 60: <c>EditTextArea</c>'s <see cref="EditTextArea.AutoSize"/> re-measure gating in
/// <c>OnAfterRenderAsync</c> (see <c>EditTextArea.razor.cs</c>) for the two paths that don't go through
/// <c>@bind-value:after</c>/the extra <c>oninput</c> handler at all: a parent setting
/// the bound value directly (not the user typing), and <see cref="EditTextArea.AutoSize"/> flipping
/// false-to-true at runtime. bUnit can't observe the actual DOM height (JSInterop runs in Loose mode --
/// no real browser, no real <c>autoSizeTextArea</c> JS), so this only proves the C# gating invokes (or
/// skips) the JS call at the right times; <c>EditTextAreaE2ETests</c> covers the real visual growth.
/// Mirrors <see cref="UpdateTriggerCascadeTests"/>' identical <c>AutoSizeCalls()</c> invocation-counting
/// pattern for the pre-existing typing-path coverage.
/// </summary>
public class EditTextAreaAutoSizeMeasureTests : BunitContext
{
    public EditTextAreaAutoSizeMeasureTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    int AutoSizeCalls() => JSInterop.Invocations.Count(i => i.Identifier == "WssEditControls.autoSizeTextArea");

    [Fact]
    public void Parent_driven_value_change_re_measures_even_though_no_input_event_fired()
    {
        var model = new PersonModel { Name = "" };
        var editContext = new EditContext(model);
        Expression<Func<string?>> field = () => model.Name;

        var cut = Render<EditTextArea>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, model.Name)
            .Add(c => c.ValueExpression, field)
            .Add(c => c.AutoSize, true));

        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1)); // first-render measurement
        var baseline = AutoSizeCalls();

        // Simulates a parent assigning the model property directly -- e.g. loading a record -- which
        // bypasses OnValueCommittedAsync/OnEditorInputAsync (those only fire from the textarea's own
        // bound DOM event) entirely.
        cut.Render(ps => ps.Add(c => c.Value, "line one\nline two\nline three\nline four"));

        cut.WaitForAssertion(() => Assert.Equal(baseline + 1, AutoSizeCalls()));
    }

    [Fact]
    public void Re_rendering_with_the_same_value_does_not_measure_again()
    {
        // Guards against over-firing: a re-render for an unrelated reason (same Value, e.g. IsDisabled
        // toggling) must not re-measure -- only an actual value/AutoSize change should.
        var model = new PersonModel { Name = "unchanged" };
        var editContext = new EditContext(model);
        Expression<Func<string?>> field = () => model.Name;

        var cut = Render<EditTextArea>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, model.Name)
            .Add(c => c.ValueExpression, field)
            .Add(c => c.AutoSize, true));

        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1));
        var baseline = AutoSizeCalls();

        cut.Render(ps => ps.Add(c => c.Value, "unchanged").Add(c => c.IsDisabled, true));

        Assert.Equal(baseline, AutoSizeCalls());
    }

    [Fact]
    public void AutoSize_flipped_on_at_runtime_measures_immediately_even_with_pre_existing_content()
    {
        var model = new PersonModel { Name = "line one\nline two\nline three\nline four\nline five" };
        var editContext = new EditContext(model);
        Expression<Func<string?>> field = () => model.Name;

        var cut = Render<EditTextArea>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, model.Name)
            .Add(c => c.AutoSize, false)
            .Add(c => c.ValueExpression, field));

        Assert.Equal(0, AutoSizeCalls()); // AutoSize off from the start -- no measurement at all yet

        cut.Render(ps => ps.Add(c => c.AutoSize, true));

        cut.WaitForAssertion(() => Assert.Equal(1, AutoSizeCalls()));
    }

    [Fact]
    public void AutoSize_flipped_off_then_back_on_measures_again_on_the_second_flip()
    {
        var model = new PersonModel { Name = "hello" };
        var editContext = new EditContext(model);
        Expression<Func<string?>> field = () => model.Name;

        var cut = Render<EditTextArea>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, model.Name)
            .Add(c => c.ValueExpression, field)
            .Add(c => c.AutoSize, true));

        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1));

        cut.Render(ps => ps.Add(c => c.AutoSize, false)); // no new measurement expected while off
        var baseline = AutoSizeCalls();

        cut.Render(ps => ps.Add(c => c.AutoSize, true)); // flipping back on must re-measure, not skip
        cut.WaitForAssertion(() => Assert.Equal(baseline + 1, AutoSizeCalls()));
    }
}
