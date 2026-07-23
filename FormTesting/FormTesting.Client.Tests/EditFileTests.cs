using System.IO;
using System.Linq.Expressions;
using System.Reflection;
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
        long? maxTotalBytes = null,   // nullable so a test can pass 0 explicitly (0 = unlimited) vs. leave the 100 MB default
        EditFileVariant? variant = null,
        string? buttonText = null,
        Func<IBrowserFile, Task<bool>>? beforeAdd = null,
        string? beforeAddRejectedMessageFormat = null)
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
            if (variant is not null)
                b.AddAttribute(9, "Variant", variant.Value);
            if (buttonText is not null)
                b.AddAttribute(10, "ButtonText", buttonText);
            if (beforeAdd is not null)
                b.AddAttribute(11, "BeforeAdd", beforeAdd);
            if (beforeAddRejectedMessageFormat is not null)
                b.AddAttribute(12, "BeforeAddRejectedMessageFormat", beforeAddRejectedMessageFormat);
            b.CloseComponent();
        }));
    }

    [Fact]
    public void Consumer_class_is_forwarded_to_the_drop_zone()
    {
        // EditControlListBase captures the unmatched class attribute; EditFile must actually render
        // it (ac15622 only wired up EditMultiSelect — the other list controls silently swallowed it).
        var model = new FileModel { Files = [] };
        Expression<Func<List<IBrowserFile>>> field = () => model.Files;
        var cut = Render(WithForm(model, b =>
        {
            b.OpenComponent<EditFile>(0);
            b.AddAttribute(1, "Value", model.Files);
            b.AddAttribute(2, "ValueExpression", field);
            b.AddAttribute(3, "class", "my-upload-class");
            b.CloseComponent();
        }));

        Assert.Contains("my-upload-class", cut.Find(".edit-file-drop-zone").ClassList);
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

    // ------------------------------------------------------------------------------------------
    // Accept tokens: MIME types and MIME wildcards (AntD/native `accept` parity), alongside the
    // pre-existing bare/dotted extension shape covered above.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Full_MIME_type_accept_tokens_are_not_dot_prefixed_and_match_by_ContentType()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["application/pdf"]);

        // The old extension-only normalizer would have turned this into the meaningless ".application/pdf".
        Assert.Equal("application/pdf", cut.Find("input[type=file]").GetAttribute("accept"));

        // Matches by ContentType, not extension -- a mismatched extension is irrelevant for a MIME token.
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "report.dat", contentType: "application/pdf"));
        Assert.NotNull(changed);
        Assert.Single(changed);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("2", "other.pdf", contentType: "application/xml"));
        Assert.Single(changed); // still 1 -- the second file's ContentType doesn't match the token
        Assert.Contains("other.pdf", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void MIME_wildcard_accept_tokens_match_any_subtype_case_insensitively()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: ["image/*"]);

        Assert.Equal("image/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.bin", contentType: "IMAGE/PNG"), // case-insensitive
            InputFileContent.CreateFromText("2", "b.bin", contentType: "image/jpeg"),
            InputFileContent.CreateFromText("3", "c.bin", contentType: "application/pdf"));

        Assert.NotNull(changed);
        Assert.Equal(2, changed.Count); // the two image/* files, not the pdf
        var message = cut.Find(".edit-validation-message").TextContent;
        Assert.Contains("c.bin", message);
        Assert.Contains("image/*", message); // human-readable token list in the rejection message
    }

    [Fact]
    public void Extension_and_MIME_wildcard_accept_tokens_combine_in_one_AllowedExtensions_list()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, allowedExtensions: [".pdf", "image/*"]);

        Assert.Equal(".pdf,image/*", cut.Find("input[type=file]").GetAttribute("accept"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.pdf", contentType: "application/pdf"), // extension match
            InputFileContent.CreateFromText("2", "b.dat", contentType: "image/png"),        // MIME wildcard match
            InputFileContent.CreateFromText("3", "c.dat", contentType: "text/plain"));      // matches neither

        Assert.NotNull(changed);
        Assert.Equal(["a.pdf", "b.dat"], changed.Select(f => f.Name));
        Assert.Contains("c.dat", cut.Find(".edit-validation-message").TextContent);
    }

    // ------------------------------------------------------------------------------------------
    // BeforeAdd: async per-file gate between the built-in checks and buffering.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void BeforeAdd_returning_true_lets_the_file_through()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, beforeAdd: _ => Task.FromResult(true));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
    }

    [Fact]
    public void BeforeAdd_returning_false_rejects_the_file_with_the_default_message()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v,
            beforeAdd: f => Task.FromResult(f.Name != "blocked.txt"));

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "ok.txt"),
            InputFileContent.CreateFromText("2", "blocked.txt"));

        Assert.NotNull(changed);
        Assert.Equal(["ok.txt"], changed.Select(f => f.Name));
        Assert.Contains("blocked.txt was rejected.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void BeforeAdd_rejection_message_is_localizable()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v,
            beforeAdd: _ => Task.FromResult(false),
            beforeAddRejectedMessageFormat: "{0} wurde abgelehnt.");

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Contains("a.txt wurde abgelehnt.", cut.Find(".edit-validation-message").TextContent);
    }

    // Minimal hand-written IBrowserFile -- lets the exception-propagation test below call LoadFiles
    // directly (via reflection) without going through bUnit's InputFile/dispatcher plumbing, whose
    // exception-surfacing behavior for a component event handler is not something to depend on here.
    sealed class FakeBrowserFile : IBrowserFile
    {
        public string Name => "a.txt";
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public long Size => 5;
        public string ContentType => "text/plain";
        public Stream OpenReadStream(long maxAllowedSize = 512000, CancellationToken cancellationToken = default) =>
            new MemoryStream(new byte[5]);
    }

    [Fact]
    public async Task BeforeAdd_exceptions_propagate_instead_of_being_swallowed_as_a_rejection()
    {
        // A throwing hook is a bug in the consumer's code, not a file rejection -- LoadFiles must not
        // catch it and turn it into an upload-error message like every other rejection path is.
        // Invoked directly (reflection) rather than through bUnit's UploadFiles/dispatcher plumbing,
        // whose exception-surfacing behavior for a faulted component event handler isn't something to
        // depend on for this assertion -- this calls the exact method under test and awaits its Task.
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v,
            beforeAdd: _ => throw new InvalidOperationException("boom"));

        var editFile = cut.FindComponent<EditFile>().Instance;
        var loadFiles = typeof(EditFile).GetMethod("LoadFiles", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var args = new InputFileChangeEventArgs([new FakeBrowserFile()]);

        var task = (Task)loadFiles.Invoke(editFile, [args])!;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => task);

        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public void BeforeAdd_does_not_run_for_a_file_rejected_by_the_extension_filter()
    {
        // Format rejection happens before BeforeAdd -- prove the hook never sees a rejected file by
        // making it throw if it's ever invoked (the upload must complete normally, hook untouched).
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model,
            allowedExtensions: [".txt"],
            beforeAdd: _ => throw new InvalidOperationException("BeforeAdd must not run for a rejected file."));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.pdf"));

        Assert.Contains("a.pdf", cut.Find(".edit-validation-message").TextContent);
        Assert.Empty(cut.FindAll(".edit-file-item"));
    }

    [Fact]
    public void BeforeAdd_does_not_run_for_a_duplicate_file()
    {
        // Duplicate rejection also happens before BeforeAdd. Seed the list with a file already
        // selected (as an earlier batch would), then re-pick the same file -- it must be caught by
        // the duplicate check and never reach the always-throwing hook.
        var seed = InputFileContent.CreateFromText("hi", "b.txt");
        var seeded = new FileModel { Files = [] };
        var seedCut = RenderEditFile(seeded, v => seeded.Files = v);
        seedCut.FindComponent<InputFile>().UploadFiles(seed);

        var model = new FileModel { Files = seeded.Files };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v,
            beforeAdd: _ => throw new InvalidOperationException("BeforeAdd must not run for a duplicate file."));

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hi", "b.txt"));

        Assert.Contains("b.txt is already added.", cut.Find(".edit-validation-message").TextContent);
        Assert.Null(changed); // ValueChanged never fires -- nothing new was added
    }

    // ------------------------------------------------------------------------------------------
    // File size in the list rows (edit-mode and read-only).
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Edit_mode_list_shows_each_files_formatted_size()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(new string('x', 2048), "a.txt"));

        Assert.Equal("2 KB", cut.Find(".edit-file-size").TextContent);
    }

    [Fact]
    public void Read_only_list_also_shows_each_files_formatted_size()
    {
        List<IBrowserFile>? uploaded = null;
        var upload = RenderEditFile(new FileModel { Files = [] }, v => uploaded = v);
        upload.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("hello", "report.pdf"));

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

        var item = cut.Find(".edit-file-list--readonly .edit-file-item");
        Assert.Contains("report.pdf", item.TextContent);
        Assert.Equal("5 B", cut.Find(".edit-file-list--readonly .edit-file-size").TextContent);
    }

    [Fact]
    public void Empty_state_DOM_is_unchanged_by_the_size_display_and_Button_variant_additions()
    {
        // No files, default (unset) Variant -- none of this batch's new markup should appear.
        var cut = RenderEditFile(new FileModel { Files = [] });

        Assert.Single(cut.FindAll(".edit-file-drop-zone"));
        Assert.Empty(cut.FindAll(".edit-file-list"));
        Assert.Empty(cut.FindAll(".edit-file-size"));
        Assert.Empty(cut.FindAll(".edit-file-select-btn"));
    }

    // ------------------------------------------------------------------------------------------
    // Variant="Button": compact plain-button picker, same validation/caps, no dropzone.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Button_variant_renders_a_button_not_a_drop_zone()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button);

        Assert.Empty(cut.FindAll(".edit-file-drop-zone"));
        var btn = cut.Find(".edit-file-select-btn");
        Assert.Equal("Select Files", btn.TextContent.Trim());
        Assert.NotNull(cut.Find(".edit-file-select-btn input[type=file]"));
    }

    [Fact]
    public void Button_variant_ButtonText_is_overridable()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button, buttonText: "Upload Documents");

        Assert.Equal("Upload Documents", cut.Find(".edit-file-select-btn").TextContent.Trim());
    }

    [Fact]
    public void Button_variant_applies_the_same_validation_as_the_drop_zone()
    {
        var model = new FileModel { Files = [] };
        List<IBrowserFile>? changed = null;
        var cut = RenderEditFile(model, v => changed = v, variant: EditFileVariant.Button, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("1", "a.txt"),
            InputFileContent.CreateFromText("2", "b.txt"));

        Assert.NotNull(changed);
        Assert.Single(changed);
        Assert.Contains("Only 1 file allowed — 1 not added.", cut.Find(".edit-validation-message").TextContent);
    }

    [Fact]
    public void Button_variant_disables_its_input_when_IsDisabled()
    {
        var cut = RenderEditFile(new FileModel { Files = [] }, variant: EditFileVariant.Button, isDisabled: true);

        Assert.True(cut.Find("input[type=file]").HasAttribute("disabled"));
        Assert.Contains("disabled", cut.Find(".edit-file-select-btn").ClassList);
    }

    [Fact]
    public void Button_variant_unmounts_its_input_at_the_MaxFiles_cap_like_the_drop_zone()
    {
        var model = new FileModel { Files = [] };
        var cut = RenderEditFile(model, v => model.Files = v, variant: EditFileVariant.Button, maxFiles: 1);

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText("1", "a.txt"));

        Assert.Empty(cut.FindAll("input[type=file]"));
        Assert.Empty(cut.FindAll(".edit-file-select-btn"));
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
