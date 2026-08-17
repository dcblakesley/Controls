using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Counts <see cref="EditContext.OnFieldChanged"/> invocations per user gesture, per control family.
/// </summary>
/// <remarks>
/// <para>
/// This is the measurement a form-level auto-save is designed against: one subscription to
/// <c>OnFieldChanged</c> replaces a per-field <c>@bind-Value:after</c>, so the DEBOUNCE has to absorb
/// whatever multiplicity each family emits per gesture. Nothing else in the suite counts these — the
/// sibling <see cref="ValidationNotifyCountTests"/> counts the OTHER event
/// (<c>OnValidationStateChanged</c>) around a different concern (parse-error retirement).
/// </para>
/// <para>
/// The numbers pinned here are behavior, not accidents:
/// <list type="bullet">
/// <item>the three radio groups that forward their <c>ValueExpression</c> to an inner
/// <c>InputRadioGroup</c> (<c>EditRadio</c>, <c>EditRadioEnum</c>, <c>EditRadioString</c>) notify
/// TWICE per click — both the inner group and the outer control's own <c>CurrentValue</c> setter
/// notify the same <see cref="FieldIdentifier"/>. <c>EditBoolNullRadio</c> renders its own radios and
/// notifies once.</item>
/// <item>a PARSE FAILURE notifies while the model still holds the OLD value and the field is now
/// invalid — <see cref="InputBase{TValue}"/>'s documented convention, so an auto-save driven off
/// this event needs a validity gate of its own.</item>
/// <item>the list-bound family notifies exactly once per mutation, on every path
/// (<c>EditControlListBase.SetValueAsync</c>) — including <c>EditFile</c>'s REMOVE, which is easy to
/// forget when hand-wiring per-field callbacks.</item>
/// </list>
/// </para>
/// </remarks>
public class FieldChangedNotificationTests : BunitContext
{
    public FieldChangedNotificationTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the JS imports

