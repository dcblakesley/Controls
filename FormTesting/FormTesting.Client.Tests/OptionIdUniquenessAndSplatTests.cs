using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Two fixes that cut across the checkbox-list / radio-group controls.
/// <para>
/// <b>Option id uniqueness.</b> <c>EnumHelpers.ToId</c> is lossy — it strips everything outside
/// <c>[A-Za-z0-9-_]</c> — so an option list of non-ASCII labels used to sanitize every entry to the
/// same (empty) trailing segment. Duplicate ids make an explicit <c>&lt;label for&gt;</c> resolve to the
/// FIRST matching input, so every label toggled the first checkbox/radio. The hosts now de-duplicate
/// across the whole list, and the established id shape for an ordinary ASCII list is asserted here
/// alongside the fix so it can't drift.
/// </para>
/// <para>
/// <b>Unmatched attribute forwarding.</b> <c>EditControlListBase.AdditionalAttributes</c> is documented
/// as "applied", but only <c>class</c> was ever read (into <c>FieldCssClass</c>) — <c>style</c>,
/// <c>data-*</c> and <c>title</c> were silently dropped by <c>EditCheckedStringList</c>,
/// <c>EditCheckedEnumList</c> and <c>EditFile</c>. They now splat onto the root wrapper, with
/// <c>class</c> still travelling its single existing channel.
/// </para>
/// <para>
/// The scalar controls had the same hole for longer and less visibly: <c>InputBase</c> captures
/// unmatched attributes for free, so nothing threw — the attributes were simply never rendered, and
/// <c>class</c> (which travels <c>CssClass</c>) was the only one that ever reached the DOM. Each
/// control now splats the rest onto the element they describe (the editor, the radio fieldset, the
/// select engine's wrapper) and merges <c>style</c> onto the root wrapper, the one element every
/// control in the library agrees on for it.
/// </para>
/// </summary>
public class OptionIdUniquenessAndSplatTests : BunitContext
{
    // ----- shared fixtures -----------------------------------------------------------------------

    // Enum whose member names are CJK ideographs -- legal C# identifiers that ToId strips entirely.
    enum CjkColor
    {
        赤,
        青,
        緑
    }

    class CjkModel
    {
        public List<CjkColor> Picks { get; set; } = [];
    }

