using System.Text.RegularExpressions;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for wss-select.js <c>placeDropdown</c>'s horizontal clamp (the shared
/// <c>clampAxis</c> in wss-overlay.js). bUnit executes no JS and measures no layout, so the only
/// place this is provable is a real browser.
/// </summary>
/// <remarks>
/// Drives the /uikit gallery directly (like <see cref="UiKitGalleryE2ETests"/>, whose standalone
/// context/page pattern this mirrors) rather than via <see cref="PageTestBase"/>, for two reasons:
/// the gallery's pill Select carries a stable <c>Id</c>, so every locator here is id-based instead of
/// positional; and the pill variant is the risky case — its dropdown is content-sized (wider than
/// the trigger, up to a 320px max) so a right-edge overflow actually has to move it, where an
/// ordinary Select's dropdown usually matches its trigger's width.
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class SelectDropdownClampE2ETests : IAsyncLifetime
{
    // Select renders Id onto the inner input and derives the listbox's own id from it, so both ends
    // of the measurement are addressable by id.
    const string PillInputId = "demo-pill-select";
    const string WrapperSelector = $".wss-select:has(#{PillInputId})";
    const string DropdownSelector = $"#{PillInputId}-listbox";

    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public SelectDropdownClampE2ETests(AppFixture app, BrowserFixture browser)
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

    [Fact]
    public async Task Dropdown_is_pulled_inside_the_viewport_when_the_triggers_own_right_edge_overflows()
    {
        await GotoAsync();

        // Pin the pill so its own right edge sits PAST the viewport's — the case the previous
        // right-edge anchoring could not help with at all (anchoring the dropdown to an off-screen
        // right edge leaves the dropdown off-screen too).
        await PinPillAsync(_page.ViewportSize!.Width - 60);
        await OpenAsync();

        var (wrapperLeft, dropLeft, dropRight, viewportWidth) = await MeasureAsync();
        Assert.True(dropRight <= viewportWidth + 1,
            $"dropdown right edge ({dropRight}) ran past the viewport width ({viewportWidth})");
        Assert.True(dropLeft >= 0,
            $"dropdown left edge ({dropLeft}) was pushed off the left of the viewport");
        Assert.True(dropLeft < wrapperLeft,
            $"dropdown ({dropLeft}) should have shifted left of its overflowing trigger ({wrapperLeft})");
    }

    [Fact]
    public async Task Dropdown_still_left_aligns_with_its_trigger_when_there_is_room()
    {
        await GotoAsync();

        await PinPillAsync(40);
        await OpenAsync();

        var (wrapperLeft, dropLeft, _, _) = await MeasureAsync();
        Assert.True(Math.Abs(dropLeft - wrapperLeft) <= 1,
            $"dropdown ({dropLeft}) should stay aligned with its trigger ({wrapperLeft}) when it fits");
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

    // Pins the pill Select at a fixed viewport position through an injected stylesheet rule rather
    // than inline styles: the wrapper's `style` attribute is Blazor-bound (Width plus the open
    // z-index mirrored back from placeDropdown), so the re-render that follows placement rewrites it
    // wholesale and would silently drop an inline position — leaving the panel measured against one
    // position and painted at another. !important also outranks that bound inline style either way.
    // position: fixed keeps the trigger still, so no scrolling (and none of the demo host's smooth
    // scroll-behavior) can drift the geometry mid-measurement.
    async Task PinPillAsync(int left)
    {
        var css = $"{WrapperSelector} {{ position: fixed !important; top: 200px !important; " +
                  $"left: {left}px !important; z-index: 9999 !important; }}";
        await _page.EvaluateAsync(
            "css => { const st = document.createElement('style'); st.textContent = css; document.head.appendChild(st); }",
            css);
    }

    async Task OpenAsync()
    {
        // DispatchEventAsync, not ClickAsync: a pinned trigger's center can sit outside the viewport,
        // which a real position-based click can never reach (and a position: fixed element cannot be
        // scrolled into view). The dispatched click runs the exact same Blazor @onclick handler.
        await _page.Locator(WrapperSelector).DispatchEventAsync("click");

        var dropdown = _page.Locator(DropdownSelector);
        await Expect(dropdown).ToBeVisibleAsync();
        // wss-measuring only drops once placeDropdown has measured AND positioned the panel, so this
        // is the signal that the clamp has run.
        await Expect(dropdown).Not.ToHaveClassAsync(new Regex("wss-measuring"));
    }

    // Both rects in one evaluate, so trigger and dropdown are read from the same layout state.
    async Task<(double WrapperLeft, double DropLeft, double DropRight, double ViewportWidth)> MeasureAsync()
    {
        var result = await _page.EvaluateAsync(
            """
            ([wrapperSel, dropSel]) => {
                const w = document.querySelector(wrapperSel).getBoundingClientRect();
                const d = document.querySelector(dropSel).getBoundingClientRect();
                return { wl: w.left, dl: d.left, dr: d.right, vw: window.innerWidth };
            }
            """,
            new[] { WrapperSelector, DropdownSelector });

        var r = result!.Value;
        return (r.GetProperty("wl").GetDouble(), r.GetProperty("dl").GetDouble(),
                r.GetProperty("dr").GetDouble(), r.GetProperty("vw").GetDouble());
    }
}
