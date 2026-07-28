using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers <see cref="FormDefaults.UpdateOn"/>'s cascade/nesting/precedence resolution (see
/// <see cref="EditControlBase{TValue}.ResolveUpdateEvent"/>) — NOT the plain per-control
/// <c>UpdateOn</c> parameter in isolation, which is covered elsewhere. Mirrors
/// <see cref="FormDefaultsTests"/>'s nested-<see cref="FormDefaults"/> render helpers, generalized
/// into a chain so the same helper serves the no-FormDefaults, single-FormDefaults, and
/// two-level-nesting cases. "Not wired to this DOM event" is asserted via bUnit's
/// <see cref="Bunit.MissingEventHandlerException"/>, which it throws (rather than no-op'ing) when an
/// event is triggered with no matching handler registered on the element.
/// </summary>
public class UpdateTriggerCascadeTests : BunitContext
{
    // AutoSize's JS resize call is exercised (not just rendered) by the EditTextArea tests below.
    public UpdateTriggerCascadeTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // Wraps `innermost` in zero, one, or two levels of <FormDefaults UpdateOn="..."> -- outermost
    // entry first, matching the MFE composition shape (host page defaults, then an inner root's own
    // overrides). An empty chain renders no FormDefaults component at all, distinct from a single
    // FormDefaults instance whose UpdateOn is left null.
    static RenderFragment WrapFormDefaultsChain(RenderFragment innermost, params UpdateTrigger?[] chain)
    {
        var current = innermost;
        for (var i = chain.Length - 1; i >= 0; i--)
        {
            var updateOn = chain[i];
            var next = current;
            current = b =>
            {
                b.OpenComponent<FormDefaults>(0);
                b.AddAttribute(1, nameof(FormDefaults.UpdateOn), updateOn);
                b.AddAttribute(2, "ChildContent", next);
                b.CloseComponent();
            };
        }
        return current;
    }

    IRenderedComponent<ContainerFragment> RenderControl(PersonModel model, RenderFragment control, params UpdateTrigger?[] formDefaultsChain) =>
        Render(WithForm(model, WrapFormDefaultsChain(control, formDefaultsChain)));

    static RenderFragment EditStringFragment(PersonModel model, UpdateTrigger? updateOn, EventCallback<string?> onChanged) => b =>
    {
        Expression<Func<string?>> field = () => model.Name;
        b.OpenComponent<EditString>(0);
        b.AddAttribute(1, "Value", model.Name);
        b.AddAttribute(2, "ValueExpression", field);
        b.AddAttribute(3, "ValueChanged", onChanged);
        if (updateOn is not null) b.AddAttribute(4, "UpdateOn", updateOn);
        b.CloseComponent();
    };

    static RenderFragment EditNumberFragment(PersonModel model, UpdateTrigger? updateOn, EventCallback<int?> onChanged) => b =>
    {
        Expression<Func<int?>> field = () => model.Age;
        b.OpenComponent<EditNumber<int?>>(0);
        b.AddAttribute(1, "Value", model.Age);
        b.AddAttribute(2, "ValueExpression", field);
        b.AddAttribute(3, "ValueChanged", onChanged);
        if (updateOn is not null) b.AddAttribute(4, "UpdateOn", updateOn);
        b.CloseComponent();
    };

    static RenderFragment EditDateNativeFragment(PersonModel model, UpdateTrigger? updateOn, EventCallback<DateTime?> onChanged) => b =>
    {
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        b.OpenComponent<EditDateNative<DateTime?>>(0);
        b.AddAttribute(1, "Value", model.BirthDate);
        b.AddAttribute(2, "ValueExpression", field);
        b.AddAttribute(3, "ValueChanged", onChanged);
        if (updateOn is not null) b.AddAttribute(4, "UpdateOn", updateOn);
        b.CloseComponent();
    };

    static RenderFragment EditTextAreaFragment(
        PersonModel model, UpdateTrigger? updateOn, EventCallback<string?> onChanged, int? minRows = null, int? maxRows = null) => b =>
    {
        Expression<Func<string?>> field = () => model.Name;
        b.OpenComponent<EditTextArea>(0);
        b.AddAttribute(1, "Value", model.Name);
        b.AddAttribute(2, "ValueExpression", field);
        b.AddAttribute(3, "ValueChanged", onChanged);
        b.AddAttribute(4, "AutoSize", true);
        if (minRows is not null) b.AddAttribute(5, "MinRows", minRows);
        if (maxRows is not null) b.AddAttribute(6, "MaxRows", maxRows);
        if (updateOn is not null) b.AddAttribute(7, "UpdateOn", updateOn);
        b.CloseComponent();
    };

