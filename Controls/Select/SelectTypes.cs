namespace Controls;

/// <summary>Selection behaviour, mirroring Ant Design's Select modes.</summary>
public enum SelectMode
{
    /// <summary>Pick a single value.</summary>
    Single,

    /// <summary>Pick many values from the supplied options, shown as removable tags.</summary>
    Multiple,

    /// <summary>Like <see cref="Multiple"/>, but the user can also type new values that are not in the option list.</summary>
    Tags
}

/// <summary>Visual size of the control, mirroring Ant Design's small/default/large.</summary>
public enum SelectSize
{
    Default,
    Small,
    Large
}

/// <summary>Trigger appearance of <see cref="Select{TValue}"/>.</summary>
public enum SelectVariant
{
    /// <summary>The default bordered-box look (Ant Design outlined select).</summary>
    Outlined,

    /// <summary>A fully-rounded outlined pill that hugs its content — the filter-button trigger.
    /// Usually paired with <see cref="Select{TValue}.Prefix"/> for a leading icon,
    /// <c>ShowSearch="false"</c> and <c>AllowClear="false"</c>. Designed for
    /// <see cref="SelectMode.Single"/> (multiple/tags render as a stadium-shaped tag box).</summary>
    Pill
}

/// <summary>A single selectable option for <see cref="Select{TValue}"/> (and the Edit* select wrappers).</summary>
/// <typeparam name="TValue">The type of the option's value.</typeparam>
public class SelectOption<TValue>
{
    public SelectOption()
    {
    }

    public SelectOption(TValue value, string? label, bool disabled = false)
    {
        Value = value;
        Label = label;
        Disabled = disabled;
    }

    /// <summary>The value carried by the option.</summary>
    public TValue Value { get; set; } = default!;

    /// <summary>The text shown for the option (and used for searching).</summary>
    public string? Label { get; set; }

    /// <summary>When true the option cannot be selected.</summary>
    public bool Disabled { get; set; }
}
