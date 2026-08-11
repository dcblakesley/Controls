using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the accessibility layer <c>wss-tooltip.js</c> adds to the CSS-only
/// <c>data-tooltip</c> convention (UIKIT-A11Y-AUDIT-2026-08-11, findings S1 and S2 prong b): while a
/// trigger is showing, its text is mirrored into one shared visually-hidden <c>role="tooltip"</c>
/// node that the trigger's <c>aria-describedby</c> points at (WCAG 4.1.2 / 1.1.1), and Escape marks
/// the trigger <c>wss-tooltip-dismissed</c> without moving the pointer or focus (WCAG 1.4.13).
/// Both live entirely in the module's document-level listeners, so this is e2e rather than bUnit —
/// bUnit executes no JavaScript.
///
/// Drives the two <c>data-tooltip</c> buttons in the /uikit gallery's "Hover Tooltip" section, both
/// addressed by <c>data-test-id</c> (no positional section locators, so appending demo sections
/// can't retarget these). Builds its own <see cref="IPage"/> harness like
/// <see cref="PopoverTriggerE2ETests"/>, since the target is the standalone /uikit route rather than
/// a form-demo view.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class HoverTooltipA11yE2ETests : IAsyncLifetime
{
    /// <summary>Id of the single shared description node the module creates on first show.</summary>
    const string DescId = "wss-tooltip-desc";

    static readonly Regex Dismissed = new(@"\bwss-tooltip-dismissed\b");

    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public HoverTooltipA11yE2ETests(AppFixture app, BrowserFixture browser)
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

    ILocator Trigger(string testId) => _page.Locator($"[data-test-id={testId}]");

    ILocator Description => _page.Locator($"#{DescId}");

    // behavior:'instant' because the host page sets scroll-behavior:smooth — a default-behavior
    // scroll animates, so a hover issued right after would aim at stale geometry.
    static Task ScrollIntoViewAsync(ILocator locator) =>
        locator.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");

    /// <summary>
    /// Waits for the bubble to actually be on screen. The reveal sits behind a 0.35s hover-intent
    /// delay (display flips through <c>transition: display allow-discrete</c>), so it has to be
    /// polled rather than asserted once — and the only handle on it is the <c>::after</c>
    /// pseudo-element's computed display, because the bubble is CSS generated content with no node
    /// for a locator to see. That absence is precisely what finding S1 is about.
    /// </summary>
    Task WaitForBubbleAsync(string testId) => _page.WaitForFunctionAsync(
        $"() => getComputedStyle(document.querySelector('[data-test-id={testId}]'), '::after').display === 'block'");

    [Fact]
    public async Task Focusing_a_trigger_describes_it_with_the_shared_tooltip_node()
    {
        await GotoAsync();
        var trigger = Trigger("tooltip-auto");

        // Created lazily on first show — before that the page carries no description node at all.
        Assert.Equal(0, await Description.CountAsync());

        await ScrollIntoViewAsync(trigger);
        await trigger.FocusAsync();

        await Expect(trigger).ToHaveAttributeAsync("aria-describedby", DescId);
        Assert.Equal(1, await Description.CountAsync()); // one singleton per page, not one per trigger
        await Expect(Description).ToHaveAttributeAsync("role", "tooltip");
        await Expect(Description).ToHaveTextAsync(await trigger.GetAttributeAsync("data-tooltip") ?? "");

        // Visually hidden, but present in the accessibility tree — the inline .wss-sr-only technique
        // (clip-path + 1px box), applied inline so it holds even if the stylesheet never loads.
        await Expect(Description).ToHaveCSSAsync("clip-path", "inset(50%)");
    }

    [Fact]
    public async Task Escape_dismisses_a_hovered_tooltip_and_leaving_the_trigger_re_arms_it()
    {
        await GotoAsync();
        var trigger = Trigger("tooltip-auto");

        await ScrollIntoViewAsync(trigger);
        await trigger.HoverAsync();
        await WaitForBubbleAsync("tooltip-auto");
        await Expect(trigger).ToHaveAttributeAsync("aria-describedby", DescId);

        await _page.Keyboard.PressAsync("Escape");

        // The class IS the dismissal as far as the module is concerned; wss-controls.css's
        // [data-tooltip].wss-tooltip-dismissed::before/::after { display: none !important } rule is
        // what turns it into the visual hide. Asserted as a class rather than a computed display so
        // this test states the JS half of the contract on its own.
        await Expect(trigger).ToHaveClassAsync(Dismissed);
        // The description is withdrawn with the bubble (they show and hide together).
        Assert.Null(await trigger.GetAttributeAsync("aria-describedby"));

        // Leaving the trigger re-arms it, so the tooltip can be shown again on the next hover
        // (WCAG 1.4.13 requires the dismissal, not a permanent suppression).
        await _page.Mouse.MoveAsync(5, 5);
        await Expect(trigger).Not.ToHaveClassAsync(Dismissed);

        await trigger.HoverAsync();
        await WaitForBubbleAsync("tooltip-auto");
        await Expect(trigger).ToHaveAttributeAsync("aria-describedby", DescId);
    }

    [Fact]
    public async Task Blurring_a_trigger_removes_the_description_again()
    {
        await GotoAsync();
        var trigger = Trigger("tooltip-auto");

        await ScrollIntoViewAsync(trigger);
        await trigger.FocusAsync();
        await Expect(trigger).ToHaveAttributeAsync("aria-describedby", DescId);

        await trigger.BlurAsync();

        // Release is deferred to the next animation frame (the leave event for one element fires
        // before the enter event for the next, so "is anything still hovered/focused" can't be
        // answered synchronously) — poll instead of reading the attribute straight back.
        await _page.WaitForFunctionAsync(
            "() => !document.querySelector('[data-test-id=tooltip-auto]').hasAttribute('aria-describedby')");

        // The shared node is emptied too, so a screen reader browsing the page linearly never meets
        // a stale description with nothing pointing at it.
        await Expect(Description).ToBeEmptyAsync();
    }

    [Fact]
    public async Task Moving_to_another_trigger_retargets_the_description_and_clears_the_dismissal()
    {
        await GotoAsync();
        var first = Trigger("tooltip-auto");
        var second = Trigger("tooltip-forced-top");

        await ScrollIntoViewAsync(first);
        await first.HoverAsync();
        await WaitForBubbleAsync("tooltip-auto");
        await _page.Keyboard.PressAsync("Escape");
        await Expect(first).ToHaveClassAsync(Dismissed);

        // Straight onto the neighbouring trigger: leave and enter arrive within one input event, so
        // the previous trigger has to be released by the new one's show path rather than by the
        // deferred check — the rapid-retargeting case.
        await second.HoverAsync();
        await Expect(second).ToHaveAttributeAsync("aria-describedby", DescId);
        await Expect(first).Not.ToHaveClassAsync(Dismissed);
        Assert.Null(await first.GetAttributeAsync("aria-describedby"));
        await Expect(Description).ToHaveTextAsync(await second.GetAttributeAsync("data-tooltip") ?? "");
    }

    [Fact]
    public async Task An_existing_aria_describedby_is_appended_to_and_restored_verbatim()
    {
        await GotoAsync();

        // The gallery's triggers carry no describedby of their own, so the append-then-restore path
        // is driven against injected DOM (the same approach PopoverTriggerE2ETests uses for cases
        // the demo can't express). The module is an IIFE with nothing to import — but it is already
        // listening on document, so dispatched focusin/focusout reach it exactly as a consumer's
        // markup would.
        var result = await _page.EvaluateAsync<string[]>(
            """
            async () => {
                const el = document.createElement('button');
                el.setAttribute('data-tooltip', 'Injected tip');
                el.setAttribute('aria-describedby', 'consumer-hint');
                document.body.appendChild(el);

                el.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
                const during = el.getAttribute('aria-describedby');
                const text = document.getElementById('wss-tooltip-desc').textContent;

                el.dispatchEvent(new FocusEvent('focusout', { bubbles: true }));
                await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                const after = el.getAttribute('aria-describedby');

                el.remove();
                return [during, text, after];
            }
            """);

        Assert.Equal($"consumer-hint {DescId}", result[0]); // appended, never overwritten
        Assert.Equal("Injected tip", result[1]);
        Assert.Equal("consumer-hint", result[2]);           // the consumer's own value restored
    }
}
