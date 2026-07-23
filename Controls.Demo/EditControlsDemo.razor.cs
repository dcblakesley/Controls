using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Routing;

namespace Controls.Demo;

public partial class EditControlsDemo : IDisposable
{
    [Inject] NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "view")]
    public string? ViewParam { get; set; }

    CurrentView _currentView = CurrentView.AllControls;

    readonly DemoModelForEditControls _allControlsModel = new();
    EditForm _form = default!; // Set by @ref during Render

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
    public HidingMode HidingMode { get; set; }

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += HandleLocationChanged;
        UpdateViewFromUrl();
        base.OnInitialized();
    }

    void UpdateViewFromUrl()
    {
        if (!string.IsNullOrEmpty(ViewParam) &&
            Enum.TryParse<CurrentView>(ViewParam, true, out var view))
        {
            _currentView = view;
        }
        // CommonFeatures' "required-star Demo" section and Comparison's live example both exist to
        // show the star; every other view keeps it suppressed for a cleaner look.
        FormOptions.IsRequiredStarHidden = _currentView is not (CurrentView.CommonFeatures or CurrentView.Comparison);
    }

    void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateViewFromUrl();
        StateHasChanged();
    }

    public void ChangeView(CurrentView view)
    {
        var uri = NavigationManager.GetUriWithQueryParameter("view", view.ToString());
        NavigationManager.NavigateTo(uri);
    }

    public void Dispose() => NavigationManager.LocationChanged -= HandleLocationChanged;

    void GoToView(CurrentView view) =>
        NavigationManager.NavigateTo(
            NavigationManager.GetUriWithQueryParameter("view", view.ToString()));
}

public enum CurrentView
{
    AllControls,
    UiKit,
    CommonFeatures,
    Comparison,
    Bool,
    BoolNullRadio,
    CheckedStringList,
    CheckedEnumList,
    Date,
    DatePicker,
    DateRange,
    Number,
    Radio,
    RadioEnum,
    RadioString,
    Select,
    SelectEnum,
    SelectString,
    SelectSearch,
    MultiSelect,
    String,
    TextArea,
    File,
    Theme
}