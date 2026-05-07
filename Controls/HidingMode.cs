namespace Controls;

/// <summary>
/// Controls when an edit control is hidden based on its bound value. Defaults to <see cref="None"/>.
/// Can be set per-control or globally via <see cref="FormOptions.Hiding"/>.
/// </summary>
public enum HidingMode
{
    /// <summary> Always show the control. </summary>
    None,

    /// <summary> Hide only in read-only mode when the value is null. </summary>
    WhenReadOnlyAndNull,

    /// <summary> Hide only in read-only mode when the value is null or its type's default (e.g. empty string, 0, default DateTime). </summary>
    WhenReadOnlyAndNullOrDefault,

    /// <summary> Hide in both edit and read-only modes when the value is null. </summary>
    WhenNull,

    /// <summary> Hide in both edit and read-only modes when the value is null or its type's default. </summary>
    WhenNullOrDefault
}