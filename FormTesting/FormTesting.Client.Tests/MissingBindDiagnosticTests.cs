using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;

namespace FormTesting.Client.Tests;

/// <summary>
/// A control rendered without <c>@bind-Value</c> throws a diagnostic naming the control and the fix.
/// That message could be replaced by an <see cref="ArgumentNullException"/> from disposal: init threw
/// before the FieldIdentifier existed, and unregistering <c>default(FieldIdentifier)</c> hashes a null
/// FieldName. Needs a cascaded <see cref="FormOptions"/> — with none, the unregister is a
/// null-conditional no-op and the masking can't happen at all.
/// <para>
/// These are contract tests, not red/green regressions: bUnit's renderer never disposes a component
/// whose parameter set/init threw, so the masking itself isn't observable here. What they pin is that
/// the diagnostic is the exception that surfaces, and that the disposal guard didn't turn the normal
/// unregister into a no-op.
/// </para>
/// </summary>
public class MissingBindDiagnosticTests : BunitContext
{
    [Fact]
    public void Scalar_control_without_a_bind_reports_the_missing_binding_not_a_disposal_failure()
    {
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();

        var ex = Record.Exception(() => Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name); // one-way only: no ValueExpression
            b.CloseComponent();
        })));

        // The scalar controls get the diagnostic from InputBase.SetParametersAsync ("requires a value
        // for the 'ValueExpression' parameter ... normally provided when using 'bind-Value'"), which
        // fires even earlier than EditControlBase.OnInitialized's own. Either way it must be what
        // surfaces — the disposal of the never-initialized control used to throw over it.
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("EditString", ex.Message);
        Assert.Contains("bind-Value", ex.Message);
    }

    [Fact]
    public void List_control_without_a_bind_reports_the_missing_binding_not_a_disposal_failure()
    {
        var model = new PersonModel { Tags = [] };
        var formOptions = new FormOptions();

        var ex = Record.Exception(() => Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags); // one-way only: no ValueExpression
            b.AddAttribute(2, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        })));

        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("EditCheckedStringList", ex.Message);
        Assert.Contains("@bind-Value", ex.Message);
    }

    [Fact]
    public void A_correctly_bound_control_still_unregisters_on_dispose()
    {
        // The guard must not turn the normal path into a no-op: a control that DID initialize still
        // drops its registration when it leaves the render tree.
        var model = new PersonModel { Name = "Alice" };
        var formOptions = new FormOptions();
        Expression<Func<string>> field = () => model.Name;
        var show = true;
        RenderFragment content = b =>
        {
            if (!show) return;
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        };

        var cut = Render<CascadingValue<FormOptions>>(ps => ps
            .Add(c => c.Value, formOptions)
            .Add(c => c.ChildContent, content));

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");

        show = false;
        cut.Render(ps => ps
            .Add(c => c.Value, formOptions)
            .Add(c => c.ChildContent, content));

        Assert.DoesNotContain(formOptions.FieldIdentifiers, fi => fi.FieldName == "Name");
    }
}
