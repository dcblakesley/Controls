namespace Controls;

/// <summary>
/// Helpers for components that capture unmatched attributes. Blazor does not merge a splatted
/// <c>class</c>/<c>style</c> with one written in markup — the rightmost duplicate attribute wins
/// outright — so components merge those two by hand and splat the rest.
/// </summary>
internal static class AttributeSplat
{
    /// <summary> The consumer's <c>class</c> from the captured attributes, or null.</summary>
    public static string? ConsumerClass(IReadOnlyDictionary<string, object>? attributes) => Get(attributes, "class");

    /// <summary>
    /// The component's own inline style merged with the consumer's <c>style</c> (consumer last, so
    /// its declarations win on conflict), or just one of the two when the other is absent/empty.
    /// </summary>
    public static string? MergeStyle(string? baseStyle, IReadOnlyDictionary<string, object>? attributes)
    {
        var consumer = Get(attributes, "style");
        if (consumer is null) return baseStyle;
        if (string.IsNullOrEmpty(baseStyle)) return consumer;
        return $"{baseStyle.TrimEnd().TrimEnd(';')}; {consumer}";
    }

    /// <summary>
    /// The captured attributes without <c>class</c>/<c>style</c> (those are merged into the markup
    /// by hand), for the <c>@attributes</c> splat. Null when nothing remains, so no splat is emitted.
    /// </summary>
    public static IReadOnlyDictionary<string, object>? Rest(IReadOnlyDictionary<string, object>? attributes)
    {
        if (attributes is null || attributes.Count == 0) return null;
        if (!attributes.ContainsKey("class") && !attributes.ContainsKey("style")) return attributes;
        var rest = attributes
            .Where(kv => kv.Key is not ("class" or "style"))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        return rest.Count == 0 ? null : rest;
    }

    /// <summary>
    /// <see cref="Rest"/> with the component's <paramref name="own"/> attributes layered on top, for an
    /// element that already binds an <c>@attributes</c> dictionary of its own (the radio fieldsets'
    /// <c>RadioAria.Fieldset(...)</c> block, the text editors' <c>EditorInputAttributes</c>). Razor
    /// allows only one <c>@attributes</c> per element, so the two have to be merged rather than
    /// splatted side by side — and <paramref name="own"/> wins every collision, which is the same
    /// "explicit beats splat" precedence a splat-first markup position gives the hand-written
    /// attributes beside it.
    /// </summary>
    /// <remarks>
    /// Returns one of the inputs unchanged whenever the other contributes nothing, so the common
    /// no-consumer-attributes case allocates nothing and renders exactly the frames it did before.
    /// </remarks>
    public static IReadOnlyDictionary<string, object>? RestWith(
        IReadOnlyDictionary<string, object>? attributes, IReadOnlyDictionary<string, object>? own)
    {
        var rest = Rest(attributes);
        if (own is null || own.Count == 0) return rest;
        if (rest is null) return own;
        var merged = new Dictionary<string, object>(rest);
        foreach (var kv in own) merged[kv.Key] = kv.Value;
        return merged;
    }

    static string? Get(IReadOnlyDictionary<string, object>? attributes, string key) =>
        attributes is not null &&
        attributes.TryGetValue(key, out var value) &&
        Convert.ToString(value, CultureInfo.InvariantCulture) is { Length: > 0 } text
            ? text
            : null;
}
