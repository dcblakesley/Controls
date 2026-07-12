namespace FormTesting.Client.Tests;

/// <summary>
/// Smoke tests for EditDisplay, the read-only display control (no prior coverage). Confirms it renders
/// its label + text, honors IsHidden, and — like ReadOnlyValue — is not announced as an editable textbox.
/// </summary>
public class EditDisplayTests : TestContext
{
    [Fact]
    public void EditDisplay_renders_label_and_text()
    {
        var cut = RenderComponent<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Contains("Volume", cut.Find("label.edit-label").TextContent);
        Assert.Contains("15.3 oz", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditDisplay_hidden_renders_nothing()
    {
        var cut = RenderComponent<EditDisplay>(p => p
            .Add(d => d.Text, "x")
            .Add(d => d.IsHidden, true));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void EditDisplay_renders_the_tooltip_when_set()
    {
        // Tooltip is a documented EditDisplay parameter; it must reach the label, not be discarded.
        var cut = RenderComponent<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Tooltip, "ounces per can")
            .Add(d => d.Text, "15.3 oz"));

        Assert.NotNull(cut.Find(".edit-tooltip-container"));
        Assert.Contains("ounces per can", cut.Find("[role=tooltip]").TextContent);
    }

    [Fact]
    public void EditDisplay_renders_the_required_star_when_IsRequired()
    {
        var cut = RenderComponent<EditDisplay>(p => p
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
        var cut = RenderComponent<EditDisplay>(p => p
            .AddCascadingValue(new FormOptions { IsLabelHidden = true })
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        Assert.Empty(cut.FindAll("label.edit-label"));
        Assert.Contains("Volume", cut.Find("label.edit-sr-only").TextContent);
    }

    [Fact]
    public void EditDisplay_id_composes_group_name_and_IdPrefix_like_bound_controls()
    {
        var cut = RenderComponent<EditDisplay>(p => p
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
        var cut = RenderComponent<EditDisplay>(p => p
            .Add(d => d.Label, "Volume")
            .Add(d => d.Text, "15.3 oz"));

        var value = cut.Find(".edit-readonly-value");
        Assert.False(value.HasAttribute("role"));
        Assert.False(value.HasAttribute("tabindex"));
    }
}
