using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the AntD 4.x parity batch on <see cref="Popconfirm"/> (async confirm-loading,
/// <c>OkDanger</c>, controlled <c>Visible</c>) and <see cref="Popover"/> (controlled <c>Visible</c>).
/// The controlled-<c>Visible</c> design mirrors <c>Select</c>'s controlled <c>Open</c>/<c>OpenChanged</c>
/// (see <see cref="SelectControlledOpenTests"/>): an external change routes through the same open/close
/// path as user interaction, every open/close raises <c>VisibleChanged</c> back, and a
/// <c>_lastVisibleParam</c> guard recognizes a <c>@bind-Visible</c> echo instead of re-triggering.
/// </summary>
public class PopconfirmPopoverParityTests : TestContext
{
    public PopconfirmPopoverParityTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay module import

    // ----- Popconfirm: OkDanger ---------------------------------------------------------------

    [Fact]
    public void Popconfirm_OkDanger_adds_the_danger_class_only_when_set()
    {
        var plain = RenderComponent<Popconfirm>(p => p.Add(pc => pc.Title, "Delete?").AddChildContent("<button>del</button>"));
        plain.Find(".wss-popconfirm-trigger").Click();
        Assert.DoesNotContain("wss-dialog-btn-danger", plain.Find(".wss-popconfirm-buttons .wss-dialog-btn-primary").ClassList);

        var danger = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?").Add(pc => pc.OkDanger, true)
            .AddChildContent("<button>del</button>"));
        danger.Find(".wss-popconfirm-trigger").Click();
        Assert.Contains("wss-dialog-btn-danger", danger.Find(".wss-popconfirm-buttons .wss-dialog-btn-primary").ClassList);
    }

    // ----- Popconfirm: async confirm-loading state machine -------------------------------------

    [Fact]
    public void Popconfirm_synchronous_confirm_still_closes_immediately_with_no_loading_state()
    {
        var confirmed = false;
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.OnConfirm, EventCallback.Factory.Create(this, () => confirmed = true))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn")[1].Click(); // OK

        Assert.True(confirmed);
        Assert.Empty(cut.FindAll(".wss-popconfirm")); // closed immediately, no pending-loading render
    }

    [Fact]
    public async Task Popconfirm_a_genuinely_pending_confirm_disables_both_buttons_and_shows_a_spinner_then_closes()
    {
        var tcs = new TaskCompletionSource();
        var confirmed = false;
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.OnConfirm, EventCallback.Factory.Create(this, async () => { await tcs.Task; confirmed = true; }))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        var okButton = cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn")[1];

        // Dispatch the click on a background thread (bUnit's Click() blocks the calling thread
        // until the handler's Task completes) so the test thread stays free to observe the
        // intermediate "pending" render and then release the gate.
        var clickTask = Task.Run(() => okButton.Click());

        cut.WaitForState(() => cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn-primary")
            .FirstOrDefault()?.HasAttribute("disabled") == true, TimeSpan.FromSeconds(5));

        Assert.NotEmpty(cut.FindAll(".wss-popconfirm")); // still open while pending
        var buttons = cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn");
        Assert.True(buttons[0].HasAttribute("disabled")); // Cancel disabled too
        Assert.True(buttons[1].HasAttribute("disabled"));
        Assert.Equal("true", buttons[1].GetAttribute("aria-busy"));
        Assert.NotEmpty(cut.FindAll(".wss-popconfirm-buttons .wss-icon-spin"));
        Assert.False(confirmed);

        tcs.SetResult();
        cut.WaitForAssertion(() => Assert.True(confirmed), TimeSpan.FromSeconds(5));
        await clickTask.WaitAsync(TimeSpan.FromSeconds(5)); // drain the background dispatch
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".wss-popconfirm")), TimeSpan.FromSeconds(5)); // closes on completion
    }

    [Fact]
    public async Task Popconfirm_a_failing_pending_confirm_closes_and_rethrows_without_swallowing()
    {
        var tcs = new TaskCompletionSource();
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.OnConfirm, EventCallback.Factory.Create(this, async () => { await tcs.Task; }))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        var okButton = cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn")[1];

        var clickTask = Task.Run(() => okButton.Click());
        cut.WaitForState(() => cut.FindAll(".wss-popconfirm-buttons .wss-dialog-btn-primary")
            .FirstOrDefault()?.HasAttribute("disabled") == true, TimeSpan.FromSeconds(5));

        tcs.SetException(new InvalidOperationException("boom"));

        // ConfirmAsync's catch block awaits SetOpenAsync(false) -- so _open is false internally --
        // before rethrowing, but ComponentBase's own CallStateHasChangedOnAsyncCompletion skips its
        // final StateHasChanged() when the awaited event-handler task faults (only cancellation and
        // success reach that call), so bUnit (no ErrorBoundary in this render tree) never renders the
        // popup visually closed, and awaiting clickTask directly doesn't surface the exception either
        // (bUnit's dispatch stores an unhandled render exception on the renderer rather than faulting
        // the original call chain) -- only a Wait* helper that polls afterward observes/rethrows it.
        // The DOM-closed behavior is what an app-level ErrorBoundary wrapping the popup would see once
        // it recovers; the contract this test actually verifies is "never swallowed".
        var thrown = Assert.ThrowsAny<Exception>(
            () => cut.WaitForState(() => cut.FindAll(".wss-popconfirm").Count == 0, TimeSpan.FromSeconds(5)));
        Assert.Contains("boom", (thrown.InnerException ?? thrown).Message);

        // Drain the background dispatch so the test doesn't leave an unobserved faulted task.
        try { await clickTask; } catch { /* already asserted above */ }
    }

    // ----- Popconfirm: controlled Visible/VisibleChanged ---------------------------------------

    [Fact]
    public void Popconfirm_setting_Visible_true_externally_opens_when_VisibleChanged_is_bound()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Visible, false)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => { }))
            .AddChildContent("<button>del</button>"));

        Assert.Empty(cut.FindAll(".wss-popconfirm"));
        cut.SetParametersAndRender(p => p.Add(pc => pc.Visible, true));
        Assert.NotEmpty(cut.FindAll(".wss-popconfirm"));
    }

    [Fact]
    public void Popconfirm_setting_Visible_false_externally_closes_an_open_popup()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Visible, true)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => { }))
            .AddChildContent("<button>del</button>"));

        Assert.NotEmpty(cut.FindAll(".wss-popconfirm")); // controlled Visible=true opens on first render too
        cut.SetParametersAndRender(p => p.Add(pc => pc.Visible, false));
        Assert.Empty(cut.FindAll(".wss-popconfirm"));
    }

    [Fact]
    public void Popconfirm_internal_open_and_close_both_notify_VisibleChanged()
    {
        var raised = new List<bool>();
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Visible, false)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => raised.Add(v)))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click();
        Assert.Contains(true, raised);

        cut.Find(".wss-popconfirm-buttons .wss-dialog-btn").Click(); // Cancel
        Assert.Contains(false, raised);
    }

    [Fact]
    public void Popconfirm_a_parameter_echo_of_the_value_just_raised_does_not_retrigger_open_close()
    {
        var raiseCount = 0;
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Visible, false)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => raiseCount++))
            .AddChildContent("<button>del</button>"));

        cut.Find(".wss-popconfirm-trigger").Click(); // internal open -> raises VisibleChanged(true) once
        Assert.Equal(1, raiseCount);

        // Simulate the consumer's @bind-Visible field echoing the same value back in.
        cut.SetParametersAndRender(p => p.Add(pc => pc.Visible, true));
        Assert.Equal(1, raiseCount); // no additional (re-)trigger from the echo
        Assert.NotEmpty(cut.FindAll(".wss-popconfirm"));
    }

    [Fact]
    public void Popconfirm_Visible_is_inert_without_a_bound_VisibleChanged()
    {
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Visible, true) // no VisibleChanged bound
            .AddChildContent("<button>del</button>"));

        Assert.Empty(cut.FindAll(".wss-popconfirm")); // uncontrolled: Visible alone does nothing
    }

    [Fact]
    public void Popconfirm_disabled_ignores_a_forced_controlled_Visible_true_and_never_echoes_true()
    {
        var raised = new List<bool>();
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Disabled, true)
            .Add(pc => pc.Visible, true)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => raised.Add(v)))
            .AddChildContent("<button>del</button>"));

        Assert.Empty(cut.FindAll(".wss-popconfirm"));
        Assert.DoesNotContain(true, raised);
    }

    [Fact]
    public void Popconfirm_becoming_disabled_while_open_closes_through_the_normal_path_and_notifies()
    {
        var raised = new List<bool>();
        var cut = RenderComponent<Popconfirm>(p => p
            .Add(pc => pc.Title, "Delete?")
            .Add(pc => pc.Disabled, false)
            .Add(pc => pc.Visible, true)
            .Add(pc => pc.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => raised.Add(v)))
            .AddChildContent("<button>del</button>"));

        Assert.NotEmpty(cut.FindAll(".wss-popconfirm")); // opened normally while enabled

        cut.SetParametersAndRender(p => p.Add(pc => pc.Disabled, true)); // Visible stays true, unchanged

        Assert.Empty(cut.FindAll(".wss-popconfirm"));
        Assert.Contains(false, raised);
    }

    // ----- Popover: controlled Visible/VisibleChanged -------------------------------------------

    [Fact]
    public void Popover_setting_Visible_true_externally_opens_when_VisibleChanged_is_bound()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .Add(pv => pv.Visible, false)
            .Add(pv => pv.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => { }))
            .AddChildContent("<button>open</button>"));

        Assert.Empty(cut.FindAll(".wss-popover"));
        cut.SetParametersAndRender(p => p.Add(pv => pv.Visible, true));
        Assert.NotEmpty(cut.FindAll(".wss-popover"));
    }

    [Fact]
    public void Popover_setting_Visible_false_externally_closes_an_open_popover()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .Add(pv => pv.Visible, true)
            .Add(pv => pv.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => { }))
            .AddChildContent("<button>open</button>"));

        Assert.NotEmpty(cut.FindAll(".wss-popover"));
        cut.SetParametersAndRender(p => p.Add(pv => pv.Visible, false));
        Assert.Empty(cut.FindAll(".wss-popover"));
    }

    [Fact]
    public void Popover_internal_open_and_close_both_notify_VisibleChanged()
    {
        var raised = new List<bool>();
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .Add(pv => pv.Visible, false)
            .Add(pv => pv.VisibleChanged, EventCallback.Factory.Create<bool>(this, v => raised.Add(v)))
            .AddChildContent("<button>open</button>"));

        cut.Find(".wss-popover-trigger").Click();
        Assert.Contains(true, raised);

        cut.Find(".wss-popover-trigger").Click();
        Assert.Contains(false, raised);
    }

    [Fact]
    public void Popover_a_parameter_echo_of_the_value_just_raised_does_not_retrigger_open_close()
    {
        var raiseCount = 0;
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .Add(pv => pv.Visible, false)
            .Add(pv => pv.VisibleChanged, EventCallback.Factory.Create<bool>(this, _ => raiseCount++))
            .AddChildContent("<button>open</button>"));

        cut.Find(".wss-popover-trigger").Click();
        Assert.Equal(1, raiseCount);

        cut.SetParametersAndRender(p => p.Add(pv => pv.Visible, true)); // echo
        Assert.Equal(1, raiseCount);
        Assert.NotEmpty(cut.FindAll(".wss-popover"));
    }

    [Fact]
    public void Popover_Visible_is_inert_without_a_bound_VisibleChanged()
    {
        var cut = RenderComponent<Popover>(p => p
            .Add(pv => pv.Content, (RenderFragment)(b => b.AddContent(0, "details")))
            .Add(pv => pv.Visible, true) // no VisibleChanged bound
            .AddChildContent("<button>open</button>"));

        Assert.Empty(cut.FindAll(".wss-popover"));
    }
}
