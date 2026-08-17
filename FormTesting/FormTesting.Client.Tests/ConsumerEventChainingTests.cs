using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// The other half of <see cref="ConsumerEventSplatTests"/>: where a control binds an event handler of
/// its own on the <em>same</em> element it splats the consumer's unmatched attributes onto, the two
/// must both run. Blazor resolves duplicate attribute names last-wins, so the library's explicit
/// <c>@onkeydown</c> written after the splat used to discard the consumer's silently; the controls
/// below now withhold the colliding name from the splat
/// (<c>AttributeSplat.RestExcept</c>) and re-invoke the consumer's handler themselves
/// (<c>ConsumerEvent</c>).
/// </summary>
/// <remarks>
/// <para>
/// Two things are asserted at every site, because either alone would pass a broken fix: the
/// <strong>library's own effect</strong> (the value stepped, the dialog closed, the dropdown opened)
/// and the <strong>consumer's counter</strong>.
/// </para>
/// <para>
/// The <strong>ordering is library-first, consumer-second</strong> — the ordering a consumer's
/// existing wrapping-element workaround already gives them by ordinary bubbling. Where it is
/// observable (the pickers' Escape, whose close the consumer's handler can see) a test pins it.
/// </para>
/// <para>
/// The consumer's handler must fire <strong>even when the library's own handler early-returns</strong>
/// — a disabled slider, a disabled <c>Select</c>, a dialog with <c>Keyboard="false"</c>, a key the
/// control has no opinion about. Only the library's state mutation is suppressed, never the
/// consumer's listener. Each of those has its own test below.
/// </para>
/// </remarks>
public class ConsumerEventChainingTests : BunitContext
{
    public ConsumerEventChainingTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the lazy JS imports

    // EditRange.TrackWidth: the width the no-JS click fallback normalizes OffsetX against.
    const double TrackWidth = 320d;

    class RangeModel { public int Volume { get; set; } = 40; }

    class DateModel
    {
        public DateTime? Start { get; set; } = new DateTime(2026, 2, 14);
        public DateTime? End { get; set; } = new DateTime(2026, 2, 20);
    }

    // ───────────────────────────── EditRange<int> ─────────────────────────────

