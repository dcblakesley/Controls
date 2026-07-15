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
/// select) the previous/next enabled tab, Home/End jump to the ends, and the roving tabindex keeps
/// one Tab stop for the whole strip. No JS interop.
/// </remarks>
public partial class Tabs
{
    /// <summary>The <see cref="Tab"/> children (declarative metadata — they emit no markup of
    /// their own and may be conditionally rendered).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The active tab's <see cref="Tab.Key"/>. Null (default) activates the first
    /// enabled tab. Supports <c>@bind-ActiveKey</c> (bind a <c>string?</c> field).</summary>
    [Parameter] public string? ActiveKey { get; set; }

    /// <summary>Raised with the new key when the selection changes (supports <c>@bind-ActiveKey</c>).</summary>
    [Parameter] public EventCallback<string?> ActiveKeyChanged { get; set; }

    /// <summary>Accessible name of the tab strip. Override to localize.</summary>
    [Parameter] public string TablistLabel { get; set; } = "Tabs";

    /// <summary>HTML id root for the ARIA tab/panel wiring. A stable generated id is used when omitted.</summary>
    [Parameter] public string? Id { get; set; }

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
            "Home" => enabled[0],
            "End" => enabled[^1],
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
