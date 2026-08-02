using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the <c>prefers-reduced-motion</c> gap-closing fixes from AUDIT-2026-07-30.md
/// findings 10 (<c>wss-controls.css</c>'s reduced-motion block was missing the <c>[data-tooltip]</c>
/// hover tooltip's arrow/body transitions and <c>.wss-table-expand-btn</c>'s color transition) and 28
/// (<c>edit-controls.css</c>'s reduced-motion block stopped at the label tooltip bubble, missing the
/// <c>.edit-theme</c> input chrome and the button-mode radio / <c>EditFile</c> button-variant color
/// transitions). Both were previously verified only by reading the two stylesheets' <c>@media
/// (prefers-reduced-motion: reduce)</c> blocks and reasoning about which selectors were listed -- see
/// the audit doc's "Still open" follow-up (d). This suite instead forces the OS preference via
/// <see cref="IPage.EmulateMediaAsync"/> and reads <c>getComputedStyle(...).transitionDuration</c>
/// back, first confirming a genuine (non-zero) transition exists without the emulation so the
/// post-emulation assertion isn't vacuous.
///
/// Cross-cutting (spans the UiKit hover tooltip, Table's expand button, and four different
/// <c>edit-controls.css</c> selectors reached from four different demo views), so it gets its own
/// class per the audit's "two classes are fine" allowance for follow-up (d) rather than living inside
/// any one control's per-control suite. Builds its own harness directly against <see cref="IPage"/>
/// (like <see cref="UiKitGalleryE2ETests"/>/<see cref="RtlE2ETests"/>) since it navigates both the
/// standalone <c>/uikit</c> route and several of the form-demo view switcher's routes.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class ReducedMotionE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public ReducedMotionE2ETests(AppFixture app, BrowserFixture browser)
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

    async Task GotoUiKitAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    // Same hydration-ready signal PageTestBase.NavigateAsync uses (a sidebar nav button for the
    // requested view) -- built locally since this class needs several form-demo views (Theme/String/
    // RadioString/File) alongside the standalone /uikit route above.
    async Task GotoViewAsync(CurrentView view)
    {
        await _page.GotoAsync($"{_app.BaseUrl}/?view={view}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("button", new() { HasTextString = view.ToString() }).First)
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    static Task<string> TransitionDurationAsync(ILocator locator, string? pseudoElement = null) =>
        pseudoElement is null
            ? locator.EvaluateAsync<string>("el => getComputedStyle(el).transitionDuration")
            : locator.EvaluateAsync<string>($"el => getComputedStyle(el, '{pseudoElement}').transitionDuration");

    [Fact]
    public async Task Hover_tooltip_pseudo_elements_and_table_expand_button_freeze_under_reduced_motion()
    {
        await GotoUiKitAsync();
        var tooltipTrigger = _page.Locator("[data-test-id=tooltip-auto]");
        var expandBtn = _page.Locator("section.demo-section", new() { HasTextString = "expandable rows" })
            .Locator(".wss-table-expand-btn").First;
        var expandSvg = expandBtn.Locator("svg");
        await Expect(tooltipTrigger).ToBeVisibleAsync();
        await Expect(expandBtn).ToBeVisibleAsync();

        // Motion-allowed baseline: every one of these genuinely animates today -- if any already read
        // "0s" here, the reduced-motion assertion below would be vacuous.
        Assert.NotEqual("0s", await TransitionDurationAsync(tooltipTrigger, "::before"));
        Assert.NotEqual("0s", await TransitionDurationAsync(tooltipTrigger, "::after"));
        Assert.NotEqual("0s", await TransitionDurationAsync(expandBtn));
        Assert.NotEqual("0s", await TransitionDurationAsync(expandSvg));

        await _page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        // Finding 10: these four selectors were the ones missing from wss-controls.css's
        // reduced-motion block (the tooltip's arrow/body and the table expand button/its chevron).
        Assert.Equal("0s", await TransitionDurationAsync(tooltipTrigger, "::before"));
        Assert.Equal("0s", await TransitionDurationAsync(tooltipTrigger, "::after"));
        Assert.Equal("0s", await TransitionDurationAsync(expandBtn));
        Assert.Equal("0s", await TransitionDurationAsync(expandSvg));
    }

    [Fact]
    public async Task Edit_theme_input_chrome_freezes_under_reduced_motion()
    {
        await GotoViewAsync(CurrentView.Theme);
        // The legacy-mode editor itself (BasicString -- carries edit-input but never edit-affix-input)
        // and an affix-mode wrapper (PrefixSuffixString) -- the exact selector pair finding 28 added.
        var legacyInput = _page.Locator(".edit-theme input.edit-input:not(.edit-affix-input)").First;
        var affixWrapper = _page.Locator(".edit-theme .edit-input-affix-wrapper").First;
        await Expect(legacyInput).ToBeVisibleAsync();
        await Expect(affixWrapper).ToBeVisibleAsync();

        Assert.NotEqual("0s", await TransitionDurationAsync(legacyInput));
        Assert.NotEqual("0s", await TransitionDurationAsync(affixWrapper));

        await _page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        Assert.Equal("0s", await TransitionDurationAsync(legacyInput));
        Assert.Equal("0s", await TransitionDurationAsync(affixWrapper));
    }

    [Fact]
    public async Task Label_tooltip_bubble_freezes_under_reduced_motion()
    {
        await GotoViewAsync(CurrentView.String);
        var content = _page.Locator(".edit-tooltip-content").First;
        // Not necessarily visible (display:none until hover/focus) -- getComputedStyle still resolves
        // the transition property regardless of the reveal state.
        Assert.NotEqual("0s", await TransitionDurationAsync(content));

        await _page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        Assert.Equal("0s", await TransitionDurationAsync(content));
    }

    [Fact]
    public async Task Button_mode_radio_freezes_under_reduced_motion()
    {
        await GotoViewAsync(CurrentView.RadioString);
        var buttonModeOption = _page.Locator(".edit-radio-button").First;
        await Expect(buttonModeOption).ToBeVisibleAsync();

        Assert.NotEqual("0s", await TransitionDurationAsync(buttonModeOption));

        await _page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        Assert.Equal("0s", await TransitionDurationAsync(buttonModeOption));
    }

    [Fact]
    public async Task EditFile_button_variant_freezes_under_reduced_motion()
    {
        await GotoViewAsync(CurrentView.File);
        var selectBtn = _page.Locator(".edit-file-select-btn").First;
        await Expect(selectBtn).ToBeVisibleAsync();

        Assert.NotEqual("0s", await TransitionDurationAsync(selectBtn));

        await _page.EmulateMediaAsync(new PageEmulateMediaOptions { ReducedMotion = ReducedMotion.Reduce });

        Assert.Equal("0s", await TransitionDurationAsync(selectBtn));
    }
}