    IRenderedComponent<ContainerFragment> RenderRange(
        RangeModel model, Action<RenderTreeBuilder, int>? extra = null)
    {
        Expression<Func<int>> field = () => model.Volume;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRange<int>>(0);
            b.AddAttribute(1, "Value", model.Volume);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<int>(this, v => model.Volume = v));
            extra?.Invoke(b, 4);
            b.CloseComponent();
        }));
    }

    static IElement Track(IRenderedComponent<ContainerFragment> cut) => cut.Find(".edit-range-track");

    [Fact]
    public void EditRange_steps_its_own_value_AND_runs_a_splatted_consumer_onkeydown()
    {
        var model = new RangeModel();
        var keyDowns = 0;
        var cut = RenderRange(model, (b, seq) =>
            b.AddAttribute(seq, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        Track(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(41, model.Volume); // the library's own stepping still happened...
        Assert.Equal(1, keyDowns);      // ...and the consumer's handler was not discarded.
    }

    [Fact]
    public void EditRange_runs_a_splatted_consumer_onkeydown_even_while_disabled()
    {
        // OnKeyDown early-returns on IsDisabled. Only the STEPPING is suppressed: a consumer
        // listening for keys on a disabled slider must still hear them.
        var model = new RangeModel();
        var keyDowns = 0;
        var cut = RenderRange(model, (b, seq) =>
        {
            b.AddAttribute(seq, "IsDisabled", true);
            b.AddAttribute(seq + 1, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++));
        });

        Track(cut).KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal(40, model.Volume); // library stepping correctly suppressed...
        Assert.Equal(1, keyDowns);      // ...consumer handler still ran.
    }

    [Fact]
    public void EditRange_runs_a_splatted_consumer_onkeydown_for_a_key_the_control_ignores()
    {
        var model = new RangeModel();
        var keyDowns = 0;
        var cut = RenderRange(model, (b, seq) =>
            b.AddAttribute(seq, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        Track(cut).KeyDown(new KeyboardEventArgs { Key = "q" });

        Assert.Equal(40, model.Volume);
        Assert.Equal(1, keyDowns);
    }

    [Fact]
    public void EditRange_commits_its_own_track_click_AND_runs_a_splatted_consumer_onclick()
    {
        // Strict, so the wss-slider.js import throws and the no-JS click fallback is live (the same
        // split EditRangeTests uses) -- that gives a library effect to observe alongside the counter.
        JSInterop.Mode = JSRuntimeMode.Strict;
        var model = new RangeModel();
        var clicks = 0;
        var cut = RenderRange(model, (b, seq) =>
            b.AddAttribute(seq, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        Track(cut).Click(new MouseEventArgs { OffsetX = TrackWidth / 2 });

        Assert.Equal(50, model.Volume);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void EditRange_runs_a_splatted_consumer_onclick_even_when_the_drag_module_owns_the_track()
    {
        // The Loose default stands in for "wss-slider.js loaded", which makes OnTrackClick inert.
        // The consumer's own onclick is chained past that early return.
        var model = new RangeModel();
        var clicks = 0;
        var cut = RenderRange(model, (b, seq) =>
            b.AddAttribute(seq, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        Track(cut).Click(new MouseEventArgs { OffsetX = TrackWidth / 2 });

        Assert.Equal(40, model.Volume); // library click fallback correctly inert...
        Assert.Equal(1, clicks);        // ...consumer handler still ran.
    }

    // ───────────────────────────── DatePicker / EditDate ─────────────────────────────

    [Fact]
    public void DatePicker_closes_on_Escape_AND_runs_a_splatted_consumer_onkeydown()
    {
        var keyDowns = 0;
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-picker-input").Click();
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));

        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown")); // the library's own Escape-to-close ran...
        Assert.Equal(1, keyDowns);                         // ...and so did the consumer's handler.
    }

    [Fact]
    public void DatePicker_runs_a_splatted_consumer_onkeydown_for_a_key_it_ignores_while_closed()
    {
        var keyDowns = 0;
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" }); // no-op while closed

        Assert.Equal(2, keyDowns);
    }

    [Fact]
    public void EditDate_forwards_a_splatted_consumer_onkeydown_through_to_the_pickers_wrapper()
    {
        // EditDate hands its WHOLE splat to the inner DatePicker's AdditionalAttributes
        // (EditControlInit.BuildPickerAttributes), so the collision -- and the fix -- is reached
        // through the form control too.
        var model = new DateModel();
        Expression<Func<DateTime?>> field = () => model.Start;
        var keyDowns = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Start);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
            b.AddAttribute(4, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));

        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(1, keyDowns);
    }

    // ───────────────────────────── DateRangePicker / EditDateRange ─────────────────────────────

    [Fact]
    public void DateRangePicker_closes_on_Escape_AND_runs_a_splatted_consumer_onkeydown()
    {
        var keyDowns = 0;
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-picker-input").Click();
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));

        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(1, keyDowns);
    }

    [Fact]
    public void EditDateRange_forwards_a_splatted_consumer_onkeydown_through_to_the_pickers_wrapper()
    {
        var model = new DateModel();
        Expression<Func<DateTime?>> start = () => model.Start;
        Expression<Func<DateTime?>> end = () => model.End;
        var keyDowns = 0;

        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", start);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", end);
            b.AddAttribute(5, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown"));

        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(cut.FindAll(".wss-picker-dropdown"));
        Assert.Equal(1, keyDowns);
    }

    // ───────────────────────────── Modal ─────────────────────────────

    [Fact]
    public void Modal_closes_on_Escape_AND_runs_a_splatted_consumer_onkeydown()
    {
        var visible = true;
        var keyDowns = 0;
        var cut = Render<Modal>(p => p
            .Add(m => m.Visible, true)
            .Add(m => m.Title, "Confirm")
            .Add(m => m.VisibleChanged, (bool v) => visible = v)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-modal").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(visible);     // the library's own Escape-to-cancel ran...
        Assert.Equal(1, keyDowns); // ...and so did the consumer's handler.
    }

    [Fact]
    public void Modal_runs_a_splatted_consumer_onkeydown_even_with_Keyboard_false()
    {
        var visible = true;
        var keyDowns = 0;
        var cut = Render<Modal>(p => p
            .Add(m => m.Visible, true)
            .Add(m => m.Title, "Confirm")
            .Add(m => m.Keyboard, false)
            .Add(m => m.VisibleChanged, (bool v) => visible = v)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-modal").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(visible);      // Keyboard="false" correctly suppressed the close...
        Assert.Equal(1, keyDowns); // ...but never the consumer's listener.
    }

    [Fact]
    public void Modal_keeps_its_panel_mousedown_gesture_tracking_AND_runs_a_splatted_consumer_onmousedown()
    {
        var visible = true;
        var mouseDowns = 0;
        var cut = Render<Modal>(p => p
            .Add(m => m.Visible, true)
            .Add(m => m.Title, "Confirm")
            .Add(m => m.VisibleChanged, (bool v) => visible = v)
            .AddUnmatched("onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, () => mouseDowns++)));

        // The cross-boundary "changed my mind" drag: press inside the panel, release on the mask.
        // OnPanelMouseDown clears the mask-down flag, so the composed click must NOT close.
        cut.Find(".wss-modal").MouseDown();
        cut.Find(".wss-modal-wrap").MouseUp();
        cut.Find(".wss-modal-wrap").Click();

        Assert.True(visible);        // the library's own gesture tracking survived...
        Assert.Equal(1, mouseDowns); // ...and the consumer's handler ran too.
    }

    // ───────────────────────────── Drawer ─────────────────────────────

    [Fact]
    public void Drawer_closes_on_Escape_AND_runs_a_splatted_consumer_onkeydown()
    {
        var visible = true;
        var keyDowns = 0;
        var cut = Render<Drawer>(p => p
            .Add(d => d.Visible, true)
            .Add(d => d.Title, "Filters")
            .Add(d => d.VisibleChanged, (bool v) => visible = v)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-drawer").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.False(visible);
        Assert.Equal(1, keyDowns);
    }

    [Fact]
    public void Drawer_runs_a_splatted_consumer_onkeydown_even_with_Keyboard_false()
    {
        var visible = true;
        var keyDowns = 0;
        var cut = Render<Drawer>(p => p
            .Add(d => d.Visible, true)
            .Add(d => d.Title, "Filters")
            .Add(d => d.Keyboard, false)
            .Add(d => d.VisibleChanged, (bool v) => visible = v)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-drawer").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.True(visible);
        Assert.Equal(1, keyDowns);
    }

    // ───────────────────────────── Select<TValue> ─────────────────────────────

    static List<SelectOption<string>> Opts(params string[] values) =>
        values.Select(v => new SelectOption<string>(v, v)).ToList();

    [Fact]
    public void Select_opens_on_a_wrapper_click_AND_runs_a_splatted_consumer_onclick()
    {
        var clicks = 0;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Low", "High"))
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        cut.Find(".wss-select").Click();

        Assert.NotEmpty(cut.FindAll(".wss-select-dropdown")); // the engine's own open still happened...
        Assert.Equal(1, clicks);                              // ...and the consumer's handler ran.
    }

    [Fact]
    public void Select_runs_a_splatted_consumer_onclick_even_while_disabled()
    {
        var clicks = 0;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Low", "High"))
            .Add(s => s.Disabled, true)
            .AddUnmatched("onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => clicks++)));

        cut.Find(".wss-select").Click();

        Assert.Empty(cut.FindAll(".wss-select-dropdown")); // Disabled correctly suppressed the open...
        Assert.Equal(1, clicks);                           // ...but never the consumer's listener.
    }

    [Fact]
    public void Select_leaves_a_wrapper_level_consumer_onkeydown_splatting_through_untouched()
    {
        // The DELIBERATE exception, pinned so a future change has to argue with a test: Select's own
        // keyboard handling lives on the inner search input, not the wrapper, so a wrapper-level
        // consumer onkeydown is splatted normally (no collision, nothing to chain). While the popup
        // is CLOSED it hears keys bubbling up from the input; once open, that input's
        // @onkeydown:stopPropagation cuts it off, because an open popup owns the keyboard.
        var keyDowns = 0;
        var cut = Render<Select<string>>(p => p
            .Add(s => s.Options, Opts("Low", "High"))
            .Add(s => s.ShowSearch, true)
            .AddUnmatched("onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, () => keyDowns++)));

        cut.Find(".wss-select").KeyDown(new KeyboardEventArgs { Key = "a" });

        Assert.Equal(1, keyDowns);
    }
}
