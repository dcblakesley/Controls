using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the <see cref="EditInputShell"/> backbone refactor (slice S1 of the AntD input-parity
/// work):
/// <list type="bullet">
///   <item>DOM-stability regression — with no affix parameters set, EditString/EditNumber/
///   EditTextArea/EditDate must still render today's exact <c>edit-input-with-icon</c> wrapper: a
///   single editor child, plus a second <see cref="InvalidIcon"/> child only while invalid.</item>
///   <item>The shell's affix-mode layout — prefix/suffix/clear/count/password-toggle content, the
///   locked suffix ordering, and the clearable/aria wiring — exercised directly against
///   <see cref="EditInputShell"/> since no host control passes affix parameters yet.</item>
/// </list>
/// </summary>
public class EditInputShellTests : TestContext
{
    // EditForm(editContext) -> DataAnnotationsValidator + CascadingValue<FormOptions> -> inner.
    // Mirrors ValidationStateTests.RenderForm: passing the EditContext explicitly (rather than
    // EditForm.Model) is what lets a test call editContext.Validate() itself.
    IRenderedFragment RenderForm(EditContext editContext, RenderFragment inner) =>
        Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenComponent<CascadingValue<FormOptions>>(1);
                formContent.AddAttribute(2, "Value", new FormOptions());
                formContent.AddAttribute(3, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });

    // ── DOM-stability regression: legacy (no affix parameters) wrapper is untouched ──────────────

    [Fact]
    public void EditString_wrapper_is_the_legacy_edit_input_with_icon_markup_when_valid()
    {
        var model = new PersonModel { Name = "Alice" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Equal("div", wrapper.TagName, ignoreCase: true);
        Assert.Single(wrapper.ClassList);
        Assert.Single(wrapper.Children); // just the editor -- no InvalidIcon while valid
        var editor = wrapper.Children[0];
        Assert.Equal("input", editor.TagName, ignoreCase: true);
        Assert.Equal("padding-inline-end: 2rem", editor.GetAttribute("style"));
        Assert.Empty(cut.FindAll(".edit-icon-invalid"));
    }

    [Fact]
    public void EditString_wrapper_adds_InvalidIcon_as_a_second_child_when_invalid()
    {
        var model = new PersonModel { Name = "" }; // [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Equal(2, wrapper.Children.Length);
        Assert.Equal("input", wrapper.Children[0].TagName, ignoreCase: true);
        Assert.Equal("svg", wrapper.Children[1].TagName, ignoreCase: true);
        Assert.Contains("edit-icon-invalid", wrapper.Children[1].ClassList);
    }

    [Fact]
    public void EditNumber_wrapper_is_the_legacy_edit_input_with_icon_markup_when_valid()
    {
        var model = new PersonModel { Age = 30 };
        var editContext = new EditContext(model);
        Expression<Func<int?>> field = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditNumber<int?>>(0);
            content.AddAttribute(1, "Value", model.Age);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Single(wrapper.Children);
        var editor = wrapper.Children[0];
        Assert.Equal("input", editor.TagName, ignoreCase: true);
        Assert.Equal("number", editor.GetAttribute("type"));
        Assert.Equal("padding-inline-end: 2rem", editor.GetAttribute("style"));
        Assert.Empty(cut.FindAll(".edit-icon-invalid"));
    }

    [Fact]
    public void EditNumber_wrapper_adds_InvalidIcon_as_a_second_child_when_invalid()
    {
        var model = new PersonModel { Age = null }; // [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<int?>> field = () => model.Age;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditNumber<int?>>(0);
            content.AddAttribute(1, "Value", model.Age);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Equal(2, wrapper.Children.Length);
        Assert.Contains("edit-icon-invalid", wrapper.Children[1].ClassList);
    }

    [Fact]
    public void EditTextArea_wrapper_is_the_legacy_edit_input_with_icon_markup_when_valid()
    {
        var model = new PersonModel { Name = "hello" };
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditTextArea>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Single(wrapper.Children);
        var editor = wrapper.Children[0];
        Assert.Equal("textarea", editor.TagName, ignoreCase: true);
        Assert.Equal("padding-inline-end: 2rem", editor.GetAttribute("style"));
        Assert.Empty(cut.FindAll(".edit-icon-invalid"));
    }

    [Fact]
    public void EditTextArea_wrapper_adds_InvalidIcon_as_a_second_child_when_invalid()
    {
        var model = new PersonModel { Name = "" }; // [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditTextArea>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Equal(2, wrapper.Children.Length);
        Assert.Contains("edit-icon-invalid", wrapper.Children[1].ClassList);
    }

    [Fact]
    public void EditDate_wrapper_is_the_legacy_edit_input_with_icon_markup_when_valid()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditDate<DateTime?>>(0);
            content.AddAttribute(1, "Value", model.BirthDate);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Single(wrapper.Children);
        var editor = wrapper.Children[0];
        Assert.Equal("input", editor.TagName, ignoreCase: true);
        Assert.Equal("date", editor.GetAttribute("type"));
        Assert.Equal("padding-inline-end: 2rem", editor.GetAttribute("style"));
        Assert.Empty(cut.FindAll(".edit-icon-invalid"));
    }

    [Fact]
    public void EditDate_wrapper_adds_InvalidIcon_as_a_second_child_when_invalid()
    {
        var model = new PersonModel { BirthDate = null }; // [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditDate<DateTime?>>(0);
            content.AddAttribute(1, "Value", model.BirthDate);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var wrapper = cut.Find(".edit-input-with-icon");
        Assert.Equal(2, wrapper.Children.Length);
        Assert.Contains("edit-icon-invalid", wrapper.Children[1].ClassList);
    }

    // ── EditInputShell affix mode (rendered directly -- no host control triggers this yet) ───────

    [Fact]
    public void EditInputShell_renders_legacy_markup_when_no_affix_parameter_is_set()
    {
        var cut = RenderComponent<EditInputShell>(p => p
            .AddChildContent("<input class=\"my-editor\" />"));

        Assert.Single(cut.FindAll(".edit-input-with-icon"));
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper"));
    }

    [Fact]
    public void EditInputShell_affix_wrapper_carries_the_invalid_class_only_while_invalid()
    {
        var invalid = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsInvalid, true)
            .AddChildContent("<input />"));
        Assert.Contains("edit-input-affix-invalid", invalid.Find(".edit-input-affix-wrapper").ClassList);

        var valid = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsInvalid, false)
            .AddChildContent("<input />"));
        Assert.DoesNotContain("edit-input-affix-invalid", valid.Find(".edit-input-affix-wrapper").ClassList);
    }

    [Fact]
    public void EditInputShell_affix_wrapper_places_prefix_before_and_suffix_after_the_editor()
    {
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.Prefix, "<span class=\"my-prefix\">$</span>")
            .Add(s => s.AllowClear, true)
            .AddChildContent("<input class=\"my-editor\" />"));

        var wrapper = cut.Find(".edit-input-affix-wrapper");
        Assert.Equal(3, wrapper.Children.Length);
        Assert.Contains("edit-input-prefix", wrapper.Children[0].ClassList);
        Assert.Contains("my-editor", wrapper.Children[1].ClassList);
        Assert.Contains("edit-input-suffix", wrapper.Children[2].ClassList);
    }

    [Fact]
    public void EditInputShell_suffix_order_is_clear_then_count_then_custom_suffix_then_password_then_invalid()
    {
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsClearable, true)
            .Add(s => s.CountText, "3 / 10")
            .Add(s => s.Suffix, "<span class=\"my-suffix\">USD</span>")
            .Add(s => s.ShowPasswordToggle, true)
            .Add(s => s.IsInvalid, true)
            .AddChildContent("<input />"));

        var suffix = cut.Find(".edit-input-suffix");
        Assert.Equal(5, suffix.Children.Length);
        Assert.Contains("edit-input-clear", suffix.Children[0].ClassList);
        Assert.Contains("edit-input-count", suffix.Children[1].ClassList);
        Assert.Equal("3 / 10", suffix.Children[1].TextContent);
        Assert.Contains("my-suffix", suffix.Children[2].ClassList);
        Assert.Contains("edit-input-password-toggle", suffix.Children[3].ClassList);
        Assert.Contains("edit-icon-invalid", suffix.Children[4].ClassList);
    }

    [Fact]
    public void EditInputShell_clear_button_is_absent_when_IsClearable_is_false()
    {
        // AllowClear alone keeps the affix wrapper in place (so the box doesn't resize as the user
        // types); the button itself only shows while IsClearable is also true.
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsClearable, false)
            .AddChildContent("<input />"));

        Assert.NotEmpty(cut.FindAll(".edit-input-affix-wrapper"));
        Assert.Empty(cut.FindAll(".edit-input-clear"));
    }

    [Theory]
    [InlineData(false, "Show value", "false", "eye-invisible")]
    [InlineData(true, "Hide value", "true", "eye")]
    public void EditInputShell_password_toggle_aria_and_icon_reflect_IsPasswordRevealed(
        bool revealed, string expectedLabel, string expectedPressed, string expectedIcon)
    {
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.ShowPasswordToggle, true)
            .Add(s => s.IsPasswordRevealed, revealed)
            .AddChildContent("<input />"));

        var button = cut.Find(".edit-input-password-toggle");
        Assert.Equal(expectedLabel, button.GetAttribute("aria-label"));
        Assert.Equal(expectedPressed, button.GetAttribute("aria-pressed"));
        Assert.Equal(expectedIcon, cut.Find(".edit-input-password-toggle svg").GetAttribute("data-icon"));
    }

    [Fact]
    public void EditInputShell_clear_and_password_buttons_invoke_their_callbacks_on_click()
    {
        var cleared = false;
        var toggled = false;
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsClearable, true)
            .Add(s => s.OnClear, EventCallback.Factory.Create(this, () => cleared = true))
            .Add(s => s.ShowPasswordToggle, true)
            .Add(s => s.OnTogglePassword, EventCallback.Factory.Create(this, () => toggled = true))
            .AddChildContent("<input />"));

        cut.Find(".edit-input-clear").Click();
        cut.Find(".edit-input-password-toggle").Click();

        Assert.True(cleared);
        Assert.True(toggled);
    }
}
