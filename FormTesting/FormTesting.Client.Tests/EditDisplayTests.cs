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
