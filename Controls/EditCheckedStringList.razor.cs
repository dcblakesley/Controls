namespace Controls;

/// <summary> Provides checkboxes for each input string (in Options), binds to a List of selected strings.</summary>
public partial class EditCheckedStringList : EditControlListBase<string>
{
    // Component-specific parameters

    /// <summary> Expression that binds to the list of strings property in the model.</summary>
    [Parameter] public required Expression<Func<List<string>>> Field { get; set; }

    /// <summary> List of string options to display as checkboxes.</summary>
    [Parameter] public List<string> Options { get; set; } = [];

    /// <summary> Labels for the checkboxes.</summary>
    [Parameter] public string? LabelClass { get; set; }

    /// <summary> If true, the checkboxes will be displayed horizontally.</summary>
    [Parameter] public bool IsHorizontal { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        InitState(Field);
    }
}
