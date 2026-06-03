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
        if (ChildContent != null)
        {
            builder.AddContent(2, ChildContent(item));
        }
        builder.CloseElement();
    };
}
