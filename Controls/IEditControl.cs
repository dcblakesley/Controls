namespace Controls;

/// <summary> Common properties for all Edit Controls. </summary>
public interface IEditControl
{
    /// <summary>
    /// IDs are used for the label and input. If not provided, the Id will be automatically generated based on the name of the Property.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Optional, can be used to distinguish between multiple forms on the same page. <br/>
    /// ex. If you have a control that allows you to enter multiple people, it would default to having duplicate IDs. <br/>
    /// </summary>
    public string? IdPrefix { get; set; }

    public bool IsEditMode { get; set; }
    public bool IsDisabled { get; set; }
    public string? Label { get; set; }
    public string? Description { get; set; }
    public HidingMode? Hiding { get; set; }
    public string? ContainerClass { get; set; }
}