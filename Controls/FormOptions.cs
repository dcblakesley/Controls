namespace Controls;

public class FormOptions
{
    public bool IsEditMode { get; set; } = true;
    public bool IsReadOnly { get; set; }
    public bool ShowBoundValues { get; set; }
    public List<FieldIdentifier> FieldIdentifiers { get; private set; } = [];

    /// <summary>
    /// Feature to quickly allow users to see only what has been entered or modified from default values. <br/>
    /// Useful for large forms where most values are default. <br/>
    /// When not editing, if the value is the same as the default value, the label and the output will be hidden.
    /// </summary>
    public bool HideNonEditModeUnusedValues { get; set; } = true;
}

public class FormGroupOptions
{
    public string? Name { get; set; }
}