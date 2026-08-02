using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// An underline tab strip (the Clark Connect / AntD "line" type), with an optional bordered count
/// chip per tab. Declare <see cref="Tab"/> children; bind the selection with
/// <c>@bind-ActiveKey</c>. Tabs with <see cref="Tab.ChildContent"/> show the active pane below the
/// strip; content-less tabs render as a bare filter strip (the consumer owns what changes).
/// </summary>
/// <remarks>
/// Keyboard follows the ARIA tabs pattern with automatic activation: Arrow keys move to (and
/// select) the previous/next enabled tab, and the roving tabindex keeps one Tab stop for the whole
/// strip. Home/End are deliberately not handled — Blazor has no per-key <c>preventDefault</c>, and
/// suppressing the resulting page-scroll would require JS interop, which this no-JS-interop control
/// forgoes.
/// </remarks>
public partial class Tabs
{
    /// <summary>The <see cref="Tab"/> children. Each one renders its own button into the tab
    /// strip, so the <i>rendered</i> strip is always in declaration order — including a tab that is
    /// conditionally rendered (<c>@if</c>), produced by a loop, or moved by <c>@key</c>. The
    /// render-tree diff places those buttons and nothing this component tracks can move one.</summary>
    /// <remarks>
    /// <para>
    /// <b>Declare only <see cref="Tab"/> here.</b> This fragment renders inside the strip's
    /// <c>role="tablist"</c> element, so anything else declared directly inside
    /// <c>&lt;Tabs&gt;</c> — a stray <c>&lt;div&gt;</c>, a component of your own — becomes a child
    /// of the tablist, which is an ARIA <c>aria-required-children</c> violation and will be reported
    /// by an accessibility audit. Put it in a tab's pane content (<see cref="Tab.ChildContent"/>),
    /// in <see cref="TabBarExtraContent"/> for the right-aligned slot beside the strip, or beside
    /// the <c>&lt;Tabs&gt;</c> element. Conditional (<c>@if</c>) and looped <see cref="Tab"/>
    /// declarations are fine — it is only non-tab content that has nowhere legal to go.
    /// </para>
    /// <para>
    /// <b>Limitation — keyboard order after an unanchored change.</b> Two behaviors read a tab
    /// <i>list</i> this component maintains rather than the rendered strip: which tab the arrow keys
    /// move to, and which tab a null <see cref="ActiveKey"/> falls back to. Blazor skips
    /// <c>SetParametersAsync</c> for a child whose own parameters are all unchanged immutable values,
    /// so on a pass where every sibling was skipped — the bare filter strip, where every parameter
    /// is a constant string — nothing reports a position: a newly revealed tab cannot be placed and
    /// is appended, and a pure reorder (a <c>@key</c>ed loop whose items change places) is not seen
    /// at all.
    /// </para>
    /// <para>
    /// The strip renders correctly regardless. What can differ until some later pass makes a
    /// neighbour re-register is that the arrows may reach a tab out of its rendered position, and an
    /// unbound <see cref="ActiveKey"/> may highlight the tab that was first <i>before</i> the change.
    /// It does not arise when any sibling re-registers on the same pass — a tab carrying pane content
    /// re-registers on every pass, because a <c>RenderFragment</c> is a fresh delegate each time —
    /// nor on a removal, nor on the first render.
    /// </para>
    /// </remarks>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The active tab's <see cref="Tab.Key"/>. Null (default) activates the first
    /// enabled tab. Supports <c>@bind-ActiveKey</c> (bind a <c>string?</c> field).</summary>
    [Parameter] public string? ActiveKey { get; set; }

    /// <summary>Raised with the new key when the selection changes (supports <c>@bind-ActiveKey</c>).
    /// Also raised when the tab a non-null <see cref="ActiveKey"/> names is removed or disabled and the
    /// strip therefore falls back to the first enabled tab, so a bound key can never disagree with the
    /// tab that is actually highlighted.</summary>
    [Parameter] public EventCallback<string?> ActiveKeyChanged { get; set; }

    /// <summary>Accessible name of the tab strip. Override to localize.</summary>
    [Parameter] public string TablistLabel { get; set; } = "Tabs";

    /// <summary>HTML id root for the ARIA tab/panel wiring. A stable generated id is used when omitted.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Right-aligned slot in the tab strip (AntD's <c>tabBarExtraContent</c>) — grouped with
    /// the strip in a <c>wss-tabs-nav-wrapper</c> only when set (unset markup is unchanged).</summary>
    [Parameter] public RenderFragment? TabBarExtraContent { get; set; }

    /// <summary>Centers the tab buttons within the strip instead of the default left alignment.</summary>
    [Parameter] public bool Centered { get; set; }

