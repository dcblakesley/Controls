using Controls;

namespace Controls.Helpers;

/// <summary>
/// Builds a value → option lookup for a Select-family option list, with one documented tie-break
/// for a duplicate <see cref="SelectOption{TValue}.Value"/>: the LAST matching option wins — matching
/// <see cref="Select{TValue}"/>'s own engine. <see cref="EditSelectSearch{TValue}"/> and
/// <see cref="EditMultiSelect{TValue}"/> each used to build their own lookup (a linear
/// <c>FirstOrDefault</c> scan, and a first-wins <c>TryAdd</c> dictionary respectively) — under a
/// duplicate-valued option list, the same bound value could render a different label in the
/// interactive dropdown than in the read-only view. Both now build through here instead.
/// </summary>
// Null option values are filtered before insertion, so the returned dictionary never holds a null
// key -- suppressed for the whole class so TValue stays unconstrained (e.g. nullable-enum options)
// both here and in every field this method's return type flows into.
#pragma warning disable CS8714
public static class SelectOptionLookup
{
    public static Dictionary<TValue, SelectOption<TValue>> Build<TValue>(IEnumerable<SelectOption<TValue>>? options)
    {
        var lookup = new Dictionary<TValue, SelectOption<TValue>>(EqualityComparer<TValue>.Default);
        foreach (var o in options ?? [])
        {
            if (o.Value is not null) lookup[o.Value] = o;
        }
        return lookup;
    }
}
#pragma warning restore CS8714
