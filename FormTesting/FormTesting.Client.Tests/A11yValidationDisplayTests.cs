using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the validation-summary and read-only-display accessibility fixes from the follow-up audit
/// wave (INF-3, INF-4, LST-2, R5, TXT-5, TXT-1):
/// <list type="bullet">
///   <item><description>INF-3: <c>ValidationView</c>'s summary section is a <c>role="status"</c> live
///   region, so a failed submit is announced without needing the user to already know to navigate
///   there.</description></item>
///   <item><description>INF-4: <c>ValidationView</c> rewrites each summary message through the same
///   label-resolution path <c>FieldValidationDisplay</c> uses, instead of leaving DataAnnotations' raw
///   member-name text (which never reads <c>[DisplayName]</c>) disagreeing with the control's own
///   label.</description></item>
///   <item><description>LST-2 / R5 / TXT-5: <c>ReadOnlyValue</c>'s empty-value fallback is real, visible,
///   assistive-technology-reachable text (not <c>aria-hidden</c> + <c>visibility:hidden</c>),
///   configurable via <c>EmptyText</c>, and the component now accepts an <c>AriaDescribedBy</c>
///   parameter. <c>EditDisplay</c>'s equivalent copy is covered by <c>EditDisplayTests</c> instead,
///   since it hand-builds its own read-only div rather than reusing this component.</description></item>
///   <item><description>TXT-1: <see cref="EditControlBase{TValue}"/> defers a
///   <see cref="HidingMode.WhenNull"/>/<see cref="HidingMode.WhenNullOrDefault"/> hide while the
///   editor holds focus (tracked via onfocus/onblur handlers injected into
///   <c>AdditionalAttributes</c> -- no JS involved), instead of unmounting the focused element out
///   from under the user. Because the mechanism is pure Blazor event wiring, bUnit's synthetic
///   focus/blur dispatch (<c>IElement.Focus()</c>/<c>Blur()</c> — see <c>DateRangePickerTests</c> for
///   the same technique) can exercise BOTH halves here, contrary to the general rule that a
///   focus-observing behavior needs a real browser: nothing here queries actual DOM focus state or
///   calls into JS, so there's nothing for bUnit to be unable to see.</description></item>
/// </list>
/// </summary>
public class A11yValidationDisplayTests : BunitContext
{
    // ───────────────────────── INF-3 / INF-4: ValidationView's summary ─────────────────────────

