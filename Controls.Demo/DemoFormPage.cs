using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Controls.Demo;

// Shared base for demo pages that eagerly validate their form so validation messages (required
// fields, range errors, etc.) are visible on first render instead of only after a submit click.
// Owns the `_form` field that each page's `@ref="_form"` binds to and the OnAfterRender override
// that triggers validation. `override` here is already further-overridable by a derived page that
// needs extra OnAfterRender logic -- it should call `base.OnAfterRender(firstRender)` plus its own.
public class DemoFormPage : ComponentBase
{
    protected EditForm? _form;

    protected override void OnAfterRender(bool firstRender) => _form?.EditContext!.Validate();
}
