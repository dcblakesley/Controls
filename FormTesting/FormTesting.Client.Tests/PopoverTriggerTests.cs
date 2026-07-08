using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit coverage for the Popover/Popconfirm trigger contract after the round-3 rework (M9/M11).
/// Only the server-rendered, JS-independent behaviour is observable here: the wrapper carries no
/// button semantics of its own, and the C# render guard limits how often the <c>syncTrigger</c>
/// interop fires. The JS half (ARIA re-resolution, reversible promotion, focus restoration) is
/// exercised by the Playwright e2e suite (PopoverTriggerE2ETests).
/// </summary>
public class PopoverTriggerTests : TestContext
{
    const string OverlayModule = "./_content/WssBlazorControls/wss-overlay.js";

    [Fact]
    public void Popover_wrapper_carries_no_button_semantics_even_when_the_trigger_child_is_swapped()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose; // tolerate the overlay module import

        // Start with a non-focusable child, then swap to a <button>. Wrapper promotion/demotion is a
        // JS concern (covered by e2e); the C#-rendered wrapper must never carry button semantics itself.
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<span>Loading…</span>"));

        var trigger = cut.Find(".wss-popover-trigger");
        Assert.False(trigger.HasAttribute("role"));
        Assert.False(trigger.HasAttribute("tabindex"));

        cut.SetParametersAndRender(p => p.AddChildContent("<button type=\"button\">Open</button>"));

        trigger = cut.Find(".wss-popover-trigger");
        Assert.False(trigger.HasAttribute("role"));
        Assert.False(trigger.HasAttribute("tabindex"));
        Assert.False(trigger.HasAttribute("aria-haspopup")); // ARIA lands on the child via JS, never the wrapper
    }

    [Fact]
    public void Popover_syncs_the_trigger_on_first_render_and_skips_an_unchanged_re_render()
    {
        var module = JSInterop.SetupModule(OverlayModule);
        var sync = module.SetupVoid("syncTrigger", _ => true);
        sync.SetVoidResult();

        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .AddChildContent("<button type=\"button\">Open</button>"));

        cut.WaitForAssertion(() => Assert.Single(sync.Invocations)); // the first render always syncs

        // A re-render that leaves (_open, false) unchanged must not re-invoke syncTrigger.
        cut.SetParametersAndRender(p => p.Add(pv => pv.Title, "changed"));
        Assert.Single(sync.Invocations);
    }

    [Fact]
    public void Popconfirm_disposes_cleanly_after_render()
    {
        JSInterop.Mode = Bunit.JSRuntimeMode.Loose;

        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .AddChildContent("<button type=\"button\">del</button>"));

        cut.Dispose(); // the _disposed guard + module dispose must not throw
    }
}
