namespace Controls;

/// <summary> 
/// Common options for a group of EditControls, intended to be used as
/// a cascading parameter. To effect many controls at once.
/// </summary>
public class FormOptions
{
    public bool IsEditMode { get; set; } = true;

    /// <summary> Do not use, this is used by the framework to keep track of which fields are in the form. </summary>
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
    /// <summary> The name for a group of controls </summary>
    public string? Name { get; set; }
}