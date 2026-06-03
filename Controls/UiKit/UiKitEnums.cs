namespace Controls;

/// <summary>Severity of an <see cref="Alert"/>.</summary>
public enum AlertType
{
    Success,
    Info,
    Warning,
    Error
}

/// <summary>Placement of a <see cref="Tooltip"/> (and <see cref="Popover"/>/<see cref="Popconfirm"/>) relative to its target.</summary>
public enum TooltipPlacement
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
