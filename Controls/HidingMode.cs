namespace Controls;

public enum HidingMode
{
    None = 1,
    WhenReadOnlyAndNull = 2,
    WhenReadOnlyAndNullOrDefault = 3,

    /// <summary> Hides both Edit and Non-Edit modes when the value is null. </summary>
    WhenNull = 4,

    /// <summary> Hides both Edit and Non-Edit modes when the value is null or empty. </summary>
    WhenNullOrDefault = 5
}