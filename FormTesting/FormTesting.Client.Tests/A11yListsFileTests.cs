using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for the 2026-08-13 checked-list/file-upload/read-only-display accessibility audit
/// (LST-1/LST-3/LST-4/LST-5/LST-6/LST-8/LST-9, TXT-5's <c>AriaDescribedBy</c> wiring, CSS-10's
/// markup half). Each section below is named for the finding it covers.
/// </summary>
public class A11yListsFileTests : BunitContext
{
    // ----- LST-1: the checked-list fieldset is named by the anchor and carries the required cue ----

    class RequiredTagsModel
    {
        [Required, MinLength(1)]
        public List<string> Tags { get; set; } = [];
    }

    class RequiredColorsModel
    {
        [Required, MinLength(1)]
        public List<Color> Colors { get; set; } = [];
    }

    [Fact]
    public void Required_EditCheckedStringList_fieldset_is_named_by_the_anchor_and_carries_the_required_cue()
    {
        var model = new RequiredTagsModel();
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-checkedList-fieldset");
        Assert.Equal("lbltext-Tags", fieldset.GetAttribute("aria-labelledby"));
        // role="group" forbids aria-required (ARIA 1.2) and the star is aria-hidden, so the sr-only
        // "(required)" inside the naming anchor is the ONLY channel requiredness reaches AT through.
        var anchor = cut.Find("#lbltext-Tags");
        Assert.Contains("(required)", anchor.TextContent);
        Assert.DoesNotContain("More information", anchor.TextContent);
        Assert.False(fieldset.HasAttribute("aria-required")); // never — ARIA 1.2 disallows it on role="group"
    }

    [Fact]
    public void Required_EditCheckedEnumList_fieldset_is_named_by_the_anchor_and_carries_the_required_cue()
    {
        var model = new RequiredColorsModel();
        Expression<Func<List<Color>>> field = () => model.Colors;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedEnumList<Color>>(0);
            b.AddAttribute(1, "Value", model.Colors);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var fieldset = cut.Find("fieldset.edit-checkedList-fieldset");
        Assert.Equal("lbltext-Colors", fieldset.GetAttribute("aria-labelledby"));
        Assert.Contains("(required)", cut.Find("#lbltext-Colors").TextContent);
    }

