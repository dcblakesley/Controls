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

/// <summary>Row-selection behavior of a <see cref="Table{TItem}"/>.</summary>
public enum SelectionMode
{
    /// <summary>Checkbox column: 0-or-more rows selected (the existing default).</summary>
    Multiple,
    /// <summary>Radio-semantics column: at most one row selected.</summary>
    Single
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
/// a quarter, or a week.</summary>
public enum DatePickerMode
{
    Date,
    Month,
    DateTime,
    Time,
    Year,
    Quarter,
    Week
}

/// <summary>Shape of a <see cref="Skeleton"/>'s <see cref="Skeleton.Avatar"/> placeholder.</summary>
public enum SkeletonAvatarShape
{
    Circle,
    Square
}

/// <summary>Which standalone shape a <see cref="SkeletonElement"/> renders.</summary>
public enum SkeletonElementKind
{
    Button,
    Input
}

/// <summary>Visual style of a <see cref="Tabs"/> strip.</summary>
public enum TabsType
{
    /// <summary>Underline tab strip (the existing default look).</summary>
    Line,
    /// <summary>AntD's boxed "card" tabs — CSS-only, keyboard/ARIA unchanged.</summary>
    Card
}

/// <summary>Corner of the viewport a <see cref="NotificationListView"/> (and its two container
/// hosts) anchors to.</summary>
public enum NotificationPlacement
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft
}
