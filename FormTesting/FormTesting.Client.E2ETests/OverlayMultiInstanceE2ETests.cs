namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the cross-instance hardening of <c>wss-overlay.js</c>'s body-scroll lock and
/// focus-trap stack. <see cref="Controls.FormDefaults.AssetBase"/> (10.6.4) lets different MFEs
/// import this module from different origin URLs, and a browser instantiates an ES module once per
/// distinct specifier URL — so two "instances" of this file (two separate module scopes, each with
/// its own closures) are routinely live on one page. Before the fix, the scroll-lock counter and the
/// focus-trap stack were module-scoped, so each instance believed it alone owned the document: an
/// interleaved open/close across instances could leave the page permanently scroll-locked (or unlock
/// it while a dialog was still open), and two instances' document-level listeners could fight over
/// focus/Escape.
/// <para>
/// Forces a second module instance the same way a cross-origin MFE would end up with one: a dynamic
/// <c>import()</c> of the same file under a different specifier URL (a query string is enough — the
/// browser keys module identity off the resolved URL, and ASP.NET Core's static file middleware
/// ignores the query string, so it serves the identical file). Drives instance 2 directly via its
/// exported <c>activateModal</c> (the same function the Modal/Drawer C# components call), and drives
/// instance 1 through the app's normal UI (the /uikit gallery's Modal).
/// </para>
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class OverlayMultiInstanceE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;
    readonly List<string> _pageErrors = [];

    public OverlayMultiInstanceE2ETests(AppFixture app, BrowserFixture browser)
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
        _page.PageError += (_, error) => _pageErrors.Add(error);
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

    // Imports a second module instance (distinct specifier URL -> distinct module scope) and
    // activates a synthetic panel through its own exported activateModal, exactly as a real
    // Modal/Drawer component would. The handle is stashed on window so later EvaluateAsync calls
    // (each a fresh round trip) can dispose it -- IJSObjectReference-style handles aren't
    // serializable back to .NET, so the handle has to live in-page between steps.
    async Task ActivateSecondInstanceAsync()
    {
        var created = await _page.EvaluateAsync<bool>(
            """
            async () => {
                const mod2 = await import('/_content/WssBlazorControls/wss-overlay.js?instance=2');
                const panel2 = document.createElement('div');
                // tabindex="-1": matches every real Modal/Drawer panel in this library (see
                // Modal.razor / Drawer.razor) -- programmatically focusable (excluded from the
                // natural tab order, but a valid target for the trap's own no-focusable-children
                // fallback, `(items[0] || panel).focus()`). A panel with no tabindex at all isn't a
                // realistic stand-in for a real overlay panel.
                panel2.tabIndex = -1;
                document.body.appendChild(panel2);
                window.__wssTestPanel2 = panel2;
                window.__wssTestHandle2 = mod2.activateModal(panel2);
                return !!window.__wssTestHandle2;
            }
            """);
        Assert.True(created, "instance 2's activateModal should return a handle for a non-null panel");
    }

    async Task DisposeSecondInstanceAsync()
    {
        await _page.EvaluateAsync(
            """
            () => {
                window.__wssTestHandle2.dispose();
                window.__wssTestPanel2.remove();
            }
            """);
    }

    Task<string> BodyOverflowAsync() => _page.EvaluateAsync<string>("() => document.body.style.overflow");

    [Fact]
    public async Task Scroll_lock_ref_counts_across_two_module_instances_and_fully_restores_on_last_dispose()
    {
        await GotoAsync();
        Assert.Equal(string.Empty, await BodyOverflowAsync()); // baseline: nothing locked yet

        // Instance 2 locks first (the old bug: this counter is invisible to instance 1 and vice versa).
        await ActivateSecondInstanceAsync();
        Assert.Equal("hidden", await BodyOverflowAsync());

        // Instance 1 (the app's own Modal) opens on top of it -- ref count should go to 2, not reset.
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();
        Assert.Equal("hidden", await BodyOverflowAsync());

        // Instance 1 closes -- instance 2's lock is still outstanding, so the page must stay locked
        // (the bug: each instance's own counter would independently think it owns the unlock).
        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();
        Assert.Equal("hidden", await BodyOverflowAsync());

        // Instance 2 closes last -- now the shared ref count reaches 0 and the original value (the
        // empty string captured by whichever activation locked first) is restored.
        await DisposeSecondInstanceAsync();
        Assert.Equal(string.Empty, await BodyOverflowAsync());

        Assert.Empty(_pageErrors);
    }

    [Fact]
    public async Task Modal_from_instance_one_still_traps_focus_and_closes_on_escape_once_while_instance_two_is_active()
    {
        await GotoAsync();

        // Instance 2 has an active trap (and holds the scroll lock) for the whole test -- the old bug
        // was that a second instance's document-level listeners believed THEY still owned the
        // document/focus, fighting with instance 1's over focus containment and Escape.
        await ActivateSecondInstanceAsync();

        var openButton = _page.Locator("button", new() { HasTextString = "Open Modal" });
        await openButton.ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();

        // Focus trap engaged: instance 1's trap must be the one that acts, even though instance 2's
        // trap is still in the shared stack (below it, not topmost).
        await _page.WaitForFunctionAsync(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");

        // Shift+Tab from the panel itself must stay trapped inside instance 1's panel (not escape to
        // instance 2's synthetic panel, and not get grabbed twice).
        await panel.EvaluateAsync("el => el.focus()");
        await _page.Keyboard.PressAsync("Shift+Tab");
        var trapped = await _page.EvaluateAsync<bool>(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");
        Assert.True(trapped);

        // A single Escape closes exactly once (a double-fire from two instances' capture listeners
        // both acting would still only close once here, but page errors from re-entrant dispatch --
        // see the isTrusted-gated redispatch in onKeydown -- would surface below).
        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeHiddenAsync();

        // Focus is NOT left on the real opener -- instance 2's dialog is still open underneath, and
        // per the onFocusIn contract ("an outer overlay restoring focus to its trigger while this one
        // is open" -- see the comment in wss-overlay.js) the now-topmost trap must reclaim it. This is
        // the same behavior a single instance already has for nested modals; the point here is that
        // it is instance 2's OWN trap that reclaims it (the shared stack correctly promoted instance
        // 2 to topmost the instant instance 1 popped off), not a fight between the two, and not focus
        // left stranded on <body>.
        await Expect(openButton).Not.ToBeFocusedAsync();
        var focusedIsInstance2Panel = await _page.EvaluateAsync<bool>(
            "() => document.activeElement === window.__wssTestPanel2");
        Assert.True(focusedIsInstance2Panel, "instance 2's still-open trap should have reclaimed focus into its own panel");

        // Instance 2's lock/trap are still live and undisturbed by instance 1's activity.
        Assert.Equal("hidden", await BodyOverflowAsync());

        await DisposeSecondInstanceAsync();
        Assert.Equal(string.Empty, await BodyOverflowAsync());

        Assert.Empty(_pageErrors);
    }
}
