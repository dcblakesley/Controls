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

    [Fact]
    public async Task AutoSize_with_UpdateOn_Change_grows_while_typing_but_commits_value_only_on_blur()
    {
        // DemoEditTextArea's "AutoSize + UpdateOn=Change" section: AutoSize="true" MinRows="2"
        // MaxRows="6" UpdateOn="UpdateTrigger.Change". Change's bound event is onchange (blur/Enter
        // only), but EditTextArea.razor.cs's AutoSizeInputAttribute splats an extra measure-only
        // oninput handler in exactly this combination, so the box must still grow mid-typing even
        // though the bound value (echoed via the "Display bound values" .bound-value div) does not
        // commit until blur.
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var section = Page.Locator("section.demo-section").Filter(new() { HasTextString = "AutoSize + UpdateOn=Change" });
        var textarea = section.Locator("textarea").First;
        var boundValue = section.Locator(".bound-value").First;

        var initialBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(initialBox);

        await textarea.FillAsync("line one\nline two\nline three\nline four");

        // Growth must happen from the measure-only oninput handler alone, before any blur -- poll
        // rather than sleep a fixed duration (async JS interop round-trip, plus this repo's
        // smooth-scroll-driven geometry pitfall if a fixed wait were used instead).
        await WaitForHeightAboveAsync(textarea, initialBox.Height);

        // Not committed yet: UpdateOn=Change's bound event is onchange, which a textarea only raises
        // on blur (Enter just inserts a newline in a multi-line field, unlike a single-line input).
        await Expect(boundValue).Not.ToContainTextAsync("line one");

        await textarea.BlurAsync();
        await Expect(boundValue).ToContainTextAsync("line one");
    }

    [Fact]
    public async Task AutoSize_with_default_UpdateOn_Input_still_grows_and_commits_value_before_any_blur()
    {
        // Regression guard for the UpdateOn feature: AutoSizeInputAttribute only splats its extra
        // oninput handler once the resolved trigger has moved away from "oninput" (see
        // EditTextArea.razor.cs). Under the default Input trigger it must stay null, so this
        // control's pre-existing behavior -- grows AND commits its bound value on every keystroke,
        // with no blur required -- must be unchanged. Targets DemoEditTextArea's last section
        // (plain AutoSize="true" MinRows="2" MaxRows="6", no UpdateOn set), the same one
        // AutoSize_grows_with_content_and_stops_growing_past_MaxRows drives.
        await NavigateAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Display bound values" }).ClickAsync();

        var section = Page.Locator("section.demo-section").Last;
        var textarea = section.Locator("textarea").First;
        var boundValue = section.Locator(".bound-value").First;

        var initialBox = await textarea.BoundingBoxAsync();
        Assert.NotNull(initialBox);

        await textarea.FillAsync("line one\nline two\nline three\nline four");

        await WaitForHeightAboveAsync(textarea, initialBox.Height);
        // Unlike UpdateOn=Change, the default Input trigger's bound event IS oninput -- the value
        // commits (and this .bound-value echo updates) immediately, with no blur required.
        await Expect(boundValue).ToContainTextAsync("line one");
    }

    /// <summary>
    /// Polls <paramref name="locator"/>'s bounding-box height until it exceeds <paramref name="thresholdPx"/>
    /// or <paramref name="timeoutMs"/> elapses. Preferred over a fixed <c>WaitForTimeoutAsync</c> sleep
    /// for asserting AutoSize growth: it returns as soon as the JS resize round-trip lands instead of
    /// racing a magic-number delay, and still fails loudly (via <see cref="Assert.Fail(string)"/>) if
    /// the height never moves.
    /// </summary>
    static async Task WaitForHeightAboveAsync(ILocator locator, float thresholdPx, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        LocatorBoundingBoxResult? box = null;
        while (DateTime.UtcNow < deadline)
        {
            box = await locator.BoundingBoxAsync();
            if (box != null && box.Height > thresholdPx) return;
            await Task.Delay(50);
        }
        Assert.Fail($"height ({box?.Height.ToString() ?? "null"}px) never grew past {thresholdPx}px within {timeoutMs}ms");
    }
}