    [Fact]
    public void Optional_checked_list_fieldset_is_named_by_the_anchor_but_carries_no_required_cue()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.CloseComponent();
        }));

        Assert.Equal("lbltext-Tags", cut.Find("fieldset.edit-checkedList-fieldset").GetAttribute("aria-labelledby"));
        Assert.DoesNotContain("(required)", cut.Find("#lbltext-Tags").TextContent);
    }

    [Fact]
    public void Read_only_checked_list_fieldset_carries_no_aria_labelledby()
    {
        // Same convention as the radio groups' RadioAria.Fieldset: in read-only mode the id/role/
        // aria-labelledby trio are all omitted, and the fieldset is named by its own <legend> content
        // (native fieldset/legend semantics) instead.
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.False(cut.Find("fieldset.edit-checkedList-fieldset").HasAttribute("aria-labelledby"));
    }

    // ----- TXT-5 / point (c): ReadOnlyValue.AriaDescribedBy wired at the checked-list/EditFile call sites --

    [Fact]
    public void Read_only_checked_list_rows_carry_aria_describedby_matching_the_description()
    {
        var model = new PersonModel { Tags = ["a"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a" });
            b.AddAttribute(4, "IsEditMode", false);
            b.AddAttribute(5, "Description", "Pick your favorite tags");
            b.CloseComponent();
        }));

        var row = cut.Find(".edit-readonly-value");
        var describedBy = (row.GetAttribute("aria-describedby") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("desc-Tags", describedBy);
        Assert.NotNull(cut.Find("#desc-Tags"));
    }

    [Fact]
    public void EditFile_empty_read_only_value_carries_aria_describedby()
    {
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.AddAttribute(4, "Description", "Attach supporting documents");
            b.CloseComponent();
        }));

        var value = cut.Find(".edit-readonly-value");
        var describedBy = (value.GetAttribute("aria-describedby") ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("desc-Files", describedBy);
    }

    // ----- LST-6: a default hint derived from [MinLength]/[MaxLength] on the bound list -------------

    class LengthModel
    {
        [MinLength(2)]
        public List<string> MinOnly { get; set; } = [];

        [MinLength(1)]
        public List<string> MinOnlySingular { get; set; } = [];

        [MinLength(1), MaxLength(3)]
        public List<string> MinAndMax { get; set; } = [];

        [MaxLength(4)]
        public List<string> MaxOnly { get; set; } = [];

        [MinLength(2)]
        [Description("Custom description wins")]
        public List<string> WithAttributeDescription { get; set; } = [];

        public List<string> NoConstraint { get; set; } = [];
    }

    string DescriptionTextFor(LengthModel model, Expression<Func<List<string>>> field, string? explicitDescription = null)
    {
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", field.Compile()());
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            if (explicitDescription is not null)
                b.AddAttribute(4, "Description", explicitDescription);
            b.CloseComponent();
        }));
        return cut.FindAll(".edit-label-description").Select(p => p.TextContent.Trim()).FirstOrDefault() ?? "";
    }

    [Fact]
    public void MinLength_only_list_gets_a_derived_at_least_hint()
    {
        var model = new LengthModel();
        Assert.Equal("Select at least 2 options.", DescriptionTextFor(model, () => model.MinOnly));
    }

    [Fact]
    public void MinLength_of_one_uses_the_singular_wording()
    {
        var model = new LengthModel();
        Assert.Equal("Select at least 1 option.", DescriptionTextFor(model, () => model.MinOnlySingular));
    }

    [Fact]
    public void MinLength_and_MaxLength_list_gets_a_combined_between_hint()
    {
        var model = new LengthModel();
        Assert.Equal("Select between 1 and 3 options.", DescriptionTextFor(model, () => model.MinAndMax));
    }

    [Fact]
    public void MaxLength_only_list_gets_a_derived_up_to_hint()
    {
        var model = new LengthModel();
        Assert.Equal("Select up to 4 options.", DescriptionTextFor(model, () => model.MaxOnly));
    }

    [Fact]
    public void No_length_constraint_renders_no_derived_description()
    {
        var model = new LengthModel();
        Assert.Equal("", DescriptionTextFor(model, () => model.NoConstraint));
    }

    [Fact]
    public void Explicit_Description_parameter_wins_over_the_derived_hint()
    {
        var model = new LengthModel();
        Assert.Equal("Custom text from the consumer",
            DescriptionTextFor(model, () => model.MinOnly, explicitDescription: "Custom text from the consumer"));
    }

    [Fact]
    public void Model_Description_attribute_wins_over_the_derived_hint()
    {
        var model = new LengthModel();
        Assert.Equal("Custom description wins", DescriptionTextFor(model, () => model.WithAttributeDescription));
    }

    [Fact]
    public void The_derived_hint_also_reaches_aria_describedby()
    {
        // Rendering the hint alone isn't enough -- an AT user who never sees the visible paragraph
        // still needs it read as part of the field's description (desc-{id}).
        var model = new LengthModel();
        Expression<Func<List<string>>> field = () => model.MinOnly;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.MinOnly);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b", "c" });
            b.CloseComponent();
        }));

        var describedBy = (cut.FindAll("input[type=checkbox]")[0].GetAttribute("aria-describedby") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("desc-MinOnly", describedBy);
        Assert.Equal("Select at least 2 options.", cut.Find("#desc-MinOnly").TextContent.Trim());
    }

    // ----- LST-8: the read-only checked list wraps its selections in a real list --------------------

    [Fact]
    public void Read_only_checked_list_wraps_selected_options_in_a_real_list()
    {
        var model = new PersonModel { Tags = ["a", "b"] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        var list = cut.Find("ul.edit-checkedList-readonly-list");
        Assert.Equal(2, list.QuerySelectorAll("li.edit-checkedList-readonly-item").Length);
        Assert.Equal(2, list.QuerySelectorAll(".edit-readonly-value").Length);
    }

    [Fact]
    public void Read_only_checked_list_with_no_selection_renders_no_list_wrapper()
    {
        // The empty-selection placeholder ("Not Set") is a single ReadOnlyValue, not a one-item list.
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "a", "b" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll("ul.edit-checkedList-readonly-list"));
        Assert.Single(cut.FindAll(".edit-readonly-value"));
    }

    // ----- LST-9: an empty-string option gets a placeholder display label ---------------------------

    [Fact]
    public void Empty_string_option_gets_a_placeholder_checkbox_label()
    {
        var model = new PersonModel { Tags = [] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "", "a" });
            b.CloseComponent();
        }));

        var labels = cut.FindAll("label.edit-checkbox-label").Select(l => l.TextContent.Trim()).ToList();
        Assert.Contains("(blank)", labels);
        Assert.Contains("a", labels);
    }

    [Fact]
    public void Empty_string_option_placeholder_also_applies_to_the_read_only_view()
    {
        var model = new PersonModel { Tags = [""] };
        Expression<Func<List<string>>> field = () => model.Tags;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditCheckedStringList>(0);
            b.AddAttribute(1, "Value", model.Tags);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "Options", new List<string> { "" });
            b.AddAttribute(4, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Contains("(blank)", cut.Find(".edit-readonly-value").TextContent);
    }

    // ----- EditFile fixtures shared by LST-3/LST-4/LST-5/CSS-10 --------------------------------------

    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = [];
    }

    sealed class FakeBrowserFile(string name, long size = 5) : IBrowserFile
    {
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => size;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(new byte[size]);
    }

    // ----- LST-3/LST-4: the polite live-status region ------------------------------------------------

    [Fact]
    public async Task Loading_status_is_announced_while_a_batch_is_buffered_and_clears_on_completion()
    {
        var model = new FileModel();
        var gate = new TaskCompletionSource<bool>();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => model.Files = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.AddAttribute(4, "BeforeAdd", (Func<IBrowserFile, Task<bool>>)(_ => gate.Task));
            b.CloseComponent();
        }));

        // Invoked directly (reflection), same technique as the existing re-entrancy tests: LoadFiles
        // suspends at `await BeforeAdd(file)`, which the gate holds open so the mid-flight render can
        // be inspected before letting it complete.
        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new InputFileChangeEventArgs([new FakeBrowserFile("a.txt")]);

        Task loadTask = Task.CompletedTask;
        await cut.InvokeAsync(() => { loadTask = (Task)loadFiles.Invoke(editFile, [args])!; });

        // Mid-flight, the flag that drives the status text is set. Asserted on the field rather than
        // on rendered markup because the intermediate RENDER is the framework's, not this method's:
        // in the app LoadFiles arrives via InputFile's OnChange and ComponentBase.HandleEventAsync
        // calls StateHasChanged the moment the handler yields on an incomplete task. Driving the
        // method directly (as this and the existing re-entrancy tests must, to hold a batch open)
        // bypasses that pipeline. Putting a StateHasChanged() in LoadFiles to compensate is NOT the
        // answer -- it throws for those re-entrancy tests, which invoke it outside the renderer's
        // synchronization context. So: verify the state here, and the rendered text below once the
        // batch completes and a normal render has happened.
        var loadingField = typeof(EditFile).GetField("_isLoadingFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Assert.True((bool)loadingField.GetValue(editFile)!, "the batch should be in flight while BeforeAdd is gated");
        Assert.Equal("Loading files…", editFile.LoadingStatusText);

        gate.SetResult(true);
        await cut.InvokeAsync(() => loadTask);

        // Once the batch completes the flag clears, so the region goes back to reporting the file
        // list instead of "Loading files…". What that committed text says is covered separately, by
        // the tests below that drive a real upload through InputFile.UploadFiles — this one uses a
        // hand-built IBrowserFile precisely so the batch can be held open mid-flight, and that double
        // isn't buffered like a genuine upload, so it is the wrong instrument for asserting contents.
        Assert.False((bool)loadingField.GetValue(editFile)!, "the batch should have completed");
        Assert.DoesNotContain("Loading files", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Adding_a_file_announces_the_updated_count_and_name()
    {
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => model.Files = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "report.pdf"));

        Assert.Equal("1 file selected: report.pdf.", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void Removing_the_last_file_announces_the_empty_state()
    {
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => model.Files = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "report.pdf"));
        Assert.Equal("1 file selected: report.pdf.", cut.Find("[role=status]").TextContent);

        cut.Find(".edit-file-delete-btn").Click();

        Assert.Equal("No files selected.", cut.Find("[role=status]").TextContent);
    }

    // ----- LST-5: the resolved caps are stated up front, alongside "Supported formats" --------------

    [Fact]
    public void Dropzone_renders_resolved_size_and_count_caps_alongside_supported_formats()
    {
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "MaxFileSizeBytes", 5L * 1024 * 1024);
            b.AddAttribute(4, "MaxTotalBytes", 20L * 1024 * 1024);
            b.AddAttribute(5, "MaxFiles", 3);
            b.CloseComponent();
        }));

        var hint = cut.Find(".edit-file-limits").TextContent;
        Assert.Contains("5 MB", hint);
        Assert.Contains("20 MB", hint);
        Assert.Contains("3", hint);
    }

    [Fact]
    public void Caps_hint_omits_the_file_count_clause_when_MaxFiles_is_unlimited()
    {
        // Defaults: 10 MB/file, 100 MB total, MaxFiles = 0 (unlimited) -- the count clause must not
        // claim a limit that doesn't exist.
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.CloseComponent();
        }));

        var hint = cut.Find(".edit-file-limits").TextContent;
        Assert.Contains("10 MB", hint);
        Assert.Contains("100 MB", hint);
        Assert.DoesNotContain("up to", hint);
    }

    // ----- CSS-10: the plain file-name span keeps the full name recoverable via title ---------------

    [Fact]
    public void Plain_file_name_span_carries_a_title_with_the_full_name()
    {
        var model = new FileModel();
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, v => model.Files = v));
            b.AddAttribute(3, "ValueExpression", field);
            b.CloseComponent();
        }));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("hi", "verylongdocumentname-report-final-v2.pdf"));

        var span = cut.Find("span.edit-file-name");
        Assert.Equal("verylongdocumentname-report-final-v2.pdf", span.GetAttribute("title"));
    }
}
