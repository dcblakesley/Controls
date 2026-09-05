using System.Text.RegularExpressions;
using Controls.Demo;

namespace FormTesting.Client.E2ETests;

/// <summary>
/// E2E coverage for <see cref="EditRange{T}"/>'s JS-owned internals — the pointer drag
/// (<c>wss-slider.js</c>), the press flag that keeps the value bubble up while the pointer is off
/// the track, the arrow-key page-scroll suppression, and the real-browser geometry the no-JS click
/// fallback depends on. None of those are reachable from bUnit, which executes no JavaScript; the
/// pure-C# halves (binding, snapping, the keyboard model, ARIA) are covered by <c>EditRangeTests</c>.
/// </summary>
public class EditRangeE2ETests(AppFixture app, BrowserFixture browser) : DemoPageTestBase(app, browser)
{
    protected override CurrentView View => CurrentView.Range;

    // Section order on the demo page: 0 basic, 1 Min/Max/Step, 2 marks (+ SnapToMarks), 3 dots,
    // 4 Included=false, 5 TooltipFormat, 6 disabled + read-only, 7 [Required].
    ILocator Section(int index) => Page.Locator("section.demo-section").Nth(index);

    static ILocator Track(ILocator scope) => scope.Locator(".edit-range-track").First;

    // Drags along a track's horizontal axis from `fromFraction` to `toFraction` of its own width,
    // through real pointer events -- the only way to exercise wss-slider.js's drag path.
    async Task DragAsync(ILocator track, double fromFraction, double toFraction)
    {
        // Mouse coordinates are viewport-relative and Mouse.MoveAsync does no scrolling of its own,
        // so a track below the fold would be dragged at coordinates that never touch it.
        await track.ScrollIntoViewIfNeededAsync();
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        var y = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync(box.X + (float)(box.Width * fromFraction), y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + (float)(box.Width * toFraction), y);
        await Page.Mouse.UpAsync();
    }

    [Fact]
    public async Task A_real_drag_moves_the_handle_and_commits()
    {
        await NavigateAsync();

        var track = Track(Section(0));
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "40");

        // A real press-move-release across the track: the reports are rAF-throttled, so the
        // assertions below rely on Playwright's own retrying.
        await DragAsync(track, 0.4, 0.8);

