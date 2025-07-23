namespace Controls.Demo;

public partial class DemoAllControls
{
    readonly DemoModelForEditControls _model = new();
    EditForm editForm; // Set by @ref during Render

    public FormOptions FormOptions { get; set; } =
        new() { IsEditMode = true, Hiding = HidingMode.None };

    readonly List<string> _colorOptions =
        ["None", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Black", "White"];

    readonly List<string> _editCheckedStringListOptions =
    [
        "Aza tho th", "Yog-Sothoth", "Shub-Niggurath", "Nyarlathotep",
        "Cthulhu", "Hastur", "Dagon", "Ithaqua", "Tsathoggua"
    ];

    List<Plant> _plants = Plant.GetTestData();
    bool _isHorizontal = false;
    public HidingMode HidingMode { get; set; }

    void ForceValidation()
    {
        editForm!.EditContext!.Validate();
    }
}