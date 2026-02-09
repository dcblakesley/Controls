namespace Controls.Helpers;

public static class EnumHelpers
{
    static readonly Dictionary<object, string> _nameCache = new();

    public static string GetName(this object value)
    {
        if (_nameCache.TryGetValue(value, out var cached))
            return cached;

        var fi = value.GetType().GetField(value.ToString() ?? string.Empty);
        var attributes = fi?.GetCustomAttributes(typeof(EnumDisplayNameAttribute), false) as EnumDisplayNameAttribute[];
        if (attributes != null && attributes.Any())
        {
            var result = attributes.First().Value;
            _nameCache[value] = result;
            return result;
        }

        var text = value.ToString();
        if (text != null)
        {
            // split by camel case
            text = string.Concat(text.Select(x => char.IsUpper(x) ? " " + x : x.ToString())).TrimStart(' ');
            _nameCache[value] = text;
            return text;
        }

        _nameCache[value] = "";
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