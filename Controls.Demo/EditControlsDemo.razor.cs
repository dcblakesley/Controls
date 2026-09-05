using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Controls.Demo;

public partial class EditControlsDemo : IDisposable
{
    internal const string FormsTabKey = "forms";
    internal const string UiKitTabKey = "uikit";

    // Roots the strip's ARIA ids, which the sibling pane's aria-labelledby has to rebuild by hand.
    internal const string TabsId = "demo-shell-tabs";

    [Inject] NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "view")]
    public string? ViewParam { get; set; }

    /// <summary> Which top-level tab is open: absent/anything else is the form controls, "uikit" the UI kit. </summary>
    [Parameter, SupplyParameterFromQuery(Name = "tab")]
    public string? TabParam { get; set; }

    string _activeTab = FormsTabKey;
    CurrentView _currentView = CurrentView.AllControls;
    UiKitView _uiKitView = UiKitView.Alert;

    readonly DemoModelForEditControls _allControlsModel = new();

    public FormOptions FormOptions { get; set; } =
        new() { IsEditMode = true, Hiding = HidingMode.None };

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += HandleLocationChanged;
        UpdateViewFromUrl();
        base.OnInitialized();
    }

    // `view` is parsed against whichever enum the open tab switches over, so every existing
    // /?view=<CurrentView> link keeps working untouched.
    void UpdateViewFromUrl()
    {
        _activeTab = string.Equals(TabParam, UiKitTabKey, StringComparison.OrdinalIgnoreCase) ? UiKitTabKey : FormsTabKey;

        if (string.IsNullOrEmpty(ViewParam))
        {
            // No `view` means the default view, so a tab switch (which drops the parameter) can't
            // leave the outgoing tab's selection showing.
            _currentView = CurrentView.AllControls;
            _uiKitView = UiKitView.Alert;
        }
        else if (_activeTab == UiKitTabKey)
        {
            if (Enum.TryParse<UiKitView>(ViewParam, true, out var uiKitView)) _uiKitView = uiKitView;
        }
        else if (Enum.TryParse<CurrentView>(ViewParam, true, out var view))
        {
            _currentView = view;
        }
        // CommonFeatures' "required-star Demo" section, FormLabel's "Required Star" section, and
        // Comparison's live example all exist to show the star; every other view keeps it
        // suppressed for a cleaner look.
        FormOptions.IsRequiredStarHidden = _currentView is not (CurrentView.CommonFeatures or CurrentView.FormLabel or CurrentView.Comparison);
    }

    void HandleLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        UpdateViewFromUrl();
        StateHasChanged();
    }

    /// <summary> Navigates to a form-control view from outside the shell. Clears `tab` too: `view` is
    /// parsed against the open tab's enum, so it would not resolve from the UI-kit tab. </summary>
    public void ChangeView(CurrentView view) =>
        NavigationManager.NavigateTo(NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["tab"] = null,
            ["view"] = view.ToString(),
        }));

    public void Dispose() => NavigationManager.LocationChanged -= HandleLocationChanged;

    void GoToView(CurrentView view) =>
        NavigationManager.NavigateTo(
            NavigationManager.GetUriWithQueryParameter("view", view.ToString()));

    void GoToUiKitView(UiKitView view) =>
        NavigationManager.NavigateTo(
            NavigationManager.GetUriWithQueryParameter("view", view.ToString()));

    // Switching tabs drops `view`: it names a member of the outgoing tab's enum, which the incoming
    // one cannot resolve. Null removes a parameter, so the forms tab is the bare URL again.
    void GoToTab(string? key) =>
        NavigationManager.NavigateTo(NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["tab"] = key == UiKitTabKey ? UiKitTabKey : null,
            ["view"] = null,
        }));
}

public enum CurrentView
{
    AllControls,
    CommonFeatures,
    FormLabel,
    Comparison,
    Bool,
    BoolNullRadio,
    CheckedStringList,
    CheckedEnumList,
    Color,
    Date,
    DateNative,
    DateRange,
    Number,
    Radio,
    RadioEnum,
    RadioString,
    Range,
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

public enum UiKitView
{
    Alert,
    Skeleton,
    Popover,
    Pagination,
    ModalDrawer,
    Popconfirm,
    Table,
    Toasts,
    Tabs,
    SearchInput,
    SelectPill,
    DateRangePicker,
    DatePicker,
    ColorPicker,
    Tooltip
}
