using Microsoft.AspNetCore.Components.Rendering;

namespace Controls;

/// <summary>
/// One tab in a <see cref="Tabs"/> strip. Declared as a child of <see cref="Tabs"/>; it registers
/// itself and renders no markup of its own (the same declarative-metadata pattern as the Table's
/// <see cref="Column{TItem}"/>).
/// </summary>
public class Tab : ComponentBase, IDisposable
{
    [CascadingParameter] public Tabs? Tabs { get; set; }

    /// <summary>Identity of this tab — the value <see cref="Tabs.ActiveKey"/> binds to.</summary>
    [Parameter, EditorRequired] public string Key { get; set; } = default!;

    /// <summary>The tab's label text.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Optional label template rendered instead of <see cref="Title"/>.</summary>
    [Parameter] public RenderFragment? TitleContent { get; set; }

    /// <summary>Optional count rendered as a bordered chip before the label (the Clark Connect
    /// "12 Overdue" pattern). Null (default) renders no chip.</summary>
    [Parameter] public int? Count { get; set; }

    /// <summary>When true the tab cannot be activated.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Optional pane content shown below the strip while this tab is active. When every
    /// tab omits it, the Tabs render as a bare filter strip (the consumer owns what changes).</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // The Tabs component focuses the rendered button during keyboard navigation.
    internal ElementReference ButtonRef;

    bool _initialized;
    string? _lastKey;
    string? _lastTitle;
    int? _lastCount;
    bool _lastDisabled;
    bool _lastHasTitleContent;
    bool _lastHasChildContent;

    // Re-register on every render so Tabs re-collects in document order each pass — conditionally
    // rendered tabs (@if) drop and re-appear in their declared position (see Table.Register). This
    // runs on every parent render regardless of whether these parameters actually changed (a
    // RenderFragment parameter is a new delegate each pass, so Blazor can't skip the call) — the
    // snapshot comparison below is what tells a real change from a re-run, and it's what guards
    // NotifyTabChanged against looping: Tabs renders its strip from these fields BEFORE this method
    // runs, so a real change on an already-registered tab needs one corrective re-render, but an
    // unguarded notification on every pass would recurse forever.
    protected override void OnParametersSet()
    {
        var displayChanged = _initialized &&
            (_lastKey != Key || _lastTitle != Title || _lastCount != Count || _lastDisabled != Disabled ||
             _lastHasTitleContent != (TitleContent is not null) || _lastHasChildContent != (ChildContent is not null));

        _lastKey = Key;
        _lastTitle = Title;
        _lastCount = Count;
        _lastDisabled = Disabled;
        _lastHasTitleContent = TitleContent is not null;
        _lastHasChildContent = ChildContent is not null;
        _initialized = true;

        Tabs?.Register(this);
        if (displayChanged) Tabs?.NotifyTabChanged();
    }

    internal RenderFragment LabelFor() => TitleContent ?? (b => b.AddContent(0, Title));

    // Tabs are declarative metadata only — they emit nothing themselves.
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    public void Dispose() => Tabs?.Unregister(this);
}
