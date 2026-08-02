using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// The <see cref="Select{TValue}"/> engine's post-rebuild highlight and focus contracts.
/// <list type="bullet">
/// <item>Every rebuild of the filtered row list settles the highlight on a row the user can act on —
/// never a group header, never a disabled option — including the rebuilds that don't go through
/// <c>SetInitialActive</c>/<c>ApplySearchAsync</c> (Options reassigned while open; the multiple-mode
/// select / clear / tag-commit paths that clear the search text).</item>
/// <item><c>DefaultOpen</c> gets the same selection-aware initial highlight a user-driven open does.</item>
/// <item>Removing a tag or clearing deletes the focused button from the DOM, so both put focus back on
/// the search input instead of dropping it to <c>&lt;body&gt;</c>.</item>
/// <item>The multiple-mode select and clear paths cancel an in-flight debounced search, so a stale
/// timer can't land afterwards and fire a spurious <c>OnSearch("")</c>.</item>
/// </list>
/// </summary>
public class SelectHighlightAndFocusTests : BunitContext
{
    public SelectHighlightAndFocusTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the scroll/placement/focus JS

    const string InputSelector = "input.wss-select-selection-search-input";

    static SelectOption<string> Opt(string value, string? group = null, bool disabled = false) =>
        new(value, value, disabled) { Group = group };

    static KeyboardEventArgs Key(string key) => new() { Key = key };

    // ----- finding 3: RebuildFiltered lands the highlight on a selectable row --------------------

    [Fact]
    public void Reassigning_Options_while_open_moves_the_highlight_off_a_group_header()
    {
        // The old bounds-only clamp kept _activeIndex at 2, which the replacement list makes a group
        // header — the highlight and aria-activedescendant vanished and Enter went dead.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B"), Opt("C") })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        var input = cut.Find(InputSelector);
        input.KeyDown(Key("ArrowDown")); // 0 -> 1
        input.KeyDown(Key("ArrowDown")); // 1 -> 2

        // Rows become [0] "Fruit" header, [1] Apple, [2] "Vegetable" header, [3] Carrot.
        cut.Render(p => p.Add(s => s.Options, new List<SelectOption<string>>
        {
            Opt("Apple", "Fruit"),
            Opt("Carrot", "Vegetable"),
        }));

        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal("Carrot", selected);
    }

    [Fact]
    public void Reassigning_Options_while_open_moves_the_highlight_off_a_disabled_option()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B") })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(Key("ArrowDown")); // 0 -> 1

        cut.Render(p => p.Add(s => s.Options, new List<SelectOption<string>>
        {
            Opt("Apple"),
            Opt("Banana", disabled: true), // index 1 is now disabled
        }));

        cut.Find(InputSelector).KeyDown(Key("Enter"));

