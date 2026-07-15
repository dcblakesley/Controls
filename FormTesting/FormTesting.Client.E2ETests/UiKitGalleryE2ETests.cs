namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for the UI Kit gallery page (/uikit). It is a standalone route (not the form-demo
/// view switcher), so this test drives the page directly rather than via PageTestBase.
/// </summary>
[Collection(PlaywrightCollection.Name)]
public class UiKitGalleryE2ETests : IAsyncLifetime
{
    readonly AppFixture _app;
    readonly BrowserFixture _browser;
    IBrowserContext _context = default!;
    IPage _page = default!;

    public UiKitGalleryE2ETests(AppFixture app, BrowserFixture browser)
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

    [Fact]
    public async Task Gallery_renders_core_controls()
    {
        await GotoAsync();
        await Expect(_page.Locator(".wss-alert").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-table").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-pagination").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Modal_opens_on_button_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        await Expect(_page.Locator(".wss-modal")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Modal_traps_focus_when_shift_tabbing_from_the_panel()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();

        // Wait until the focus trap has activated (it moves focus into the panel on open).
        await _page.WaitForFunctionAsync(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");

        // Focus the panel itself (tabindex=-1, as if the user clicked an empty area of the body),
        // then Shift+Tab. The old trap only caught Tab on the first/last item, so focus on the panel
        // escaped backwards to the page behind the overlay.
        await panel.EvaluateAsync("el => el.focus()");
        await _page.Keyboard.PressAsync("Shift+Tab");

        var trapped = await _page.EvaluateAsync<bool>(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");
        Assert.True(trapped);
    }

    [Fact]
    public async Task Modal_escape_still_closes_after_the_focused_footer_button_is_disabled()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Modal" }).ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]");
        await Expect(panel).ToBeVisibleAsync();
        await _page.WaitForFunctionAsync(
            "() => { const d = document.querySelector('.wss-modal[role=dialog]'); return !!d && d.contains(document.activeElement); }");

        // Simulate ConfirmLoading: focus the default OK button, then disable it. The browser silently
        // drops focus to <body> (no focusin fires), which used to strand the panel-scoped Escape
        // handler — Escape went dead until the user tabbed or clicked back into the panel.
        await _page.EvaluateAsync(
            "() => { const ok = document.querySelector('.wss-modal .wss-dialog-btn-primary'); ok.focus(); ok.disabled = true; }");
        await _page.Keyboard.PressAsync("Escape");

        await Expect(panel).ToBeHiddenAsync();
    }

    // KNOWN FLAKY (investigated extensively, unresolved): this test can fail with a blank
    // screenshot when it runs after another /uikit navigation in the same test process, on some
    // machines/environments. At capture time the DOM, the applied CSS, and computed styles for the
    // alert are all provably correct (verified directly via getComputedStyle) -- the discrepancy is
    // specifically between that and the actual painted pixels, which survives element-level
    // re-targeting, a hard page reload, a brand-new Page/context, double-requestAnimationFrame
    // paint sync, and Chromium flags disabling background-tab throttling and paint-holding. This
    // looks like a Chromium/Playwright compositing anomaly tied to this sandbox, not a product
    // regression. If this fails, verify on windows-latest CI (see ci.yml) or a real machine before
    // treating it as a real bug.
    [Fact]
    public async Task Alert_section_visual_baseline()
    {
        await GotoAsync();
        var alertSection = _page.Locator("section.demo-section").First; // first section is Alert
        await Expect(alertSection).ToBeVisibleAsync();
        var bytes = await alertSection.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-alert-section");
    }

    [Fact]
    public async Task Drawer_opens_and_matches_baseline()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Open Drawer" }).ClickAsync();
        var drawer = _page.Locator(".wss-drawer");
        await Expect(drawer).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-drawer-right")).ToBeVisibleAsync();
        await BaselineAsync(drawer, "drawer");
    }

    [Fact]
    public async Task Popover_opens_and_anchors_to_trigger()
    {
        await GotoAsync();
        // .First: the swapped-trigger demo section adds a second Popover to the page.
        await _page.Locator(".wss-popover-trigger").First.ClickAsync();
        var popover = _page.Locator(".wss-popover");
        await Expect(popover).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-popover-content")).ToContainTextAsync("popover content");
        await AssertAnchoredAboveAsync(".wss-popover-trigger", ".wss-popover");
    }

    [Fact]
    public async Task Popover_child_button_owns_the_popup_aria_and_keyboard_path()
    {
        await GotoAsync();

        // M7: the consumer's button is the trigger — the popup ARIA is mirrored onto it by JS and
        // the wrapper span carries no button semantics (it used to nest a button inside role="button").
        // .First: the swapped-trigger demo section adds a second Popover to the page.
        var wrapper = _page.Locator(".wss-popover-trigger").First;
        var button = _page.Locator(".wss-popover-trigger button").First;
        await Expect(button).ToHaveAttributeAsync("aria-haspopup", "dialog");
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "false");
        Assert.Null(await wrapper.GetAttributeAsync("role"));
        Assert.Null(await wrapper.GetAttributeAsync("tabindex"));
        Assert.Null(await wrapper.GetAttributeAsync("aria-expanded"));

        // Keyboard: Enter on the focused button opens exactly once (its native click bubbles to the
        // toggle; a duplicate key handler on the wrapper would instantly re-close).
        await button.FocusAsync();
        await _page.Keyboard.PressAsync("Enter");
        await Expect(_page.Locator(".wss-popover")).ToBeVisibleAsync();
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "true");

        // Escape closes and focus returns to the real trigger, not the wrapper.
        await _page.Keyboard.PressAsync("Escape");
        await Expect(_page.Locator(".wss-popover")).Not.ToBeVisibleAsync();
        await Expect(button).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(button).ToBeFocusedAsync();
    }

    [Fact]
    public async Task Popconfirm_anchors_to_trigger_then_confirms()
    {
        await GotoAsync();
        // .First: the swapped-trigger demo section adds a second (disabled) Popconfirm to the page.
        await _page.Locator(".wss-popconfirm-trigger").First.ClickAsync();
        var pop = _page.Locator(".wss-popconfirm");
        await Expect(pop).ToBeVisibleAsync();

        // Regression guard for the flex/grid stretch bug (left:50% on a full-width wrap): the panel
        // must be centred over the trigger and sit just above it, not drift to the section centre.
        await AssertAnchoredAboveAsync(".wss-popconfirm-trigger", ".wss-popconfirm");

        // The primary button confirms, closes the popover, and records the result.
        await _page.Locator(".wss-popconfirm-buttons .wss-dialog-btn-primary").ClickAsync();
        await Expect(_page.Locator(".wss-popconfirm")).Not.ToBeVisibleAsync();
        await Expect(_page.GetByText("Last action: confirmed")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Pagination_baselines_then_changes_page()
    {
        await GotoAsync();
        // The Table also renders a pager, so scope to the standalone Pagination demo (the first one).
        var pager = _page.Locator(".wss-pagination").First;
        await BaselineAsync(pager, "pagination");

        await pager.Locator(".wss-pagination-item").Nth(2).ClickAsync(); // page 3
        await Expect(_page.GetByText("Current page: 3")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Table_select_all_checkbox_reaches_the_indeterminate_state()
    {
        await GotoAsync();
        // The gallery has more than one table, so scope to the first -- the selectable/sortable one
        // (also the UseStyledCheckbox demo; the indeterminate DOM property + wss-table.js wiring
        // this test covers are identical either way, only the visual glyph differs).
        var table = _page.Locator(".wss-table").First;
        var header = table.Locator(".wss-table-thead .wss-table-checkbox");
        var rows = table.Locator(".wss-table-tbody .wss-table-checkbox");
        await Expect(header).ToBeVisibleAsync();

        // The demo preselects row Id 1 on this 13-row/5-per-page table, so page 1 starts with the
        // header already mixed (some but not all of the page selected).
        Assert.False(await header.IsCheckedAsync());
        Assert.True(await header.EvaluateAsync<bool>("el => el.indeterminate"));

        // Select the rest of the page → fully checked, no longer indeterminate.
        await header.ClickAsync();
        await Expect(header).ToBeCheckedAsync();
        Assert.False(await header.EvaluateAsync<bool>("el => el.indeterminate"));
        await Expect(rows.First).ToBeCheckedAsync();

        // Clear all → neither.
        await header.ClickAsync();
        await Expect(header).Not.ToBeCheckedAsync();
        Assert.False(await header.EvaluateAsync<bool>("el => el.indeterminate"));
    }

    [Fact]
    public async Task Table_styled_checkbox_renders_the_custom_box_and_the_indeterminate_square()
    {
        await GotoAsync();
        var table = _page.Locator(".wss-table").First;
        var header = table.Locator(".wss-table-thead .wss-table-checkbox-input-styled");
        var headerBox = table.Locator(".wss-table-thead .wss-table-checkbox-box");
        await Expect(header).ToBeVisibleAsync();

        // The demo preselects row Id 1 on a 13-row/5-per-page table, so page 1 starts indeterminate
        // (a DOM property with no HTML attribute, set from C# via wss-table.js). Per the AntD mixed
        // state, the box itself stays unfilled — the primary color appears only as the centered
        // square drawn by the ::after.
        Assert.True(await header.EvaluateAsync<bool>("el => el.indeterminate"));
        var mixedBoxColor = await headerBox.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        var mixedSquareColor = await headerBox.EvaluateAsync<string>("el => getComputedStyle(el, '::after').backgroundColor");
        Assert.NotEqual(mixedBoxColor, mixedSquareColor);

        // Clicking a partially-selected header selects the rest of the page (AntD convention): fully
        // checked, no longer indeterminate — now the box itself fills with the same primary the
        // mixed-state square used.
        await header.ClickAsync();
        await Expect(header).ToBeCheckedAsync();
        Assert.False(await header.EvaluateAsync<bool>("el => el.indeterminate"));
        var checkedColor = await headerBox.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.Equal(mixedSquareColor, checkedColor);

        // Clearing the selection returns the box to its unfilled appearance — same as the mixed box.
        await header.ClickAsync();
        var clearedColor = await headerBox.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        Assert.Equal(mixedBoxColor, clearedColor);
        Assert.NotEqual(checkedColor, clearedColor);
    }

    [Fact]
    public async Task Table_styled_checkbox_row_box_reflects_the_checked_row_too()
    {
        await GotoAsync();
        var table = _page.Locator(".wss-table").First;
        // Row 1 (Item 1) is preselected by the demo; rows 2-5 are not.
        var checkedRowBox = table.Locator(".wss-table-tbody .wss-table-checkbox-box").First;
        var uncheckedRowBox = table.Locator(".wss-table-tbody .wss-table-checkbox-box").Nth(1);
        await Expect(checkedRowBox).ToBeVisibleAsync();

        var checkedColor = await checkedRowBox.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        var uncheckedColor = await uncheckedRowBox.EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");
        // The header is mixed here (AntD: unfilled box + primary ::after square), so the shared
        // primary color to compare against is the square's, not the header box's.
        var headerMixedSquareColor = await table.Locator(".wss-table-thead .wss-table-checkbox-box")
            .EvaluateAsync<string>("el => getComputedStyle(el, '::after').backgroundColor");

        Assert.Equal(headerMixedSquareColor, checkedColor); // checked row fills with the same primary color
        Assert.NotEqual(checkedColor, uncheckedColor); // unchecked row stays unfilled
    }

    [Fact]
    public async Task Table_styled_checkbox_visual_baseline_indeterminate()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "styled checkboxes" });
        await BaselineAsync(section, "table-styled-checkbox-indeterminate");
    }

    [Fact]
    public async Task Table_sorting_a_column_reorders_rows_and_sets_aria_sort()
    {
        await GotoAsync();
        // The gallery has more than one table (the server-paging demo also has an "Id" column),
        // so scope to the first table — the selectable, sortable one.
        var table = _page.Locator(".wss-table").First;
        var idTrigger = table.Locator(".wss-table-sort-trigger", new() { HasTextString = "Id" });
        var idHeader = table.Locator(".wss-table-thead th").Filter(new() { HasTextString = "Id" });
        // First data cell is the selection checkbox (col 0); the Id value is col 1.
        var firstIdCell = table.Locator(".wss-table-tbody .wss-table-row").First.Locator("td").Nth(1);

        // Page 1 starts in the original (ascending) order: Id 1 first.
        await Expect(firstIdCell).ToHaveTextAsync("1");

        // 1st click = ascending (already ascending here); 2nd click = descending -> Id 13 first.
        await idTrigger.ClickAsync();
        await Expect(idHeader).ToHaveAttributeAsync("aria-sort", "ascending");
        await idTrigger.ClickAsync();
        await Expect(idHeader).ToHaveAttributeAsync("aria-sort", "descending");
        await Expect(firstIdCell).ToHaveTextAsync("13");

        // 3rd click clears the sort -> original order restored, aria-sort "none".
        await idTrigger.ClickAsync();
        await Expect(idHeader).ToHaveAttributeAsync("aria-sort", "none");
        await Expect(firstIdCell).ToHaveTextAsync("1");
    }

    [Fact]
    public async Task Server_paging_demo_swaps_the_page_on_pager_click()
    {
        await GotoAsync();
        // The server-paging demo is the last table on the page (no selection column -> Id is col 0),
        // and its standalone pager is the last .wss-pagination.
        var table = _page.Locator(".wss-table").Last;
        var firstId = table.Locator(".wss-table-tbody .wss-table-row").First.Locator("td").First;
        await Expect(firstId).ToHaveTextAsync("1"); // page 1 -> Row 1

        await _page.Locator(".wss-pagination").Last
            .Locator(".wss-pagination-item", new() { HasTextString = "2" }).ClickAsync();

        await Expect(firstId).ToHaveTextAsync("11"); // page 2 -> Row 11 (PageSize 10), proving the fetch ran
    }

    [Fact]
    public async Task Expandable_table_toggles_the_nested_detail_and_matches_baseline()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "expandable rows" });
        var firstChevron = section.Locator(".wss-table-expand-btn").First;

        await firstChevron.ClickAsync();
        var detail = section.Locator(".wss-table-expanded-row");
        await Expect(detail).ToBeVisibleAsync();
        // The detail hosts the nested selectable child table (the Vendor PO pattern).
        await Expect(detail.Locator(".wss-table-row")).ToHaveCountAsync(2);
        await Expect(firstChevron).ToHaveAttributeAsync("aria-expanded", "true");

        await BaselineAsync(section, "table-expandable-open");

        await firstChevron.ClickAsync();
        await Expect(detail).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Tabs_arrow_key_moves_selection_and_focus()
    {
        await GotoAsync();
        var tabs = _page.Locator(".wss-tabs [role=tab]");
        await Expect(tabs.Nth(1)).ToHaveAttributeAsync("aria-selected", "true"); // pinned "missing"

        await tabs.Nth(1).FocusAsync();
        await _page.Keyboard.PressAsync("ArrowRight");

        await Expect(tabs.Nth(2)).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(tabs.Nth(2)).ToBeFocusedAsync(); // FocusAsync moved the roving tab stop
        await Expect(_page.Locator("[data-test-id='tabs-result']")).ToContainTextAsync("Active: other");
    }

    [Fact]
    public async Task Search_input_commits_on_enter_and_on_the_button()
    {
        await GotoAsync();
        await _page.Locator("#demo-search-pos").FillAsync("8999");
        await _page.Locator("#demo-search-pos").PressAsync("Enter");
        await Expect(_page.Locator("[data-test-id='tabs-result']")).ToContainTextAsync("POs: 8999");

        await _page.Locator("#demo-search-skus").FillAsync("150005");
        // The SKUs field's own search button (second .wss-search-btn on the page).
        await _page.Locator(".wss-search", new() { Has = _page.Locator("#demo-search-skus") })
            .Locator(".wss-search-btn").ClickAsync();
        await Expect(_page.Locator("[data-test-id='tabs-result']")).ToContainTextAsync("SKUs: 150005");
    }

    [Fact]
    public async Task Tabs_and_search_section_visual_baseline()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Tabs + SearchInput" });
        await BaselineAsync(section, "tabs-search-section");
    }

    [Fact]
    public async Task Pill_select_opens_picks_and_closes_on_outside_click()
    {
        await GotoAsync();
        var pill = _page.Locator(".wss-select-pill").First;
        var dropdown = _page.Locator(".wss-select-pill .wss-select-dropdown");

        await pill.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();

        // The current value renders as the bold/tinted row, with the checkmark glyph suppressed
        // (pill dropdowns convey selection by the row treatment alone).
        var selected = _page.Locator(".wss-select-item-option-selected");
        await Expect(selected).ToContainTextAsync("All shipments");
        await Expect(selected.Locator(".wss-select-item-option-state")).ToBeHiddenAsync();

        // Picking an option commits the binding and closes the dropdown.
        await _page.Locator(".wss-select-item-option", new() { HasTextString = "Drop shipments" }).ClickAsync();
        await Expect(dropdown).ToBeHiddenAsync();
        await Expect(_page.Locator("[data-test-id=pill-result]")).ToContainTextAsync("drop");

        // Reopen; a click anywhere outside (the backdrop) closes without changing the value.
        await pill.ClickAsync();
        await Expect(dropdown).ToBeVisibleAsync();
        await _page.Locator(".wss-select-backdrop").ClickAsync(new LocatorClickOptions
        {
            Position = new() { X = 5, Y = 5 }, // far corner — the center may be covered by the panel
        });
        await Expect(dropdown).ToBeHiddenAsync();
        await Expect(_page.Locator("[data-test-id=pill-result]")).ToContainTextAsync("drop");
    }

    [Fact]
    public async Task Pill_select_section_visual_baseline()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "pill filter variant" });
        await BaselineAsync(section, "pill-select-section");
    }

    [Fact]
    public async Task Message_toast_appears_on_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Message" }).ClickAsync();
        await Expect(_page.Locator(".wss-msg")).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-msg-content")).ToContainTextAsync("Saved!");
    }

    [Fact]
    public async Task Notification_appears_on_click()
    {
        await GotoAsync();
        await _page.Locator("button", new() { HasTextString = "Notification" }).ClickAsync();
        await Expect(_page.Locator(".wss-notification")).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-notification-message")).ToContainTextAsync("Notification");
    }

    // Asserts an overlay panel is centred over its trigger and sits just above it (Top placement).
    // A precise geometric guard for anchoring — more reliable than a screenshot for an absolutely
    // -positioned overlay that can overflow the viewport when the trigger is near an edge.
    async Task AssertAnchoredAboveAsync(string triggerSelector, string panelSelector)
    {
        var panel = _page.Locator(panelSelector);
        // Edge-aware positioning: auto-wait for the JS flip/shift to settle and assert the panel
        // is fully within the viewport (Ratio = 1 ⇒ no part overflows the edge).
        await Expect(panel).ToBeInViewportAsync(new() { Ratio = 1 });

        // .First: trigger selectors can match the swapped-trigger demo section's second instance too.
        var t = await _page.Locator(triggerSelector).First.BoundingBoxAsync();
        var p = await panel.BoundingBoxAsync();
        Assert.NotNull(t);
        Assert.NotNull(p);
        var triggerCenterX = t!.X + t.Width / 2;
        // The panel still covers the trigger (so the arrow points at it) ...
        Assert.InRange(triggerCenterX, p!.X, p.X + p.Width);
        // ... and sits just above the trigger (Top placement, ~10px gap).
        var gapAbove = t.Y - (p.Y + p.Height);
        Assert.InRange(gapAbove, -2.0, 40.0);
    }

    async Task BaselineAsync(ILocator locator, string name)
    {
        await Expect(locator).ToBeVisibleAsync();
        var bytes = await locator.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Animations = ScreenshotAnimations.Disabled,
            Type = ScreenshotType.Png,
        });
        VisualRegression.Assert(bytes, $"{GetType().Name}-{name}");
    }
}
