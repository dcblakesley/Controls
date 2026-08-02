using System.Globalization;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Regression tests for fixes made during the library audit:
/// <list type="bullet">
///   <item>ReadOnlyValue must HTML-encode the bound text — it previously rendered it as a raw
///   <c>MarkupString</c>, which let bound user data inject markup (an XSS hole).</item>
///   <item>EditDateNative's read-only display must format the bound value with <c>DateFormat</c> by the
///   value's own type, and degrade (not throw) when the format is incompatible with that type.</item>
///   <item>EditDateNative's read-only display must also force the Gregorian calendar under a
///   non-Gregorian-default culture (th-TH, ar-SA), matching <see cref="EditDate{T}"/>'s own contract --
///   the two controls are documented as interchangeable (same bound types, same <c>Type</c> values,
///   native input vs. calendar dropdown is the only difference) and must never disagree about the year.</item>
/// </list>
/// </summary>
public class ReadOnlyAndFormatTests : BunitContext
{
    class HtmlModel { public string Name { get; set; } = ""; }

    [Fact]
    public void ReadOnlyValue_html_encodes_bound_text_rather_than_rendering_markup()
    {
        // A value containing HTML must show as literal text, never inject DOM elements.
        var model = new HtmlModel { Name = "<b>x</b><img src=z onerror=alert(1)>" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ro = cut.Find(".edit-readonly-value");
        Assert.Empty(ro.QuerySelectorAll("b, img"));   // no elements injected
        Assert.Contains("<b>x</b>", ro.TextContent);    // raw text preserved verbatim
    }

    class DateModel { public DateTime? When { get; set; } }

    [Fact]
    public void EditDateNative_read_only_formats_value_with_DateFormat()
    {
        var model = new DateModel { When = new DateTime(2020, 3, 5) };
        Expression<Func<DateTime?>> field = () => model.When;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.When);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "DateFormat", "yyyy-MM-dd");
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("2020-03-05", cut.Find(".edit-readonly-value").TextContent);
    }

    [Fact]
    public void EditDateNative_read_only_renders_gregorian_years_under_non_gregorian_cultures()
    {
        // Same Gregorian contract as EditDate's own read-only display (and the picker controls) --
        // th-TH's Buddhist calendar (year + 543) must not make EditDateNative show 2563 while
        // EditDate shows 2020 for the identical bound value.
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var model = new DateModel { When = new DateTime(2020, 3, 5) };
            Expression<Func<DateTime?>> field = () => model.When;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditDateNative<DateTime?>>(0);
                b.AddAttribute(1, "Value", model.When);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "DateFormat", "yyyy-MM-dd");
                b.AddAttribute(5, "IsEditMode", false);
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
    public void EditDateNative_read_only_gregorian_fallback_applies_when_DateFormat_is_incompatible()
    {
        // The FormatException degrade path must stay Gregorian-forced too, same as the primary path
        // above -- it previously fell back to a bare CurrentValue.ToString() (CurrentCulture).
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");
            var model = new DateModel { When = new DateTime(2020, 3, 5) };
            Expression<Func<DateTime?>> field = () => model.When;
            var cut = Render(WithForm(model, b =>
            {
                b.OpenComponent<EditDateNative<DateTime?>>(0);
                b.AddAttribute(1, "Value", model.When);
                b.AddAttribute(2, "ValueExpression", field);
                b.AddAttribute(4, "DateFormat", "'unterminated"); // unterminated literal -> FormatException
                b.AddAttribute(5, "IsEditMode", false);
                b.CloseComponent();
            }));

            var text = cut.Find(".edit-readonly-value").TextContent;
            Assert.False(string.IsNullOrWhiteSpace(text)); // degraded, didn't crash
            Assert.DoesNotContain("2563", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    class TimeModel { public TimeOnly? At { get; set; } }

    [Fact]
    public void EditDateNative_read_only_with_incompatible_format_degrades_without_throwing()
    {
        // A date-style DateFormat applied to a TimeOnly throws FormatException inside ToString; the
        // control must catch it and fall back to the value's own ToString rather than crash the render.
        var model = new TimeModel { At = new TimeOnly(13, 45) };
        Expression<Func<TimeOnly?>> field = () => model.At;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<TimeOnly?>>(0);
            b.AddAttribute(1, "Value", model.At);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(4, "Type", InputDateType.Time);
            b.AddAttribute(5, "DateFormat", "yyyy-MM-dd");   // incompatible with TimeOnly
            b.AddAttribute(6, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ro = cut.Find(".edit-readonly-value");
        Assert.False(string.IsNullOrWhiteSpace(ro.TextContent));   // rendered the fallback, no crash
    }
}
