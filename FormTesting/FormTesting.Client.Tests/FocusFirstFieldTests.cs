using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// <see cref="FormDefaults.FocusFirstField"/>'s C# half: the resolution chain, the once-per-instance
/// firing, and the pair of <c>&lt;template&gt;</c> scope markers that only exist while the feature is
/// on. What is deliberately NOT here is whether focus moved — that is decided in
/// <c>WssEditControls.focusFirstField</c> from the rendered DOM, and bUnit's JS interop is a
/// recorder, not a browser. <c>FocusFirstFieldE2ETests</c> owns every assertion about
/// <c>document.activeElement</c>, the skip rules, and the precedence of a control's own
/// <c>FocusOnFirstRender</c>.
/// </summary>
public class FocusFirstFieldTests : BunitContext
{
    const string Identifier = "WssEditControls.focusFirstField";

    // A scope with plain (non-control) child content: nothing else in the tree makes an interop call,
    // so every recorded invocation below is unambiguously this feature's.
    IRenderedComponent<FormDefaults> RenderScope(bool? focusFirstField) =>
        Render<FormDefaults>(p => p
            .Add(o => o.FocusFirstField, focusFirstField)
            .Add(o => o.ChildContent, (RenderFragment)(b =>
            {
                b.OpenElement(0, "p");
                b.AddContent(1, "content");
                b.CloseElement();
            })));

    static IReadOnlyList<IElement> Markers(IRenderedComponent<FormDefaults> cut) => cut.FindAll("template");

    // ───────────────────────────── default off ─────────────────────────────

    [Fact]
    public void Unset_renders_no_markers_and_makes_no_interop_call()
    {
        // The byte-identical-when-off contract: a FormDefaults that doesn't opt in must emit exactly
        // what it emitted before this feature existed, and must not talk to JS at all.
        var cut = RenderScope(null);

        Assert.Empty(Markers(cut));
        Assert.Empty(JSInterop.Invocations);
        Assert.Equal("<p>content</p>", cut.Markup);
    }

