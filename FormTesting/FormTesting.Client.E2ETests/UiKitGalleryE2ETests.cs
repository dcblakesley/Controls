using System.Text.Json;
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
        // .First: the Placement demo section's second WasmNotificationContainer (bottom-left) reads
        // the same static service, so this same toast also renders there.
        await Expect(_page.Locator(".wss-notification").First).ToBeVisibleAsync();
        await Expect(_page.Locator(".wss-notification-message").First).ToContainTextAsync("Notification");
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
    public async Task Table_Enter_on_a_button_in_a_plain_column_activates_the_row_exactly_once()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "keyboard/pointer parity" });
        var button = section.Locator("[data-test-id=parity-cell-button]").First;
        var rowActivations = section.Locator("[data-test-id=parity-row-activations]");
        var buttonClicks = section.Locator("[data-test-id=parity-button-clicks]");

        await Expect(rowActivations).ToHaveTextAsync("Row activations: 0");

        // Enter on the focused button fires keydown on it AND makes the browser synthesize a click.
        // Both used to reach the row, so one keypress activated it twice; only the click does now.
        // This is the half bUnit cannot cover -- it never synthesizes the click (same single-fire
        // invariant the Popover child-button test above pins, for the same reason).
        await button.FocusAsync();
        await _page.Keyboard.PressAsync("Enter");
        await Expect(buttonClicks).ToHaveTextAsync("Button clicks: 1");
        await Expect(rowActivations).ToHaveTextAsync("Row activations: 1");

        // A second press advances each by exactly one again (the pre-fix double-fire read 4 here).
        await _page.Keyboard.PressAsync("Enter");
        await Expect(buttonClicks).ToHaveTextAsync("Button clicks: 2");
        await Expect(rowActivations).ToHaveTextAsync("Row activations: 2");

        // Pointer parity, the behavior the keyboard path has to match: a real mouse click on the same
        // button runs its own handler and bubbles into the row -- one activation, not zero and not two.
        await button.ClickAsync();
        await Expect(buttonClicks).ToHaveTextAsync("Button clicks: 3");
        await Expect(rowActivations).ToHaveTextAsync("Row activations: 3");
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
        var filterButton = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Name']");
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
        var nameFilter = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Name']");
        await nameFilter.ClickAsync();
        var nameDropdown = section.Locator(".wss-table-filter-dropdown");
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 2" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 5" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 8" }).Locator("input").CheckAsync();
        await nameDropdown.Locator(".wss-table-filter-ok").ClickAsync();
        await Expect(rows).ToHaveCountAsync(3);

        var priceFilter = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Price']");
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
        var filterButton = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Name']");
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
        await AssertDropdownHugsTheTriggerAsync(dropdown, filterButton, "right after open");

        // The dropdown (6 options + footer) is taller than the 160px ScrollY wrapper -- if it were
        // still clipped by the wrapper's overflow instead of escaping, checking/clicking an option
        // near the bottom of the list would fail Playwright's actionability checks.
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Item 25" }).Locator("input").CheckAsync();
        await dropdown.Locator(".wss-table-filter-ok").ClickAsync();

        await Expect(rows).ToHaveCountAsync(1);
        await Expect(rows.First).ToContainTextAsync("Item 25");
    }

    [Fact]
    public async Task Table_ScrollY_filter_OK_button_is_clickable_while_Loading_is_on()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "ScrollY (sticky header)" });
        var toggle = section.Locator("[data-test-id=toggle-scrolly-loading]");
        var mask = section.Locator(".wss-table-loading-mask");

        // Open the filter FIRST, while the table is still interactive, then flip Loading on with the
        // dropdown already open -- Loading masks the whole table (by design, the mask is meant to
        // block interaction), so the trigger button itself is rightfully unreachable once Loading is
        // already on; the bug this guards is that an ALREADY-open dropdown's OK button became
        // unreachable too, which is never the intended behavior.
        var filterButton = section.Locator(".wss-table-filter-trigger");
        await filterButton.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        var ok = dropdown.Locator(".wss-table-filter-ok");

        // A direct DOM .click() (not a coordinate-based Playwright click): the now-open dropdown can
        // itself visually overlap the toggle button (it's placed right above the table) badly enough
        // that even a forced coordinate click could still land on the dropdown instead -- that's an
        // artifact of this demo's layout, not something this click needs to respect.
        await toggle.EvaluateAsync("el => el.click()");
        await Expect(mask).ToBeVisibleAsync();
        await Expect(ok).ToBeVisibleAsync(); // still open, unaffected by the mask appearing

        // Regression guard for the sticky-header stacking-context trap (Fix 1): confirm the topmost
        // element at the OK button's own coordinates is the button itself, not the loading mask
        // painting over it. Computed in ONE JS round trip scoped to the resolved element (rect +
        // elementFromPoint together) rather than threading a separately-fetched bounding box back
        // into a second call -- avoids a race against any still-in-flight Blazor Server render batch.
        var topElementIsOk = await ok.EvaluateAsync<bool>(@"el => {
            const r = el.getBoundingClientRect();
            const x = r.left + r.width / 2;
            const y = r.top + r.height / 2;
            const top = document.elementFromPoint(x, y);
            return top === el || el.contains(top);
        }");
        Assert.True(topElementIsOk);

        // And an actual click lands on it -- Playwright's own actionability check would time out
        // here if the mask were still intercepting the click.
        await ok.ClickAsync();
        await Expect(dropdown).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Table_ScrollY_filter_dropdown_tracks_the_trigger_on_page_scroll()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "ScrollY (sticky header)" });
        var filterButton = section.Locator(".wss-table-filter-trigger");

        // Position the trigger toward the CENTER of the viewport, not an edge -- the ScrollY demo is
        // the last section on the page, and a trigger placed right at the top/bottom edge can cross
        // the fixed dropdown's own above/below flip threshold (see placeFixedBelow's flip logic in
        // wss-overlay.js) partway through a subsequent scroll, which changes the trigger-to-dropdown
        // gap relationship for a reason that has nothing to do with this fix. A small scroll amount
        // below keeps the trigger comfortably on-screen throughout, so the flip state can't change.
        // behavior: 'instant' -- the host's reboot CSS sets scroll-behavior: smooth on :root, so a
        // default-behavior scrollIntoView animates and everything below reads a mid-flight layout.
        await ScrollIntoViewAsync(filterButton);
        await filterButton.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        await Expect(dropdown).ToHaveCSSAsync("position", "fixed");

        var triggerBefore = await filterButton.BoundingBoxAsync();
        var dropdownBefore = await dropdown.BoundingBoxAsync();
        Assert.NotNull(triggerBefore);
        Assert.NotNull(dropdownBefore);

        // window.scrollTo with an explicit behavior: 'instant' -- Bootstrap's reboot CSS sets
        // `scroll-behavior: smooth` on :root, which makes scrollTo/scrollBy/scrollIntoView (when
        // 'behavior' is left to default) animate asynchronously instead of jumping, so reading
        // scrollY right back would still show the pre-scroll value; 'instant' explicitly overrides
        // that CSS per spec. A wheel event over the trigger, separately, would scroll the TABLE's own
        // ScrollY wrapper (overflow-y: auto) instead of the page, which wouldn't move a sticky
        // header's viewport position at all.
        var scrollDelta = await _page.EvaluateAsync<double>(@"() => {
            const before = window.scrollY;
            window.scrollTo({ top: Math.max(0, before - 100), behavior: 'instant' });
            return before - window.scrollY;
        }");
        Assert.True(Math.Abs(scrollDelta) > 5, $"scrollDelta={scrollDelta}"); // the page genuinely scrolled

        // The browser dispatches a programmatic scroll's 'scroll' event asynchronously (a later task,
        // not synchronously within the script that called scrollTo), so the reposition it triggers
        // may not have run yet the instant our EvaluateAsync call above returns -- poll briefly for
        // the dropdown to catch up instead of reading a possibly-stale layout immediately.
        float triggerDelta = 0, dropdownDelta = 0;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var triggerAfter = await filterButton.BoundingBoxAsync();
            var dropdownAfter = await dropdown.BoundingBoxAsync();
            Assert.NotNull(triggerAfter);
            Assert.NotNull(dropdownAfter);
            triggerDelta = triggerBefore!.Y - triggerAfter!.Y; // positive: moved up as the page scrolled down
            dropdownDelta = dropdownBefore!.Y - dropdownAfter!.Y;
            if (Math.Abs(triggerDelta - dropdownDelta) < 0.5) break;
            await Task.Delay(50);
        }
        Assert.Equal(triggerDelta, dropdownDelta, 1); // tracked the trigger instead of staying stuck

        // A reposition re-measures offsetWidth: leaving the stylesheet's `right: 0` in place alongside
        // the written `left` made the used width `viewport - left`, which each scroll then compounded.
        await AssertDropdownHugsTheTriggerAsync(dropdown, filterButton, "after a page scroll");
    }

    // The fixed-positioned filter panel is right-aligned under its funnel (AntD's default) and sized
    // by its own content -- never stretched to the viewport edge.
    static async Task AssertDropdownHugsTheTriggerAsync(ILocator dropdown, ILocator trigger, string when)
    {
        var panelBox = await dropdown.BoundingBoxAsync();
        var triggerBox = await trigger.BoundingBoxAsync();
        Assert.NotNull(panelBox);
        Assert.NotNull(triggerBox);
        Assert.True(panelBox!.Width < 400,
            $"{when}: panel width={panelBox.Width} (viewport 1280) -- it stretched to the viewport edge");
        var panelRight = panelBox.X + panelBox.Width;
        var triggerRight = triggerBox!.X + triggerBox.Width;
        Assert.True(Math.Abs(panelRight - triggerRight) <= 4,
            $"{when}: panel right={panelRight} trigger right={triggerRight}");
    }


    [Fact]
    public async Task Table_sortable_filterable_header_keeps_the_filter_button_inside_a_narrow_th()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "narrow fixed-width column" });
        var th = section.Locator("thead th").First;
        var filterButton = section.Locator(".wss-table-filter-trigger");
        var sortTrigger = section.Locator(".wss-table-sort-trigger");

        await Expect(filterButton).ToBeVisibleAsync();
        await Expect(sortTrigger).ToBeVisibleAsync();

        var thBox = await th.BoundingBoxAsync();
        var filterBox = await filterButton.BoundingBoxAsync();
        Assert.NotNull(thBox);
        Assert.NotNull(filterBox);

        // Before the fix, the sort label's unshrinkable min-content width pushed the filter button
        // past the cell's right edge instead of letting the label truncate.
        Assert.True(filterBox!.X + filterBox.Width <= thBox!.X + thBox.Width + 0.5);
        Assert.True(filterBox.X >= thBox.X - 0.5);
    }

    // ---- Table filtering expansion: Text/Custom kinds, FilterPlacement.Row + type-derived editors,
    // the filter row under ScrollY, and the dropdown extras. Every test below anchors its section by
    // HasTextString rather than by position, so further sections can be appended anywhere. ----

    ILocator TextAndCustomFilterSection =>
        _page.Locator("section.demo-section", new() { HasTextString = "text and custom filters" });

    ILocator FilterRowSection =>
        _page.Locator("section.demo-section", new() { HasTextString = "filter row (FilterPlacement.Row)" });

    ILocator ScrollYFilterRowSection =>
        _page.Locator("section.demo-section", new() { HasTextString = "filter row under ScrollY" });

    ILocator DropdownExtrasSection =>
        _page.Locator("section.demo-section", new() { HasTextString = "dropdown extras" });

    // A row-placement editor is a kit control named after its column ("Filter by {header}"), so the
    // accessible name is the stable handle onto a specific cell's Select/box — no positional
    // nth-child into a header row whose column order a later demo edit could change.
    static ILocator FilterRowSelect(ILocator section, string label) =>
        section.Locator($".wss-table-filter-row .wss-select:has(input[aria-label='{label}'])");

    [Fact]
    public async Task Table_text_filter_applies_on_Enter_and_reports_its_description()
    {
        await GotoAsync();
        var section = TextAndCustomFilterSection;
        var trigger = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Name']");
        var rows = section.Locator("tbody .wss-table-row");
        var summary = section.Locator("[data-test-id=filter-summary-1]");

        await Expect(rows).ToHaveCountAsync(6);
        await trigger.ClickAsync();
        var box = section.Locator(".wss-table-filter-dropdown input.wss-table-filter-input");
        await Expect(box).ToBeVisibleAsync();

        // Enter is the OK button's keyboard twin for APPLYING. Deliberately not asserting that the
        // panel closed: a real Enter keypress leaves it open. TableFilterEditor.OnEditorKeyDown does
        // apply-and-close, and TableColumnFilter's close path then restores focus to the funnel
        // button — after which the SAME keydown's browser default action activates that now-focused
        // button and re-opens the panel. (Verified: a synthetic, untrusted keydown, which carries no
        // default action, closes it and leaves it closed.) OK-closes-the-panel is covered by
        // Table_filter_OK_narrows_the_rows_and_shows_the_active_icon_state.
        await box.FillAsync("gasket");
        await box.PressAsync("Enter");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(trigger).ToHaveClassAsync(new Regex("wss-table-filter-active"));
        await Expect(summary).ToContainTextAsync("Name: contains \"gasket\"");

        // A panel showing again re-stages from the APPLIED text, and the clear button empties the box
        // without applying anything — the rows stay narrowed until OK/Enter says otherwise.
        await Expect(box).ToHaveValueAsync("gasket");
        await section.Locator(".wss-table-filter-dropdown .wss-table-filter-input-clear").ClickAsync();
        await Expect(box).ToHaveValueAsync("");
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(summary).ToContainTextAsync("Name: contains \"gasket\"");
    }

    [Fact]
    public async Task Table_text_filter_StartsWith_only_matches_a_leading_substring()
    {
        await GotoAsync();
        var section = TextAndCustomFilterSection;
        var trigger = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Code']");
        // The empty-state placeholder is a <tr class="wss-table-row wss-table-placeholder"> too, so
        // "how many DATA rows" has to exclude it.
        var rows = section.Locator("tbody .wss-table-row:not(.wss-table-placeholder)");
        var box = section.Locator(".wss-table-filter-dropdown input.wss-table-filter-input");

        await trigger.ClickAsync();
        await box.FillAsync("AX");
        await box.PressAsync("Enter");
        await Expect(rows).ToHaveCountAsync(2); // AX-100, AX-200
        await Expect(section.Locator("[data-test-id=filter-summary-1]"))
            .ToContainTextAsync("Code: starts with \"AX\"");

        // "X-1" IS a substring of AX-100 — under the default Contains match it would still match.
        // Typed straight into the panel Enter left open (see the Enter note in the test above)
        // rather than re-clicking the funnel, which the open panel's backdrop would intercept.
        await box.FillAsync("X-1");
        await box.PressAsync("Enter");
        await Expect(rows).ToHaveCountAsync(0);
        await Expect(section.Locator("tbody .wss-table-placeholder")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Table_custom_filter_dropdown_applies_closes_and_reports_open_changes()
    {
        await GotoAsync();
        var section = TextAndCustomFilterSection;
        var trigger = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Quantity']");
        var rows = section.Locator("tbody .wss-table-row");
        var openState = section.Locator("[data-test-id=widget-dropdown-open]");

        await Expect(openState).ToContainTextAsync("false");
        await trigger.ClickAsync();
        // OnFilterDropdownOpenChange fires on the transition, not on a render of the panel.
        await Expect(openState).ToContainTextAsync("true");
        await Expect(section.Locator(".wss-demo-filter-panel")).ToBeVisibleAsync();
        // A FilterDropdown template owns the WHOLE panel: no built-in option list, no OK/Reset footer.
        await Expect(section.Locator(".wss-table-filter-dropdown .wss-table-filter-ok")).ToHaveCountAsync(0);

        await section.Locator("[data-test-id=widget-custom-input]").FillAsync("40");
        await section.Locator("[data-test-id=widget-custom-apply]").ClickAsync();

        await Expect(section.Locator(".wss-table-filter-dropdown")).Not.ToBeVisibleAsync();
        await Expect(openState).ToContainTextAsync("false");
        await Expect(rows).ToHaveCountAsync(2); // quantity >= 40: 40 and 55
        await Expect(section.Locator("tbody")).ToContainTextAsync("Alpha sprocket");
        await Expect(section.Locator("tbody")).Not.ToContainTextAsync("Bravo gasket");
        // Custom shares the Options kind's keyed serialization, hence its "n selected" description.
        await Expect(section.Locator("[data-test-id=filter-summary-1]")).ToContainTextAsync("Quantity: 1 selected");
    }

    [Fact]
    public async Task Table_custom_filter_icon_reports_the_applied_state()
    {
        await GotoAsync();
        var section = TextAndCustomFilterSection;
        var trigger = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Quantity']");
        var icon = section.Locator("[data-test-id=widget-filter-icon]");
        var rows = section.Locator("tbody .wss-table-row");

        // FilterIcon replaces the funnel GLYPH only — the button, its classes and its name are the
        // built-in ones either way, so the applied state shows up in both places.
        await Expect(icon).ToHaveAttributeAsync("data-applied", "false");
        await Expect(icon).ToHaveTextAsync("#");
        await Expect(section.Locator(".wss-table-filter-trigger svg")).ToHaveCountAsync(2); // Name + Code keep the funnel

        await trigger.ClickAsync();
        await section.Locator("[data-test-id=widget-custom-input]").FillAsync("40");
        await section.Locator("[data-test-id=widget-custom-apply]").ClickAsync();

        await Expect(icon).ToHaveAttributeAsync("data-applied", "true");
        await Expect(icon).Not.ToHaveTextAsync("#"); // the glyph itself swapped, not just the flag
        await Expect(trigger).ToHaveClassAsync(new Regex("wss-table-filter-active"));

        // The template's own Reset button is ctx.ResetAsync() — the built-in Reset path.
        await trigger.ClickAsync();
        await section.Locator("[data-test-id=widget-custom-reset]").ClickAsync();
        await Expect(icon).ToHaveAttributeAsync("data-applied", "false");
        await Expect(rows).ToHaveCountAsync(6);
        await Expect(section.Locator("[data-test-id=filter-summary-1]")).ToContainTextAsync("(no filters)");
    }

    [Fact]
    public async Task Table_filter_row_text_editor_narrows_after_the_debounce()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");
        var summary = section.Locator("[data-test-id=filter-summary-2]");

        // Row placement renders editors, never funnels.
        await Expect(section.Locator(".wss-table-filter-trigger")).ToHaveCountAsync(0);
        await Expect(section.Locator("table")).ToHaveClassAsync(new Regex("wss-table-has-filter-row"));
        await Expect(rows).ToHaveCountAsync(30);

        // Typing commits itself after FilterDebounceMilliseconds of quiet — there is no OK here.
        await section.Locator("input[aria-label='Filter by Name']").FillAsync("mixer");
        await Expect(rows).ToHaveCountAsync(6);
        await Expect(summary).ToContainTextAsync("Name: contains \"mixer\"");
    }

    [Fact]
    public async Task Table_filter_row_number_range_is_inclusive_at_both_bounds()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");

        // Prices run $5, $10 … $150. 20–40 inclusive is exactly five rows; an exclusive comparison
        // at either end would drop one.
        await section.Locator("input[aria-label='Filter by Price: Minimum']").FillAsync("20");
        await section.Locator("input[aria-label='Filter by Price: Maximum']").FillAsync("40");
        await Expect(rows).ToHaveCountAsync(5);
        await Expect(section.Locator("tbody")).ToContainTextAsync("$20.00");
        await Expect(section.Locator("tbody")).ToContainTextAsync("$40.00");
        await Expect(section.Locator("tbody")).Not.ToContainTextAsync("$45.00");
        // "20 <en dash> 40" — asserted by its ASCII prefix so the test isn't pinned to the glyph.
        await Expect(section.Locator("[data-test-id=filter-summary-2]")).ToContainTextAsync("Price: 20");
    }

    [Fact]
    public async Task Table_filter_row_bool_select_narrows_the_rows()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");
        var select = FilterRowSelect(section, "Filter by In stock");

        await select.Locator(".wss-select-selector").ClickAsync();
        var dropdown = select.Locator(".wss-select-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        // The bool editor is a single Select over exactly two literal keys; "any" is the absence of a
        // selection (AllowClear + the FilterBoolAnyText placeholder), not a third option.
        await Expect(dropdown.Locator(".wss-select-item-option")).ToHaveCountAsync(2);

        await dropdown.Locator(".wss-select-item-option", new() { HasTextString = "No" }).ClickAsync();
        await Expect(rows).ToHaveCountAsync(10);
        await Expect(section.Locator("[data-test-id=filter-summary-2]")).ToContainTextAsync("In stock: No");

        // AllowClear puts the column back to "any".
        await select.Locator(".wss-select-clear").ClickAsync();
        await Expect(rows).ToHaveCountAsync(30);
    }

    [Fact]
    public async Task Table_filter_row_enum_select_ORs_the_picked_members()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");
        var select = FilterRowSelect(section, "Filter by Category");

        await select.Locator(".wss-select-selector").ClickAsync();
        var dropdown = select.Locator(".wss-select-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();
        // Options come from the enum's members, labelled by [EnumDisplayName]/[Display] and
        // camel-case-split for the ones with neither.
        await Expect(dropdown.Locator(".wss-select-item-option")).ToHaveCountAsync(4);
        await Expect(dropdown).ToContainTextAsync("Kitchen & bar");
        await Expect(dropdown).ToContainTextAsync("Front of house");

        await dropdown.Locator(".wss-select-item-option", new() { HasTextString = "Kitchen & bar" }).ClickAsync();
        await Expect(rows).ToHaveCountAsync(8);
        // FilterMultiple: a second pick ORs within the column (the Select stays open in Multiple mode).
        await dropdown.Locator(".wss-select-item-option", new() { HasTextString = "Front of house" }).ClickAsync();
        await Expect(rows).ToHaveCountAsync(16);
        await Expect(section.Locator("[data-test-id=filter-summary-2]")).ToContainTextAsync("Category: 2 selected");
    }

    [Fact]
    public async Task Table_filter_row_options_from_data_list_the_distinct_values()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");
        var select = FilterRowSelect(section, "Filter by Supplier");

        await select.Locator(".wss-select-selector").ClickAsync();
        var options = select.Locator(".wss-select-dropdown .wss-select-item-option");
        // Three suppliers across 30 rows, de-duplicated and ordered by the underlying value.
        await Expect(options).ToHaveCountAsync(3);
        await Expect(options.Nth(0)).ToContainTextAsync("Acme Supply");
        await Expect(options.Nth(1)).ToContainTextAsync("Bay State Foods");
        await Expect(options.Nth(2)).ToContainTextAsync("Cascade Equipment");

        await options.Nth(1).ClickAsync();
        await Expect(rows).ToHaveCountAsync(10);
    }

    [Fact]
    public async Task Table_filter_row_clear_filters_restores_every_row_and_empties_the_summary()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var rows = section.Locator("tbody .wss-table-row");
        var summary = section.Locator("[data-test-id=filter-summary-2]");

        await section.Locator("input[aria-label='Filter by Name']").FillAsync("mixer");
        await Expect(rows).ToHaveCountAsync(6);
        await section.Locator("input[aria-label='Filter by Price: Minimum']").FillAsync("100");
        await Expect(rows).ToHaveCountAsync(2); // Mixer 21 ($105) and Mixer 26 ($130)
        await Expect(summary).ToContainTextAsync("Name:");
        await Expect(summary).ToContainTextAsync("Price:");

        await section.Locator("[data-test-id=filter-row-clear]").ClickAsync();
        await Expect(rows).ToHaveCountAsync(30);
        await Expect(summary).ToHaveTextAsync("(no filters)");
        await Expect(section.Locator("input[aria-label='Filter by Name']")).ToHaveValueAsync("");
        await Expect(section.Locator("input[aria-label='Filter by Price: Minimum']")).ToHaveValueAsync("");
    }

    [Fact]
    public async Task Table_filter_row_editors_are_named_after_their_column()
    {
        await GotoAsync();
        var section = FilterRowSection;
        var filterRow = section.Locator(".wss-table-filter-row");

        // FilterRowLabelFormat ("Filter by {0}") for every editor; the two range bounds qualify the
        // Min/Max wording with the column name so two numeric columns can be told apart.
        await Expect(filterRow.Locator("input[aria-label='Filter by Name']")).ToBeVisibleAsync();
        await Expect(filterRow.Locator("input[aria-label='Filter by Price: Minimum']")).ToBeVisibleAsync();
        await Expect(filterRow.Locator("input[aria-label='Filter by Price: Maximum']")).ToBeVisibleAsync();
        await Expect(filterRow.Locator("input[aria-label='Filter by In stock']")).ToBeVisibleAsync();
        await Expect(filterRow.Locator("input[aria-label='Filter by Category']")).ToBeVisibleAsync();
        await Expect(filterRow.Locator("input[aria-label='Filter by Supplier']")).ToBeVisibleAsync();
        // The date range's two inputs are named as one field through the picker's group.
        await Expect(filterRow.Locator("[role=group][aria-label='Filter by Added']")).ToHaveCountAsync(1);
        // One cell per column, filterable or not, so the row still spans the table.
        await Expect(filterRow.Locator(".wss-table-filter-row-cell")).ToHaveCountAsync(6);
    }

    [Fact]
    public async Task Table_filter_row_under_ScrollY_keeps_both_header_rows_pinned()
    {
        await GotoAsync();
        await WaitForStablePageHeightAsync();
        var section = ScrollYFilterRowSection;
        await Expect(section.Locator(".wss-table-wrapper-scroll-y")).ToHaveCountAsync(1);

        // Under a filter row the sticky element moves from each header CELL to the <thead>, so both
        // rows pin as one block at their natural offsets.
        await Expect(section.Locator("thead")).ToHaveCSSAsync("position", "sticky");
        await Expect(section.Locator("thead th").First).ToHaveCSSAsync("position", "relative");

        // Scroll + measure in ONE round trip, so nothing can move between reading the rects.
        var geo = await section.EvaluateAsync<JsonElement>(@"section => {
            const wrapper = section.querySelector('.wss-table-wrapper-scroll-y');
            wrapper.scrollTo({ top: wrapper.scrollHeight, behavior: 'instant' });
            const w = wrapper.getBoundingClientRect();
            const title = section.querySelector('thead tr').getBoundingClientRect();
            const filter = section.querySelector('thead .wss-table-filter-row').getBoundingClientRect();
            return {
                scrollTop: wrapper.scrollTop,
                wTop: w.top, wBottom: w.bottom,
                titleTop: title.top, titleBottom: title.bottom,
                filterTop: filter.top, filterBottom: filter.bottom,
            };
        }");

        var scrollTop = geo.GetProperty("scrollTop").GetDouble();
        Assert.True(scrollTop > 0, $"the wrapper did not scroll (scrollTop={scrollTop})");

        var wTop = geo.GetProperty("wTop").GetDouble();
        var wBottom = geo.GetProperty("wBottom").GetDouble();
        var titleTop = geo.GetProperty("titleTop").GetDouble();
        var titleBottom = geo.GetProperty("titleBottom").GetDouble();
        var filterTop = geo.GetProperty("filterTop").GetDouble();
        var filterBottom = geo.GetProperty("filterBottom").GetDouble();

        Assert.InRange(titleTop, wTop - 0.5, wBottom);
        Assert.InRange(titleBottom, wTop, wBottom + 0.5);
        Assert.InRange(filterTop, wTop, wBottom);
        Assert.InRange(filterBottom, wTop, wBottom + 0.5);
        // Stacked, not overlapping: the filter row sits below the title row, both still on screen.
        Assert.True(filterTop >= titleBottom - 0.5, $"filterTop={filterTop} titleBottom={titleBottom}");
    }

    [Fact]
    public async Task Table_filter_row_under_ScrollY_Select_dropdown_is_clipped_by_the_wrapper()
    {
        await GotoAsync();
        await WaitForStablePageHeightAsync();
        var section = ScrollYFilterRowSection;
        await ScrollIntoViewAsync(section);

        var select = FilterRowSelect(section, "Filter by Group");
        await select.Locator(".wss-select-selector").ClickAsync();
        var dropdown = select.Locator(".wss-select-dropdown");
        await Expect(dropdown).ToBeVisibleAsync();

        var geo = await MeasureOverlayClipAsync(section, ".wss-select-dropdown");
        var overhang = geo.GetProperty("overhang").GetDouble();
        var hitBeyond = geo.GetProperty("hitBeyondWrapper").GetBoolean();

        // KNOWN LIMITATION, deliberately pinned rather than patched: Select places its panel with
        // wss-select.js's placeDropdown, which flips/clamps but leaves position: absolute, so the
        // panel is still a descendant of .wss-table-wrapper — whose overflow-x: auto makes overflow-y
        // compute to auto as well, i.e. it clips in BOTH axes. Only TableColumnFilter's own funnel
        // panel escapes (activateFixedDropdown re-positions it fixed). The dropdown reports as
        // "visible" and overhangs the wrapper's bottom edge, but the overhanging strip is not
        // painted, which is what the hit test below proves. Flip both assertions if a row-placement
        // editor ever gains the same escape.
        Assert.True(overhang > 0, $"expected the panel to extend past the wrapper (overhang={overhang})");
        Assert.False(hitBeyond, "the Select panel is no longer clipped by the ScrollY wrapper — update this test");

        // The part inside the wrapper is fully usable, which is what keeps the editor workable here.
        await dropdown.Locator(".wss-select-item-option", new() { HasTextString = "Kitchen & bar" }).ClickAsync();
        await Expect(section.Locator("tbody .wss-table-row")).ToHaveCountAsync(8);
    }

    [Fact]
    public async Task Table_filter_row_under_ScrollY_date_panel_is_clipped_by_the_wrapper()
    {
        await GotoAsync();
        await WaitForStablePageHeightAsync();
        var section = ScrollYFilterRowSection;
        await ScrollIntoViewAsync(section);

        await section.Locator(".wss-table-filter-row input[aria-label='Start date']").ClickAsync();
        var panel = section.Locator(".wss-picker-dropdown");
        await Expect(panel).ToBeVisibleAsync();

        var geo = await MeasureOverlayClipAsync(section, ".wss-picker-dropdown");
        // Same clip as the Select above, but far worse: the two-month range panel is ~600x266 against
        // a 200px-tall wrapper, so most of the day grid — including the second month — is unpainted
        // and unreachable, and the wrapper grows a horizontal scrollbar while the panel is open.
        Assert.True(geo.GetProperty("overhang").GetDouble() > 0);
        Assert.True(geo.GetProperty("overhangRight").GetDouble() > 0);
        Assert.False(geo.GetProperty("hitBeyondWrapper").GetBoolean(),
            "the date panel is no longer clipped by the ScrollY wrapper — update this test");

        var daysInside = geo.GetProperty("daysInsideWrapper").GetInt32();
        var dayCount = geo.GetProperty("dayCount").GetInt32();
        Assert.True(dayCount > 0);
        Assert.True(daysInside < dayCount, $"{daysInside} of {dayCount} day cells are inside the wrapper");
    }

    [Fact]
    public async Task Table_filter_search_narrows_the_options_and_shows_the_empty_text()
    {
        await GotoAsync();
        var section = DropdownExtrasSection;
        await section.Locator(".wss-table-filter-trigger[aria-label^='Filter Region']").ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        var items = dropdown.Locator(".wss-table-filter-item");
        await Expect(items).ToHaveCountAsync(12);

        var search = dropdown.Locator(".wss-table-filter-search input");
        await Expect(search).ToHaveAttributeAsync("placeholder", "Search in filters");
        await search.FillAsync("or");
        await Expect(items).ToHaveCountAsync(1);
        await Expect(items.First).ToContainTextAsync("Portland");

        await search.FillAsync("zzz");
        await Expect(items).ToHaveCountAsync(0);
        await Expect(dropdown.Locator(".wss-table-filter-empty")).ToHaveTextAsync("No matches");
        // Nothing visible to select: the check-all row goes with the list.
        await Expect(dropdown.Locator(".wss-table-filter-checkall")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Table_filter_check_all_ticks_every_visible_option_and_reports_mixed()
    {
        await GotoAsync();
        var section = DropdownExtrasSection;
        await section.Locator(".wss-table-filter-trigger[aria-label^='Filter Region']").ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        var checkAll = dropdown.Locator(".wss-table-filter-checkall input");
        var boxes = dropdown.Locator(".wss-table-filter-item input");

        await Expect(dropdown.Locator(".wss-table-filter-checkall")).ToContainTextAsync("Select all");
        await checkAll.CheckAsync();
        await Expect(boxes).ToHaveCountAsync(12);
        for (var i = 0; i < 12; i++) await Expect(boxes.Nth(i)).ToBeCheckedAsync();

        // "Mixed" is a DOM property with no HTML attribute — mirrored through wss-table.js exactly as
        // the table's own select-all is, so it has to be read off the element, not the markup.
        await boxes.Nth(0).UncheckAsync();
        await Expect(checkAll).Not.ToBeCheckedAsync();
        await _page.WaitForFunctionAsync(
            "sel => { const el = document.querySelector(sel); return !!el && el.indeterminate; }",
            ".wss-table-filter-checkall input",
            new PageWaitForFunctionOptions { Timeout = 5_000 });
    }

    [Fact]
    public async Task Table_default_filter_values_start_applied_and_Reset_returns_to_them()
    {
        await GotoAsync();
        var section = DropdownExtrasSection;
        var rows = section.Locator("tbody .wss-table-row");
        var status = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Status']");

        // DefaultFilterValues is applied on the column's first parameter pass, silently.
        await Expect(rows).ToHaveCountAsync(6);
        await Expect(status).ToHaveClassAsync(new Regex("wss-table-filter-active"));

        await status.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await Expect(dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Open" }).Locator("input"))
            .ToBeCheckedAsync();
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Open" }).Locator("input").UncheckAsync();
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Closed" }).Locator("input").CheckAsync();
        await dropdown.Locator(".wss-table-filter-ok").ClickAsync();
        await Expect(rows).ToHaveCountAsync(3);

        // FilterResetToDefault: Reset goes back to the default, so the column stays ACTIVE.
        await status.ClickAsync();
        await dropdown.Locator(".wss-table-filter-reset").ClickAsync();
        await Expect(rows).ToHaveCountAsync(6);
        await Expect(status).ToHaveClassAsync(new Regex("wss-table-filter-active"));
        await status.ClickAsync();
        await Expect(dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Open" }).Locator("input"))
            .ToBeCheckedAsync();
    }

    [Fact]
    public async Task Table_filter_on_close_applies_the_staged_selection_on_an_outside_click()
    {
        await GotoAsync();
        var section = DropdownExtrasSection;
        var rows = section.Locator("tbody .wss-table-row");
        var tier = section.Locator(".wss-table-filter-trigger[aria-label^='Filter Tier']");

        await tier.ClickAsync();
        var dropdown = section.Locator(".wss-table-filter-dropdown");
        await dropdown.Locator(".wss-table-filter-item", new() { HasTextString = "Gold" }).Locator("input").CheckAsync();

        // The default is "an outside click discards"; FilterOnClose turns the dismissal into an OK.
        await section.Locator(".wss-table-filter-backdrop").ClickAsync(new() { Position = new Position { X = 5, Y = 5 } });
        await Expect(dropdown).Not.ToBeVisibleAsync();
        await Expect(tier).ToHaveClassAsync(new Regex("wss-table-filter-active"));
        // ANDed with the Status column's still-applied default: Open AND Gold.
        await Expect(rows).ToHaveCountAsync(2);
        await Expect(section.Locator("tbody")).ToContainTextAsync("Atlanta");
        await Expect(section.Locator("tbody")).ToContainTextAsync("Houston");
    }

    [Fact]
    public async Task Table_filter_row_scrolly_section_visual_baseline()
    {
        await GotoAsync();
        await WaitForStablePageHeightAsync();
        var section = ScrollYFilterRowSection;
        await Expect(section.Locator(".wss-table-filter-row-cell")).ToHaveCountAsync(3);
        await BaselineAsync(section, "table-filter-row-scrolly");
    }

    // The gallery's own late layout shifts (the server-paging demo fills its rows ~150ms after init,
    // twice under prerender + hydration) move everything below them, so any geometry read has to wait
    // for the document height to hold still first — see DateRangePickerE2ETests.GotoAsync.
    async Task WaitForStablePageHeightAsync() =>
        await _page.WaitForFunctionAsync(
            @"() => {
                const h = document.body.scrollHeight;
                if (window.__wssLastHeight !== h) { window.__wssLastHeight = h; window.__wssStableSince = Date.now(); }
                return Date.now() - window.__wssStableSince > 600;
            }",
            null,
            new PageWaitForFunctionOptions { Timeout = 15_000 });

    // behavior: 'instant' explicitly — the demo host's reboot CSS sets scroll-behavior: smooth on
    // :root, so a default-behavior scroll animates and every rect read right after it is mid-flight.
    static Task ScrollIntoViewAsync(ILocator locator) =>
        locator.EvaluateAsync("el => el.scrollIntoView({ block: 'center', behavior: 'instant' })");

    // Whether an overlay opened from a filter-row cell survives the ScrollY wrapper's overflow clip.
    // getBoundingClientRect alone cannot answer that (a clipped box still reports its full rect, and
    // Playwright's own visibility check ignores ancestor overflow), so this probes a point that lies
    // INSIDE the overlay but outside the wrapper: elementFromPoint returns the overlay there only if
    // it genuinely escaped. Every rect + the hit test in one round trip, so nothing moves in between.
    static Task<JsonElement> MeasureOverlayClipAsync(ILocator section, string overlaySelector) =>
        section.EvaluateAsync<JsonElement>(@"(section, sel) => {
            const wrapper = section.querySelector('.wss-table-wrapper-scroll-y');
            const overlay = section.querySelector(sel);
            const w = wrapper.getBoundingClientRect();
            const o = overlay.getBoundingClientRect();
            const probeY = Math.min(o.bottom - 2, w.bottom + 4);
            const probeX = Math.min(o.left + o.width / 2, w.right - 2);
            const hit = document.elementFromPoint(probeX, probeY);
            const days = [...overlay.querySelectorAll('.wss-picker-day')];
            const inside = days.filter(d => {
                const r = d.getBoundingClientRect();
                return r.top >= w.top && r.bottom <= w.bottom && r.left >= w.left && r.right <= w.right;
            });
            return {
                wrapperTop: w.top, wrapperBottom: w.bottom, wrapperRight: w.right,
                overlayTop: o.top, overlayBottom: o.bottom, overlayRight: o.right,
                overhang: o.bottom - w.bottom,
                overhangRight: o.right - w.right,
                probeY,
                hitBeyondWrapper: !!hit && overlay.contains(hit) && probeY > w.bottom,
                dayCount: days.length,
                daysInsideWrapper: inside.length,
            };
        }", overlaySelector);

    // ---- AntD 4.x parity batch 2: Modal/Drawer Keyboard+Centered+Extra, Popconfirm/Popover
    // controlled Visible, Popconfirm async-confirm/OkDanger, notification Placement, Tabs
    // Card/Centered/TabBarExtraContent, Alert Banner/Action, SearchInput AllowClear/EnterButtonText
    // (appended -- existing sections/baselines above are untouched). ----

    [Fact]
    public async Task Modal_keyboard_false_blocks_escape_while_closable_stays_true()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Centered, Keyboard, Extra" });
        await section.Locator("[data-test-id=open-centered-modal]").ClickAsync();
        var panel = _page.Locator(".wss-modal[role=dialog]", new() { HasTextString = "Centered modal" });
        await Expect(panel).ToBeVisibleAsync();

        // The header X (Closable, default true) is still there...
        await Expect(panel.Locator(".wss-modal-close")).ToBeVisibleAsync();
        // ...but Escape (Keyboard="false") does nothing -- decoupled from Closable.
        await _page.Keyboard.PressAsync("Escape");
        await Expect(panel).ToBeVisibleAsync();

        // The X still closes it (Closable governs the X independently of Keyboard).
        await panel.Locator(".wss-modal-close").ClickAsync();
        await Expect(panel).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Modal_centered_adds_the_wrap_modifier_class()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Centered, Keyboard, Extra" });
        await section.Locator("[data-test-id=open-centered-modal]").ClickAsync();

        await Expect(_page.Locator(".wss-modal-wrap")).ToHaveClassAsync(new Regex("wss-modal-wrap-centered"));
        await _page.Keyboard.PressAsync("Escape"); // no-op (Keyboard=false); close via the X instead
        await _page.Locator(".wss-modal-close").ClickAsync();
    }

    [Fact]
    public async Task Drawer_extra_renders_a_working_button_beside_the_close_icon()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "Centered, Keyboard, Extra" });
        await section.Locator("[data-test-id=open-extra-drawer]").ClickAsync();

        var drawer = _page.Locator(".wss-drawer[role=dialog]", new() { HasTextString = "Drawer with Extra" });
        await Expect(drawer).ToBeVisibleAsync();
        var extraBtn = drawer.Locator("[data-test-id=drawer-extra-btn]");
        await Expect(extraBtn).ToBeVisibleAsync();
        await extraBtn.ClickAsync(); // just confirms it's a real, clickable, non-overlapping button

        await Expect(drawer.Locator(".wss-drawer-close")).ToBeVisibleAsync();
        await drawer.Locator(".wss-drawer-close").ClickAsync();
        await Expect(drawer).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Popconfirm_controlled_Visible_button_opens_and_closes_it_with_JS_positioning()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "controlled Visible" });
        var panel = section.Locator(".wss-popconfirm");

        await Expect(panel).ToHaveCountAsync(0);
        await section.Locator("[data-test-id=controlled-popconfirm-toggle]").ClickAsync();

        // Visible AND positioned (not stuck at wss-measuring) -- proves the externally-driven open
        // ran through the same JS placement path as a click, not just that _open flipped. The OK
        // button also gains focus -- a prior investigation of this controlled-Visible path had
        // surfaced two compounding issues: an activation race in OnAfterRenderAsync (overlapping
        // invocations around the position/focus state machine could leave _pendingFocus never
        // consumed), now guarded by an _activationSeq sequence token (Modal/Drawer's equivalent
        // lives in the shared JsHandle holder); and,
        // independently, a Blazor render-batch focus-restore race specific to focusing a <button>
        // from this externally-driven path, fixed by routing the focus call through
        // wss-overlay.js's focusDeferred instead of a direct FocusAsync() (see its doc comment).
        // Popover's equivalent _panelRef.FocusAsync() below was never affected by the second issue.
        await PageTestBase.WaitForOpenAndPositionedAsync(panel);
        await Expect(panel.Locator(".wss-dialog-btn-primary")).ToBeFocusedAsync();

        // Toggle again (still the same external button; the popup's own full-viewport backdrop would
        // intercept a real coordinate-based click on our page-level toggle button, so dispatch the
        // click event directly -- same technique as the Select controlled-Open e2e test).
        await section.Locator("[data-test-id=controlled-popconfirm-toggle]").DispatchEventAsync("click");
        await Expect(panel).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Popconfirm_rapid_close_reopen_still_positions_and_focuses_deterministically()
    {
        // Exercises the race the _activationSeq token guards against: a close immediately followed
        // by a reopen, back-to-back, before the first attempt's place() JS round trip can resolve --
        // this used to be able to leave stale _positioned/_pendingFocus state that skipped the next
        // open's own measure/focus.
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "controlled Visible" });
        var panel = section.Locator(".wss-popconfirm");
        var toggle = section.Locator("[data-test-id=controlled-popconfirm-toggle]");

        await toggle.ClickAsync();               // open
        await toggle.DispatchEventAsync("click"); // close
        await toggle.DispatchEventAsync("click"); // reopen, with no settling time in between

        await PageTestBase.WaitForOpenAndPositionedAsync(panel);
        await Expect(panel.Locator(".wss-dialog-btn-primary")).ToBeFocusedAsync();

        await toggle.DispatchEventAsync("click");
        await Expect(panel).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Popover_controlled_Visible_button_opens_it_and_JS_focuses_the_panel()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "controlled Visible" });
        var panel = section.Locator(".wss-popover");

        await Expect(panel).ToHaveCountAsync(0);
        await section.Locator("[data-test-id=controlled-popover-toggle]").ClickAsync();

        await PageTestBase.WaitForOpenAndPositionedAsync(panel);
        await Expect(panel).ToBeFocusedAsync();

        // The popup's own full-viewport backdrop would intercept a real coordinate-based click on
        // our page-level toggle button, so dispatch the click event directly (same technique as the
        // Select controlled-Open e2e test).
        await section.Locator("[data-test-id=controlled-popover-toggle]").DispatchEventAsync("click");
        await Expect(panel).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Popconfirm_async_confirm_disables_both_buttons_with_a_spinner_then_closes()
    {
        await GotoAsync();
        await _page.Locator("[data-test-id=async-confirm-trigger]").ClickAsync();
        var panel = _page.Locator(".wss-popconfirm", new() { HasTextString = "Permanently delete" });
        await Expect(panel).ToBeVisibleAsync();

        var okButton = panel.Locator(".wss-dialog-btn-primary");
        await Expect(okButton).ToHaveClassAsync(new Regex("wss-dialog-btn-danger")); // OkDanger
        await okButton.ClickAsync();

        // Genuinely pending (the demo's OnConfirm awaits a 1s delay): both buttons disabled, spinner up.
        await Expect(okButton).ToBeDisabledAsync();
        await Expect(panel.Locator(".wss-dialog-btn").First).ToBeDisabledAsync();
        await Expect(panel.Locator(".wss-icon-spin")).ToBeVisibleAsync();

        // Closes on completion, and the demo's result text updates.
        await Expect(panel).Not.ToBeVisibleAsync(new() { Timeout = 5_000 });
        await Expect(_page.Locator("[data-test-id=async-confirm-result]")).ToContainTextAsync("deleted");
    }

    [Fact]
    public async Task Notification_bottom_left_container_anchors_to_the_bottom_left_corner()
    {
        await GotoAsync();
        await _page.Locator("[data-test-id=bottom-left-notification-btn]").ClickAsync();

        var containers = _page.Locator(".wss-notification-container.wss-notification-bottomleft");
        await Expect(containers).ToHaveCountAsync(1);
        var box = await containers.BoundingBoxAsync();
        Assert.NotNull(box);
        var viewport = _page.ViewportSize;
        Assert.NotNull(viewport);
        // Anchored toward the bottom-left: comfortably in the left half, and its bottom edge sits
        // near the viewport's own bottom (not pinned to the top like the default TopRight stack).
        Assert.True(box!.X < viewport!.Width / 2, $"x={box.X}");
        Assert.True(box.Y + box.Height > viewport.Height * 0.6, $"bottom={box.Y + box.Height}");
    }

    [Fact]
    public async Task Tabs_card_section_renders_card_styling_and_the_extra_content_button()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "TabBarExtraContent, Centered, Card type" });
        await Expect(section.Locator(".wss-tabs")).ToHaveClassAsync(new Regex("wss-tabs-card"));
        await Expect(section.Locator(".wss-tabs-nav")).ToHaveClassAsync(new Regex("wss-tabs-nav-centered"));
        await Expect(section.Locator("[data-test-id=tabs-extra-btn]")).ToBeVisibleAsync();

        // Still a plain ARIA tab strip -- clicking a tab still switches the active pane.
        await section.Locator("[role=tab]", new() { HasTextString = "Tab 2" }).ClickAsync();
        await Expect(section.Locator("[role=tabpanel]")).ToContainTextAsync("Pane two");
    }

    [Fact]
    public async Task Tabs_arrow_key_focuses_a_conditionally_inserted_tab()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "TabBarExtraContent, Centered, Card type" });

        // Inserts "Priority" BEFORE Tab 1/2/3 -- a structural insertion whose position only the
        // render-tree diff knows, which is why each Tab renders its own nav button (see Tab.razor).
        // The newcomer's button and its @ref capture are created together by that diff; this test's
        // real point is that arrow navigation's FocusAsync against the freshly-captured ref lands
        // real DOM focus, which bUnit cannot observe (no JS runtime).
        await section.Locator("[data-test-id=toggle-priority-tab]").CheckAsync();

        var priorityTab = section.Locator("[role=tab]", new() { HasTextString = "Priority" });
        await Expect(priorityTab).ToBeVisibleAsync();

        var tab1 = section.Locator("[role=tab]", new() { HasTextString = "Tab 1" });
        await tab1.FocusAsync();
        await _page.Keyboard.PressAsync("ArrowLeft"); // wraps/moves onto the newly-inserted Priority tab

        await Expect(priorityTab).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(priorityTab).ToBeFocusedAsync(); // real DOM focus, not just the interop call
        await Expect(section.Locator("[role=tabpanel]")).ToContainTextAsync("Pane priority");
    }

    [Fact]
    public async Task Alert_banner_and_action_section_renders_as_expected()
    {
        await GotoAsync();
        var alertSection = _page.Locator("section.demo-section").First;
        var banner = alertSection.Locator(".wss-alert-banner");
        await Expect(banner).ToBeVisibleAsync();
        await Expect(banner).ToHaveClassAsync(new Regex("wss-alert-warning")); // default severity while Banner + no explicit Type

        var actionBtn = alertSection.Locator("[data-test-id=alert-action-btn]");
        await Expect(actionBtn).ToBeVisibleAsync();
        // The section already has an earlier Closable alert (Error, no Action) -- scope to the one
        // that actually has the action slot, and confirm it's still closable alongside Action.
        var actionAlert = alertSection.Locator(".wss-alert:has([data-test-id=alert-action-btn])");
        await Expect(actionAlert.Locator(".wss-alert-close")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SearchInput_allow_clear_and_enter_button_text_section_works()
    {
        await GotoAsync();
        var section = _page.Locator("section.demo-section", new() { HasTextString = "AllowClear, EnterButtonText" });

        var clearableInput = section.Locator("#demo-search-clearable");
        var clearBtn = section.Locator(".wss-search-clear");
        await Expect(clearBtn).ToBeVisibleAsync(); // pre-filled with "pre-filled"
        await clearBtn.ClickAsync();
        await Expect(clearableInput).ToHaveValueAsync("");
        // The enter-button search starts empty (never had a value), so this was the only clear
        // button on the page -- clearing it leaves none.
        await Expect(clearBtn).ToHaveCountAsync(0);

        var enterBtn = section.Locator(".wss-search:has(#demo-search-enter-button) .wss-search-btn-enter");
        await Expect(enterBtn).ToBeVisibleAsync();
        await Expect(enterBtn).ToContainTextAsync("Search");
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
