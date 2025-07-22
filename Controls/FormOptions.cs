namespace Controls;

public class FormOptions
{
    public bool IsEditMode { get; set; } = true;

    public List<FieldIdentifier> FieldIdentifiers { get; private set; } = [];

    /// <summary> Allows you to set the hiding mode for the entire form. </summary>
    public HidingMode? Hiding { get; set; }

    /// <summary> This is only for debugging purposes. </summary>
    public bool ShowBoundValues { get; set; }
}

public class FormGroupOptions
{
    public string? Name { get; set; }
}