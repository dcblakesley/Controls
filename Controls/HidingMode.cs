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

internal static class ExtensionMethods
{
    /// <summary> Removes all non-alphanumeric characters from the input string to create a valid ID within html. </summary>
    internal static string ToId(this string input) => string.IsNullOrEmpty(input)
            ? string.Empty
            : new(input.Where(char.IsLetterOrDigit).ToArray());
}