    /// <summary>Visual style of the strip. Defaults to <see cref="TabsType.Line"/> (the existing
    /// underline look); <see cref="TabsType.Card"/> is AntD's boxed tabs (CSS-only — keyboard/ARIA
    /// are identical either way).</summary>
    [Parameter] public TabsType Type { get; set; } = TabsType.Line;

    /// <summary>
    /// Unmatched attributes (e.g. a consumer's <c>class</c>, <c>style</c>, or <c>data-*</c>),
    /// applied to the root <c>div.wss-tabs</c>. <c>class</c> and <c>style</c> merge with the
    /// component's own; the rest are splatted verbatim.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    readonly List<Tab> _liveTabs = new();  // registered and not yet disposed, in construction order
    List<Tab> _tabs = new();               // the live set in declaration order (see ResolveOrder)
    List<Tab> _orderBeforePass = new();    // _tabs as it stood when the current render pass began
    List<Tab> _passOrder = new();          // tabs that (re-)registered during this pass, in order

    // The last selection made through this component (uncontrolled fallback while the consumer
    // doesn't bind ActiveKey).
    string? _selectedKey;

    // The fallback already reported through ActiveKeyChanged: the requested key that no longer names a
    // usable tab, and the key resolution fell back to. See SyncFallbackKey.
    string? _reportedFallbackFrom;
    string? _reportedFallbackTo;

    string? _generatedId;
    internal string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-tabs-{Guid.NewGuid():N}");

    // Resolution: the bound ActiveKey wins, then the last local selection, then the first enabled tab.
    internal Tab? ActiveTab =>
        _tabs.FirstOrDefault(t => t.Key == (ActiveKey ?? _selectedKey) && !t.Disabled)
        ?? _tabs.FirstOrDefault(t => !t.Disabled);

    internal bool IsActive(Tab tab) => ReferenceEquals(tab, ActiveTab);

    internal bool HasPanel => ActiveTab?.ChildContent is not null;

    // ----- Render pass bookkeeping -------------------------------------------
    //
    // Nothing here places a button: each Tab renders its own, so the rendered strip is whatever the
    // render-tree diff makes of the ChildContent. What the strip still needs is the tab SET -- to
    // render the active tab's pane below the nav -- and its declaration ORDER, which exactly two
    // behaviors read: the "first enabled tab" fallback when no key resolves, and the arrow-key
    // neighbor order.

    // Strip-level state the tabs' own buttons are built from, as last pushed to them. See BeginPass.
    Tab? _pushedActive;
    bool _pushedHasPanel;
    string? _pushedBaseId;

    // Called from the top of the markup, i.e. once per render of this component.
    void BeginPass()
    {
        _orderBeforePass = _tabs;
        _passOrder = new List<Tab>();

        // Everything a tab's button shows that is NOT one of its own parameters: which tab is
        // active (underline, aria-selected, the roving tabindex), whether there is a panel to point
        // aria-controls at, and the id root. None of that is visible to Blazor's parameter diff, so
        // a tab whose own parameters are unchanged is skipped and would keep rendering the previous
        // selection. Mark every live tab dirty when it changes; they render later in this same batch
        // and read the settled state, so this costs no extra render pass and no extra paint.
        // (Queueing a render for a tab this pass removes is a documented no-op in the renderer.)
        var active = ActiveTab;
        var hasPanel = active?.ChildContent is not null;
        if (ReferenceEquals(active, _pushedActive) && hasPanel == _pushedHasPanel && BaseId == _pushedBaseId) return;

        _pushedActive = active;
        _pushedHasPanel = hasPanel;
        _pushedBaseId = BaseId;
        foreach (var tab in _liveTabs) tab.Refresh();
    }

    internal void Register(Tab tab)
    {
        var isNew = !_liveTabs.Contains(tab);
        if (isNew) _liveTabs.Add(tab);
        if (_passOrder.Contains(tab)) return;

        _passOrder.Add(tab);
        ResolveOrder();

        // The strip's own markup (the pane below the nav) was built before this tab registered, so a
        // newcomer needs one corrective render. Re-registrations must not request one -- they happen
        // on every pass, and an unguarded request would never settle.
        if (isNew) StateHasChanged();
    }

    internal void Unregister(Tab tab)
    {
        if (!_liveTabs.Remove(tab)) return;
        _passOrder.Remove(tab);
        ResolveOrder();
        StateHasChanged();
    }

