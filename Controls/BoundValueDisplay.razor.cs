namespace Controls;

/// <summary>
/// Shown at the bottom of every scalar/list edit control while <see cref="Controls.FormOptions.ShowBoundValues"/>
/// is on -- a debug echo of the field's current bound value. Public only because a Razor component's
/// generated partial is always declared <c>public</c> (an <c>internal</c> code-behind here conflicts
/// with it -- CS0262); every call site is still internal to this library, the same as
/// <see cref="ReadOnlyValue"/>/<see cref="FieldValidationDisplay"/> which ship the same way.
/// </summary>
public partial class BoundValueDisplay
{
    [CascadingParameter] FormOptions? FormOptions { get; set; }

    /// <summary> The value text to echo. Each host computes its own display string (e.g. EditFile's
    /// "(none)" for an empty selection), so this component just renders whatever it's given. </summary>
    [Parameter] public string? Text { get; set; }
}
