using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// Base class for all per-control e2e tests. Wires up the shared <see cref="AppFixture"/> +
/// <see cref="BrowserFixture"/>, gives each test method a fresh <see cref="IBrowserContext"/> +
/// <see cref="IPage"/>, and exposes helpers for navigating to a specific demo view + waiting
/// for Blazor WebAssembly to hydrate.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public abstract class PageTestBase : IAsyncLifetime
{
    protected readonly AppFixture App;
    protected readonly BrowserFixture Browser;

    protected IBrowserContext Context { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;

    /// <summary>
    /// The <see cref="CurrentView"/> this test class targets. Used by <see cref="NavigateAsync"/>
    /// to build the demo URL.
    /// </summary>
    protected abstract CurrentView View { get; }

    protected PageTestBase(AppFixture app, BrowserFixture browser)
    {
        App = app;
        Browser = browser;
    }

    public async Task InitializeAsync()
    {
        // Each test gets a fresh context — independent cookies, storage, and page state.
        Context = await Browser.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Fixed viewport so screenshot baselines are deterministic across machines.
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            // Reduces noise from font/anti-aliasing differences between machines.
            DeviceScaleFactor = 1,
        });
        Page = await Context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        await Page.CloseAsync();
        await Context.CloseAsync();
    }

    /// <summary>
    /// Navigate to this test class's demo view and wait for Blazor WebAssembly to hydrate enough
    /// that controls are interactive. Use this at the start of each test rather than ad-hoc
    /// <c>Page.GotoAsync</c> calls.
    /// </summary>
    protected async Task NavigateAsync()
    {
        await Page.GotoAsync($"{App.BaseUrl}/?view={View}", new PageGotoOptions
        {
            // NetworkIdle waits until the WASM bundle has finished downloading and no further
            // network activity is happening — a reliable hydration-ready signal for this app.
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000, // first-run WASM download can be slow.
        });

        // Belt-and-suspenders: confirm the sidebar nav rendered. If this isn't visible, hydration
        // failed and downstream interactions would all retry-fail anyway.
        await Expect(Page.Locator("button", new() { HasTextString = View.ToString() }).First)
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
    }

    /// <summary>
    /// Captures a PNG screenshot of the locator and asserts it matches the committed baseline
    /// under <c>Snapshots/&lt;TestClass&gt;-&lt;name&gt;.png</c>. Re-run with
    /// <c>UPDATE_SNAPSHOTS=1</c> to regenerate baselines after intentional UI changes.
    /// </summary>
    protected async Task ExpectMatchesBaselineAsync(ILocator locator, string name)
    {
        var bytes = await locator.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-{name}");
    }

    /// <summary>
    /// Waits for a JS-positioned overlay panel (a wss-picker/wss-select dropdown, a popover/popconfirm
    /// panel, etc.) to be both visible AND fully positioned. The JS placement modules apply a
    /// "wss-measuring" class while they measure the panel and compute its placement, dropping it only
    /// once positioning is done -- asserting visibility alone can observe the panel mid-measurement, so
    /// every open needs both checks. Consolidates the pair that used to be copy-pasted at every open
    /// site across the picker/select/uikit e2e suites.
    /// </summary>
    /// <remarks>
    /// <c>protected internal</c> rather than <c>protected</c>: <see cref="DatePickerE2ETests"/>,
    /// <see cref="DateRangePickerE2ETests"/>, and <see cref="UiKitGalleryE2ETests"/> build their own
    /// <see cref="IAsyncLifetime"/> harness directly against <c>IPage</c> instead of subclassing this
    /// base, so they need same-assembly (not just derived-type) access.
    /// </remarks>
    protected internal static async Task WaitForOpenAndPositionedAsync(ILocator panel)
    {
        await Expect(panel).ToBeVisibleAsync();
        await Expect(panel).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }
}

/// <summary>
/// Base class for the per-control demo-page e2e suites (one per <c>Edit*</c> control's own demo
/// view). Adds the two tests that were byte-identical across every one of those suites: the
/// page-heading smoke test and the "basic section" visual baseline.
/// </summary>
/// <remarks>
/// This sits between <see cref="PageTestBase"/> and the per-control classes rather than folding
/// these two tests into <see cref="PageTestBase"/> itself, because not every <see cref="PageTestBase"/>
/// subclass wants them: <c>ComparisonE2ETests</c> targets a demo view with no "{Control} Demo"
/// heading and no basic-section baseline of its own, so it stays directly on <see cref="PageTestBase"/>.
/// </remarks>
public abstract class DemoPageTestBase(AppFixture app, BrowserFixture browser) : PageTestBase(app, browser)
{
    /// <summary>
    /// The exact <c>&lt;h1&gt;</c> text this control's demo page renders. Every current per-control
    /// view's heading follows "Edit{View} Demo" (e.g. <c>CurrentView.Radio</c> -&gt; "EditRadio Demo")
    /// -- verified against all 19 existing usages -- so this derives from <see cref="PageTestBase.View"/>
    /// and needs no per-class override unless a future control's heading genuinely breaks that
    /// convention, in which case override it there.
    /// </summary>
    protected virtual string ExpectedHeading => $"Edit{View} Demo";

    [Fact]
    public async Task Demo_page_renders_with_expected_heading()
    {
        await NavigateAsync();
        await Expect(Page.Locator("h1", new() { HasTextString = ExpectedHeading })).ToBeVisibleAsync();
    }

    // virtual: EditCheckedEnumListE2ETests overrides this to attach a class-specific "known flaky"
    // rationale comment to an otherwise-identical baseline capture -- see its override.
    [Fact]
    public virtual async Task Visual_baseline_basic_section()
    {
        await NavigateAsync();
        var firstSection = Page.Locator("section.demo-section").First;
        await Expect(firstSection).ToBeVisibleAsync();
        await ExpectMatchesBaselineAsync(firstSection, "basic-section");
    }
}
