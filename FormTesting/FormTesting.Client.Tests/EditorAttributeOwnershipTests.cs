using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Pins who owns each attribute on EditString's and EditNumber's inner <c>&lt;input&gt;</c> when the
/// consumer splats an attribute of the same name. The rule, in one line:
/// <b>the library wins the collision when it HAS an opinion; the consumer's value survives untouched
/// when it does not</b> — with two deliberate exceptions: the attributes the library owns
/// UNCONDITIONALLY (<c>type</c>, <c>id</c>, <c>class</c>, <c>aria-labelledby</c>,
/// <c>aria-describedby</c>), which it always wins, and <c>aria-invalid</c>, which the FRAMEWORK owns
/// (see the last two tests).
/// </summary>
/// <remarks>
/// <para>
/// This is not the same statement as "explicitly-written attributes beat the splat", which is what
/// <see cref="EditControlBase{TValue}"/>'s class remarks describe and what the markup's splat-first
/// position gives. The gap: in Blazor an explicit attribute frame written after a splat wins outright
/// over an earlier same-named frame EVEN WHEN ITS OWN VALUE IS NULL OR FALSE — the builder still calls
/// <c>TrackAttributeName</c>, and <c>ProcessDuplicateAttributes</c> deletes the earlier frame, so the
/// attribute is omitted entirely. Written as <c>disabled=@IsDisabled</c> /
/// <c>aria-invalid=@(IsInvalid ? "true" : null)</c> beside the splat, the library was therefore not
/// declining to override a consumer's value while it had no opinion — it was silently DELETING it. A
/// consumer who splatted <c>disabled</c> got an editable field.
/// </para>
/// <para>
/// The fix is mechanical: every CONDITIONAL library attribute rides the merged <c>@attributes</c>
/// dictionary (<see cref="EditControlBase{TValue}.EditorStateAttributes"/> for the state set,
/// <c>SuggestionsInputAttributes</c> for <c>list</c>), where contributing nothing really does mean
/// contributing nothing. These tests are the contract; the erasure is invisible in a visual baseline
/// and silent at runtime, so it needs pinning at this level.
/// </para>
/// </remarks>
public class EditorAttributeOwnershipTests : BunitContext
{
    // EditForm(editContext) -> DataAnnotationsValidator + CascadingValue<FormOptions> -> inner, copied
    // from EditInputShellTests.RenderForm. Passing the EditContext explicitly (rather than
    // EditForm.Model) is what lets a test call editContext.Validate() itself, which is the only way to
    // reach the aria-invalid/aria-errormessage branch. The CascadingValue is not incidental: it gives
    // `inner` its own render-tree scope, so its sequence numbers can't collide with the validator's --
    // sharing the scope produced a correct FIRST render and a mangled diff on the re-render Validate()
    // triggers, which quietly dropped attributes from the element under test.
    IRenderedComponent<ContainerFragment> RenderForm(EditContext editContext, RenderFragment inner) =>
        Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => formContent =>
            {
                formContent.OpenComponent<DataAnnotationsValidator>(0);
                formContent.CloseComponent();
                formContent.OpenComponent<CascadingValue<FormOptions>>(1);
                formContent.AddAttribute(2, "Value", new FormOptions());
                formContent.AddAttribute(3, "ChildContent", inner);
                formContent.CloseComponent();
            }));
            b.CloseComponent();
        });

    // PersonModel.Name is [Required] (so the library HAS an aria-required opinion) and fails
    // validation while empty (so it can reach the invalid branch). Username is neither.
    IRenderedComponent<ContainerFragment> RenderString(
        PersonModel model, bool required, Action<RenderTreeBuilder, int> splat, bool validate = false)
    {
        var editContext = new EditContext(model);
        Expression<Func<string>> field = required ? () => model.Name : () => model.Username;
        var cut = RenderForm(editContext, content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", required ? model.Name : model.Username);
            content.AddAttribute(2, "ValueExpression", field);
            splat(content, 3);
            content.CloseComponent();
        });
        if (validate) cut.InvokeAsync(() => editContext.Validate());
        return cut;
    }

    IRenderedComponent<ContainerFragment> RenderNumber(
        PersonModel model, bool required, Action<RenderTreeBuilder, int> splat, bool validate = false)
    {
        var editContext = new EditContext(model);
        var cut = RenderForm(editContext, content =>
        {
            if (required)
            {
                Expression<Func<int?>> field = () => model.Age;
                content.OpenComponent<EditNumber<int?>>(0);
                content.AddAttribute(1, "Value", model.Age);
                content.AddAttribute(2, "ValueExpression", field);
            }
            else
            {
                Expression<Func<decimal?>> field = () => model.Price;
                content.OpenComponent<EditNumber<decimal?>>(0);
                content.AddAttribute(1, "Value", model.Price);
                content.AddAttribute(2, "ValueExpression", field);
            }
            splat(content, 3);
            content.CloseComponent();
        });
        if (validate) cut.InvokeAsync(() => editContext.Validate());
        return cut;
    }

    static Action<RenderTreeBuilder, int> Splat(params (string Name, string Value)[] attributes) =>
        (b, seq) =>
        {
            foreach (var (name, value) in attributes) b.AddAttribute(seq++, name, value);
        };

    // ───────────────────────── disabled ─────────────────────────

    [Fact]
    public void EditString_keeps_a_consumer_splatted_disabled_while_the_library_is_not_disabled()
    {
        // The sharp case. The consumer asked for a non-editable field; the erased frame gave them an
        // editable one, silently, with the control's own IsDisabled sitting at its default the whole
        // time. Nothing about the rendered page said why.
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false,
            Splat(("disabled", "disabled")));

        Assert.True(cut.Find("input.edit-string-input").HasAttribute("disabled"));
    }

    [Fact]
    public void EditString_library_disabled_wins_and_renders_once()
    {
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false, (b, seq) =>
        {
            b.AddAttribute(seq, "disabled", "disabled");
            b.AddAttribute(seq + 1, "IsDisabled", true);
        });

        var input = cut.Find("input.edit-string-input");
        Assert.True(input.HasAttribute("disabled"));
        Assert.Single(input.Attributes, a => a.Name == "disabled");
    }

    [Fact]
    public void EditString_renders_no_disabled_attribute_when_neither_side_asks_for_one()
    {
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false, (_, _) => { });

        Assert.False(cut.Find("input.edit-string-input").HasAttribute("disabled"));
    }

    [Fact]
    public void EditNumber_keeps_a_consumer_splatted_disabled_while_the_library_is_not_disabled()
    {
        var cut = RenderNumber(new PersonModel { Price = 5m }, required: false,
            Splat(("disabled", "disabled")));

        Assert.True(cut.Find("input.edit-number-input").HasAttribute("disabled"));
    }

    // ───────────────────────── aria-required ─────────────────────────

    [Fact]
    public void EditString_keeps_a_consumer_splatted_aria_required_on_an_optional_field()
    {
        // A consumer doing their own required-ness wiring (a FluentValidation rule the library can't
        // see, a conditional the model doesn't express) had it deleted on every optional field.
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false,
            Splat(("aria-required", "true")));

        Assert.Equal("true", cut.Find("input.edit-string-input").GetAttribute("aria-required"));
    }

    [Fact]
    public void EditString_library_aria_required_wins_on_a_required_field()
    {
        var cut = RenderString(new PersonModel { Name = "Alice" }, required: true,
            Splat(("aria-required", "false")));

        Assert.Equal("true", cut.Find("input.edit-string-input").GetAttribute("aria-required"));
    }

    [Fact]
    public void EditNumber_keeps_a_consumer_splatted_aria_required_on_an_optional_field()
    {
        var cut = RenderNumber(new PersonModel { Price = 5m }, required: false,
            Splat(("aria-required", "true")));

        Assert.Equal("true", cut.Find("input.edit-number-input").GetAttribute("aria-required"));
    }

    [Fact]
    public void EditNumber_library_aria_required_wins_on_a_required_field()
    {
        var cut = RenderNumber(new PersonModel { Age = 30 }, required: true,
            Splat(("aria-required", "false")));

        Assert.Equal("true", cut.Find("input.edit-number-input").GetAttribute("aria-required"));
    }

    // ───────────────────────── aria-errormessage ─────────────────────────

    [Fact]
    public void EditString_keeps_a_consumer_splatted_aria_errormessage_while_the_field_is_valid()
    {
        var cut = RenderString(new PersonModel { Name = "Alice" }, required: true,
            Splat(("aria-errormessage", "my-own-error")), validate: true);

        Assert.Equal("my-own-error", cut.Find("input.edit-string-input").GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void EditString_library_aria_errormessage_wins_once_the_field_is_invalid()
    {
        var cut = RenderString(new PersonModel { Name = "" }, required: true, // [Required] fails
            Splat(("aria-errormessage", "my-own-error")), validate: true);

        var input = cut.Find("input.edit-string-input");
        Assert.Equal("error-msg-Name", input.GetAttribute("aria-errormessage"));
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
    }

    [Fact]
    public void EditNumber_keeps_a_consumer_splatted_aria_errormessage_while_the_field_is_valid()
    {
        var cut = RenderNumber(new PersonModel { Age = 30 }, required: true,
            Splat(("aria-errormessage", "my-own-error")), validate: true);

        Assert.Equal("my-own-error", cut.Find("input.edit-number-input").GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void EditNumber_library_aria_errormessage_wins_once_the_field_is_invalid()
    {
        var cut = RenderNumber(new PersonModel { Age = null }, required: true, // [Required] fails
            Splat(("aria-errormessage", "my-own-error")), validate: true);

        var input = cut.Find("input.edit-number-input");
        Assert.Equal("error-msg-Age", input.GetAttribute("aria-errormessage"));
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
    }

    // ───────────────────────── aria-invalid: the FRAMEWORK owns it ─────────────────────────

    [Fact]
    public void Aria_invalid_is_owned_by_the_frameworks_InputBase_not_by_this_library()
    {
        // The one attribute in the table this library cannot hand back to the consumer, and the
        // reason is upstream: InputBase<TValue>.UpdateAdditionalValidationAttributes() edits
        // AdditionalAttributes itself before any of this control's code runs -- it INSERTS
        // aria-invalid="true" when the field has validation messages and REMOVES the key outright
        // when it does not, precisely so an Input* component can't announce a valid field as invalid.
        // A consumer's splatted aria-invalid on a valid field is therefore deleted by the framework,
        // not by the null-attribute-frame trap the rest of this file is about, and moving the
        // library's own contribution into the splat dictionary does not (and should not) change that.
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false,
            Splat(("aria-invalid", "true"), ("data-probe", "1")));

        var input = cut.Find("input.edit-string-input");
        Assert.False(input.HasAttribute("aria-invalid"));
        Assert.Equal("1", input.GetAttribute("data-probe")); // ...and only that one key is touched
    }

    [Fact]
    public void The_library_aria_invalid_still_wins_over_a_consumers_on_an_invalid_field()
    {
        // Where the library IS stricter than the framework: InputBase leaves a consumer's existing
        // aria-invalid alone ("do not overwrite the attribute value"), so a splatted "false" would
        // survive an actual validation failure. EditorStateAttributes overwrites it -- an invalid
        // field announcing aria-invalid="false" is the one outcome that cannot be allowed, and this
        // matches what the old explicit attribute frame did.
        var cut = RenderString(new PersonModel { Name = "" }, required: true, // [Required] fails
            Splat(("aria-invalid", "false")), validate: true);

        Assert.Equal("true", cut.Find("input.edit-string-input").GetAttribute("aria-invalid"));
    }

    // ───────────────────────── type: library-owned outright ─────────────────────────

    [Fact]
    public void EditString_drops_a_consumer_splatted_type_even_when_it_has_no_type_of_its_own()
    {
        // The DELIBERATE exception to the rule above, and the one attribute in this file that is NOT
        // "consumer survives when the library is silent". `type` is not a description of an unchanged
        // element the way disabled/aria-* are -- it changes what the element IS, and with it what
        // @bind-value, the parse path, and the shell's layout are talking to. Letting it through would
        // also let a consumer manufacture a HALF password field: masked pixels with none of the
        // control's secret handling (no reveal toggle, no bullet-masked read-only row, no redacted
        // ShowBoundValues echo, no "new-password" autocomplete, no Suggestions suppression). The
        // legitimate need behind `type="email"`/`"tel"` -- a mobile soft keyboard -- is already served
        // by two supported channels that do not change the element's kind: `inputmode`, which splats
        // through untouched (see OptionIdUniquenessAndSplatTests), and the `Autocomplete` parameter /
        // [Autocomplete] / the property-name inference.
        var cut = RenderString(new PersonModel { Username = "abc" }, required: false,
            Splat(("type", "email")));

        // No type attribute at all -> the native text default, which is what the library intends here.
        Assert.False(cut.Find("input.edit-string-input").HasAttribute("type"));
    }

    [Fact]
    public void A_splatted_type_can_never_unmask_a_password_field()
    {
        // Non-negotiable, and structurally true rather than conditionally true: the library writes
        // `type` unconditionally, so there is no "library has no opinion" branch for a splat to slip
        // through.
        var cut = RenderString(new PersonModel { Username = "hunter2" }, required: false, (b, seq) =>
        {
            b.AddAttribute(seq, "type", "text");
            b.AddAttribute(seq + 1, "IsPassword", true);
        });

        Assert.Equal("password", cut.Find("input.edit-string-input").GetAttribute("type"));
    }

    [Fact]
    public void EditNumber_type_stays_number_when_the_consumer_splats_one()
    {
        var cut = RenderNumber(new PersonModel { Price = 5m }, required: false,
            Splat(("type", "text")));

        Assert.Equal("number", cut.Find("input.edit-number-input").GetAttribute("type"));
    }
}