    /// <summary>
    /// Rebuilds <c>_tabs</c> from the tabs that registered this pass plus the ones that did not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blazor skips <c>SetParametersAsync</c> entirely for a child whose own parameters are all
    /// unchanged immutable values, so a pass sees only a <i>subsequence</i> of the declared order:
    /// content-less tabs (the bare filter strip) never re-register unless their own text changes.
    /// The tabs that did register give the exact relative order of everything in that subsequence;
    /// the ones that did not keep their previous relative order and are slotted back around it.
    /// A brand-new tab is the one thing neither list places, so it is held until the next tab that
    /// did re-register pins it — "as late as its neighbours allow". With no such neighbour at all
    /// (every sibling skipped, the classic conditional leading/trailing tab) the position is simply
    /// not in the data, and it is appended.
    /// </para>
    /// <para>
    /// That append is a <b>guess, and it can be wrong in a way the user can observe</b> — see the
    /// limitation on <see cref="ChildContent"/>. It is not "at worst a rotation": no placement rule
    /// can promise that, because for a middle insertion neither appending nor prepending produces
    /// one (declared <c>[a, b, mid, c]</c> has rotations <c>[a,b,mid,c] [b,mid,c,a] [mid,c,a,b]
    /// [c,a,b,mid]</c>, and the two candidates are <c>[a,b,c,mid]</c> and <c>[mid,a,b,c]</c>).
    /// Arrow navigation is cyclic, so a rotation would indeed be unobservable there, and a leading
    /// or trailing insertion does happen to produce one; a middle insertion does not, and the arrows
    /// then reach the newcomer out of its rendered position. The "first enabled tab" fallback for an
    /// unbound <see cref="ActiveKey"/> can differ in every one of these shapes. Both self-correct on
    /// the first later pass in which a neighbour re-registers, which may never come.
    /// </para>
    /// <para>
    /// Removing the guess needs an exact re-collection in declaration order, which no bookkeeping
    /// here can produce: the information is not in the pass. Dropping <c>IsFixed</c> from the
    /// cascade does make every live tab report on every pass, but in cascade-subscription order
    /// (construction order, newcomers last), which is the same wrong answer for the price of an
    /// extra render per tab per pass — pinned by
    /// <c>Blazor_offers_no_document_ordered_re_registration_of_parameter_skipped_children</c>. The
    /// mechanisms that <i>are</i> exact re-create the children (a generation <c>@key</c> rebuild,
    /// which tears down everything declared inside <c>&lt;Tabs&gt;</c>) or read the rendered DOM.
    /// </para>
    /// </remarks>
    void ResolveOrder()
    {
        var order = new List<Tab>(_liveTabs.Count);
        List<Tab>? pending = null;  // newcomers waiting for the anchor that pins them
        var next = 0;               // read position in the previous order

        foreach (var registered in _passOrder)
        {
            var was = _orderBeforePass.IndexOf(registered);
            if (was < 0)
            {
                (pending ??= new List<Tab>()).Add(registered);
                continue;
            }
            while (next < was) TakeStraggler(_orderBeforePass[next++]);
            TakePending();
            if (!order.Contains(registered)) order.Add(registered);
            next = Math.Max(next, was + 1);
        }

        while (next < _orderBeforePass.Count) TakeStraggler(_orderBeforePass[next++]);
        TakePending();
        // Defensive: every live tab is in one of the two lists, but a tab that somehow reached
        // neither still belongs in the set rather than vanishing from keyboard navigation.
        foreach (var tab in _liveTabs)
        {
            if (!order.Contains(tab)) order.Add(tab);
        }

        if (!_tabs.SequenceEqual(order)) _tabs = order;

        // A tab from the previous order that did not re-register this pass keeps its place; one that
        // did is placed by the loop above instead, and one that was disposed is dropped.
        void TakeStraggler(Tab tab)
        {
            if (_liveTabs.Contains(tab) && !_passOrder.Contains(tab) && !order.Contains(tab)) order.Add(tab);
        }

        void TakePending()
        {
            if (pending is null) return;
            foreach (var tab in pending)
            {
                if (!order.Contains(tab)) order.Add(tab);
            }
            pending = null;
        }
    }

    /// <summary>
    /// Requests a follow-up render of the strip after an already-registered <see cref="Tab"/>'s
    /// parameters changed. The strip's own markup — active-tab resolution and the panel, which
    /// embeds <c>ActiveTab.ChildContent</c> — is built from the <see cref="Tab"/> instances in
    /// <c>_tabs</c> before the changed tab's <c>OnParametersSet</c> runs, so it renders the previous
    /// pass' values and would only self-correct on some later, unrelated render.
    /// </summary>
    /// <remarks>
    /// The label and count are rendered by the tab itself and need no help from here, but they are
    /// still part of the signal: a change to any of them means the consumer's fragment produced new
    /// values this pass, which is the strip's cue that the pane delegate it embedded is one pass
    /// stale. See <see cref="Tab"/>'s snapshot for why that is the available signal.
    /// </remarks>
    internal void NotifyTabChanged() => StateHasChanged();

