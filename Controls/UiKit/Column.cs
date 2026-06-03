using Microsoft.AspNetCore.Components.Rendering;

namespace Controls;

/// <summary>
/// A table column with a custom cell template. Declared as a child of
/// <see cref="Table{TItem}"/>; it registers itself and renders no markup of its own.
/// </summary>
public class Column<TItem> : ComponentBase
{
    [CascadingParameter] public Table<TItem>? Table { get; set; }

    [Parameter] public string? Title { get; set; }

    /// <summary>Cell template; receives the row item as context.</summary>
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }

    protected override void OnInitialized() => Table?.Register(this);

    public virtual string? HeaderText => Title;

    public virtual RenderFragment CellFor(TItem item) =>
        ChildContent != null ? ChildContent(item) : _ => { };

    // Columns are declarative metadata only — they emit nothing themselves.
    protected override void BuildRenderTree(RenderTreeBuilder builder) { }
}
