namespace Controls.Demo;

public partial class DemoAllControls
{
    [Inject] IJSRuntime JsRuntime { get; set; } = null!;

    readonly DemoModelForEditControls _model = new();
    EditForm editForm = null!; // Set by @ref during Render

    public FormOptions FormOptions { get; set; } =
        new() { IsEditMode = true, Hiding = HidingMode.None };

    readonly List<string> _colorOptions =
        ["None", "Red", "Green", "Blue", "Yellow", "Orange", "Purple", "Black", "White"];

    readonly List<string> _editCheckedStringListOptions =
    [
        "Azathoth", "Yog-Sothoth", "Shub-Niggurath", "Nyarlathotep",
        "Cthulhu", "Hastur", "Dagon", "Ithaqua", "Tsathoggua"
    ];

    readonly List<Plant> _plants = Plant.GetTestData();
    bool _isHorizontal = false;
    public HidingMode HidingMode { get; set; }

    void ForceValidation()
    {
        editForm!.EditContext!.Validate();
    }
    async Task HandleInvalidSubmit() => await JsInteropEc.FocusFirstInvalidField(JsRuntime);
    async Task HandleValidSubmit() => await JsInteropEc.Log(JsRuntime, "Hello there");

}