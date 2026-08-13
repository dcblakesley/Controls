namespace FormTesting.Client.Tests;

/// <summary>
/// Smoke tests for EditDisplay, the read-only display control (no prior coverage). Confirms it renders
/// its label + text, honors IsHidden, and — like ReadOnlyValue — is not announced as an editable textbox.
/// </summary>
public class EditDisplayTests : BunitContext
{
    [Fact]
    public void EditDisplay_renders_label_and_text()
    {
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Contains("Volume", cut.Find("label.edit-label").TextContent);
        Assert.Contains("15.3 oz", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditDisplay_hidden_renders_nothing()
    {
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Text, "x")
            .Add(d => d.IsHidden, true));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void EditDisplay_renders_the_tooltip_when_set()
    {
        // Tooltip is a documented EditDisplay parameter; it must reach the label, not be discarded.
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Tooltip, "ounces per can")
            .Add(d => d.Text, "15.3 oz"));

        Assert.NotNull(cut.Find(".edit-tooltip-container"));
        Assert.Contains("ounces per can", cut.Find("[role=tooltip]").TextContent);
    }

    [Fact]
    public void EditDisplay_renders_the_required_star_when_IsRequired()
    {
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.IsRequired, true)
            .Add(d => d.Text, "15.3 oz"));

        Assert.NotNull(cut.Find(".edit-label-required-star"));
    }

    [Fact]
    public void EditDisplay_honors_the_cascaded_FormOptions_IsLabelHidden()
    {
        // The cascaded FormOptions used to be declared but ignored — a form-wide label-hidden
        // setting must reach EditDisplay like every other control (sr-only label, not a visible one).
        var cut = Render<EditDisplay>(p => p
            .AddCascadingValue(new FormOptions { IsLabelHidden = true })
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Empty(cut.FindAll("label.edit-label"));
        Assert.Contains("Volume", cut.Find("label.edit-sr-only").TextContent);
    }

    [Fact]
    public void EditDisplay_applies_the_Class_parameter_to_the_value_element()
    {
        // Class was a documented parameter that the markup never rendered.
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Class, "highlight")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Contains("highlight", cut.Find(".edit-readonly-value").ClassList);
    }

    [Fact]
    public void EditDisplay_id_composes_group_name_and_IdPrefix_like_bound_controls()
    {
        var cut = Render<EditDisplay>(p => p
            .AddCascadingValue(new FormGroupOptions { Name = "shipping" })
            .Add(d => d.IdPrefix, "row1")
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        var id = cut.Find(".edit-readonly-value").GetAttribute("id");
        Assert.Equal("row1-shipping-Volume", id);
    }

    [Fact]
    public void EditDisplay_value_is_not_an_editable_textbox()
    {
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        var value = cut.Find(".edit-readonly-value");
        // role="group" (R5) legitimizes the aria-labelledby a roleless div can't reliably carry -- this
        // assertion's real intent (not an editable field) is preserved by asserting "group", not
        // "textbox" or absent, rather than by asserting no role at all.
        Assert.Equal("group", value.GetAttribute("role"));
        Assert.False(value.HasAttribute("tabindex"));
    }

    [Fact]
    public void EditDisplay_merges_a_consumer_class_and_splats_other_attributes_onto_the_value_element()
    {
        // Unmatched attributes used to throw InvalidOperationException; per the library owner's
        // decision, class merges with the component's own and the rest splat onto the value element.
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Text, "15.3 oz")
            .AddUnmatched("class", "consumer-class")
            .AddUnmatched("data-testid", "volume"));

        var value = cut.Find(".edit-readonly-value");
        Assert.Contains("consumer-class", value.ClassList);
        Assert.Contains("edit-readonly-value", value.ClassList); // merged, not replaced
        Assert.Equal("volume", value.GetAttribute("data-testid"));
    }

    [Fact]
    public void EditDisplay_Class_parameter_composes_with_splatted_style_and_data_attributes()
    {
        // Class + unmatched class can never compose (case-insensitive parameter matching binds a
        // consumer's class= to the Class parameter — same knob), but Class alongside splatted
        // style/data-* must all land on the value element together.
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Class, "highlight")
            .Add(d => d.Text, "15.3 oz")
            .AddUnmatched("style", "margin-top:4px")
            .AddUnmatched("data-testid", "volume"));

        var value = cut.Find(".edit-readonly-value");
        Assert.Contains("highlight", value.ClassList);
        Assert.Equal("margin-top:4px", value.GetAttribute("style"));
        Assert.Equal("volume", value.GetAttribute("data-testid"));
    }

    [Fact]
    public void EditDisplay_with_no_text_renders_an_accessible_fallback_that_still_reserves_a_line()
    {
        // EditDisplay hand-builds its read-only div instead of using ReadOnlyValue, and shares that
        // component's fallback-placeholder contract (LST-2): the old placeholder was BOTH aria-hidden
        // AND visibility:hidden, reaching neither sighted users nor assistive technology -- a
        // screen-reader user heard the label and then silence. EmptyText is now real, visible text
        // (reaching everyone) that reserves the row's line-height simply by being a real text node, with
        // no visibility trick needed.
        var cut = Render<EditDisplay>(p => p.Add(d => d.Label, "Volume"));

        var placeholder = cut.Find(".edit-readonly-value span");
        Assert.False(placeholder.HasAttribute("aria-hidden"));
        Assert.Equal("Not Set", placeholder.TextContent);
    }

    [Fact]
    public void EditDisplay_EmptyText_parameter_overrides_the_default_fallback()
    {
        var cut = Render<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.EmptyText, "None recorded"));

        var placeholder = cut.Find(".edit-readonly-value span");
        Assert.Equal("None recorded", placeholder.TextContent);
    }

    [Fact]
    public void EditDisplay_with_text_renders_no_placeholder()
    {
        var cut = Render<EditDisplay>(p => p.Add(d => d.Text, "15.3 oz"));

        Assert.Empty(cut.FindAll(".edit-readonly-value span"));
        Assert.Equal("15.3 oz", cut.Find(".edit-readonly-value").TextContent.Trim());
    }
}
