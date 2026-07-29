using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDate{T}"/>'s model-attribute fallbacks: <c>Format</c>/<c>DateFormat</c>
/// resolving through the bound property's <see cref="DisplayFormatAttribute"/> (see
/// <see cref="Controls.Helpers.AttributesHelper.FormatString"/>), and <c>Type</c> resolving through
/// <see cref="DataTypeAttribute"/> (see <see cref="Controls.Helpers.AttributesHelper.DateInputType"/>).
/// <c>Format</c> only affects the inner <see cref="DatePicker"/>'s own parse/display text entry (no
/// inspectable DOM attribute for it), so those assertions read the resolved value straight off the
/// rendered <see cref="DatePicker"/> instance -- the same approach <c>ModelMinMaxDateTests</c> uses for
/// Min/Max. <c>DateFormat</c> drives the read-only view, asserted against <c>.edit-readonly-value</c>.
/// The extension-method resolution itself is covered at the unit level in <c>AttributesHelperTests</c>;
/// this file proves <see cref="EditDate{T}"/> actually wires it through.
/// </summary>
public class EditDateModelAttributeTests : BunitContext
{
    public EditDateModelAttributeTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static DatePicker Picker(IRenderedComponent<ContainerFragment> cut) =>
        cut.FindComponent<DatePicker>().Instance;

    class DateAttrModel
    {
        [DisplayFormat(DataFormatString = "yyyy-MM-dd")]
        public DateTime? FormatAttr { get; set; }

        public DateTime? NoFormatAttr { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? DateTimeAttr { get; set; }

        [DataType(DataType.Time)]
        public DateTime? TimeAttr { get; set; }

        public DateTime? NoTypeAttr { get; set; }
    }

    // ----- Format (forwarded to the inner DatePicker) ---------------------------------------

    [Fact]
    public void Model_declared_DisplayFormat_attribute_reaches_the_pickers_Format_when_no_parameter_is_set()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.FormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.FormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("yyyy-MM-dd", Picker(cut).Format);
    }

    [Fact]
    public void Explicit_Format_parameter_overrides_the_model_attribute()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.FormatAttr; // carries [DisplayFormat("yyyy-MM-dd")]
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.FormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Format", "MM/dd/yyyy");
            b.CloseComponent();
        }));

        Assert.Equal("MM/dd/yyyy", Picker(cut).Format);
    }

    [Fact]
    public void Pickers_Format_stays_null_when_neither_parameter_nor_model_attribute_is_set()
    {
        // Regression guard: null must be preserved (not substituted) so DatePicker's own mode-derived
        // default still applies.
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.NoFormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.NoFormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Null(Picker(cut).Format);
    }

    // ----- DateFormat (read-only view) -------------------------------------------------------

    [Fact]
    public void Model_declared_DisplayFormat_attribute_formats_the_read_only_view_when_no_DateFormat_parameter_is_set()
    {
        var model = new DateAttrModel { FormatAttr = new DateTime(2024, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.FormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.FormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2024-03-05", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Explicit_DateFormat_parameter_overrides_the_model_attribute_for_the_read_only_view()
    {
        var model = new DateAttrModel { FormatAttr = new DateTime(2024, 3, 5) }; // carries [DisplayFormat("yyyy-MM-dd")]
        Expression<Func<DateTime?>> field = () => model.FormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.FormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "DateFormat", "MM/dd/yyyy");
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("03/05/2024", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Read_only_view_falls_back_to_the_MM_dd_yyyy_default_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new DateAttrModel { NoFormatAttr = new DateTime(2024, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.NoFormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.NoFormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("03-05-2024", cut.Find(".edit-readonly-value").TextContent);
    }

    // ----- Type (DataType -> InputDateType -> DatePickerMode) ---------------------------------

    [Fact]
    public void DataType_DateTime_attribute_switches_the_pickers_mode_to_DateTime_when_no_Type_parameter_is_set()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.DateTimeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.DateTimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal(DatePickerMode.DateTime, Picker(cut).Mode);
    }

    [Fact]
    public void DataType_Time_attribute_switches_the_pickers_mode_to_Time_when_no_Type_parameter_is_set()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.TimeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.TimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal(DatePickerMode.Time, Picker(cut).Mode);
    }

    [Fact]
    public void Explicit_Type_parameter_wins_over_the_DataType_attribute()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.DateTimeAttr; // carries [DataType(DataType.DateTime)]
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.DateTimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Date);
            b.CloseComponent();
        }));

        Assert.Equal(DatePickerMode.Date, Picker(cut).Mode);
    }

    [Fact]
    public void Pickers_mode_defaults_to_Date_when_neither_Type_parameter_nor_DataType_attribute_is_set()
    {
        var model = new DateAttrModel();
        Expression<Func<DateTime?>> field = () => model.NoTypeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.NoTypeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal(DatePickerMode.Date, Picker(cut).Mode);
    }
}
