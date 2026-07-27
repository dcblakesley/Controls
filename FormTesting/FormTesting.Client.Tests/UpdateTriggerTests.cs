using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the per-control instance-level <c>UpdateOn</c> parameter (<see cref="UpdateTrigger"/>) on
/// the six controls that carry it: <see cref="EditString"/>, <see cref="EditTextArea"/>,
/// <see cref="EditNumber{T}"/>, <see cref="EditDate{T}"/>, <see cref="EditRadioString"/> (the "Other"
/// free-text box only), and <see cref="EditRadioEnum{TEnum}"/> (the "Other" free-text box only).
/// Deliberately out of scope: the <c>FormDefaults.EffectiveUpdateOn</c> cascade — that resolution
/// level is covered elsewhere.
/// </summary>
/// <remarks>
/// Blazor's <c>@bind-value:event</c>/<c>@bind:event</c> (and the equivalent manual
/// <c>@attributes</c> splat EditRadioEnum uses for its Other box) wire exactly ONE DOM event's
/// attribute per render — never both. So the DOM event this feature does NOT resolve to has no
/// handler attached at all, and triggering it through bUnit throws
/// <see cref="Bunit.MissingEventHandlerException"/> rather than silently no-op'ing. Every test below
/// asserts that throw for the non-resolved event before asserting the resolved event actually commits
/// the value — proving the two events are mutually exclusive, not just that the expected one works.
/// </remarks>
public class UpdateTriggerTests : BunitContext
{
    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ───────────────────────── EditString (control default: Input) ─────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Input)]
    public void EditString_default_or_explicit_Input_commits_every_keystroke_and_has_no_change_handler(UpdateTrigger? updateOn)
    {
        var model = new PersonModel { Name = "Alice" };
        string? captured = "Alice";
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            if (updateOn.HasValue) b.AddAttribute(5, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Change("Zed"));
        input.Input("Alicia");
        Assert.Equal("Alicia", captured);
    }

    [Fact]
    public void EditString_UpdateOn_Change_defers_commit_until_blur_and_has_no_input_handler()
    {
        var model = new PersonModel { Name = "Alice" };
        string? captured = "Alice";
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("Alicia"));
        input.Change("Alicia");
        Assert.Equal("Alicia", captured);
    }

    // ───────────────────────── EditTextArea (control default: Input) ───────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Input)]
    public void EditTextArea_default_or_explicit_Input_commits_every_keystroke_and_has_no_change_handler(UpdateTrigger? updateOn)
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = "hello";
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            if (updateOn.HasValue) b.AddAttribute(5, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => textarea.Change("goodbye"));
        textarea.Input("hello world");
        Assert.Equal("hello world", captured);
    }

    [Fact]
    public void EditTextArea_UpdateOn_Change_defers_commit_until_blur_and_has_no_input_handler()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = "hello";
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => textarea.Input("hello world"));
        textarea.Change("hello world");
        Assert.Equal("hello world", captured);
    }

    // ───────────────────────── EditNumber<int?> (control default: Change) ──────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Change)]
    public void EditNumber_default_or_explicit_Change_commits_only_on_change_and_has_no_input_handler(UpdateTrigger? updateOn)
    {
        var model = new PersonModel { Age = 1 };
        int? captured = 1;
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => captured = v));
            if (updateOn.HasValue) b.AddAttribute(5, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("42"));
        input.Change("42");
        Assert.Equal(42, captured);
    }

    [Fact]
    public void EditNumber_UpdateOn_Input_commits_every_keystroke_and_has_no_change_handler()
    {
        var model = new PersonModel { Age = 1 };
        int? captured = 1;
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<int?>(this, v => captured = v));
            b.AddAttribute(5, "UpdateOn", UpdateTrigger.Input);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Change("42"));
        input.Input("42");
        Assert.Equal(42, captured);
    }

    // ───────────────────────── EditDate<DateTime?> (control default: Change) ───────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Change)]
    public void EditDate_default_or_explicit_Change_commits_only_on_change_and_has_no_input_handler(UpdateTrigger? updateOn)
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        DateTime? captured = model.BirthDate;
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => captured = v));
            if (updateOn.HasValue) b.AddAttribute(5, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Input("2026-02-10"));
        input.Change("2026-02-10");
        Assert.Equal(new DateTime(2026, 2, 10), captured);
    }

    [Fact]
    public void EditDate_UpdateOn_Input_commits_every_keystroke_and_has_no_change_handler()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        DateTime? captured = model.BirthDate;
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => captured = v));
            b.AddAttribute(5, "UpdateOn", UpdateTrigger.Input);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.Throws<Bunit.MissingEventHandlerException>(() => input.Change("2026-02-10"));
        input.Input("2026-02-10");
        Assert.Equal(new DateTime(2026, 2, 10), captured);
    }

    // ───────────────────────── EditRadioString "Other" box (control default: Input) ────────────
    // UpdateOn scopes ONLY the free-text "Other" box -- the radio inputs themselves always commit
    // on native radio onchange and are never affected by this parameter.

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Input)]
    public void EditRadioString_Other_textbox_default_or_explicit_Input_commits_every_keystroke_and_has_no_change_handler(UpdateTrigger? updateOn)
    {
        var model = new PersonModel { Name = "" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "HasOther", true);
            b.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            if (updateOn.HasValue) b.AddAttribute(6, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        // Select "Other" first so its free-text box becomes enabled.
        var otherRadio = cut.Find("#rb-Name-other");
        otherRadio.Change(otherRadio.GetAttribute("value"));

        var otherBox = cut.Find("#txt-Name-custom-value");
        Assert.False(otherBox.HasAttribute("disabled"));
        Assert.Throws<Bunit.MissingEventHandlerException>(() => otherBox.Change("nope"));
        otherBox.Input("free text");
        Assert.Equal("free text", captured);
    }

    [Fact]
    public void EditRadioString_UpdateOn_Change_only_affects_the_Other_textbox_not_the_radios()
    {
        var model = new PersonModel { Name = "a" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "HasOther", true);
            b.AddAttribute(5, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(6, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        }));

        // Radios commit on native onchange immediately, regardless of the Other box's UpdateOn scoping.
        cut.Find("#rb-Name-b").Change("b");
        Assert.Equal("b", captured);

        // Select "Other" so its free-text box becomes enabled.
        var otherRadio = cut.Find("#rb-Name-other");
        otherRadio.Change(otherRadio.GetAttribute("value"));

        var otherBox = cut.Find("#txt-Name-custom-value");
        Assert.False(otherBox.HasAttribute("disabled"));
        Assert.Throws<Bunit.MissingEventHandlerException>(() => otherBox.Input("nope"));
        otherBox.Change("free text");
        Assert.Equal("free text", captured);
    }

    // ───────────────────────── EditRadioEnum "Other" box (control default: Input) ──────────────
    // Same scoping contract as EditRadioString: UpdateOn drives only the free-text "Other" box's
    // wired event (via a manual @attributes splat here rather than @bind:event); the enum radios
    // commit on native onchange and are unaffected. The Other box's model is the separate
    // OtherValue/OtherValueChanged parameter pair, not CurrentValue.

    [Theory]
    [InlineData(null)]
    [InlineData(UpdateTrigger.Input)]
    public void EditRadioEnum_Other_textbox_default_or_explicit_Input_commits_every_keystroke_and_has_no_change_handler(UpdateTrigger? updateOn)
    {
        // Critical is the last enum value -- with HasOtherOption it's the built-in "Other" slot, so
        // starting the model there means the Other box is enabled without any radio interaction.
        var model = new PersonModel { Priority = Priority.Critical };
        string? capturedOther = null;
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.AddAttribute(4, "OtherValueChanged", EventCallback.Factory.Create<string?>(this, v => capturedOther = v));
            if (updateOn.HasValue) b.AddAttribute(5, "UpdateOn", updateOn.Value);
            b.CloseComponent();
        }));

        var otherBox = cut.Find("input.edit-radio-other-input");
        Assert.False(otherBox.HasAttribute("disabled"));
        Assert.Throws<Bunit.MissingEventHandlerException>(() => otherBox.Change("nope"));
        otherBox.Input("details");
        Assert.Equal("details", capturedOther);
    }

    [Fact]
    public void EditRadioEnum_UpdateOn_Change_only_affects_the_Other_textbox_not_the_radios()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Priority? capturedValue = null;
        string? capturedOther = null;
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "HasOtherOption", true);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<Priority?>(this, v => capturedValue = v));
            b.AddAttribute(5, "OtherValueChanged", EventCallback.Factory.Create<string?>(this, v => capturedOther = v));
            b.AddAttribute(6, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        }));

        // Selecting the "Other" enum slot (Critical, the last value) commits on native radio onchange
        // immediately, regardless of the Other box's UpdateOn scoping.
        cut.Find("#rb-Priority-Critical").Change("Critical");
        Assert.Equal(Priority.Critical, capturedValue);

        var otherBox = cut.Find("input.edit-radio-other-input");
        Assert.False(otherBox.HasAttribute("disabled"));
        Assert.Throws<Bunit.MissingEventHandlerException>(() => otherBox.Input("nope"));
        otherBox.Change("details");
        Assert.Equal("details", capturedOther);
    }
}
