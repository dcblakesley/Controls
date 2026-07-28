using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the two parameters <see cref="DatePicker"/>/<see cref="EditDate{T}"/> gained to close
/// the API gap with <see cref="EditDateNative{T}"/> (Phase 1 of the EditDateNative/EditDate rename, so
/// the renamed control is a genuine superset of the old one): <c>Size</c> (<see cref="SelectSize"/>,
/// mirroring the Select family's <c>wss-select-sm</c>/<c>wss-select-lg</c>) and
/// <c>ParsingErrorMessage</c> (a validation message for typed text the picker can't parse as a date at
/// all -- something <see cref="EditDateNative{T}"/>'s native <c>&lt;input type="date"&gt;</c> never
/// needed since the browser itself constrains what can be typed).
/// </summary>
public class PickerSizeAndParsingTests : BunitContext
{
    public PickerSizeAndParsingTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static readonly DateTime Feb14 = new(2026, 2, 14);

    static RenderFragment WithForm(object model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    // ----- DatePicker / DateRangePicker: Size ---------------------------------------------------

    [Fact]
    public void DatePicker_Size_Default_adds_no_class_to_the_wrapper()
    {
        var cut = Render<DatePicker>(p => p.Add(c => c.Size, SelectSize.Default));

        var wrapper = cut.Find(".wss-picker");
        Assert.DoesNotContain("wss-picker-sm", wrapper.ClassList);
        Assert.DoesNotContain("wss-picker-lg", wrapper.ClassList);
    }

    [Theory]
    [InlineData(SelectSize.Small, "wss-picker-sm")]
    [InlineData(SelectSize.Large, "wss-picker-lg")]
    public void DatePicker_Size_appends_the_token_to_the_wrapper(SelectSize size, string token)
    {
        var cut = Render<DatePicker>(p => p.Add(c => c.Size, size));

        Assert.Contains(token, cut.Find(".wss-picker").ClassList);
    }

    [Fact]
    public void DateRangePicker_Size_appends_the_token_to_the_wrapper()
    {
        // DateRangePicker shares the exact same wrapper/CSS structure as DatePicker (both render one
        // .wss-picker-input containing .wss-picker-input-slot input elements), so Size was added
        // there too as part of the same change.
        var cut = Render<DateRangePicker>(p => p.Add(c => c.Size, SelectSize.Large));

        Assert.Contains("wss-picker-lg", cut.Find(".wss-picker").ClassList);
    }

    [Fact]
    public void DateRangePicker_Size_Default_adds_no_class_to_the_wrapper()
    {
        var cut = Render<DateRangePicker>(p => p.Add(c => c.Size, SelectSize.Default));

        var wrapper = cut.Find(".wss-picker");
        Assert.DoesNotContain("wss-picker-sm", wrapper.ClassList);
        Assert.DoesNotContain("wss-picker-lg", wrapper.ClassList);
    }

    // ----- EditDate: Size forwarding -------------------------------------------------------

    [Fact]
    public void EditDate_Size_Default_adds_no_class()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".wss-picker");
        Assert.DoesNotContain("wss-picker-sm", wrapper.ClassList);
        Assert.DoesNotContain("wss-picker-lg", wrapper.ClassList);
    }

    [Fact]
    public void EditDate_forwards_Size_to_the_inner_picker()
    {
        var model = new PersonModel { BirthDate = new DateTime(2020, 1, 1) };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Size", SelectSize.Small);
            b.CloseComponent();
        }));

        Assert.Contains("wss-picker-sm", cut.Find(".wss-picker").ClassList);
    }

    // ----- DatePicker: OnParseError --------------------------------------------------------------

    [Fact]
    public void Unparseable_typed_text_raises_OnParseError_with_the_offending_text()
    {
        string? reported = null;
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Value, Feb14)
            .Add(c => c.OnParseError, (string t) => reported = t));

        cut.Find(".wss-picker-input").Click(); // open
        cut.Find(".wss-picker-input-date").Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("not a date", reported);
        Assert.Equal(Feb14, cut.Instance.Value); // unchanged -- same revert behavior as before
    }

    [Fact]
    public void Out_of_range_but_parseable_text_does_not_raise_OnParseError()
    {
        // A well-formed date Min/Max rejects is a different situation from a parse failure (see
        // DatePicker.OnParseError's doc comment) -- only TryParseDate actually failing raises this.
        var raised = false;
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Value, Feb14)
            .Add(c => c.Min, new DateTime(2026, 2, 10))
            .Add(c => c.Max, new DateTime(2026, 2, 20))
            .Add(c => c.OnParseError, (string t) => raised = true));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("03/01/2026"); // parseable, but outside Min/Max
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.False(raised);
    }

    [Fact]
    public void Standalone_DatePicker_with_no_handler_still_reverts_silently()
    {
        // OnParseError is optional -- a DatePicker with no handler attached must behave exactly as it
        // did before this parameter existed.
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Value, Feb14));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("garbage");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(Feb14, cut.Instance.Value);
        Assert.Equal("02/14/2026", cut.Find(".wss-picker-input-date").GetAttribute("value"));
    }

    // ----- EditDate: ParsingErrorMessage ----------------------------------------------------

    [Fact]
    public void Unparseable_typed_text_surfaces_the_default_ParsingErrorMessage_with_the_field_name()
    {
        var model = new PersonModel { BirthDate = Feb14 };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Format", "MM/dd/yyyy");
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        // {0} is FieldIdentifier.FieldName (the raw property name, "BirthDate") -- same substitution
        // EditDateNative<T>.TryParseValueFromString uses for its own identical-shaped default message.
        Assert.Contains("The BirthDate field must be a date.", cut.Find(".edit-validation-message").TextContent);
        // aria-invalid/aria-errormessage reach the picker's actual <input> too, mirroring the Required
        // coverage in EditDateTests.
        var input = cut.Find(".wss-picker-input-date");
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.StartsWith("error-msg-", input.GetAttribute("aria-errormessage"));
    }

    [Fact]
    public void Custom_ParsingErrorMessage_is_honored()
    {
        var model = new PersonModel { BirthDate = Feb14 };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Format", "MM/dd/yyyy");
            b.AddAttribute(4, "ParsingErrorMessage", "{0} isn't a real date.");
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Contains("BirthDate isn't a real date.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void A_subsequent_valid_entry_clears_the_parsing_error_message()
    {
        var model = new PersonModel { BirthDate = Feb14 };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Format", "MM/dd/yyyy");
            b.AddAttribute(4, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.BirthDate = v));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        Assert.Contains("must be a date", cut.Find(".edit-validation-message").TextContent);

        cut.Find(".wss-picker-input-date").Input("03/05/2026");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(string.Empty, cut.Find(".edit-validation-message").TextContent);
        // aria-invalid is omitted entirely once valid again (DatePicker.razor renders it only when
        // AriaInvalid is true), not set to the literal string "false".
        Assert.Null(cut.Find(".wss-picker-input-date").GetAttribute("aria-invalid"));
        Assert.Equal(new DateTime(2026, 3, 5), model.BirthDate);
    }

    [Fact]
    public void Out_of_range_rejection_does_not_surface_a_parsing_error_message()
    {
        // Min/Max rejecting a well-formed date is not a parse failure (see DatePicker.OnParseError's
        // doc comment) -- ParsingErrorMessage must not appear for it.
        var model = new PersonModel { BirthDate = Feb14 };
        Expression<Func<DateTime?>> field = () => model.BirthDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.BirthDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Format", "MM/dd/yyyy");
            b.AddAttribute(4, "Min", new DateTime(2026, 2, 10));
            b.AddAttribute(5, "Max", new DateTime(2026, 2, 20));
            b.CloseComponent();
        }));

        cut.Find(".wss-picker-input").Click();
        cut.Find(".wss-picker-input-date").Input("03/01/2026"); // parseable, but outside Min/Max
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(string.Empty, cut.Find(".edit-validation-message").TextContent);
    }
}