    [Fact]
    public void ValidationView_summary_section_is_a_status_live_region()
    {
        var model = new PersonModel(); // Name = "" -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
            content.OpenComponent<ValidationView>(4);
            content.CloseComponent();
        }));

        Assert.Equal("status", cut.Find(".validation-summary").GetAttribute("role"));
    }

    [Fact]
    public void ValidationView_summary_message_uses_the_resolved_label_not_the_raw_member_name()
    {
        // PersonModel.Name carries [DisplayName("Full Name")] -- DataAnnotations itself never reads
        // that attribute (only [Display(Name=...)]), so its own raw message says "The Name field is
        // required." FieldValidationDisplay already rewrites its OWN inline copy to "Full Name is
        // required."; the summary link used to disagree, reading the raw, un-rewritten text for the
        // very same field.
        var model = new PersonModel();
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(RenderValidatedForm(editContext, new FormOptions(), content =>
        {
            content.OpenComponent<EditString>(0);
            content.AddAttribute(1, "Value", model.Name);
            content.AddAttribute(2, "ValueExpression", field);
            content.CloseComponent();
            content.OpenComponent<ValidationView>(4);
            content.CloseComponent();
        }));

        cut.InvokeAsync(() => editContext.Validate());

        var link = cut.Find("a.validation-summary-message");
        Assert.Equal("Full Name is required.", link.TextContent);
    }

    [Fact]
    public void ValidationView_falls_back_to_the_raw_message_for_a_field_with_no_registered_metadata()
    {
        // FormOptions.FieldMetadata is only populated by EditControlBase<TValue>-derived scalar
        // controls (see its RefreshAriaState) -- a field registered by hand (no owning control at all,
        // mirroring how list-bound controls/EditRadio/EditDateRange register today) has no entry, so
        // the summary must fall back to the message verbatim rather than guessing at a label.
        var model = new PersonModel();
        var editContext = new EditContext(model);
        Expression<Func<string>> field = () => model.Name;
        var fieldIdentifier = FieldIdentifier.Create(field);
        var formOptions = new FormOptions();
        formOptions.RegisterField(fieldIdentifier, "Name");

        // CascadingValue<T>.ChildContent is a plain RenderFragment, not RenderFragment<T> — the value
        // reaches ValidationView by cascade, not as a fragment argument.
        var cut = Render<CascadingValue<EditContext>>(ps => ps
            .Add(c => c.Value, editContext)
            .Add(c => c.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<CascadingValue<FormOptions>>(0);
                b.AddAttribute(1, "Value", formOptions);
                b.AddAttribute(2, "ChildContent", (RenderFragment)(inner =>
                {
                    inner.OpenComponent<ValidationView>(0);
                    inner.CloseComponent();
                }));
                b.CloseComponent();
            })));

        var store = new ValidationMessageStore(editContext);
        cut.InvokeAsync(() =>
        {
            store.Add(fieldIdentifier, "The Name field is required.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Equal("The Name field is required.", cut.Find("a.validation-summary-message").TextContent);
    }

    // ───────────────────────────── LST-2 / TXT-5: ReadOnlyValue ─────────────────────────────

    [Fact]
    public void ReadOnlyValue_empty_fallback_is_real_visible_text_reachable_by_assistive_technology()
    {
        var cut = Render<ReadOnlyValue>(p => p.Add(r => r.Id, "Field1"));

        var placeholder = cut.Find(".edit-readonly-value span");
        Assert.False(placeholder.HasAttribute("aria-hidden"));
        Assert.DoesNotContain("hidden", placeholder.GetAttribute("style") ?? "");
        Assert.Equal("Not Set", placeholder.TextContent);
    }

    [Fact]
    public void ReadOnlyValue_EmptyText_parameter_overrides_the_default_fallback()
    {
        var cut = Render<ReadOnlyValue>(p => p
            .Add(r => r.Id, "Field1")
            .Add(r => r.EmptyText, "None recorded"));

        Assert.Equal("None recorded", cut.Find(".edit-readonly-value span").TextContent);
    }

    [Fact]
    public void ReadOnlyValue_renders_no_placeholder_when_Text_is_set()
    {
        var cut = Render<ReadOnlyValue>(p => p
            .Add(r => r.Id, "Field1")
            .Add(r => r.Text, "Alice"));

        Assert.Empty(cut.FindAll(".edit-readonly-value span"));
        Assert.Equal("Alice", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void ReadOnlyValue_renders_the_AriaDescribedBy_parameter_when_set()
    {
        var cut = Render<ReadOnlyValue>(p => p
            .Add(r => r.Id, "Field1")
            .Add(r => r.Text, "Alice")
            .Add(r => r.AriaDescribedBy, "desc-Field1 error-msg-Field1"));

        Assert.Equal("desc-Field1 error-msg-Field1", cut.Find(".edit-readonly-value").GetAttribute("aria-describedby"));
    }

    [Fact]
    public void ReadOnlyValue_omits_aria_describedby_when_unset()
    {
        var cut = Render<ReadOnlyValue>(p => p.Add(r => r.Id, "Field1").Add(r => r.Text, "Alice"));

        Assert.False(cut.Find(".edit-readonly-value").HasAttribute("aria-describedby"));
    }

    // ───────────────────────────── TXT-1: focus preservation under value-driven hiding ─────────────────────────────

    class OptionalTextModel
    {
        public string? Text { get; set; }
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_defers_hiding_while_the_editor_holds_focus_then_hides_on_blur()
    {
        // Backspacing a WhenNullOrDefault-hidden field to empty used to unmount the whole control --
        // including the focused <input> -- on the very next render, dropping focus to <body>. The base
        // class now defers the hide while the editor's own onfocus/onblur (wired through
        // AdditionalAttributes -- no JS) say it's still focused, and finally hides once blur fires.
        var model = new OptionalTextModel { Text = "Alice" };
        Expression<Func<string?>> field = () => model.Text;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        var input = cut.Find("input.edit-string-input");
        input.Focus();
        input.Input(""); // the user's own backspace-to-empty, committed per keystroke (UpdateTrigger.Input)

        Assert.NotEmpty(cut.FindAll(".edit-control-wrapper"));  // deferred -- still focused
        Assert.NotNull(cut.Find("input.edit-string-input"));   // same editor, not torn down mid-edit

        cut.Find("input.edit-string-input").Blur();

        Assert.Empty(cut.FindAll(".edit-control-wrapper")); // now safe -- focus already moved on its own
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_still_hides_immediately_when_the_editor_never_had_focus()
    {
        // The other half of the gate: a value that arrives already empty/default with no user
        // interaction at all (e.g. the bound model reset by a parent) must still hide exactly as
        // before -- the deferral is keyed off the editor's OWN focus state, not a blanket exemption.
        var model = new OptionalTextModel { Text = null };
        Expression<Func<string?>> field = () => model.Text;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }
}
