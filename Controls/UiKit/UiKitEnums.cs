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

/// <summary>
/// Which kind of filter a <see cref="Table{TItem}"/> column offers — derived from the column's
/// parameters, never set directly. Decides the editor the column renders and the shape of its
/// serialized values (see <see cref="Table{TItem}.OnFilterChanged"/>'s payload).
/// </summary>
public enum TableFilterKind
{
    /// <summary>A fixed list of <see cref="TableFilterOption"/>s (<see cref="Column{TItem}.FilterOptions"/>
    /// + <see cref="Column{TItem}.OnFilter"/>): a checkbox list, or radios under
    /// <see cref="Column{TItem}.FilterMultiple"/> = false. Values are the selected option keys.</summary>
    Options,
    /// <summary>A consumer-supplied dropdown template (AntD's <c>filterDropdown</c>); values are the
    /// string keys the template selects, narrowed through <see cref="Column{TItem}.OnFilter"/>.</summary>
    Custom,
    /// <summary>Free-text match over a string accessor; a single trimmed value.</summary>
    Text,
    /// <summary>Inclusive numeric range; two values (min, max), either may be empty.</summary>
    NumberRange,
    /// <summary>Inclusive date range at day granularity; two round-trip (<c>"o"</c>) values.</summary>
    DateRange,
    /// <summary>Tri-state boolean (any / true / false); one value, <c>"true"</c> or <c>"false"</c>.</summary>
    Bool
}

/// <summary>Where a <see cref="Table{TItem}"/> renders its column filter editors.</summary>
public enum TableFilterPlacement
{
    /// <summary>AntD's default: a funnel button in each filterable header cell opens a dropdown
    /// panel whose edits are staged until OK (Reset clears; outside click / Escape discards).</summary>
    Dropdown,
    /// <summary>A dedicated filter row under the header, one always-visible editor per filterable
    /// column; edits apply as they are made (text/number inputs debounced). No funnel buttons.</summary>
    Row
}

/// <summary>How a <see cref="TableFilterKind.Text"/> column filter compares the typed text against a
/// row's value. Every mode is case-insensitive (<see cref="StringComparison.OrdinalIgnoreCase"/>).</summary>
public enum TextFilterMatch
{
    /// <summary>The row's text contains the typed text anywhere (the default).</summary>
    Contains,
    /// <summary>The row's text begins with the typed text.</summary>
    StartsWith,
    /// <summary>The row's text equals the typed text.</summary>
    Equals
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

/// <summary>
/// Which end(s) of a <see cref="DateRangePicker"/>'s range a single commit assigned — the payload of
/// <see cref="DateRangePicker.OnValidCommit"/>. A flags enum rather than one-callback-per-endpoint
/// (the shape every other per-endpoint parameter on that control uses) because one commit genuinely
/// can report both: a two-click range pick, a preset, a session OK and the clear all assign both ends
/// at once, while a typed entry in one input assigns only that one.
/// </summary>
[Flags]
public enum DateRangeEndpoints
{
    /// <summary>Neither endpoint — nothing was assigned, so nothing is reported.</summary>
    None = 0,
    /// <summary>The range's start.</summary>
    Start = 1,
    /// <summary>The range's end.</summary>
    End = 2,
    /// <summary>Both endpoints, assigned by the same commit.</summary>
    Both = Start | End
}

/// <summary>
/// Which text form a <see cref="ColorPicker"/>'s input row edits. Presentation only — the bound
/// <see cref="ColorPicker.Value"/> is always normalized hex regardless of the selected format.
/// </summary>
public enum ColorFormat
{
    /// <summary><c>#rrggbb</c> / <c>#rrggbbaa</c> in a single text box (the default).</summary>
    Hex,
    /// <summary>One number box per channel, plus an alpha percentage box when alpha is enabled.</summary>
    Rgb
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
