using System.IO;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace FormTesting.Client.Tests;

/// <summary>
/// Coverage for <c>EditFile</c>, starting with the null-bound-list regressions: a model whose
/// <c>List&lt;IBrowserFile&gt;</c> property is never initialized must render and accept uploads
/// rather than throwing.
/// </summary>
public class EditFileTests : TestContext
{
    class FileModel
    {
        public List<IBrowserFile> Files { get; set; } = null!; // required only guarantees set, not non-null
    }

    static RenderFragment WithForm(FileModel model, RenderFragment inner) => builder =>
    {
        builder.OpenComponent<EditForm>(0);
        builder.AddAttribute(1, "Model", model);
        builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => content => inner(content)));
        builder.CloseComponent();
    };

    IRenderedFragment RenderEditFile(
        FileModel model,
        Action<List<IBrowserFile>>? onChanged = null,
        int maxFiles = 0,
        bool isDisabled = false,
        long maxFileSizeBytes = 0,
        string[]? allowedExtensions = null,
        long? maxTotalBytes = null)   // nullable so a test can pass 0 explicitly (0 = unlimited) vs. leave the 100 MB default
    {
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        return Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            if (onChanged is not null)
                b.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<List<IBrowserFile>>(this, onChanged));
            if (maxFiles > 0)
                b.AddAttribute(4, "MaxFiles", maxFiles);
            if (isDisabled)
                b.AddAttribute(5, "IsDisabled", true);
            if (maxFileSizeBytes > 0)
                b.AddAttribute(6, "MaxFileSizeBytes", maxFileSizeBytes);
            if (allowedExtensions is not null)
                b.AddAttribute(7, "AllowedExtensions", allowedExtensions);
            if (maxTotalBytes is not null)
                b.AddAttribute(8, "MaxTotalBytes", maxTotalBytes.Value);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void Null_bound_list_renders_the_drop_zone_without_throwing()
    {
        var cut = RenderEditFile(new FileModel()); // Files is null

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
        Assert.Empty(cut.FindAll(".edit-file-list"));
    }

    [Fact]
    public void Null_bound_list_with_MaxFiles_renders_the_drop_zone_without_throwing()
    {
        var cut = RenderEditFile(new FileModel(), maxFiles: 3); // hits the Value.Count < MaxFiles branch

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
    }

    [Fact]
    public void Upload_into_a_null_bound_list_creates_the_list()
    {
        var model = new FileModel();
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Equal("a.txt", changed[0].Name);
    }

    [Fact]
    public void IsDisabled_disables_the_file_input_and_remove_buttons()
    {
        List<IBrowserFile>? uploaded = null;
        var enabled = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        enabled.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "a.txt"));
        Assert.NotNull(uploaded);

        var disabled = RenderEditFile(new FileModel { Files = uploaded }, isDisabled: true);

        Assert.True(disabled.Find("input[type=file]").HasAttribute("disabled"));
        Assert.Contains("disabled", disabled.Find(".edit-file-drop-zone").ClassList);
        Assert.True(disabled.Find(".edit-file-delete-btn").HasAttribute("disabled"));
    }

    [Fact]
    public void Files_beyond_MaxFiles_are_reported_not_silently_dropped()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("Only 1 file allowed — 1 not added.", cut.Find(".edit-validation-message").TextContent);
    }

    // A text file of exactly n bytes ('x' is one byte in UTF-8), for driving the byte-size caps precisely.
    static InputFileContent FileOfBytes(int n, string name) => InputFileContent.CreateFromText(new string('x', n), name);

    [Fact]
    public void Upload_error_message_formats_are_localizable()
    {
        // The default English strings are pinned by the surrounding tests; this pins the override
        // path: consumer-supplied formats replace the built-ins, including a plural-handling format
        // that ignores the pre-pluralized English unit argument.
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "MaxFiles", 1);
            b.AddAttribute(4, "AllowedExtensions", new[] { ".txt" });
            b.AddAttribute(5, "MaxFilesMessageFormat", "Maximal {0} Dateien erlaubt — {1} nicht hinzugefügt.");
            b.AddAttribute(6, "UnsupportedFormatMessageFormat", "{0}: Format nicht unterstützt ({1}).");
            b.CloseComponent();
        }));

        // Batch 1: extension rejection (the count cap runs first, so keep this batch under it).
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "b.csv"));
        Assert.Contains("b.csv: Format nicht unterstützt (.txt).",
            cut.Find(".edit-validation-message").TextContent);

        // Batch 2: one accepted, one over the count cap.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("2", "a.txt"),
            InputFileContent.CreateFromText("3", "c.txt"));
        Assert.Contains("Maximal 1 Dateien erlaubt — 1 nicht hinzugefügt.",
            cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Files_within_the_per_file_cap_but_over_the_total_cap_stop_buffering_at_the_cap()
    {
        // M12: each file passes MaxFileSizeBytes (4 KB) individually, but the aggregate cap (2 KB) admits
        // only the first two 1 KB files; the third would push the running total to 3 KB and is skipped.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFileSizeBytes: 4096, maxTotalBytes: 2048);

        cut.FindComponent<InputFile>().UploadFiles(
            FileOfBytes(1024, "a.txt"),
            FileOfBytes(1024, "b.txt"),
            FileOfBytes(1024, "c.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);   // only up to the cap got buffered
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("Total size limit", message);
        Assert.Contains("1 file not added", message);
    }

    [Fact]
    public void MaxTotalBytes_zero_disables_the_aggregate_cap()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxTotalBytes: 0);

        cut.FindComponent<InputFile>().UploadFiles(
            FileOfBytes(1024, "a.txt"),
            FileOfBytes(1024, "b.txt"),
            FileOfBytes(1024, "c.txt"));

        Assert.NotNull(changed);
        Assert.Equal(3, changed.Count);   // 0 = unlimited, so nothing is turned away
        // The upload-error block is the only role="alert" (FieldValidationDisplay renders always-present,
        // empty .edit-validation-message divs), so its absence means no cap message was produced.
        Assert.Empty(cut.FindAll("div.edit-validation-message[role='alert']"));
    }

    [Fact]
    public void The_total_cap_counts_already_selected_files_from_earlier_batches()
    {
        // The cap must include the bytes already buffered: batch 1 fills 1 KB of a 2 KB budget, so batch 2
        // can add only one more 1 KB file — the second exceeds the total ONLY because of batch 1.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; }, maxFileSizeBytes: 4096, maxTotalBytes: 2048);

        cut.FindComponent<InputFile>().UploadFiles(FileOfBytes(1024, "a.txt"));                       // batch 1: total now 1 KB
        cut.FindComponent<InputFile>().UploadFiles(FileOfBytes(1024, "b.txt"), FileOfBytes(1024, "c.txt")); // batch 2

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);                       // a.txt + b.txt; c.txt turned away by the running total
        Assert.Equal(["a.txt", "b.txt"], changed.Select(f => f.Name));
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("Total size limit", message);
        Assert.Contains("1 file not added", message);
    }

    [Fact]
    public void Sub_megabyte_size_cap_is_reported_in_KB_not_zero_MB()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, maxFileSizeBytes: 500 * 1024);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText(new string('x', 600 * 1024), "big.txt"));

        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("500 KB", message);
        Assert.DoesNotContain("0 MB", message);
    }

    [Fact]
    public void Reselecting_the_same_file_is_skipped_and_reported_not_added_twice()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt"));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "a.txt")); // re-pick, same name/size/last-modified

        Assert.NotNull(changed);
        Assert.Single(changed); // not two slots for the same file
        Assert.Contains("a.txt is already added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Dropping_two_copies_of_the_same_file_in_one_batch_keeps_only_one()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("hello", "a.txt"),
            InputFileContent.CreateFromText("hello", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("a.txt is already added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Every_rejected_file_gets_its_own_error_message()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, allowedExtensions: [".pdf"]);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.csv"));

        // Two rejections used to overwrite each other, leaving only the last visible.
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("a.txt", message);
        Assert.Contains("b.csv", message);
    }

    [Fact]
    public void Remove_button_removes_only_that_file()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));
        Assert.Equal(2, changed!.Count);

        cut.FindAll(".edit-file-delete-btn")[0].Click();

        Assert.Single(changed);
        Assert.Equal("b.txt", changed[0].Name);
    }

    [Fact]
    public void Disabled_drop_zone_does_not_show_the_drag_hover_highlight()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, isDisabled: true);
        var zone = cut.Find(".edit-file-drop-zone");
        Assert.Contains("disabled", zone.ClassList);

        zone.DragEnter(); // drag a file over the disabled zone

        // The drop is refused when disabled, so the zone must not light up as if it accepts one.
        Assert.DoesNotContain("hover", cut.Find(".edit-file-drop-zone").ClassList);
    }

    [Fact]
    public void Read_only_mode_lists_the_file_names_without_a_drop_zone()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        Assert.Empty(cut.FindAll(".edit-file-drop-zone"));
        Assert.Contains("report.pdf", cut.Find(".edit-file-list--readonly").TextContent);
    }

    // Reads a selected file with NO size argument — a buffered file must ignore the framework's
    // 500 KB default, so a bare OpenReadStream() always succeeds regardless of size.
    static string ReadAll(IBrowserFile f)
    {
        using var s = f.OpenReadStream();
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    [Fact]
    public void Selected_files_are_buffered_into_memory_and_stay_readable()
    {
        // H1: the control buffers each file's bytes at pick time (BufferedBrowserFile) rather than
        // holding the framework IBrowserFile, whose OpenReadStream dies once Blazor wipes the file map.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello world", "a.txt"));

        Assert.NotNull(changed);
        var stored = Assert.Single(changed);
        Assert.Equal("BufferedBrowserFile", stored.GetType().Name); // not the framework's transient file
        Assert.Equal("a.txt", stored.Name);
        Assert.Equal("hello world", ReadAll(stored));               // bare OpenReadStream() works
    }

    [Fact]
    public void Files_from_multiple_selection_batches_all_survive_and_stay_readable()
    {
        // The core H1 regression: pick one file, then pick another. Both must remain in the list AND
        // both must still be readable. (bUnit can't reproduce the browser file-map wipe that breaks an
        // un-buffered earlier batch, but this proves the buffering that defeats it — batch 1 is read
        // back after batch 2 arrived.)
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => { changed = v; model.Files = v; });

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("first", "a.txt"));
        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("second", "b.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
        Assert.Equal(["a.txt", "b.txt"], changed.Select(f => f.Name));
        Assert.Equal("first", ReadAll(changed[0]));  // earlier batch still readable after the later one
        Assert.Equal("second", ReadAll(changed[1]));
    }

    [Fact]
    public void Buffered_file_reads_without_a_size_argument_even_above_the_500KB_framework_default()
    {
        // A 600 KB file: the framework's bare OpenReadStream() (512,000-byte default) would throw.
        // The buffered copy serves the whole thing because the bytes are already in memory.
        var big = new string('x', 600 * 1024);
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, maxFileSizeBytes: 1024 * 1024);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(big, "big.txt"));

        Assert.NotNull(changed);
        Assert.Equal(big.Length, ReadAll(Assert.Single(changed)).Length);
    }

    [Fact]
    public void AllowedExtensions_without_a_leading_dot_still_match_and_emit_a_valid_accept()
    {
        // Path.GetExtension always returns the dot, so a consumer's bare "pdf" used to silently
        // reject every file and emit an invalid accept attribute.
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["pdf", ".txt"]);

        Assert.Equal(".pdf,.txt", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count);
    }

    [Fact]
    public void At_the_MaxFiles_cap_the_label_stops_pointing_at_the_unmounted_input()
    {
        // The <InputFile> (which carries the control id) unmounts at the cap — the label's `for`
        // must drop with it rather than dangle at a missing id.
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, maxFiles: 1);
        Assert.True(cut.Find("label.edit-label").HasAttribute("for")); // labelable while under the cap

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Empty(cut.FindAll("input[type=file]"));
        Assert.False(cut.Find("label.edit-label").HasAttribute("for"));
    }

    [Fact]
    public void Read_only_file_list_is_labelled_by_the_field_label()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "report.pdf"));

        var model = new FileModel { Files = uploaded! };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "IsEditMode", false);
            b.CloseComponent();
        }));

        var list = cut.Find(".edit-file-list--readonly");
        Assert.Equal($"lbl-{list.GetAttribute("id")}", list.GetAttribute("aria-labelledby"));
    }

    [Fact]
    public void Toggle_on_a_null_bound_checked_list_creates_the_list()
    {
        // Same base-class fix, exercised through EditCheckedStringList.ToggleAsync.
        var model = new PersonModel { Tags = null! };
        Expression<Func<List<string>>> field = () => model.Tags;
        List<string>? changed = null;
        var cut = Render(builder =>
        {
            builder.OpenComponent<EditForm>(0);
            builder.AddAttribute(1, "Model", model);
            builder.AddAttribute(2, "ChildContent", (RenderFragment<EditContext>)(_ => b =>
            {
                b.OpenComponent<EditCheckedStringList>(0);
                b.AddAttribute(1, "Value", model.Tags);
                b.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<List<string>>(this, v => changed = v));
                b.AddAttribute(3, "ValueExpression", field);
                b.AddAttribute(4, "Options", new List<string> { "a", "b" });
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        cut.FindAll("input[type=checkbox]")[0].Change(true);

        Assert.NotNull(changed);
        Assert.Equal(["a"], changed);
    }
}
