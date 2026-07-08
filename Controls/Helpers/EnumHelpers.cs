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
            // IsEnum gate keeps the reflection trim-safe: ILLink preserves every field of an enum
            // type it keeps (enum ToString/Parse depend on them), so GetField can't come back null
            // for a live enum member. A non-enum object skips the lookup and gets the name split.
            var fi = t.IsEnum ? GetEnumField(t, name) : null;
            if (fi != null)
            {
                var enumDisplayName = fi.GetCustomAttribute<EnumDisplayNameAttribute>();
                if (enumDisplayName != null)
                    return enumDisplayName.Value;

                // GetName() (not .Name) so a localized [Display(Name=…, ResourceType=…)] resolves
                // through its resource manager instead of returning the raw resource key.
                var displayAttr = fi.GetCustomAttribute<DisplayAttribute>();
                var displayName = displayAttr?.GetName();
                if (!string.IsNullOrEmpty(displayName))
                    return displayName;
            }

            // Fallback: split camelCase into words ("InProgress" → "In Progress")
            return string.Concat(name.Select(c => char.IsUpper(c) ? " " + c : c.ToString())).TrimStart(' ');
        });
    }

    // Trimming (IL2070): callers only reach this behind an IsEnum check, and ILLink keeps all
    // fields of preserved enum types, so the lookup target always survives trimming.
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Only called with enum types; ILLink preserves enum fields on kept enum types.")]
    static FieldInfo? GetEnumField(Type enumType, string name) => enumType.GetField(name);

    /// <summary>
    /// AOT-safe replacement for <c>Enum.GetValues(Type).Cast&lt;T&gt;()</c>: works when only the
    /// non-nullable underlying <see cref="Type"/> is known at runtime (the enum controls accept a
    /// nullable <typeparamref name="T"/>). <see cref="Enum.GetValuesAsUnderlyingType(Type)"/> avoids
    /// the <c>RequiresDynamicCode</c> array creation in <c>Enum.GetValues(Type)</c>, and
    /// <see cref="Enum.ToObject(Type, object)"/> re-boxes each value as the enum type, which unboxes
    /// cleanly to both <c>TEnum</c> and <c>TEnum?</c>.
    /// </summary>
    public static List<T> GetValues<T>(Type underlyingType) =>
        Enum.GetValuesAsUnderlyingType(underlyingType)
            .Cast<object>()
            .Select(v => (T)Enum.ToObject(underlyingType, v))
            .ToList();

    // Cache the sanitized ids — ToId is called 2-3x per option per render in the enum/string
    // checkbox/radio/select loops, and the regex + intermediate string would otherwise run every time.
    // Bounded: option strings can be runtime/user data, and this is a process-wide static on Blazor
    // Server — unbounded it would grow for the process lifetime, one entry per distinct string ever
    // rendered. Past the cap, compute without caching (correctness unaffected, memoization lost).
    const int IdCacheCap = 10_000;
    static readonly ConcurrentDictionary<string, string> _idCache = new();
    // Latches true once the cache saturates. Without it, every post-saturation call paid
    // ConcurrentDictionary.Count — which acquires ALL internal locks — forever. The flag flips once,
    // then every later miss skips straight to compute-without-cache and never touches Count again.
    static volatile bool _idCacheFull;

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
        if (!_idCacheFull)
        {
            _idCache[value] = computed;
            // Count runs only on a miss while filling (bounded), then never again once latched.
            // A slight overshoot of the cap under concurrency is benign.
            if (_idCache.Count >= IdCacheCap)
                _idCacheFull = true;
        }
        return computed;
    }

    /// <summary>
    /// Converts an object (typically an enum) to a valid HTML ID. Returns <see cref="string.Empty"/> when value is null.
    /// </summary>
    public static string ToId(this object? value) =>
        value?.ToString()?.ToId() ?? string.Empty;
}
