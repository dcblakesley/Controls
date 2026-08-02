using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Two defects in the shared text-editor surface (<c>EditTextInputBase</c>), both about the affix
/// chrome disagreeing with what the user can see in the box.
/// <list type="bullet">
/// <item><description><c>Clear()</c> assigned null while deleting the text by hand produces <c>""</c> —
/// two gestures with the same meaning writing different model values, and under
/// <see cref="HidingMode.WhenNull"/> the null answer unmounted the whole control the instant its own
/// clear button was clicked.</description></item>
/// <item><description>The counter and the clear button read <c>CurrentValue</c>, which under
/// <see cref="UpdateTrigger.Change"/> (per-control or cascaded) only moves on blur — so the count
/// froze for the whole time the user was typing and the clear button appeared a gesture late.</description></item>
/// </list>
/// </summary>
public class EditTextClearAndLiveTextTests : BunitContext
{
    public EditTextClearAndLiveTextTests() => JSInterop.Mode = JSRuntimeMode.Loose; // Clear()'s FocusAsync + AutoSize's JS call

    class UnconstrainedModel
    {
        public string? Text { get; set; }
    }

    IRenderedComponent<EditForm> RenderForm(object model, Action<RenderTreeBuilder> inner) =>
        Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => content => inner(content))));

    int AutoSizeCalls() => JSInterop.Invocations.Count(i => i.Identifier == "WssEditControls.autoSizeTextArea");

    // ───────────────────────────── Clear() writes "", not null ─────────────────────────────────

    [Theory]
    [InlineData(true)]   // EditString
    [InlineData(false)]  // EditTextArea
    public void Clear_writes_the_empty_string_so_a_WhenNull_hidden_control_stays_on_screen(bool isEditString)
    {
        // HidingMode.WhenNull hides in edit mode too, and it keys off null specifically. Clearing to
        // null therefore unmounted the control -- including the editor the user would have typed a
        // replacement into -- with no way back short of the parent re-assigning a value.
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            if (isEditString) b.OpenComponent<EditString>(0);
            else b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => model.Text = v));
            b.AddAttribute(5, "AllowClear", true);
            b.AddAttribute(6, "Hiding", HidingMode.WhenNull);
            b.CloseComponent();
        });

        var editorSelector = isEditString ? "input.edit-string-input" : "textarea.edit-textarea-input";
        Assert.NotNull(cut.Find(editorSelector));

        cut.Find(".edit-input-clear").Click();

        Assert.Equal(string.Empty, model.Text);
        Assert.NotEmpty(cut.FindAll(".edit-control-wrapper"));
        Assert.NotNull(cut.Find(editorSelector));                 // still typable
        Assert.Empty(cut.FindAll(".edit-input-clear"));           // and the button withdrew, as before
    }

    [Fact]
    public void Clear_matches_the_value_manual_deletion_produces()
    {
        // The point of the change: both "there is no text here" gestures land the same model value.
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => model.Text = v));
            b.AddAttribute(5, "AllowClear", true);
            b.CloseComponent();
        });

        cut.Find("input.edit-string-input").Input("");
        var byDeletion = model.Text;

        model.Text = "Alice";
        cut.Render();
        cut.Find(".edit-input-clear").Click();

        Assert.Equal(byDeletion, model.Text);
        Assert.Equal(string.Empty, model.Text);
    }

    // ─────────────────── live count/clear under a commit-on-blur binding ───────────────────────

    [Fact]
    public void EditString_count_follows_typing_under_UpdateOn_Change_while_the_model_waits_for_blur()
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        string? captured = model.Text;
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "ShowCount", true);
            b.AddAttribute(6, "MaxLength", 20);
            b.AddAttribute(7, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        });

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("5 / 20", cut.Find(".edit-input-count").TextContent);

        input.Input("Alicia");
        Assert.Equal("6 / 20", cut.Find(".edit-input-count").TextContent);  // the counter moved...
        Assert.Equal("Alice", captured);                                     // ...the bound value did not

        input.Input("Alicia Smith");
        Assert.Equal("12 / 20", cut.Find(".edit-input-count").TextContent);
        Assert.Equal("Alice", captured);

        // Blur still commits, exactly as UpdateTrigger.Change promises -- the extra handler is
        // display-only and doesn't disturb the bound onchange.
        input.Change("Alicia Smith");
        Assert.Equal("Alicia Smith", captured);
        Assert.Equal("12 / 20", cut.Find(".edit-input-count").TextContent);
    }

    [Fact]
    public void EditString_clear_button_appears_and_withdraws_on_input_under_UpdateOn_Change()
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        string? captured = model.Text;
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "AllowClear", true);
            b.AddAttribute(6, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        });

        var input = cut.Find("input.edit-string-input");
        Assert.Single(cut.FindAll(".edit-input-clear"));

        input.Input("");                                  // deleted every character, not yet committed
        Assert.Empty(cut.FindAll(".edit-input-clear"));
        Assert.Equal("Alice", captured);

        input.Input("A");
        Assert.Single(cut.FindAll(".edit-input-clear"));
        Assert.Equal("Alice", captured);
    }

    [Fact]
    public void EditTextArea_count_and_clear_follow_typing_under_a_cascaded_Change_default()
    {
        // Same defect via the other resolution path -- a cascaded FormDefaults.UpdateOn rather than the
        // control's own parameter -- on the other string editor.
        var model = new UnconstrainedModel { Text = "hello" };
        string? captured = model.Text;
        Expression<Func<string?>> field = () => model.Text;
        var cut = Render<FormDefaults>(ps => ps
            .Add(d => d.UpdateOn, UpdateTrigger.Change)
            .Add(d => d.ChildContent, (RenderFragment)(defaults =>
            {
                defaults.OpenComponent<EditForm>(0);
                defaults.AddAttribute(1, "Model", model);
                defaults.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => b =>
                {
                    b.OpenComponent<EditTextArea>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", field);
                    b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
                    b.AddAttribute(5, "ShowCount", true);
                    b.AddAttribute(6, "AllowClear", true);
                    b.CloseComponent();
                }));
                defaults.CloseComponent();
            })));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Equal("5", cut.Find(".edit-textarea-count").TextContent);

        textarea.Input("hello world");
        Assert.Equal("11", cut.Find(".edit-textarea-count").TextContent);
        Assert.Single(cut.FindAll(".edit-input-clear"));
        Assert.Equal("hello", captured);

        textarea.Input("");
        Assert.Equal("0", cut.Find(".edit-textarea-count").TextContent);
        Assert.Empty(cut.FindAll(".edit-input-clear"));
        Assert.Equal("hello", captured);

        textarea.Change("hello world");
        Assert.Equal("hello world", captured);
        Assert.Equal("11", cut.Find(".edit-textarea-count").TextContent);
    }

    [Fact]
    public void EditTextArea_AutoSize_and_the_live_count_share_the_one_oninput_handler()
    {
        // An element can only carry one oninput. With both features asking for one under a
        // commit-on-blur binding, whichever splatted second would have replaced the first.
        var model = new UnconstrainedModel { Text = "hello" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.AddAttribute(5, "AutoSize", true);
            b.AddAttribute(6, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        });

        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1)); // first-render measurement
        var baseline = AutoSizeCalls();

        cut.Find("textarea.edit-textarea-input").Input("hello world");

        Assert.Equal("11", cut.Find(".edit-textarea-count").TextContent);                 // counter moved
        cut.WaitForAssertion(() => Assert.Equal(baseline + 1, AutoSizeCalls()));          // and so did the measure
    }

    [Fact]
    public void Live_text_resyncs_when_the_bound_value_is_set_from_outside()
    {
        // Uncommitted keystrokes are stale the moment a parent assigns a different value (a record
        // load, a reset button) -- the chrome must go back to describing what is now in the box.
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(5, "ShowCount", true);
            b.AddAttribute(7, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        });

        cut.Find("input.edit-string-input").Input("Alicia Smith");
        Assert.Equal("12", cut.Find(".edit-input-count").TextContent);

        model.Text = "Bob";
        cut.Render();

        Assert.Equal("3", cut.Find(".edit-input-count").TextContent);
    }

    [Fact]
    public void The_extra_input_handler_attaches_only_for_the_features_that_need_it()
    {
        // The affix-free DOM must stay byte-identical: with neither ShowCount nor AllowClear in use,
        // a commit-on-blur EditString has no oninput handler at all (bUnit throws rather than
        // silently no-op'ing when an event has no handler -- see UpdateTriggerTests' remarks).
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$")));  // affix mode, but no live-text feature
            b.AddAttribute(5, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        });

        Assert.Throws<Bunit.MissingEventHandlerException>(() => cut.Find("input.edit-string-input").Input("Alicia"));
    }
}
