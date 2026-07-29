using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers EditNumber's model-declared Step/Format resolution: the control's own <c>Step</c>/<c>Format</c>
/// parameters win, else the bound property's <c>[Step]</c>/<c>[DisplayFormat(DataFormatString = …)]</c>
/// supplies the rendered <c>step</c> attribute / read-only formatting (see
/// <see cref="Controls.Helpers.AttributesHelper.Step"/>/<see cref="Controls.Helpers.AttributesHelper.FormatString"/>),
/// else EditNumber's own built-in default applies -- the extension-method resolution itself is covered
/// at the unit level in <see cref="AttributesHelperTests"/>, this file proves EditNumber actually wires
/// it through to the DOM. Mirrors <see cref="ModelMinMaxNumberTests"/>'s shape for Min/Max.
/// </summary>
public class EditNumberModelAttributeTests : BunitContext
{
    class StepFormatModel
    {
        [Step(0.01)]
        public double? StepDoubleAttr { get; set; }

        [Step("0.01")]
        public double? StepStringAttr { get; set; }

        public double? StepNoAttr { get; set; }

        [DisplayFormat(DataFormatString = "{0:N2}")]
        public decimal? FormatAttr { get; set; }

        public decimal? FormatNoAttr { get; set; }
    }

    // Step

    [Fact]
    public void Model_declared_Step_attribute_double_ctor_renders_the_step_attribute_when_no_parameter_is_set()
    {
        var model = new StepFormatModel();
        Expression<Func<double?>> field = () => model.StepDoubleAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.StepDoubleAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("0.01", cut.Find("input.edit-number-input").GetAttribute("step"));
    }

    [Fact]
    public void Model_declared_Step_attribute_string_ctor_renders_the_step_attribute_when_no_parameter_is_set()
    {
        var model = new StepFormatModel();
        Expression<Func<double?>> field = () => model.StepStringAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.StepStringAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Equal("0.01", cut.Find("input.edit-number-input").GetAttribute("step"));
    }

    [Fact]
    public void Explicit_Step_parameter_overrides_the_model_attribute()
    {
        var model = new StepFormatModel();
        Expression<Func<double?>> field = () => model.StepDoubleAttr; // carries [Step(0.01)]
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.StepDoubleAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Step", 0.5m);
            b.CloseComponent();
        }));

        Assert.Equal("0.5", cut.Find("input.edit-number-input").GetAttribute("step"));
    }

    [Fact]
    public void Default_step_of_1_is_used_when_neither_parameter_nor_model_attribute_is_set()
    {
        var model = new StepFormatModel();
        Expression<Func<double?>> field = () => model.StepNoAttr;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<double?>>(0);
            b.AddAttribute(1, "Value", model.StepNoAttr);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        // "1.0", not "1" — the default has always been the literal 1.0m, and decimal.ToString
        // preserves trailing scale, so this pins the pre-existing rendering exactly.
        Assert.Equal("1.0", cut.Find("input.edit-number-input").GetAttribute("step"));
    }

    // Format (read-only display)

    [Fact]
    public void Model_declared_DisplayFormat_attribute_formats_the_read_only_view_when_no_parameter_is_set()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var model = new StepFormatModel { FormatAttr = 1234.5m };
            Expression<Func<decimal?>> field = () => model.FormatAttr;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditNumber<decimal?>>(0);
                b.AddAttribute(1, "Value", model.FormatAttr);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "IsEditMode", false);
                b.CloseComponent();
            }));

            Assert.Equal("1,234.50", cut.Find(".edit-readonly-value").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Explicit_Format_parameter_overrides_the_model_attribute()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var model = new StepFormatModel { FormatAttr = 1234.5m }; // carries [DisplayFormat("{0:N2}")]
            Expression<Func<decimal?>> field = () => model.FormatAttr;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditNumber<decimal?>>(0);
                b.AddAttribute(1, "Value", model.FormatAttr);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "Format", "N1");
                b.AddAttribute(5, "IsEditMode", false);
                b.CloseComponent();
            }));

            Assert.Equal("1,234.5", cut.Find(".edit-readonly-value").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Renders_default_ToString_when_neither_Format_parameter_nor_model_attribute_is_set()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            var model = new StepFormatModel { FormatNoAttr = 1234.5m };
            Expression<Func<decimal?>> field = () => model.FormatNoAttr;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditNumber<decimal?>>(0);
                b.AddAttribute(1, "Value", model.FormatNoAttr);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "IsEditMode", false);
                b.CloseComponent();
            }));

            Assert.Equal("1234.5", cut.Find(".edit-readonly-value").TextContent);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
