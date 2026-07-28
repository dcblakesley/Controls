using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDateRange"/>'s model-attribute fallbacks: <c>Format</c>/<c>DateFormat</c>
/// resolving through the Start property's <see cref="DisplayFormatAttribute"/>, falling back to the
/// End property's (see <see cref="Controls.Helpers.AttributesHelper.FormatString"/>) -- mirrors
/// <c>ModelMinMaxDateRangeTests</c>'s Start-first-then-End preference for <c>Min</c>/<c>Max</c>, since
/// <c>Format</c>/<c>DateFormat</c> (like Min/Max) drive the single shared calendar rather than two
/// independent per-field values. <c>Format</c> only affects the inner <see cref="DateRangePicker"/>'s
/// own parse/display text entry (no inspectable DOM attribute for it), so those assertions read the
/// resolved value straight off the rendered <see cref="DateRangePicker"/> instance. <c>DateFormat</c>
/// drives the read-only "start - end" view, asserted against <c>.edit-readonly-value</c>. <c>Mode</c>
/// is not attribute-mapped, so it isn't covered here. The extension-method resolution itself is
/// covered at the unit level in <c>AttributesHelperTests</c>; this file proves <see cref="EditDateRange"/>
/// actually wires it through.
/// </summary>
public class EditDateRangeModelAttributeTests : BunitContext
{
    public EditDateRangeModelAttributeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    static DateRangePicker Picker(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponent<DateRangePicker>().Instance;

    // Start alone carries the [DisplayFormat] -- proves the "natural"/preferred field.
    class FormatOnStartModel
    {
        [DisplayFormat(DataFormatString = "yyyy-MM-dd")]
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    // Only End carries [DisplayFormat] -- proves the fallback to the OTHER field's attribute.
    class FormatOnEndModel
    {
        public DateTime? Start { get; set; }
        [DisplayFormat(DataFormatString = "yyyy-MM-dd")]
        public DateTime? End { get; set; }
    }

    class NoFormatModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    // ----- Format (forwarded to the shared DateRangePicker) -----------------------------------

    [Fact]
    public void Model_declared_DisplayFormat_attribute_on_Start_reaches_the_shared_pickers_Format_when_no_parameter_is_set()
    {
        var model = new FormatOnStartModel();
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

        Assert.Equal("yyyy-MM-dd", Picker(cut).Format);
    }

    [Fact]
    public void DisplayFormat_attribute_on_End_is_the_fallback_when_Start_has_none()
    {
        var model = new FormatOnEndModel();
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

        Assert.Equal("yyyy-MM-dd", Picker(cut).Format);
    }

    [Fact]
    public void Explicit_Format_parameter_wins_over_both_attributes()
    {
        var model = new FormatOnStartModel();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "Format", "MM/dd/yyyy");
            b.CloseComponent();
        }));

        Assert.Equal("MM/dd/yyyy", Picker(cut).Format);
    }

    [Fact]
    public void Pickers_Format_stays_null_when_neither_parameter_nor_either_attribute_is_set()
    {
        // Regression guard: null must be preserved (not substituted) so DateRangePicker's own
        // mode-derived default still applies.
        var model = new NoFormatModel();
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

        Assert.Null(Picker(cut).Format);
    }

    // ----- DateFormat (read-only "start - end" view) -------------------------------------------

    [Fact]
    public void Model_declared_DisplayFormat_attribute_on_Start_formats_the_read_only_view_when_no_DateFormat_parameter_is_set()
    {
        var model = new FormatOnStartModel { Start = new DateTime(2024, 3, 5), End = new DateTime(2024, 3, 6) };
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
            b.CloseComponent();
        }));

        Assert.Equal("2024-03-05 - 2024-03-06", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void DisplayFormat_attribute_on_End_is_the_read_only_view_fallback_when_Start_has_none()
    {
        var model = new FormatOnEndModel { Start = new DateTime(2024, 3, 5), End = new DateTime(2024, 3, 6) };
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
            b.CloseComponent();
        }));

        Assert.Equal("2024-03-05 - 2024-03-06", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Explicit_DateFormat_parameter_overrides_both_attributes_for_the_read_only_view()
    {
        var model = new FormatOnStartModel { Start = new DateTime(2024, 3, 5), End = new DateTime(2024, 3, 6) };
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "DateFormat", "MM/dd/yyyy");
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Equal("03/05/2024 - 03/06/2024", cut.Find(".edit-readonly-value").TextContent.Trim());
    }

    [Fact]
    public void Read_only_view_falls_back_to_the_MM_dd_yyyy_default_when_neither_parameter_nor_either_attribute_is_set()
    {
        var model = new NoFormatModel { Start = new DateTime(2024, 3, 5), End = new DateTime(2024, 3, 6) };
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
            b.CloseComponent();
        }));

        Assert.Equal("03-05-2024 - 03-06-2024", cut.Find(".edit-readonly-value").TextContent.Trim());
    }
}
