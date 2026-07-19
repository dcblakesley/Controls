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
}
