using System.Text.RegularExpressions;

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
        // Scoped by section (rather than .wss-table/.wss-pagination .Last) so appending further
        // demo sections below this one -- each with their own Table/Pagination -- can never shift
        // which element these ordinal-free locators resolve to.
        var section = _page.Locator("section.demo-section", new() { HasTextString = "server-side paging" });
        var table = section.Locator(".wss-table");
        var firstId = table.Locator(".wss-table-tbody .wss-table-row").First.Locator("td").First;
        await Expect(firstId).ToHaveTextAsync("1"); // page 1 -> Row 1

        await section.Locator(".wss-pagination")
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

    // ---- AntD 4.x parity batch: Pagination + Table ----

    [Fact]
    public async Task Pagination_size_changer_and_quick_jumper_drive_the_pager()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "size changer, quick jumper" });
        var pager = section.Locator(".wss-pagination").First;
        var result = _page.Locator("[data-test-id=pagination-demo-result]");

        await Expect(result).ToContainTextAsync("Page 1, size 10");
        await Expect(pager.Locator(".wss-pagination-total")).ToContainTextAsync("1-10 of 95 items");

        // Size changer: picking 20 re-clamps the page (first item index 0 -> floor(0/20)+1 = 1).
        await pager.Locator(".wss-pagination-size-select").SelectOptionAsync("20");
        await Expect(result).ToContainTextAsync("Page 1, size 20");
        await Expect(pager.Locator(".wss-pagination-total")).ToContainTextAsync("1-20 of 95 items");

        // Quick jumper: typing a page and pressing Enter jumps directly.
        var jumperInput = pager.Locator(".wss-pagination-jumper-input");
        await jumperInput.FillAsync("3");
        await jumperInput.PressAsync("Enter");
        await Expect(result).ToContainTextAsync("Page 3, size 20");
        await Expect(jumperInput).ToHaveValueAsync(string.Empty); // clears after commit
    }

    [Fact]
    public async Task Pagination_small_variant_renders_the_compact_modifier_class()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "size changer, quick jumper" });
        // The second Pagination in this section demos Small -- distinguish it from the first pager
        // (which also matches .wss-pagination) by its own modifier class.
        var smallPager = section.Locator(".wss-pagination.wss-pagination-sm");
        await Expect(smallPager).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Table_loading_overlay_shows_and_hides_over_still_visible_rows()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Loading overlay, disabled rows" });
        // aria-busy now lives on .wss-table-root (it spans the pagers too, not just the wrapper).
        var root = section.Locator(".wss-table-root");
        var mask = section.Locator(".wss-table-loading-mask");
        var toggle = _page.Locator("[data-test-id=toggle-table-loading]");

        await Expect(mask).Not.ToBeVisibleAsync();
        await Expect(root).Not.ToHaveAttributeAsync("aria-busy", "true");

        await toggle.ClickAsync();
        await Expect(mask).ToBeVisibleAsync();
        await Expect(root).ToHaveAttributeAsync("aria-busy", "true");
        // Rows stay rendered beneath the translucent mask, not replaced by it.
        await Expect(section.Locator(".wss-table-tbody .wss-table-row").First).ToBeVisibleAsync();

        await toggle.ClickAsync();
        await Expect(mask).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Table_single_select_mode_uses_radios_and_disables_the_configured_row()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Loading overlay, disabled rows" });
        var radios = section.Locator("tbody input[type=radio].wss-table-radio");
        var result = _page.Locator("[data-test-id=single-select-result]");

        await Expect(radios).ToHaveCountAsync(3);
        await Expect(section.Locator("thead input")).ToHaveCountAsync(0); // no select-all control
        await Expect(radios.Nth(1)).ToBeDisabledAsync(); // row 2 (Bravo) is IsRowSelectable="false"

        await radios.Nth(0).CheckAsync();
        await Expect(result).ToContainTextAsync("Alpha");

        await radios.Nth(2).CheckAsync();
        await Expect(result).ToContainTextAsync("Charlie"); // picking another row replaces the selection
        await Expect(radios.Nth(0)).Not.ToBeCheckedAsync();
    }

    [Fact]
    public async Task Table_expand_row_by_click_toggles_the_detail_and_still_raises_OnRowClick()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "OnRowClick, ExpandRowByClick" });
        var firstRow = section.Locator("tbody .wss-table-row").First;
        var detail = section.Locator("[data-test-id=row-detail]");
        var result = _page.Locator("[data-test-id=row-click-result]");

        await Expect(detail).ToHaveCountAsync(0);

        await firstRow.ClickAsync();
        await Expect(result).ToContainTextAsync("First"); // OnRowClick fired
        await Expect(detail).ToContainTextAsync("Detail for First"); // and expansion toggled

        await firstRow.ClickAsync();
        await Expect(detail).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Table_ellipsis_footer_and_empty_content_section_visual_baseline()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Ellipsis, EmptyContent, FooterContent" });
        await Expect(section.Locator("[data-test-id=ellipsis-footer-total]")).ToContainTextAsync("$19.75");
        await Expect(section.Locator("[data-test-id=empty-content]")).ToBeVisibleAsync();
        await BaselineAsync(section, "table-ellipsis-footer-empty");
    }

    [Fact]
    public async Task Table_filter_OK_narrows_the_rows_and_shows_the_active_icon_state()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "column filtering" });
        var filterButton = section.Locator(".wss-table-filter-trigger[aria-label='Filter Name']");
        var rows = section.Locator("tbody .wss-table-row");

        await Expect(rows).ToHaveCountAsync(10);
        await Expect(filterButton).Not.ToHaveClassAsync(new Regex("wss-table-filter-active"));

        await filterButton.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(filterButton).ToHaveAttributeAsync("aria-expanded", "true");

        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 5" }).Locator("input").CheckAsync();
        await dropdown.Locator(".wss-table-filter-ok").ClickAsync();

        await Expect(dropdown).Not.ToBeVisibleAsync(); // OK applies and closes
        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.First).ToContainTextAsync("Item 5");
        await Expect(filterButton).ToHaveClassAsync(new Regex("wss-table-filter-active"));
        await Expect(_page.Locator("[data-test-id=filter-demo-result]")).ToContainTextAsync("Name: Item 5");
    }

    [Fact]
    public async Task Table_filter_AND_across_two_columns_then_Reset_restores_every_row()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "column filtering" });
        var rows = section.Locator("tbody .wss-table-row");

        // Name in {Item 2, Item 5, Item 8} AND Price >= $20 (Item 5 = $25, Item 8 = $40 qualify;
        // Item 2 = $10 doesn't) -> AND narrows to Item 5 + Item 8 only.
        var nameFilter = section.Locator(".wss-table-filter-trigger[aria-label='Filter Name']");
        await nameFilter.ClickAsync();
        var nameDropdown = section.Locator(".wss-table-filter-dropdown");
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 2" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 5" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 8" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-ok").ClickAsync();
        await Expect(rows).ToHaveCountAsync(3);

        var priceFilter = section.Locator(".wss-table-filter-trigger[aria-label='Filter Price']");
        await priceFilter.ClickAsync();
        var priceDropdown = section.Locator(".wss-table-filter-dropdown");
        await priceDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "$20 and over" }).Locator("input").CheckAsync();
        await priceDropdown.Locator(".wss-table-filter-ok").ClickAsync();

        await Expect(rows).ToHaveCountAsync(2);
        await Expect(section.Locator("tbody")).ToContainTextAsync("Item 5");
        await Expect(section.Locator("tbody")).ToContainTextAsync("Item 8");
        await Expect(section.Locator("tbody")).Not.ToContainTextAsync("Item 2");

        // Reset the Name column only -- Price's filter stays applied.
        await priceFilter.ClickAsync(); // re-open Price to confirm it's still marked active
        await Expect(priceFilter).ToHaveClassAsync(new Regex("wss-table-filter-active"));
        await _page.Keyboard.PressAsync("Escape"); // close without changing anything

        await nameFilter.ClickAsync();
        await section.Locator(".wss-table-filter-dropdown .wss-table-filter-reset").ClickAsync();
        await Expect(nameFilter).Not.ToHaveClassAsync(new Regex("wss-table-filter-active"));
        await Expect(_page.Locator("[data-test-id=filter-demo-result]")).ToContainTextAsync("Name: cleared");

        // Price filter alone now drives the row set (Item 4/6/7/8/9/10 are all >= $20 alongside 5/8).
        await Expect(rows).Not.ToHaveCountAsync(10);
    }

    [Fact]
    public async Task Table_filter_dropdown_outside_click_closes_without_applying_pending_changes()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "column filtering" });
        var filterButton = section.Locator(".wss-table-filter-trigger[aria-label='Filter Name']");
        var rows = section.Locator("tbody .wss-table-row");

        await filterButton.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 2" }).Locator("input").CheckAsync();

        // Click the invisible backdrop (anywhere outside the dropdown) instead of OK.
        await section.Locator(".wss-table-filter-backdrop").ClickAsync(new() { Position = new Position { X = 5, Y = 5 } });

        await Expect(dropdown).Not.ToBeVisibleAsync();
        await Expect(rows).ToHaveCountAsync(10); // unfiltered -- nothing was applied
        await Expect(filterButton).Not.ToHaveClassAsync(new Regex("wss-table-filter-active"));

        // Re-opening must not resurrect the discarded check -- it re-syncs from the (still empty)
        // applied selection, not from whatever was left pending.
        await filterButton.ClickAsync();
        var reopened = section.Locator(".wss-table-filter-dropdown .wss-table-filter-item", new() { HasTextString = "Item 2" }).Locator("input");
        await Expect(reopened).Not.ToBeCheckedAsync();
    }

    [Fact]
    public async Task Table_ScrollY_header_stays_sticky_while_the_body_scrolls()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "ScrollY (sticky header)" });
        var wrapper = section.Locator(".wss-table-wrapper");
        var headerCell = section.Locator("thead th").First;

        await Expect(wrapper).ToHaveClassAsync(new Regex("wss-table-wrapper-scroll-y"));
        await Expect(headerCell).ToHaveCSSAsync("position", "sticky");

        var beforeTop = await headerCell.EvaluateAsync<double>("el => el.getBoundingClientRect().top");

        var scrollTop = await wrapper.EvaluateAsync<double>("el => { el.scrollTop = 400; return el.scrollTop; }");
        Assert.True(scrollTop > 0); // the wrapper is genuinely scrollable, and it scrolled

        var afterTop = await headerCell.EvaluateAsync<double>("el => el.getBoundingClientRect().top");
        Assert.Equal(beforeTop, afterTop, 3); // sticky: the header's viewport position doesn't move
    }

    [Fact]
    public async Task Table_ScrollY_filter_dropdown_escapes_the_wrapper_overflow_clip()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "ScrollY (sticky header)" });
        var filterButton = section.Locator(".wss-table-filter-trigger");
        var rows = section.Locator("tbody .wss-table-row");

        await filterButton.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();

        // JS repositions the dropdown to position: fixed once ScrollY makes clipping possible (see
        // wss-overlay.js's placeFixedBelow) -- confirms the escape path actually ran, not just that
        // the dropdown "happened to fit" within the 160px wrapper.
        await Expect(dropdown).ToHaveCSSAsync("position", "fixed");

        // The dropdown (6 options + footer) is taller than the 160px ScrollY wrapper -- if it were
        // still clipped by the wrapper's overflow instead of escaping, checking/clicking an option
        // near the bottom of the list would fail Playwright's actionability checks.
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 25" }).Locator("input").CheckAsync();
        await dropdown.Locator(".wss-table-filter-ok").ClickAsync();

        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.First).ToContainTextAsync("Item 25");
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
