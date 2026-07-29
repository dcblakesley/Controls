namespace FormTesting.Client.Tests;

/// <summary>
/// Covers <see cref="EditCheckedEnumList{TEnum}"/>'s adoption of the shared
/// <see cref="EnumOptionCache{TEnum}"/> (it previously carried a private copy of the same
/// resolve/build/rebuild machinery) plus the non-nullable option view that adoption needed.
/// <see cref="EnumOptionCacheTests"/> covers the helper's own build/refresh rules; these tests pin
/// the two things the swap could have silently changed — the control's runtime <c>Sort</c> rebuild,
/// and the two option views agreeing.
/// </summary>
public class EnumOptionCacheAdoptionTests : BunitContext
{
    class ColorListModel
    {
        public List<Color> Colors { get; set; } = [];
    }

    // Color's numeric order, by the display name each option's label renders:
    // Red, Green ([EnumDisplayName("Forest Green")]), Blue ([Display(Name = "Sky Blue")]), PaleYellow.
    static readonly string[] NumericOrder = ["Red", "Forest Green", "Sky Blue", "Pale Yellow"];

    static List<string> OptionLabels(IRenderedComponent<EditCheckedEnumList<Color>> cut) =>
        cut.FindAll(".edit-checkbox-label").Select(l => l.TextContent.Trim()).ToList();

    [Fact]
    public void Sort_flipped_at_runtime_reorders_the_options()
    {
        // The option list is cached, so a Sort change after the first render has to go through the
        // cache's Refresh guard — without it the order stays frozen at its init-time shape forever.
        var model = new ColorListModel();
        var cut = Render<EditCheckedEnumList<Color>>(ps => ps
            .Add(c => c.Value, model.Colors)
            .Add(c => c.ValueExpression, () => model.Colors));

        Assert.Equal(NumericOrder, OptionLabels(cut));

        cut.Render(ps => ps.Add(c => c.Sort, true));

        var sorted = OptionLabels(cut);
        Assert.Equal(sorted.OrderBy(x => x, StringComparer.Ordinal).ToList(), sorted);
        Assert.NotEqual(NumericOrder, sorted);
    }

    [Fact]
    public void Sort_flipped_back_restores_the_enum_numeric_order()
    {
        var model = new ColorListModel();
        var cut = Render<EditCheckedEnumList<Color>>(ps => ps
            .Add(c => c.Value, model.Colors)
            .Add(c => c.ValueExpression, () => model.Colors)
            .Add(c => c.Sort, true));

        Assert.NotEqual(NumericOrder, OptionLabels(cut));

        cut.Render(ps => ps.Add(c => c.Sort, false));

        Assert.Equal(NumericOrder, OptionLabels(cut));
    }

    [Fact]
    public void OptionsNonNullable_carries_the_same_options_in_the_same_order_as_Options()
    {
        // EditCheckedEnumList reads OptionsNonNullable (its bound list and IsOptionDisabled predicate
        // are typed on a bare TEnum); EditSelectEnum/EditRadioEnum read Options. The two views must
        // never disagree on content or order -- including through the Other-reserved-last branch,
        // which only one of them exercises in production.
        // (At a closed instantiation the two properties share one type: a bare `TEnum?` on an
        // unconstrained parameter is a nullability annotation only, so the distinction is visible
        // solely inside the still-generic control classes.)
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: true, hasOtherOption: true);

        Assert.Equal(cache.OptionsNonNullable, cache.Options);
    }

    [Fact]
    public void Both_option_views_stay_reference_stable_until_a_rebuild()
    {
        // Each accessor hands back a cached list rather than converting per call, so a render never
        // allocates and a parameter cycle that changes nothing keeps the same instances.
        var cache = new EnumOptionCache<Priority>();
        cache.Initialize(sort: false, hasOtherOption: false);
        var options = cache.Options;
        var nonNullable = cache.OptionsNonNullable;

        cache.Refresh(sort: false, hasOtherOption: false);

        Assert.Same(options, cache.Options);
        Assert.Same(nonNullable, cache.OptionsNonNullable);

        cache.Refresh(sort: true, hasOtherOption: false);

        // A real shaping change rebuilds both views, not just the one the consumer happens to read.
        Assert.NotSame(options, cache.Options);
        Assert.NotSame(nonNullable, cache.OptionsNonNullable);
    }
}
