namespace FormTesting.Client.Tests;

/// <summary>
/// Tests <see cref="EnumOptionCache{TEnum}"/> directly -- the cached-enum-options machinery extracted
/// out of EditSelectEnum's and EditRadioEnum's near-identical BuildOptions/OnParametersSet pairs.
/// Uses <see cref="Priority"/> (Low, Medium, High, Critical -- no display attributes) so numeric order
/// and alphabetical order visibly differ.
/// </summary>
public class EnumOptionCacheTests
{
    // TEnum is unconstrained (no `where TEnum : struct`), matching EditSelectEnum/EditRadioEnum -- for
    // a non-nullable value-type TEnum like Priority, the cache's `List<TEnum?>` erases to plain
    // `List<Priority>` (the "Priority?" instantiation below is the one that actually produces
    // `List<Priority?>`; see Initialize_resolves_a_nullable_enum).
    static readonly List<Priority> NumericOrder = [Priority.Low, Priority.Medium, Priority.High, Priority.Critical];
    static readonly List<Priority> AlphabeticalOrder = [Priority.Critical, Priority.High, Priority.Low, Priority.Medium];
    static readonly List<Priority> OtherReservedLast = [Priority.High, Priority.Low, Priority.Medium, Priority.Critical];

    [Fact]
    public void Initialize_defaults_to_the_enum_numeric_order()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: false, hasOtherOption: false);

        Assert.Equal(NumericOrder, cache.Options);
    }

    [Fact]
    public void Initialize_with_Sort_orders_alphabetically_by_display_name()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: true, hasOtherOption: false);

        Assert.Equal(AlphabeticalOrder, cache.Options);
    }

    [Fact]
    public void HasOtherOption_without_Sort_still_reserves_the_last_numeric_value_as_Other()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: false, hasOtherOption: true);

        // Critical (the last numeric value) is pulled out and appended back unsorted -- since it was
        // already last, order is unchanged from the plain numeric list, but this still exercises the
        // "Other" removal/append branch rather than skipping it.
        Assert.Equal(NumericOrder, cache.Options);
    }

    [Fact]
    public void HasOtherOption_keeps_the_last_numeric_value_last_even_when_Sort_reorders_the_rest()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: true, hasOtherOption: true);

        // Critical is pulled out before the alphabetical sort of the remaining three (Low/Medium/High
        // -> High/Low/Medium) and appended back at the end -- it stays last regardless of where it
        // would otherwise land alphabetically ("Critical" sorts first).
        Assert.Equal(OtherReservedLast, cache.Options);
    }

    [Fact]
    public void Initialize_resolves_a_non_nullable_enum()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: false, hasOtherOption: false);

        Assert.False(cache.IsNullable);
        Assert.Equal(typeof(Priority), cache.UnderlyingType);
    }

    [Fact]
    public void Initialize_resolves_a_nullable_enum()
    {
        var cache = new EnumOptionCache<Priority?>();
        cache.Initialize(sort: false, hasOtherOption: false);

        Assert.True(cache.IsNullable);
        Assert.Equal(typeof(Priority), cache.UnderlyingType);
        // The option list itself is unaffected by nullable-ness -- same members, same order, just
        // carried as Priority? instead of Priority.
        Assert.Equal<Priority?>([Priority.Low, Priority.Medium, Priority.High, Priority.Critical], cache.Options);
    }

    [Fact]
    public void Refresh_rebuilds_when_Sort_flips_at_runtime()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: false, hasOtherOption: false);
        Assert.Equal(NumericOrder, cache.Options);

        cache.Refresh(sort: true, hasOtherOption: false);

        Assert.Equal(AlphabeticalOrder, cache.Options);
    }

    [Fact]
    public void Refresh_rebuilds_when_HasOtherOption_flips_at_runtime()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: true, hasOtherOption: false);
        Assert.Equal(AlphabeticalOrder, cache.Options);

        cache.Refresh(sort: true, hasOtherOption: true);

        Assert.Equal(OtherReservedLast, cache.Options);
    }

    [Fact]
    public void Refresh_is_a_no_op_when_neither_shaping_parameter_changed()
    {
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: true, hasOtherOption: false);
        var before = cache.Options;

        cache.Refresh(sort: true, hasOtherOption: false);

        // Same list instance -- Refresh must skip rebuilding when nothing that shapes the options
        // actually changed, exactly like the OnParametersSet guard it replaced.
        Assert.Same(before, cache.Options);
    }
}
