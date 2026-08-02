using System.Linq.Expressions;
using Bunit.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// The assistive-tech half of <c>ShowCount</c>, shared by EditString and EditTextArea through
/// <see cref="EditInputShell"/>. The visible count span used to be the only thing rendered: no id, not
/// in <c>aria-describedby</c>, and not <c>aria-hidden</c> — so a screen-reader user got it as orphan
/// noise while browsing, never on focus, and was never told a limit existed at all (nor that
/// <c>maxlength</c> had just silently truncated a paste).
/// </summary>
public class CharacterCountAccessibilityTests : BunitContext
{
    // A plain string property with no [StringLength], so MaxLength is exercised as a parameter in
    // isolation (PersonModel.Name carries one -- see EditStringAffixTests' note).
    class UnconstrainedModel
    {
        public string? Text { get; set; }
    }

    // The two hosts differ only in which editor element carries aria-describedby and where the visible
    // count lands (inline suffix vs. its own line below) -- everything asserted here is shared, so each
    // test runs against both rather than being written twice.
    public static TheoryData<string, string, string> Hosts => new()
    {
        { nameof(EditString), "input.edit-string-input", ".edit-input-count" },
        { nameof(EditTextArea), "textarea", ".edit-textarea-count" }
    };

    /// <summary>The same two hosts with only what each test actually needs — xUnit flags an unused theory parameter.</summary>
    public static TheoryData<string, string> HostEditors => new()
    {
        { nameof(EditString), "input.edit-string-input" },
        { nameof(EditTextArea), "textarea" }
    };

    public static TheoryData<string> HostNames => new() { nameof(EditString), nameof(EditTextArea) };

    IRenderedComponent<ContainerFragment> RenderHost(
        string host, UnconstrainedModel model, Expression<Func<string?>> field, bool showCount = true,
        int? maxLength = null, bool isEditMode = true, string? description = null) =>
        Render(WithForm(model, b =>
        {
            if (host == nameof(EditString)) b.OpenComponent<EditString>(0);
            else b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", showCount);
            b.AddAttribute(5, "MaxLength", maxLength);
            b.AddAttribute(6, "IsEditMode", isEditMode);
            b.AddAttribute(7, "Description", description);
            b.CloseComponent();
        }));