    sealed class FakeBrowserFile(string name, long size) : IBrowserFile
    {
        public string Name { get; } = name;
        public DateTimeOffset LastModified { get; } = DateTimeOffset.UnixEpoch;
        public long Size { get; } = size;
        public string ContentType => "text/plain";
        // Never reached: only the read-only view (Name + Size) is rendered in these tests.
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            Stream.Null;
    }

    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = [];
    }

    IRenderedComponent<ContainerFragment> RenderStringList(PersonModel model, List<string> options,
        string? cssClass = null, string? style = null, string? dataFoo = null)
    {
        Expression<Func<List<string>>> field = () => model.Tags;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", options);
            if (cssClass is not null) b.AddAttribute(4, "class", cssClass);
            if (style is not null) b.AddAttribute(5, "style", style);
            if (dataFoo is not null) b.AddAttribute(6, "data-foo", dataFoo);
            b.CloseComponent();
        }));
    }

    IRenderedComponent<ContainerFragment> RenderRadioString(PersonModel model, List<string> options,
        RadioOptionType optionType = RadioOptionType.Default, bool hasOther = false)
    {
        Expression<Func<string>> field = () => model.Name;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioString>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", options);
            b.AddAttribute(4, "OptionType", optionType);
            b.AddAttribute(5, "HasOther", hasOther);
            b.CloseComponent();
        }));
    }

    // Every label's explicit `for` must name the input it wraps -- otherwise the label activates
    // whichever input claimed the id first.
    static void AssertLabelsPairWithTheirOwnInputs(IReadOnlyList<IElement> labels, IReadOnlyList<IElement> inputs)
    {
        Assert.Equal(inputs.Count, labels.Count);
        var ids = inputs.Select(i => i.Id!).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        for (var i = 0; i < labels.Count; i++)
            Assert.Equal(ids[i], labels[i].GetAttribute("for"));
    }

    // ----- EditCheckedStringList / EditCheckedEnumList option ids ---------------------------------

    [Fact]
    public void EditCheckedStringList_keeps_the_established_cbx_id_shape_for_an_ascii_list()
    {
        var cut = RenderStringList(new PersonModel(), ["a", "b", "Hello World"]);

        // Unchanged by the de-duplication pass: first claim on a sanitized segment keeps it verbatim.
        Assert.NotNull(cut.Find("#cbx-Tags-a"));
        Assert.NotNull(cut.Find("#cbx-Tags-b"));
        Assert.NotNull(cut.Find("#cbx-Tags-Hello-World"));
        AssertLabelsPairWithTheirOwnInputs(cut.FindAll("label.edit-checkbox-label"), cut.FindAll("input[type=checkbox]"));
    }

    [Fact]
    public void EditCheckedStringList_gives_options_that_sanitize_alike_distinct_ids()
    {
        // "a!" and "a?" both sanitized to "a", so both labels pointed at the first checkbox.
        var cut = RenderStringList(new PersonModel(), ["a!", "a?"]);

        AssertLabelsPairWithTheirOwnInputs(cut.FindAll("label.edit-checkbox-label"), cut.FindAll("input[type=checkbox]"));
        Assert.NotNull(cut.Find("#cbx-Tags-a"));
        Assert.NotNull(cut.Find("#cbx-Tags-1-a"));
    }

    [Fact]
    public void EditCheckedStringList_gives_an_all_non_ascii_option_list_distinct_ids()
    {
        var cut = RenderStringList(new PersonModel(), ["赤", "青", "緑"]);

        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, boxes.Count);
        AssertLabelsPairWithTheirOwnInputs(cut.FindAll("label.edit-checkbox-label"), boxes);
        Assert.Equal(new[] { "cbx-Tags-0", "cbx-Tags-1", "cbx-Tags-2" }, boxes.Select(b => b.Id!).ToArray());
    }

    [Fact]
    public void EditCheckedStringList_labels_still_toggle_their_own_option_when_ids_collide()
    {
        // The end-user symptom, exercised through the change event the label's own input raises.
        var model = new PersonModel();
        List<string>? captured = null;
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => captured = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.AddAttribute(4, "Options", new List<string> { "赤", "青" });
            b.CloseComponent();
        }));

        cut.Find("#cbx-Tags-1").Change(true);

        Assert.NotNull(captured);
        Assert.Equal(new List<string> { "青" }, captured);
    }

    [Fact]
    public void EditCheckedEnumList_gives_a_non_ascii_enum_distinct_ids()
    {
        var model = new CjkModel();
        Expression<Func<List<CjkColor>>> field = () => model.Picks;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<CjkColor>>(0);
            b.AddAttribute(1, "Value", model.Picks);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var boxes = cut.FindAll("input[type=checkbox]");
        Assert.Equal(3, boxes.Count);
        AssertLabelsPairWithTheirOwnInputs(cut.FindAll("label.edit-checkbox-label"), boxes);
    }

    [Fact]
    public void EditCheckedEnumList_keeps_the_established_cbx_id_shape_for_an_ascii_enum()
    {
        var model = new PersonModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find("#cbx-FavoriteColors-Red"));
        Assert.NotNull(cut.Find("#cbx-FavoriteColors-PaleYellow"));
    }

    [Fact]
    public void EditCheckedStringList_read_only_view_gives_look_alike_selections_distinct_ids()
    {
        var model = new PersonModel { Tags = ["赤", "青"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "赤", "青" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        var ids = cut.FindAll(".edit-readonly-value").Select(v => v.Id!).ToList();
        Assert.Equal(2, ids.Count);
        Assert.Equal(2, ids.Distinct().Count());
    }

    // ----- radio option ids -----------------------------------------------------------------------

    [Fact]
    public void EditRadioString_keeps_the_established_rb_id_shape_for_an_ascii_list()
    {
        var cut = RenderRadioString(new PersonModel { Name = "a" }, ["a", "b"]);

        Assert.NotNull(cut.Find("#rb-Name-a"));
        Assert.NotNull(cut.Find("#rb-Name-b"));
    }

    [Fact]
    public void EditRadioString_button_mode_gives_options_that_sanitize_alike_distinct_ids()
    {
        // Button mode is where a duplicate id actually mis-wires the UI: the visible control is a
        // sibling <label for> rather than a wrapping label.
        var cut = RenderRadioString(new PersonModel { Name = "a!" }, ["a!", "a?"], RadioOptionType.Button);

        AssertLabelsPairWithTheirOwnInputs(
            cut.FindAll("label.edit-radio-button"), cut.FindAll("input.edit-radio-button-input"));
        Assert.NotNull(cut.Find("#rb-Name-a"));
        Assert.NotNull(cut.Find("#rb-Name-1-a"));
    }

    [Fact]
    public void EditRadioString_button_mode_gives_an_all_non_ascii_option_list_distinct_ids()
    {
        var cut = RenderRadioString(new PersonModel { Name = "赤" }, ["赤", "青", "緑"], RadioOptionType.Button);

        AssertLabelsPairWithTheirOwnInputs(
            cut.FindAll("label.edit-radio-button"), cut.FindAll("input.edit-radio-button-input"));
    }

    [Fact]
    public void EditRadioString_built_in_Other_keeps_its_own_id_when_a_real_option_is_named_other()
    {
        var cut = RenderRadioString(new PersonModel { Name = "" }, ["other", "b"], RadioOptionType.Button, hasOther: true);

        // The library's own suffix is the fixed point (several suites pin #rb-Name-other); the consumer
        // option yields to it rather than shadowing it.
        Assert.NotNull(cut.Find("#rb-Name-other"));
        Assert.NotNull(cut.Find("#rb-Name-0-other"));
        AssertLabelsPairWithTheirOwnInputs(
            cut.FindAll("label.edit-radio-button"), cut.FindAll("input.edit-radio-button-input"));
    }

    [Fact]
    public void EditRadioEnum_keeps_the_established_rb_id_shape_for_an_ascii_enum()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find("#rb-Priority-Low"));
        Assert.NotNull(cut.Find("#rb-Priority-Critical"));
    }

    [Fact]
    public void EditRadioEnum_button_mode_gives_a_non_ascii_enum_distinct_ids()
    {
        var model = new CjkEnumModel();
        Expression<Func<CjkColor?>> field = () => model.Pick;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<CjkColor?>>(0);
            b.AddAttribute(1, "Value", model.Pick);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "OptionType", RadioOptionType.Button);
            b.CloseComponent();
        }));

        AssertLabelsPairWithTheirOwnInputs(
            cut.FindAll("label.edit-radio-button"), cut.FindAll("input.edit-radio-button-input"));
    }

    class CjkEnumModel
    {
        public CjkColor? Pick { get; set; }
    }

    // ----- native <select> option ids (SelectOptionList) ------------------------------------------

    IRenderedComponent<ContainerFragment> RenderStringSelect(PersonModel model, List<string> options,
        string? nullOptionText = "")
    {
        Expression<Func<string>> field = () => model.Name;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectString<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", options);
            b.AddAttribute(4, "NullOptionText", nullOptionText);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void EditSelectString_keeps_the_established_option_id_shape_for_an_ascii_list()
    {
        var cut = RenderStringSelect(new PersonModel(), ["a", "b", "Hello World"]);

        Assert.NotNull(cut.Find("#Name-option-a"));
        Assert.NotNull(cut.Find("#Name-option-b"));
        Assert.NotNull(cut.Find("#Name-option-Hello-World"));
        Assert.NotNull(cut.Find("#Name-option-none")); // the synthetic leading blank option is untouched
    }

    [Fact]
    public void EditSelectString_gives_options_that_sanitize_alike_distinct_ids()
    {
        // "a!" and "a?" both sanitize to "a", so both <option>s carried the same DOM id.
        var cut = RenderStringSelect(new PersonModel(), ["a!", "a?"]);

        AssertOptionIdsAreDistinct(cut);
        Assert.NotNull(cut.Find("#Name-option-a"));
        Assert.NotNull(cut.Find("#Name-option-1-a"));
    }

    [Fact]
    public void EditSelectString_gives_an_all_non_ascii_option_list_distinct_ids()
    {
        var cut = RenderStringSelect(new PersonModel(), ["赤", "青", "緑"]);

        AssertOptionIdsAreDistinct(cut);
        Assert.NotNull(cut.Find("#Name-option-0"));
        Assert.NotNull(cut.Find("#Name-option-1"));
        Assert.NotNull(cut.Find("#Name-option-2"));
    }

    [Fact]
    public void EditSelectString_literal_none_option_yields_to_the_synthetic_leading_option()
    {
        // The library's own {Id}-option-none is the fixed point; a literal "none" option takes the
        // index-qualified form rather than shadowing it.
        var cut = RenderStringSelect(new PersonModel(), ["none", "b"]);

        AssertOptionIdsAreDistinct(cut);
        Assert.Equal("", cut.Find("#Name-option-none").GetAttribute("value")); // still the blank option
        Assert.Equal("none", cut.Find("#Name-option-0-none").GetAttribute("value"));
    }

    [Fact]
    public void EditSelectString_literal_placeholder_option_yields_to_the_synthetic_unmatched_option()
    {
        // Bound to a value that is in neither list position, so the hidden unmatched-value option
        // (#Name-option-placeholder) renders alongside the literal "placeholder" option.
        var cut = RenderStringSelect(new PersonModel { Name = "unmatched" }, ["placeholder", "b"],
            nullOptionText: null);

        AssertOptionIdsAreDistinct(cut);
        Assert.True(cut.Find("#Name-option-placeholder").HasAttribute("hidden")); // the synthetic one
        Assert.Equal("placeholder", cut.Find("#Name-option-0-placeholder").GetAttribute("value"));
    }

    [Fact]
    public void EditSelectString_gives_duplicate_string_options_distinct_ids()
    {
        var cut = RenderStringSelect(new PersonModel(), ["a", "a"]);

        AssertOptionIdsAreDistinct(cut);
    }

    [Fact]
    public void EditSelectEnum_keeps_the_established_option_id_shape_for_an_ascii_enum()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.NotNull(cut.Find("#Priority-option-Low"));
        Assert.NotNull(cut.Find("#Priority-option-Critical"));
        AssertOptionIdsAreDistinct(cut);
    }

    static void AssertOptionIdsAreDistinct(IRenderedComponent<ContainerFragment> cut)
    {
        var ids = cut.FindAll("option").Select(o => o.Id!).ToList();
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    // ----- unmatched attribute forwarding ---------------------------------------------------------

    [Fact]
    public void EditCheckedStringList_forwards_style_and_data_attributes_to_its_wrapper()
    {
        var cut = RenderStringList(new PersonModel(), ["a", "b"],
            cssClass: "my-class", style: "margin-top:4px", dataFoo: "bar");

        var wrapper = cut.Find(".edit-control-wrapper");
        Assert.Equal("margin-top:4px", wrapper.GetAttribute("style"));
        Assert.Equal("bar", wrapper.GetAttribute("data-foo"));
        // class keeps its single existing channel -- every checkbox, not the wrapper (no double-apply).
        Assert.DoesNotContain("my-class", wrapper.ClassList);
        Assert.All(cut.FindAll("input[type=checkbox]"), box => Assert.Contains("my-class", box.ClassList));
    }

    [Fact]
    public void EditCheckedEnumList_forwards_style_and_data_attributes_to_its_wrapper()
    {
        var model = new PersonModel();
        Expression<Func<List<Color>>> field = () => model.FavoriteColors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.FavoriteColors);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "margin-top:4px");
            b.AddAttribute(5, "data-foo", "bar");
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".edit-control-wrapper");
        Assert.Equal("margin-top:4px", wrapper.GetAttribute("style"));
        Assert.Equal("bar", wrapper.GetAttribute("data-foo"));
        Assert.All(cut.FindAll("input[type=checkbox]"), box => Assert.Contains("my-class", box.ClassList));
    }

    [Fact]
    public void EditFile_forwards_style_and_data_attributes_to_its_wrapper()
    {
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "margin-top:4px");
            b.AddAttribute(5, "title", "pick files");
            b.CloseComponent();
        }));

        var wrapper = cut.Find(".edit-control-wrapper");
        Assert.Equal("margin-top:4px", wrapper.GetAttribute("style"));
        Assert.Equal("pick files", wrapper.GetAttribute("title"));
        // The drop zone keeps owning the class channel (its own class list is state-driven).
        Assert.Contains("my-class", cut.Find(".edit-file-drop-zone").ClassList);
    }

    [Fact]
    public void List_controls_render_no_style_attribute_when_the_consumer_supplies_none()
    {
        var cut = RenderStringList(new PersonModel(), ["a"], cssClass: "my-class");

        Assert.False(cut.Find(".edit-control-wrapper").HasAttribute("style"));
    }

    // ----- scalar controls: splat lands on the editor, style on the wrapper -----------------------

    [Fact]
    public void EditNumber_forwards_inputmode_and_data_attributes_to_its_input()
    {
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "width:8rem");
            b.AddAttribute(5, "data-foo", "bar");
            b.AddAttribute(6, "inputmode", "numeric");
            b.CloseComponent();
        }));

        var input = cut.Find("input[type=number]");
        Assert.Equal("bar", input.GetAttribute("data-foo"));
        Assert.Equal("numeric", input.GetAttribute("inputmode"));
        // class keeps its single channel (CssClass -> the input) and style its own (the wrapper);
        // neither is re-emitted by the splat, so neither can double-render.
        Assert.Contains("my-class", input.ClassList);
        Assert.False(input.HasAttribute("style"));
        var wrapper = cut.Find(".edit-control-wrapper");
        Assert.Equal("width:8rem", wrapper.GetAttribute("style"));
        Assert.DoesNotContain("my-class", wrapper.ClassList);
    }

    [Fact]
    public void EditNumber_renders_no_splat_or_style_when_the_consumer_supplies_no_extra_attributes()
    {
        // The legacy DOM has to stay byte-identical for the overwhelmingly common case.
        var model = new PersonModel { Age = 30 };
        Expression<Func<int?>> field = () => model.Age;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditNumber<int?>>(0);
            b.AddAttribute(1, "Value", model.Age);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find(".edit-control-wrapper").HasAttribute("style"));
        var input = cut.Find("input[type=number]");
        Assert.False(input.HasAttribute("style"));
        // The whole attribute list, in order, so an accidentally-emitted empty splat (or a reordering
        // from moving one) fails here rather than silently in a visual baseline. `value` and
        // `blazor:onchange` trail the hand-written ones because bUnit's htmlizer defers the bound
        // value/event; everything before them is this control's own markup order. `aria-labelledby`
        // (TXT naming-anchor wiring) is a deliberate addition -- it points at FormLabel's
        // lbltext-{id} span rather than relying on <label for>, which used to fold the tooltip
        // trigger's own name into the field's accessible name.
        //
        // `blazor:elementreference` last is the @ref this control's public FocusAsync() needs (see
        // EditControlBase._editorRef). It is NOT free of DOM effect, contrary to the usual assumption:
        // a real browser renders a reference capture as an empty `_bl_{guid}` attribute on the element,
        // which is exactly what bUnit's htmlizer is standing in for here. Valueless and unstyled, so it
        // moves no visual baseline and matches no selector -- but it does belong in this pin, and
        // EditString/EditTextArea have carried the same one since their clear button shipped.
        Assert.Equal(
            "type|id|data-test-id|min|max|class|aria-labelledby|aria-required|aria-describedby|value|blazor:onchange|blazor:elementreference",
            string.Join("|", input.Attributes.Select(a => a.Name)));
    }

    [Fact]
    public void EditTextArea_forwards_spellcheck_and_data_attributes_alongside_its_own_input_handler()
    {
        // ShowCount + UpdateOn=Change is the combination that makes EditTextInputBase splat an
        // `oninput` handler dictionary of its own onto this same element -- an element can carry only
        // one @attributes, so the consumer's attributes have to merge with it rather than replace it.
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "ShowCount", true);
            b.AddAttribute(4, "UpdateOn", UpdateTrigger.Change);
            b.AddAttribute(5, "class", "my-class");
            b.AddAttribute(6, "style", "resize:none");
            b.AddAttribute(7, "data-foo", "bar");
            b.AddAttribute(8, "spellcheck", "false");
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea");
        Assert.Equal("bar", textarea.GetAttribute("data-foo"));
        Assert.Equal("false", textarea.GetAttribute("spellcheck"));
        Assert.Contains("my-class", textarea.ClassList);
        // Never on the textarea: its inline style is JS-owned while AutoSize is on.
        Assert.False(textarea.HasAttribute("style"));
        Assert.Equal("resize:none", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    [Fact]
    public void EditTextArea_own_attributes_win_over_a_consumer_splatting_the_same_name()
    {
        // AttributeSplat.RestWith layers the control's own dictionary on top of the consumer's, and
        // the merged splat sits FIRST, so the hand-written attributes beside it win too.
        var model = new PersonModel { Name = "hello" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditTextArea>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "id", "consumer-id");       // matches the Id parameter, so never splatted
            b.AddAttribute(4, "data-test-id", "consumer"); // unmatched, but the control writes its own
            b.CloseComponent();
        }));

        var textarea = cut.Find("textarea");
        Assert.Equal("consumer-id", textarea.Id);          // Id="..." is a real parameter
        Assert.Equal("consumer-id", textarea.GetAttribute("data-test-id"));
    }

    [Fact]
    public void EditBool_forwards_data_and_aria_attributes_to_its_checkbox()
    {
        var model = new PersonModel { IsActive = true };
        Expression<Func<bool>> field = () => model.IsActive;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBool>(0);
            b.AddAttribute(1, "Value", model.IsActive);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "margin-top:4px");
            b.AddAttribute(5, "data-foo", "bar");
            b.AddAttribute(6, "aria-keyshortcuts", "Alt+A");
            b.CloseComponent();
        }));

        var box = cut.Find("input[type=checkbox]");
        Assert.Equal("bar", box.GetAttribute("data-foo"));
        Assert.Equal("Alt+A", box.GetAttribute("aria-keyshortcuts"));
        Assert.Contains("my-class", box.ClassList);
        Assert.False(box.HasAttribute("style"));
        Assert.Equal("margin-top:4px", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    [Fact]
    public void EditSelectEnum_forwards_data_and_title_attributes_to_its_select()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "max-width:12rem");
            b.AddAttribute(5, "data-foo", "bar");
            b.AddAttribute(6, "title", "pick one");
            b.CloseComponent();
        }));

        var select = cut.Find("select");
        Assert.Equal("bar", select.GetAttribute("data-foo"));
        Assert.Equal("pick one", select.GetAttribute("title"));
        Assert.Contains("my-class", select.ClassList);
        Assert.False(select.HasAttribute("style"));
        Assert.Equal("max-width:12rem", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    // ----- radio groups: splat lands on the radiogroup fieldset, merged with RadioAria's block ----

    [Fact]
    public void EditRadioEnum_forwards_data_attributes_to_its_fieldset_without_displacing_RadioAria()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-class");
            b.AddAttribute(4, "style", "padding:2px");
            b.AddAttribute(5, "data-foo", "bar");
            b.AddAttribute(6, "aria-keyshortcuts", "Alt+P");
            // A collision with RadioAria's own block: the control's answer has to win.
            b.AddAttribute(7, "role", "presentation");
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("bar", fieldset.GetAttribute("data-foo"));
        Assert.Equal("Alt+P", fieldset.GetAttribute("aria-keyshortcuts"));
        Assert.Equal("radiogroup", fieldset.GetAttribute("role"));
        Assert.Equal("Priority", fieldset.Id);
        Assert.False(fieldset.HasAttribute("style"));
        // class keeps its single channel (CssClass -> each radio input), style the wrapper.
        Assert.DoesNotContain("my-class", fieldset.ClassList);
        Assert.All(cut.FindAll("input[type=radio]"), r => Assert.Contains("my-class", r.ClassList));
        Assert.Equal("padding:2px", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    [Fact]
    public void EditRadioEnum_renders_no_splat_or_style_when_the_consumer_supplies_no_extra_attributes()
    {
        var model = new PersonModel { Priority = Priority.Low };
        Expression<Func<Priority?>> field = () => model.Priority;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditRadioEnum<Priority?>>(0);
            b.AddAttribute(1, "Value", model.Priority);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        Assert.False(cut.Find(".edit-control-wrapper").HasAttribute("style"));
        Assert.Equal(
            "id|data-test-id|role|aria-labelledby|aria-required|aria-describedby|class",
            string.Join("|", cut.Find("fieldset.edit-radio-fieldset").Attributes.Select(a => a.Name)));
    }

    [Fact]
    public void EditBoolNullRadio_forwards_data_attributes_to_its_fieldset()
    {
        var model = new PersonModel { IsSubscribed = true };
        Expression<Func<bool?>> field = () => model.IsSubscribed;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditBoolNullRadio>(0);
            b.AddAttribute(1, "Value", model.IsSubscribed);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "style", "padding:2px");
            b.AddAttribute(4, "data-foo", "bar");
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-radio-fieldset");
        Assert.Equal("bar", fieldset.GetAttribute("data-foo"));
        Assert.Equal("radiogroup", fieldset.GetAttribute("role"));
        Assert.Equal("padding:2px", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    // ----- EditSelectSearch: forwarded through to the Select engine's wrapper ---------------------

    [Fact]
    public void EditSelectSearch_forwards_data_attributes_through_to_the_select_engine_wrapper()
    {
        var model = new PersonModel { Name = "a" };
        Expression<Func<string>> field = () => model.Name;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditSelectSearch<string>>(0);
            b.AddAttribute(1, "Value", model.Name);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<SelectOption<string>> { new("a", "A"), new("b", "B") });
            b.AddAttribute(4, "class", "my-class");
            b.AddAttribute(5, "style", "width:14rem");
            b.AddAttribute(6, "data-foo", "bar");
            b.CloseComponent();
        }));

        // The engine has no AdditionalAttributes of its own before this change, so an unmatched
        // attribute on EditSelectSearch had nowhere to go at all.
        var engine = cut.Find(".wss-select");
        Assert.Equal("bar", engine.GetAttribute("data-foo"));
        // class still arrives through CssClass (which the wrapper folds into WrapperClass); style
        // stays on the form wrapper, since the engine's inline style is JS-owned when open.
        Assert.Contains("my-class", engine.ClassList);
        Assert.False(engine.HasAttribute("style"));
        Assert.Equal("width:14rem", cut.Find(".edit-control-wrapper").GetAttribute("style"));
    }

    [Fact]
    public void A_standalone_Select_no_longer_throws_on_an_unmatched_attribute()
    {
        var cut = Render(b =>
        {
            b.OpenComponent<Select<string>>(0);
            b.AddAttribute(1, "Options", new List<SelectOption<string>> { new("a", "A") });
            b.AddAttribute(2, "data-foo", "bar");
            b.CloseComponent();
        });

        Assert.Equal("bar", cut.Find(".wss-select").GetAttribute("data-foo"));
    }

    // ----- EditFile read-only label association ---------------------------------------------------

    [Fact]
    public void EditFile_read_only_list_stays_labelled_by_the_field_label_when_the_label_is_hidden()
    {
        // FormLabel renders lbl-{id} even for a hidden label (sr-only), so the old
        // ShouldHideLabel gate dropped the association on a premise that no longer holds.
        var model = new FileModel { Files = [new FakeBrowserFile("report.pdf", 10)] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.AddAttribute(4, "IsLabelHidden", true);
            b.CloseComponent();
        }));

        var list = cut.Find(".edit-file-list--readonly");
        Assert.Equal("lbltext-Files", list.GetAttribute("aria-labelledby"));
        Assert.NotNull(cut.Find("#lbltext-Files"));        // the sr-only naming anchor it points at really exists
        Assert.False(list.HasAttribute("aria-label"));     // no competing name now that labelledby is on
    }
}
