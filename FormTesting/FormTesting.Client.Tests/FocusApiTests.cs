using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;

namespace FormTesting.Client.Tests;

/// <summary>
/// Covers the public <c>FocusAsync()</c> every Edit* control exposes, and the <c>FocusOnFirstRender</c>
/// parameter that calls it once after first render.
/// </summary>
/// <remarks>
/// <para>
/// <b>What these tests can and cannot prove.</b> Both focus channels bottom out in JS interop —
/// <see cref="ElementReference"/>'s own <c>FocusAsync</c> calls
/// <c>Blazor._internal.domWrapper.focus</c>, and the radio/checkbox groups call
/// <c>WssEditControls.focusGroupInput</c>. bUnit runs interop in loose mode against no browser, so
/// nothing here can observe <c>document.activeElement</c> actually moving. What it CAN observe is that
/// the right call was issued, with the right arguments, exactly once — and that the wrong ones weren't
/// (a read-only control must issue none at all). Proof that focus really lands is
/// <c>FocusApiE2ETests</c>'s job.
/// </para>
/// <para>
/// The other half is the contract that <c>FocusAsync()</c> never throws: on an unrendered/read-only
/// control, on a disabled one, and when called repeatedly. Those are real assertions here, since the
/// swallowing happens entirely in C#.
/// </para>
/// </remarks>
public class FocusApiTests : BunitContext
{
    public FocusApiTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    // ElementReference.FocusAsync()'s interop identifier -- the channel every single-element control
    // uses. Counted rather than asserted-once so a test can pin "no focus happened" too.
    int ElementFocusCalls() => JSInterop.Invocations.Count(i => i.Identifier == "Blazor._internal.domWrapper.focus");

    // The group channel, for the four radio groups and the two checkbox lists.
    List<JSRuntimeInvocation> GroupFocusCalls() =>
        [.. JSInterop.Invocations.Where(i => i.Identifier == "WssEditControls.focusGroupInput")];

    class FocusModel
    {
        public string Text { get; set; } = "Alice";
        public string? Color { get; set; } = "#ff0000";
        public int? Number { get; set; } = 7;
        public int Volume { get; set; } = 40;
        public bool Flag { get; set; }
        public bool? Tri { get; set; }
        public DateTime? Date { get; set; } = new DateTime(2026, 3, 4);
        public DateTime? End { get; set; } = new DateTime(2026, 3, 8);
        public Priority? Priority { get; set; } = Tests.Priority.Medium;
        public List<string> Tags { get; set; } = ["a"];
        public List<Color> Colors { get; set; } = [];
        public List<IBrowserFile> Files { get; set; } = [];
    }

    // Renders `inner` inside an EditForm and hands back the cut. Every control below binds against the
    // one model instance so the field expressions stay simple.
    IRenderedComponent<ContainerFragment> RenderControl(FocusModel model, RenderFragment inner) =>
        Render(WithForm(model, inner));

    // Calls the control's FocusAsync on the renderer's dispatcher, the way a consumer's own
    // OnAfterRenderAsync would.
    Task Focus<TComponent>(IRenderedComponent<ContainerFragment> cut, Func<TComponent, ValueTask> call)
        where TComponent : IComponent =>
        cut.InvokeAsync(() => call(cut.FindComponent<TComponent>().Instance).AsTask());

    // ───────────────────────── the single-element controls ─────────────────────────

    // One entry per control that focuses a captured ElementReference. Each renders the control and
    // returns the cut; the shared facts below drive every one of them through the same assertions, so
    // a control added without a focus target fails here rather than silently no-op'ing in production.
    public static TheoryData<string> ElementFocusControls =>
    [
        nameof(EditString), nameof(EditTextArea), "EditNumber", "EditDateNative", nameof(EditBool),
        "EditSelect", "EditSelectEnum", "EditSelectString", "EditRange", nameof(EditColor),
        "EditDate", "EditSelectSearch", "EditMultiSelect", nameof(EditFile), nameof(EditDateRange)
    ];

