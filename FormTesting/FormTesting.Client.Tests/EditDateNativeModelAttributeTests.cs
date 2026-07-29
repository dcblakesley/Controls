using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// bUnit tests for <see cref="EditDateNative{T}"/>'s model-attribute fallbacks: <c>DateFormat</c>
/// resolving through the bound property's <see cref="DisplayFormatAttribute"/> (see
/// <see cref="Controls.Helpers.AttributesHelper.FormatString"/>), and <c>Type</c> resolving through
/// <see cref="DataTypeAttribute"/> (see <see cref="Controls.Helpers.AttributesHelper.DateInputType"/>).
/// <c>Type</c> renders a native <c>type</c> attribute directly, so those assertions read the DOM --
/// mirrors <c>ModelMinMaxDateTests</c>' approach for this same control's Min/Max. <c>DateFormat</c>
/// drives the read-only view, asserted against <c>.edit-readonly-value</c>. The extension-method
/// resolution itself is covered at the unit level in <c>AttributesHelperTests</c>; this file proves
/// <see cref="EditDateNative{T}"/> actually wires it through.
/// </summary>
public class EditDateNativeModelAttributeTests : BunitContext
{
    class DateNativeAttrModel
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

    // ----- DateFormat (read-only view) -------------------------------------------------------

    [Fact]
    public void Model_declared_DisplayFormat_attribute_formats_the_read_only_view_when_no_DateFormat_parameter_is_set()
    {
        var model = new DateNativeAttrModel { FormatAttr = new DateTime(2024, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.FormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.FormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2024-03-05", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void Explicit_DateFormat_parameter_overrides_the_model_attribute()
    {
        var model = new DateNativeAttrModel { FormatAttr = new DateTime(2024, 3, 5) }; // carries [DisplayFormat("yyyy-MM-dd")]
        Expression<Func<DateTime?>> field = () => model.FormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
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
        var model = new DateNativeAttrModel { NoFormatAttr = new DateTime(2024, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.NoFormatAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.NoFormatAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("03-05-2024", cut.Find(".edit-readonly-value").TextContent);
    }

    // ----- Type (DataType -> InputDateType -> native input type) ------------------------------

    [Fact]
    public void DataType_DateTime_attribute_switches_the_input_type_to_datetime_local_when_no_Type_parameter_is_set()
    {
        var model = new DateNativeAttrModel();
        Expression<Func<DateTime?>> field = () => model.DateTimeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.DateTimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("datetime-local", cut.Find("input.edit-date-input").GetAttribute("type"));
    }

    [Fact]
    public void DataType_Time_attribute_switches_the_input_type_to_time_when_no_Type_parameter_is_set()
    {
        var model = new DateNativeAttrModel();
        Expression<Func<DateTime?>> field = () => model.TimeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.TimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("time", cut.Find("input.edit-date-input").GetAttribute("type"));
    }

    [Fact]
    public void Explicit_Type_parameter_wins_over_the_DataType_attribute()
    {
        var model = new DateNativeAttrModel();
        Expression<Func<DateTime?>> field = () => model.DateTimeAttr; // carries [DataType(DataType.DateTime)]
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.DateTimeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Date);
            b.CloseComponent();
        }));

        Assert.Equal("date", cut.Find("input.edit-date-input").GetAttribute("type"));
    }

    [Fact]
    public void Input_type_defaults_to_date_when_neither_Type_parameter_nor_DataType_attribute_is_set()
    {
        var model = new DateNativeAttrModel();
        Expression<Func<DateTime?>> field = () => model.NoTypeAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.NoTypeAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("date", cut.Find("input.edit-date-input").GetAttribute("type"));
    }
}
