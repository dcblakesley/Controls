using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// The list-bound controls support swapping the model/EditContext at runtime — unlike the scalar
/// InputBase controls, which throw. These tests pin the re-derived FieldIdentifier (a swap must
/// re-target the new model, not keep notifying the old one) and null-EditContext tolerance
/// (usable outside an EditForm without FieldValidationDisplay NRE-ing).
/// </summary>
public class ListBaseEditContextSwapTests : TestContext
{
    class ItemsModel
    {
        public List<string> Items { get; set; } = [];
    }

    class Holder
    {
        public ItemsModel Model = new();
    }

    [Fact]
    public void Swapping_the_model_retargets_field_notifications_to_the_new_instance()
    {
        var holder = new Holder();
        holder.Model.Items = ["a"];
        EditContext? currentContext = null;

        var cut = RenderComponent<EditForm>(ps => ps
            .Add(f => f.Model, holder.Model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(ctx => b =>
            {
                currentContext = ctx;
                b.OpenComponent<EditCheckedStringList>(0);
                b.AddAttribute(1, "Value", holder.Model.Items);
                b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => holder.Model.Items = v));
                b.AddAttribute(3, "Field", (Expression<Func<List<string>>>)(() => holder.Model.Items));
                b.AddAttribute(4, "Options", new List<string> { "a", "b", "c" });
                b.CloseComponent();
            })));

        // Swap in a fresh model — EditForm creates a new EditContext for it.
        holder.Model = new ItemsModel { Items = ["a", "b"] };
        cut.SetParametersAndRender(ps => ps.Add(f => f.Model, holder.Model));

        FieldChangedEventArgs? notified = null;
        Assert.NotNull(currentContext);
        currentContext.OnFieldChanged += (_, e) => notified = e;

        cut.FindAll("input[type=checkbox]")[2].Change(true); // toggle "c" on

        // With the stale FieldIdentifier the notification still referenced the OLD model instance,
        // so validation silently ran against dead state forever.
        Assert.NotNull(notified);
        Assert.Same(holder.Model, notified.FieldIdentifier.Model);
        Assert.Equal(nameof(ItemsModel.Items), notified.FieldIdentifier.FieldName);
    }

    [Fact]
    public void Swapping_the_model_does_not_accumulate_stale_field_registrations()
    {
        var holder = new Holder();
        var formOptions = new FormOptions();
        var cut = RenderComponent<EditForm>(ps => ps
            .Add(f => f.Model, holder.Model)
            .Add(f => f.ChildContent, (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<EditCheckedStringList>(0);
                    inner.AddAttribute(1, "Value", holder.Model.Items);
                    inner.AddAttribute(2, "Field", (Expression<Func<List<string>>>)(() => holder.Model.Items));
                    inner.AddAttribute(3, "Options", new List<string> { "a", "b" });
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            })));

        // Several model swaps, each creating a new EditContext and re-deriving the FieldIdentifier.
        for (var i = 0; i < 5; i++)
        {
            holder.Model = new ItemsModel();
            cut.SetParametersAndRender(ps => ps.Add(f => f.Model, holder.Model));
        }

        // Without unregister-on-swap this grew by one dead identifier per swap (ValidationView then
        // iterated all of them every render). The old ones are dropped, so only the live one remains.
        Assert.Single(formOptions.FieldIdentifiers);
        Assert.Same(holder.Model, formOptions.FieldIdentifiers[0].Model);
    }

    [Fact]
    public void List_control_works_without_an_EditForm()
    {
        var model = new ItemsModel();
        List<string>? changed = null;
        var cut = RenderComponent<EditCheckedStringList>(ps => ps
            .Add(c => c.Value, model.Items)
            .Add(c => c.ValueChanged, EventCallback.Factory.Create<List<string>>(this, v => changed = v))
            .Add(c => c.Field, () => model.Items)
            .Add(c => c.Options, new List<string> { "a", "b" }));

        cut.FindAll("input[type=checkbox]")[0].Change(true); // previously NRE'd in FieldValidationDisplay

        Assert.Equal(["a"], changed);
    }
}
