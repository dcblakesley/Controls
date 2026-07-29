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
    /// <summary>The <see cref="Tab"/> children (declarative metadata — they emit no markup of
    /// their own and may be conditionally rendered).</summary>
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

    List<Tab> _tabs = new();          // promoted, ordered tab set the strip renders
    List<Tab>? _collecting;           // buffer the current pass collects into (promoted next pass)
    readonly List<Tab> _liveTabs = new(); // registered and not yet disposed
    // The last selection made through this component (uncontrolled fallback while the consumer
    // doesn't bind ActiveKey).
    string? _selectedKey;

    // The fallback already reported through ActiveKeyChanged: the requested key that no longer names a
    // usable tab, and the key resolution fell back to. See SyncFallbackKey.
    string? _reportedFallbackFrom;
    string? _reportedFallbackTo;

    string? _generatedId;
    string BaseId => !string.IsNullOrEmpty(Id) ? Id : (_generatedId ??= $"wss-tabs-{Guid.NewGuid():N}");

    // Resolution: the bound ActiveKey wins, then the last local selection, then the first enabled tab.
    internal Tab? ActiveTab =>
        _tabs.FirstOrDefault(t => t.Key == (ActiveKey ?? _selectedKey) && !t.Disabled)
        ?? _tabs.FirstOrDefault(t => !t.Disabled);

    bool IsActive(Tab tab) => ReferenceEquals(tab, ActiveTab);

    bool HasPanel => ActiveTab?.ChildContent is not null;

    // ----- Child registration (the Table column collect/promote pattern) -----

    void StartCollectingTabs()
    {
        if (_collecting is not null)
        {
            // Merge still-live stragglers whose parameters were all unchanged (their
            // OnParametersSet never ran this pass) back in at their previous position.
            var promoted = _collecting;
            if (promoted.Count != _liveTabs.Count)
            {
                foreach (var straggler in _liveTabs)
                {
                    if (!promoted.Contains(straggler))
                    {
                        var prevIdx = _tabs.IndexOf(straggler);
                        promoted.Insert(Math.Min(prevIdx < 0 ? promoted.Count : prevIdx, promoted.Count), straggler);
                    }
                }
            }
            if (!_tabs.SequenceEqual(promoted)) _tabs = promoted;
        }
        _collecting = new List<Tab>();
    }

    internal void Register(Tab tab)
    {
        if (!_liveTabs.Contains(tab)) _liveTabs.Add(tab);
        if (_collecting is null || _collecting.Contains(tab)) return;
        _collecting.Add(tab);
        if (!_tabs.Contains(tab)) StateHasChanged();
    }

    internal void Unregister(Tab tab)
    {
        _liveTabs.Remove(tab);
        if (_tabs.Contains(tab)) StateHasChanged();
    }

    /// <summary>
    /// Requests a follow-up render of the strip after an already-registered <see cref="Tab"/>'s
    /// display-relevant parameters changed. The strip's markup is built from the <see cref="Tab"/>
    /// instances in <c>_tabs</c> before that <see cref="Tab"/>'s own <c>OnParametersSet</c> runs, so
    /// a parameter change (Count, Title, Disabled, ...) on an existing tab would otherwise render
    /// stale for this pass and only self-correct on some later, unrelated render.
    /// </summary>
    internal void NotifyTabChanged() => StateHasChanged();

    /// <inheritdoc/>
    protected override void OnAfterRender(bool firstRender) => SyncFallbackKey();

    // ActiveTab silently falls back to the first enabled tab when the requested key names a tab that
    // was removed or disabled, so the strip renders one tab active while a bound ActiveKey still holds
    // the old, now-unusable key -- and only SelectAsync ever raised ActiveKeyChanged, so nothing told
    // the consumer. Their own pane/filter state then disagreed with the highlighted tab until the next
    // click. Notified from OnAfterRender rather than from the promotion in StartCollectingTabs: that
    // runs mid-render, BEFORE the child Tabs' own OnParametersSet, so a Key/Disabled change on an
    // already-registered tab is still one pass stale there and would report a fallback that isn't real
    // (see Existing_tab_Key_change_renders_on_the_same_pass_instead_of_one_behind). By OnAfterRender
    // every Tab in the batch has its current parameters.
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

    async Task SelectAsync(Tab tab)
    {
        if (tab.Disabled || IsActive(tab)) return;
        _selectedKey = tab.Key;
        await ActiveKeyChanged.InvokeAsync(tab.Key);
    }

    // ARIA tabs pattern, automatic activation: arrows select the neighboring enabled tab and move
    // focus onto it (the roving tabindex above keeps the strip a single Tab stop).
    async Task OnKeyDownAsync(KeyboardEventArgs e, Tab from)
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
        catch
        {
            // No JS runtime (prerender, tests) — the selection still moved; only focus is lost.
        }
    }
}
