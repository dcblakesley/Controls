namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the demo host's shell at <c>/</c>: the two top-level tabs (form controls / UI
/// kit), each tab's own sidebar, and the <c>tab</c>/<c>view</c> query parameters that deep-link
/// them. Drives <see cref="IPage"/> directly rather than subclassing <see cref="PageTestBase"/>,
/// which targets one <c>CurrentView</c> per class.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class DemoShellE2ETests : IAsyncLifetime
{
    // Every section the /uikit composition renders. Pinned so splitting or dropping one of the
    // per-component demos it composes can't silently shrink the standalone gallery.
    const int UiKitSectionCount = 35;

    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public DemoShellE2ETests(AppFixture app, BrowserFixture browser)
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

    // The strip's Id roots the ARIA wiring, so each tab button has a stable id of its own.
    ILocator FormsTab => _page.Locator("#demo-shell-tabs-tab-forms");
    ILocator UiKitTab => _page.Locator("#demo-shell-tabs-tab-uikit");

    async Task GotoAsync(string path)
    {
        await _page.GotoAsync($"{_app.BaseUrl}{path}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(FormsTab.Or(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" })))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    [Fact]
    public async Task Root_opens_on_the_form_controls_tab_with_its_sidebar()
    {
        await GotoAsync("/");

        await Expect(FormsTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(UiKitTab).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "AllControls", Exact = true })).ToBeVisibleAsync();
        // The UI-kit sidebar belongs to the other pane, which isn't rendered.
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Skeleton", Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Clicking_the_UI_Kit_tab_swaps_in_its_sidebar_and_records_the_tab_in_the_url()
    {
        await GotoAsync("/");
        await UiKitTab.ClickAsync();

        await Expect(UiKitTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "All", Exact = true })).ToBeVisibleAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Table", Exact = true })).ToBeVisibleAsync();
        Assert.Contains("tab=uikit", _page.Url);
        // Switching tabs drops `view`: it names a member of the outgoing tab's enum.
        Assert.DoesNotContain("view=", _page.Url);
    }

    [Fact]
    public async Task Ui_kit_view_deep_link_renders_that_component_alone()
    {
        await GotoAsync("/?tab=uikit&view=Table");

        await Expect(UiKitTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(_page.Locator("h1", new() { HasTextString = "Table Demo" })).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-alert")).ToHaveCountAsync(0); // a different demo's sections stay out
    }

    [Fact]
    public async Task Existing_form_view_deep_links_are_unchanged_by_the_tabs()
    {
        await GotoAsync("/?view=String");

        await Expect(FormsTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(_page.Locator("h1", new() { HasTextString = "EditString Demo" })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Standalone_gallery_route_still_renders_every_section()
    {
        await GotoAsync("/uikit");

        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" })).ToBeVisibleAsync();
        await Expect(_page.Locator("section.demo-section")).ToHaveCountAsync(UiKitSectionCount);
        await Expect(FormsTab).ToHaveCountAsync(0); // the standalone route has no shell around it
    }

    [Fact]
    public async Task Arrow_key_moves_and_activates_the_next_tab()
    {
        await GotoAsync("/");
        await FormsTab.FocusAsync();

        // Tabs follows the ARIA pattern with AUTOMATIC activation, so the arrow both moves focus and
        // selects -- no Enter needed (see Tabs' class remarks).
        await _page.Keyboard.PressAsync("ArrowRight");

        await Expect(UiKitTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(UiKitTab).ToBeFocusedAsync();
        await Expect(_page.GetByRole(AriaRole.Button, new() { Name = "All", Exact = true })).ToBeVisibleAsync();
        Assert.Contains("tab=uikit", _page.Url);
    }
}
