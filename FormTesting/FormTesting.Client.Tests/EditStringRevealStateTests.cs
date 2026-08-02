using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditString's two "show me the secret" states — the password toggle's
/// <c>_passwordRevealed</c> and the read-only masked row's <c>_showMaskedValue</c>. Neither is a
/// parameter, so nothing reset them unless the control does it deliberately: they used to survive a
/// mode round-trip, an <c>IsPassword</c> flip, and (for the masked row) being handed a different
/// record's value, each of which re-exposes a secret with no user gesture. Also pins that a disabled
/// password field can't be revealed at all.
/// </summary>
/// <remarks>
/// These tests re-parameterize an already-rendered control, so they render <see cref="EditForm"/>
/// itself (rather than the usual <c>WithForm</c> fragment) and re-run it with <c>cut.Render()</c>:
/// that re-invokes <c>ChildContent</c> from the top, which is what re-reads the mutable locals below
/// and pushes the new values down as real parameter changes. Rendering the fragment instead leaves
/// the <c>EditForm</c> child's parameters reference-identical, and the diff then skips its
/// <c>SetParametersAsync</c> entirely.
/// </remarks>
public class EditStringRevealStateTests : BunitContext
{
    class UnconstrainedModel
    {
        public string? Text { get; set; }
    }

    IRenderedComponent<EditForm> RenderForm(object model, Action<RenderTreeBuilder> inner) =>
        Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => content => inner(content))));

    [Fact]
    public void A_disabled_password_field_renders_a_disabled_toggle_that_cannot_reveal()
    {
        // The toggle stays in the DOM (the field's chrome keeps its width), but native `disabled`
        // both blocks the reveal in a browser and drops it out of the tab order -- AT should not
        // offer a working control inside a field it announces as unavailable. The C# guard is what
        // this assertion actually proves: bUnit dispatches to disabled elements regardless, which is
        // exactly the "some other path reached the handler" case the guard exists for.
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.AddAttribute(5, "IsDisabled", true);
            b.CloseComponent();
        });

        var toggle = cut.Find(".edit-input-password-toggle");
        Assert.True(toggle.HasAttribute("disabled"));
        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));

        toggle.Click();

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
        Assert.Equal("false", cut.Find(".edit-input-password-toggle").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void An_enabled_password_toggle_carries_no_disabled_attribute()
    {
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.CloseComponent();
        });

        Assert.False(cut.Find(".edit-input-password-toggle").HasAttribute("disabled"));
    }

    [Fact]
    public void Password_reveal_does_not_survive_a_round_trip_through_read_only_mode()
    {
        var isEditMode = true;
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", true);
            b.AddAttribute(5, "IsEditMode", isEditMode);
            b.CloseComponent();
        });

        cut.Find(".edit-input-password-toggle").Click();
        Assert.Equal("text", cut.Find("input.edit-string-input").GetAttribute("type"));

        // Out to read-only and back -- the same component instance, so the reveal state persists
        // unless the control drops it.
        isEditMode = false;
        cut.Render();
        Assert.Empty(cut.FindAll("input.edit-string-input"));

        isEditMode = true;
        cut.Render();

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
        Assert.Equal("false", cut.Find(".edit-input-password-toggle").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Password_reveal_does_not_survive_IsPassword_being_turned_off_and_on()
    {
        var isPassword = true;
        var model = new PersonModel { Name = "secret" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsPassword", isPassword);
            b.CloseComponent();
        });

        cut.Find(".edit-input-password-toggle").Click();
        Assert.Equal("text", cut.Find("input.edit-string-input").GetAttribute("type"));

        isPassword = false;
        cut.Render();
        Assert.False(cut.Find("input.edit-string-input").HasAttribute("type"));

        isPassword = true;
        cut.Render();

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
    }

    [Fact]
    public void Typing_does_not_un_reveal_a_password_field()
    {
        // The deliberate asymmetry: _passwordRevealed must NOT reset on a value change. In edit mode
        // every keystroke changes the value, so that rule would re-hide the box the moment the user
        // types the next character of the password they just asked to see. The label change is only
        // there to force a real parameter cycle with the new value already committed.
        var label = "Secret";
        var model = new UnconstrainedModel { Text = "secret" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => model.Text = v));
            b.AddAttribute(5, "IsPassword", true);
            b.AddAttribute(6, "Label", label);
            b.CloseComponent();
        });

        cut.Find(".edit-input-password-toggle").Click();
        Assert.Equal("text", cut.Find("input.edit-string-input").GetAttribute("type"));

        cut.Find("input.edit-string-input").Input("secretx");
        Assert.Equal("secretx", model.Text);

        label = "Secret (still)";
        cut.Render();

        Assert.Equal("text", cut.Find("input.edit-string-input").GetAttribute("type"));
    }

    [Fact]
    public void Masked_read_only_reveal_does_not_survive_a_round_trip_through_edit_mode()
    {
        var isEditMode = false;
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", isEditMode);
            b.CloseComponent();
        });

        cut.Find(".edit-masked-value button").Click();
        Assert.Equal("abcdefgh", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);

        isEditMode = true;
        cut.Render();
        Assert.NotNull(cut.Find("input.edit-string-input"));

        isEditMode = false;
        cut.Render();

        Assert.Equal("****-fgh", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
        Assert.Equal("false", cut.Find(".edit-masked-value button").GetAttribute("aria-pressed"));
    }

    [Fact]
    public void Masked_read_only_reveal_does_not_survive_the_bound_value_changing()
    {
        // The record-swap case: a list re-bound to a different record with no @key reuses the same
        // component instance, so revealing record A's masked value used to show record B's in the
        // clear the moment the parent re-parameterized.
        var model = new PersonModel { Name = "aaaaaaaa" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        });

        cut.Find(".edit-masked-value button").Click();
        Assert.Equal("aaaaaaaa", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);

        model.Name = "bbbbbbbb";
        cut.Render();

        Assert.Equal("****-bbb", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
        Assert.DoesNotContain("bbbbbbbb", cut.Find(".edit-masked-value").TextContent);
    }

    [Fact]
    public void Masked_read_only_reveal_survives_a_re_render_that_leaves_the_value_alone()
    {
        // The reset must be targeted: a parent re-rendering for its own reasons (here a label
        // change), with the same value still bound, leaves the user's reveal alone.
        var label = "Card";
        var model = new PersonModel { Name = "abcdefgh" };
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaskText", "****-");
            b.AddAttribute(5, "IsEditMode", false);
            b.AddAttribute(6, "Label", label);
            b.CloseComponent();
        });

        cut.Find(".edit-masked-value button").Click();

        label = "Card number";
        cut.Render();

        Assert.Equal("abcdefgh", cut.Find(".edit-masked-value .edit-readonly-value").TextContent);
    }
}
