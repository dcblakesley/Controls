using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace FormTesting.Client.Tests;

/// <summary>
/// Accessibility regressions across the whole date subsystem — <see cref="DatePicker"/>,
/// <see cref="DateRangePicker"/>, <see cref="EditDate{T}"/>, <see cref="EditDateNative{T}"/> and
/// <see cref="EditDateRange"/>. Grouped here rather than split across the five existing per-control
/// files because each fix spans two of them (a picker parameter plus the form control that feeds it),
/// and because they share one theme: information that existed in the control but reached nobody.
/// </summary>
/// <remarks>
/// The JS-owned halves (real focus movement, the focus-out dismissal) belong to the e2e suite —
/// bUnit executes no JavaScript. The one exception is <c>ElementReference.FocusAsync</c>, which
/// routes through <c>Blazor._internal.domWrapper.focus</c> and so IS observable in loose JS-interop
/// mode; the clear-button focus reclaim below leans on exactly that.
/// </remarks>
public class A11yDateTests : BunitContext
{
    public A11yDateTests() => JSInterop.Mode = JSRuntimeMode.Loose; // tolerate the overlay JS import

    static readonly DateTime Feb14 = new(2026, 2, 14);

    // ----- Models -------------------------------------------------------------

    class BirthdayModel
    {
        [DisplayName("Birth Date")]
        [Description("When you were born")]
        public DateTime? Birthday { get; set; }
    }

    class NonNullableDateModel { public DateTime ShipDate { get; set; } }

    class MonthModel { public DateTime? Period { get; set; } }

    class AutocompleteModel
    {
        [Autocomplete("bday")]
        public DateTime? Birthday { get; set; }
    }

    class StayModel
    {
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
    }

    class EndRequiredStayModel
    {
        public DateTime? Start { get; set; }
        [Required]
        public DateTime? End { get; set; }
    }

    // ----- Shared helpers -----------------------------------------------------

    static void Open(IRenderedComponent<ContainerFragment> cut) => cut.Find(".wss-picker-input").Click();

