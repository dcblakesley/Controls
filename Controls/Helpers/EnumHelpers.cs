using System.Collections.Concurrent;

namespace Controls.Helpers;

public static class EnumHelpers
{
    // Type-aware cache: keyed on (declaring type, member name) so two enum types that share a member name
    // can't collide. ConcurrentDictionary is safe across the WebAssembly main thread plus any pre-rendering
    // server threads that touch enums during component init.
    static readonly ConcurrentDictionary<(Type, string), string> _displayNameCache = new();

    /// <summary>
    /// Returns the human-readable display name for an enum value, in priority order:
    /// 1. <c>[EnumDisplayName("…")]</c> (this library's attribute)
    /// 2. <c>[Display(Name="…")]</c> (System.ComponentModel.DataAnnotations)
    /// 3. The enum member name with camelCase split into spaced words (e.g. <c>InProgress</c> → <c>"In Progress"</c>).
    /// Result is cached per (enum type, member name).
    /// </summary>
    public static string GetName(this object? value)
    {
        if (value is null)
            return string.Empty;

        var type = value.GetType();
        var memberName = value.ToString() ?? string.Empty;

        return _displayNameCache.GetOrAdd((type, memberName), key =>
        {
            var (t, name) = key;
            var fi = t.GetField(name);
            if (fi != null)
            {
                var enumDisplayName = fi.GetCustomAttribute<EnumDisplayNameAttribute>();
                if (enumDisplayName != null)
                    return enumDisplayName.Value;

                var displayAttr = fi.GetCustomAttribute<DisplayAttribute>();
                if (displayAttr != null && !string.IsNullOrEmpty(displayAttr.Name))
                    return displayAttr.Name;
            }

            // Fallback: split camelCase into words ("InProgress" → "In Progress")
            return string.Concat(name.Select(c => char.IsUpper(c) ? " " + c : c.ToString())).TrimStart(' ');
        });
    }

    /// <summary>
    /// Converts a string to a valid HTML ID by removing invalid characters and spaces.
    /// </summary>
    public static string ToId(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Replace spaces with hyphens, then drop anything that isn't alphanumeric / hyphen / underscore.
        return System.Text.RegularExpressions.Regex.Replace(value.Replace(" ", "-"), @"[^a-zA-Z0-9\-_]", "");
    }

    /// <summary>
    /// Converts an object (typically an enum) to a valid HTML ID. Returns <see cref="string.Empty"/> when value is null.
    /// </summary>
    public static string ToId(this object? value) =>
        value?.ToString()?.ToId() ?? string.Empty;
}