    [Fact]
    public void An_explicit_false_is_off_too()
    {
        var cut = RenderScope(false);

        Assert.Empty(Markers(cut));
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void Unset_leaves_EffectiveFocusFirstField_null_rather_than_false()
    {
        // Null and false are distinguishable all the way through, so an inner scope can opt OUT from
        // under an opted-in outer one (see the nesting tests below) instead of merely failing to opt in.
        Assert.Null(RenderScope(null).Instance.EffectiveFocusFirstField);
        Assert.False(RenderScope(false).Instance.EffectiveFocusFirstField);
    }

    // ───────────────────────────── armed ─────────────────────────────

    [Fact]
    public void On_renders_a_start_and_end_marker_around_the_child_content()
    {
        var cut = RenderScope(true);

        var markers = Markers(cut);
        Assert.Equal(2, markers.Count);
        Assert.StartsWith("wss-focus-scope-", markers[0].Id);
        Assert.Equal(markers[0].Id + "-end", markers[1].Id);
        // Between them, and nothing else: the markers are inert anchors, not a wrapper.
        Assert.Equal($"<template id=\"{markers[0].Id}\"></template><p>content</p>"
            + $"<template id=\"{markers[1].Id}\"></template>", cut.Markup);
    }

    [Fact]
    public void On_carries_nothing_but_the_id_on_each_marker()
    {
        // No class, no style, no data-*: a consumer stylesheet can't accidentally target these, and
        // there is no attribute for a future change to have to keep byte-compatible.
        var cut = RenderScope(true);

        foreach (var marker in Markers(cut))
            Assert.Equal(["id"], marker.Attributes.Select(a => a.Name).ToArray());
    }

    [Fact]
    public void On_invokes_focusFirstField_once_with_the_start_marker_id()
    {
        var planned = JSInterop.SetupVoid(Identifier, _ => true);
        planned.SetVoidResult();

        var cut = RenderScope(true);

        var invocation = Assert.Single(planned.Invocations);
        // The START marker only — the JS side derives the end marker's id by appending the same
        // suffix, so exactly one id crosses the interop boundary.
        Assert.Equal(Markers(cut)[0].Id, Assert.Single(invocation.Arguments));
    }

    [Fact]
    public void It_fires_once_per_instance_and_not_on_later_renders()
    {
        // "On FIRST render" is the whole contract: a value change, a validation pass or any other
        // re-render must not drag focus back to the top of the form.
        var planned = JSInterop.SetupVoid(Identifier, _ => true);
        planned.SetVoidResult();

        var cut = RenderScope(true);
        cut.Render();
        cut.Render();

        Assert.Single(planned.Invocations);
    }

    [Fact]
    public async Task It_never_throws_when_js_is_unavailable()
    {
        // Strict-mode JSInterop throws on any unconfigured call, exactly as a prerender IJSRuntime
        // does. Rendering an armed scope must still succeed — the no-JS fallback is simply that focus
        // doesn't move (see JsInteropEc's best-effort contract).
        var cut = RenderScope(true);

        await cut.InvokeAsync(() => { });
        Assert.Equal(2, Markers(cut).Count);
    }

    [Fact]
    public void Two_sibling_scopes_get_distinct_marker_ids()
    {
        // Marker ids have to be unique document-wide (an MFE root plus a dialog's own scope), with no
        // shared counter to coordinate through.
        var planned = JSInterop.SetupVoid(Identifier, _ => true);
        planned.SetVoidResult();

        var cut = Render(b =>
        {
            b.OpenComponent<FormDefaults>(0);
            b.AddAttribute(1, nameof(FormDefaults.FocusFirstField), (bool?)true);
            b.CloseComponent();
            b.OpenComponent<FormDefaults>(2);
            b.AddAttribute(3, nameof(FormDefaults.FocusFirstField), (bool?)true);
            b.CloseComponent();
        });

        var ids = cut.FindAll("template").Select(t => t.Id).ToArray();
        Assert.Equal(4, ids.Length);
        Assert.Equal(4, ids.Distinct().Count());
        Assert.Equal(2, planned.Invocations.Count);
    }

    // ───────────────────────── the nesting chain ─────────────────────────

    // outer FormDefaults -> inner FormDefaults -> a <p>, mirroring RenderNestedAssetBase in
    // FormDefaultsTests: the MFE composition shape, host-page defaults wrapping an MFE root's own.
    IRenderedComponent<FormDefaults> RenderNested(bool? outer, bool? inner) =>
        Render<FormDefaults>(p => p
            .Add(o => o.FocusFirstField, outer)
            .Add(o => o.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<FormDefaults>(0);
                b.AddAttribute(1, nameof(FormDefaults.FocusFirstField), inner);
                b.CloseComponent();
            })));

    [Fact]
    public void An_unset_inner_FocusFirstField_falls_through_to_the_outer()
    {
        var cut = RenderNested(outer: true, inner: null);

        Assert.True(cut.FindComponent<FormDefaults>().Instance.EffectiveFocusFirstField);
    }

    [Fact]
    public void An_inner_FocusFirstField_wins_over_the_outer()
    {
        var cut = RenderNested(outer: null, inner: true);

        Assert.True(cut.FindComponent<FormDefaults>().Instance.EffectiveFocusFirstField);
        Assert.Null(cut.Instance.EffectiveFocusFirstField);
    }

    [Fact]
    public void An_inner_false_opts_its_own_scope_out_from_under_a_true_outer()
    {
        var planned = JSInterop.SetupVoid(Identifier, _ => true);
        planned.SetVoidResult();

        var cut = RenderNested(outer: true, inner: false);

        Assert.False(cut.FindComponent<FormDefaults>().Instance.EffectiveFocusFirstField);
        // Only the outer scope armed: two markers, one call. (The outer scope still SPANS the inner
        // one, so this opts the inner instance out of firing, not its fields out of being candidates.)
        Assert.Equal(2, cut.FindAll("template").Count);
        Assert.Single(planned.Invocations);
    }

    [Fact]
    public void Each_armed_scope_in_a_nest_arms_and_fires_for_itself()
    {
        // Both fire, and that is deliberate rather than wasteful: it is what makes a dialog's own
        // FormDefaults focus its form when it opens, long after an enclosing app-root scope already
        // fired at page load. The JS-side "don't take focus off a field that has it" guard is what
        // keeps the two from fighting when they DO overlap.
        var planned = JSInterop.SetupVoid(Identifier, _ => true);
        planned.SetVoidResult();

        var cut = RenderNested(outer: true, inner: null);

        Assert.Equal(4, cut.FindAll("template").Count);
        Assert.Equal(2, planned.Invocations.Count);
    }
}
