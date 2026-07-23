using Microsoft.AspNetCore.Components.Web;

namespace Controls;

/// <summary>
/// A table column for row actions (edit, delete, ...). Same as <see cref="Column{TItem}"/>
/// but wraps the cell content in a flex container so action buttons space evenly.
/// Access the row via the cell template's <c>@context</c>.
/// </summary>
public class ActionColumn<TItem> : Column<TItem>
{
    public override RenderFragment CellFor(TItem item) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "wss-table-actions");
        // Table.OnRowClick / ExpandRowByClick must not fire from an action button click -- stopping
        // propagation here (rather than requiring every consumer button to do it) covers the whole
        // cell, matching the same guard already on the selection/expand cells.
        builder.AddEventStopPropagationAttribute(2, "onclick", true);
        if (ChildContent != null)
        {
            builder.AddContent(3, ChildContent(item));
        }
        builder.CloseElement();
    };
}
