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

    // Cache the sanitized ids — ToId is called 2-3x per option per render in the enum/string
    // checkbox/radio/select loops, and the regex + intermediate string would otherwise run every time.
    // Bounded: option strings can be runtime/user data, and this is a process-wide static on Blazor
    // Server — unbounded it would grow for the process lifetime, one entry per distinct string ever
    // rendered. Past the cap, compute without caching (correctness unaffected, memoization lost).
    const int IdCacheCap = 10_000;
    static readonly ConcurrentDictionary<string, string> _idCache = new();

    /// <summary>
    /// Converts a string to a valid HTML ID by removing invalid characters and spaces.
    /// </summary>
    public static string ToId(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (_idCache.TryGetValue(value, out var cached))
            return cached;

        // Replace spaces with hyphens, then drop anything that isn't alphanumeric / hyphen / underscore.
        var computed = System.Text.RegularExpressions.Regex.Replace(value.Replace(" ", "-"), @"[^a-zA-Z0-9\-_]", "");
        if (_idCache.Count < IdCacheCap)
            _idCache[value] = computed;
        return computed;
    }

    /// <summary>
    /// Converts an object (typically an enum) to a valid HTML ID. Returns <see cref="string.Empty"/> when value is null.
    /// </summary>
    public static string ToId(this object? value) =>
        value?.ToString()?.ToId() ?? string.Empty;
}
