namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the round-3 Popover/Popconfirm trigger rework (M9 re-resolution, M11 render
/// guard, focusin repair, and L14 disabled-child aria). Drives the swapped-trigger demo section on
/// the /uikit gallery. Everything is scoped by <c>data-test-id</c> so these assertions target the
/// new section, not the page's other Popover/Popconfirm instances.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class PopoverTriggerE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public PopoverTriggerE2ETests(AppFixture app, BrowserFixture browser)
    {
        _app = app;
        _browser = browser;
    }

    public async Task InitializeAsync()
    {
        _context = await _browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            DeviceScaleFactor = 1,
        });
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
    }

    async Task GotoAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Swapped_trigger_child_regains_the_popup_aria_and_close_focus()
    {
        await GotoAsync();

        var toggle = _page.Locator("[data-test-id=swap-toggle]");
        var child = _page.Locator("[data-test-id=swap-trigger-child]");
        var content = _page.Locator("[data-test-id=swap-popover-content]");

        // Swap the trigger child span -> button. Each toggle re-creates the element; the C# render
        // guard skips syncTrigger while (open, disabled) is unchanged, so the fresh button starts
        // without popup ARIA — exactly the M9 "stale/detached child" scenario.
        await toggle.ClickAsync(); // -> loading <span>
        await toggle.ClickAsync(); // -> a brand-new <button>
        await Expect(child).ToHaveTextAsync("Open popover"); // wait for the swap render to settle
        Assert.Equal("BUTTON", await child.EvaluateAsync<string>("el => el.tagName"));

        // Focusing the child fires the wrapper's focusin listener, which re-resolves the live child
        // and re-applies the popup ARIA (the focusin repair path).
        await child.FocusAsync();
        await Expect(child).ToHaveAttributeAsync("aria-haspopup", "dialog");
        await Expect(child).ToHaveAttributeAsync("aria-expanded", "false");

        // The button's native Enter click bubbles to the wrapper and opens; aria-expanded tracks it.
        await _page.Keyboard.PressAsync("Enter");
        await Expect(content).ToBeVisibleAsync();
        await Expect(child).ToHaveAttributeAsync("aria-expanded", "true");

        // Escape closes, and focus is restored to the swapped-in child (not <body>).
        await _page.Keyboard.PressAsync("Escape");
        await Expect(content).Not.ToBeVisibleAsync();
        await Expect(child).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(child).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Disabled_popconfirm_marks_its_interactive_child_aria_disabled()
    {
        await GotoAsync();

        // L14: a disabled Popconfirm with a <button> child must mark the child aria-disabled and drop
        // the popup ARIA, so assistive tech announces it as inert (it used to look live but do nothing).
        var button = _page.Locator("[data-test-id=disabled-popconfirm-child]");
        await Expect(button).ToHaveAttributeAsync("aria-disabled", "true");
        Assert.Null(await button.GetAttributeAsync("aria-haspopup"));
    }
}