    static void Commit(IRenderedComponent<ContainerFragment> cut, string inputSelector, string text)
    {
        cut.Find(inputSelector).Input(text);
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });
    }

    // The sr-only validation region for a field, addressed by the input's own id -- exactly what that
    // input's aria-errormessage points at. (Each FieldValidationDisplay renders the class twice, so
    // indexing FindAll would be ambiguous -- mirrors EditDateRangeTests' own MessageFor.)
    static string MessageFor(IRenderedComponent<ContainerFragment> cut, string inputSelector) =>
        cut.Find($"#error-msg-{cut.Find(inputSelector).GetAttribute("id")}").TextContent;

    const string DateInput = ".wss-picker-input-date";
    const string StartInput = ".wss-picker-input-start";
    const string EndInput = ".wss-picker-input-end";

    RenderFragment RenderEditDate(BirthdayModel model, Action<RenderTreeBuilder, int>? extra = null)
    {
        Expression<Func<DateTime?>> field = () => model.Birthday;
        return WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Birthday);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Birthday = v));
            b.AddAttribute(4, "FirstDayOfWeek", DayOfWeek.Sunday);
            b.AddAttribute(5, "Format", "MM/dd/yyyy");
            extra?.Invoke(b, 6);
            b.CloseComponent();
        });
    }

    RenderFragment RenderRange(StayModel model, Action<RenderTreeBuilder, int>? extra = null)
    {
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        return WithForm(model, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "StartChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.Start = v));
            b.AddAttribute(4, "End", model.End);
            b.AddAttribute(5, "EndExpression", endField);
            b.AddAttribute(6, "EndChanged", EventCallback.Factory.Create<DateTime?>(this, v => model.End = v));
            b.AddAttribute(7, "Format", "MM/dd/yyyy");
            extra?.Invoke(b, 8);
            b.CloseComponent();
        });
    }

    // ----- R1/DTE-4: the trigger is a combobox, so aria-expanded is legal on it ------------------

    [Fact]
    public void The_single_date_trigger_is_a_combobox()
    {
        // An <input type="text"> is implicitly a textbox, which does NOT permit aria-expanded --
        // axe's aria-allowed-attr, critical. And because the panel deliberately leaves focus on the
        // field, aria-expanded is the ONLY signal that a dialog opened at all, so dropping it leaves
        // the open state reaching nobody. role="combobox" is what makes the whole trio legal (the
        // same shape Select's own search input already uses).
        var cut = Render<DatePicker>(p => p.Add(c => c.Id, "birthday"));
        var input = cut.Find(DateInput);

        Assert.Equal("combobox", input.GetAttribute("role"));
        Assert.Equal("dialog", input.GetAttribute("aria-haspopup"));
        Assert.Equal("false", input.GetAttribute("aria-expanded"));

        cut.Find(".wss-picker-input").Click();
        Assert.Equal("true", cut.Find(DateInput).GetAttribute("aria-expanded"));
        Assert.Equal("birthday-panel", cut.Find(DateInput).GetAttribute("aria-controls"));
    }

    [Fact]
    public void Both_range_triggers_are_comboboxes()
    {
        var cut = Render<DateRangePicker>(p => p.Add(c => c.Id, "stay"));

        foreach (var selector in new[] { StartInput, EndInput })
        {
            var input = cut.Find(selector);
            Assert.Equal("combobox", input.GetAttribute("role"));
            Assert.Equal("dialog", input.GetAttribute("aria-haspopup"));
            Assert.Equal("false", input.GetAttribute("aria-expanded"));
        }
    }

    // ----- DTE-1: an out-of-range typed date is no longer refused in silence ---------------------

    [Fact]
    public void A_refused_but_well_formed_typed_date_raises_the_range_callback_not_the_parse_one()
    {
        var parseErrors = new List<string>();
        var rangeErrors = new List<string>();
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Min, new DateTime(2026, 2, 1))
            .Add(c => c.Max, new DateTime(2026, 2, 28))
            .Add(c => c.OnParseError, EventCallback.Factory.Create<string>(this, parseErrors.Add))
            .Add(c => c.OnRangeError, EventCallback.Factory.Create<string>(this, rangeErrors.Add)));

        cut.Find(DateInput).Input("03/15/2026"); // parses fine, outside Max
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("03/15/2026", Assert.Single(rangeErrors));
        Assert.Empty(parseErrors); // the two are different situations and must not be conflated
        Assert.Null(cut.Instance.Value); // still reverted -- the signal is additive, not a behavior change
    }

    [Fact]
    public void An_unparseable_entry_still_raises_only_the_parse_callback()
    {
        var parseErrors = new List<string>();
        var rangeErrors = new List<string>();
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Min, new DateTime(2026, 2, 1))
            .Add(c => c.OnParseError, EventCallback.Factory.Create<string>(this, parseErrors.Add))
            .Add(c => c.OnRangeError, EventCallback.Factory.Create<string>(this, rangeErrors.Add)));

        cut.Find(DateInput).Input("not a date");
        cut.Find(".wss-picker").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("not a date", Assert.Single(parseErrors));
        Assert.Empty(rangeErrors);
    }

    [Fact]
    public void EditDate_surfaces_a_refused_typed_date_as_a_validation_message_and_aria_invalid()
    {
        // The whole point of DTE-1: before this, CurrentValue never changed, NotifyFieldChanged never
        // fired and no validator ran, so the field silently reverted and nothing anywhere said why.
        var model = new BirthdayModel { Birthday = Feb14 };
        var cut = Render(RenderEditDate(model, (b, i) =>
        {
            b.AddAttribute(i, "Min", new DateTime(2026, 2, 1));
            b.AddAttribute(i + 1, "Max", new DateTime(2026, 2, 28));
        }));

        Commit(cut, DateInput, "03/15/2026");

        Assert.Contains("must be an allowed date", MessageFor(cut, DateInput));
        var input = cut.Find(DateInput);
        Assert.Equal("true", input.GetAttribute("aria-invalid"));
        Assert.StartsWith("error-msg-", input.GetAttribute("aria-errormessage"));
        Assert.Equal(Feb14, model.Birthday); // unchanged, as before -- only the announcement is new
    }

    [Fact]
    public void EditDate_range_message_is_worded_separately_from_the_parse_message()
    {
        var model = new BirthdayModel { Birthday = Feb14 };
        var cut = Render(RenderEditDate(model, (b, i) =>
        {
            b.AddAttribute(i, "Min", new DateTime(2026, 2, 1));
            b.AddAttribute(i + 1, "RangeErrorMessage", "The {0} field is outside the bookable window.");
        }));

        Commit(cut, DateInput, "01/15/2026");
        Assert.Contains("outside the bookable window", MessageFor(cut, DateInput));

        // ...and a later valid commit clears it, through the same store the parse path uses.
        Commit(cut, DateInput, "02/20/2026");
        Assert.Equal(string.Empty, MessageFor(cut, DateInput));
        Assert.Null(cut.Find(DateInput).GetAttribute("aria-invalid"));
    }

    [Fact]
    public void EditDateRange_surfaces_a_refused_typed_date_on_the_failing_endpoint_only()
    {
        var model = new StayModel { Start = new DateTime(2026, 2, 10), End = new DateTime(2026, 2, 20) };
        var cut = Render(RenderRange(model, (b, i) =>
        {
            b.AddAttribute(i, "Min", new DateTime(2026, 2, 1));
            b.AddAttribute(i + 1, "Max", new DateTime(2026, 2, 28));
        }));

        Open(cut);
        Commit(cut, EndInput, "03/15/2026");

        Assert.Contains("must be an allowed date", MessageFor(cut, EndInput));
        Assert.Equal(string.Empty, MessageFor(cut, StartInput));
        Assert.Equal("true", cut.Find(EndInput).GetAttribute("aria-invalid"));
        Assert.Null(cut.Find(StartInput).GetAttribute("aria-invalid"));
        Assert.Equal(new DateTime(2026, 2, 20), model.End);
    }

    // ----- DTE-2: clearing must not strand focus on <body> ---------------------------------------

    // Every ElementReference.FocusAsync lands here in bUnit's loose JS-interop mode.
    int FocusCalls() => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

    [Fact]
    public void Clearing_the_single_date_field_reclaims_focus_onto_the_input()
    {
        // ShowClear turns false the instant the value goes, so the focused button unmounts. Focus fell
        // to <body>, and since the panel is open (the field opens on focus) Escape could no longer
        // reach the wrapper's keydown -- leaving a live role="dialog" behind a full-viewport backdrop
        // that only a mouse could dismiss.
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Value, Feb14));
        cut.Find(".wss-picker-input").Click(); // opens; the panel stays open across the clear
        var before = FocusCalls();

        cut.Find(".wss-picker-clear").Click();

        Assert.True(FocusCalls() > before);
        Assert.Empty(cut.FindAll(".wss-picker-clear")); // the button really did unmount
        Assert.NotEmpty(cut.FindAll(".wss-picker-dropdown")); // ...while the panel stayed open
    }

    [Fact]
    public void Clearing_the_range_field_reclaims_focus_onto_the_active_input()
    {
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Start, Feb14)
            .Add(c => c.End, Feb14.AddDays(3)));
        cut.Find(".wss-picker-input").Click();
        var before = FocusCalls();

        cut.Find(".wss-picker-clear").Click();

        Assert.True(FocusCalls() > before);
        Assert.Empty(cut.FindAll(".wss-picker-clear"));
    }

    // ----- DTE-5: the End input keeps the control's shared description -------------------------

    [Fact]
    public void The_range_end_input_references_the_shared_description_element()
    {
        // Description/Tooltip are written once for the control and rendered once by the single
        // FormLabel (ids derived from the Start id). aria-describedby is a REFERENCE, not ownership,
        // so both inputs may point at them -- building End's chain from nulls announced the field's
        // instructions on Start and dropped them entirely on End.
        var model = new StayModel();
        var cut = Render(RenderRange(model, (b, i) =>
        {
            b.AddAttribute(i, "Description", "Both dates are inclusive.");
            b.AddAttribute(i + 1, "Tooltip", "Nights are counted from the start date.");
        }));

        var startId = cut.Find(StartInput).GetAttribute("id")!;
        var endId = cut.Find(EndInput).GetAttribute("id")!;
        var endChain = cut.Find(EndInput).GetAttribute("aria-describedby")!.Split(' ');

        Assert.Equal($"error-msg-{endId}", endChain[0]);   // its OWN error message stays first
        Assert.Contains($"desc-{startId}", endChain);      // ...then the shared description
        Assert.Contains($"tooltip-{startId}", endChain);
        Assert.DoesNotContain($"desc-{endId}", endChain);  // never an element nothing renders
        Assert.NotNull(cut.Find($"#desc-{startId}"));
        Assert.NotNull(cut.Find($"#tooltip-{startId}"));
    }

    // ----- DTE-6: the two inputs are one named group -------------------------------------------

    [Fact]
    public void EditDateRange_wraps_both_inputs_in_a_group_named_from_the_label_text_anchor()
    {
        // One visible FormLabel, and label[for] can only associate with the Start input -- so without
        // a group the End input read as an unrelated field. The name points at lbltext-{id} (the
        // label TEXT) rather than the whole <label>, which also contains the tooltip trigger.
        var model = new StayModel();
        var cut = Render(RenderRange(model, (b, i) => b.AddAttribute(i, "Label", "Stay Dates")));

        var startId = cut.Find(StartInput).GetAttribute("id")!;
        var group = cut.Find(".wss-picker-input");
        Assert.Equal("group", group.GetAttribute("role"));
        Assert.Equal($"lbltext-{startId}", group.GetAttribute("aria-labelledby"));
        Assert.Equal("Stay Dates", cut.Find($"#lbltext-{startId}").TextContent.Trim());
    }

    [Fact]
    public void A_standalone_range_picker_takes_no_group_role_because_nothing_names_it()
    {
        // An unnamed role="group" is a boundary announcement with no information in it, so the role
        // is omitted entirely rather than rendered empty.
        var cut = Render<DateRangePicker>();
        Assert.Null(cut.Find(".wss-picker-input").GetAttribute("role"));

        var named = Render<DateRangePicker>(p => p.Add(c => c.GroupLabel, "Reporting period"));
        Assert.Equal("group", named.Find(".wss-picker-input").GetAttribute("role"));
        Assert.Equal("Reporting period", named.Find(".wss-picker-input").GetAttribute("aria-label"));
    }

    // ----- DTE-7: each popup names itself after its own field -----------------------------------

    [Fact]
    public void The_single_date_dialog_is_named_from_the_resolved_field_label()
    {
        // "Choose date" on every date field of a form meant three popups announcing identically.
        var model = new BirthdayModel();
        var cut = Render(RenderEditDate(model));
        Open(cut);

        Assert.Equal("Choose Birth Date", cut.Find(".wss-picker-dropdown").GetAttribute("aria-label"));
    }

    [Fact]
    public void An_explicit_DialogLabel_still_wins_as_the_localization_override()
    {
        var model = new BirthdayModel();
        var cut = Render(RenderEditDate(model, (b, i) => b.AddAttribute(i, "DialogLabel", "Kalender öffnen")));
        Open(cut);

        Assert.Equal("Kalender öffnen", cut.Find(".wss-picker-dropdown").GetAttribute("aria-label"));
    }

    [Fact]
    public void The_range_dialog_is_named_from_the_resolved_control_label()
    {
        var model = new StayModel();
        var cut = Render(RenderRange(model, (b, i) => b.AddAttribute(i, "Label", "Stay Dates")));
        Open(cut);

        Assert.Equal("Choose Stay Dates", cut.Find(".wss-picker-dropdown").GetAttribute("aria-label"));
    }

    // ----- DTE-8: Min/Max reach someone TYPING, not just someone clicking cells ------------------

    [Fact]
    public void The_bounds_are_described_as_text_alongside_the_format()
    {
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Id, "trip")
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.Min, new DateTime(2026, 2, 1))
            .Add(c => c.Max, new DateTime(2026, 2, 28)));

        var hint = cut.Find("#trip-format");
        Assert.Equal("Format: MM/dd/yyyy. Earliest date: 02/01/2026. Latest date: 02/28/2026", hint.TextContent);
        // Still one element, still last in the chain -- the describedby contract is unchanged.
        Assert.Equal("trip-format", cut.Find(DateInput).GetAttribute("aria-describedby"));
    }

    [Fact]
    public void One_sided_bounds_describe_only_the_side_that_exists()
    {
        var minOnly = Render<DatePicker>(p => p
            .Add(c => c.Id, "a").Add(c => c.Format, "MM/dd/yyyy").Add(c => c.Min, new DateTime(2026, 2, 1)));
        Assert.Equal("Format: MM/dd/yyyy. Earliest date: 02/01/2026", minOnly.Find("#a-format").TextContent);

        var maxOnly = Render<DatePicker>(p => p
            .Add(c => c.Id, "b").Add(c => c.Format, "MM/dd/yyyy").Add(c => c.Max, new DateTime(2026, 2, 28)));
        Assert.Equal("Format: MM/dd/yyyy. Latest date: 02/28/2026", maxOnly.Find("#b-format").TextContent);
    }

    [Fact]
    public void The_range_hint_is_absent_where_the_bounds_are_not_enforced_or_are_blanked()
    {
        // Time mode ignores Min/Max outright, so naming them would describe a constraint that isn't
        // applied; blanking a label drops that clause exactly as blanking FormatHintLabel always did.
        var time = Render<DatePicker>(p => p
            .Add(c => c.Id, "t").Add(c => c.Mode, DatePickerMode.Time).Add(c => c.Min, new DateTime(2026, 2, 1)));
        Assert.DoesNotContain("Earliest", time.Find("#t-format").TextContent, StringComparison.Ordinal);

        var blanked = Render<DatePicker>(p => p
            .Add(c => c.Id, "c")
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FormatHintLabel, string.Empty)
            .Add(c => c.RangeHintMinLabel, string.Empty)
            .Add(c => c.RangeHintMaxLabel, string.Empty)
            .Add(c => c.Min, new DateTime(2026, 2, 1)));
        Assert.Empty(blanked.FindAll("#c-format"));
        Assert.Null(blanked.Find(DateInput).GetAttribute("aria-describedby"));
    }

    [Fact]
    public void EditDate_forwards_the_resolved_bounds_into_the_hint()
    {
        var model = new BirthdayModel();
        var cut = Render(RenderEditDate(model, (b, i) =>
        {
            b.AddAttribute(i, "Min", new DateTime(2026, 2, 1));
            b.AddAttribute(i + 1, "Max", new DateTime(2026, 2, 28));
        }));

        var hintId = cut.Find(DateInput).GetAttribute("aria-describedby")!.Split(' ').Last();
        Assert.Contains("Earliest date: 02/01/2026", cut.Find($"#{hintId}").TextContent);
        Assert.Contains("Latest date: 02/28/2026", cut.Find($"#{hintId}").TextContent);
    }

    // ----- DTE-9: the native month input's fallback needs a format hint --------------------------

    [Fact]
    public void EditDateNative_month_describes_its_strict_parse_format()
    {
        // A browser without native month support falls back to a plain text box with no affordance,
        // while the parse demands a strict invariant "yyyy-MM".
        var model = new MonthModel();
        Expression<Func<DateTime?>> field = () => model.Period;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Period);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Month);
            b.CloseComponent();
        }));

        var input = cut.Find("input");
        Assert.Equal("month", input.GetAttribute("type"));
        var chain = input.GetAttribute("aria-describedby")!.Split(' ');
        Assert.Equal("format-Period", chain.Last()); // appended, so the error/description read first
        Assert.Equal("Format: yyyy-MM", cut.Find("#format-Period").TextContent);
    }

    [Fact]
    public void EditDateNative_date_type_keeps_its_describedby_untouched()
    {
        var model = new BirthdayModel();
        Expression<Func<DateTime?>> field = () => model.Birthday;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Birthday);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("#format-Birthday"));
        Assert.DoesNotContain("format-", cut.Find("input").GetAttribute("aria-describedby")!, StringComparison.Ordinal);
    }

    // ----- DTE-10: autocomplete is reachable (WCAG 1.3.5) ---------------------------------------

    [Fact]
    public void The_picker_input_takes_an_autocomplete_token_instead_of_a_hardcoded_off()
    {
        Assert.Equal("off", Render<DatePicker>().Find(DateInput).GetAttribute("autocomplete"));
        Assert.Equal("bday", Render<DatePicker>(p => p.Add(c => c.Autocomplete, "bday"))
            .Find(DateInput).GetAttribute("autocomplete"));
    }

    [Fact]
    public void EditDate_resolves_autocomplete_from_the_parameter_then_the_model_attribute()
    {
        var model = new AutocompleteModel();
        Expression<Func<DateTime?>> field = () => model.Birthday;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Birthday);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));
        Assert.Equal("bday", cut.Find(DateInput).GetAttribute("autocomplete"));

        var overridden = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Birthday);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Autocomplete", "off");
            b.CloseComponent();
        }));
        Assert.Equal("off", overridden.Find(DateInput).GetAttribute("autocomplete"));
    }

    [Fact]
    public void The_range_picker_takes_one_autocomplete_token_per_input()
    {
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.StartAutocomplete, "cc-exp-month")
            .Add(c => c.EndAutocomplete, "cc-exp-year"));

        Assert.Equal("cc-exp-month", cut.Find(StartInput).GetAttribute("autocomplete"));
        Assert.Equal("cc-exp-year", cut.Find(EndInput).GetAttribute("autocomplete"));
    }

    // ----- DTE-11: the two panels' selects are told apart ---------------------------------------

    [Fact]
    public void Each_range_panel_names_its_month_and_year_selects_after_its_own_view()
    {
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.Format, "MM/dd/yyyy")
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Feb14));
        cut.Find(".wss-picker-input").Click();

        var names = cut.FindAll(".wss-picker-month-header select")
            .Select(s => s.GetAttribute("aria-label")!)
            .ToList();

        Assert.Equal(4, names.Count);
        Assert.Equal(4, names.Distinct(StringComparer.Ordinal).Count()); // was two names across four boxes
        Assert.Contains("Month, February 2026", names);
        Assert.Contains("Year, February 2026", names);
        Assert.Contains("Month, March 2026", names);
        Assert.Contains("Year, March 2026", names);
    }

    [Fact]
    public void Month_mode_panels_suffix_their_year_selects_with_their_own_year()
    {
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Month)
            .Add(c => c.Start, Feb14));
        cut.Find(".wss-picker-input").Click();

        var names = cut.FindAll(".wss-picker-month-header select")
            .Select(s => s.GetAttribute("aria-label")!)
            .ToList();

        Assert.Equal(new[] { "Year, 2026", "Year, 2027" }, names);
    }

    // ----- DTE-13: the star and aria-required must agree ----------------------------------------

    [Fact]
    public void Required_on_the_End_field_alone_still_raises_the_shared_star()
    {
        // One visible label serves both fields, so a [Required] on either has to raise it -- otherwise
        // the sighted user sees an optional field while the End input announces itself as required.
        var model = new EndRequiredStayModel();
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
        // aria-required stays strictly per-field: only the End input carries it.
        Assert.Null(cut.Find(StartInput).GetAttribute("aria-required"));
        Assert.Equal("true", cut.Find(EndInput).GetAttribute("aria-required"));
    }

    // ----- DTE-14: the End summary anchor has to exist ------------------------------------------

    [Fact]
    public void Read_only_mode_anchors_the_End_field_on_an_element_that_actually_renders()
    {
        // Read-only renders ONE ReadOnlyValue carrying the Start id and showing "start - end"; the
        // "-end" id belongs to an input that doesn't exist there, so a ValidationView link pointing at
        // it went nowhere.
        var model = new StayModel { Start = Feb14, End = Feb14.AddDays(3) };
        var formOptions = new FormOptions();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.AddAttribute(5, "IsEditMode", false);
            b.CloseComponent();
        }));

        var endAnchor = formOptions.FieldIds.Single(kv => kv.Key.FieldName == "End").Value;
        var startAnchor = formOptions.FieldIds.Single(kv => kv.Key.FieldName == "Start").Value;
        Assert.Equal(startAnchor, endAnchor);
        Assert.NotNull(cut.Find($"#{endAnchor}"));
    }

    [Fact]
    public void Edit_mode_still_anchors_the_End_field_on_its_own_input()
    {
        var model = new StayModel { Start = Feb14 };
        var formOptions = new FormOptions();
        Expression<Func<DateTime?>> startField = () => model.Start;
        Expression<Func<DateTime?>> endField = () => model.End;
        var cut = Render(WithForm(model, formOptions, b =>
        {
            b.OpenComponent<EditDateRange>(0);
            b.AddAttribute(1, "Start", model.Start);
            b.AddAttribute(2, "StartExpression", startField);
            b.AddAttribute(3, "End", model.End);
            b.AddAttribute(4, "EndExpression", endField);
            b.CloseComponent();
        }));

        var endAnchor = formOptions.FieldIds.Single(kv => kv.Key.FieldName == "End").Value;
        Assert.EndsWith("-end", endAnchor);
        Assert.Equal(endAnchor, cut.Find(EndInput).GetAttribute("id"));
    }

    // ----- DTE-15: a default date on a non-nullable binding is empty, not 01/01/0001 -------------

    [Fact]
    public void A_default_date_on_a_non_nullable_binding_reads_as_empty_and_offers_no_clear()
    {
        // A non-nullable T can't hold null, so "Clear date" writes default(T) back -- and the field
        // then showed 01/01/0001 with the clear button still offered, so clearing appeared to do
        // nothing at all.
        var model = new NonNullableDateModel { ShipDate = new DateTime(2026, 3, 5) };
        Expression<Func<DateTime>> field = () => model.ShipDate;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<DateTime>>(0);
            b.AddAttribute(1, "Value", model.ShipDate);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<DateTime>(this, v => model.ShipDate = v));
            b.AddAttribute(4, "Format", "MM/dd/yyyy");
            b.CloseComponent();
        }));

        Assert.Equal("03/05/2026", cut.Find(DateInput).GetAttribute("value"));

        cut.Find(".wss-picker-clear").Click();

        Assert.Equal(default(DateTime), model.ShipDate);
        Assert.True(string.IsNullOrEmpty(cut.Find(DateInput).GetAttribute("value")));
        Assert.Empty(cut.FindAll(".wss-picker-clear"));
    }

    [Fact]
    public void A_non_nullable_TimeOnly_midnight_is_still_a_real_value()
    {
        // The one exemption: default(TimeOnly) is 00:00, an entirely legitimate time-of-day, so
        // blanking it would make midnight unrepresentable.
        var model = new TimeModel { ShipTime = default };
        Expression<Func<TimeOnly>> field = () => model.ShipTime;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDate<TimeOnly>>(0);
            b.AddAttribute(1, "Value", model.ShipTime);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Type", InputDateType.Time);
            b.CloseComponent();
        }));

        Assert.False(string.IsNullOrEmpty(cut.Find(DateInput).GetAttribute("value")));
        Assert.NotEmpty(cut.FindAll(".wss-picker-clear"));
    }

    class TimeModel { public TimeOnly ShipTime { get; set; } }

    // ----- DTE-16: Week mode exposes the number the field displays -------------------------------

    [Fact]
    public void Week_mode_exposes_each_rows_week_number_as_a_row_header()
    {
        // The ROW is the selection unit in Week mode and the field displays "2026-W08", so without
        // this the grid and the bound value cannot be correlated at all.
        var cut = Render<DatePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14));
        cut.Find(".wss-picker-input").Click();

        var headers = cut.FindAll(".wss-picker-week-no");
        Assert.Equal(6, headers.Count);
        Assert.All(headers, h =>
        {
            Assert.Equal("rowheader", h.GetAttribute("role"));
            Assert.Null(h.GetAttribute("aria-hidden"));
            Assert.Equal($"Week {h.TextContent}", h.GetAttribute("aria-label"));
        });
    }

    [Fact]
    public void ShowWeekNumbers_in_Date_mode_keeps_the_column_decorative()
    {
        // A day click still commits the day there, so the number is context, not structure.
        var cut = Render<DatePicker>(p => p
            .Add(c => c.ShowWeekNumbers, true)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Value, Feb14));
        cut.Find(".wss-picker-input").Click();

        Assert.All(cut.FindAll(".wss-picker-week-no"), h =>
        {
            Assert.Null(h.GetAttribute("role"));
            Assert.Equal("true", h.GetAttribute("aria-hidden"));
            Assert.Null(h.GetAttribute("aria-label"));
        });
    }

    [Fact]
    public void The_range_pickers_Week_mode_exposes_the_same_row_headers()
    {
        var cut = Render<DateRangePicker>(p => p
            .Add(c => c.Mode, DatePickerMode.Week)
            .Add(c => c.FirstDayOfWeek, DayOfWeek.Sunday)
            .Add(c => c.Start, Feb14));
        cut.Find(".wss-picker-input").Click();

        var headers = cut.FindAll(".wss-picker-week-no");
        Assert.Equal(12, headers.Count); // six rows per panel, two panels
        Assert.All(headers, h => Assert.Equal("rowheader", h.GetAttribute("role")));
        Assert.All(headers, h => Assert.Equal($"Week {h.TextContent}", h.GetAttribute("aria-label")));
    }

    // ----- Wave 1: editors name themselves from the label TEXT anchor ---------------------------

    [Fact]
    public void EditDate_names_its_input_from_the_label_text_anchor_unless_overridden()
    {
        // lbltext-{id} is the label text alone; the <label> itself also contains the tooltip trigger,
        // whose own name would otherwise be concatenated into the field's.
        var model = new BirthdayModel();
        var cut = Render(RenderEditDate(model, (b, i) => b.AddAttribute(i, "Tooltip", "Used for age checks")));

        var input = cut.Find(DateInput);
        Assert.Equal("lbltext-Birthday", input.GetAttribute("aria-labelledby"));
        Assert.Equal("Birth Date", cut.Find("#lbltext-Birthday").TextContent.Trim());

        // An explicit InputLabel has to suppress it -- aria-labelledby wins over aria-label, so
        // leaving both would make the override inert.
        var overridden = Render(RenderEditDate(model, (b, i) => b.AddAttribute(i, "InputLabel", "Custom name")));
        Assert.Null(overridden.Find(DateInput).GetAttribute("aria-labelledby"));
        Assert.Equal("Custom name", overridden.Find(DateInput).GetAttribute("aria-label"));
    }

    [Fact]
    public void EditDateNative_names_its_input_from_the_label_text_anchor()
    {
        var model = new BirthdayModel();
        Expression<Func<DateTime?>> field = () => model.Birthday;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditDateNative<DateTime?>>(0);
            b.AddAttribute(1, "Value", model.Birthday);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Tooltip", "Used for age checks");
            b.CloseComponent();
        }));

        Assert.Equal("lbltext-Birthday", cut.Find("input").GetAttribute("aria-labelledby"));
        Assert.Equal("Birth Date", cut.Find("#lbltext-Birthday").TextContent.Trim());
    }
}
