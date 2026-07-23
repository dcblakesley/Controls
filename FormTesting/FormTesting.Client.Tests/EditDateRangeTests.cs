using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDateRange"/> — the composite two-field form control wrapping the
/// <see cref="DateRangePicker"/> UI-kit calendar dropdown. These cover the layer this control adds
/// over DateRangePicker itself (dual FieldIdentifier registration, EditContext notification for each
/// field independently, one shared label, per-field validation messages, read-only view); the
/// picker's own open/close/two-click-pick/navigation/keyboard behavior is already covered by
/// <c>DateRangePickerTests</c> and the JS-owned parts by <c>DateRangePickerE2ETests</c>.
/// </summary>
public class EditDateRangeTests : TestContext
{
    public EditDateRangeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    // A dedicated two-property test model — EditDateRange binds two independent scalar fields, which
    // PersonModel (shared by the other tests) doesn't have a pair of.
    class RangeModel
    {
        [Required]
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    // A second model with [Required] on End instead of Start, so an End-only validation failure can
    // be produced without Start ever being invalid -- RangeModel's Start is always the one under
    // [Required], which the other tests rely on (e.g. asserting End's aria-required stays unset).
    class EndRequiredModel
    {
        public DateTime? Start { get; set; }
        [Required]
        public DateTime? End { get; set; }
    }

    static readonly DateTime Jan15 = new(2025, 1, 15);
    static readonly DateTime Feb3 = new(2025, 2, 3);

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static void Open(IRenderedFragment cut) => cut.Find(".wss-picker-input").Click();

    // The given panel's (0 = left/start month, 1 = right/end month) in-month day button.
    static IElement Day(IRenderedFragment cut, int panel, int dayNumber) =>
        cut.FindAll(".wss-picker-month")[panel]
            .QuerySelectorAll(".wss-picker-day")
            .First(b => !b.ClassList.Contains("wss-picker-day-outside") &&
                        b.TextContent == dayNumber.ToString("00", CultureInfo.InvariantCulture));

    [Fact]
    public void Two_day_clicks_commit_both_fields_and_notify_both_field_identifiers()
    {
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        var editContext = new EditContext(model);
        var notified = new List<string>();
        editContext.OnFieldChanged += (_, e) => notified.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditDateRange>(0);
                content.AddAttribute(1, "Start", model.Start);
                content.AddAttribute(2, "StartExpression", startField);
                content.AddAttribute(3, "StartChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.AddAttribute(6, "EndChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.End = v));
                content.AddAttribute(7, "FirstDayOfWeek", DayOfWeek.Sunday);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        Open(cut); // anchored on Jan/Feb 2025 (the bound Start)
        Day(cut, 0, 10).Click(); // first click: pending start = Jan 10
        Day(cut, 1, 20).Click(); // second click: commits Jan 10 - Feb 20 and closes

        Assert.Equal(new DateTime(2025, 1, 10), model.Start);
        Assert.Equal(new DateTime(2025, 2, 20), model.End);
        Assert.Contains("Start", notified);
        Assert.Contains("End", notified);
    }

