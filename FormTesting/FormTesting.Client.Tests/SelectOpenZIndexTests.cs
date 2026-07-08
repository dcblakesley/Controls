using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers L13 (C# mirrors the open z-index placeDropdown returns into the wrapper's Blazor-bound
/// <c>style</c>, so a re-render can't clobber it) and the L12 dispose-race hardening on
/// <see cref="Select{TValue}"/>. bUnit can't run the real module, so the z-index tests fake
/// <c>placeDropdown</c> to return a fixed value; the disposal tests assert the new <c>_disposed</c>
/// guard + import re-check path tears down without throwing (the leak itself isn't observable here).
/// </summary>
public class SelectOpenZIndexTests : TestContext
{
    public SelectOpenZIndexTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the scroll/init/clearZ imports

    static List<SelectOption<string>> Opts(params string[] values) =>
        values.Select(v => new SelectOption<string>(v, v)).ToList();

    // Registers the RCL module and pins placeDropdown's return so C# has a concrete z to mirror.
    void FakePlaceDropdown(int z)
    {
        var module = JSInterop.SetupModule("./_content/WssBlazorControls/wss-select.js");
        module.Setup<int>("placeDropdown", _ => true).SetResult(z);
    }

    [Fact]
    public void Open_mirrors_the_placeDropdown_z_index_into_the_wrapper_style()
    {
        FakePlaceDropdown(1051);

        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B"))
            .Add(s => s.Width, "200px")
            .Add(s => s.DefaultOpen, true));

        // After the open render's OnAfterRenderAsync positions the panel, WidthStyle re-emits the z
        // so a later bound-style re-render can't drop the wrapper below its own backdrop.
        cut.WaitForAssertion(() =>
            Assert.Contains("z-index:1051", cut.Find(".wss-select").GetAttribute("style") ?? string.Empty));
    }

    [Fact]
    public void A_mid_open_Width_change_keeps_the_z_index_in_the_style()
    {
        FakePlaceDropdown(1051);

        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B"))
            .Add(s => s.Width, "200px")
            .Add(s => s.DefaultOpen, true));

        cut.WaitForAssertion(() =>
            Assert.Contains("z-index:1051", cut.Find(".wss-select").GetAttribute("style") ?? string.Empty));

        // The L13 trigger: parent re-renders with a different Width while the dropdown is open. The
        // rewritten style attribute must still carry the mirrored z-index (not just the new width).
        cut.SetParametersAndRender(p => p.Add(s => s.Width, "300px"));

        var style = cut.Find(".wss-select").GetAttribute("style") ?? string.Empty;
        Assert.Contains("width:300px", style);
        Assert.Contains("z-index:1051", style);
    }

    [Fact]
    public void Closing_drops_the_mirrored_z_index_from_the_style()
    {
        FakePlaceDropdown(1051);

        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B"))
            .Add(s => s.Width, "200px")
            .Add(s => s.DefaultOpen, true));

        cut.WaitForAssertion(() =>
            Assert.Contains("z-index:1051", cut.Find(".wss-select").GetAttribute("style") ?? string.Empty));

        // Escape closes the dropdown; the close render must no longer emit the z (a stale high z on a
        // persistent wrapper would poke through later overlays' masks).
        cut.Find("input.wss-select-selection-search-input").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.DoesNotContain("z-index", cut.Find(".wss-select").GetAttribute("style") ?? string.Empty);
    }

    [Fact]
    public void Disposing_a_closed_Select_does_not_throw()
    {
        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B")));

        cut.Dispose();
    }

    [Fact]
    public void Disposing_an_open_Select_does_not_throw()
    {
        // Exercises the L12 path: an open dropdown has imported the module; DisposeAsync sets _disposed
        // and tears the module down. The race (import completing after disposal) isn't observable in
        // bUnit, but the guarded disposal must not throw.
        FakePlaceDropdown(1051);

        var cut = RenderComponent<Select<string>>(p => p
            .Add(s => s.Options, Opts("A", "B"))
            .Add(s => s.DefaultOpen, true));

        cut.Dispose();
    }
}