        // 80% of 0..100, give or take a pixel of subpixel slop either way.
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", new Regex("^(79|80|81)$"));
        await Expect(Section(0).Locator(".edit-range-handle"))
            .ToHaveAttributeAsync("style", new Regex(@"^left:(79|80|81)%$"));
    }

    [Fact]
    public async Task The_value_bubble_stays_up_while_a_drag_runs_off_the_track()
    {
        await NavigateAsync();

        var section = Section(0);
        var track = Track(section);
        var tooltip = section.Locator(".edit-range-tooltip");
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);

        // Press on the track, then drag 80px BELOW it. Pointer capture keeps the reports coming, but
        // CSS :hover has stopped applying -- so the bubble can only still be up because of the
        // C#-owned visible class the module's press flag drives.
        await Page.Mouse.MoveAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(box.X + (float)(box.Width * 0.7), box.Y + box.Height + 80);

        await Expect(tooltip).ToHaveClassAsync(new Regex("edit-range-tooltip-visible"));
        await Expect(tooltip).ToBeVisibleAsync();
        await Expect(tooltip).ToHaveTextAsync(new Regex("^(69|70|71)$"));
        // The drag kept reporting past the track's own edge, which is what pointer capture is for.
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", new Regex("^(69|70|71)$"));

        await Page.Mouse.UpAsync();

        // The release report ends the gesture -- the C#-owned class is what the module's press flag
        // drives, so this is the assertion that pins the flag itself.
        await Expect(tooltip).Not.ToHaveClassAsync(new Regex("edit-range-tooltip-visible"));

        // The bubble itself is still up, and correctly so: the module focused the track on
        // pointerdown (so the keyboard is available the instant a drag ends), and Chromium treats
        // that programmatic focus as :focus-visible, which is the OTHER reveal condition. Moving the
        // pointer away first, so this can't be :hover keeping it open.
        await Page.Mouse.MoveAsync(2, 2);
        await Expect(track).ToBeFocusedAsync();
        await Expect(tooltip).ToBeVisibleAsync();

        // Only once focus leaves too is there nothing left holding it open.
        await track.EvaluateAsync("el => el.blur()");
        await Expect(tooltip).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task Arrow_keys_step_the_value_without_scrolling_the_page()
    {
        await NavigateAsync();

        var track = Track(Section(0));
        // wss-slider.js focuses the track on pointerdown, so the keyboard path is available straight
        // after a press -- no Tab needed.
        await track.ClickAsync();
        await Expect(track).ToBeFocusedAsync();
        var scrollBefore = await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");

        await Page.Keyboard.PressAsync("Home");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "0");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "1");
        await Page.Keyboard.PressAsync("ArrowUp");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "2");
        await Page.Keyboard.PressAsync("PageUp");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "12");
        await Page.Keyboard.PressAsync("End");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "100");

        // The page must not have scrolled: wss-slider.js preventDefaults exactly these keys, which
        // is the one part of the keyboard model C# cannot express.
        await Page.Keyboard.PressAsync("PageDown");
        await Page.Keyboard.PressAsync("ArrowDown");
        Assert.Equal(scrollBefore, await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)"));
    }

    [Fact]
    public async Task The_rendered_track_width_matches_the_no_js_fallback_constant()
    {
        // EditRange.TrackWidth (320) is what the no-JS click fallback normalizes OffsetX against, and
        // it only stays correct while --edit-range-width agrees with it. If this ever reads something
        // else, that fallback silently skews -- and the two visual baselines moved with it.
        await NavigateAsync();

        var box = await Track(Section(0)).BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(box.Width is > 319 and < 321, $"track width {box.Width}");
    }

    [Fact]
    public async Task The_track_takes_a_press_from_its_expanded_hit_area()
    {
        await NavigateAsync();

        var track = Track(Section(0));
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        // The visible design is unchanged (WCAG 2.5.8 is met with an invisible ::before, not a taller
        // track) -- if this ever reads 24, the visual baselines moved with it.
        Assert.True(box.Height is > 13 and < 15, $"track height {box.Height}");

        // 4px above the track's top edge is outside its own box but inside its hit area...
        await Page.Mouse.ClickAsync(box.X + box.Width / 4, box.Y - 4);

        // ...and the coordinate math still normalizes against the VISIBLE track, so a quarter of the
        // way along is still 25 -- the expanded box must never become the denominator.
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", new Regex("^(24|25|26)$"));
    }

    [Fact]
    public async Task SnapToMarks_pulls_a_press_onto_the_nearest_mark()
    {
        await NavigateAsync();

        // The marks section renders two sliders over the same 0/26/37/100 marks: the first free, the
        // second snapped.
        var section = Section(2);
        var free = section.Locator(".edit-range").Nth(0).Locator(".edit-range-track");
        var snapped = section.Locator(".edit-range").Nth(1).Locator(".edit-range-track");

        await DragAsync(free, 0.6, 0.6);
        await Expect(free).ToHaveAttributeAsync("aria-valuenow", new Regex("^(59|60|61)$"));

        await DragAsync(snapped, 0.6, 0.6);
        // 60 is nearer 37 than 100, and only the marked values are reachable.
        await Expect(snapped).ToHaveAttributeAsync("aria-valuenow", "37");
        // The mark's own label is what a sighted user reads, so it's what gets announced.
        await Expect(snapped).ToHaveAttributeAsync("aria-valuetext", "37°C");
    }

    [Fact]
    public async Task A_mark_label_click_commits_that_marks_value()
    {
        await NavigateAsync();

        var section = Section(2);
        var slider = section.Locator(".edit-range").Nth(1);
        var track = slider.Locator(".edit-range-track");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "26");

        await slider.Locator(".edit-range-mark").Last.ClickAsync(); // "100°C"

        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "100");
    }

    [Fact]
    public async Task A_disabled_slider_ignores_a_drag()
    {
        await NavigateAsync();

        var track = Track(Section(6));
        await Expect(track).ToHaveAttributeAsync("aria-disabled", "true");

        await DragAsync(track, 0.6, 0.9);

        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "60");
    }

    [Fact]
    public async Task The_required_field_is_marked_invalid_before_any_interaction()
    {
        await NavigateAsync();

        // The demo force-validates on every render (DemoFormPage), so the [Required] instance is
        // already invalid with no interaction -- and its null value parks the handle at Min.
        var section = Section(7);
        var track = section.Locator("#Required");
        await Expect(track).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "1");
        await Expect(section.Locator("#error-msg-Required")).ToContainTextAsync("Priority is required.");

        // The .edit-range-track.invalid rule actually applies: --edit-color-danger bridges to
        // --color-danger, which the FormTesting host's app.css overrides to #CF1322 at :root.
        await Expect(section.Locator(".edit-range-handle")).ToHaveCSSAsync("border-color", "rgb(207, 19, 34)");

        // The first interaction commits a real value and clears the error.
        await track.ClickAsync();
        await Page.Keyboard.PressAsync("End");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "10");
        await Expect(track).Not.ToHaveAttributeAsync("aria-invalid", "true");
    }

    [Fact]
    public async Task Without_the_js_module_a_click_still_positions_the_handle()
    {
        // The documented no-JS degrade, verified in a real browser: assetBase points every lazily
        // imported wss-*.js at a path nothing serves, so the drag module never loads and the
        // component's own @onclick fallback -- which reads MouseEventArgs.OffsetX -- has to carry the
        // press. bUnit can only simulate this with synthetic event args (see EditRangeTests'
        // Strict-mode fallback tests); this proves the browser really does populate that offset
        // relative to the track, which the handle being pointer-events:none guarantees.
        await Page.GotoAsync($"{App.BaseUrl}/?view={View}&assetBase={Uri.EscapeDataString("/definitely-not-a-real-path")}",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });
        await Expect(Page.Locator("h1", new() { HasTextString = "EditRange Demo" }))
            .ToBeVisibleAsync(new() { Timeout = 15_000 });

        var track = Track(Section(0));
        var box = await track.BoundingBoxAsync();
        Assert.NotNull(box);
        await Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);

        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "50");
        // No JS also means no focus-on-press, so the keyboard path is reached by Tab instead -- and
        // still works.
        await track.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(track).ToHaveAttributeAsync("aria-valuenow", "51");
    }

    [Fact]
    public async Task Visual_baseline_marks_section()
    {
        await NavigateAsync();
        var section = Section(2);
        await Expect(section).ToBeVisibleAsync();

        await ExpectMatchesBaselineAsync(section, "marks-section");
    }
}
