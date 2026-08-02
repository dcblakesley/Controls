using Controls.Demo;

namespace FormTesting.Client.E2ETests;

public class EditSelectSearchE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.SelectSearch;

    [Fact]
    public async Task First_select_renders_search_input()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await Expect(select).ToBeVisibleAsync();
        await Expect(select.Locator("input.wss-select-selection-search-input")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Opening_dropdown_shows_options()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Enter_picks_an_option_without_submitting_the_enclosing_form()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section", new() { HasTextString = "Inside a submitting form" });
        var select = section.Locator(".wss-select").First;

        // Open, highlight an option, and commit it with Enter — the browser's implicit form
        // submission used to fire alongside the selection.
        await select.ClickAsync();
        var input = select.Locator("input.wss-select-selection-search-input");
        await input.PressAsync("ArrowDown");
        await input.PressAsync("Enter");

        await Expect(select.Locator(".wss-select-selection-item")).Not.ToBeEmptyAsync(); // something got picked
        await Expect(section.Locator(".submit-count")).ToHaveTextAsync("Submits: 0");

        // Sanity: the button still submits.
        await section.GetByRole(AriaRole.Button, new() { Name = "Submit" }).ClickAsync();
        await Expect(section.Locator(".submit-count")).ToHaveTextAsync("Submits: 1");
    }

    [Fact]
    public async Task Escape_closes_only_the_open_dropdown()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();

        await select.Locator("input.wss-select-selection-search-input").PressAsync("Escape");
        await Expect(Page.Locator(".wss-select-item-option")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Tab_away_closes_the_dropdown_and_frees_the_page()
    {
        await NavigateAsync();
        var select = Page.Locator("section.demo-section").First.Locator(".wss-select").First;
        await select.ClickAsync();
        await Expect(Page.Locator(".wss-select-item-option").First).ToBeVisibleAsync();

        // First Tab lands on the clear button (still inside the select — the dropdown correctly
        // stays open); the second leaves the control, which must close the dropdown and its
        // click-swallowing backdrop.
        await select.Locator("input.wss-select-selection-search-input").PressAsync("Tab");
        await Page.Keyboard.PressAsync("Tab");
        await Expect(Page.Locator(".wss-select-item-option")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".wss-select-backdrop")).ToHaveCountAsync(0);
    }

    // ----- Controlled Open (bUnit can't run JS, so the actual placement side effect of an
    // externally-driven open is only provable here) -----------------------------------------------

    [Fact]
    public async Task Controlled_Open_button_opens_the_dropdown_and_JS_positions_it()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section", new() { HasTextString = "Controlled Open" });
        var dropdown = section.Locator(".wss-select-dropdown");
        var stateDiv = section.Locator(".controlled-open-state");

        await Expect(dropdown).ToHaveCountAsync(0);
        await Expect(stateDiv).ToHaveTextAsync("Open: False");

        await section.GetByRole(AriaRole.Button, new() { Name = "Open externally" }).ClickAsync();

        // Visible AND positioned (not stuck at wss-measuring) proves placeDropdown's JS ran on the
        // externally-driven open, not just that _open flipped.
        await WaitForOpenAndPositionedAsync(dropdown);
        await Expect(stateDiv).ToHaveTextAsync("Open: True");

        // Close externally too -- the round trip back through OpenChanged updates the demo's state div.
        // DispatchEventAsync (not ClickAsync): the open dropdown's full-viewport backdrop legitimately
        // covers every other element while open (that's what makes an outside click close it), so a
        // real position-based click can never reach this button until the dropdown is already closed.
        // Dispatching the click event directly still exercises the exact same Blazor @onclick handler.
        await section.GetByRole(AriaRole.Button, new() { Name = "Close externally" }).DispatchEventAsync("click");
        await Expect(dropdown).ToHaveCountAsync(0);
        await Expect(stateDiv).ToHaveTextAsync("Open: False");
    }

    // ----- JS horizontal clamp (bUnit can't run JS/measure layout) ---------------------------------

    [Fact]
    public async Task Horizontal_clamp_keeps_a_wide_dropdown_on_screen_near_the_right_viewport_edge()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section", new() { HasTextString = "JS horizontal clamp" });
        var select = section.Locator(".wss-select").First;
        var dropdown = section.Locator(".wss-select-dropdown");

        // Force the (normally document-flow-positioned) trigger near the right edge of the fixed
        // 1280px viewport -- its long-labeled options make the dropdown far wider than the 90px
        // trigger, so without the clamp it would run off-screen from this position.
        await select.EvaluateAsync("el => { el.style.position = 'fixed'; el.style.left = (window.innerWidth - 100) + 'px'; el.style.top = '300px'; }");

        await select.ClickAsync();
        await WaitForOpenAndPositionedAsync(dropdown);

        var box = await dropdown.BoundingBoxAsync();
        Assert.NotNull(box);
        var viewportWidth = Page.ViewportSize!.Width;
        Assert.True(box!.X + box.Width <= viewportWidth + 1,
            $"dropdown right edge ({box.X + box.Width}) ran past the viewport width ({viewportWidth})");
    }

    // ----- All-disabled + DefaultOpen (bUnit covers the initial render's DOM state -- see
    // SelectHighlightAndFocusTests.DefaultOpen_on_an_all_disabled_list_leaves_no_active_descendant --
    // but not real keyboard events in a live browser) -------------------------------------------------

    [Fact]
    public async Task All_disabled_default_open_select_has_no_active_descendant_and_ignores_arrow_and_enter()
    {
        await NavigateAsync();
        var section = Page.Locator("section.demo-section", new() { HasTextString = "All options disabled + DefaultOpen" });

        // The select isn't rendered at all until toggled -- DefaultOpen must be true on the very
        // first render of a fresh instance, so this can't be simulated by opening an already-rendered
        // control (see the demo's comment on why it isn't just always on).
        await section.Locator("[data-test-id=toggle-all-disabled-open-select]").CheckAsync();

        var select = section.Locator(".wss-select");
        var input = select.Locator("input.wss-select-selection-search-input");
        await Expect(section.Locator(".wss-select-item-option").First).ToBeVisibleAsync(); // opened by default

        Assert.Null(await input.GetAttributeAsync("aria-activedescendant"));

        await input.PressAsync("ArrowDown");
        Assert.Null(await input.GetAttributeAsync("aria-activedescendant"));

        await input.PressAsync("ArrowUp");
        Assert.Null(await input.GetAttributeAsync("aria-activedescendant"));

        await input.PressAsync("Enter");
        Assert.Null(await input.GetAttributeAsync("aria-activedescendant"));

        // Nothing committed: the trigger still shows the placeholder, not a selection chip, and the
        // dropdown is still open (Enter had no active row to commit, so it's a true no-op, not a
        // close-without-selecting).
        await Expect(select.Locator(".wss-select-selection-item")).ToHaveCountAsync(0);
        await Expect(select.Locator(".wss-select-selection-placeholder")).ToBeVisibleAsync();
        await Expect(section.Locator(".wss-select-item-option").First).ToBeVisibleAsync();
    }
}
