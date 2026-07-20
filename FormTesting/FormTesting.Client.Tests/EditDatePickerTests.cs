using System.Globalization;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDatePicker"/> — the form-control wrapper around the
/// <see cref="DatePicker"/> UI-kit calendar dropdown. These cover the layer this control adds over
/// DatePicker itself (EditContext binding, validation, label, read-only view, parameter forwarding);
/// DatePicker's own open/close/navigation/keyboard behavior is already covered by
/// <c>DatePickerTests</c> and the JS-owned parts by <c>DatePickerE2ETests</c>.
/// </summary>
public class EditDatePickerTests : TestContext
{
    public EditDatePickerTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static void Open(IRenderedFragment cut) => cut.Find(".wss-picker-input").Click();

    // The in-month day button for the given day number (skips the leading adjacent-month cells).
    static IElement Day(IRenderedFragment cut, int dayNumber) =>
        cut.FindAll(".wss-picker-day")
            .First(b => !b.ClassList.Contains("wss-picker-day-outside") &&
                        b.TextContent == dayNumber.ToString("00", CultureInfo.InvariantCulture));

    [Fact]
    public void Day_click_updates_the_bound_model_and_notifies_the_field()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.BirthDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(2020, 3, 20), model.BirthDate);
    }

    [Fact]
    public void Commit_notifies_the_EditContext_field_changed_event()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        var editContext = new EditContext(model);
        var notifiedFields = new List<string>();
        editContext.OnFieldChanged += (_, e) => notifiedFields.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> field = () => model.BirthDate;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditDatePicker>(0);
                content.AddAttribute(1, "Value", model.BirthDate);
                content.AddAttribute(2, "ValueExpression", field);
                content.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.BirthDate = v));
                content.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        Open(cut);
        Day(cut, 20).Click();

        Assert.Contains("BirthDate", notifiedFields);
    }

    [Fact]
    public void Required_attribute_flows_to_the_star_and_aria_required()
    {
        var model = new PersonModel(); // BirthDate is [Required]
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        // aria-required reaches the picker's actual <input> via DatePicker's AriaRequired parameter
        // (the same forwarding shape as EditSelectSearch -> Select).
        Assert.Equal("true", cut.Find(".wss-picker-input-date").GetAttribute("aria-required"));
    }

    [Fact]
    public void Invalid_field_marks_the_pickers_input_aria_invalid()
    {
        var model = new PersonModel(); // BirthDate empty -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditDatePicker>(1);
                content.AddAttribute(2, "Value", model.BirthDate);
                content.AddAttribute(3, "ValueExpression", field);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        // aria-invalid/aria-errormessage land on the picker's actual <input> via DatePicker's
        // dedicated Aria* parameters — full parity with EditDate's native <input>.
        var input = cut.Find(".wss-picker-input-date");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.StartsWith("error-msg-", input.GetAttribute("aria-errormessage"));
        Assert.Contains("required", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Label_auto_generates_from_the_property_name()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Contains("Birth Date", cut.Find("label.edit-label").TextContent);
    }

    [Fact]
    public void Input_label_defaults_to_the_resolved_field_label_so_it_matches_the_visible_label()
    {
        // DatePicker's own aria-label wins the accessible-name computation over label[for] (per the
        // AccName spec) — EditDatePicker defaults InputLabel to the resolved field label instead of
        // DatePicker's generic "Date" default so the two never diverge.
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("Birth Date", cut.Find(".wss-picker-input-date").GetAttribute("aria-label"));
    }

    [Fact]
    public void Label_param_flows_to_the_pickers_aria_label_when_set()
    {
        // Label overrides the visible FormLabel text; EffectiveInputLabel must track it too so the
        // aria-label (which wins the accessible-name computation over label[for]) never diverges from
        // what's on screen (WCAG 2.5.3 Label in Name).
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Label", "Date of Birth");
            b.CloseComponent();
        }));

        Assert.Contains("Date of Birth", cut.Find("label.edit-label").TextContent);
        Assert.Equal("Date of Birth", cut.Find(".wss-picker-input-date").GetAttribute("aria-label"));
    }

    [Fact]
    public void InputLabel_overrides_Label_for_the_pickers_aria_label()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Label", "Date of Birth");
            b.AddAttribute(4, "InputLabel", "Custom accessible name");
            b.CloseComponent();
        }));

        // The visible label still shows Label; only the aria-label takes the explicit InputLabel override.
        Assert.Contains("Date of Birth", cut.Find("label.edit-label").TextContent);
        Assert.Equal("Custom accessible name", cut.Find(".wss-picker-input-date").GetAttribute("aria-label"));
    }

    [Fact]
    public void Label_for_resolves_to_the_pickers_actual_input_id()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var label = cut.Find("label.edit-label");
        var input = cut.Find(".wss-picker-input-date");
        Assert.Equal(input.Id, label.GetAttribute("for"));
    }

    [Fact]
    public void Read_only_mode_renders_ReadOnlyValue_formatted_with_DateFormat()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "DateFormat", "yyyy-MM-dd");
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2020-03-05", cut.Find(".edit-readonly-value").TextContent);
        Assert.Empty(cut.FindAll(".wss-picker"));
    }

    [Fact]
    public void Read_only_mode_renders_gregorian_years_under_non_gregorian_cultures()
    {
        // Same Gregorian contract as the picker itself: th-TH (Buddhist calendar, year + 543) must
        // not make read-only mode show 2563 while edit mode's picker shows 2020 for the same value.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
            Expression<Func<DateTime?>> field = () => model.BirthDate;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditDatePicker>(0);
                b.AddAttribute(1, "Value", model.BirthDate);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(3, "DateFormat", "yyyy-MM-dd");
                b.AddAttribute(4, "IsEditMode", false);
                b.CloseComponent();
            }));

            var text = cut.Find(".edit-readonly-value").TextContent;
            Assert.Contains("2020-03-05", text);
            Assert.DoesNotContain("2563", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Field_registers_with_FormOptions()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        var formOptions = new FormOptions();
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        _ = Render(builder =>
        {
            builder.OpenComponent<CascadingValue<FormOptions>>(0);
            builder.AddAttribute(1, "Value", formOptions);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(b =>
            {
                b.OpenComponent<EditForm>(0);
                b.AddAttribute(1, "Model", model);
                b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
                {
                    content.OpenComponent<EditDatePicker>(0);
                    content.AddAttribute(1, "Value", model.BirthDate);
                    content.AddAttribute(2, "ValueExpression", field);
                    content.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "BirthDate");
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_hides_when_the_value_is_default_DateTime()
    {
        // default(DateTime) (0001-01-01) must count as semantically empty for the hiding contract,
        // the same way EditDate<T>'s IsValueDefault override already treats it (see HidingModeTests'
        // EditDate coverage for the native-input case). EditDatePicker previously fell through to
        // EditControlBase's plain EqualityComparer check, which treats a non-null default DateTime
        // as NOT default.
        var model = new PersonModel { BirthDate = default(DateTime) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_shows_a_real_date()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void Min_max_and_placeholder_forward_to_the_inner_picker()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 15) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Min", new DateTime(2020, 1, 10));
            b.AddAttribute(4, "Max", new DateTime(2020, 1, 20));
            b.AddAttribute(5, "Placeholder", "Pick a birthday");
            b.CloseComponent();
        }));

        Assert.Equal("Pick a birthday", cut.Find(".wss-picker-input-date").GetAttribute("placeholder"));

        Open(cut);
        Assert.True(Day(cut, 9).HasAttribute("disabled"));
        Assert.False(Day(cut, 10).HasAttribute("disabled"));
        Assert.True(Day(cut, 21).HasAttribute("disabled"));
    }
}