    // ── 1. Cascade ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Cascaded_FormDefaults_UpdateOn_Change_overrides_EditStrings_Input_default()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v)),
            UpdateTrigger.Change);

        var input = cut.Find("input.edit-string-input");
        // EditString's own default is Input (oninput) -- the cascaded FormDefaults must move it to Change.
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("typed"));
        input.Change("typed");
        Assert.Equal("typed", captured);
    }

    // ── 2. Nesting / chaining ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Inner_FormDefaults_leaving_UpdateOn_null_falls_through_to_the_outer()
    {
        // Host-level FormDefaults sets Change; an MFE-root FormDefaults nested inside leaves UpdateOn
        // unset. The unset inner property must fall through to the outer, not skip past it to the
        // control's own Input default.
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v)),
            UpdateTrigger.Change, null);

        var input = cut.Find("input.edit-string-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("typed"));
        input.Change("typed");
        Assert.Equal("typed", captured);
    }

    [Fact]
    public void Inner_FormDefaults_setting_UpdateOn_overrides_the_outer_for_its_subtree()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v)),
            UpdateTrigger.Change, UpdateTrigger.Input);

        var input = cut.Find("input.edit-string-input");
        // The inner FormDefaults (Input) wins for this subtree over the outer's Change.
        input.Input("typed");
        Assert.Equal("typed", captured);
    }

    // ── 3. Precedence: the control's own UpdateOn beats the cascaded FormDefaults, either direction ──

    [Fact]
    public void Instance_UpdateOn_Input_beats_a_cascaded_FormDefaults_Change()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, UpdateTrigger.Input, EventCallback.Factory.Create<string?>(this, v => captured = v)),
            UpdateTrigger.Change);

        var input = cut.Find("input.edit-string-input");
        // The control's own parameter wins outright -- no exception, commits per keystroke.
        input.Input("typed");
        Assert.Equal("typed", captured);
    }

    [Fact]
    public void Instance_UpdateOn_Change_beats_a_cascaded_FormDefaults_Input()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, UpdateTrigger.Change, EventCallback.Factory.Create<string?>(this, v => captured = v)),
            UpdateTrigger.Input);

        var input = cut.Find("input.edit-string-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("typed"));
        input.Change("typed");
        Assert.Equal("typed", captured);
    }

    // ── 4. Per-control default survives with no FormDefaults in scope and no instance parameter ────

    [Fact]
    public void EditNumber_defaults_to_Change_with_no_FormDefaults_in_scope()
    {
        var model = new PersonModel { Age = 1 };
        int? captured = null;
        var cut = RenderControl(model,
            EditNumberFragment(model, updateOn: null, EventCallback.Factory.Create<int?>(this, v => captured = v)));

        var input = cut.Find("input.edit-number-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("5"));
        input.Change("5");
        Assert.Equal(5, captured);
    }

    [Fact]
    public void EditDateNative_defaults_to_Change_with_no_FormDefaults_in_scope()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        DateTime? captured = null;
        var cut = RenderControl(model,
            EditDateNativeFragment(model, updateOn: null, EventCallback.Factory.Create<DateTime?>(this, v => captured = v)));

        var input = cut.Find("input.edit-date-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("2021-06-15"));
        input.Change("2021-06-15");
        Assert.Equal(new DateTime(2021, 6, 15), captured);
    }

    [Fact]
    public void EditString_defaults_to_Input_with_no_FormDefaults_in_scope()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        var cut = RenderControl(model,
            EditStringFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v)));

        var input = cut.Find("input.edit-string-input");
        // No cascade, no instance override -- EditString's own built-in default (Input) applies.
        input.Input("typed");
        Assert.Equal("typed", captured);
    }

    // ── 5. EditTextArea + AutoSize interaction ──────────────────────────────────────────────────

    int AutoSizeCalls() => JSInterop.Invocations.Count(i => i.Identifier == "WssEditControls.autoSizeTextArea");

    [Fact]
    public void EditTextArea_AutoSize_with_a_cascaded_Change_trigger_still_measures_via_the_extra_oninput_handler()
    {
        // AutoSize + Change is the one combination where AutoSizeInputAttribute splats an extra
        // measure-only oninput handler onto the textarea (see EditTextArea.razor.cs) -- otherwise the
        // box would stop growing mid-typing, since @bind-value:after only fires on the bound event
        // (onchange here), i.e. blur/Enter.
        var model = new PersonModel { Name = "hello" };
        string? captured = model.Name;
        var cut = RenderControl(model,
            EditTextAreaFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v), minRows: 2, maxRows: 8),
            UpdateTrigger.Change);

        var textarea = cut.Find("textarea.edit-textarea-input");
        // Let the first-render AutoSize call (fired from OnAfterRenderAsync) land before establishing
        // the baseline -- its completion isn't necessarily synchronous with Render() returning.
        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1));
        var baseline = AutoSizeCalls();

        // The bound commit event is onchange -- typing alone must not throw (a handler IS registered
        // on oninput) and must not commit the model value.
        textarea.Input("hello world");
        Assert.Equal("hello", captured);
        cut.WaitForAssertion(() => Assert.Equal(baseline + 1, AutoSizeCalls())); // the extra handler measured this keystroke

        // Blur/Enter (onchange) still commits the model value as normal.
        textarea.Change("hello world");
        Assert.Equal("hello world", captured);
    }

    [Fact]
    public void EditTextArea_AutoSize_with_the_default_Input_trigger_has_no_stray_duplicate_handler()
    {
        // Default resolution (no FormDefaults, no instance UpdateOn) is Input for EditTextArea, so
        // AutoSizeInputAttribute must be null and splat no attribute at all -- @bind-value:after alone
        // drives the resize. Proves both halves: the bound value still commits per keystroke (a
        // reintroduced splat here would win the last-attribute-wins race and silently replace the
        // bind's own oninput handler), and the JS resize fires exactly once per keystroke, not twice.
        var model = new PersonModel { Name = "hello" };
        string? captured = model.Name;
        var cut = RenderControl(model,
            EditTextAreaFragment(model, updateOn: null, EventCallback.Factory.Create<string?>(this, v => captured = v), minRows: 2, maxRows: 8));

        var textarea = cut.Find("textarea.edit-textarea-input");
        cut.WaitForAssertion(() => Assert.True(AutoSizeCalls() >= 1));
        var baseline = AutoSizeCalls();

        textarea.Input("hello world");

        Assert.Equal("hello world", captured);
        cut.WaitForAssertion(() => Assert.Equal(baseline + 1, AutoSizeCalls()));
    }
}
