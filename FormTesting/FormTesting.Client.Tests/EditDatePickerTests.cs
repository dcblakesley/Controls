using System.Globalization;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDatePicker{T}"/> — the form-control wrapper around the
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

    // Scoped to this file's non-DateTime?/non-nullable coverage -- PersonModel.BirthDate is
    // DateTime? only.
    class DateOnlyModel { public DateOnly ShipDate { get; set; } }
    class NullableDateOnlyModel { public DateOnly? ShipDate { get; set; } }
    class NonNullableDateTimeModel { public DateTime ShipDate { get; set; } }
    class DateTimeOffsetModel { public DateTimeOffset ShipDate { get; set; } }
    class NullableDateTimeOffsetModel { public DateTimeOffset? ShipDate { get; set; } }
    class TimeOnlyModel { public TimeOnly ShipTime { get; set; } }
    class NullableTimeOnlyModel { public TimeOnly? ShipTime { get; set; } }
    class UnsupportedTypeModel { public int ShipDate { get; set; } }

    // The three hour/minute/second selects rendered by Type="Time"/"DateTimeLocal" -- mirrors
    // DatePickerTests' TimeSelects helper.
    static IReadOnlyList<IElement> TimeSelects(IRenderedFragment cut) =>
        cut.FindAll(".wss-picker-time-row select");

    [Fact]
    public void Day_click_updates_the_bound_model_and_notifies_the_field()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
                content.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
                content.OpenComponent<EditDatePicker<DateTime?>>(1);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
                b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
                    content.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
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

    // EditDatePicker<T> generalizes beyond the original DateTime?-only binding -- these cover the
    // conversion bridge to/from the inner DatePicker's DateTime? (PickerValue/OnValueChanged/FromPickerValue).

    [Fact]
    public void DateOnly_binding_round_trips_through_the_picker()
    {
        var model = new DateOnlyModel { ShipDate = new DateOnly(2020, 3, 5) };
        Expression<Func<DateOnly>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateOnly>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateOnly(2020, 3, 20), model.ShipDate);
    }

    [Fact]
    public void Nullable_DateOnly_binding_round_trips_and_clears()
    {
        var model = new NullableDateOnlyModel { ShipDate = new DateOnly(2020, 3, 5) };
        Expression<Func<DateOnly?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateOnly?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateOnly?>(this, v => model.ShipDate = v));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-clear").Click();

        Assert.Null(model.ShipDate);
    }

    [Fact]
    public void NonNullable_DateTime_binding_round_trips_through_the_picker()
    {
        var model = new NonNullableDateTimeModel { ShipDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTime(2020, 3, 20), model.ShipDate);
    }

    [Fact]
    public void NonNullable_DateTime_default_counts_as_semantically_empty_for_hiding()
    {
        // Mirrors the DateTime? HidingMode_WhenNullOrDefault test above -- a non-nullable binding can
        // never be null, so this is the only way to reach IsValueDefault's "DateTime dt => dt == default"
        // branch for a non-nullable T.
        var model = new NonNullableDateTimeModel { ShipDate = default };
        Expression<Func<DateTime>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void Nullable_DateOnly_day_click_round_trips_the_bound_model()
    {
        // Nullable_DateOnly_binding_round_trips_and_clears (above) only exercises the clear path --
        // this covers the actual day-click round trip for the same bound shape.
        var model = new NullableDateOnlyModel { ShipDate = new DateOnly(2020, 3, 5) };
        Expression<Func<DateOnly?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateOnly?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateOnly?>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateOnly(2020, 3, 20), model.ShipDate);
    }

    [Fact]
    public void DateTimeOffset_binding_round_trips_through_the_picker()
    {
        var model = new DateTimeOffsetModel { ShipDate = new DateTimeOffset(2020, 3, 5, 0, 0, 0, TimeSpan.Zero) };
        Expression<Func<DateTimeOffset>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTimeOffset>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTimeOffset>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        // FromPickerValue assumes the local offset for the picker's Unspecified-Kind value -- the
        // commit is the LOCAL day-20 instant, not day 20 at the original UTC offset.
        Assert.Equal(new DateTimeOffset(new DateTime(2020, 3, 20)), model.ShipDate);
    }

    [Fact]
    public void NonNullable_DateTimeOffset_default_counts_as_semantically_empty_for_hiding()
    {
        var model = new DateTimeOffsetModel { ShipDate = default };
        Expression<Func<DateTimeOffset>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTimeOffset>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void Nullable_DateTimeOffset_day_click_round_trips_the_bound_model()
    {
        var model = new NullableDateTimeOffsetModel { ShipDate = new DateTimeOffset(2020, 3, 5, 0, 0, 0, TimeSpan.Zero) };
        Expression<Func<DateTimeOffset?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTimeOffset?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTimeOffset?>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 20).Click();

        Assert.Equal(new DateTimeOffset(new DateTime(2020, 3, 20)), model.ShipDate);
    }

    [Fact]
    public void Nullable_DateTimeOffset_clears_to_null()
    {
        var model = new NullableDateTimeOffsetModel { ShipDate = new DateTimeOffset(2020, 3, 5, 0, 0, 0, TimeSpan.Zero) };
        Expression<Func<DateTimeOffset?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTimeOffset?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTimeOffset?>(this, v => model.ShipDate = v));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-clear").Click();

        Assert.Null(model.ShipDate);
    }

    [Fact]
    public void TimeOnly_binding_round_trips_through_the_picker()
    {
        var model = new TimeOnlyModel { ShipTime = new TimeOnly(9, 30) };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<TimeOnly>(this, v => model.ShipTime = v));
            b.AddAttribute(4, "Type", InputDateType.Time);
            b.CloseComponent();
        }));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // hour select

        Assert.Equal(new TimeOnly(13, 30), model.ShipTime);
    }

    [Fact]
    public void NonNullable_TimeOnly_default_counts_as_semantically_empty_for_hiding()
    {
        var model = new TimeOnlyModel { ShipTime = default };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void Nullable_TimeOnly_binding_round_trips_and_clears()
    {
        var model = new NullableTimeOnlyModel { ShipTime = new TimeOnly(9, 30) };
        Expression<Func<TimeOnly?>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly?>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<TimeOnly?>(this, v => model.ShipTime = v));
            b.AddAttribute(4, "Type", InputDateType.Time);
            b.CloseComponent();
        }));

        Open(cut);
        TimeSelects(cut)[0].Change("13"); // hour select

        Assert.Equal(new TimeOnly(13, 30), model.ShipTime);

        cut.Find(".wss-picker-clear").Click();

        Assert.Null(model.ShipTime);
    }

    // ----- Type forwarding to the picker's Mode ------------------------------------------------

    [Fact]
    public void Type_Month_renders_the_pickers_month_grid_instead_of_the_day_calendar()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Month);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(cut.FindAll(".wss-picker-month-btn"));
        Assert.Empty(cut.FindAll(".wss-picker-day"));
    }

    [Fact]
    public void Type_Time_renders_the_pickers_time_row_instead_of_the_day_calendar()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(TimeSelects(cut));
        Assert.NotEmpty(cut.FindAll(".wss-picker-ok"));
        Assert.Empty(cut.FindAll(".wss-picker-day"));
    }

    [Fact]
    public void Type_DateTimeLocal_renders_the_day_calendar_plus_the_time_row()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.DateTimeLocal);
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(cut.FindAll(".wss-picker-day"));
        Assert.NotEmpty(TimeSelects(cut));
    }

    [Fact]
    public void Month_and_time_labels_forward_to_the_pickers_nav_buttons_and_selects()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Month);
            b.AddAttribute(4, "PrevYearLabel", "Custom prev year");
            b.AddAttribute(5, "NextYearLabel", "Custom next year");
            b.CloseComponent();
        }));

        Open(cut);
        var navLabels = cut.FindAll(".wss-picker-nav").Select(n => n.GetAttribute("aria-label"));
        Assert.Contains("Custom prev year", navLabels);
        Assert.Contains("Custom next year", navLabels);
    }

    [Fact]
    public void Time_labels_and_OkText_forward_to_the_pickers_selects_and_button()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "HourSelectLabel", "Custom hour");
            b.AddAttribute(5, "MinuteSelectLabel", "Custom minute");
            b.AddAttribute(6, "SecondSelectLabel", "Custom second");
            b.AddAttribute(7, "OkText", "Done");
            b.CloseComponent();
        }));

        Open(cut);
        var selects = TimeSelects(cut);
        Assert.Equal("Custom hour", selects[0].GetAttribute("aria-label"));
        Assert.Equal("Custom minute", selects[1].GetAttribute("aria-label"));
        Assert.Equal("Custom second", selects[2].GetAttribute("aria-label"));
        Assert.Equal("Done", cut.Find(".wss-picker-ok").TextContent);
    }

    // ----- Read-only display: effective DateFormat default by Type ----------------------------

    [Fact]
    public void Read_only_mode_defaults_to_HHmmss_for_Type_Time()
    {
        var model = new TimeOnlyModel { ShipTime = new TimeOnly(13, 45, 30) };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("13:45:30", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Read_only_mode_defaults_to_MMyyyy_for_Type_Month()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Month);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("03-2020", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Read_only_mode_defaults_to_MMddyyyyHHmmss_for_Type_DateTimeLocal()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5, 13, 45, 30) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.DateTimeLocal);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("03-05-2020 13:45:30", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Explicit_DateFormat_still_wins_over_the_Type_default()
    {
        var model = new TimeOnlyModel { ShipTime = new TimeOnly(13, 45, 30) };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "DateFormat", "h:mm tt");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("1:45 PM", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Unsupported_bound_type_throws_on_render()
    {
        var model = new UnsupportedTypeModel { ShipDate = 5 };
        Expression<Func<int>> field = () => model.ShipDate;

        Assert.Throws<NotSupportedException>(() => Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<int>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        })));
    }

    // ----- Mode override (phase 2) -------------------------------------------------------------

    [Fact]
    public void Mode_Week_overrides_Type_and_renders_week_rows()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Mode", DatePickerMode.Week);
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(cut.FindAll(".wss-picker-week-row"));
        Assert.Empty(cut.FindAll(".wss-picker-grid")); // the flat 42-cell grid is replaced by rows
    }

    [Fact]
    public void Mode_Year_overrides_Type_and_renders_the_decade_header()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Mode", DatePickerMode.Year);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(cut.FindAll(".wss-picker-decade-label"));
        Assert.Equal(12, cut.FindAll(".wss-picker-month-btn").Count); // 10 decade years + 2 dimmed
    }

    [Fact]
    public void Type_alone_still_maps_to_Date_mode_when_Mode_is_unset()
    {
        // Regression guard: Mode defaults to null, so an EditDatePicker that never sets it (every
        // other test in this file) must keep resolving Type -> PickerMode exactly as before.
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.NotEmpty(cut.FindAll(".wss-picker-day"));
        Assert.Empty(cut.FindAll(".wss-picker-week-row"));
        Assert.Empty(cut.FindAll(".wss-picker-decade-label"));
    }

    [Fact]
    public void Mode_Week_DateOnly_binding_commits_the_week_start()
    {
        // March 2020: the 1st is a Sunday, so the Sunday-Saturday week containing March 5 (Thu) is
        // March 1-7. Clicking March 7 (Sat) must commit March 1 -- the week START, not the click.
        var model = new NullableDateOnlyModel { ShipDate = new DateOnly(2020, 3, 5) };
        Expression<Func<DateOnly?>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateOnly?>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateOnly?>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "Mode", DatePickerMode.Week);
            b.AddAttribute(5, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);
        Day(cut, 7).Click();

        Assert.Equal(new DateOnly(2020, 3, 1), model.ShipDate);
    }

    // ----- Forwarded phase-2 parameters ---------------------------------------------------------

    [Fact]
    public void DisabledDate_forwards_and_disables_a_day_cell()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) }; // Thursday
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "DisabledDate", (Func<DateTime, bool>)(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.True(Day(cut, 7).HasAttribute("disabled")); // Saturday, same week as the 5th
        Assert.False(Day(cut, 5).HasAttribute("disabled"));
    }

    [Fact]
    public void Use12Hours_forwards_and_renders_the_period_select()
    {
        var model = new TimeOnlyModel { ShipTime = new TimeOnly(14, 0) };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "Use12Hours", true);
            b.CloseComponent();
        }));

        Open(cut);

        var selects = TimeSelects(cut);
        Assert.Equal(4, selects.Count); // hour, minute, second, period
        Assert.Equal("AM/PM", selects[3].GetAttribute("aria-label"));
    }

    [Fact]
    public void ShowToday_forwards_and_renders_the_today_link()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ShowToday", true);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.Equal("Today", cut.Find(".wss-picker-today-btn").TextContent);
    }

    // ----- Read-only display: Year/Quarter/Week and 12-hour Time -------------------------------

    [Fact]
    public void Read_only_mode_displays_just_the_year_for_Mode_Year()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Mode", DatePickerMode.Year);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("2020", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_mode_displays_the_quarter_shorthand_for_Mode_Quarter()
    {
        var model = new PersonModel { BirthDate = new DateTime(2026, 8, 15) }; // Q3
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Mode", DatePickerMode.Quarter);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("2026-Q3", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_mode_displays_the_week_shorthand_for_Mode_Week()
    {
        var value = new DateTime(2026, 2, 14);
        const DayOfWeek firstDayOfWeek = DayOfWeek.Sunday;
        var lead = ((int)value.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        var weekStart = value.AddDays(-lead);
        var rule = CultureInfo.CurrentCulture.DateTimeFormat.CalendarWeekRule;
        var weekNumber = new GregorianCalendar().GetWeekOfYear(weekStart, rule, firstDayOfWeek);
        var expected = $"{weekStart.Year}-W{weekNumber:00}";

        var model = new PersonModel { BirthDate = value };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Mode", DatePickerMode.Week);
            b.AddAttribute(4, "FirstDayOfWeek", firstDayOfWeek);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal(expected, cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_mode_honors_Use12Hours_for_Type_Time()
    {
        var model = new TimeOnlyModel { ShipTime = new TimeOnly(14, 5, 9) };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDatePicker<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.AddAttribute(4, "Use12Hours", true);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2:05:09 PM", cut.Find(".edit-readonly-value").TextContent);
    }
}