    static string[] DescribedBy(IRenderedComponent<ContainerFragment> cut, string selector) =>
        (cut.Find(selector).GetAttribute("aria-describedby") ?? "")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [MemberData(nameof(Hosts))]
    public void The_visible_count_is_hidden_from_AT_and_a_spoken_one_takes_its_place(
        string host, string editorSelector, string visibleCountSelector)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field, maxLength: 100);

        // "5 / 100" is read as "five slash one hundred" (or just "five one hundred") -- it is a visual
        // shorthand, so it is aria-hidden and the sr-only span below says it in words instead.
        var visible = cut.Find(visibleCountSelector);
        Assert.Equal("5 / 100", visible.TextContent);
        Assert.Equal("true", visible.GetAttribute("aria-hidden"));

        var spoken = cut.Find("#count-Text");
        Assert.Contains("edit-sr-only", spoken.ClassList);
        Assert.Equal("5 of 100 characters", spoken.TextContent);

        // ...and the field actually points at it, so it is read on focus rather than only stumbled
        // over in browse mode.
        Assert.Contains("count-Text", DescribedBy(cut, editorSelector));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void Without_a_max_length_the_spoken_count_is_just_the_length(
        string host, string editorSelector, string visibleCountSelector)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field);

        Assert.Equal("5", cut.Find(visibleCountSelector).TextContent);
        Assert.Equal("5 characters", cut.Find("#count-Text").TextContent);
        Assert.Contains("count-Text", DescribedBy(cut, editorSelector));
    }

    [Theory]
    [MemberData(nameof(Hosts))]
    public void A_field_without_ShowCount_keeps_a_byte_identical_describedby(
        string host, string editorSelector, string visibleCountSelector)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field, showCount: false, maxLength: 100);

        Assert.Equal("error-msg-Text", cut.Find(editorSelector).GetAttribute("aria-describedby"));
        Assert.Empty(cut.FindAll(visibleCountSelector));
        Assert.DoesNotContain("count-Text", cut.Markup);
    }

    [Theory]
    [MemberData(nameof(HostEditors))]
    public void The_count_token_sits_after_the_description_and_never_dangles(string host, string editorSelector)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field, maxLength: 100, description: "Your display name");

        // Order is the reading order: the field's own instructions first, the counter last.
        Assert.Equal("error-msg-Text desc-Text count-Text", cut.Find(editorSelector).GetAttribute("aria-describedby"));
        // Every token has to resolve to something rendered.
        foreach (var token in DescribedBy(cut, editorSelector))
            Assert.NotNull(cut.Find("#" + token));
    }

    [Theory]
    [MemberData(nameof(HostNames))]
    public void Read_only_mode_drops_the_count_token_because_no_count_renders(string host)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field, maxLength: 100, isEditMode: false);

        // The counter lives in EditInputShell, which the read-only views don't render at all -- a
        // count- token anywhere here would point at nothing.
        Assert.DoesNotContain("count-Text", cut.Markup);
    }

    [Theory]
    [MemberData(nameof(HostNames))]
    public void The_limit_status_region_exists_from_the_start_but_stays_silent_far_from_the_limit(string host)
    {
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = RenderHost(host, model, field, maxLength: 100);

        // A live region has to be in the DOM before its content changes, or the announcement that
        // matters -- the one at the limit -- is the one that gets missed.
        var status = cut.Find("span[role=\"status\"]");
        Assert.Contains("edit-sr-only", status.ClassList);
        Assert.Equal("", status.TextContent);
    }

    [Theory]
    [MemberData(nameof(HostNames))]
    public void The_limit_status_speaks_inside_the_last_stretch_and_at_the_limit(string host)
    {
        var model = new UnconstrainedModel();
        Expression<Func<string?>> field = () => model.Text;

        string Status(int length)
        {
            model.Text = new string('x', length);
            return RenderHost(host, model, field, maxLength: 100).Find("span[role=\"status\"]").TextContent;
        }

        Assert.Equal("", Status(89));                            // 11 remaining -- one past the window
        Assert.Equal("10 characters remaining", Status(90));     // exactly at the window's edge
        Assert.Equal("1 character remaining", Status(99));
        // maxlength silently truncates a paste; this is the only signal that anything was dropped.
        Assert.Equal("Character limit reached", Status(100));
    }

    [Fact]
    public void The_warning_window_scales_down_for_a_short_field()
    {
        // Ten characters is a useful warning distance for a long field but HALF of a twenty-character
        // one -- the window is min(10, 10% of the max), floor 1, so a short field doesn't nag from the
        // moment it is half full.
        Assert.Null(EditInputShell.BuildCountLimitStatus(true, 15, 20));   // 5 remaining, window is 2
        Assert.Equal("2 characters remaining", EditInputShell.BuildCountLimitStatus(true, 18, 20));
        Assert.Equal("1 character remaining", EditInputShell.BuildCountLimitStatus(true, 4, 5)); // floor of 1
        Assert.Null(EditInputShell.BuildCountLimitStatus(true, 3, 5));
        // ...and it caps at ten for a very long field rather than growing to 10% of it.
        Assert.Null(EditInputShell.BuildCountLimitStatus(true, 900, 1000));
        Assert.Equal("10 characters remaining", EditInputShell.BuildCountLimitStatus(true, 990, 1000));
    }

    [Fact]
    public void The_count_helpers_answer_nothing_when_the_counter_is_off_or_unbounded()
    {
        Assert.Null(EditInputShell.BuildCountAccessibleText(false, 5, 100));
        Assert.Null(EditInputShell.BuildCountLimitStatus(false, 99, 100));
        Assert.Null(EditInputShell.BuildCountLimitStatus(true, 5, null));  // no limit to approach
        Assert.Null(EditInputShell.BuildCountLimitStatus(true, 5, 0));     // degenerate max
        Assert.Equal("1 character", EditInputShell.BuildCountAccessibleText(true, 1, null));
        Assert.Equal("0 characters", EditInputShell.BuildCountAccessibleText(true, 0, null));
    }

    [Fact]
    public void A_shell_used_without_a_CountId_renders_the_visible_count_alone()
    {
        // The AT spans are gated on the id, not on CountText: with nothing for an aria-describedby to
        // point at, an unreferenced sr-only span would only add browse-mode noise.
        var cut = Render<EditInputShell>(p => p
            .Add(s => s.CountText, "3 / 10")
            .AddChildContent("<input />"));

        Assert.Equal("3 / 10", cut.Find(".edit-input-count").TextContent);
        Assert.Empty(cut.FindAll(".edit-sr-only"));
        Assert.Empty(cut.FindAll("[role=\"status\"]"));
    }

    [Fact]
    public void The_spoken_count_follows_typing_under_a_commit_on_blur_binding()
    {
        // Same live-text path the visible count uses (EditTextInputBase.EditorText): under
        // UpdateOn=Change the bound value doesn't move until blur, and a counter AT reads as stale is
        // worse than none.
        var model = new UnconstrainedModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.AddAttribute(5, "MaxLength", 100);
            b.AddAttribute(6, "UpdateOn", UpdateTrigger.Change);
            b.CloseComponent();
        }));

        Assert.Equal("5 of 100 characters", cut.Find("#count-Text").TextContent);

        cut.Find("input.edit-string-input").Input("Alicia");

        Assert.Equal("6 of 100 characters", cut.Find("#count-Text").TextContent);
        Assert.Equal("Alice", model.Text); // ...while the bound value still waits for blur
    }

    [Fact]
    public void A_model_StringLength_supplies_the_max_the_spoken_count_reports()
    {
        // The count's max resolves through EffectiveMaxLength, so the model attribute feeds the spoken
        // text and the limit warning exactly as the MaxLength parameter does.
        var model = new PersonModel { Name = new string('x', 95) }; // [StringLength(100, MinimumLength = 2)]
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "ShowCount", true);
            b.CloseComponent();
        }));

        Assert.Equal("95 of 100 characters", cut.Find("#count-Name").TextContent);
        Assert.Equal("5 characters remaining", cut.Find("span[role=\"status\"]").TextContent);
    }

    [Fact]
    public void EditControlInit_only_emits_the_count_token_when_asked()
    {
        // The default keeps every non-counting control's describedby byte-identical.
        Assert.Equal("error-msg-Name", EditControlInit.BuildDescribedBy("Name", false, false));
        Assert.Equal("error-msg-Name count-Name", EditControlInit.BuildDescribedBy("Name", false, false, true));
        Assert.Equal("error-msg-Name desc-Name tooltip-Name count-Name",
            EditControlInit.BuildDescribedBy("Name", true, true, true));
    }
}