    IRenderedComponent<ContainerFragment> RenderByName(string control, FocusModel model, bool isEditMode = true)
    {
        Expression<Func<string>> text = () => model.Text;
        Expression<Func<string?>> color = () => model.Color;
        Expression<Func<int?>> number = () => model.Number;
        Expression<Func<int>> volume = () => model.Volume;
        Expression<Func<bool>> flag = () => model.Flag;
        Expression<Func<DateTime?>> date = () => model.Date;
        Expression<Func<DateTime?>> end = () => model.End;
        Expression<Func<Priority?>> priority = () => model.Priority;
        Expression<Func<List<string>>> tags = () => model.Tags;
        Expression<Func<List<IBrowserFile>>> files = () => model.Files;

        return RenderControl(model, b =>
        {
            switch (control)
            {
                case nameof(EditString):
                    b.OpenComponent<EditString>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    break;
                case nameof(EditTextArea):
                    b.OpenComponent<EditTextArea>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    break;
                case "EditNumber":
                    b.OpenComponent<EditNumber<int?>>(0);
                    b.AddAttribute(1, "Value", model.Number);
                    b.AddAttribute(2, "ValueExpression", number);
                    break;
                case "EditDateNative":
                    b.OpenComponent<EditDateNative<DateTime?>>(0);
                    b.AddAttribute(1, "Value", model.Date);
                    b.AddAttribute(2, "ValueExpression", date);
                    break;
                case nameof(EditBool):
                    b.OpenComponent<EditBool>(0);
                    b.AddAttribute(1, "Value", model.Flag);
                    b.AddAttribute(2, "ValueExpression", flag);
                    break;
                case "EditSelect":
                    b.OpenComponent<EditSelect<string>>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
                    {
                        cb.OpenElement(0, "option");
                        cb.AddAttribute(1, "value", "Alice");
                        cb.AddContent(2, "Alice");
                        cb.CloseElement();
                    }));
                    break;
                case "EditSelectEnum":
                    b.OpenComponent<EditSelectEnum<Priority?>>(0);
                    b.AddAttribute(1, "Value", model.Priority);
                    b.AddAttribute(2, "ValueExpression", priority);
                    break;
                case "EditSelectString":
                    b.OpenComponent<EditSelectString<string>>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    b.AddAttribute(3, "Options", new List<string> { "Alice", "Bob" });
                    break;
                case "EditRange":
                    b.OpenComponent<EditRange<int>>(0);
                    b.AddAttribute(1, "Value", model.Volume);
                    b.AddAttribute(2, "ValueExpression", volume);
                    break;
                case nameof(EditColor):
                    b.OpenComponent<EditColor>(0);
                    b.AddAttribute(1, "Value", model.Color);
                    b.AddAttribute(2, "ValueExpression", color);
                    break;
                case "EditDate":
                    b.OpenComponent<EditDate<DateTime?>>(0);
                    b.AddAttribute(1, "Value", model.Date);
                    b.AddAttribute(2, "ValueExpression", date);
                    break;
                case "EditSelectSearch":
                    b.OpenComponent<EditSelectSearch<Priority?>>(0);
                    b.AddAttribute(1, "Value", model.Priority);
                    b.AddAttribute(2, "ValueExpression", priority);
                    b.AddAttribute(3, "Options", new List<SelectOption<Priority?>> { new(Tests.Priority.Medium, "Medium") });
                    break;
                case "EditMultiSelect":
                    b.OpenComponent<EditMultiSelect<string>>(0);
                    b.AddAttribute(1, "Value", model.Tags);
                    b.AddAttribute(2, "ValueExpression", tags);
                    b.AddAttribute(3, "Options", new List<SelectOption<string>> { new("a", "A") });
                    break;
                case nameof(EditFile):
                    b.OpenComponent<EditFile>(0);
                    b.AddAttribute(1, "Value", model.Files);
                    b.AddAttribute(2, "ValueExpression", files);
                    break;
                case nameof(EditDateRange):
                    b.OpenComponent<EditDateRange>(0);
                    b.AddAttribute(1, "Start", model.Date);
                    b.AddAttribute(2, "StartExpression", date);
                    b.AddAttribute(3, "End", model.End);
                    b.AddAttribute(4, "EndExpression", end);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(control), control, "Unmapped control");
            }
            if (!isEditMode) b.AddAttribute(90, "IsEditMode", false);
            b.CloseComponent();
        });
    }

    // Dispatches FocusAsync through the non-generic surface -- these controls share no base type, and
    // the four that declare it independently (the two bases plus EditRadio and EditDateRange) can't be
    // reached through one interface. Reflection-free: each case names the type it renders.
    static ValueTask FocusOf(string control, IRenderedComponent<ContainerFragment> cut) => control switch
    {
        nameof(EditString) => cut.FindComponent<EditString>().Instance.FocusAsync(),
        nameof(EditTextArea) => cut.FindComponent<EditTextArea>().Instance.FocusAsync(),
        "EditNumber" => cut.FindComponent<EditNumber<int?>>().Instance.FocusAsync(),
        "EditDateNative" => cut.FindComponent<EditDateNative<DateTime?>>().Instance.FocusAsync(),
        nameof(EditBool) => cut.FindComponent<EditBool>().Instance.FocusAsync(),
        "EditSelect" => cut.FindComponent<EditSelect<string>>().Instance.FocusAsync(),
        "EditSelectEnum" => cut.FindComponent<EditSelectEnum<Priority?>>().Instance.FocusAsync(),
        "EditSelectString" => cut.FindComponent<EditSelectString<string>>().Instance.FocusAsync(),
        "EditRange" => cut.FindComponent<EditRange<int>>().Instance.FocusAsync(),
        nameof(EditColor) => cut.FindComponent<EditColor>().Instance.FocusAsync(),
        "EditDate" => cut.FindComponent<EditDate<DateTime?>>().Instance.FocusAsync(),
        "EditSelectSearch" => cut.FindComponent<EditSelectSearch<Priority?>>().Instance.FocusAsync(),
        "EditMultiSelect" => cut.FindComponent<EditMultiSelect<string>>().Instance.FocusAsync(),
        nameof(EditFile) => cut.FindComponent<EditFile>().Instance.FocusAsync(),
        nameof(EditDateRange) => cut.FindComponent<EditDateRange>().Instance.FocusAsync(),
        _ => throw new ArgumentOutOfRangeException(nameof(control), control, "Unmapped control")
    };

    [Theory]
    [MemberData(nameof(ElementFocusControls))]
    public async Task FocusAsync_issues_exactly_one_element_focus_per_call(string control)
    {
        var cut = RenderByName(control, new FocusModel());
        var before = ElementFocusCalls();

        await cut.InvokeAsync(() => FocusOf(control, cut).AsTask());

        Assert.Equal(before + 1, ElementFocusCalls());
    }

    [Theory]
    [MemberData(nameof(ElementFocusControls))]
    public async Task FocusAsync_is_repeatable(string control)
    {
        // Nothing latches: a command palette that reopens over the same control focuses it again.
        var cut = RenderByName(control, new FocusModel());
        var before = ElementFocusCalls();

        await cut.InvokeAsync(() => FocusOf(control, cut).AsTask());
        await cut.InvokeAsync(() => FocusOf(control, cut).AsTask());

        Assert.Equal(before + 2, ElementFocusCalls());
    }

    [Theory]
    [MemberData(nameof(ElementFocusControls))]
    public async Task FocusAsync_is_a_silent_no_op_in_read_only_mode(string control)
    {
        // Read-only renders no editor at all, so there is nothing to focus. The contract is that this
        // is a no-op rather than a throw -- a consumer focusing a field can't be expected to know
        // whether a cascaded FormOptions.IsEditMode has flipped it to a display value.
        var cut = RenderByName(control, new FocusModel(), isEditMode: false);
        var before = ElementFocusCalls();

        await cut.InvokeAsync(() => FocusOf(control, cut).AsTask());

        Assert.Equal(before, ElementFocusCalls());
    }

    // ───────────────────────── the group controls (radio / checkbox lists) ─────────────────────────

    [Fact]
    public async Task EditRadio_focuses_the_checked_radio_inside_its_own_fieldset()
    {
        var model = new FocusModel();
        Expression<Func<string>> field = () => model.Text;
        var cut = RenderControl(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "Alice");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        });

        await Focus<EditRadio<string>>(cut, c => c.FocusAsync());

        var call = Assert.Single(GroupFocusCalls());
        // The container is the control's own resolved id -- the fieldset RadioAria puts it on.
        Assert.Equal("Text", call.Arguments[0]);
        Assert.Equal("input[type=radio]", call.Arguments[1]);
        Assert.Equal(true, call.Arguments[2]); // prefer the checked radio: real radiogroup tab semantics
    }

    [Fact]
    public async Task EditRadioEnum_and_EditRadioString_share_EditRadios_group_focus_contract()
    {
        var model = new FocusModel();
        Expression<Func<Priority?>> priority = () => model.Priority;
        Expression<Func<string>> text = () => model.Text;

        var enumCut = RenderControl(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", priority);
            b.CloseComponent();
        });
        await Focus<EditRadioEnum<Priority?>>(enumCut, c => c.FocusAsync());

        var stringCut = RenderControl(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", text);
            b.AddAttribute(3, "Options", new List<string> { "Alice", "Bob" });
            b.CloseComponent();
        });
        await Focus<EditRadioString>(stringCut, c => c.FocusAsync());

        var calls = GroupFocusCalls();
        Assert.Equal(2, calls.Count);
        Assert.Equal(["Priority", "Text"], calls.Select(c => c.Arguments[0]));
        // Same selector and same checked-preference as EditRadio -- the whole point of routing all
        // four radio groups through one helper is that they can't drift apart.
        Assert.All(calls, c => Assert.Equal("input[type=radio]", c.Arguments[1]));
        Assert.All(calls, c => Assert.Equal(true, c.Arguments[2]));
    }

    [Fact]
    public async Task EditBoolNullRadio_uses_the_same_radio_group_focus_as_the_other_three()
    {
        var model = new FocusModel();
        Expression<Func<bool?>> field = () => model.Tri;
        var cut = RenderControl(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.Tri);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        });

        await Focus<EditBoolNullRadio>(cut, c => c.FocusAsync());

        var call = Assert.Single(GroupFocusCalls());
        Assert.Equal("Tri", call.Arguments[0]);
        Assert.Equal("input[type=radio]", call.Arguments[1]);
        Assert.Equal(true, call.Arguments[2]);
    }

    [Fact]
    public async Task Checked_lists_focus_the_first_enabled_checkbox_not_the_first_checked_one()
    {
        // preferChecked FALSE is the deliberate difference from the radio groups: every checkbox is
        // its own tab stop, so entering the group means the top of the list, whatever is ticked.
        var model = new FocusModel { Tags = ["b"] };
        Expression<Func<List<string>>> tags = () => model.Tags;
        Expression<Func<List<Color>>> colors = () => model.Colors;

        var stringCut = RenderControl(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", tags);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        });
        await Focus<EditCheckedStringList>(stringCut, c => c.FocusAsync());

        var enumCut = RenderControl(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", colors);
            b.CloseComponent();
        });
        await Focus<EditCheckedEnumList<Color>>(enumCut, c => c.FocusAsync());

        var calls = GroupFocusCalls();
        Assert.Equal(2, calls.Count);
        Assert.Equal(["Tags", "Colors"], calls.Select(c => c.Arguments[0]));
        Assert.All(calls, c => Assert.Equal("input[type=checkbox]", c.Arguments[1]));
        Assert.All(calls, c => Assert.Equal(false, c.Arguments[2]));
    }

    [Fact]
    public async Task A_read_only_radio_group_still_calls_through_and_resolves_to_nothing()
    {
        // Read-only renders no fieldset id at all (RadioAria.Fieldset returns null), so the container
        // lookup finds nothing and the JS side no-ops. The C# side must not special-case it: whether an
        // element exists is a DOM question, and the answer can change between the call and the render.
        var model = new FocusModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = RenderControl(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        });

        await Focus<EditRadioEnum<Priority?>>(cut, c => c.FocusAsync());

        Assert.Single(GroupFocusCalls()); // issued, harmless, and no exception reached the caller
    }

    // ─────────────────── the native `autofocus` attribute must still splat ───────────────────

    // Renders an EditString carrying one extra unmatched attribute, and hands back the inner input.
    IElement RenderWithExtraAttribute(FocusModel model, string name, object value)
    {
        var cut = RenderControl(model, b =>
        {
            b.OpenComponent<EditString>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", (Expression<Func<string>>)(() => model.Text));
            b.AddAttribute(3, name, value);
            b.CloseComponent();
        });
        return cut.Find("input#Text");
    }

    [Fact]
    public void Native_autofocus_reaches_the_input_and_does_not_bind_to_a_component_parameter()
    {
        // THE regression pin for a whole defect class. Blazor matches component parameter names
        // case-INSENSITIVELY -- both the Razor compiler and ComponentProperties, which looks names up
        // with StringComparer.OrdinalIgnoreCase. So a [Parameter] spelled `AutoFocus` silently SWALLOWS
        // the native `autofocus` attribute: `<EditString autofocus />` compiles to a parameter
        // assignment, the attribute never reaches the DOM, and the browser's own pre-hydration focus is
        // replaced by a post-first-render JS call. (`autofocus="autofocus"` was worse: a build error on
        // a literal, an InvalidOperationException string->bool cast from a runtime splat.) The
        // parameter is named FocusOnFirstRender precisely so this can't happen -- no HTML attribute is
        // spelled that way.
        var model = new FocusModel();

        var input = RenderWithExtraAttribute(model, "autofocus", true);

        Assert.True(input.HasAttribute("autofocus"));
        Assert.Equal(0, ElementFocusCalls()); // native attribute only -- no programmatic focus
    }

    [Fact]
    public void Native_autofocus_with_a_string_value_reaches_the_input_too()
    {
        // The XHTML spelling `autofocus="autofocus"`, which is also the shape a wrapper component
        // produces when it splats a runtime attribute dictionary rather than writing the literal.
        var model = new FocusModel();

        var input = RenderWithExtraAttribute(model, "autofocus", "autofocus");

        Assert.Equal("autofocus", input.GetAttribute("autofocus"));
        Assert.Equal(0, ElementFocusCalls());
    }

    // ───────────────────────────── FocusOnFirstRender ─────────────────────────────

    [Fact]
    public void FocusOnFirstRender_defaults_off_so_rendering_a_control_focuses_nothing()
    {
        RenderByName(nameof(EditString), new FocusModel());

        Assert.Equal(0, ElementFocusCalls());
    }

    [Theory]
    [InlineData(nameof(EditString))]
    [InlineData("EditNumber")]
    [InlineData(nameof(EditBool))]
    [InlineData(nameof(EditFile))]        // EditControlListBase's copy of the hook
    [InlineData(nameof(EditDateRange))]   // EditDateRange's own copy (it shares no base with the rest)
    [InlineData(nameof(EditTextArea))]    // overrides OnAfterRenderAsync itself -- must still chain
    public async Task FocusOnFirstRender_focuses_once_on_first_render(string control)
    {
        var model = new FocusModel();
        Expression<Func<string>> text = () => model.Text;
        Expression<Func<int?>> number = () => model.Number;
        Expression<Func<bool>> flag = () => model.Flag;
        Expression<Func<DateTime?>> date = () => model.Date;
        Expression<Func<DateTime?>> end = () => model.End;
        Expression<Func<List<IBrowserFile>>> files = () => model.Files;

        var cut = RenderControl(model, b =>
        {
            switch (control)
            {
                case nameof(EditString):
                    b.OpenComponent<EditString>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    break;
                case nameof(EditTextArea):
                    b.OpenComponent<EditTextArea>(0);
                    b.AddAttribute(1, "Value", model.Text);
                    b.AddAttribute(2, "ValueExpression", text);
                    break;
                case "EditNumber":
                    b.OpenComponent<EditNumber<int?>>(0);
                    b.AddAttribute(1, "Value", model.Number);
                    b.AddAttribute(2, "ValueExpression", number);
                    break;
                case nameof(EditBool):
                    b.OpenComponent<EditBool>(0);
                    b.AddAttribute(1, "Value", model.Flag);
                    b.AddAttribute(2, "ValueExpression", flag);
                    break;
                case nameof(EditFile):
                    b.OpenComponent<EditFile>(0);
                    b.AddAttribute(1, "Value", model.Files);
                    b.AddAttribute(2, "ValueExpression", files);
                    break;
                default:
                    b.OpenComponent<EditDateRange>(0);
                    b.AddAttribute(1, "Start", model.Date);
                    b.AddAttribute(2, "StartExpression", date);
                    b.AddAttribute(3, "End", model.End);
                    b.AddAttribute(4, "EndExpression", end);
                    break;
            }
            b.AddAttribute(90, "FocusOnFirstRender", true);
            b.CloseComponent();
        });

        Assert.Equal(1, ElementFocusCalls());

        // ...and only on the FIRST render: a later re-render must not steal focus back from wherever
        // the user has since moved it.
        cut.Render();
        Assert.Equal(1, ElementFocusCalls());
    }

    [Fact]
    public async Task FocusOnFirstRender_on_a_radio_group_goes_through_the_group_channel()
    {
        var model = new FocusModel();
        Expression<Func<Priority?>> field = () => model.Priority;
        RenderControl(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "FocusOnFirstRender", true);
            b.CloseComponent();
        });

        await Task.Yield(); // OnAfterRenderAsync's continuation
        Assert.Single(GroupFocusCalls());
    }

    [Fact]
    public async Task FocusOnFirstRender_on_EditRadio_goes_through_its_own_hook()
    {
        // EditRadio inherits InputRadioGroup, so it carries its own copy of the FocusOnFirstRender
        // hook rather than either control base's -- pinned separately for exactly that reason.
        var model = new FocusModel();
        Expression<Func<string>> field = () => model.Text;
        RenderControl(model, b =>
        {
            b.OpenComponent<EditRadio<string>>(0);
            b.AddAttribute(1, "Value", model.Text);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "FocusOnFirstRender", true);
            b.AddAttribute(4, "ChildContent", (RenderFragment)(cb =>
            {
                cb.OpenComponent<InputRadio<string>>(0);
                cb.AddAttribute(1, "Value", "Alice");
                cb.CloseComponent();
            }));
            b.CloseComponent();
        });

        await Task.Yield();
        Assert.Single(GroupFocusCalls());
    }
}
