using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit coverage for <see cref="EditBool.Indeterminate"/> — JS-interop wiring only. bUnit can't
/// observe the actual DOM <c>indeterminate</c> property (there is no HTML attribute for it; see
/// wss-checkbox.js), so the real visual/state assertions live in <c>EditBoolE2ETests</c>. Mirrors
/// <c>UiKitTableTests</c>' equivalent coverage for the Table header checkbox, which shares the same
/// JS helper (see wss-checkbox.js / wss-table.js).
/// </summary>
public class EditBoolIndeterminateTests : TestContext
{
    // EditBool lazily imports wss-checkbox.js to set the indeterminate DOM property; tolerate the import.
    public EditBoolIndeterminateTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    int IndeterminateCalls() => JSInterop.Invocations.Count(i => i.Identifier == "setIndeterminate");

    [Fact]
    public void Indeterminate_unused_never_calls_JS_and_the_checkbox_stays_byte_identical()
    {
        // Default (Indeterminate not set) must not pay a JS round-trip at all, and the rendered
        // checkbox must carry no new attribute -- indeterminate is a DOM property, never HTML markup,
        // so the no-new-params render is unaffected by this feature by construction.
        var model = new PersonModel { IsActive = false };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var checkbox = cut.Find("input[type=checkbox]");
        Assert.False(checkbox.HasAttribute("indeterminate"));
        Assert.Equal(0, IndeterminateCalls());
    }

    [Fact]
    public void Indeterminate_true_invokes_setIndeterminate_with_the_checkbox_id_and_true()
    {
        var model = new PersonModel { IsActive = false };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Indeterminate", true);
            b.CloseComponent();
        }));

        cut.WaitForAssertion(() => Assert.Equal(1, IndeterminateCalls()));
        var invocation = JSInterop.Invocations.Single(i => i.Identifier == "setIndeterminate");
        Assert.Equal("IsActive", invocation.Arguments[0]); // _id defaults to the bound field name
        Assert.Equal(true, invocation.Arguments[1]);

        // Still no HTML attribute -- it's a DOM property, applied via JS only.
        Assert.False(cut.Find("input[type=checkbox]").HasAttribute("indeterminate"));
    }

    [Fact]
    public void Toggling_Indeterminate_reapplies_via_JS()
    {
        var model = new PersonModel { IsActive = false };
        var editContext = new EditContext(model);
        Expression<Func<bool>> field = () => model.IsActive;

        var cut = RenderComponent<EditBool>(ps => ps
            .AddCascadingValue(editContext)
            .Add(c => c.Value, false)
            .Add(c => c.ValueExpression, field)
            .Add(c => c.Indeterminate, true));

        cut.WaitForAssertion(() => Assert.Equal(1, IndeterminateCalls()));

        cut.SetParametersAndRender(ps => ps.Add(c => c.Indeterminate, false));

        cut.WaitForAssertion(() => Assert.Equal(2, IndeterminateCalls()));
        var last = JSInterop.Invocations.Last(i => i.Identifier == "setIndeterminate");
        Assert.Equal(false, last.Arguments[1]);
    }

    [Fact]
    public void Indeterminate_does_not_affect_the_bound_value()
    {
        // AntD semantics: Indeterminate is visual only. Clicking the (unchecked, indeterminate)
        // checkbox still toggles CurrentValue normally.
        var model = new PersonModel { IsActive = false };
        var captured = false;
        var changed = false;
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<bool>(this, v => { captured = v; changed = true; }));
            b.AddAttribute(4, "Indeterminate", true);
            b.CloseComponent();
        }));

        cut.Find("input[type=checkbox]").Change(true);

        Assert.True(changed);
        Assert.True(captured);
    }
}
