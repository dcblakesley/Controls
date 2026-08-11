using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the two JS-only accessibility behaviors of the overlay stack, both of which bUnit
/// cannot observe (no JS runtime): the <c>inert</c> background a Modal/Drawer applies while it is open
/// (audit M7 — <c>aria-modal</c> alone leaves a screen reader's virtual cursor free to read and
/// activate the page behind the dialog), and the <c>aria-controls</c> a Popover/Popconfirm trigger
/// gains while its panel is open (audit OVR-7). Drives the /uikit gallery, plus two direct
/// <c>wss-overlay.js</c> module calls for the stacked case the gallery has no demo for.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class DialogBackgroundA11yE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public DialogBackgroundA11yE2ETests(AppFixture app, BrowserFixture browser)
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

    // True when the first match for `selector` is itself inert or sits inside an inert subtree.
    // closest() covers both, which is what matters: the JS marks whole branches, not leaves.
    Task<bool> IsInertAsync(string selector) => _page.EvaluateAsync<bool>(
        "s => { const el = document.querySelector(s); return !!el && !!el.closest('[inert]'); }", selector);

    // How many matches for `selector` are inert (or inside an inert branch) — for the toast layers,
    // where every instance on the page must stay live, not just the first.
    Task<int> InertCountAsync(string selector) => _page.EvaluateAsync<int>(
        "s => [...document.querySelectorAll(s)].filter(el => el.closest('[inert]')).length", selector);

    Task WaitForDialogFocusAsync(string dialogSelector) => _page.WaitForFunctionAsync(
        $"() => {{ const d = document.querySelector('{dialogSelector}'); return !!d && d.contains(document.activeElement); }}");

    // Polled, not a one-shot read: closing removes the dialog from the DOM in the Blazor render batch
    // that PRECEDES the interop call releasing the JS handle, so there is a brief window where the
    // dialog is already gone but the background is still marked.
    Task WaitForNotInertAsync(string selector) => _page.WaitForFunctionAsync(
        "s => { const el = document.querySelector(s); return !!el && !el.closest('[inert]'); }", selector);

    [Fact]
    public async Task Open_modal_inerts_the_background_and_closing_restores_it()
    {
        await GotoAsync();
        Assert.False(await IsInertAsync("h1")); // baseline: nothing inert before any dialog opens

        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();
        // The inert application and the initial focus grab happen in the same activateModal call, so
        // waiting for focus to land inside the dialog also means the marking has run.
        await WaitForDialogFocusAsync(".wss-modal[role=dialog]");

        // Background: marked all the way up the ancestor chain (the page heading is several levels
        // above the dialog's own section).
        Assert.True(await IsInertAsync("h1"));
        Assert.True(await IsInertAsync("[data-test-id=toggle-table-loading]"));
        // The dialog itself and its mask wrapper stay interactive.
        Assert.False(await IsInertAsync(".wss-modal[role=dialog]"));
        Assert.False(await IsInertAsync(".wss-modal-wrap"));

        // Behavioral cross-check: a background control can no longer take focus (inert removes it
        // from hit testing and the tab order), and focus stays where the trap put it.
        var focusStayedInDialog = await _page.EvaluateAsync<bool>(
            """
            () => {
                const bg = document.querySelector('[data-test-id=toggle-table-loading]');
                bg.focus();
                const dialog = document.querySelector('.wss-modal[role=dialog]');
                return document.activeElement !== bg && dialog.contains(document.activeElement);
            }
            """);
        Assert.True(focusStayedInDialog);

        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();
        await WaitForNotInertAsync("h1");

        Assert.False(await IsInertAsync("[data-test-id=toggle-table-loading]"));
        var backgroundFocusable = await _page.EvaluateAsync<bool>(
            """
            () => {
                const bg = document.querySelector('[data-test-id=toggle-table-loading]');
                bg.focus();
                return document.activeElement === bg;
            }
            """);
        Assert.True(backgroundFocusable);
    }

    [Fact]
    public async Task Open_modal_leaves_the_toast_layers_interactive()
    {
        // The toast containers deliberately paint ABOVE the dialog mask (z-index 5000) and are ARIA
        // live regions, so they are excluded from the inert sweep: inert would silence a message
        // raised while a dialog is open and make a notification's close button unclickable. The
        // gallery has three containers, one of them nested inside an otherwise-inert demo section --
        // which is also the regression guard for the "descend past a live branch" path.
        await GotoAsync();
        // Guard the "0 inert" assertions below against silently passing on an empty set: the static
        // toast hosts only render on the WebAssembly renderer (see UiKitGallery.StaticToastsSupported).
        await Expect(_page.Locator(".wss-msg-container")).ToHaveCountAsync(1);
        await Expect(_page.Locator(".wss-notification-container")).ToHaveCountAsync(2);

        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        await Expect(_page.Locator(".wss-modal[role=dialog]")).ToBeVisibleAsync();
        await WaitForDialogFocusAsync(".wss-modal[role=dialog]");

        Assert.True(await IsInertAsync("h1")); // ...while the rest of the page IS inert
        Assert.Equal(0, await InertCountAsync(".wss-msg-container"));
        Assert.Equal(0, await InertCountAsync(".wss-notification-container"));
    }

    [Fact]
    public async Task Open_drawer_inerts_the_background_but_keeps_its_own_mask_clickable()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Drawer" }).ClickAsync();
        var drawer = _page.Locator(".wss-drawer[role=dialog]");
        await Expect(drawer).ToBeVisibleAsync();
        await WaitForDialogFocusAsync(".wss-drawer[role=dialog]");

        Assert.True(await IsInertAsync("h1"));
        // .wss-drawer-root is the overlay's own root, and the mask lives INSIDE it -- inert-ing the
        // mask would silently kill click-outside-to-close (which is why the sweep is anchored on the
        // root, not on the panel).
        Assert.False(await IsInertAsync(".wss-drawer-root"));
        Assert.False(await IsInertAsync(".wss-drawer-mask"));

        await _page.Locator(".wss-drawer-mask").ClickAsync(new LocatorClickOptions
        {
            Position = new Position { X = 5, Y = 5 }, // far corner -- away from the drawer panel
        });
        await Expect(drawer).ToBeHiddenAsync();
        await WaitForNotInertAsync("h1"); // restored on close
    }

    [Fact]
    public async Task Stacked_overlays_hand_the_background_to_the_topmost_and_restore_on_the_last_close()
    {
        await GotoAsync();

        // The gallery has no "dialog that opens a second dialog" demo, so this drives the real module
        // against two synthetic overlay roots (same technique as PopoverTriggerE2ETests' module-level
        // tests). Each carries one of the two root classes activateModal recognizes.
        var states = await _page.EvaluateAsync<string[]>(
            """
            async () => {
                const mod = await import('/_content/WssBlazorControls/wss-overlay.js');
                const host = document.createElement('div');
                const make = (cls) => {
                    const root = document.createElement('div');
                    root.className = cls;
                    const panel = document.createElement('div');
                    panel.tabIndex = -1; // every real Modal/Drawer panel has this
                    root.appendChild(panel);
                    host.appendChild(root);
                    return { root, panel };
                };
                const outer = make('wss-modal-wrap');
                const inner = make('wss-drawer-root');
                const bystander = document.createElement('div');
                host.appendChild(bystander);
                document.body.appendChild(host);

                // outer / inner / bystander / the app's own page content
                const read = () => [outer.root, inner.root, bystander]
                    .map(el => el.inert ? '1' : '0')
                    .join('') + (document.querySelector('h1').closest('[inert]') ? ' page-inert' : ' page-live');

                const outerHandle = mod.activateModal(outer.panel);
                const afterOuter = read();
                const innerHandle = mod.activateModal(inner.panel);
                const afterInner = read();
                innerHandle.dispose();
                const afterInnerClosed = read();
                outerHandle.dispose();
                const afterAllClosed = read();

                host.remove();
                return [afterOuter, afterInner, afterInnerClosed, afterAllClosed];
            }
            """);

        // Only the topmost dialog owns the background, and it always owns its own ancestor chain --
        // a stacked dialog rendered inside a branch the first one had marked would otherwise open
        // dead-on-arrival, inert along with everything else around it.
        Assert.Equal("011 page-inert", states[0]); // outer live, inner + bystander + page inert
        Assert.Equal("101 page-inert", states[1]); // inner takes over: the outer dialog goes inert
        Assert.Equal("011 page-inert", states[2]); // outer resumes ownership when the inner closes
        Assert.Equal("000 page-live", states[3]);  // last close restores everything we marked
    }

    [Fact]
    public async Task Popover_trigger_gains_aria_controls_pointing_at_the_open_panel()
    {
        await GotoAsync();

        // .First: later demo sections add more Popovers to the page.
        var trigger = _page.Locator(".wss-popover-trigger button").First;
        await trigger.ClickAsync();
        var panel = _page.Locator(".wss-popover");
        await PageTestBase.WaitForOpenAndPositionedAsync(panel);

        var panelId = await panel.GetAttributeAsync("id");
        Assert.False(string.IsNullOrEmpty(panelId));
        await Expect(trigger).ToHaveAttributeAsync("aria-controls", panelId!);

        // Closed again, the panel no longer exists -- so neither may the IDREF pointing at it.
        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToHaveCountAsync(0);
        await Expect(trigger).Not.ToHaveAttributeAsync("aria-controls", new Regex("."));
        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false"); // the rest of the popup ARIA is untouched
    }

    [Fact]
    public async Task Popconfirm_trigger_gains_aria_controls_pointing_at_the_open_panel()
    {
        await GotoAsync();

        // .First: the swapped-trigger demo section adds a second (disabled) Popconfirm to the page.
        var trigger = _page.Locator(".wss-popconfirm-trigger button").First;
        await trigger.ClickAsync();
        var panel = _page.Locator(".wss-popconfirm");
        await PageTestBase.WaitForOpenAndPositionedAsync(panel);

        var panelId = await panel.GetAttributeAsync("id");
        Assert.False(string.IsNullOrEmpty(panelId));
        await Expect(trigger).ToHaveAttributeAsync("aria-controls", panelId!);

        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToHaveCountAsync(0);
        await Expect(trigger).Not.ToHaveAttributeAsync("aria-controls", new Regex("."));
    }
}
