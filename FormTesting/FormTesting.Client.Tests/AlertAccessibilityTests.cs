namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for the <see cref="Alert"/> accessibility fixes from the 2026-08-11 UI-kit a11y
/// audit: a screen-reader-only severity word ahead of the message, the <see cref="Alert.Live"/>
/// live-region opt-out, and the localizable <see cref="Alert.CloseButtonLabel"/>.
/// </summary>
/// <remarks>
/// A new file rather than an addition to <c>UiKitLeafControlsTests.cs</c>: that file is shared with
/// other UI-kit leaf controls (Skeleton, Tooltip, Popover, Pagination) that are not part of this
/// change, so Alert coverage grows here instead of risking a concurrent edit collision. The existing
/// <c>Alert_role_and_aria_live_match_severity</c> test there is untouched by these changes — it
/// covers the <see cref="Alert.Live"/> default (true), which preserves the prior unconditional
/// behavior.
/// </remarks>
public class AlertAccessibilityTests : BunitContext
{
    [Theory]
    [InlineData(AlertType.Success, "Success: ")]
    [InlineData(AlertType.Info, "Info: ")]
    [InlineData(AlertType.Warning, "Warning: ")]
    [InlineData(AlertType.Error, "Error: ")]
    public void Alert_emits_a_screen_reader_only_severity_word_before_the_message(AlertType type, string expected)
    {
        var cut = Render<Alert>(p => p.Add(a => a.Type, type).Add(a => a.Message, "Something happened"));

        var srSpan = cut.Find(".wss-alert-message .wss-sr-only");
        Assert.Equal(expected, srSpan.TextContent);
        Assert.Contains("Something happened", cut.Find(".wss-alert-message").TextContent);
        // The icon remains the only OTHER severity signal, and stays decorative.
        Assert.Equal("true", cut.Find(".wss-alert-icon svg").GetAttribute("aria-hidden"));
    }

    [Fact]
    public void Alert_severity_word_precedes_MessageContent_too()
    {
        var cut = Render<Alert>(p => p
            .Add(a => a.Type, AlertType.Warning)
            .Add(a => a.MessageContent, b => b.AddContent(0, "Rich message")));

        var message = cut.Find(".wss-alert-message");
        Assert.StartsWith("Warning: ", message.TextContent);
        Assert.Contains("Rich message", message.TextContent);
    }

    [Fact]
    public void Alert_Live_false_renders_a_group_with_no_live_region_semantics()
    {
        var cut = Render<Alert>(p => p
            .Add(a => a.Type, AlertType.Error) // even Error's normally-assertive role is suppressed
            .Add(a => a.Message, "x")
            .Add(a => a.Live, false));

        var root = cut.Find(".wss-alert");
        Assert.Equal("group", root.GetAttribute("role"));
        Assert.Null(root.GetAttribute("aria-live"));
    }

    [Fact]
    public void Alert_Live_true_default_is_unchanged_from_the_prior_unconditional_behavior()
    {
        var cut = Render<Alert>(p => p.Add(a => a.Type, AlertType.Success).Add(a => a.Message, "x"));

        var root = cut.Find(".wss-alert");
        Assert.Equal("status", root.GetAttribute("role"));
        Assert.Equal("polite", root.GetAttribute("aria-live"));
    }

    [Fact]
    public void Alert_SeverityLabel_default_keeps_the_built_in_English_word()
    {
        var cut = Render<Alert>(p => p.Add(a => a.Type, AlertType.Warning).Add(a => a.Message, "x"));
        Assert.Equal("Warning: ", cut.Find(".wss-alert-message .wss-sr-only").TextContent);
    }

    [Fact]
    public void Alert_SeverityLabel_overrides_the_word_but_the_component_still_appends_the_separator()
    {
        var cut = Render<Alert>(p => p
            .Add(a => a.Type, AlertType.Warning)
            .Add(a => a.Message, "x")
            .Add(a => a.SeverityLabel, "Attention"));

        Assert.Equal("Attention: ", cut.Find(".wss-alert-message .wss-sr-only").TextContent);
    }

    [Fact]
    public void Alert_close_button_default_label_is_Close()
    {
        var cut = Render<Alert>(p => p.Add(a => a.Message, "x").Add(a => a.Closable, true));
        Assert.Equal("Close", cut.Find(".wss-alert-close").GetAttribute("aria-label"));
    }

    [Fact]
    public void Alert_CloseButtonLabel_overrides_the_default_on_both_close_button_layouts()
    {
        var plain = Render<Alert>(p => p
            .Add(a => a.Message, "x")
            .Add(a => a.Closable, true)
            .Add(a => a.CloseButtonLabel, "Dismiss"));
        Assert.Equal("Dismiss", plain.Find(".wss-alert-close").GetAttribute("aria-label"));

        // The Action-present layout renders a second, differently-nested close button (see
        // Alert.razor) -- the override must reach both.
        var withAction = Render<Alert>(p => p
            .Add(a => a.Message, "x")
            .Add(a => a.Closable, true)
            .Add(a => a.CloseButtonLabel, "Dismiss")
            .Add(a => a.Action, b => b.AddContent(0, "Undo")));
        Assert.Equal("Dismiss", withAction.Find(".wss-alert-actions .wss-alert-close").GetAttribute("aria-label"));
    }
}
