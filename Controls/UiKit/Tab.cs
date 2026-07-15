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

    // Re-register on every render so Tabs re-collects in document order each pass — conditionally
    // rendered tabs (@if) drop and re-appear in their declared position (see Table.Register).
    protected override void OnParametersSet() => Tabs?.Register(this);

    internal RenderFragment LabelFor() => TitleContent ?? (b => b.AddContent(0, Title));

    // Tabs are declarative metadata only — they emit nothing themselves.
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }

    public void Dispose() => Tabs?.Unregister(this);
}