    // Renders `inner` inside an EditForm bound to `editContext`, with a DataAnnotationsValidator ahead
    // of it so the invalid-field assertions below see real validation messages.
    IRenderedComponent<ContainerFragment> RenderCounted(EditContext editContext, List<string> notified,
        Action<RenderTreeBuilder> inner)
    {
        editContext.OnFieldChanged += (_, e) => notified.Add(e.FieldIdentifier.FieldName);
        return Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                inner(content);
            }));
            b.CloseComponent();
        });
    }

    // ───────────────────────────── (1) radio groups ─────────────────────────────

    [Fact]
    public void EditRadio_notifies_twice_per_click_inner_group_and_outer_control()
    {
        // EditRadio IS an InputRadioGroup<TValue> and ALSO assigns its own CurrentValue from the inner
        // group's ValueChanged, so one click walks two InputBase commit paths over the same field.
        var model = new PersonModel { Name = "a" };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditRadio<string>>(1);
            content.AddAttribute(2, "Value", model.Name);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<string>(this, v => model.Name = v));
            content.AddAttribute(5, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "a");
                cb.CloseComponent();
                cb.OpenComponent<InputRadio<string>>(2);
                cb.AddAttribute(3, "Value", "b");
                cb.CloseComponent();
            }));
            content.CloseComponent();
        });

        var b = cut.FindAll("input[type=radio]").First(r => r.GetAttribute("value") == "b");
        b.Change("b");

        Assert.Equal("b", model.Name);
        Assert.Equal(["Name", "Name"], notified);
    }

    [Fact]
    public void EditRadioEnum_notifies_twice_per_click()
    {
        var model = new PersonModel { Priority = Priority.Low };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditRadioEnum<Priority?>>(1);
            content.AddAttribute(2, "Value", model.Priority);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<Priority?>(this, v => model.Priority = v));
            content.CloseComponent();
        });

        var radios = cut.FindAll("input[type=radio]");
        radios[1].Change(radios[1].GetAttribute("value"));

        Assert.Equal(Priority.Medium, model.Priority);
        Assert.Equal(["Priority", "Priority"], notified);
    }

    [Fact]
    public void EditRadioString_notifies_twice_per_click()
    {
        var model = new PersonModel { Name = "a" };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<string>> field = () => model.Name;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditRadioString>(1);
            content.AddAttribute(2, "Value", model.Name);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<string?>(this, v => model.Name = v ?? ""));
            content.AddAttribute(5, "Options", new List<string> { "a", "b" });
            content.CloseComponent();
        });

        var b = cut.FindAll("input[type=radio]").First(r => r.GetAttribute("value") == "b");
        b.Change("b");

        Assert.Equal("b", model.Name);
        Assert.Equal(["Name", "Name"], notified);
    }

    [Fact]
    public void EditBoolNullRadio_notifies_once_per_click_it_owns_its_radios()
    {
        // The one radio group that does NOT double-notify: it inherits EditControlBase<bool?> and
        // renders its own <input type=radio>s, so there is no inner InputRadioGroup to notify too.
        var model = new PersonModel { IsSubscribed = null };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditBoolNullRadio>(1);
            content.AddAttribute(2, "Value", model.IsSubscribed);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<bool?>(this, v => model.IsSubscribed = v));
            content.CloseComponent();
        });

        cut.Find("#rb-IsSubscribed-true").Change("true");

        Assert.True(model.IsSubscribed);
        Assert.Equal(["IsSubscribed"], notified);
    }

    // ───────────────────────────── (2) parse failure ─────────────────────────────

    [Fact]
    public void EditNumber_parse_failure_notifies_while_the_model_keeps_the_old_value_and_the_field_is_invalid()
    {
        // InputBase's documented convention, mirrored by EditDate/EditDateNative/EditColor/EditDateRange:
        // an unparseable entry still raises OnFieldChanged so the parse error can be shown -- but the
        // model is untouched. An OnFieldChanged-driven auto-save would therefore re-save the OLD value
        // under a now-invalid field, which is why FormAutoSave gates on validity by default.
        var model = new PersonModel { Age = 30 };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<int?>> field = () => model.Age;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditNumber<int?>>(1);
            content.AddAttribute(2, "Value", model.Age);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<int?>(this, v => model.Age = v));
            content.CloseComponent();
        });

        cut.Find("input").Change("abc");

        Assert.Equal(["Age"], notified);           // it DID notify
        Assert.Equal(30, model.Age);               // ...with the old value still on the model
        Assert.NotEmpty(editContext.GetValidationMessages(editContext.Field(nameof(PersonModel.Age))));
        Assert.False(editContext.IsValid(editContext.Field(nameof(PersonModel.Age))));
    }

    // ───────────────────────────── (3) the list family ─────────────────────────────

    [Fact]
    public void EditCheckedStringList_toggle_notifies_exactly_once()
    {
        var model = new PersonModel { Tags = ["a"] };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditCheckedStringList>(1);
            content.AddAttribute(2, "Value", model.Tags);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<List<string>>(this, v => model.Tags = v));
            content.AddAttribute(5, "Options", new List<string> { "a", "b", "c" });
            content.CloseComponent();
        });

        cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "b").Change(true);
        Assert.Equal(["Tags"], notified);

        // ...and once more on the untoggle, so a "remove" is as visible to an auto-save as an "add".
        cut.FindAll("input[type=checkbox]").First(c => c.GetAttribute("value") == "b").Change(false);
        Assert.Equal(["Tags", "Tags"], notified);
    }

    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = [];
    }

    [Fact]
    public void EditFile_add_and_remove_each_notify_exactly_once()
    {
        var model = new FileModel();
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditFile>(1);
            content.AddAttribute(2, "Value", model.Files);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<List<IBrowserFile>>(this, v => model.Files = v));
            content.CloseComponent();
        });

        // One ADD batch of two files is one list assignment -> one notification, not one per file.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));
        Assert.Equal(["Files"], notified);

        // The REMOVE path notifies too -- the case a hand-wired @bind-Value:after can't reach at all,
        // because no bound field's setter runs for it.
        cut.FindAll(".edit-file-delete-btn")[0].Click();
        Assert.Equal(["Files", "Files"], notified);
        Assert.Single(model.Files);
    }

    [Fact]
    public void EditMultiSelect_selection_notifies_exactly_once()
    {
        var model = new PersonModel { FavoriteColors = [] };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = RenderCounted(editContext, notified, content =>
        {
            content.OpenComponent<EditMultiSelect<Color>>(1);
            content.AddAttribute(2, "Value", model.FavoriteColors);
            content.AddAttribute(3, "ValueExpression", field);
            content.AddAttribute(4, "ValueChanged",
                EventCallback.Factory.Create<List<Color>>(this, v => model.FavoriteColors = v));
            content.AddAttribute(5, "Options", new List<SelectOption<Color>>
            {
                new(Color.Red, "Red"), new(Color.Green, "Green"), new(Color.Blue, "Blue")
            });
            content.CloseComponent();
        });

        cut.Find(".wss-select").Click();
        cut.FindAll("[role=option]").First(o => o.TextContent.Contains("Blue")).Click();

        Assert.Equal([Color.Blue], model.FavoriteColors);
        Assert.Equal(["FavoriteColors"], notified);
    }
}
