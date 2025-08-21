namespace Controls.Helpers;

public static class EnumHelpers
{
    public static string GetName(this object value)
    {
        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);
        var attributes = fi?.GetCustomAttributes(typeof(EnumDisplayNameAttribute), false) as EnumDisplayNameAttribute[];
        if (attributes != null && attributes.Any())
        {
            return attributes.First().Value;
        }

        var text = value.ToString();
        if (text != null)
        {
            // split by camel case
            text = string.Concat(text.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            return text;
        }

        return "";
    }

    /// <summary>
    /// Converts a string to a valid HTML ID by removing invalid characters and spaces
    /// </summary>
    public static string ToId(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Replace spaces and special characters with hyphens, then remove any invalid characters
        return System.Text.RegularExpressions.Regex.Replace(value.Replace(" ", "-"), @"[^a-zA-Z0-9\-_]", "");
    }

    /// <summary>
    /// Converts an object (typically enum) to a valid HTML ID
    /// </summary>
    public static string ToId(this object value)
    {
        return value?.ToString()?.ToId() ?? string.Empty;
    }
}