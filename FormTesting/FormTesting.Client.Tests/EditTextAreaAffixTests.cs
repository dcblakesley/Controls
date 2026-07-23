using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditTextArea's slice-S3 AntD-parity parameters (AllowClear/MaxLength/ShowCount/AutoSize/
/// MinRows/MaxRows) — the DOM-stability regression for the no-new-params case already lives in
/// <see cref="EditInputShellTests"/> (slice S1) and is re-run by the same test pass; this file only
/// adds coverage for the new affix/count-below/autosize surface. AutoSize's actual JS resize can't be
/// exercised under bUnit (no JS engine runs) — <see cref="AutoSize_true_adds_the_resize_disabling_class_and_does_not_throw_under_bUnits_JSInterop"/>
/// only proves the render completes and the binding still works; the real resize behavior gets e2e
/// coverage instead (see EditTextAreaE2ETests in FormTesting.Client.E2ETests).
/// </summary>
public class EditTextAreaAffixTests : TestContext
{
    public EditTextAreaAffixTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate Clear()'s FocusAsync + AutoSize's JS call

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    [Fact]
    public void With_no_new_params_the_textarea_stays_in_legacy_mode_with_no_autosize_class()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.DoesNotContain("edit-textarea-autosize", textarea.ClassList);
        Assert.DoesNotContain("edit-affix-input", textarea.ClassList);
        Assert.Equal("2", textarea.GetAttribute("rows"));
        Assert.False(textarea.HasAttribute("maxlength"));
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper"));
        Assert.NotEmpty(cut.FindAll(".edit-input-with-icon"));
    }

    [Theory]
    [InlineData("hello", false, true)]  // non-empty + enabled -> visible
    [InlineData("", false, false)]      // empty -> hidden
    [InlineData("hello", true, false)]  // disabled -> hidden
    public void AllowClear_button_appears_only_when_value_non_empty_and_enabled(string value, bool disabled, bool expectVisible)
    {
        var model = new PersonModel { Name = value };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.AddAttribute(5, "IsDisabled", disabled);
            b.CloseComponent();
        }));

        Assert.Equal(expectVisible, cut.FindAll(".edit-input-clear").Count == 1);
        // AllowClear alone still switches the shell into affix mode (so the box never resizes as the
        // user types down to empty), regardless of whether the button itself is showing right now.
        Assert.NotEmpty(cut.FindAll(".edit-input-affix-wrapper"));
    }

    [Fact]
    public void AllowClear_click_clears_the_bound_value_and_the_button_disappears()
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
            b.AddAttribute(5, "AllowClear", true);
            b.CloseComponent();
        }));

        cut.Find(".edit-input-clear").Click();

        Assert.Null(captured);
        Assert.Empty(cut.FindAll(".edit-input-clear")); // value is now empty -- button withdraws
    }

    [Fact]
    public void MaxLength_renders_the_maxlength_attribute_and_omits_it_when_null()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;

        var withMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "MaxLength", 50);
            b.CloseComponent();
        }));
        Assert.Equal("50", withMax.Find("textarea.edit-textarea-input").GetAttribute("maxlength"));

        var withoutMax = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        Assert.False(withoutMax.Find("textarea.edit-textarea-input").HasAttribute("maxlength"));
    }

    [Fact]
    public void ShowCount_renders_below_the_editor_as_a_trailing_sibling_of_the_suffix_span_not_inside_it()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.AddAttribute(5, "MaxLength", 20);
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".edit-input-affix-wrapper");
        Assert.Equal("5 / 20", wrapper.QuerySelector(".edit-textarea-count")!.TextContent);
        // Not EditString's inline count span -- CountBelow moves it out of edit-input-suffix.
        Assert.Empty(cut.FindAll(".edit-input-count"));
        // A trailing sibling of edit-input-suffix -- the wrapper's last child -- not nested inside it.
        Assert.Contains("edit-textarea-count", wrapper.Children[^1].ClassList);
        Assert.Contains("edit-input-suffix", wrapper.Children[^2].ClassList);
    }

    [Fact]
    public void ShowCount_updates_as_the_value_changes()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));
        Assert.Equal("5", cut.Find(".edit-textarea-count").TextContent);

        cut.Find("textarea.edit-textarea-input").Input("hello world");
        Assert.Equal("11", cut.Find(".edit-textarea-count").TextContent);
    }

    [Fact]
    public void Rows_attribute_uses_Rows_when_AutoSize_is_off_even_if_MinRows_is_set()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Rows", 4);
            b.AddAttribute(5, "MinRows", 8); // inert -- AutoSize is off
            b.CloseComponent();
        }));

        Assert.Equal("4", cut.Find("textarea.edit-textarea-input").GetAttribute("rows"));
    }

    [Fact]
    public void Rows_attribute_falls_back_to_Rows_when_AutoSize_is_on_and_MinRows_is_unset()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Rows", 4);
            b.AddAttribute(5, "AutoSize", true);
            b.CloseComponent();
        }));

        Assert.Equal("4", cut.Find("textarea.edit-textarea-input").GetAttribute("rows"));
    }

    [Fact]
    public void Rows_attribute_uses_MinRows_when_AutoSize_is_on_and_MinRows_is_set()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Rows", 4);
            b.AddAttribute(5, "AutoSize", true);
            b.AddAttribute(6, "MinRows", 6);
            b.CloseComponent();
        }));

        Assert.Equal("6", cut.Find("textarea.edit-textarea-input").GetAttribute("rows"));
    }

    [Fact]
    public void AutoSize_false_never_adds_the_resize_disabling_class()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.DoesNotContain("edit-textarea-autosize", cut.Find("textarea.edit-textarea-input").ClassList);
    }

    [Fact]
    public void AutoSize_true_adds_the_resize_disabling_class_and_does_not_throw_under_bUnits_JSInterop()
    {
        var model = new PersonModel { Name = "hello" };
        string? captured = null;
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<string?>(this, v => captured = v));
            b.AddAttribute(5, "AutoSize", true);
            b.AddAttribute(6, "MinRows", 2);
            b.AddAttribute(7, "MaxRows", 8);
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea.edit-textarea-input");
        Assert.Contains("edit-textarea-autosize", textarea.ClassList);

        // Typing invokes the JS resize (via @bind-value:after) on every keystroke. bUnit's Loose
        // JSInterop tolerates the unconfigured "WssEditControls.autoSizeTextArea" call instead of
        // throwing -- this only needs to prove the render completes and the binding still round-trips.
        textarea.Input("line one\nline two\nline three");
        Assert.Equal("line one\nline two\nline three", captured);
    }
}
