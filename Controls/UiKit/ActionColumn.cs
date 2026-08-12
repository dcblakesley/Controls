namespace Controls;

/// <summary>
/// A table column for row actions (edit, delete, ...). Same as <see cref="Column{TItem}"/>
/// but wraps the cell content in a flex container so action buttons space evenly.
/// Access the row via the cell template's <c>@context</c>.
/// </summary>
public class ActionColumn<TItem> : Column<TItem>
{
    // Table.OnRowClick / ExpandRowByClick must not fire from anywhere in an action cell (rather than
    // requiring every consumer button to stop propagation itself). The guard belongs on the <td>,
    // exactly like the selection and expand cells: .wss-table-actions is inline-flex, so it only
    // covers the buttons themselves, leaving the cell's own 16px padding around them bubbling
    // straight into the row handler -- a click a hair off an action button toggled the row.
    // This flag now gates only the CLICK guard: the Table stops cell-originated keydowns on every
    // <td> unconditionally (see its row markup), so Enter on an action button can't leak into the
    // keyboard-activatable row regardless of this override.
    internal override bool StopsRowClickPropagation => true;

    public override RenderFragment CellFor(TItem item) => builder =>
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "wss-table-actions");
        if (ChildContent != null)
        {
            builder.AddContent(2, ChildContent(item));
        }
        builder.CloseElement();
    };
}
