namespace Controls;

/// <summary>Severity of an <see cref="Alert"/>.</summary>
public enum AlertType
{
    Success,
    Info,
    Warning,
    Error
}

/// <summary>Placement of a <see cref="Popover"/> / <see cref="Popconfirm"/> relative to its target.</summary>
public enum PopupPlacement
{
    Top,
    Bottom,
    Left,
    Right
}

/// <summary>Edge a <see cref="Drawer"/> slides in from.</summary>
public enum DrawerPlacement
{
    Left,
    Right,
    Top,
    Bottom
}

/// <summary>Row density of a <see cref="Table{TItem}"/>.</summary>
public enum TableSize
{
    Default,
    Middle,
    Small
}

/// <summary>Horizontal alignment of a paged <see cref="Table{TItem}"/>'s pager.</summary>
public enum PagerAlign
{
    Left,
    Center,
    Right
}

/// <summary>Where a paged <see cref="Table{TItem}"/> renders its pager(s).</summary>
public enum PagerPosition
{
    Bottom,
    Top,
    Both
}

/// <summary>Sort state of a <see cref="Table{TItem}"/> column.</summary>
public enum SortDirection
{
    /// <summary>Unsorted — rows keep their original <c>DataSource</c> order.</summary>
    None,
    Ascending,
    Descending
}

/// <summary>What a <see cref="DatePicker"/> selects: a day, a month, a date+time, a time, a year,
/// or a quarter.</summary>
public enum DatePickerMode
{
    Date,
    Month,
    DateTime,
    Time,
    Year,
    Quarter
}
