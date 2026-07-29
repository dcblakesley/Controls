namespace FormTesting.Client.Tests;

/// <summary>
/// Pins the JS activation-handle lifecycle shared by <see cref="Modal"/>/<see cref="Drawer"/> (via
/// <c>OverlayActivationBase</c>) at the level bUnit can observe: <c>activateModal</c> runs on the
/// transition to visible and not again while it stays visible, a close releases the handle so the next
/// open activates a <em>fresh</em> one, and disposing while visible is exception-free. bUnit executes
/// no JavaScript, so the handle it hands back is null there — that the JS-side <c>dispose</c> actually
/// runs, and that a close→reopen across an in-flight activation orphans nothing, is only observable in
/// the e2e suite.
/// </summary>
public class OverlayActivationHandleTests : BunitContext
{
    // Loose mode so the module import resolves to a recording stub instead of throwing (throwing is the
    // no-JS path the other dialog tests deliberately exercise).
    public OverlayActivationHandleTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    int ActivateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "activateModal");

    [Fact]
    public void Modal_activates_once_per_open_and_reactivates_after_a_close()
    {
        var cut = Render<Modal>(p => p.Add(m => m.Visible, true).Add(m => m.Title, "T"));
        Assert.Equal(1, ActivateCalls());

        // Still visible: a re-render must not activate a second focus trap over the first. The handle
        // holder can only hold one, so the other would be orphaned along with its body-scroll lock.
        cut.Render(p => p.Add(m => m.Title, "T2"));
        Assert.Equal(1, ActivateCalls());

        // A close releases the handle, so reopening has to activate a new one.
        cut.Render(p => p.Add(m => m.Visible, false));
        Assert.Equal(1, ActivateCalls());
        cut.Render(p => p.Add(m => m.Visible, true));
        Assert.Equal(2, ActivateCalls());
    }

    [Fact]
    public async Task Modal_disposed_while_visible_tears_down_without_throwing()
    {
        var cut = Render<Modal>(p => p.Add(m => m.Visible, true).Add(m => m.Title, "T"));
        Assert.Equal(1, ActivateCalls());

        // The handle holder flips itself closed before releasing, so an activation still in flight
        // releases its own late-arriving handle instead of stranding it on this dead instance.
        await DisposeComponentsAsync();

        Assert.Equal(1, ActivateCalls()); // nothing re-activates on the way down
    }

    [Fact]
    public void Drawer_shares_the_same_activation_lifecycle()
    {
        var cut = Render<Drawer>(p => p.Add(d => d.Visible, true).Add(d => d.Title, "T"));
        Assert.Equal(1, ActivateCalls());

        cut.Render(p => p.Add(d => d.Visible, false));
        cut.Render(p => p.Add(d => d.Visible, true));

        Assert.Equal(2, ActivateCalls());
    }
}
