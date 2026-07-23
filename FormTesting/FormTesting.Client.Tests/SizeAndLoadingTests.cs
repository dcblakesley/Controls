using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers slice S4 of the AntD input-parity work: the shared <see cref="SelectSize"/> parameter on
/// EditString/EditNumber/EditTextArea/EditDate (editor + affix-wrapper class, DOM-stability at
/// <see cref="SelectSize.Default"/>), <see cref="EditInputShell"/>'s new <c>WrapperClass</c>/
/// <c>IsDisabled</c> parameters, and <see cref="SearchInput"/>'s <c>Loading</c> state. The
/// `.edit-theme` CSS itself isn't exercised here (bUnit doesn't apply stylesheets) -- only the class
/// wiring that CSS keys off.
/// </summary>
public class SizeAndLoadingTests : TestContext
{
    public SizeAndLoadingTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate Clear()'s FocusAsync where exercised incidentally

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ── EditInputShell: WrapperClass / IsDisabled ────────────────────────────────────────────────

    [Fact]
    public void EditInputShell_WrapperClass_is_appended_to_the_affix_wrapper()
    {
        var cut = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.WrapperClass, "edit-input-lg")
            .AddChildContent("<input />"));

        Assert.Contains("edit-input-lg", cut.Find(".edit-input-affix-wrapper").ClassList);
    }

    [Fact]
    public void EditInputShell_IsDisabled_adds_the_affix_disabled_class_only_while_true()
    {
        var disabled = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsDisabled, true)
            .AddChildContent("<input />"));
        Assert.Contains("edit-input-affix-disabled", disabled.Find(".edit-input-affix-wrapper").ClassList);

        var enabled = RenderComponent<EditInputShell>(p => p
            .Add(s => s.AllowClear, true)
            .Add(s => s.IsDisabled, false)
            .AddChildContent("<input />"));
        Assert.DoesNotContain("edit-input-affix-disabled", enabled.Find(".edit-input-affix-wrapper").ClassList);
    }

    [Fact]
    public void EditInputShell_SizeClass_maps_the_shared_enum_and_Default_yields_null()
    {
        Assert.Null(EditInputShell.SizeClass(SelectSize.Default));
        Assert.Equal("edit-input-sm", EditInputShell.SizeClass(SelectSize.Small));
        Assert.Equal("edit-input-lg", EditInputShell.SizeClass(SelectSize.Large));
    }

    // ── EditString: Size ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditString_Size_Default_adds_no_class_in_legacy_or_affix_mode()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;

        var legacy = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Size", SelectSize.Default);
            b.CloseComponent();
        }));
        var legacyInput = legacy.Find("input.edit-string-input");
        Assert.DoesNotContain("edit-input-sm", legacyInput.ClassList);
        Assert.DoesNotContain("edit-input-lg", legacyInput.ClassList);

        var affix = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.CloseComponent();
        }));
        var wrapper = affix.Find(".edit-input-affix-wrapper");
        Assert.DoesNotContain("edit-input-sm", wrapper.ClassList);
        Assert.DoesNotContain("edit-input-lg", wrapper.ClassList);
    }

    [Theory]
    [InlineData(SelectSize.Small, "edit-input-sm")]
    [InlineData(SelectSize.Large, "edit-input-lg")]
    public void EditString_Size_appends_the_token_to_the_input_in_legacy_mode(SelectSize size, string token)
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Size", size);
            b.CloseComponent();
        }));

        Assert.Contains(token, cut.Find("input.edit-string-input").ClassList);
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper")); // Size alone doesn't switch to affix mode
    }

    [Fact]
    public void EditString_Size_appends_the_token_to_both_the_input_and_the_affix_wrapper()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.AddAttribute(5, "Size", SelectSize.Large);
            b.CloseComponent();
        }));

        Assert.Contains("edit-input-lg", cut.Find("input.edit-string-input").ClassList);
        Assert.Contains("edit-input-lg", cut.Find(".edit-input-affix-wrapper").ClassList);
    }

    // ── EditNumber: Size ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditNumber_Size_Default_adds_no_class()
    {
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-number-input");
        Assert.DoesNotContain("edit-input-sm", input.ClassList);
        Assert.DoesNotContain("edit-input-lg", input.ClassList);
    }

    [Fact]
    public void EditNumber_Size_appends_the_token_to_both_the_input_and_the_affix_wrapper()
    {
        var model = new PersonModel { Price = 19.99m };
        Expression<Func<decimal?>> field = () => model.Price;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<decimal?>>(0);
            b.AddAttribute(1, "Value", model.Price);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Prefix", (RenderFragment)(rb => rb.AddContent(0, "$")));
            b.AddAttribute(5, "Size", SelectSize.Small);
            b.CloseComponent();
        }));

        Assert.Contains("edit-input-sm", cut.Find("input.edit-number-input").ClassList);
        Assert.Contains("edit-input-sm", cut.Find(".edit-input-affix-wrapper").ClassList);
    }

    // ── EditTextArea: Size ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditTextArea_Size_Default_adds_no_class()
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
        Assert.DoesNotContain("edit-input-sm", textarea.ClassList);
        Assert.DoesNotContain("edit-input-lg", textarea.ClassList);
    }

    [Fact]
    public void EditTextArea_Size_appends_the_token_to_both_the_textarea_and_the_affix_wrapper()
    {
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.AddAttribute(5, "Size", SelectSize.Large);
            b.CloseComponent();
        }));

        Assert.Contains("edit-input-lg", cut.Find("textarea.edit-textarea-input").ClassList);
        Assert.Contains("edit-input-lg", cut.Find(".edit-input-affix-wrapper").ClassList);
    }

    // ── EditDate: Size ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void EditDate_Size_Default_adds_no_class()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-date-input");
        Assert.DoesNotContain("edit-input-sm", input.ClassList);
        Assert.DoesNotContain("edit-input-lg", input.ClassList);
        Assert.Empty(cut.FindAll(".edit-input-affix-wrapper"));
    }

    [Fact]
    public void EditDate_Size_appends_the_token_to_the_input()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Size", SelectSize.Small);
            b.CloseComponent();
        }));

        Assert.Contains("edit-input-sm", cut.Find("input.edit-date-input").ClassList);
    }

    // ── EditString/EditNumber/EditTextArea: IsDisabled forwarded to the shell's wrapper ──────────

    [Fact]
    public void EditString_disabled_affix_control_marks_the_wrapper_disabled()
    {
        var model = new PersonModel { Name = "Alice" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "AllowClear", true);
            b.AddAttribute(5, "IsDisabled", true);
            b.CloseComponent();
        }));

        Assert.Contains("edit-input-affix-disabled", cut.Find(".edit-input-affix-wrapper").ClassList);
    }

    // ── SearchInput: Loading ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void SearchInput_Loading_false_renders_the_search_glyph_and_stays_byte_stable()
    {
        var cut = RenderComponent<SearchInput>(p => p.Add(s => s.AddonLabel, "POs"));

        var button = cut.Find(".wss-search-btn");
        Assert.Null(button.GetAttribute("aria-busy"));
        Assert.False(button.HasAttribute("disabled"));
        Assert.Empty(cut.FindAll(".wss-icon-spin"));
        Assert.NotEmpty(button.QuerySelectorAll("svg"));
    }

    [Fact]
    public void SearchInput_Loading_true_swaps_the_icon_and_marks_the_button_busy_and_disabled()
    {
        var cut = RenderComponent<SearchInput>(p => p.Add(s => s.Loading, true));

        var button = cut.Find(".wss-search-btn");
        Assert.Equal("true", button.GetAttribute("aria-busy"));
        Assert.True(button.HasAttribute("disabled"));
        Assert.NotEmpty(cut.FindAll(".wss-icon-spin"));
        Assert.Single(button.QuerySelectorAll("svg")); // one spinner svg, not both icons
    }

    [Fact]
    public void SearchInput_Loading_blocks_OnSearch_on_both_enter_and_click()
    {
        var searches = new List<string?>();
        var cut = RenderComponent<SearchInput>(p => p
            .Add(s => s.Value, "abc")
            .Add(s => s.Loading, true)
            .Add(s => s.OnSearch, (string? v) => searches.Add(v)));

        cut.Find(".wss-search-input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        cut.Find(".wss-search-btn").Click();

        Assert.Empty(searches);
    }

    [Fact]
    public void SearchInput_Loading_does_not_disable_the_text_input_itself()
    {
        // Only the button is busy/disabled -- the input stays editable while a search is pending
        // (matching AntD's own Input.Search loading behavior).
        var cut = RenderComponent<SearchInput>(p => p.Add(s => s.Loading, true));

        Assert.False(cut.Find(".wss-search-input").HasAttribute("disabled"));
    }
}