        // No selectable row at/after 1, so the scan falls back to the one before it.
        Assert.Equal("Apple", selected);
    }

    [Fact]
    public void Selecting_in_multiple_mode_leaves_the_highlight_on_an_option_not_a_header()
    {
        // Selecting clears the search text and restores the full (grouped) list under an index that
        // pointed at an option in the FILTERED list — index 2 there is "Ay", index 2 here is a header.
        var captured = new List<List<string>>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Zed", "G1"), Opt("Ax", "G2"), Opt("Ay", "G2") })
            .Add(s => s.Values, new List<string>())
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => captured.Add(v.ToList())));

        var input = cut.Find(InputSelector);
        input.Input("A");                // filtered rows: [0] "G2" header, [1] Ax, [2] Ay
        input.KeyDown(Key("ArrowDown")); // 1 -> 2 (Ay)
        input.KeyDown(Key("Enter"));     // selects Ay, clears the search, restores the full list

        Assert.Equal(new List<string> { "Ay" }, captured[^1]);

        cut.Find(InputSelector).KeyDown(Key("Enter")); // the highlight must still be on a real option

        Assert.Equal(2, captured.Count);
        Assert.Contains("Ax", captured[^1]);
    }

    [Fact]
    public void Clearing_in_multiple_mode_leaves_the_highlight_on_an_option_not_a_header()
    {
        var captured = new List<List<string>>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Zed", "G1"), Opt("Ax", "G2"), Opt("Ay", "G2") })
            .Add(s => s.Values, new List<string> { "Zed" })
            .Add(s => s.AllowClear, true)
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => captured.Add(v.ToList())));

        var input = cut.Find(InputSelector);
        input.Input("A");                // filtered rows: [0] "G2" header, [1] Ax, [2] Ay
        input.KeyDown(Key("ArrowDown")); // 1 -> 2 (Ay)

        cut.Find("button.wss-select-clear").Click(); // clears the search + restores the full list

        Assert.Empty(captured[^1]);

        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal(2, captured.Count);
        Assert.Contains("Ax", captured[^1]);
    }

    [Fact]
    public void Committing_a_tag_leaves_the_highlight_on_an_option_not_a_header()
    {
        var captured = new List<List<string>>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Ax", "G1") })
            .Add(s => s.Values, new List<string>())
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => captured.Add(v.ToList())));

        var input = cut.Find(InputSelector);
        input.Input("zzz");          // matches nothing -> the empty row, active index 0
        input.KeyDown(Key("Enter")); // commits the free tag, clears the search, restores [header, Ax, zzz]

        Assert.Equal(new List<string> { "zzz" }, captured[^1]);

        cut.Find(InputSelector).KeyDown(Key("Enter")); // index 0 is the "G1" header — must have moved on

        Assert.Equal(2, captured.Count);
        Assert.Contains("Ax", captured[^1]);
    }

    // ----- finding 19: DefaultOpen runs the same initial-highlight pass as a user-driven open -----

    [Fact]
    public void DefaultOpen_highlights_the_bound_value_not_the_first_option()
    {
        // Mirrors SelectEngineTests.Opening_by_click_highlights_the_current_selection_not_the_first_option
        // for the DefaultOpen path, which used to skip SetInitialActive entirely.
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B"), Opt("C") })
            .Add(s => s.Value, "C")
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal("C", selected);
    }

    [Fact]
    public void DefaultOpen_skips_a_group_header_and_a_disabled_first_option()
    {
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A", "G", disabled: true), Opt("B", "G") })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Equal("B", selected);
    }

    [Fact]
    public void DefaultOpen_highlights_the_bound_selection_in_multiple_mode()
    {
        var captured = new List<List<string>>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B"), Opt("C") })
            .Add(s => s.Values, new List<string> { "C" })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValuesChanged, (IEnumerable<string> v) => captured.Add(v.ToList())));

        // Enter on the highlighted row toggles it: the bound "C" comes back off, proving the highlight
        // opened on the selection rather than at raw index 0.
        cut.Find(InputSelector).KeyDown(Key("Enter"));

        Assert.Empty(captured[^1]);
    }

    // ----- No selectable row at all: the highlight goes nowhere, not onto a disabled option -----
    // Both highlight-settling paths used to fall back to raw index 0 when nothing was selectable,
    // which on an all-disabled list is a disabled option: aria-activedescendant pointed at a row
    // carrying aria-disabled="true", the active-highlight class landed on it, a screen reader
    // announced it as current, and Enter silently did nothing while the arrows couldn't move off it.
    // (Header-only and empty lists were already fine -- index 0 there yields no ActiveOption.)

    [Fact]
    public void DefaultOpen_on_an_all_disabled_list_leaves_no_active_descendant()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A", disabled: true), Opt("B", disabled: true) })
            .Add(s => s.DefaultOpen, true));

        Assert.Null(cut.Find(InputSelector).GetAttribute("aria-activedescendant"));
        Assert.DoesNotContain(cut.FindAll(".wss-select-item-option"),
            o => o.ClassList.Contains("wss-select-item-option-active"));
    }

    [Fact]
    public void Reassigning_Options_to_an_all_disabled_list_drops_the_highlight()
    {
        // The other settling path: RebuildFiltered's clamp, reached when Options are swapped while
        // the dropdown is already open (SetInitialActive never runs again).
        string? selected = null;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B") })
            .Add(s => s.DefaultOpen, true)
            .Add(s => s.ValueChanged, (string v) => selected = v));

        cut.Render(p => p.Add(s => s.Options, new List<SelectOption<string>>
        {
            Opt("X", disabled: true),
            Opt("Y", disabled: true),
        }));

        Assert.Null(cut.Find(InputSelector).GetAttribute("aria-activedescendant"));
        Assert.DoesNotContain(cut.FindAll(".wss-select-item-option"),
            o => o.ClassList.Contains("wss-select-item-option-active"));

        // Enter was already inert on a disabled active row -- it must stay inert, not start
        // committing the row the highlight no longer sits on.
        cut.Find(InputSelector).KeyDown(Key("Enter"));
        Assert.Null(selected);
    }

    // ----- finding 4: tag-remove x / clear restore focus ----------------------------------------

    [Fact]
    public void Removing_a_tag_puts_focus_back_on_the_search_input()
    {
        // The x is deleted from the DOM by this render and element removal fires no focusout, so focus
        // fell to <body>: Tab restarted at the top of the page and an open dropdown stayed open with
        // focus outside it.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B") })
            .Add(s => s.Values, new List<string> { "A", "B" }));

        cut.Find("button.wss-select-selection-item-remove").Click();

        JSInterop.VerifyFocusAsyncInvoke(1);
    }

    [Fact]
    public void Backspace_removing_the_last_tag_does_not_re_focus_the_search_input()
    {
        // The refocus above exists for the x button, whose element leaves the DOM and drops focus to
        // <body>. The Backspace path never loses focus -- the search input is what received the
        // keydown -- so refocusing it is pure overhead: on Blazor Server every FocusAsync is a
        // circuit round-trip, and holding Backspace to clear 20 tags issued 20 interop calls where
        // the same gesture previously issued zero.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B") })
            .Add(s => s.Values, new List<string> { "A", "B" }));

        cut.Find(InputSelector).KeyDown(Key("Backspace"));
        cut.Find(InputSelector).KeyDown(Key("Backspace"));

        Assert.Equal(0, JSInterop.Invocations.Count(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Clearing_a_single_select_puts_focus_back_on_the_search_input()
    {
        // ShowClear goes false the moment the value is gone, so the button the user just activated
        // leaves the DOM with focus on it.
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A") })
            .Add(s => s.Value, "A")
            .Add(s => s.AllowClear, true));

        cut.Find("button.wss-select-clear").Click();

        JSInterop.VerifyFocusAsyncInvoke(1);
    }

    [Fact]
    public void Clearing_a_multi_select_puts_focus_back_on_the_search_input()
    {
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("A"), Opt("B") })
            .Add(s => s.Values, new List<string> { "A", "B" })
            .Add(s => s.AllowClear, true));

        cut.Find("button.wss-select-clear").Click();

        JSInterop.VerifyFocusAsyncInvoke(1);
    }

    // ----- finding 20: the multiple-select and clear paths cancel the in-flight debounce ---------

    // Long enough that the timer is unambiguously still in flight when the next interaction happens;
    // a cancelled Task.Delay unwinds immediately, so a passing run never waits for it.
    const int PendingDebounceMs = 5000;

    [Fact]
    public async Task Selecting_in_multiple_mode_cancels_an_in_flight_debounced_search()
    {
        // Only CloseAsync used to drop the pending timer, so a selection let it land afterwards and
        // fire OnSearch("") against the text the selection had already cleared.
        var searches = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Multiple)
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple"), Opt("Apricot") })
            .Add(s => s.Values, new List<string>())
            .Add(s => s.DebounceMilliseconds, PendingDebounceMs)
            .Add(s => s.OnSearch, (string t) => searches.Add(t)));

        var pending = cut.Find(InputSelector).InputAsync(new ChangeEventArgs { Value = "Ap" });
        Assert.False(pending.IsCompleted); // the debounce timer is in flight

        cut.Find(".wss-select-item-option").Click();

        await pending;
        Assert.Empty(searches);
    }

    [Fact]
    public async Task Clearing_cancels_an_in_flight_debounced_search()
    {
        var searches = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, new List<SelectOption<string>> { Opt("Apple") })
            .Add(s => s.Value, "Apple")
            .Add(s => s.AllowClear, true)
            .Add(s => s.DebounceMilliseconds, PendingDebounceMs)
            .Add(s => s.OnSearch, (string t) => searches.Add(t)));

        var pending = cut.Find(InputSelector).InputAsync(new ChangeEventArgs { Value = "Ap" });
        Assert.False(pending.IsCompleted);

        cut.Find("button.wss-select-clear").Click();

        await pending;
        Assert.Empty(searches);
    }

    [Fact]
    public async Task Committing_a_tag_cancels_an_in_flight_debounced_search()
    {
        var searches = new List<string>();
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Mode, SelectMode.Tags)
            .Add(s => s.Options, new List<SelectOption<string>>())
            .Add(s => s.Values, new List<string>())
            .Add(s => s.DebounceMilliseconds, PendingDebounceMs)
            .Add(s => s.OnSearch, (string t) => searches.Add(t)));

        var pending = cut.Find(InputSelector).InputAsync(new ChangeEventArgs { Value = "custom" });
        Assert.False(pending.IsCompleted);

        // Enter flushes the pending search first (that's the flush path's own contract), so this asserts
        // the committed text is the last thing OnSearch ever sees -- never a trailing "".
        cut.Find(InputSelector).KeyDown(Key("Enter"));

        await pending;
        Assert.Equal(new[] { "custom" }, searches);
    }
}
