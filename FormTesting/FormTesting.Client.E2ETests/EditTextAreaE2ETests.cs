using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditTextAreaE2ETests(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.TextArea;

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = "EditTextArea Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Textarea_accepts_multiline_text_and_round_trips_to_bound_display()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var input = Page.Locator("section.demo-section").First.Locator("textarea").First;
        await input.FillAsync("line one\nline two");
        await input.PressAsync("Tab");

        await Expect(Page.Locator("section.demo-section").First.Locator(".bound-value").First)
            .ToContainTextAsync("line one");
    }

    [Fact]
    public async Task Toggling_FormOptions_edit_mode_swaps_inputs_for_ReadOnlyValue()
    {
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Edit Mode" }).ClickAsync();

        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection.Locator("textarea").First).Not.ToBeVisibleAsync();
        await Expect(firstSection.Locator(".edit-readonly-value").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Visual_baseline_basic_section()
    {
        await NavigateAsync();
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(firstSection, "basic-section");
    }

    [Fact]
    public async Task AutoSize_grows_with_content_and_stops_growing_past_MaxRows()
    {
        // DemoEditTextArea's "AutoSize" section (the last one): AutoSize="true" MinRows="2" MaxRows="6".
        await NavigateAsync();
        var section = Page.Locator("section.demo-section").Last;
        var textarea = section.Locator("textarea").First;

        var initialBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(initialBox);

        // A few lines should grow the box past its MinRows="2" starting height, and it must not have
        // scrolled yet (still under the MaxRows="6" clamp).
        await textarea.FillAsync("line one\nline two\nline three\nline four");
        await Page.WaitForTimeoutAsync(300); // let the JS resize (async interop round-trip) settle
        var grownBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(grownBox);
        Assert.True(grownBox.Height > initialBox.Height,
            $"height ({grownBox.Height}px) should have grown past the initial MinRows height ({initialBox.Height}px)");
        await Expect(textarea).ToHaveCSSAsync("overflow-y", "hidden");

        // Typing past MaxRows="6" grows the box up to the clamp (4 lines above was still under it)
        // and gains a scrollbar...
        await textarea.FillAsync(string.Join("\n", Enumerable.Range(1, 10).Select(i => $"line {i}")));
        await Page.WaitForTimeoutAsync(300);
        var clampedBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(clampedBox);
        await Expect(textarea).ToHaveCSSAsync("overflow-y", "auto");
        Assert.True(clampedBox.Height > grownBox.Height,
            $"height ({clampedBox.Height}px) should have grown to the MaxRows clamp (was {grownBox.Height}px at 4 lines)");

        // ...and further content past the clamp must not grow it any more.
        await textarea.FillAsync(string.Join("\n", Enumerable.Range(1, 20).Select(i => $"line {i}")));
        await Page.WaitForTimeoutAsync(300);
        var stillClampedBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(stillClampedBox);
        await Expect(textarea).ToHaveCSSAsync("overflow-y", "auto");
        Assert.True(stillClampedBox.Height <= clampedBox.Height + 2, // +2px slack for sub-pixel rounding
            $"height ({stillClampedBox.Height}px) should have stopped growing once MaxRows was exceeded (was {clampedBox.Height}px)");
    }
}
