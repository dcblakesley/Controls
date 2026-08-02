namespace Controls;

/// <summary>
/// One tab in a <see cref="Tabs"/> strip. Declared as a child of <see cref="Tabs"/>; it renders its
/// own nav button into the strip's tablist, so its position there is its declared position.
/// </summary>
public partial class Tab : IDisposable
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
    /// <remarks>Rendered by <see cref="Tabs"/> into the shared panel below the strip (never here),
    /// so it renders exactly once, and only while this tab is the active one.</remarks>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    // The strip focuses the rendered button during keyboard navigation. The capture lives in this
    // component's own render tree, alongside the element it captures: Blazor re-runs an element
    // reference capture only for an element it CREATES, and the button is created and destroyed
    // with this Tab, so the two can never come apart.
    internal ElementReference ButtonRef;

    bool _initialized;
    string? _lastKey;
    bool _lastDisabled;
    bool _lastHasChildContent;

    // Register on every render pass so the strip's tab set follows the markup. Display parameters
    // (Title/TitleContent/Count) are deliberately NOT snapshotted here: they only feed this
    // component's own button, and a parameter change already re-renders it. Key, Disabled and
    // "has pane content" are different -- they feed the STRIP's own markup (active-tab resolution
    // and the panel), which is built before this method runs, so a change to one of them needs the
    // corrective re-render below. The snapshot comparison is what tells a real change from a
    // re-run and is what keeps that notification from looping: OnParametersSet runs on every parent
    // render regardless of whether anything changed (a RenderFragment parameter is a new delegate
    // each pass, so Blazor cannot skip the call).
    protected override void OnParametersSet()
    {
        var stripInputChanged = _initialized &&
            (_lastKey != Key || _lastDisabled != Disabled || _lastHasChildContent != (ChildContent is not null));

        _lastKey = Key;
        _lastDisabled = Disabled;
        _lastHasChildContent = ChildContent is not null;
        _initialized = true;

        Tabs?.Register(this);
        if (stripInputChanged) Tabs?.NotifyTabChanged();
    }

    // Strip-level state (which tab is active, whether a panel exists, the id root) is not a
    // parameter of this component, so a change to it reaches this button only when the strip marks
    // it dirty -- see Tabs.BeginPass.
    internal void Refresh() => StateHasChanged();

    internal RenderFragment LabelFor() => TitleContent ?? (b => b.AddContent(0, Title));

    public void Dispose() => Tabs?.Unregister(this);
}
