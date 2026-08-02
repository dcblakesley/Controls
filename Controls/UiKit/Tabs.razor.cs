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
    /// declarations are fine — it is only non-tab content that has nowhere legal to go. A stray
    /// component declared here is also the one thing the re-collection below can cost you: it is
    /// re-created, and loses its own state, on a pass that inserts a tab whose place the pass did
    /// not report.
    /// </para>
    /// <para>
    /// <b>How the declared order is kept exact.</b> Two behaviors read a tab <i>list</i> this
    /// component maintains rather than the rendered strip: which tab the arrow keys move to, and
    /// which tab a null <see cref="ActiveKey"/> falls back to. Blazor skips
    /// <c>SetParametersAsync</c> for a child whose own parameters are all unchanged immutable values,
    /// so a pass in which a tab appears among siblings that all skipped reports no position for it —
    /// the bare filter strip, where every parameter is a constant string, is exactly that shape.
    /// When a pass leaves a newcomer's place genuinely unknown, this fragment is re-created once
    /// (a generation key on the cascade it renders under), which makes every <see cref="Tab"/>
    /// construct and register in document order and the list exact again, within the same render
    /// batch. Ordinary re-renders, removals, label/count changes and insertions the pass could place
    /// from what it saw re-create nothing.
    /// </para>
    /// <para>
    /// <b>Limitation — a pure reorder among skipped siblings.</b> A <c>@key</c>ed loop whose items
    /// merely change places changes no tab's parameters at all, so nothing registers and there is no
    /// signal that anything moved — not even the newcomer-registers-late one the re-collection hangs
    /// off. The buttons do move (<c>@key</c> moves the component instances, and each carries its own
    /// button), but the arrow-key order and the null-<see cref="ActiveKey"/> fallback keep the
    /// previous order until some later pass makes a tab re-register.
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

    // The @key the ChildContent cascade carries. Bumping it makes the diff drop that subtree and
    // build it again, which is the only way to get every Tab -- including the ones Blazor would
    // otherwise skip -- to register in document order. See ResolveOrder and BeginPass.
    int _generation;
    bool _guessedPlacement;  // the last collection had to guess a newcomer's place
    bool _rebuilding;        // this pass IS the re-collection (its own registrations must not re-arm it)

    // The last selection made through this component (uncontrolled fallback while the consumer
    // doesn't bind ActiveKey).
    string? _selectedKey;

    // The fallback already reported through ActiveKeyChanged: the requested key that no longer names a
    // usable tab, and the key resolution fell back to. See SyncFallbackKey.
    string? _reportedFallbackFrom;
    string? _reportedFallbackTo;

    string? _generatedId;
    internal string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-tabs-{Guid.NewGuid():N}");

    // Arrow-key navigation moves DOM focus onto a nav button, and the consumer's ActiveKeyChanged
    // handler is free to insert a tab in response -- which is the one situation where a re-collection
    // destroys the very button the strip just focused. True from the keypress until the render cycle
    // it starts has completed, so a re-collection anywhere in that cycle is recognized; false at
    // every other moment, so one driven by consumer state the user never touched (a poll, a timer, a
    // sibling component) can never pull focus out of wherever the user actually is.
    bool _keyboardNav;

    // How many arrow-key operations are still running. The latch cannot just be cleared by the FIRST
    // OnAfterRender after the keypress: an ASYNC ActiveKeyChanged handler is still awaiting at that
    // point -- dispatching an EventCallback re-renders the consumer around the await, so the pending
    // render is flushed while the handler is suspended -- and the insertion that triggers the
    // re-collection happens only when the handler RESUMES, one render later. Clearing on that first
    // render disarmed the latch before the very re-collection it exists for, the rebuild ran with
    // _restoreFocus false, and focus fell to <body>. Nor can OnKeyDownAsync clear the latch in its own
    // finally: in the synchronous case no render has been PROCESSED by then (a render requested from
    // an event handler runs after the handler returns), so BeginPass would never see it. Counting the
    // operations keeps both -- the latch survives every render taken while one is in flight, and the
    // finally's StateHasChanged guarantees a clearing cycle exists once the last one ends.
    int _keyboardNavInFlight;

    bool _restoreFocus;  // a re-collection tore down the button _keyboardNav had focused

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
        // Repair a pass that had to guess a newcomer's place, once. A fresh generation key makes the
        // diff drop the ChildContent subtree and build it again, so every Tab is constructed and
        // registers in document order and the list below is exact -- still inside this render batch,
        // so the guessed order is never painted. _rebuilding covers this pass' own registrations:
        // every tab in it is a newcomer and the outgoing instances are stragglers until the renderer
        // disposes them, which is the guess shape all over again and would otherwise re-arm forever.
        _rebuilding = _guessedPlacement;
        if (_guessedPlacement)
        {
            _generation++;
            _guessedPlacement = false;
            _restoreFocus = _keyboardNav;
        }

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
    /// Rebuilds <c>_tabs</c> from the tabs that registered this pass plus the ones that did not, and
    /// records whether any newcomer had to be <i>guessed</i> into place.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Blazor skips <c>SetParametersAsync</c> entirely for a child whose own parameters are all
    /// unchanged immutable values, so a pass sees only a <i>subsequence</i> of the declared order:
    /// content-less tabs (the bare filter strip) never re-register unless their own text changes.
    /// The tabs that did register give the exact relative order of everything in that subsequence;
    /// the ones that did not — the stragglers — keep their previous relative order and are slotted
    /// back around it. A brand-new tab is the one thing neither list places: it is held until the
    /// next tab that did re-register pins it, and it lands exactly as long as no straggler had to be
    /// placed in the span it lands in. When one did, which side of that straggler the newcomer
    /// belongs on is simply not in the pass — that, and nothing else, is a guess.
    /// </para>
    /// <para>
    /// Guesses are recorded rather than lived with: <see cref="BeginPass"/> re-creates the whole
    /// <see cref="ChildContent"/> subtree once under a fresh generation key, which makes every
    /// <see cref="Tab"/> construct and register in document order, so the guessed list is replaced by
    /// the real one inside the same render batch. Everything that places from data alone re-creates
    /// nothing: the first collection, a removal, a label/count/disabled change, and an insertion
    /// whose span held no straggler (a strip of tabs that all carry pane content re-registers in
    /// full every pass, because a <c>RenderFragment</c> is a fresh delegate each time).
    /// </para>
    /// <para>
    /// The re-creation is the only exact mechanism available. Dropping <c>IsFixed</c> from the
    /// cascade does make every live tab report on every pass, but in cascade-subscription order
    /// (construction order, newcomers last), which is a wrong answer for the price of an extra render
    /// per tab per pass — pinned by
    /// <c>Blazor_offers_no_document_ordered_re_registration_of_parameter_skipped_children</c>. What
    /// nothing here can see is a pure reorder among skipped siblings: no tab's parameters change, so
    /// nothing registers, and there is no newcomer to notice either.
    /// </para>
    /// </remarks>
    void ResolveOrder()
    {
        var order = new List<Tab>(_liveTabs.Count);
        List<Tab>? pending = null;  // newcomers waiting for the anchor that pins them
        var next = 0;               // read position in the previous order
        var guessed = false;        // a newcomer had to be placed on one side of a straggler, unseen

        foreach (var registered in _passOrder)
        {
            var was = _orderBeforePass.IndexOf(registered);
            if (was < 0)
            {
                (pending ??= new List<Tab>()).Add(registered);
                continue;
            }
            var beforeStragglers = order.Count;
            while (next < was) TakeStraggler(_orderBeforePass[next++]);
            guessed |= pending is not null && order.Count > beforeStragglers;
            TakePending();
            if (!order.Contains(registered)) order.Add(registered);
            next = Math.Max(next, was + 1);
        }

        var beforeTail = order.Count;
        while (next < _orderBeforePass.Count) TakeStraggler(_orderBeforePass[next++]);
        guessed |= pending is not null && order.Count > beforeTail;
        TakePending();

        // Assigned, never accumulated: this runs again after every registration in the pass, over the
        // whole of _passOrder, so the last call is the pass' verdict -- a newcomer that looks
        // unplaceable when it registers is placed exactly once the anchor after it reports.
        if (!_rebuilding) _guessedPlacement = guessed;

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
    protected override void OnAfterRender(bool firstRender)
    {
        SyncFallbackKey();

        // A re-collection destroyed and re-created every nav button, and a browser drops focus on
        // <body> when the focused element is removed. That leaves the roving tabindex pointing at a
        // button the user is no longer on, so the next arrow key fires from nowhere and Tab leaves
        // for the top of the page. Only ever restored for a re-collection that happened during
        // arrow-key navigation, i.e. when the strip is the thing that put focus on a button in the
        // first place. Fire-and-forget for the same reason as the notification above: OnAfterRender
        // is synchronous, and TryFocusAsync cannot throw.
        if (_restoreFocus)
        {
            _restoreFocus = false;
            if (ActiveTab is { } active) _ = TryFocusAsync(active);
        }
        // Only once every arrow-key operation has finished -- see _keyboardNavInFlight. A render taken
        // mid-operation (the one an awaited ActiveKeyChanged handler forces) must leave the latch armed.
        if (_keyboardNavInFlight == 0) _keyboardNav = false;
    }

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

        // Raised for the whole operation, not just around the focus call: SelectAsync dispatches
        // ActiveKeyChanged, the consumer's handler may reveal a tab, and the re-collection that
        // repairs the order then destroys the very button focused below. None of that runs inside
        // this method -- a render requested from an event handler is processed after the handler
        // returns -- so the flag cannot be cleared here; OnAfterRender clears it once no operation is
        // in flight. StateHasChanged guarantees there is such a cycle even in the corner where
        // SelectAsync finds nothing to do.
        _keyboardNav = true;
        _keyboardNavInFlight++;
        StateHasChanged();

        try
        {
            await SelectAsync(target);
            await TryFocusAsync(target);
        }
        finally
        {
            // The count has to come down even if the consumer's handler throws, or the strip would
            // pull focus back into itself on every later re-collection. The paired StateHasChanged
            // guarantees a render cycle after the last operation ends: an async handler's renders were
            // all taken while the count was non-zero, so without this there may be no further cycle to
            // clear the latch in.
            _keyboardNavInFlight--;
            StateHasChanged();
        }
    }

    static async Task TryFocusAsync(Tab tab)
    {
        try
        {
            await tab.ButtonRef.FocusAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JSException or JSDisconnectedException)
        {
            // Exactly three tolerated failures, none of which the strip can do anything about:
            //   InvalidOperationException  - no JS runtime at all (static SSR / prerender), or the
            //                                ElementReference was never captured. The capture lives
            //                                in the same render tree as the button it captures (see
            //                                Tab.razor), so the second case is limited to a tab whose
            //                                button has not rendered yet.
            //   JSException                - the browser rejected the focus call (element detached --
            //                                e.g. a re-collection replaced this tab mid-operation, in
            //                                which case OnAfterRender focuses the replacement).
            //   JSDisconnectedException    - the Blazor Server circuit went away mid-call.
            // The selection has already moved either way; only DOM focus is lost. Anything else is a
            // real defect and must not be swallowed.
        }
    }
}
