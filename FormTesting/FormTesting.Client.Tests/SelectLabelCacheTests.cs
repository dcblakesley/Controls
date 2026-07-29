using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Pins the read-only label text of both AntD-style form selects, which now resolve it through one
/// shared per-instance cache (<c>SelectLabelCache</c>, internal — exercised through the wrappers):
/// the matching option's label, the value's own <c>ToString</c> when no option matches, and
/// EditMultiSelect's ", " join in bound-list order. The reference-guarded caching is what these guard
/// against over-caching: a new <c>Options</c> reference that relabels an already-bound value, and (for
/// EditSelectSearch) a changed value against an unchanged option list, must both re-resolve.
/// The other side — the lookup must NOT be rebuilt per parameter set — is pinned by
/// <see cref="PerfGuardTests"/>; the edit/read-only switch itself by <see cref="EditSelectControlsTests"/>.
/// </summary>
public class SelectLabelCacheTests : BunitContext
{
    static string ReadOnlyText<TComponent>(IRenderedComponent<TComponent> cut) where TComponent : IComponent =>
        cut.Find(".edit-readonly-value").TextContent.Trim();

    // A read-only EditSelectSearch inside an EditForm whose ChildContent reads the captured state, so
    // re-rendering the form re-parameterizes the control with whatever the caller's locals hold now
    // (the PerfGuardTests pattern). Scalar controls are InputBase-derived, so they need the cascading
    // EditContext an EditForm supplies; `options` is a factory so a test can swap the *reference*
    // deliberately rather than handing over a new list on every render.
    IRenderedComponent<EditForm> RenderReadOnlySelectSearch(PersonModel model, Func<List<SelectOption<Priority?>>> options)
    {
        Expression<Func<Priority?>> field = () => model.Priority;
        return Render<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<EditSelectSearch<Priority?>>(0);
                b.AddAttribute(1, "Value", model.Priority);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "Options", options());
                b.AddAttribute(4, "IsEditMode", false);
                b.CloseComponent();
            })));
    }

    [Fact]
    public void EditSelectSearch_read_only_label_re_resolves_when_a_new_Options_reference_relabels_the_bound_value()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        List<SelectOption<Priority?>> options = [new(Priority.Low, "Low"), new(Priority.Medium, "Medium")];
        var cut = RenderReadOnlySelectSearch(model, () => options);

        Assert.Equal("Medium", ReadOnlyText(cut));

        // Same bound value, new option list carrying a new label for it: the cached text is keyed on the
        // value, so it only updates if rebuilding the lookup also invalidates it.
        options = [new(Priority.Low, "Low"), new(Priority.Medium, "Medium (renamed)")];
        cut.Render(ps => ps.Add(f => f.Model, model));

        Assert.Equal("Medium (renamed)", ReadOnlyText(cut));
    }

    [Fact]
    public void EditSelectSearch_read_only_label_re_resolves_when_the_value_changes_against_the_same_Options()
    {
        var model = new PersonModel { Priority = Priority.Medium };
        List<SelectOption<Priority?>> options = [new(Priority.Low, "Low"), new(Priority.Medium, "Medium")];
        var cut = RenderReadOnlySelectSearch(model, () => options);

        Assert.Equal("Medium", ReadOnlyText(cut));

        model.Priority = Priority.Low;                              // same Options reference throughout
        cut.Render(ps => ps.Add(f => f.Model, model));

        Assert.Equal("Low", ReadOnlyText(cut));
    }

    [Fact]
    public void EditSelectSearch_read_only_label_falls_back_to_ToString_when_no_option_matches()
    {
        // Critical is bound but absent from the option list — the read-only view shows the value itself
        // rather than blanking, matching what the engine renders for an unmatched value in edit mode.
        var model = new PersonModel { Priority = Priority.Critical };
        List<SelectOption<Priority?>> options = [new(Priority.Low, "Low"), new(Priority.Medium, "Medium")];
        var cut = RenderReadOnlySelectSearch(model, () => options);

        Assert.Equal("Critical", ReadOnlyText(cut));
    }

    [Fact]
    public void EditMultiSelect_read_only_labels_re_resolve_when_a_new_Options_reference_relabels_a_bound_value()
    {
        // List controls tolerate a null EditContext (see PerfGuardTests), so this renders standalone and
        // can be re-parameterized directly.
        var model = new PersonModel { FavoriteColors = [Color.Green, Color.Red] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render<EditMultiSelect<Color>>(ps => ps
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options, [new SelectOption<Color>(Color.Green, "Green"), new SelectOption<Color>(Color.Red, "Red")])
            .Add(x => x.IsEditMode, false));

        Assert.Equal("Green, Red", ReadOnlyText(cut));

        // Same bound list reference, relabelled options: the joined text is cached against the list
        // reference, so this only updates if rebuilding the lookup invalidates it.
        cut.Render(ps => ps
            .Add(x => x.Options, [new SelectOption<Color>(Color.Green, "Forest Green"), new SelectOption<Color>(Color.Red, "Red")]));

        Assert.Equal("Forest Green, Red", ReadOnlyText(cut));
    }

    [Fact]
    public void EditMultiSelect_read_only_labels_join_in_bound_list_order_with_a_ToString_fallback_per_value()
    {
        // Bound order is Blue, Red, PaleYellow while the options are listed Red, Green, Blue — the join
        // follows the bound list, and PaleYellow (no option) falls back to its own ToString.
        var model = new PersonModel { FavoriteColors = [Color.Blue, Color.Red, Color.PaleYellow] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render<EditMultiSelect<Color>>(ps => ps
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.ValueExpression, field)
            .Add(x => x.Options,
            [
                new SelectOption<Color>(Color.Red, "Red"),
                new SelectOption<Color>(Color.Green, "Green"),
                new SelectOption<Color>(Color.Blue, "Blue")
            ])
            .Add(x => x.IsEditMode, false));

        Assert.Equal("Blue, Red, PaleYellow", ReadOnlyText(cut));
    }
}