    /// <inheritdoc/>
    protected override void OnAfterRender(bool firstRender) => SyncFallbackKey();

    // ActiveTab silently falls back to the first enabled tab when the requested key names a tab that
    // was removed or disabled, so the strip renders one tab active while a bound ActiveKey still holds
    // the old, now-unusable key -- and only SelectAsync ever raised ActiveKeyChanged, so nothing told
    // the consumer. Their own pane/filter state then disagreed with the highlighted tab until the next
    // click. Notified from OnAfterRender rather than from Register: that runs mid-render, BEFORE the
    // remaining Tabs' own OnParametersSet, so a Key/Disabled change on an already-registered tab is
    // still one pass stale there and would report a fallback that isn't real (see
    // Existing_tab_Key_change_renders_on_the_same_pass_instead_of_one_behind). By OnAfterRender every
    // Tab in the batch has its current parameters.
    void SyncFallbackKey()
    {
        var active = ActiveTab;
        // No usable tab at all (none registered yet, or every one disabled): there is nothing to fall
        // back TO, and reporting null here would clobber a bound key on the very first render, before
        // the children have registered.
        if (active is null) return;

        var requested = ActiveKey ?? _selectedKey;
        // A null request is not a desync: null is the documented "activate the first enabled tab",
        // which is exactly what's rendered.
        if (requested is null || requested == active.Key)
        {
            _reportedFallbackFrom = null;
            _reportedFallbackTo = null;
            return;
        }
        // Report each distinct fallback once. A consumer that honors it re-renders with the new key and
        // the branch above clears this; one that ignores it (or keeps re-passing the stale key) must
        // not be told again -- EventCallback.InvokeAsync re-renders the parent, so an unguarded
        // notification here would loop forever.
        if (requested == _reportedFallbackFrom && active.Key == _reportedFallbackTo) return;
        _reportedFallbackFrom = requested;
        _reportedFallbackTo = active.Key;
        // Keep the uncontrolled fallback in step too, so an unbound strip doesn't re-report the same
        // fallback from a _selectedKey that no longer resolves.
        _selectedKey = active.Key;
        // Fire-and-forget, mirroring Table's own selection-clamp notification: OnAfterRender is
        // synchronous (Blazor's lifecycle contract), and EventCallback.InvokeAsync is safe to not await.
        _ = ActiveKeyChanged.InvokeAsync(active.Key);
    }

    // ----- Interaction -------------------------------------------------------

    internal async Task SelectAsync(Tab tab)
    {
        if (tab.Disabled || IsActive(tab)) return;
        _selectedKey = tab.Key;
        // The click was handled by the tab, so Blazor re-renders the TAB, not the strip. Every other
        // button's active state and the pane below the nav are the strip's business: without this an
        // unbound strip (no ActiveKeyChanged handler to re-render the parent) would move nothing but
        // the clicked button.
        StateHasChanged();
        await ActiveKeyChanged.InvokeAsync(tab.Key);
    }

    // ARIA tabs pattern, automatic activation: arrows select the neighboring enabled tab and move
    // focus onto it (the roving tabindex above keeps the strip a single Tab stop).
    internal async Task OnKeyDownAsync(KeyboardEventArgs e, Tab from)
    {
        var enabled = _tabs.Where(t => !t.Disabled).ToList();
        if (enabled.Count == 0) return;
        var idx = enabled.IndexOf(from);
        if (idx < 0) return;

        Tab? target = e.Key switch
        {
            "ArrowRight" => enabled[(idx + 1) % enabled.Count],
            "ArrowLeft" => enabled[(idx - 1 + enabled.Count) % enabled.Count],
            _ => null,
        };
        if (target is null || ReferenceEquals(target, from)) return;

        await SelectAsync(target);
        try
        {
            await target.ButtonRef.FocusAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException)
        {
            // Exactly three tolerated failures, none of which the strip can do anything about:
            //   InvalidOperationException  - no JS runtime at all (static SSR / prerender), or the
            //                                ElementReference was never captured. The capture now
            //                                lives in the same render tree as the button it captures
            //                                (see Tab.razor), so the second case is limited to a tab
            //                                whose button has not rendered yet.
            //   JSException                - the browser rejected the focus call (element detached).
            //   JSDisconnectedException    - the Blazor Server circuit went away mid-call.
            // The selection has already moved either way; only DOM focus is lost. Anything else is a
            // real defect and must not be swallowed.
        }
    }
}