    [Fact]
    public void Typed_start_commit_notifies_only_the_Start_field()
    {
        var model = new RangeModel();
        var editContext = new EditContext(model);
        var notified = new List<string>();
        editContext.OnFieldChanged += (_, e) => notified.Add(e.FieldIdentifier.FieldName);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditDateRange>(0);
                content.AddAttribute(1, "Start", model.Start);
                content.AddAttribute(2, "StartExpression", startField);
                content.AddAttribute(3, "StartChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.AddAttribute(6, "EndChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.End = v));
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        Open(cut);
        cut.Find(".wss-picker-input-start").Input("03/05/2025");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(new DateTime(2025, 3, 5), model.Start);
        Assert.Null(model.End);
        Assert.Contains("Start", notified);
        Assert.DoesNotContain("End", notified);
    }

    [Fact]
    public void Both_fields_register_with_FormOptions()
    {
        var model = new RangeModel();
        var formOptions = new FormOptions();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
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
                    content.OpenComponent<EditDateRange>(0);
                    content.AddAttribute(1, "Start", model.Start);
                    content.AddAttribute(2, "StartExpression", startField);
                    content.AddAttribute(3, "End", model.End);
                    content.AddAttribute(4, "EndExpression", endField);
                    content.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "Start");
        Assert.Contains(formOptions.FieldIdentifiers, fi => fi.FieldName == "End");

        // Each field registers under its own input's DOM id — a ValidationView link for an End-only
        // error must land on the End input, not Start's (the ids differ by the "-end" suffix).
        var startId = formOptions.FieldIds.Single(kv => kv.Key.FieldName == "Start").Value;
        var endId = formOptions.FieldIds.Single(kv => kv.Key.FieldName == "End").Value;
        Assert.Equal($"{startId}-end", endId);
    }

    [Fact]
    public void Required_on_Start_flows_to_the_star_and_aria_required()
    {
        var model = new RangeModel(); // Start is [Required]
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-label-required-star"));
        // aria-required reaches the Start <input> via DateRangePicker's StartAriaRequired parameter;
        // the End field carries no [Required], so its input stays unmarked (per-field independence).
        Assert.Equal("true", cut.Find(".wss-picker-input-start").GetAttribute("aria-required"));
        Assert.Null(cut.Find(".wss-picker-input-end").GetAttribute("aria-required"));
    }

    [Fact]
    public void Validation_message_displays_independently_for_either_field()
    {
        var model = new RangeModel(); // Start empty -> [Required] fails
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditDateRange>(1);
                content.AddAttribute(2, "Start", model.Start);
                content.AddAttribute(3, "StartExpression", startField);
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Contains(cut.FindAll(".edit-validation-message"),
            m => m.TextContent.Contains("required", StringComparison.OrdinalIgnoreCase));

        // End has no DataAnnotation of its own — push a message directly and confirm it renders
        // independently alongside Start's, and that the ComponentBase-based control (not an InputBase)
        // still reacts live to OnValidationStateChanged the way EditControlListBase controls do.
        var store = new ValidationMessageStore(editContext);
        cut.InvokeAsync(() =>
        {
            store.Add(editContext.Field(nameof(RangeModel.End)), "End must be after Start");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Contains(cut.FindAll(".edit-validation-message"),
            m => m.TextContent.Contains("End must be after Start"));
    }

    [Fact]
    public void Start_only_invalid_field_marks_the_shared_wrapper_invalid()
    {
        var model = new RangeModel(); // Start empty -> [Required] fails; End carries no annotation
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditDateRange>(1);
                content.AddAttribute(2, "Start", model.Start);
                content.AddAttribute(3, "StartExpression", startField);
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        Assert.Contains("invalid", cut.Find(".wss-picker").ClassList);
    }

    [Fact]
    public void End_only_invalid_field_marks_the_shared_wrapper_invalid_and_not_valid()
    {
        // Start is set and passes (no annotation of its own), End is left null against its own
        // [Required] -- the shared wrapper's state class is derived from Start's own EditContext
        // state, so an End-only error must still be folded in to turn the wrapper invalid rather
        // than leaving it at Start's own "valid".
        var model = new EndRequiredModel { Start = Jan15 };
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<DataAnnotationsValidator>(0);
                content.CloseComponent();
                content.OpenComponent<EditDateRange>(1);
                content.AddAttribute(2, "Start", model.Start);
                content.AddAttribute(3, "StartExpression", startField);
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        cut.InvokeAsync(() => editContext.Validate());

        var wrapper = cut.Find(".wss-picker");
        Assert.Contains("invalid", wrapper.ClassList);
        Assert.DoesNotContain("valid", wrapper.ClassList);
    }

    [Fact]
    public void Both_fields_valid_after_modification_show_modified_valid_and_no_invalid()
    {
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        var editContext = new EditContext(model);
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;

        var cut = Render(b =>
        {
            b.OpenComponent<EditForm>(0);
            b.AddAttribute(1, "EditContext", editContext);
            b.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content =>
            {
                content.OpenComponent<EditDateRange>(0);
                content.AddAttribute(1, "Start", model.Start);
                content.AddAttribute(2, "StartExpression", startField);
                content.AddAttribute(3, "StartChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
                content.AddAttribute(4, "End", model.End);
                content.AddAttribute(5, "EndExpression", endField);
                content.CloseComponent();
            }));
            b.CloseComponent();
        });

        Open(cut);
        cut.Find(".wss-picker-input-start").Input("03/05/2025");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        var wrapper = cut.Find(".wss-picker");
        Assert.Contains("modified", wrapper.ClassList);
        Assert.Contains("valid", wrapper.ClassList);
        Assert.DoesNotContain("invalid", wrapper.ClassList);
    }

    [Fact]
    public void Label_auto_generates_from_the_Start_property_name()
    {
        var model = new RangeModel { Start = Jan15 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.Contains("Start", cut.Find("label.edit-label").TextContent);
    }

    [Fact]
    public void No_label_defaults_each_inputs_aria_label_to_its_own_field_name_and_stays_unique()
    {
        // With no Label override, each input's aria-label falls back to its own field's auto-derived
        // label rather than a shared/generic string, so Start and End are already unique out of the box.
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        Assert.Equal("Start", cut.Find(".wss-picker-input-start").GetAttribute("aria-label"));
        Assert.Equal("End", cut.Find(".wss-picker-input-end").GetAttribute("aria-label"));
    }

    [Fact]
    public void Label_flows_into_both_inputs_aria_label_containing_the_visible_text()
    {
        // Label sets the one shared visible FormLabel; both Start's and End's aria-label (which wins
        // the accessible-name computation over label[for]) must each contain that text while staying
        // unique from one another (WCAG 2.5.3 Label in Name).
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Label", "Trip Dates");
            b.CloseComponent();
        }));

        var startLabel = cut.Find(".wss-picker-input-start").GetAttribute("aria-label");
        var endLabel = cut.Find(".wss-picker-input-end").GetAttribute("aria-label");

        Assert.Contains("Trip Dates", startLabel);
        Assert.Contains("Trip Dates", endLabel);
        Assert.NotEqual(startLabel, endLabel);
    }

    [Fact]
    public void StartInputLabel_and_EndInputLabel_override_the_composed_label()
    {
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Label", "Trip Dates");
            b.AddAttribute(6, "StartInputLabel", "Check-in");
            b.AddAttribute(7, "EndInputLabel", "Check-out");
            b.CloseComponent();
        }));

        Assert.Equal("Check-in", cut.Find(".wss-picker-input-start").GetAttribute("aria-label"));
        Assert.Equal("Check-out", cut.Find(".wss-picker-input-end").GetAttribute("aria-label"));
    }

    [Fact]
    public void Read_only_mode_renders_start_and_end_formatted_with_DateFormat()
    {
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "DateFormat", "yyyy-MM-dd");
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        var text = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("2025-01-15", text);
        Assert.Contains("2025-02-03", text);
        Assert.Empty(cut.FindAll(".wss-picker"));
    }

    [Fact]
    public void Read_only_mode_forwards_the_consumers_class_and_the_Start_fields_state_class()
    {
        // The edit/read-only class-forwarding asymmetry fixed across the rest of the library
        // (EditMultiSelect/EditFile/EditChecked* all forward to ReadOnlyValue) applies here too --
        // FieldCssClass merges the consumer's class with the Start field's EditContext state class,
        // and it must reach the read-only view, not just DateRangePicker's edit-mode wrapper.
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "IsEditMode", false);
            b.AddAttribute(6, "class", "my-range-class");
            b.CloseComponent();
        }));

        var value = cut.Find(".edit-readonly-value");
        Assert.Contains("my-range-class", value.ClassList);
        // EditContext.FieldCssClass emits "valid" for an untouched, currently-passing field -- the
        // same token the Start input would carry in edit mode.
        Assert.Contains("valid", value.ClassList);
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_hides_when_Start_is_default_DateTime_and_End_is_null()
    {
        // Mirrors EditDatePicker's per-field default(DateTime) contract: neither endpoint carries a
        // meaningful value, so the pair counts as "default" even though Start isn't literally null.
        var model = new RangeModel { Start = default(DateTime), End = null };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-control-wrapper"));
    }

    [Fact]
    public void HidingMode_WhenNullOrDefault_shows_when_one_endpoint_has_a_real_date()
    {
        var model = new RangeModel { Start = Jan15, End = null };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Hiding", HidingMode.WhenNullOrDefault);
            b.CloseComponent();
        }));

        Assert.NotEmpty(cut.FindAll(".edit-control-wrapper"));
    }

    // ----- Forwarded DateRangePicker parameters -------------------------------------------------

    [Fact]
    public void Mode_Month_forwards_and_renders_the_month_dual_panels()
    {
        var model = new RangeModel { Start = Jan15, End = Feb3 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Mode", DatePickerMode.Month);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.Equal(2, cut.FindAll(".wss-picker-month").Count); // dual panels, forwarded via Mode
        Assert.NotEmpty(cut.FindAll(".wss-picker-month-btn")); // month-grid buttons, not day cells
        Assert.Empty(cut.FindAll(".wss-picker-day"));
    }

    [Fact]
    public void DisabledDate_forwards_and_disables_a_day_cell()
    {
        var model = new RangeModel { Start = Jan15 }; // Wednesday
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "DisabledDate", (Func<DateTime, bool>)(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));
            b.AddAttribute(6, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.CloseComponent();
        }));

        Open(cut);

        Assert.True(Day(cut, 0, 18).HasAttribute("disabled")); // Jan 18, 2025 is a Saturday
        Assert.False(Day(cut, 0, 15).HasAttribute("disabled"));
    }

    [Fact]
    public void ExtraFooter_forwards_and_renders_in_the_dropdown()
    {
        var model = new RangeModel { Start = Jan15 };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "ExtraFooter", (RenderFragment)(rb => rb.AddMarkupContent(0, "<span class=\"my-extra\">extra</span>")));
            b.CloseComponent();
        }));

        Open(cut);

        Assert.Equal("extra", cut.Find(".wss-picker-extra-footer .my-extra").TextContent);
    }

    [Fact]
    public void Use12Hours_forwards_and_reaches_the_datetime_session_time_row()
    {
        var model = new RangeModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Mode", DatePickerMode.DateTime);
            b.AddAttribute(6, "Use12Hours", true);
            b.CloseComponent();
        }));

        Open(cut);

        var selects = cut.FindAll(".wss-picker-time-row select");
        Assert.Equal(4, selects.Count); // hour, minute, second, period
        Assert.Equal("AM/PM", selects[3].GetAttribute("aria-label"));
    }

    // ----- Read-only display: mode-aware default DateFormat + Quarter/DateTime special cases -----

    [Fact]
    public void Read_only_mode_displays_the_quarter_shorthand_for_Mode_Quarter()
    {
        var model = new RangeModel { Start = new DateTime(2026, 8, 15), End = new DateTime(2026, 11, 1) }; // Q3, Q4
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Mode", DatePickerMode.Quarter);
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        // No DateFormat set -- Quarter's own default bypasses ToString entirely (no .NET token for a
        // quarter number) via PickerMath.FormatQuarterDisplay, mirroring EditDatePicker's own display.
        Assert.Equal("2026-Q3 - 2026-Q4", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_mode_includes_time_of_day_for_Mode_DateTime_with_no_DateFormat_set()
    {
        var model = new RangeModel
        {
            Start = new DateTime(2025, 1, 15, 9, 30, 0),
            End = new DateTime(2025, 2, 3, 17, 45, 0),
        };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Mode", DatePickerMode.DateTime);
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        // DateTime's own default DateFormat ("MM-dd-yyyy HH:mm:ss") flows through when unset -- a
        // plain Date-mode default here would silently drop the time-of-day.
        var text = cut.Find(".edit-readonly-value").TextContent;
        Assert.Contains("09:30:00", text);
        Assert.Contains("17:45:00", text);
    }
}
