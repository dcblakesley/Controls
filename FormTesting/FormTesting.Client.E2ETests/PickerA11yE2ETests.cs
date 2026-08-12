namespace FormTesting.Client.E2ETests;

/// <summary>
/// Accessibility e2e coverage for <see cref="Controls.DatePicker"/>/<see cref="Controls.DateRangePicker"/>,
/// driven on the /uikit gallery's pinned picker demos. Two things live here that bUnit cannot answer:
/// the ArrowDown-from-the-field affordance (a JS-owned DOM focus move — bUnit only sees the interop
/// call) and the calendar's ARIA GRID STRUCTURE as an accessibility tree, rather than as the DOM
/// attributes the bUnit suites already pin.
/// </summary>
/// <remarks>
/// The grid assertions exist because the day grid's rows are <c>display: contents</c> wrappers
/// carrying an explicit <c>role="row"</c>: the day cells have to stay the CSS grid's own items, so a
/// row can't be a box of its own. That pattern is the historically shaky one — a tree built from a
/// stylesheet-erased element is exactly what an implementation is free to get wrong — and every
/// existing assertion about it reads DOM attributes, which would keep passing regardless.
/// <para>
/// Playwright's aria snapshot is the role/name/nesting tree computed in the page, so what it proves
/// is that the roles still NEST (grid owns rows, rows own cells) once the layout has erased the row
/// boxes — not the platform (MSAA/UIA/AT-SPI) mapping, which no browser-automation API exposes.
/// </para>
/// </remarks>
[Collection(PlaywrightCollection.Name)]
public class PickerA11yE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public PickerA11yE2ETests(AppFixture app, BrowserFixture browser)
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

    // The same /uikit stabilization every gallery suite uses: wait for the page height to stop moving
    // before any geometry- or focus-sensitive step (see DateRangePickerE2ETests.GotoAsync for the
    // full story on the late layout shifts).
    async Task GotoAsync()
    {
        await _page.GotoAsync($"{_app.BaseUrl}/uikit", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60_000,
        });
        await Expect(_page.Locator("h1", new() { HasTextString = "UI Kit Gallery" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });
        await _page.WaitForFunctionAsync(
            @"() => {
                const h = document.body.scrollHeight;
                if (window.__wssLastHeight !== h) { window.__wssLastHeight = h; window.__wssStableSince = Date.now(); }
                return Date.now() - window.__wssStableSince > 600;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });
    }

    ILocator DropdownOf(string inputId) =>
        _page.Locator(".wss-picker", new() { Has = _page.Locator($"#{inputId}") }).Locator(".wss-picker-dropdown");

    // Opens by clicking the INPUT itself (not the surrounding field chrome, which has tabindex="-1"
    // and would take the focus): every test here starts from the state the picker's own focus model
    // produces -- panel open, focus still on the text field. behavior:'instant' -- the app CSS sets
    // html { scroll-behavior: smooth }, so a default scroll animates and anything measured right
    // after it is mid-flight garbage.
    async Task<ILocator> OpenAsync(string inputId)
    {
        var input = _page.Locator($"#{inputId}");
        await input.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");
        await input.ClickAsync();
        var dropdown = DropdownOf(inputId);
        await PageTestBase.WaitForOpenAndPositionedAsync(dropdown);
        return dropdown;
    }

    // Only one picker can be open at a time on the page: the open one's backdrop covers the viewport
    // and would swallow the next field click.
    async Task CloseAsync(ILocator dropdown)
    {
        await _page.Keyboard.PressAsync("Escape");
        await Expect(dropdown).Not.ToBeVisibleAsync();
    }

    // --- ArrowDown from the field into the grid ----------------------------------------------

    [Fact]
    public async Task ArrowDown_in_the_date_field_moves_focus_onto_the_grids_roving_cell()
    {
        await GotoAsync();
        var dropdown = await OpenAsync("demo-date");

        // The panel opens with focus deliberately still on the field (the combobox-like model), so
        // before ArrowDown the calendar holds no focus at all -- Tab was the only way in.
        await Expect(_page.Locator("#demo-date")).ToBeFocusedAsync();

        await _page.Locator("#demo-date").PressAsync("ArrowDown");

        // The demo pins Value=2026-02-14, which is also where the roving tabindex sits on open.
        await Expect(dropdown.Locator("[data-date='2026-02-14'][tabindex='0']")).ToBeFocusedAsync();
        await Expect(dropdown).ToBeVisibleAsync(); // the panel stays open

        // ...and focus really landed ON the roving stop, not merely inside the panel: the grid's own
        // arrow navigation continues from there.
        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(dropdown.Locator("[data-date='2026-02-15']")).ToBeFocusedAsync();
    }

    [Fact]
    public async Task ArrowDown_in_a_range_field_moves_focus_onto_whichever_panel_owns_the_roving_cell()
    {
        await GotoAsync();
        var dropdown = await OpenAsync("demo-range");

        await Expect(_page.Locator("#demo-range")).ToBeFocusedAsync();

        await _page.Locator("#demo-range").PressAsync("ArrowDown");

        // The demo pins Start=2025-01-15 / End=2025-02-03, so the single roving stop across both
        // panels is the start endpoint, in the LEFT one.
        await Expect(dropdown.Locator("[data-date='2025-01-15'][tabindex='0']")).ToBeFocusedAsync();
        await Expect(dropdown).ToBeVisibleAsync();

        await _page.Keyboard.PressAsync("ArrowRight");
        await Expect(dropdown.Locator("[data-date='2025-01-16'][tabindex='0']")).ToBeFocusedAsync();
    }

    // --- The ARIA grid structure, as the accessibility tree sees it --------------------------

    // Playwright's aria snapshot is 2-space-indented YAML -- one line per accessibility-tree node,
    // shaped `- role "name":` -- so a line's indentation IS that node's depth in the tree. Walking
    // the expected roles in order, each has to appear BELOW and DEEPER than the previous match, which
    // is what pins "the grid still owns rows, and the rows still own cells" rather than the far
    // weaker "all three words appear somewhere".
    static void AssertAriaNesting(string snapshot, params string[] roles)
    {
        var lines = snapshot.Split('\n');
        var index = -1;
        var depth = -1;
        foreach (var role in roles)
        {
            var head = $"- {role}";
            var found = false;
            for (var i = index + 1; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith(head, StringComparison.Ordinal)) continue;
                // "- grid" must not match "- gridcell": the role token ends at a space or a colon.
                if (trimmed.Length > head.Length && trimmed[head.Length] is not (' ' or ':')) continue;
                var indent = lines[i].Length - trimmed.Length;
                if (indent <= depth) continue; // a sibling/ancestor of the last match, not a child
                index = i;
                depth = indent;
                found = true;
                break;
            }

            Assert.True(found,
                $"No '{role}' nested under '{string.Join(" > ", roles)}' in the accessibility tree:\n{snapshot}");
        }
    }

    [Fact]
    public async Task The_day_grid_exposes_grid_row_gridcell_to_the_accessibility_tree()
    {
        await GotoAsync();
        var dropdown = await OpenAsync("demo-date");

        var snapshot = await dropdown.Locator(".wss-picker-grid").AriaSnapshotAsync();

        // The row wrappers are display:contents -- erased as boxes, but they must still be the grid's
        // own rows in the tree, with the day cells owned by them.
        AssertAriaNesting(snapshot, "grid", "row", "gridcell");
        // The cell's own accessible name comes from the day button it wraps (the full "D"-format
        // date), so a gridcell is never an unnamed node.
        Assert.Contains("February 14, 2026", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_month_and_quarter_grids_expose_grid_row_gridcell_to_the_accessibility_tree()
    {
        await GotoAsync();

        // Month: 4 rows of 3, chunked to match the CSS grid-template so no row spans a line break.
        var month = await OpenAsync("demo-month");
        AssertAriaNesting(await month.Locator(".wss-picker-month-grid").AriaSnapshotAsync(),
            "grid", "row", "gridcell");
        await CloseAsync(month);

        // Quarter: the single-row shape, which has its own 4-column chunking.
        var quarter = await OpenAsync("demo-quarter");
        AssertAriaNesting(await quarter.Locator(".wss-picker-quarter-grid").AriaSnapshotAsync(),
            "grid", "row", "gridcell");
    }

    [Fact]
    public async Task Both_range_panels_expose_grid_row_gridcell_to_the_accessibility_tree()
    {
        await GotoAsync();
        var dropdown = await OpenAsync("demo-range");

        var grids = dropdown.Locator(".wss-picker-grid");
        await Expect(grids).ToHaveCountAsync(2);

        // Each panel is its own grid -- neither may collapse into the other, or a screen reader's
        // grid navigation would run the two months together as one 12-row table.
        AssertAriaNesting(await grids.Nth(0).AriaSnapshotAsync(), "grid", "row", "gridcell");
        AssertAriaNesting(await grids.Nth(1).AriaSnapshotAsync(), "grid", "row", "gridcell");
    }
}
