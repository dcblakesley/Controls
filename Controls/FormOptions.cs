namespace Controls;

/// <summary>
/// Common options for a group of EditControls, intended to be as
/// a cascading parameter. To effect many controls at once.
/// </summary>
public class FormOptions
{
    public bool IsEditMode { get; set; } = true;

    public List<FieldIdentifier> FieldIdentifiers { get; set; } = [];

    /// <summary> Allows you to set the hiding mode for the entire form. </summary>
    public HidingMode? Hiding { get; set; }

    /// <summary> This is only for debugging purposes. </summary>
    public bool ShowBoundValues { get; set; }

    /// <summary> When true, hides labels for all controls in the form. </summary>
    public bool IsLabelHidden { get; set; }
}

/// <summary>
/// Provides a name for the group of controls, for the purpose of creating a unique ID for each
/// control when using multiple instances of the same class.
/// </summary>
public class FormGroupOptions
{
    public string? Name { get; set; }
}