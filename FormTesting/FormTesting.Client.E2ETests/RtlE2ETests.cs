using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the RTL CSS fixes from AUDIT-2026-07-30.md findings 9 (pagination's and the
/// UiKit pickers' native page-size / month-year <c>&lt;select&gt;</c>s pairing a physical padding
/// split with a logical <c>inset-inline-end</c> arrow) and 27 (<c>.edit-tooltip-container</c>'s last
/// physical <c>margin-left</c>). Both were previously verified only by reading
/// <c>wss-controls.css</c> / <c>edit-controls.css</c> and reasoning about what
/// <c>padding-inline</c>/<c>margin-inline-start</c> resolve to under <c>dir="rtl"</c> (see the audit
/// doc's "Still open" follow-up (d)) -- nothing ever actually flipped the document direction and read
/// a value back. This suite does: it sets <c>document.documentElement.dir = 'rtl'</c> after
/// navigating (every demo page renders LTR by default; none offers a direction toggle) and asserts
/// the *physical* left/right padding/margin/inset on the fixed elements swap sides relative to the
/// LTR baseline captured on those same elements moments earlier -- a regression back to a physical
/// declaration would leave the reading unchanged instead of flipping.
///
/// Cross-cutting (spans Pagination, the UiKit pickers, and EditString's label tooltip), so it gets
/// its own class per the audit's "two classes are fine" allowance for follow-up (d) rather than
/// living inside any one control's per-control suite. Builds its own harness directly against
/// <see cref="IPage"/> (like <see cref="UiKitGalleryE2ETests"/> and <see cref="DatePickerE2ETests"/>)
/// rather than <see cref="PageTestBase"/>, since it needs both the standalone <c>/uikit</c> route and
/// the form-demo view switcher's <c>?view=String</c> route in the same class.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class RtlE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public RtlE2ETests(AppFixture app, BrowserFixture browser)
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

    // Mirrors DatePickerE2ETests/DateRangePickerE2ETests.GotoAsync -- the picker-select test below
    // opens a JS-positioned dropdown, so the page height (and every section above it) must be
    // settled first, same reasoning as those two suites' own copy of this wait.
    async Task GotoUiKitAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Expect(_page.Locator("section.demo-section", new() { HasTextString = "server-side paging" })
                .Locator(".wss-table-row"))
            .ToHaveCountAsync(10, new() { Timeout = 15_000 });
        await _page.WaitForFunctionAsync(
            @"() => {
                const h = document.body.scrollHeight;
                if (window.__wssLastHeight !== h) { window.__wssLastHeight = h; window.__wssStableSince = Date.now(); }
                return Date.now() - window.__wssStableSince > 600;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    // Same hydration-ready signal PageTestBase.NavigateAsync uses (a sidebar nav button for the
    // requested view, present regardless of exact heading wording) -- built locally since this
    // class needs the form-demo view switcher alongside the standalone /uikit route above.
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

    // Flips the whole document to RTL -- the exact condition every fixed selector's own code comment
    // reasons about ("under dir=\"rtl\"").
    Task SetRtlAsync() => _page.EvaluateAsync("() => { document.documentElement.dir = 'rtl'; }");

    static async Task<(string Left, string Right)> ReadPaddingAsync(ILocator locator)
    {
        var values = await locator.EvaluateAsync<string[]>(
            "el => { const s = getComputedStyle(el); return [s.paddingLeft, s.paddingRight]; }");
        return (values[0], values[1]);
    }

    static async Task<(string Left, string Right)> ReadMarginAsync(ILocator locator)
    {
        var values = await locator.EvaluateAsync<string[]>(
            "el => { const s = getComputedStyle(el); return [s.marginLeft, s.marginRight]; }");
        return (values[0], values[1]);
    }

    [Fact]
    public async Task Pagination_size_select_padding_and_arrow_flip_under_rtl()
    {
        await GotoUiKitAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "size changer, quick jumper" });
        var select = section.Locator(".wss-pagination-size-select").First;
        var arrow = section.Locator(".wss-pagination-size-arrow").First;
        await Expect(select).ToBeVisibleAsync();

        // LTR baseline: finding 9's documented split (8px on the text side, 28px on the arrow side),
        // and the arrow itself sits at inset-inline-end -- physically `right` under LTR.
        var (leftBefore, rightBefore) = await ReadPaddingAsync(select);
        Assert.Equal("8px", leftBefore);
        Assert.Equal("28px", rightBefore);
        Assert.Equal("8px", await arrow.EvaluateAsync<string>("el => getComputedStyle(el).right"));

        await SetRtlAsync();

        // RTL: both the padding split and the arrow's inset must flip sides -- a regression to the
        // physical `padding: 0 28px 0 8px` finding 9 replaced would leave these readings unchanged
        // instead of swapping.
        var (leftAfter, rightAfter) = await ReadPaddingAsync(select);
        Assert.Equal("28px", leftAfter);
        Assert.Equal("8px", rightAfter);
        Assert.Equal("8px", await arrow.EvaluateAsync<string>("el => getComputedStyle(el).left"));

        // Geometry cross-check: the arrow now sits over the LEFT half of the select (where the wide
        // 28px padding now lives), not the right -- a physical read on real layout, not just the
        // declared CSS values.
        var selectBox = await select.BoundingBoxAsync();
        var arrowBox = await arrow.BoundingBoxAsync();
        Assert.NotNull(selectBox);
        Assert.NotNull(arrowBox);
        var arrowCenterX = arrowBox!.X + arrowBox.Width / 2;
        Assert.True(arrowCenterX < selectBox!.X + selectBox.Width / 2,
            $"arrow center ({arrowCenterX}) should sit in the left half of the select ({selectBox.X}-{selectBox.X + selectBox.Width}) under RTL");
    }

    [Fact]
    public async Task Picker_month_year_select_padding_and_arrow_flip_under_rtl()
    {
        await GotoUiKitAsync();
        var picker = _page.Locator(".wss-picker", new() { Has = _page.Locator("#demo-date") });
        var field = picker.Locator(".wss-picker-input");
        var dropdown = picker.Locator(".wss-picker-dropdown");

        // behavior:'instant' -- the host page sets scroll-behavior:smooth, which would leave geometry
        // mid-animation for a default scroll (same reasoning as DatePickerE2ETests.OpenAsync).
        await field.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await field.ClickAsync();
        await PageTestBase.WaitForOpenAndPositionedAsync(dropdown);

        var monthSelect = dropdown.Locator(".wss-picker-select select").First;
        var monthArrow = dropdown.Locator(".wss-picker-select-arrow").First;

        // LTR baseline: the same 8px/28px split finding 9 gave the pagination select, reused here.
        var (leftBefore, rightBefore) = await ReadPaddingAsync(monthSelect);
        Assert.Equal("8px", leftBefore);
        Assert.Equal("28px", rightBefore);
        Assert.Equal("8px", await monthArrow.EvaluateAsync<string>("el => getComputedStyle(el).right"));

        await SetRtlAsync();

        var (leftAfter, rightAfter) = await ReadPaddingAsync(monthSelect);
        Assert.Equal("28px", leftAfter);
        Assert.Equal("8px", rightAfter);
        Assert.Equal("8px", await monthArrow.EvaluateAsync<string>("el => getComputedStyle(el).left"));
    }

    [Fact]
    public async Task Tooltip_trigger_margin_flips_under_rtl()
    {
        await GotoViewAsync(CurrentView.String);
        var trigger = _page.Locator(".edit-tooltip-container").First;
        await Expect(trigger).ToBeVisibleAsync();

        // LTR baseline: finding 27's documented margin-inline-start resolves to a physical
        // margin-left: 4px (margin-inline-end is never set, so the trailing side stays at 0).
        var (leftBefore, rightBefore) = await ReadMarginAsync(trigger);
        Assert.Equal("4px", leftBefore);
        Assert.Equal("0px", rightBefore);

        await SetRtlAsync();

        // RTL: the gap must follow to the other side of the trigger icon -- a regression to the
        // physical `margin-left: 4px` finding 27 replaced would leave the gap on the LTR side.
        var (leftAfter, rightAfter) = await ReadMarginAsync(trigger);
        Assert.Equal("0px", leftAfter);
        Assert.Equal("4px", rightAfter);
    }
}
