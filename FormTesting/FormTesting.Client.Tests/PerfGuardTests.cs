using System.Collections;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Pins the per-keystroke perf guards: any validation-state change re-renders every InputBase
/// control in the form and re-parameterizes its FormLabel / FieldValidationDisplay children (the
/// List/FieldIdentifier parameters defeat Blazor's change skip), so without an inputs-changed guard
/// their OnParametersSet re-derived labels/attributes — and re-invoked the consumer's
/// <see cref="FormOptions.RequiredResolver"/> — on every keystroke in any field. Also pins the
/// EditMultiSelect read-only label join building one options lookup per Options reference instead
/// of scanning Options once per selected value, and that the guards don't over-cache.
/// </summary>
public sealed class PerfGuardTests : TestContext
{
    // EditForm(editContext) -> CascadingValue<FormOptions> -> inner, matching RequiredResolverTests.
    IRenderedFragment RenderForm(EditContext editContext, FormOptions formOptions, RenderFragment inner) =>
        Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<CascadingValue<FormOptions>>(0);
                formContent.AddAttribute(1, "Value", formOptions);
                formContent.AddAttribute(2, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });

    [Fact]
    public void RequiredResolver_is_not_reinvoked_by_validation_state_rerenders()
    {
        var model = new PersonModel { Username = "bob" };
        var editContext = new EditContext(model);
        var store = new ValidationMessageStore(editContext);
        Expression<Func<string>> field = () => model.Username;
        var resolverCalls = 0;
        var formOptions = new FormOptions
        {
            RequiredResolver = f =>
            {
                resolverCalls++;
                return f.FieldName == nameof(PersonModel.Username);
            },
        };
        var cut = RenderForm(editContext, formOptions, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Username);
            content.AddAttribute(2, "ValueExpression", field);
            content.AddAttribute(3, "Field", field);
            content.CloseComponent();
        });

        Assert.NotNull(cut.Find(".edit-label-required-star")); // resolver was consulted on init
        var callsAfterInitialRender = resolverCalls;

        // NotifyValidationStateChanged re-renders the InputBase control (and re-parameterizes its
        // FormLabel) WITHOUT re-rendering the parent — the precise per-keystroke path, isolated
        // from the bases' legitimate resolver consultation on real parameter sets.
        cut.InvokeAsync(() =>
        {
            store.Add(editContext.Field(nameof(PersonModel.Username)), "not empty enough");
            editContext.NotifyValidationStateChanged();
        });
        cut.InvokeAsync(() => editContext.NotifyValidationStateChanged());
        cut.InvokeAsync(() => editContext.NotifyValidationStateChanged());

        // The churn really did re-render the control subtree (the store message appeared) ...
        Assert.Contains("not empty enough", cut.Find("#error-msg-Username").TextContent);
        // ... yet the resolver was not re-consulted per notification.
        Assert.Equal(callsAfterInitialRender, resolverCalls);
    }

    sealed class CountingOptions<TValue>(List<SelectOption<TValue>> _inner) : IEnumerable<SelectOption<TValue>>
    {
        public int Enumerations { get; private set; }

        public IEnumerator<SelectOption<TValue>> GetEnumerator()
        {
            Enumerations++;
            return _inner.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void MultiSelect_readonly_labels_use_one_options_scan_per_options_reference()
    {
        var options = new CountingOptions<Color>([new(Color.Red, "Red"), new(Color.Green, "Green"), new(Color.Blue, "Blue")]);
        var model = new PersonModel { FavoriteColors = [Color.Green, Color.Blue] };
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;

        // Read-only, so the Select engine (which builds its own lookup) never renders — the only
        // Options consumer is the read-only label join. List controls tolerate a null EditContext,
        // so the control renders standalone and can be re-parameterized directly.
        var cut = RenderComponent<EditMultiSelect<Color>>(ps => ps
            .Add(x => x.Value, model.FavoriteColors)
            .Add(x => x.Field, field)
            .Add(x => x.Options, options)
            .Add(x => x.IsEditMode, false));

        var readOnlyText = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("Green", readOnlyText);
        Assert.Contains("Blue", readOnlyText);
        Assert.Equal(1, options.Enumerations); // one scan builds the lookup — not one per selected value

        // Every selection toggle produces a NEW Value list (same Options reference); the cached
        // lookup must be reused, not rebuilt per click.
        cut.SetParametersAndRender(ps => ps.Add(x => x.Value, new List<Color> { Color.Blue, Color.Red }));

        readOnlyText = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("Blue", readOnlyText);
        Assert.Contains("Red", readOnlyText);
        Assert.Equal(1, options.Enumerations); // join re-ran (new labels) with zero extra Options scans
    }

    [Fact]
    public void FormLabel_still_updates_when_inputs_actually_change()
    {
        var model = new PersonModel { Username = "bob" };
        Expression<Func<string>> field = () => model.Username;
        var label = "First";
        var formOptions = new FormOptions { RequiredResolver = _ => false };

        // The fragment reads the captured locals, so re-rendering the EditForm root re-parameterizes
        // the control with the current label / FormOptions instance.
        var cut = RenderComponent<EditForm>(ps => ps
            .Add(f => f.Model, model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<EditString>(0);
                    inner.AddAttribute(1, "Value", model.Username);
                    inner.AddAttribute(2, "ValueExpression", field);
                    inner.AddAttribute(3, "Field", field);
                    inner.AddAttribute(4, "Label", label);
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            })));

        Assert.Contains("First", cut.Find("label").TextContent);
        Assert.Empty(cut.FindAll(".edit-label-required-star"));

        // Real input changes must still recompute: a new Label string, and a NEW FormOptions whose
        // resolver answers differently — the documented way to force a resolver re-evaluation.
        label = "Second";
        formOptions = new FormOptions { RequiredResolver = _ => true };
        cut.SetParametersAndRender(ps => ps.Add(f => f.Model, model));

        var labelText = cut.Find("label").TextContent;
        Assert.Contains("Second", labelText);
        Assert.DoesNotContain("First", labelText);
        Assert.NotNull(cut.Find(".edit-label-required-star")); // guard didn't over-cache the resolver answer
    }
}
